using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Small living-world manager that spawns and retires ambient traffic around configured zones.
    /// </summary>
    public sealed class TrafficManager
    {
        private sealed class TrafficZoneRuntime
        {
            public TrafficZoneConfig Zone { get; set; }
            public List<NpcShip> ActiveShips { get; } = new();
            public float SpawnTimer { get; set; }
            public int SpawnSerial { get; set; }
        }

        private sealed class TrafficShipRuntime
        {
            public string ZoneId { get; set; } = string.Empty;
            public float CombatHoldTimer { get; set; }
        }

        private const float TraderFleeHoldSeconds = 8f;
        private const float PirateAttackHoldSeconds = 12f;
        private const float PatrolInterceptHoldSeconds = 10f;

        private readonly ConfigurationManager _config;
        private readonly List<NpcShip> _npcShips;
        private readonly List<SpaceObject> _spaceObjects;
        private readonly Random _random = new();
        private readonly Dictionary<string, TrafficZoneRuntime> _zonesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<NpcShip, TrafficShipRuntime> _shipRuntimes = new();
        private readonly Action<NpcShip> _onNpcDestroyed;
        private ContentManager _content;

        public TrafficManager(ConfigurationManager config, List<NpcShip> npcShips, List<SpaceObject> spaceObjects, Action<NpcShip> onNpcDestroyed = null, ContentManager content = null)
        {
            _config = config ?? new ConfigurationManager();
            _npcShips = npcShips ?? new List<NpcShip>();
            _spaceObjects = spaceObjects ?? new List<SpaceObject>();
            _onNpcDestroyed = onNpcDestroyed;
            _content = content;
        }

        public IReadOnlyList<TrafficZoneConfig> LoadedZones => _zonesById.Values.Select(runtime => runtime.Zone).ToList();

        public IReadOnlyList<NpcShip> GetActiveShipsForZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId) || !_zonesById.TryGetValue(zoneId, out TrafficZoneRuntime runtime))
            {
                return Array.Empty<NpcShip>();
            }

            return runtime.ActiveShips.ToList();
        }

        public int GetActiveShipCount(string zoneId)
        {
            return GetActiveShipsForZone(zoneId).Count;
        }

        public void SetContent(ContentManager content)
        {
            _content = content;
        }

        public void LoadZonesForSystem(int systemIndex, Action<string> log = null)
        {
            ClearTrackedTraffic(log);
            _zonesById.Clear();

            List<TrafficZoneConfig> zones = _config.GetTrafficZonesForSystem(systemIndex);
            foreach (TrafficZoneConfig zone in zones)
            {
                if (zone == null || string.IsNullOrWhiteSpace(zone.Id) || string.IsNullOrWhiteSpace(zone.ShipDescription))
                {
                    log?.Invoke("[TRAFFIC] ERROR: Invalid zone config skipped.");
                    continue;
                }

                TrafficZoneRuntime runtime = new TrafficZoneRuntime
                {
                    Zone = zone,
                    SpawnTimer = Math.Max(0f, zone.SpawnInterval)
                };
                _zonesById[zone.Id] = runtime;
                log?.Invoke($"[TRAFFIC] Zone loaded: {zone.Name} ({zone.BehaviorType})");
            }

            foreach (TrafficZoneRuntime runtime in _zonesById.Values)
            {
                EnsureMinimumPopulation(runtime, log);
            }

            log?.Invoke($"[TRAFFIC] Loaded {_zonesById.Count} zones for system {systemIndex}");
        }

        public void Update(GameTime gameTime, Ship playerShip, ReputationManager reputationManager, Action<string> log = null)
        {
            if (gameTime == null || _zonesById.Count == 0)
            {
                return;
            }

            float deltaTime = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);

            UpdateTrafficInteractions(playerShip, reputationManager, log, deltaTime);

            foreach (TrafficZoneRuntime runtime in _zonesById.Values)
            {
                if (runtime.Zone == null)
                {
                    continue;
                }

                CleanupRuntime(runtime, log);
                DespawnInactiveTraffic(runtime, playerShip, reputationManager, log);
                SpawnTrafficIfNeeded(runtime, log, deltaTime);
            }
        }

        public void NotifyNpcDestroyed(NpcShip destroyedShip)
        {
            if (destroyedShip == null)
            {
                return;
            }

            if (_shipRuntimes.TryGetValue(destroyedShip, out TrafficShipRuntime shipRuntime))
            {
                if (!string.IsNullOrWhiteSpace(shipRuntime.ZoneId) && _zonesById.TryGetValue(shipRuntime.ZoneId, out TrafficZoneRuntime zoneRuntime))
                {
                    zoneRuntime.ActiveShips.Remove(destroyedShip);
                }

                _shipRuntimes.Remove(destroyedShip);
            }
        }

        private void SpawnTrafficIfNeeded(TrafficZoneRuntime runtime, Action<string> log, float deltaTime)
        {
            if (runtime.Zone == null)
            {
                return;
            }

            int targetMin = Math.Max(0, runtime.Zone.MinShips);
            int targetMax = Math.Max(targetMin, runtime.Zone.MaxShips);

            while (runtime.ActiveShips.Count < targetMin)
            {
                if (!TrySpawnTraffic(runtime, log))
                {
                    return;
                }
            }

            runtime.SpawnTimer = Math.Max(0f, runtime.SpawnTimer - deltaTime);
            if (runtime.ActiveShips.Count >= targetMax || runtime.SpawnTimer > 0f)
            {
                return;
            }

            if (TrySpawnTraffic(runtime, log))
            {
                runtime.SpawnTimer = Math.Max(1f, runtime.Zone.SpawnInterval);
            }
        }

        private void EnsureMinimumPopulation(TrafficZoneRuntime runtime, Action<string> log)
        {
            if (runtime.Zone == null)
            {
                return;
            }

            int targetMin = Math.Max(0, runtime.Zone.MinShips);
            while (runtime.ActiveShips.Count < targetMin)
            {
                if (!TrySpawnTraffic(runtime, log))
                {
                    break;
                }
            }
        }

        private void CleanupRuntime(TrafficZoneRuntime runtime, Action<string> log)
        {
            for (int i = runtime.ActiveShips.Count - 1; i >= 0; i--)
            {
                NpcShip ship = runtime.ActiveShips[i];
                if (ship == null || ship.IsDestroyed || !_npcShips.Contains(ship))
                {
                    ReleaseShip(runtime, ship, log, "stale");
                }
            }
        }

        private void UpdateTrafficInteractions(Ship playerShip, ReputationManager reputationManager, Action<string> log, float deltaTime)
        {
            if (_npcShips.Count == 0)
            {
                return;
            }

            List<NpcShip> traderShips = new();
            List<NpcShip> pirateShips = new();
            List<NpcShip> patrolShips = new();

            for (int i = 0; i < _npcShips.Count; i++)
            {
                NpcShip ship = _npcShips[i];
                if (ship == null || ship.IsDestroyed)
                {
                    continue;
                }

                if (ship.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute)
                {
                    traderShips.Add(ship);
                }
                else if (ship.TrafficBehavior == TrafficZoneBehaviorType.PirateAmbush)
                {
                    pirateShips.Add(ship);
                }
                else if (ship.TrafficBehavior == TrafficZoneBehaviorType.LawfulPatrol)
                {
                    patrolShips.Add(ship);
                }
            }

            UpdatePirateEngagements(pirateShips, traderShips, playerShip, reputationManager, log, deltaTime);
            UpdateTraderEscapes(traderShips, pirateShips, log, deltaTime);
            UpdatePatrolIntercepts(patrolShips, pirateShips, log, deltaTime);
        }

        private void UpdateTrafficInteractions(TrafficZoneRuntime runtime, Ship playerShip, ReputationManager reputationManager, Action<string> log, float deltaTime)
        {
            if (runtime?.Zone == null)
            {
                return;
            }

            UpdateTrafficInteractions(playerShip, reputationManager, log, deltaTime);
        }

        private void UpdatePirateEngagements(
            List<NpcShip> pirateShips,
            List<NpcShip> traderShips,
            Ship playerShip,
            ReputationManager reputationManager,
            Action<string> log,
            float deltaTime)
        {
            foreach (NpcShip pirate in pirateShips)
            {
                if (pirate == null || pirate.IsDestroyed)
                {
                    continue;
                }

                if (!TryGetTrafficRuntime(pirate, out TrafficShipRuntime pirateRuntime, out TrafficZoneRuntime pirateZoneRuntime))
                {
                    continue;
                }

                float pirateRange = Math.Max(6500f, pirateZoneRuntime.Zone.Radius * 1.1f);
                float pirateRangeSq = pirateRange * pirateRange;

                NpcShip traderTarget = FindNearestTrafficTarget(pirate, traderShips, pirateRangeSq);
                bool canAttackPlayer = playerShip != null && (reputationManager == null || reputationManager.IsHostile(pirate.FactionId));
                float playerDistanceSq = canAttackPlayer ? Vector3.DistanceSquared(pirate.Position, playerShip.Position) : float.MaxValue;
                bool playerInRange = playerDistanceSq <= pirateRangeSq;

                bool shouldTargetTrader = traderTarget != null && (!playerInRange || Vector3.DistanceSquared(pirate.Position, traderTarget.Position) <= playerDistanceSq);
                if (playerInRange && canAttackPlayer && !shouldTargetTrader)
                {
                    SetEncounterState(pirate, TrafficEncounterState.AttackingPlayer, playerShip.Position, null, log,
                        $"[TRAFFIC] Pirate ambush started: {pirate.Name} targeting player.");
                    RefreshHold(pirateRuntime, PirateAttackHoldSeconds);
                    continue;
                }

                if (traderTarget != null)
                {
                    Vector3 escapePosition = GetTraderEscapePosition(pirateZoneRuntime.Zone, traderTarget, pirate.Position);
                    SetEncounterState(traderTarget, TrafficEncounterState.Fleeing, pirate.Position, escapePosition, log,
                        $"[TRAFFIC] Trader under attack: {traderTarget.Name} fleeing {pirate.Name}.");
                    if (TryGetTrafficRuntime(traderTarget, out TrafficShipRuntime traderRuntime, out TrafficZoneRuntime traderZoneRuntime))
                    {
                        RefreshHold(traderRuntime, TraderFleeHoldSeconds);
                    }

                    SetEncounterState(pirate, TrafficEncounterState.AttackingTrader, traderTarget.Position, null, log,
                        $"[TRAFFIC] Pirate ambush started: {pirate.Name} targeting trader {traderTarget.Name}.");
                    RefreshHold(pirateRuntime, PirateAttackHoldSeconds);
                    continue;
                }

                TickEncounterHold(pirateRuntime, pirate, deltaTime, log, $"[TRAFFIC] Pirate broke off pursuit: {pirate.Name}.");
            }
        }

        private void UpdateTraderEscapes(
            List<NpcShip> traderShips,
            List<NpcShip> pirateShips,
            Action<string> log,
            float deltaTime)
        {
            foreach (NpcShip trader in traderShips)
            {
                if (trader == null || trader.IsDestroyed)
                {
                    continue;
                }

                if (!TryGetTrafficRuntime(trader, out TrafficShipRuntime traderRuntime, out TrafficZoneRuntime traderZoneRuntime))
                {
                    continue;
                }

                float traderThreatRange = Math.Max(5000f, traderZoneRuntime.Zone.Radius * 1.0f);
                float traderThreatRangeSq = traderThreatRange * traderThreatRange;

                NpcShip nearestPirate = FindNearestTrafficTarget(trader, pirateShips, traderThreatRangeSq);
                if (nearestPirate != null)
                {
                    Vector3 escapePosition = GetTraderEscapePosition(traderZoneRuntime.Zone, trader, nearestPirate.Position);
                    SetEncounterState(trader, TrafficEncounterState.Fleeing, nearestPirate.Position, escapePosition, log,
                        $"[TRAFFIC] Trader under attack: {trader.Name} fleeing {nearestPirate.Name}.");
                    RefreshHold(traderRuntime, TraderFleeHoldSeconds);
                    continue;
                }

                if (trader.EncounterState == TrafficEncounterState.Fleeing)
                {
                    TickEncounterHold(traderRuntime, trader, deltaTime, log, $"[TRAFFIC] Trader escaped: {trader.Name}.");
                }
            }
        }

        private void UpdatePatrolIntercepts(
            List<NpcShip> patrolShips,
            List<NpcShip> pirateShips,
            Action<string> log,
            float deltaTime)
        {
            foreach (NpcShip patrol in patrolShips)
            {
                if (patrol == null || patrol.IsDestroyed)
                {
                    continue;
                }

                if (!TryGetTrafficRuntime(patrol, out TrafficShipRuntime patrolRuntime, out TrafficZoneRuntime patrolZoneRuntime))
                {
                    continue;
                }

                float interceptRange = Math.Max(7000f, patrolZoneRuntime.Zone.Radius * 1.15f);
                float interceptRangeSq = interceptRange * interceptRange;

                NpcShip pirateTarget = FindNearestTrafficTarget(patrol, pirateShips, interceptRangeSq);
                if (pirateTarget != null)
                {
                    SetEncounterState(patrol, TrafficEncounterState.InterceptingPirate, pirateTarget.Position, null, log,
                        $"[TRAFFIC] Police engaging pirate: {patrol.Name} intercepting {pirateTarget.Name}.");
                    RefreshHold(patrolRuntime, PatrolInterceptHoldSeconds);
                    continue;
                }

                if (patrol.EncounterState == TrafficEncounterState.InterceptingPirate)
                {
                    TickEncounterHold(patrolRuntime, patrol, deltaTime, log, $"[TRAFFIC] Police disengaged: {patrol.Name}.");
                }
            }
        }

        private static NpcShip FindNearestTrafficTarget(NpcShip source, IReadOnlyList<NpcShip> candidates, float maxDistanceSq)
        {
            if (source == null || candidates == null || candidates.Count == 0)
            {
                return null;
            }

            NpcShip nearest = null;
            float nearestDistanceSq = maxDistanceSq;

            for (int i = 0; i < candidates.Count; i++)
            {
                NpcShip candidate = candidates[i];
                if (candidate == null || candidate == source || candidate.IsDestroyed)
                {
                    continue;
                }

                float distanceSq = Vector3.DistanceSquared(source.Position, candidate.Position);
                if (distanceSq <= nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static Vector3 GetTraderEscapePosition(TrafficZoneConfig zone, NpcShip trader, Vector3 attackerPosition)
        {
            if (zone?.RouteStart.HasValue == true && zone.RouteEnd.HasValue)
            {
                Vector3 routeStart = zone.RouteStart.Value;
                Vector3 routeEnd = zone.RouteEnd.Value;
                return Vector3.DistanceSquared(attackerPosition, routeStart) >= Vector3.DistanceSquared(attackerPosition, routeEnd)
                    ? routeStart
                    : routeEnd;
            }

            Vector3 awayDirection = trader != null ? trader.Position - attackerPosition : Vector3.Zero;
            if (awayDirection.LengthSquared() < 0.0001f)
            {
                awayDirection = Vector3.Forward;
            }
            else
            {
                awayDirection.Normalize();
            }

            Vector3 zoneCenter = zone != null ? zone.Center : Vector3.Zero;
            float escapeDistance = Math.Max(2000f, zone?.Radius > 0f ? zone.Radius : 2000f);
            return zoneCenter + awayDirection * escapeDistance;
        }

        private void SetEncounterState(NpcShip ship, TrafficEncounterState state, Vector3? targetPosition, Vector3? escapePosition, Action<string> log, string message)
        {
            if (ship == null || ship.IsDestroyed)
            {
                return;
            }

            TrafficEncounterState previousState = ship.EncounterState;
            ship.SetEncounterState(state, targetPosition, escapePosition);
            if (previousState != state && !string.IsNullOrWhiteSpace(message))
            {
                log?.Invoke(message);
            }
        }

        private bool TryGetTrafficRuntime(NpcShip ship, out TrafficShipRuntime shipRuntime, out TrafficZoneRuntime zoneRuntime)
        {
            shipRuntime = null;
            zoneRuntime = null;

            if (ship == null || ship.IsDestroyed || !_shipRuntimes.TryGetValue(ship, out shipRuntime))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(shipRuntime.ZoneId) || !_zonesById.TryGetValue(shipRuntime.ZoneId, out zoneRuntime))
            {
                return false;
            }

            return true;
        }

        private void RefreshHold(TrafficShipRuntime shipRuntime, float holdSeconds)
        {
            if (shipRuntime == null)
            {
                return;
            }

            shipRuntime.CombatHoldTimer = Math.Max(shipRuntime.CombatHoldTimer, holdSeconds);
        }

        private void TickEncounterHold(TrafficShipRuntime shipRuntime, NpcShip ship, float deltaTime, Action<string> log, string escapeMessage)
        {
            if (shipRuntime == null || ship == null || ship.IsDestroyed)
            {
                return;
            }

            shipRuntime.CombatHoldTimer = Math.Max(0f, shipRuntime.CombatHoldTimer - deltaTime);
            if (shipRuntime.CombatHoldTimer <= 0f && ship.EncounterState != TrafficEncounterState.Cruising)
            {
                TrafficEncounterState previousState = ship.EncounterState;
                ship.ClearEncounterState();
                if (previousState == TrafficEncounterState.Fleeing || previousState == TrafficEncounterState.InterceptingPirate)
                {
                    log?.Invoke(escapeMessage);
                }
            }
        }

        private void UpdateCombatHolds(TrafficZoneRuntime runtime, Ship playerShip, ReputationManager reputationManager, float deltaTime)
        {
            if (playerShip == null)
            {
                return;
            }

            foreach (NpcShip ship in runtime.ActiveShips)
            {
                if (ship == null || ship.IsDestroyed)
                {
                    continue;
                }

                if (!_shipRuntimes.TryGetValue(ship, out TrafficShipRuntime shipRuntime))
                {
                    continue;
                }

                bool hostile = reputationManager?.IsHostile(ship.FactionId) == true;
                float distanceToPlayer = Vector3.Distance(ship.Position, playerShip.Position);
                float combatHoldRange = Math.Max(5000f, runtime.Zone.Radius * 1.5f);
                if (hostile && distanceToPlayer <= combatHoldRange)
                {
                    shipRuntime.CombatHoldTimer = Math.Max(shipRuntime.CombatHoldTimer, 12f);
                }
                else if (shipRuntime.CombatHoldTimer > 0f)
                {
                    shipRuntime.CombatHoldTimer = Math.Max(0f, shipRuntime.CombatHoldTimer - deltaTime);
                }
            }
        }

        private void DespawnInactiveTraffic(TrafficZoneRuntime runtime, Ship playerShip, ReputationManager reputationManager, Action<string> log)
        {
            float despawnDistance = GetDespawnDistance(runtime.Zone);

            for (int i = runtime.ActiveShips.Count - 1; i >= 0; i--)
            {
                NpcShip ship = runtime.ActiveShips[i];
                if (ship == null)
                {
                    ReleaseShip(runtime, ship, log, "null");
                    continue;
                }

                if (ship.IsDestroyed)
                {
                    ReleaseShip(runtime, ship, log, "destroyed");
                    continue;
                }

                if (!_shipRuntimes.TryGetValue(ship, out TrafficShipRuntime shipRuntime))
                {
                    ReleaseShip(runtime, ship, log, "untracked");
                    continue;
                }

                bool hostile = reputationManager?.IsHostile(ship.FactionId) == true;
                if (ship.TrafficLifetimeSeconds > 0f && ship.TrafficAgeSeconds >= ship.TrafficLifetimeSeconds && shipRuntime.CombatHoldTimer <= 0f && !ship.IsTrafficEngaged)
                {
                    ReleaseShip(runtime, ship, log, "lifetime");
                    continue;
                }

                if (playerShip == null)
                {
                    continue;
                }

                float distanceToPlayer = Vector3.Distance(ship.Position, playerShip.Position);
                if (distanceToPlayer >= despawnDistance && shipRuntime.CombatHoldTimer <= 0f && !ship.IsTrafficEngaged && (!hostile || distanceToPlayer > despawnDistance * 0.5f))
                {
                    ReleaseShip(runtime, ship, log, "far");
                }
            }
        }

        private bool TrySpawnTraffic(TrafficZoneRuntime runtime, Action<string> log)
        {
            if (runtime.Zone == null)
            {
                return false;
            }

            TrafficZoneConfig zone = runtime.Zone;
            if (!_config.GetAllShipConfigs().Any())
            {
                log?.Invoke($"[TRAFFIC] ERROR: No ship configs are loaded for zone {zone.Name}.");
                return false;
            }

            ShipConfig shipConfig = _config.GetAllShipConfigs().FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.Description, zone.ShipDescription, StringComparison.OrdinalIgnoreCase));

            if (shipConfig == null)
            {
                log?.Invoke($"[TRAFFIC] ERROR: Ship config '{zone.ShipDescription}' not found for zone {zone.Name}.");
                return false;
            }

            int targetMax = Math.Max(Math.Max(0, zone.MinShips), zone.MaxShips);
            if (runtime.ActiveShips.Count >= targetMax)
            {
                return false;
            }

            Vector3 spawnPosition = DetermineSpawnPosition(zone, runtime.SpawnSerial++);
            Vector3 patrolCenter = zone.Center;
            string factionId = FactionManager.CoalesceFactionId(zone.FactionId, shipConfig.FactionId);

            NpcShip npc = new NpcShip(
                $"{shipConfig.Description} {runtime.SpawnSerial}",
                spawnPosition,
                patrolCenter,
                Math.Max(400f, zone.Radius),
                GetTrafficPatrolSpeed(zone.BehaviorType),
                factionId);

            npc.ConfigureTrafficBehavior(
                zone.BehaviorType,
                zone.Id,
                patrolCenter,
                zone.Radius,
                GetTrafficCruiseSpeed(zone.BehaviorType),
                GetTrafficActivationRange(zone.BehaviorType, zone.Radius),
                zone.RouteStart,
                zone.RouteEnd);
            npc.TrafficLifetimeSeconds = GetTrafficLifetime(zone.BehaviorType);

            ModelConfig modelConfig = shipConfig.ModelIndex > 0 ? _config.GetModel(shipConfig.ModelIndex) : null;
            if (_content != null && modelConfig != null && !string.IsNullOrWhiteSpace(modelConfig.Path))
            {
                try
                {
                    npc.ModelPath = modelConfig.Path;
                    npc.Model = _content.Load<Model>(modelConfig.Path);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[TRAFFIC] ERROR: Failed to load model for {npc.Name}: {ex.Message}");
                }
            }

            if (modelConfig != null)
            {
                npc.ModelRotationCorrection = shipConfig.ModelCorrectionRotation;
                npc.ModelPath = modelConfig.Path;
            }

            runtime.ActiveShips.Add(npc);
            _shipRuntimes[npc] = new TrafficShipRuntime
            {
                ZoneId = zone.Id,
                CombatHoldTimer = 0f
            };
            if (_onNpcDestroyed != null)
            {
                npc.OnDestroyed += _onNpcDestroyed;
            }
            _npcShips.Add(npc);
            _spaceObjects.Add(npc);
            log?.Invoke($"[TRAFFIC] Spawned {npc.Name} in {zone.Name} ({zone.BehaviorType})");
            return true;
        }

        private Vector3 DetermineSpawnPosition(TrafficZoneConfig zone, int sequence)
        {
            Vector3 center = zone.Center;
            float radius = Math.Max(100f, zone.Radius);

            if (zone.BehaviorType == TrafficZoneBehaviorType.TraderRoute && zone.RouteStart.HasValue && zone.RouteEnd.HasValue)
            {
                Vector3 routeStart = zone.RouteStart.Value;
                Vector3 routeEnd = zone.RouteEnd.Value;
                Vector3 anchor = (sequence % 2 == 0) ? routeStart : routeEnd;
                return anchor + RandomOffset(Math.Min(600f, radius * 0.15f));
            }

            float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
            float distance = (float)(_random.NextDouble() * radius);
            float vertical = (float)(_random.NextDouble() * radius * 0.2f - radius * 0.1f);
            return center + new Vector3(
                (float)Math.Cos(angle) * distance,
                vertical,
                (float)Math.Sin(angle) * distance);
        }

        private Vector3 RandomOffset(float radius)
        {
            float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
            float distance = (float)(_random.NextDouble() * radius);
            return new Vector3(
                (float)Math.Cos(angle) * distance,
                (float)(_random.NextDouble() * radius * 0.4f - radius * 0.2f),
                (float)Math.Sin(angle) * distance);
        }

        private static float GetTrafficLifetime(TrafficZoneBehaviorType behaviorType)
        {
            return behaviorType switch
            {
                TrafficZoneBehaviorType.TraderRoute => 220f,
                TrafficZoneBehaviorType.PirateAmbush => 260f,
                TrafficZoneBehaviorType.StationTraffic => 180f,
                _ => 300f,
            };
        }

        private static float GetTrafficCruiseSpeed(TrafficZoneBehaviorType behaviorType)
        {
            return behaviorType switch
            {
                TrafficZoneBehaviorType.TraderRoute => 190f,
                TrafficZoneBehaviorType.PirateAmbush => 220f,
                TrafficZoneBehaviorType.StationTraffic => 90f,
                _ => 180f,
            };
        }

        private static float GetTrafficPatrolSpeed(TrafficZoneBehaviorType behaviorType)
        {
            return behaviorType switch
            {
                TrafficZoneBehaviorType.TraderRoute => 0.15f,
                TrafficZoneBehaviorType.PirateAmbush => 0.35f,
                TrafficZoneBehaviorType.StationTraffic => 0.55f,
                _ => 0.85f,
            };
        }

        private static float GetTrafficActivationRange(TrafficZoneBehaviorType behaviorType, float zoneRadius)
        {
            return behaviorType == TrafficZoneBehaviorType.PirateAmbush
                ? Math.Max(6000f, zoneRadius * 1.25f)
                : Math.Max(3000f, zoneRadius * 0.9f);
        }

        private static float GetDespawnDistance(TrafficZoneConfig zone)
        {
            return zone.BehaviorType switch
            {
                TrafficZoneBehaviorType.TraderRoute => Math.Max(22000f, zone.Radius * 4f),
                TrafficZoneBehaviorType.PirateAmbush => Math.Max(32000f, zone.Radius * 4.5f),
                TrafficZoneBehaviorType.StationTraffic => Math.Max(14000f, zone.Radius * 2.5f),
                _ => Math.Max(26000f, zone.Radius * 4f),
            };
        }

        private void ReleaseShip(TrafficZoneRuntime runtime, NpcShip ship, Action<string> log, string reason)
        {
            if (ship == null)
            {
                return;
            }

            runtime.ActiveShips.Remove(ship);
            _shipRuntimes.Remove(ship);
            if (_onNpcDestroyed != null)
            {
                ship.OnDestroyed -= _onNpcDestroyed;
            }
            _npcShips.Remove(ship);
            _spaceObjects.Remove(ship);
            log?.Invoke($"[TRAFFIC] Despawned {ship.Name} from {runtime.Zone?.Name} ({reason})");
        }

        private void ClearTrackedTraffic(Action<string> log)
        {
            foreach (TrafficZoneRuntime runtime in _zonesById.Values)
            {
                for (int i = runtime.ActiveShips.Count - 1; i >= 0; i--)
                {
                    ReleaseShip(runtime, runtime.ActiveShips[i], log, "reload");
                }
            }

            _shipRuntimes.Clear();
        }
    }
}
