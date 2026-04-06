using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Full autopilot for the G (GOTO) command.
    /// Computes an optimal route: tradelanes ? jumpholes ? final approach ? docking.
    /// Handles smooth speed management, collision avoidance, and real-time re-routing.
    /// </summary>
    public class GotoAutopilot
    {
        // ?????????????????????????????????????????????????????????????????
        //  Route node types
        // ?????????????????????????????????????????????????????????????????
        public enum NodeType
        {
            FlyTo,        // plain free-space flight
            TradelaneEntry,
            TradelaneTravel,
            JumpholeApproach,
            JumpholeEnter,
            DockingApproach,
            Docked
        }

        private class RouteNode
        {
            public NodeType Type;
            public Vector3 Position;          // world-space goal
            public SpaceObject Reference;     // jump hole / tradelane ring / station
            public float ArrivalRadius;       // how close is "arrived"
        }

        // ?????????????????????????????????????????????????????????????????
        //  Autopilot state
        // ?????????????????????????????????????????????????????????????????
        public enum AutopilotState
        {
            Inactive,
            Planning,
            Executing,
            Docked,
            Cancelled
        }

        private AutopilotState _state = AutopilotState.Inactive;
        private List<RouteNode> _route = new();
        private int _nodeIndex = 0;

        // external references (set via Initialize)
        private Ship _ship;
        private TradelaneManager _tradelaneManager;
        private JumpHoleManager _jumpHoleManager;
        private List<Station> _stations;
        private List<SpaceObject> _obstacles;    // all space objects for avoidance
        private List<NpcShip> _npcShips;
        private NotificationManager _notifications;

        // final destination
        private SpaceObject _destination;

        // collision avoidance
        private Vector3 _avoidanceOffset = Vector3.Zero;
        private float _avoidanceCooldown = 0f;
        private const float AvoidanceScanInterval = 0.2f;  // seconds between scans
        private float _avoidanceScanTimer = 0f;

        // smooth steering
        private const float SteeringRate = 1.2f;           // rad/s turn rate while in autopilot (reduced for smoother turns)
        private const float MinSteeringRate = 0.3f;        // minimum turn rate when close to target
        private const float AlignmentThreshold = 0.98f;    // dot product threshold for "aligned"
        private const float CruiseDropDistance = 1500f;    // exit cruise inside this range
        private const float TradelaneActivationRange = 450f;
        private const float JumpholeActivationRange = 180f;
        private const float DockingApproachRange = 1200f;
        private const float DockingFinalRange = 150f;

        // nav-arrow HUD
        private Texture2D _pixel;
        private SpriteFont _font;
        private GraphicsDevice _graphicsDevice;

        // re-route request
        private float _rerouteTimer = 0f;
        private const float RerouteInterval = 5f;

        public bool IsActive => _state == AutopilotState.Executing;
        public bool IsDocked => _state == AutopilotState.Docked;
        public SpaceObject Destination => _destination;
        public AutopilotState State => _state;
        public string CurrentNodeDescription => _route.Count > 0 && _nodeIndex < _route.Count
            ? _route[_nodeIndex].Type.ToString()
            : "None";

        public event Action OnDockingComplete;
        public event Action<int, string> OnJumpholeTransit;   // systemIndex, jhName

        // ?????????????????????????????????????????????????????????????????
        //  Initialization
        // ?????????????????????????????????????????????????????????????????
        public void Initialize(
            Ship ship,
            TradelaneManager tradelaneManager,
            JumpHoleManager jumpHoleManager,
            List<Station> stations,
            List<SpaceObject> allSpaceObjects,
            List<NpcShip> npcShips,
            NotificationManager notifications,
            GraphicsDevice graphicsDevice,
            SpriteFont font)
        {
            _ship = ship;
            _tradelaneManager = tradelaneManager;
            _jumpHoleManager = jumpHoleManager;
            _stations = stations;
            _obstacles = allSpaceObjects;
            _npcShips = npcShips;
            _notifications = notifications;
            _graphicsDevice = graphicsDevice;
            _font = font;

            if (graphicsDevice != null)
            {
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
        }

        // ?????????????????????????????????????????????????????????????????
        //  Public API
        // ?????????????????????????????????????????????????????????????????

        /// <summary>Activate GOTO to the given target.</summary>
        public void Activate(SpaceObject target)
        {
            if (target == null) return;
            _destination = target;
            _state = AutopilotState.Planning;
            _avoidanceOffset = Vector3.Zero;
            _nodeIndex = 0;
            _rerouteTimer = 0f;
            BuildRoute();
        }

        /// <summary>Cancel autopilot and return control to the player.</summary>
        public void Cancel()
        {
            if (_state == AutopilotState.Inactive) return;
            _state = AutopilotState.Cancelled;
            _route.Clear();
            _nodeIndex = 0;
            _destination = null;
            _avoidanceOffset = Vector3.Zero;
            Console.WriteLine("[AUTOPILOT] Cancelled.");
        }

        // ?????????????????????????????????????????????????????????????????
        //  Update (called every frame from Ship.UpdateGoto or Game.Update)
        // ?????????????????????????????????????????????????????????????????
        public void Update(float deltaTime)
        {
            if (_state != AutopilotState.Executing) return;
            if (_ship == null || _destination == null)
            {
                Cancel();
                return;
            }

            // Periodic re-route (target might have moved or lane broken)
            _rerouteTimer += deltaTime;
            if (_rerouteTimer >= RerouteInterval)
            {
                _rerouteTimer = 0f;
                RebuildRouteIfNeeded();
            }

            if (_nodeIndex >= _route.Count)
            {
                FinishRoute();
                return;
            }

            // Collision avoidance scan
            _avoidanceScanTimer += deltaTime;
            if (_avoidanceScanTimer >= AvoidanceScanInterval)
            {
                _avoidanceScanTimer = 0f;
                ComputeAvoidanceOffset();
            }
            if (_avoidanceCooldown > 0f) _avoidanceCooldown -= deltaTime;

            RouteNode node = _route[_nodeIndex];

            switch (node.Type)
            {
                case NodeType.FlyTo:
                    ExecuteFlyTo(deltaTime, node);
                    break;
                case NodeType.TradelaneEntry:
                    ExecuteTradelaneEntry(deltaTime, node);
                    break;
                case NodeType.TradelaneTravel:
                    ExecuteTradelaneTravel(deltaTime);
                    break;
                case NodeType.JumpholeApproach:
                    ExecuteJumpholeApproach(deltaTime, node);
                    break;
                case NodeType.JumpholeEnter:
                    ExecuteJumpholeEnter(deltaTime, node);
                    break;
                case NodeType.DockingApproach:
                    ExecuteDockingApproach(deltaTime, node);
                    break;
            }
        }

        // ?????????????????????????????????????????????????????????????????
        //  Route building
        // ?????????????????????????????????????????????????????????????????
        private void BuildRoute()
        {
            _route.Clear();
            _nodeIndex = 0;

            if (_ship == null || _destination == null)
            {
                _state = AutopilotState.Cancelled;
                return;
            }

            Vector3 start = _ship.Position;
            Vector3 end = _destination.Position;

            // 1. Check if a tradelane can save travel time
            TryAddTradelaneNodes(start, end);

            // 2. Check for jump holes if no beneficial tradelane was added
            //    (cross-system not yet supported beyond single jump)
            if (_route.Count == 0)
                TryAddJumpholeNodes(start, end);

            // 3. Determine if destination is a dockable station
            bool isDockable = _destination is Station || IsStationTarget(_destination);

            // 4. Final leg: plain FlyTo or Docking approach
            if (isDockable)
            {
                // Approach waypoint (stand-off distance)
                Vector3 approachOffset = Vector3.Normalize(start - end);
                if (approachOffset.LengthSquared() < 0.001f) approachOffset = Vector3.Forward;
                Vector3 approachPos = end + approachOffset * DockingApproachRange;

                _route.Add(new RouteNode
                {
                    Type = NodeType.DockingApproach,
                    Position = end,
                    Reference = _destination,
                    ArrivalRadius = DockingFinalRange
                });
            }
            else
            {
                float arrivalRadius = Math.Max(150f, _destination.Radius + 200f);
                _route.Add(new RouteNode
                {
                    Type = NodeType.FlyTo,
                    Position = end,
                    Reference = _destination,
                    ArrivalRadius = arrivalRadius
                });
            }

            _state = AutopilotState.Executing;
            _notifications?.ShowMessage($"AUTOPILOT: Routing to {_destination.Name}");
            Console.WriteLine($"[AUTOPILOT] Route built: {_route.Count} nodes to '{_destination.Name}'");
            foreach (var n in _route)
                Console.WriteLine($"  [{n.Type}] pos={n.Position:F0} arrR={n.ArrivalRadius:F0}");
        }

        private void TryAddTradelaneNodes(Vector3 start, Vector3 end)
        {
            if (_tradelaneManager == null) return;

            var lanes = _tradelaneManager.GetTradeLanes();
            if (lanes == null || lanes.Count == 0) return;

            // Find the lane whose midpoint is roughly between us and the target, and
            // whose travel direction reduces total distance.
            float bestSaving = 500f;    // must save at least this many units
            TradeLane bestLane = null;
            TradelaneRing bestEntry = null;

            foreach (var lane in lanes)
            {
                if (lane.IsBroken) continue;

                // Check both directions
                CheckLaneDirection(start, end, lane, lane.ForwardRings, ref bestSaving, ref bestLane, ref bestEntry);
                CheckLaneDirection(start, end, lane, lane.ReverseRings, ref bestSaving, ref bestLane, ref bestEntry);
            }

            if (bestLane == null || bestEntry == null) return;

            // Add: FlyTo entry ring, then TradelaneEntry, then TradelaneTravel
            _route.Add(new RouteNode
            {
                Type = NodeType.FlyTo,
                Position = bestEntry.Position,
                Reference = bestEntry,
                ArrivalRadius = TradelaneActivationRange
            });

            _route.Add(new RouteNode
            {
                Type = NodeType.TradelaneEntry,
                Position = bestEntry.Position,
                Reference = bestEntry,
                ArrivalRadius = TradelaneActivationRange
            });

            _route.Add(new RouteNode
            {
                Type = NodeType.TradelaneTravel,
                Position = Vector3.Zero,    // managed by TradelaneManager
                Reference = null,
                ArrivalRadius = 0f
            });

            Console.WriteLine($"[AUTOPILOT] Tradelane '{bestLane.Config.Name}' selected (entry ring: {bestEntry.Name})");
        }

        private void CheckLaneDirection(
            Vector3 start, Vector3 end,
            TradeLane lane,
            List<TradelaneRing> rings,
            ref float bestSaving, ref TradeLane bestLane, ref TradelaneRing bestEntry)
        {
            if (rings == null || rings.Count < 2) return;

            TradelaneRing entryRing = rings[0];
            TradelaneRing exitRing = rings[rings.Count - 1];

            // Naïve travel time comparison (straight-line distances)
            float directDist = Vector3.Distance(start, end);
            float viaLane = Vector3.Distance(start, entryRing.Position)
                          + Vector3.Distance(entryRing.Position, exitRing.Position) / (lane.Config.TravelSpeed / lane.Config.TravelSpeed) // normalised
                          + Vector3.Distance(exitRing.Position, end);

            // Correct for travel-speed ratio: time via lane
            float shipSpeed = _ship.CruiseSpeed;
            float timeViaLane = Vector3.Distance(start, entryRing.Position) / shipSpeed
                              + Vector3.Distance(entryRing.Position, exitRing.Position) / lane.Config.TravelSpeed
                              + Vector3.Distance(exitRing.Position, end) / shipSpeed;
            float timeDirect = directDist / shipSpeed;

            float saving = timeDirect - timeViaLane;
            if (saving > bestSaving && !entryRing.IsDestroyed)
            {
                bestSaving = saving;
                bestLane = lane;
                bestEntry = entryRing;
            }
        }

        private void TryAddJumpholeNodes(Vector3 start, Vector3 end)
        {
            if (_jumpHoleManager == null) return;

            var jumpHoles = _jumpHoleManager.GetJumpHoles();
            if (jumpHoles == null || jumpHoles.Count == 0) return;

            // Find the nearest jumphole to the destination that connects us closer
            JumpHole best = null;
            float bestDist = float.MaxValue;

            foreach (var jh in jumpHoles)
            {
                float d = Vector3.Distance(start, jh.Position);
                // Only consider jump holes closer than the destination (would be a shortcut)
                if (d < bestDist && d < Vector3.Distance(start, end) * 0.8f)
                {
                    bestDist = d;
                    best = jh;
                }
            }

            if (best == null) return;

            // Approach node (fly to outside the jumphole)
            Vector3 approachDir = Vector3.Normalize(best.Position - start);
            if (approachDir.LengthSquared() < 0.001f) approachDir = Vector3.Forward;
            Vector3 approachPos = best.Position - approachDir * (best.Config.Radius * 2f);

            _route.Add(new RouteNode
            {
                Type = NodeType.JumpholeApproach,
                Position = approachPos,
                Reference = best,
                ArrivalRadius = best.Config.Radius * 1.5f
            });

            _route.Add(new RouteNode
            {
                Type = NodeType.JumpholeEnter,
                Position = best.Position,
                Reference = best,
                ArrivalRadius = best.Config.ActivationRange
            });

            Console.WriteLine($"[AUTOPILOT] Jump hole '{best.Name}' selected.");
        }

        private bool IsStationTarget(SpaceObject obj)
        {
            if (_stations == null) return false;
            foreach (var s in _stations)
                if (s == obj) return true;
            return false;
        }

        private void RebuildRouteIfNeeded()
        {
            if (_destination == null) return;

            // If in TradelaneTravel, wait for it to complete naturally
            if (_nodeIndex < _route.Count && _route[_nodeIndex].Type == NodeType.TradelaneTravel)
                return;

            // Check if the current node's reference was destroyed (ring broken etc.)
            if (_nodeIndex < _route.Count)
            {
                var node = _route[_nodeIndex];
                if (node.Reference is TradelaneRing ring && ring.IsDestroyed)
                {
                    Console.WriteLine("[AUTOPILOT] Tradelane ring destroyed – rebuilding route.");
                    BuildRoute();
                }
            }
        }

        // ?????????????????????????????????????????????????????????????????
        //  Node executors
        // ?????????????????????????????????????????????????????????????????
        private void ExecuteFlyTo(float deltaTime, RouteNode node)
        {
            Vector3 rawTarget = node.Position;
            Vector3 avoidedTarget = ApplyAvoidance(rawTarget);

            SteerToward(avoidedTarget, deltaTime);
            
            float dist = Vector3.Distance(_ship.Position, rawTarget);
            
            // Only manage speed when reasonably aligned to prevent erratic movement
            Vector3 toTarget = rawTarget - _ship.Position;
            if (toTarget.LengthSquared() > 0.001f)
            {
                float alignment = Vector3.Dot(_ship.Forward, Vector3.Normalize(toTarget));
                
                // Check if we're moving towards or away from target
                float approachRate = Vector3.Dot(_ship.Velocity, Vector3.Normalize(toTarget));
                
                if (alignment > 0.7f)
                {
                    ManageSpeed(dist, node.ArrivalRadius, isTrdelane: false);
                }
                else
                {
                    // Slow down while turning
                    SetTargetSpeed(_ship.MaxSpeed * 0.5f);
                }
                
                // Extra safety: if moving away or very close, slow down drastically
                if (approachRate < 0 && dist < node.ArrivalRadius * 2f)
                {
                    SetTargetSpeed(_ship.MaxSpeed * 0.1f);
                }
            }

            if (dist <= node.ArrivalRadius)
                AdvanceNode();
        }

        private void ExecuteTradelaneEntry(float deltaTime, RouteNode node)
        {
            // Steer toward ring, approach slowly
            SteerToward(node.Position, deltaTime);
            float dist = Vector3.Distance(_ship.Position, node.Position);

            // Slow down as we close in
            float approachSpeed = MathHelper.Lerp(
                _ship.MaxSpeed * 0.4f,
                _ship.MaxSpeed,
                MathHelper.Clamp((dist - 50f) / (TradelaneActivationRange - 50f), 0f, 1f));
            SetTargetSpeed(approachSpeed);

            if (dist <= TradelaneActivationRange)
            {
                // Ask TradelaneManager to enter
                if (node.Reference is TradelaneRing entryRing)
                {
                    bool entered = _tradelaneManager?.TryEnterTradelaneAt(entryRing, _ship) ?? false;
                    if (entered)
                    {
                        Console.WriteLine($"[AUTOPILOT] Entered tradelane via ring {entryRing.Name}");
                        AdvanceNode(); // next node is TradelaneTravel
                    }
                }
                else
                {
                    AdvanceNode();
                }
            }
        }

        private void ExecuteTradelaneTravel(float deltaTime)
        {
            // TradelaneManager controls the ship position.
            // We just wait for it to complete.
            if (_tradelaneManager != null && !_tradelaneManager.IsInTransit)
            {
                Console.WriteLine("[AUTOPILOT] Tradelane travel complete.");
                AdvanceNode();
            }
        }

        private void ExecuteJumpholeApproach(float deltaTime, RouteNode node)
        {
            SteerToward(node.Position, deltaTime);
            float dist = Vector3.Distance(_ship.Position, node.Position);
            
            // Only accelerate when aligned
            float alignment = Vector3.Dot(_ship.Forward, Vector3.Normalize(node.Position - _ship.Position));
            if (alignment > 0.8f)
            {
                ManageSpeed(dist, node.ArrivalRadius, isTrdelane: false);
            }
            else
            {
                SetTargetSpeed(_ship.MaxSpeed * 0.4f);
            }

            if (dist <= node.ArrivalRadius)
                AdvanceNode();
        }

        private void ExecuteJumpholeEnter(float deltaTime, RouteNode node)
        {
            if (node.Reference is JumpHole jh)
            {
                // Align and accelerate toward center
                SteerToward(jh.Position, deltaTime);
                float dist = Vector3.Distance(_ship.Position, jh.Position);

                // Slow to appropriate entry speed
                float entrySpeed = _ship.MaxSpeed * 0.6f;
                SetTargetSpeed(entrySpeed);

                if (dist <= JumpholeActivationRange)
                {
                    bool entered = _jumpHoleManager?.TryInitiateTransitFor(jh, _ship) ?? false;
                    if (entered)
                    {
                        Console.WriteLine($"[AUTOPILOT] Entering jump hole '{jh.Name}'");
                        // OnJumpholeTransit fires when the manager completes transit
                        AdvanceNode(); // next nodes after this are in destination system
                    }
                }
            }
        }

        private void ExecuteDockingApproach(float deltaTime, RouteNode node)
        {
            Vector3 targetPos = node.Position;
            float dist = Vector3.Distance(_ship.Position, targetPos);

            // Calculate alignment for smoother approach
            float alignment = Vector3.Dot(_ship.Forward, Vector3.Normalize(targetPos - _ship.Position));

            // Phase 1: Approach waypoint
            if (dist > DockingFinalRange * 3f)
            {
                SteerToward(targetPos, deltaTime);
                
                // Only accelerate when reasonably aligned
                if (alignment > 0.7f)
                {
                    float distFactor = MathHelper.Clamp((dist - DockingFinalRange * 3f) / (DockingApproachRange * 0.5f), 0f, 1f);
                    float speed = MathHelper.Lerp(_ship.MaxSpeed * 0.2f, _ship.MaxSpeed * 0.6f, distFactor);
                    SetTargetSpeed(speed);
                }
                else
                {
                    SetTargetSpeed(_ship.MaxSpeed * 0.3f);
                }
            }
            else
            {
                // Phase 2: Slow final approach
                SteerToward(targetPos, deltaTime);
                
                // Very gentle approach speed curve
                float distFactor = MathHelper.Clamp(dist / (DockingFinalRange * 3f), 0f, 1f);
                float smoothFactor = distFactor * distFactor * (3f - 2f * distFactor);
                float speed = MathHelper.Lerp(15f, _ship.MaxSpeed * 0.2f, smoothFactor);
                SetTargetSpeed(speed);
            }

            if (dist <= node.ArrivalRadius)
            {
                // Dock
                SetTargetSpeed(0f);
                _ship.SetEnginesKilled(false);
                _state = AutopilotState.Docked;
                _notifications?.ShowMessage($"Docked at {node.Reference?.Name ?? "station"}");
                Console.WriteLine($"[AUTOPILOT] Docked at '{node.Reference?.Name}'.");
                OnDockingComplete?.Invoke();
            }
        }

        // ?????????????????????????????????????????????????????????????????
        //  Steering helpers
        // ?????????????????????????????????????????????????????????????????
        private void SteerToward(Vector3 worldTarget, float deltaTime)
        {
            Vector3 toTarget = worldTarget - _ship.Position;
            if (toTarget.LengthSquared() < 0.001f) return;

            Vector3 desired = Vector3.Normalize(toTarget);
            Vector3 current = _ship.Forward;
            float dot = MathHelper.Clamp(Vector3.Dot(current, desired), -1f, 1f);

            // Already aligned - no steering needed
            if (dot >= AlignmentThreshold) return;

            Vector3 axis = Vector3.Cross(current, desired);
            if (axis.LengthSquared() < 0.0001f) return;
            axis.Normalize();

            float angle = (float)Math.Acos(dot);
            
            // Adaptive steering rate: slower when closer to alignment
            float alignmentFactor = MathHelper.Clamp((1f - dot) / (1f - AlignmentThreshold), 0f, 1f);
            float adaptiveRate = MathHelper.Lerp(MinSteeringRate, SteeringRate, alignmentFactor);
            
            // Apply damping to prevent overshoot
            float step = Math.Min(angle * 0.5f, adaptiveRate * deltaTime);
            _ship.ApplyRotation(axis, step);
        }

        private void ManageSpeed(float distToTarget, float arrivalRadius, bool isTrdelane)
        {
            float brakeStart = arrivalRadius * 4f + 800f;  // Increased brake distance for smoother deceleration
            float cruiseDropDist = CruiseDropDistance;

            // Exit cruise if close
            if (_ship.IsCruiseActive && distToTarget < cruiseDropDist)
            {
                _ship.SetCruiseActive(false);
                _ship.SetCruiseCharging(false);
            }

            // Only engage cruise when far away and aligned
            float alignment = Vector3.Dot(_ship.Forward, Vector3.Normalize(_ship.Position + _ship.Forward * distToTarget - _ship.Position));
            if (distToTarget > cruiseDropDist * 2.0f && !_ship.IsCruiseActive && !_ship.IsCruiseCharging && alignment > 0.9f)
            {
                // Engage cruise for long legs only when well-aligned
                _ship.SetCruiseCharging(true);
            }

            float speed;
            if (_ship.IsCruiseActive)
            {
                speed = _ship.CruiseSpeed;
            }
            else if (distToTarget > brakeStart)
            {
                speed = _ship.MaxSpeed;
            }
            else
            {
                // Smoother braking curve with exponential falloff
                float distFactor = MathHelper.Clamp((distToTarget - arrivalRadius) / (brakeStart - arrivalRadius), 0f, 1f);
                // Use smoothstep for more natural deceleration
                float smoothFactor = distFactor * distFactor * (3f - 2f * distFactor);
                speed = MathHelper.Lerp(
                    _ship.MaxSpeed * 0.05f,  // Lower minimum speed
                    _ship.MaxSpeed,
                    smoothFactor);
            }

            SetTargetSpeed(speed);
        }

        private void SetTargetSpeed(float speed)
        {
            // We reach into Ship's private field via the public Speed setter path.
            // Ship.UpdateGoto already drives _targetSpeed; here we rely on the public
            // wrapper ActivateGotoTargetSpeed that we add to Ship in step 4.
            _ship.SetAutopilotTargetSpeed(speed);
        }

        // ?????????????????????????????????????????????????????????????????
        //  Collision avoidance
        // ?????????????????????????????????????????????????????????????????
        private void ComputeAvoidanceOffset()
        {
            if (_obstacles == null)
            {
                _avoidanceOffset = Vector3.Zero;
                return;
            }

            Vector3 steer = Vector3.Zero;
            float lookAhead = 400f;  // Reduced lookahead for less aggressive avoidance
            Vector3 fwd = _ship.Forward;
            Vector3 pos = _ship.Position;

            foreach (var obj in _obstacles)
            {
                if (obj == _destination) continue;
                if (obj is TradelaneRing) continue;  // rings are route waypoints

                float avoidRadius = obj.Radius + _ship.CollisionRadius + 60f;
                Vector3 toObj = obj.Position - pos;
                float distAlongFwd = Vector3.Dot(toObj, fwd);

                if (distAlongFwd <= 0 || distAlongFwd > lookAhead) continue;

                Vector3 closest = pos + fwd * distAlongFwd;
                float lateralDist = Vector3.Distance(closest, obj.Position);

                if (lateralDist < avoidRadius)
                {
                    Vector3 lateral = closest - obj.Position;
                    if (lateral.LengthSquared() < 0.001f) lateral = _ship.Up;
                    lateral.Normalize();
                    float strength = 1f - (lateralDist / avoidRadius);
                    steer += lateral * strength * 0.8f;  // Reduced strength
                }
            }

            // Also check NPC ships
            if (_npcShips != null)
            {
                foreach (var npc in _npcShips)
                {
                    if (npc.IsDestroyed) continue;
                    float avoidRadius = npc.Radius + _ship.CollisionRadius + 80f;
                    Vector3 toObj = npc.Position - pos;
                    float distAlongFwd = Vector3.Dot(toObj, fwd);
                    if (distAlongFwd <= 0 || distAlongFwd > lookAhead * 0.5f) continue;

                    Vector3 closest = pos + fwd * distAlongFwd;
                    float lateralDist = Vector3.Distance(closest, npc.Position);

                    if (lateralDist < avoidRadius)
                    {
                        Vector3 lateral = closest - npc.Position;
                        if (lateral.LengthSquared() < 0.001f) lateral = _ship.Up;
                        lateral.Normalize();
                        float strength = 1f - (lateralDist / avoidRadius);
                        steer += lateral * strength * 1.2f;  // Reduced from 2.0f
                    }
                }
            }

            // Smooth the avoidance with damping
            _avoidanceOffset = Vector3.Lerp(_avoidanceOffset, steer, 0.3f);
        }

        private Vector3 ApplyAvoidance(Vector3 target)
        {
            if (_avoidanceOffset.LengthSquared() < 0.001f) return target;

            // Push the target point laterally by the avoidance vector
            Vector3 toTarget = target - _ship.Position;
            float dist = toTarget.Length();
            if (dist < 1f) return target;

            Vector3 dir = toTarget / dist;
            Vector3 avoidDir = _avoidanceOffset;
            // Remove component along flight direction
            avoidDir -= dir * Vector3.Dot(avoidDir, dir);
            // Reduced avoidance magnitude
            avoidDir *= Math.Min(dist * 0.25f, 200f);
            return target + avoidDir;
        }

        // ?????????????????????????????????????????????????????????????????
        //  Route state machine helpers
        // ?????????????????????????????????????????????????????????????????
        private void AdvanceNode()
        {
            _nodeIndex++;
            _avoidanceOffset = Vector3.Zero;
            if (_nodeIndex >= _route.Count)
            {
                FinishRoute();
            }
            else
            {
                Console.WriteLine($"[AUTOPILOT] Advancing to node {_nodeIndex}: {_route[_nodeIndex].Type}");
            }
        }

        private void FinishRoute()
        {
            _state = AutopilotState.Inactive;
            SetTargetSpeed(0f);
            _ship.SetEnginesKilled(true);
            _notifications?.ShowMessage($"Arrived at {_destination?.Name ?? "target"}");
            Console.WriteLine($"[AUTOPILOT] Arrived at destination.");
            _destination = null;
        }

        // ?????????????????????????????????????????????????????????????????
        //  HUD rendering
        // ?????????????????????????????????????????????????????????????????
        /// <summary>
        /// Draw the navigation arrow and status bar.
        /// Call from Game.Draw inside a SpriteBatch.Begin/End block.
        /// </summary>
        public void DrawHUD(SpriteBatch spriteBatch, Matrix view, Matrix projection, Viewport viewport)
        {
            if (!IsActive || _destination == null || _pixel == null) return;

            DrawNavArrow(spriteBatch, view, projection, viewport);
            DrawStatusBar(spriteBatch, viewport);
        }

        private void DrawNavArrow(SpriteBatch spriteBatch, Matrix view, Matrix projection, Viewport viewport)
        {
            // Project destination onto screen
            Vector3 dest = _destination.Position;
            Vector4 clip = Vector4.Transform(new Vector4(dest, 1f), view * projection);

            if (clip.W <= 0) return; // behind camera

            Vector3 ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);

            float screenX = (ndc.X * 0.5f + 0.5f) * viewport.Width;
            float screenY = (1f - (ndc.Y * 0.5f + 0.5f)) * viewport.Height;
            bool onScreen = screenX >= 0 && screenX <= viewport.Width && screenY >= 0 && screenY <= viewport.Height;

            float pulse = 0.6f + 0.4f * (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 4.0);
            Color arrowColor = Color.Lerp(Color.Lime, Color.White, pulse * 0.4f);

            if (onScreen)
            {
                // Draw a small diamond marker at target location
                DrawDiamond(spriteBatch, new Vector2(screenX, screenY), 14f, arrowColor * pulse);
            }
            else
            {
                // Clamp to screen edge and draw an arrow pointing toward the target
                float cx = viewport.Width / 2f;
                float cy = viewport.Height / 2f;
                float dx = screenX - cx;
                float dy = screenY - cy;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.001f) return;
                float nx = dx / len;
                float ny = dy / len;
                float maxX = viewport.Width * 0.47f;
                float maxY = viewport.Height * 0.47f;
                float tx = MathHelper.Clamp(cx + nx * maxX, 20, viewport.Width - 20);
                float ty = MathHelper.Clamp(cy + ny * maxY, 20, viewport.Height - 20);
                DrawArrowHead(spriteBatch, new Vector2(tx, ty), (float)Math.Atan2(ny, nx), 18f, arrowColor * pulse);
            }

            // Distance label
            if (_font != null)
            {
                float distance = Vector3.Distance(_ship.Position, _destination.Position);
                string distLabel = distance > 1000f
                    ? $"{_destination.Name}  {distance / 1000f:F1}km"
                    : $"{_destination.Name}  {distance:F0}m";

                Vector2 labelPos = onScreen
                    ? new Vector2(screenX + 18f, screenY - 8f)
                    : new Vector2(viewport.Width / 2f - 60f, 60f);

                // Shadow
                spriteBatch.DrawString(_font, distLabel, labelPos + Vector2.One, Color.Black * 0.7f);
                spriteBatch.DrawString(_font, distLabel, labelPos, arrowColor);
            }
        }

        private void DrawStatusBar(SpriteBatch spriteBatch, Viewport viewport)
        {
            if (_pixel == null) return;

            int barW = 300;
            int barH = 28;
            int x = viewport.Width / 2 - barW / 2;
            int y = 10;

            spriteBatch.Draw(_pixel, new Rectangle(x, y, barW, barH), Color.Black * 0.65f);
            spriteBatch.Draw(_pixel, new Rectangle(x, y, barW, 1), Color.Lime * 0.8f);
            spriteBatch.Draw(_pixel, new Rectangle(x, y + barH - 1, barW, 1), Color.Lime * 0.8f);
            spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, barH), Color.Lime * 0.8f);
            spriteBatch.Draw(_pixel, new Rectangle(x + barW - 1, y, 1, barH), Color.Lime * 0.8f);

            if (_font != null)
            {
                string phase = CurrentNodeDescription;
                string txt = $"AUTOPILOT  [{phase}]  ? {_destination?.Name}";
                Vector2 sz = _font.MeasureString(txt);
                Vector2 pos = new Vector2(x + barW / 2f - sz.X / 2f, y + barH / 2f - sz.Y / 2f);
                spriteBatch.DrawString(_font, txt, pos + Vector2.One, Color.Black * 0.6f);
                spriteBatch.DrawString(_font, txt, pos, Color.Lime);
            }
        }

        private void DrawDiamond(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
        {
            // Simple 4-point diamond from pixel quads
            int s = (int)size;
            spriteBatch.Draw(_pixel, new Rectangle((int)center.X - 1, (int)center.Y - s, 2, s), color);
            spriteBatch.Draw(_pixel, new Rectangle((int)center.X - 1, (int)center.Y, 2, s), color);
            spriteBatch.Draw(_pixel, new Rectangle((int)center.X - s, (int)center.Y - 1, s, 2), color);
            spriteBatch.Draw(_pixel, new Rectangle((int)center.X, (int)center.Y - 1, s, 2), color);
        }

        private void DrawArrowHead(SpriteBatch spriteBatch, Vector2 tip, float angle, float size, Color color)
        {
            // Three short lines forming a > arrowhead
            int s = (int)size;
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);

            // Shaft
            var shaft = new Rectangle((int)(tip.X - cos * s), (int)(tip.Y - sin * s), s, 2);
            spriteBatch.Draw(_pixel, shaft, null, color, angle, Vector2.Zero, SpriteEffects.None, 0);

            // Upper fin
            float finAngle = angle + MathHelper.PiOver2 * 0.6f;
            var fin1 = new Rectangle((int)tip.X, (int)tip.Y, (int)(s * 0.6f), 2);
            spriteBatch.Draw(_pixel, fin1, null, color,
                angle + MathHelper.Pi + MathHelper.PiOver4 * 0.8f, Vector2.Zero, SpriteEffects.None, 0);

            // Lower fin
            var fin2 = new Rectangle((int)tip.X, (int)tip.Y, (int)(s * 0.6f), 2);
            spriteBatch.Draw(_pixel, fin2, null, color,
                angle + MathHelper.Pi - MathHelper.PiOver4 * 0.8f, Vector2.Zero, SpriteEffects.None, 0);
        }
    }
}
