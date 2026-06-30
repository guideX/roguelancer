using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// A single waypoint node along a mission guidance path
    /// </summary>
    public class MissionWaypoint
    {
        public Vector3 Position { get; set; }
        public string Label { get; set; }
        public MissionWaypointType Type { get; set; }
    }

    public enum MissionWaypointType
    {
        Direct,
        TradelaneEntry,
        TradelaneExit,
        JumpHole,
        Destination
    }

    /// <summary>
    /// Per-mission guidance data: resolved target, waypoint path, proximity state
    /// </summary>
    public class MissionGuidanceData
    {
        public Mission Mission { get; set; }
        public Vector3? ResolvedTarget { get; set; }
        public SpaceObject TargetObject { get; set; }
        public List<MissionWaypoint> Waypoints { get; set; } = new();
        public float DistanceToTarget { get; set; } = float.MaxValue;
        public bool IsNearTarget { get; set; }
        public float ProximityRadius { get; set; } = 800f;
    }

    /// <summary>
    /// Resolves mission targets to world positions, builds waypoint paths using
    /// tradelanes and jump holes, and tracks proximity to targets.
    /// </summary>
    public class MissionWaypointSystem
    {
        private Dictionary<int, MissionGuidanceData> _guidanceData = new();

        // Proximity threshold for highlighting
        private const float NearTargetRange = 2000f;
        private const float CompletionRange = 500f;

        /// <summary>
        /// All active guidance data for rendering
        /// </summary>
        public IReadOnlyDictionary<int, MissionGuidanceData> GuidanceData => _guidanceData;

        /// <summary>
        /// Register a newly accepted mission for waypoint tracking
        /// </summary>
        public void RegisterMission(Mission mission)
        {
            if (_guidanceData.ContainsKey(mission.Id)) return;

            _guidanceData[mission.Id] = new MissionGuidanceData
            {
                Mission = mission
            };
            Console.WriteLine($"[WAYPOINT] Registered mission #{mission.Id}: {mission.Description}");
        }

        /// <summary>
        /// Remove a completed/failed mission from tracking
        /// </summary>
        public void UnregisterMission(Mission mission)
        {
            if (_guidanceData.Remove(mission.Id))
            {
                mission.TargetPosition = null;
                mission.TargetSpaceObject = null;
                Console.WriteLine($"[WAYPOINT] Unregistered mission #{mission.Id}");
            }
        }

        /// <summary>
        /// Update all tracked missions: resolve targets, build paths, check proximity
        /// </summary>
        public void Update(
            Vector3 playerPosition,
            List<SpaceObject> spaceObjects,
            List<NpcShip> npcShips,
            List<TradeLane> tradeLanes,
            List<JumpHole> jumpHoles)
        {
            foreach (var kvp in _guidanceData)
            {
                var data = kvp.Value;
                var mission = data.Mission;

                if (mission.Status != MissionStatus.Active) continue;

                // Step 1: Resolve target position if not yet resolved
                if (data.ResolvedTarget == null)
                {
                    ResolveTarget(data, spaceObjects, npcShips);
                }

                // Update mission object's cached position
                mission.TargetPosition = data.ResolvedTarget;
                mission.TargetSpaceObject = data.TargetObject;

                // For bounty missions, track the NPC's moving position
                if (mission.Type == MissionType.Bounty && data.TargetObject != null)
                {
                    data.ResolvedTarget = data.TargetObject.Position;
                    mission.TargetPosition = data.ResolvedTarget;
                }

                if (data.ResolvedTarget == null) continue;

                // Step 2: Update distance and proximity
                data.DistanceToTarget = Vector3.Distance(playerPosition, data.ResolvedTarget.Value);
                data.IsNearTarget = data.DistanceToTarget <= NearTargetRange;

                // Step 3: Build/update waypoint path
                BuildWaypointPath(data, playerPosition, tradeLanes);
            }
        }

        /// <summary>
        /// Resolve a mission's target/destination to a world position
        /// </summary>
        private void ResolveTarget(MissionGuidanceData data, List<SpaceObject> spaceObjects, List<NpcShip> npcShips)
        {
            var mission = data.Mission;

            switch (mission.Type)
            {
                case MissionType.Delivery:
                case MissionType.Escort:
                    // Find station/space object matching destination name
                    var destObj = FindSpaceObjectByName(spaceObjects, mission.Destination);
                    if (destObj != null)
                    {
                        data.ResolvedTarget = destObj.Position;
                        data.TargetObject = destObj;
                        Console.WriteLine($"[WAYPOINT] Resolved delivery target '{mission.Destination}' -> {destObj.Name} at {destObj.Position}");
                    }
                    break;

                case MissionType.Bounty:
                    // Find NPC matching bounty target name
                    var targetNpc = npcShips.FirstOrDefault(n =>
                        !n.IsDestroyed && n.Name.Contains(mission.Target, StringComparison.OrdinalIgnoreCase));
                    if (targetNpc != null)
                    {
                        data.ResolvedTarget = targetNpc.Position;
                        data.TargetObject = targetNpc;
                        Console.WriteLine($"[WAYPOINT] Resolved bounty target '{mission.Target}' -> {targetNpc.Name} at {targetNpc.Position}");
                    }
                    else
                    {
                        // Bounty target not found as NPC - use destination hint from "Last seen near ..."
                        string locationHint = (mission.Destination ?? string.Empty).Replace("Last seen near ", string.Empty);
                        var nearObj = FindSpaceObjectByName(spaceObjects, locationHint);
                        if (nearObj != null)
                        {
                            data.ResolvedTarget = nearObj.Position;
                            data.TargetObject = nearObj;
                            Console.WriteLine($"[WAYPOINT] Resolved bounty area '{locationHint}' -> {nearObj.Name}");
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Find a space object whose name contains the search string
        /// </summary>
        private SpaceObject FindSpaceObjectByName(List<SpaceObject> objects, string searchName)
        {
            if (string.IsNullOrEmpty(searchName)) return null;

            // Try exact match first
            var exact = objects.FirstOrDefault(o =>
                o.Name.Equals(searchName, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Try contains match
            return objects.FirstOrDefault(o =>
                o.Name.Contains(searchName, StringComparison.OrdinalIgnoreCase) ||
                searchName.Contains(o.Name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Build a waypoint path from player to target using tradelanes when beneficial
        /// </summary>
        private void BuildWaypointPath(MissionGuidanceData data, Vector3 playerPos, List<TradeLane> tradeLanes)
        {
            data.Waypoints.Clear();

            if (data.ResolvedTarget == null) return;
            Vector3 targetPos = data.ResolvedTarget.Value;

            float directDistance = Vector3.Distance(playerPos, targetPos);

            // Check if any tradelane can shorten the path
            TradeLane bestLane = null;
            float bestSaving = 0f;
            Vector3 bestEntry = Vector3.Zero;
            Vector3 bestExit = Vector3.Zero;

            foreach (var lane in tradeLanes)
            {
                Vector3 laneStart = lane.Config.StartPosition;
                Vector3 laneEnd = lane.Config.EndPosition;

                // Check both directions
                CheckTradelaneDirection(playerPos, targetPos, directDistance, laneStart, laneEnd, lane,
                    ref bestLane, ref bestSaving, ref bestEntry, ref bestExit);
                CheckTradelaneDirection(playerPos, targetPos, directDistance, laneEnd, laneStart, lane,
                    ref bestLane, ref bestSaving, ref bestEntry, ref bestExit);
            }

            if (bestLane != null && bestSaving > 500f)
            {
                // Path: Player -> Tradelane Entry -> Tradelane Exit -> Target
                data.Waypoints.Add(new MissionWaypoint
                {
                    Position = bestEntry,
                    Label = $"Tradelane: {bestLane.Config.Name}",
                    Type = MissionWaypointType.TradelaneEntry
                });
                data.Waypoints.Add(new MissionWaypoint
                {
                    Position = bestExit,
                    Label = "Exit Tradelane",
                    Type = MissionWaypointType.TradelaneExit
                });
            }

            // Final waypoint: destination
            data.Waypoints.Add(new MissionWaypoint
            {
                Position = targetPos,
                Label = data.Mission.Type == MissionType.Bounty ? data.Mission.Target : data.Mission.Destination,
                Type = MissionWaypointType.Destination
            });
        }

        private void CheckTradelaneDirection(
            Vector3 playerPos, Vector3 targetPos, float directDistance,
            Vector3 entry, Vector3 exit, TradeLane lane,
            ref TradeLane bestLane, ref float bestSaving,
            ref Vector3 bestEntry, ref Vector3 bestExit)
        {
            float distPlayerToEntry = Vector3.Distance(playerPos, entry);
            float distExitToTarget = Vector3.Distance(exit, targetPos);

            // Estimate tradelane travel as much faster (effectively shorter distance)
            float laneLength = Vector3.Distance(entry, exit);
            float laneEffectiveDistance = laneLength * 0.15f; // Tradelane is ~6x faster

            float totalViaLane = distPlayerToEntry + laneEffectiveDistance + distExitToTarget;
            float saving = directDistance - totalViaLane;

            if (saving > bestSaving)
            {
                bestSaving = saving;
                bestLane = lane;
                bestEntry = entry;
                bestExit = exit;
            }
        }
    }
}
