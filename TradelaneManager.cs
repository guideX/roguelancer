using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer
{
    /// <summary>
    /// Loads and coordinates the existing trade-lane routes. Player and NPC
    /// travelers share each route while retaining explicit direction and
    /// independent transit state.
    /// </summary>
    public class TradelaneManager
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;
        private readonly List<TradeLane> _tradeLanes = new();
        private readonly List<TradelaneConfig> _allConfigs = new();
        private readonly Dictionary<NpcShip, float> _npcEntryCooldowns = new();
        private readonly HashSet<NpcShip> _subscribedNpcs = new();

        private List<NpcShip> _npcShips = new();
        private Ship _subscribedPlayerShip;
        private TradeLane _activeLane;
        private KeyboardState _prevKeys;
        private TradelaneRing _nearbyRing;
        private TradeLane _nearbyLane;
        private bool _isAutoOrienting;
        private TradelaneRing _autoOrientTarget;
        private TradeLane _autoOrientLane;

        public bool IsInTransit => _activeLane?.IsActive == true;
        public int CurrentSystemIndex { get; private set; } = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public TradelaneManager(GraphicsDevice graphicsDevice, SpriteFont font)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            if (graphicsDevice != null)
            {
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
        }

        public void SetNpcShips(List<NpcShip> npcShips)
        {
            if (!ReferenceEquals(_npcShips, npcShips))
            {
                foreach (NpcShip subscribedNpc in _subscribedNpcs.ToList())
                {
                    if (npcShips == null || !npcShips.Contains(subscribedNpc))
                    {
                        subscribedNpc.CombatDamageReceived -= HandleNpcCombatDamage;
                        subscribedNpc.OnDestroyed -= HandleNpcDestroyed;
                        _subscribedNpcs.Remove(subscribedNpc);
                    }
                }
            }
            _npcShips = npcShips ?? new List<NpcShip>();
            EnsureNpcSubscriptions();
        }

        public void LoadAllConfigs()
        {
            _allConfigs.Clear();
            string directory = Path.Combine("Configuration", "tradelanes");
            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"[TRADELANES] Directory not found: {directory}");
                return;
            }

            foreach (string file in Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    TradelaneConfig config = JsonSerializer.Deserialize<TradelaneConfig>(File.ReadAllText(file), JsonOptions);
                    if (config == null)
                        continue;
                    _allConfigs.Add(config);
                    Console.WriteLine($"[TRADELANES] Loaded: {config.Name} (System {config.SystemIndex})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TRADELANES] Error loading {file}: {ex.Message}");
                }
            }

            Console.WriteLine($"[TRADELANES] Total configs loaded: {_allConfigs.Count}");
        }

        public void LoadTradelanesForSystem(int systemIndex)
        {
            ResetTransientState();
            _tradeLanes.Clear();
            CurrentSystemIndex = systemIndex;

            foreach (TradelaneConfig config in _allConfigs)
            {
                if (config == null || config.SystemIndex != systemIndex)
                    continue;

                try
                {
                    TradeLane lane = new(_graphicsDevice, config);
                    _tradeLanes.Add(lane);
                    Console.WriteLine($"[TRADELANES] Created tradelane: {config.Name} with {lane.Rings.Count} rings");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TRADELANES] Error creating tradelane '{config.Name}': {ex.Message}");
                }
            }

            Console.WriteLine($"[TRADELANES] Active tradelanes in system {systemIndex}: {_tradeLanes.Count}");
        }

        public void LoadContent(ContentManager content)
        {
            if (content == null)
                return;

            foreach (TradeLane lane in _tradeLanes)
            {
                string modelPath = string.IsNullOrWhiteSpace(lane.Config.ModelPath)
                    ? "BASES/TRACK_RING/TRACK_RING"
                    : lane.Config.ModelPath;
                try
                {
                    lane.SetModel(content.Load<Model>(modelPath));
                    Console.WriteLine($"[TRADELANES] Model loaded for: {lane.Config.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TRADELANES] Error loading model '{modelPath}': {ex.Message}");
                }
            }
        }

        public List<TradeLane> GetTradeLanes() => _tradeLanes;

        public List<SpaceObject> GetTradelaneSpaceObjects()
        {
            var objects = new List<SpaceObject>();
            foreach (TradeLane lane in _tradeLanes)
                objects.AddRange(lane.GetRingsAsSpaceObjects());
            return objects;
        }

        /// <summary>
        /// Main simulation hook. The three-argument overload remains for
        /// existing callers and uses the NPC list previously supplied.
        /// </summary>
        public void Update(GameTime gameTime, Ship playerShip, KeyboardState keyboardState)
        {
            Update(gameTime, playerShip, _npcShips, keyboardState);
        }

        public void Update(GameTime gameTime, Ship playerShip, IReadOnlyList<NpcShip> npcShips, KeyboardState keyboardState)
        {
            if (gameTime == null)
                return;

            if (npcShips != null && !ReferenceEquals(_npcShips, npcShips))
                SetNpcShips(npcShips as List<NpcShip> ?? npcShips.ToList());
            EnsurePlayerSubscription(playerShip);
            EnsureNpcSubscriptions();

            float deltaTime = TradeLaneStateSanitizer.Elapsed((float)gameTime.ElapsedGameTime.TotalSeconds);
            TickNpcEntryCooldowns(deltaTime);

            foreach (TradeLane lane in _tradeLanes)
                lane.Update(gameTime);

            ProcessTransitEvents(playerShip);
            ApplyActiveTransitPositions(playerShip);

            _nearbyRing = null;
            _nearbyLane = null;
            if (!IsInTransit && playerShip != null && !playerShip.IsTradeLaneTransit)
            {
                foreach (TradeLane lane in _tradeLanes)
                {
                    TradelaneRing nearby = lane.GetNearbyEntryRing(playerShip.Position);
                    if (nearby == null)
                        continue;
                    _nearbyRing = nearby;
                    _nearbyLane = lane;
                    break;
                }
            }

            _isAutoOrienting = false;
            _autoOrientTarget = null;
            _autoOrientLane = null;

            if (_nearbyRing != null && _nearbyLane != null &&
                keyboardState.IsKeyDown(Keys.F5) && _prevKeys.IsKeyUp(Keys.F5))
            {
                TryEnterPlayer(_nearbyLane, _nearbyRing, playerShip);
            }

            TryEnterNpcTraffic();

            if (IsInTransit && _activeLane != null && playerShip != null)
            {
                ApplyPlayerTransitPosition(_activeLane, playerShip);
                if (keyboardState.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape))
                {
                    EjectTraveler(playerShip, TradeLaneTransitExitReason.Manual);
                }
            }

            _prevKeys = keyboardState;
        }

        private void ApplyActiveTransitPositions(Ship playerShip)
        {
            if (_activeLane != null && _activeLane.IsActive && playerShip != null)
                ApplyPlayerTransitPosition(_activeLane, playerShip);

            foreach (TradeLane lane in _tradeLanes)
            {
                foreach (TradeLaneTransitSnapshot snapshot in lane.GetActiveTransitSnapshots())
                {
                if (snapshot?.Traveler is not NpcShip npc || npc.IsDestroyed)
                        continue;
                    npc.Position = snapshot.Position;
                    npc.Velocity = snapshot.Velocity;
                    npc.SetFacing(snapshot.Velocity);
                    npc.SetTradeLaneTransit(true, lane.LaneId, snapshot.Direction, snapshot.CurrentRingIndex);
                }
            }
        }

        private void ApplyPlayerTransitPosition(TradeLane lane, Ship playerShip)
        {
            if (lane == null || playerShip == null || !lane.IsActive)
                return;
            playerShip.Position = lane.GetCurrentTransitPosition();
            playerShip.Velocity = lane.GetTravelForward() * lane.GetTransitSpeed();
            playerShip.SetFacing(lane.GetTravelForward());
            playerShip.SetTradeLaneTransit(true);
        }

        private void ProcessTransitEvents(Ship playerShip)
        {
            foreach (TradeLane lane in _tradeLanes)
            {
                foreach (TradeLaneTransitEvent transitEvent in lane.DrainTransitEvents())
                {
                    if (transitEvent == null)
                        continue;

                    if (transitEvent.Traveler is Ship ship && ReferenceEquals(ship, playerShip))
                    {
                        ship.Position = transitEvent.Position;
                        ship.Velocity = transitEvent.IsCompleted ? Vector3.Zero : transitEvent.Velocity;
                        ship.SetTradeLaneTransit(false);
                        if (ReferenceEquals(_activeLane, lane))
                            _activeLane = null;
                        Console.WriteLine(transitEvent.IsCompleted
                            ? $"[TRADELANES] Player exited tradelane: {lane.Config.Name}"
                            : $"[TRADELANES] Player left tradelane: {lane.Config.Name} ({transitEvent.Reason})");
                    }
                    else if (transitEvent.Traveler is NpcShip npc)
                    {
                        npc.Position = transitEvent.Position;
                        npc.Velocity = transitEvent.IsCompleted ? GetNpcExitVelocity(npc, transitEvent) : transitEvent.Velocity;
                        npc.SetTradeLaneTransit(false, null, null, -1);
                        _npcEntryCooldowns[npc] = transitEvent.IsCompleted ? 1.5f : 3f;
                        if (transitEvent.Reason != TradeLaneTransitExitReason.Completed)
                            npc.ClearEncounterState();
                    }
                }
            }
        }

        private static Vector3 GetNpcExitVelocity(NpcShip npc, TradeLaneTransitEvent transitEvent)
        {
            if (npc == null || !TradeLaneStateSanitizer.IsFinite(transitEvent.Velocity))
                return Vector3.Zero;
            float speed = MathHelper.Clamp(Math.Max(40f, npc.TrafficCruiseSpeed), 40f, 500f);
            Vector3 direction = transitEvent.Velocity.LengthSquared() > 0.0001f
                ? Vector3.Normalize(transitEvent.Velocity)
                : Vector3.Forward;
            return direction * speed;
        }

        private void TryEnterNpcTraffic()
        {
            if (_npcShips == null)
                return;

            for (int i = 0; i < _npcShips.Count; i++)
            {
                NpcShip npc = _npcShips[i];
                if (npc == null || npc.IsDestroyed || npc.IsTradeLaneTransit || npc.IsTrafficEngaged ||
                    npc.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute ||
                    !npc.TrafficRouteStart.HasValue || !npc.TrafficRouteEnd.HasValue ||
                    (_npcEntryCooldowns.TryGetValue(npc, out float cooldown) && cooldown > 0f))
                    continue;

                TryEnterNpcTraffic(npc);
            }
        }

        public bool TryEnterNpcTraffic(NpcShip npc)
        {
            if (npc == null || npc.IsDestroyed || npc.IsTradeLaneTransit || npc.IsTrafficEngaged)
                return false;
            if (!npc.TrafficRouteStart.HasValue || !npc.TrafficRouteEnd.HasValue)
                return false;

            foreach (TradeLane lane in _tradeLanes)
            {
                if (!TryGetMatchingDirection(lane, npc, out TradeLaneDirection direction))
                    continue;

                TradelaneRing entry = lane.GetEntryRing(direction);
                // Existing route spawns can be offset from the configured ring
                // endpoint. Give NPCs a generous approach envelope while still
                // requiring a real route match and entry availability.
                float entryRange = Math.Max(1500f, TradeLaneStateSanitizer.Positive(lane.Config.DockingRange, 600f));
                if (Vector3.Distance(npc.Position, entry.Position) > entryRange ||
                    !lane.IsEntryAvailable(direction, entry) ||
                    HasSameDirectionSpacingConflict(lane, direction, entry))
                    continue;

                if (!lane.StartTravelForNpc(npc, entry))
                    continue;

                Vector3 position = lane.GetCurrentTransitPosition(npc);
                npc.Position = position;
                npc.Velocity = lane.GetTravelForward(direction) * lane.GetTransitSpeed();
                npc.SetFacing(lane.GetTravelForward(direction));
                npc.SetTradeLaneTransit(true, lane.LaneId, direction, direction == TradeLaneDirection.Forward ? 0 : lane.ReverseRings.Count - 1);
                return true;
            }

            return false;
        }

        private bool TryGetMatchingDirection(TradeLane lane, NpcShip npc, out TradeLaneDirection direction)
        {
            direction = default;
            if (lane == null || npc?.TrafficRouteStart == null || npc.TrafficRouteEnd == null)
                return false;

            Vector3 routeStart = npc.TrafficRouteStart.Value;
            Vector3 routeEnd = npc.TrafficRouteEnd.Value;
            float tolerance = Math.Max(2500f, lane.Config.RingSpacing * 2.5f);
            bool forwardMatch = Vector3.Distance(routeStart, lane.Config.StartPosition) <= tolerance &&
                Vector3.Distance(routeEnd, lane.Config.EndPosition) <= tolerance;
            bool reverseMatch = Vector3.Distance(routeStart, lane.Config.EndPosition) <= tolerance &&
                Vector3.Distance(routeEnd, lane.Config.StartPosition) <= tolerance;
            if (!forwardMatch && !reverseMatch)
                return false;

            float fromStart = Vector3.DistanceSquared(npc.Position, lane.Config.StartPosition);
            float fromEnd = Vector3.DistanceSquared(npc.Position, lane.Config.EndPosition);
            if (fromStart <= fromEnd && forwardMatch)
                direction = TradeLaneDirection.Forward;
            else if (reverseMatch)
                direction = TradeLaneDirection.Reverse;
            else if (forwardMatch)
                direction = TradeLaneDirection.Forward;
            else
                return false;
            return true;
        }

        private static bool HasSameDirectionSpacingConflict(TradeLane lane, TradeLaneDirection direction, TradelaneRing entry)
        {
            Vector3 entryPosition = lane.GetRingTravelPosition(entry, direction);
            foreach (TradeLaneTransitSnapshot snapshot in lane.GetActiveTransitSnapshots())
            {
                if (snapshot == null || snapshot.Direction != direction)
                    continue;
                if (Vector3.Distance(snapshot.Position, entryPosition) < TradeLane.MinimumFollowingDistance)
                    return true;
            }
            return false;
        }

        private void EnsurePlayerSubscription(Ship playerShip)
        {
            if (ReferenceEquals(_subscribedPlayerShip, playerShip))
                return;
            if (_subscribedPlayerShip != null)
                _subscribedPlayerShip.CombatDamageReceived -= HandlePlayerCombatDamage;
            _subscribedPlayerShip = playerShip;
            if (_subscribedPlayerShip != null)
                _subscribedPlayerShip.CombatDamageReceived += HandlePlayerCombatDamage;
        }

        private void EnsureNpcSubscriptions()
        {
            if (_npcShips == null)
                return;
            for (int i = 0; i < _npcShips.Count; i++)
            {
                NpcShip npc = _npcShips[i];
                if (npc == null || !_subscribedNpcs.Add(npc))
                    continue;
                npc.CombatDamageReceived += HandleNpcCombatDamage;
                npc.OnDestroyed += HandleNpcDestroyed;
            }
        }

        private void HandlePlayerCombatDamage(float damage, bool hostile)
        {
            if (hostile && damage > 0f && _subscribedPlayerShip != null)
                EjectTraveler(_subscribedPlayerShip, TradeLaneTransitExitReason.HostileDamage);
        }

        private void HandleNpcCombatDamage(NpcShip npc, float damage, NpcDestructionSource source, bool hostile)
        {
            if (npc != null && hostile && damage > 0f)
                EjectTraveler(npc, TradeLaneTransitExitReason.HostileDamage);
        }

        private void HandleNpcDestroyed(NpcShip npc)
        {
            if (npc != null)
                EjectTraveler(npc, TradeLaneTransitExitReason.Destroyed);
        }

        private void EjectTraveler(object traveler, TradeLaneTransitExitReason reason)
        {
            foreach (TradeLane lane in _tradeLanes)
            {
                if (lane.EjectTraveler(traveler, reason))
                {
                    ProcessTransitEvents(traveler as Ship);
                    return;
                }
            }
        }

        private void TickNpcEntryCooldowns(float deltaTime)
        {
            if (_npcEntryCooldowns.Count == 0)
                return;
            foreach (NpcShip npc in _npcEntryCooldowns.Keys.ToList())
                _npcEntryCooldowns[npc] = Math.Max(0f, _npcEntryCooldowns[npc] - deltaTime);
        }

        private bool TryEnterPlayer(TradeLane lane, TradelaneRing entryRing, Ship playerShip)
        {
            if (lane == null || playerShip == null || IsInTransit || playerShip.IsTradeLaneTransit || entryRing == null)
                return false;

            float distance = Vector3.Distance(playerShip.Position, entryRing.Position);
            float range = Math.Max(TradeLaneStateSanitizer.Positive(lane.Config.ActivationRange, 200f),
                TradeLaneStateSanitizer.Positive(lane.Config.DockingRange, 600f));
            if (distance > range)
                return false;
            if (!lane.StartTravel(entryRing, playerShip))
                return false;

            _activeLane = lane;
            playerShip.CancelCruise(CruiseCancellationReason.IncompatibleFlight);
            playerShip.StopAfterburner();
            playerShip.SetTradeLaneTransit(true);
            playerShip.SetFacing(lane.GetTravelForward());
            ApplyPlayerTransitPosition(lane, playerShip);
            Console.WriteLine($"[TRADELANES] Player entered tradelane: {lane.Config.Name} direction={lane.ActiveDirection}");
            return true;
        }

        public bool TryEnterTradelaneAt(TradelaneRing entryRing, Ship playerShip)
        {
            if (entryRing == null || playerShip == null || IsInTransit || playerShip.IsTradeLaneTransit)
                return false;
            foreach (TradeLane lane in _tradeLanes)
            {
                if ((lane.GetEntryRing(TradeLaneDirection.Forward) != entryRing) &&
                    (lane.GetEntryRing(TradeLaneDirection.Reverse) != entryRing))
                    continue;
                return TryEnterPlayer(lane, entryRing, playerShip);
            }
            return false;
        }

        public bool IsEntryRingReachable(TradelaneRing entryRing, Vector3 fromPosition)
        {
            if (entryRing == null || !TradeLaneStateSanitizer.IsFinite(fromPosition))
                return false;
            foreach (TradeLane lane in _tradeLanes)
            {
                if (lane.GetEntryRing(TradeLaneDirection.Forward) == entryRing)
                    return lane.IsEntryAvailable(TradeLaneDirection.Forward, entryRing);
                if (lane.GetEntryRing(TradeLaneDirection.Reverse) == entryRing)
                    return lane.IsEntryAvailable(TradeLaneDirection.Reverse, entryRing);
            }
            return false;
        }

        public bool TryDisruptSegment(string laneId, int ringIndex, TradeLaneDisruptionSource source, string sourceName = null)
        {
            TradeLane lane = FindLane(laneId);
            return lane != null && lane.TryDisruptSegment(ringIndex, source, sourceName);
        }

        public bool TryApplyRingDamage(string laneId, int ringIndex, float damage, bool hostile, TradeLaneDisruptionSource source, string sourceName = null)
        {
            TradeLane lane = FindLane(laneId);
            return lane != null && lane.ApplyRingDamage(ringIndex, damage, hostile, source, sourceName);
        }

        public TradeLane FindLane(string laneId)
        {
            if (string.IsNullOrWhiteSpace(laneId))
                return null;
            return _tradeLanes.FirstOrDefault(lane => string.Equals(lane.LaneId, laneId, StringComparison.OrdinalIgnoreCase));
        }

        public void ResetTransientState()
        {
            foreach (TradeLane lane in _tradeLanes)
                lane.ResetTransientState();
            if (_subscribedPlayerShip != null)
            {
                _subscribedPlayerShip.SetTradeLaneTransit(false);
                _subscribedPlayerShip.CombatDamageReceived -= HandlePlayerCombatDamage;
                _subscribedPlayerShip = null;
            }
            foreach (NpcShip npc in _subscribedNpcs)
            {
                if (npc == null)
                    continue;
                npc.CombatDamageReceived -= HandleNpcCombatDamage;
                npc.OnDestroyed -= HandleNpcDestroyed;
            }
            _subscribedNpcs.Clear();
            foreach (NpcShip npc in _npcShips)
                npc?.SetTradeLaneTransit(false, null, null, -1);
            _activeLane = null;
            _npcEntryCooldowns.Clear();
            _nearbyRing = null;
            _nearbyLane = null;
            _prevKeys = default;
        }

        public void SetLanesForTesting(IEnumerable<TradeLane> lanes)
        {
            ResetTransientState();
            _tradeLanes.Clear();
            if (lanes != null)
                _tradeLanes.AddRange(lanes.Where(lane => lane != null));
        }

        public void Draw3D(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            foreach (TradeLane lane in _tradeLanes)
                lane.Draw(view, projection, lightDirection);
        }

        public void DrawEnergyEffects(Matrix view, Matrix projection)
        {
            foreach (TradeLane lane in _tradeLanes)
                lane.DrawEnergyEffects(view, projection);
        }

        public void DrawHUD(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null || _pixel == null || _font == null || _graphicsDevice == null)
                return;

            if (IsInTransit && _activeLane != null)
            {
                int width = _graphicsDevice.Viewport.Width;
                string direction = _activeLane.ActiveDirection == TradeLaneDirection.Reverse ? "B → A" : "A → B";
                string statusText = $"TRADELANE: {_activeLane.Config.Name} [{direction}]";
                Vector2 size = _font.MeasureString(statusText);
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, 40), new Color(0, 50, 100) * 0.7f);
                spriteBatch.Draw(_pixel, new Rectangle(0, 38, width, 2), _activeLane.Config.RingColor);
                spriteBatch.DrawString(_font, statusText, new Vector2((width - size.X) / 2f, 10f), Color.White);

                int progressWidth = 300;
                int progressX = (width - progressWidth) / 2;
                float progress = _activeLane.TransitProgress;
                spriteBatch.Draw(_pixel, new Rectangle(progressX, 45, progressWidth, 6), Color.DarkGray * 0.5f);
                spriteBatch.Draw(_pixel, new Rectangle(progressX, 45, (int)(progressWidth * progress), 6), _activeLane.Config.RingColor);
                return;
            }

            if (_nearbyRing == null || _nearbyLane == null)
                return;
            int w = _graphicsDevice.Viewport.Width;
            string directionLabel = _nearbyRing.Direction == TradelaneRing.RingDirection.Forward ? "forward" : "reverse";
            string prompt = $"Press F5 to enter {_nearbyLane.Config.Name} ({directionLabel})";
            Vector2 promptSize = _font.MeasureString(prompt);
            Rectangle box = new((w - (int)promptSize.X - 40) / 2, _graphicsDevice.Viewport.Height / 2 + 100,
                (int)promptSize.X + 40, (int)promptSize.Y + 20);
            spriteBatch.Draw(_pixel, box, Color.Black * 0.7f);
            spriteBatch.DrawString(_font, prompt, new Vector2(box.X + 20, box.Y + 10), Color.White);
        }
    }

}
