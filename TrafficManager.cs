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
            foreach (TrafficZoneRuntime runtime in _zonesById.Values)
            {
                if (runtime.Zone == null)
                {
                    continue;
                }

                CleanupRuntime(runtime, log);
                UpdateCombatHolds(runtime, playerShip, reputationManager, deltaTime);
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
                if (ship.TrafficLifetimeSeconds > 0f && ship.TrafficAgeSeconds >= ship.TrafficLifetimeSeconds && shipRuntime.CombatHoldTimer <= 0f)
                {
                    ReleaseShip(runtime, ship, log, "lifetime");
                    continue;
                }

                if (playerShip == null)
                {
                    continue;
                }

                float distanceToPlayer = Vector3.Distance(ship.Position, playerShip.Position);
                if (distanceToPlayer >= despawnDistance && shipRuntime.CombatHoldTimer <= 0f && (!hostile || distanceToPlayer > despawnDistance * 0.5f))
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
