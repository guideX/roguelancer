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
        private readonly FactionDistressResponseService _distressResponse;
        private readonly FactionCombatEscalationService _combatEscalation;
        private readonly FactionCombatDisengagementService _combatDisengagement;
        private ContentManager _content;

        public TrafficManager(ConfigurationManager config, List<NpcShip> npcShips, List<SpaceObject> spaceObjects, Action<NpcShip> onNpcDestroyed = null, ContentManager content = null)
        {
            _config = config ?? new ConfigurationManager();
            _npcShips = npcShips ?? new List<NpcShip>();
            _spaceObjects = spaceObjects ?? new List<SpaceObject>();
            _onNpcDestroyed = onNpcDestroyed;
            _content = content;
            _distressResponse = new FactionDistressResponseService(
                _npcShips,
                reputationManager: null,
                SpawnDistressReinforcements);
            _combatEscalation = new FactionCombatEscalationService(
                _npcShips,
                _distressResponse,
                reputationManager: null,
                SpawnEscalationReinforcements);
            _combatDisengagement = new FactionCombatDisengagementService(
                _npcShips,
                isResponseEncounterActive: encounterId =>
                    _distressResponse.IsEncounterActive(encounterId) ||
                    _combatEscalation.IsEncounterActive(encounterId));
        }

        public IReadOnlyList<TrafficZoneConfig> LoadedZones => _zonesById.Values.Select(runtime => runtime.Zone).ToList();
        public FactionDistressResponseService DistressResponse => _distressResponse;
        public FactionCombatEscalationService CombatEscalation => _combatEscalation;
        public FactionCombatDisengagementService CombatDisengagement => _combatDisengagement;

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
            _combatEscalation.Reset();
            _combatDisengagement.Reset();
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

            _combatEscalation.SetReputationManager(reputationManager);
            _combatDisengagement.SetReputationManager(reputationManager);
            _combatEscalation.Update(deltaTime);
            _combatDisengagement.Update(deltaTime, playerShip);
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

            _combatEscalation.NotifyNpcDestroyed(destroyedShip);
            _combatDisengagement.NotifyNpcDestroyed(destroyedShip);

            foreach (NpcShip other in _npcShips)
            {
                if (other != null && other.FactionCombatTarget == destroyedShip)
                    other.ClearFactionCombatTarget();
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

        public FactionDistressResponseResult NotifyPlayerDamage(NpcShip damagedShip, Ship playerShip = null)
        {
            FactionDistressResponseResult result = _combatEscalation.ProcessPlayerDamage(damagedShip, playerShip);
            _combatDisengagement.RecordPlayerDamage(damagedShip);
            return result;
        }

        public FactionDistressResponseResult NotifyNpcDamage(NpcShip attacker, NpcShip damagedShip, float damage)
        {
            FactionDistressResponseResult result = _combatEscalation.ProcessNpcDamage(attacker, damagedShip, damage);
            _combatDisengagement.RecordNpcDamage(attacker, damagedShip, damage);
            return result;
        }

        public void ResetTransientDistressState()
        {
            for (int i = _npcShips.Count - 1; i >= 0; i--)
            {
                NpcShip ship = _npcShips[i];
                if (ship == null || !ship.IsFactionTransientReinforcement)
                    continue;

                foreach (TrafficZoneRuntime runtime in _zonesById.Values)
                    runtime.ActiveShips.Remove(ship);

                _shipRuntimes.Remove(ship);
                _npcShips.RemoveAt(i);
                _spaceObjects.Remove(ship);
            }

            _combatEscalation.Reset();
            _combatDisengagement.Reset();
            foreach (NpcShip ship in _npcShips)
            {
                if (ship != null && !ship.IsDestroyed)
                    ship.ClearEncounterState();
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
            UpdateFactionCombatEngagements(playerShip, reputationManager, log);
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
                bool canAttackPlayer = playerShip != null && (reputationManager == null || reputationManager.IsFactionCurrentlyHostile(pirate.FactionId));
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

        /// <summary>
        /// Resolves only the active NPCs in the current traffic simulation.
        /// A small spatial hash limits each source to nearby cells; the
        /// configured traffic activation range then performs the exact check.
        /// </summary>
        private void UpdateFactionCombatEngagements(Ship playerShip, ReputationManager reputationManager, Action<string> log)
        {
            if (_npcShips.Count < 2)
                return;

            float cellSize = 1000f;
            for (int i = 0; i < _npcShips.Count; i++)
            {
                NpcShip ship = _npcShips[i];
                if (ship != null && !ship.IsDestroyed)
                    cellSize = Math.Max(cellSize, Math.Max(100f, ship.TrafficActivationRange));
            }

            Dictionary<FactionCombatCell, List<NpcShip>> cells = new();
            for (int i = 0; i < _npcShips.Count; i++)
            {
                NpcShip ship = _npcShips[i];
                if (ship == null || ship.IsDestroyed)
                    continue;

                FactionCombatCell cell = GetFactionCombatCell(ship.Position, cellSize);
                if (!cells.TryGetValue(cell, out List<NpcShip> occupants))
                {
                    occupants = new List<NpcShip>();
                    cells[cell] = occupants;
                }

                occupants.Add(ship);
            }

            for (int i = 0; i < _npcShips.Count; i++)
            {
                NpcShip source = _npcShips[i];
                if (source == null || source.IsDestroyed)
                    continue;

                if (source.FactionCombatTarget != null &&
                    source.HasValidFactionCombatTarget())
                {
                    source.SetFactionCombatTarget(
                        source.FactionCombatTarget,
                        preserveExistingEncounterState: true,
                        targetOrigin: source.FactionCombatTargetOrigin);
                    continue;
                }

                if (source.FactionCombatTarget != null)
                    source.ClearFactionCombatTarget();

                // An existing valid player target or flee/legacy attack state
                // keeps priority. The police intercept state remains eligible
                // for a matrix target because it already represents local NPC
                // combat; its movement state is preserved below.
                if ((source.HasPlayerTarget && source.HasValidPlayerTarget(reputationManager)) ||
                    source.EncounterState == TrafficEncounterState.Fleeing ||
                    source.EncounterState == TrafficEncounterState.AttackingTrader)
                {
                    continue;
                }

                // Leave newly eligible player hostility for NpcShip's live
                // player-disposition path. This preserves the explicit order:
                // current valid target, player, then faction contact.
                float playerRange = Math.Max(100f, source.TrafficActivationRange);
                bool playerTargetEligible = playerShip != null && reputationManager != null &&
                    (FactionDispositionEvaluator.IsHostile(source.FactionId, reputationManager) ||
                        (source.WasDamagedByPlayer && reputationManager.IsTemporarilyHostile(source.FactionId))) &&
                    Vector3.DistanceSquared(source.Position, playerShip.Position) <= playerRange * playerRange;
                if (playerTargetEligible && source.EncounterState == TrafficEncounterState.Cruising)
                    continue;

                List<NpcShip> nearbyCandidates = new();
                FactionCombatCell sourceCell = GetFactionCombatCell(source.Position, cellSize);
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            FactionCombatCell cell = new(sourceCell.X + x, sourceCell.Y + y, sourceCell.Z + z);
                            if (!cells.TryGetValue(cell, out List<NpcShip> occupants))
                                continue;

                            for (int candidateIndex = 0; candidateIndex < occupants.Count; candidateIndex++)
                            {
                                NpcShip candidate = occupants[candidateIndex];
                                if (!_combatDisengagement.IsTargetAcquisitionSuppressed(source, candidate))
                                    nearbyCandidates.Add(candidate);
                            }
                        }
                    }
                }

                float range = Math.Max(100f, source.TrafficActivationRange);
                NpcShip target = NpcFactionCombatTargeting.SelectNearestHostileTarget(source, nearbyCandidates, range);
                if (target == null)
                    continue;

                bool preserveLegacyState = source.EncounterState == TrafficEncounterState.InterceptingPirate;
                source.SetFactionCombatTarget(
                    target,
                    preserveLegacyState,
                    FactionCombatTargetOrigin.OrdinaryAcquisition);
                log?.Invoke($"[TRAFFIC] Faction combat: {source.Name} ({source.FactionId}) targeting {target.Name} ({target.FactionId}).");
            }
        }

        private static FactionCombatCell GetFactionCombatCell(Vector3 position, float cellSize)
        {
            return new FactionCombatCell(
                (int)Math.Floor(position.X / cellSize),
                (int)Math.Floor(position.Y / cellSize),
                (int)Math.Floor(position.Z / cellSize));
        }

        private readonly record struct FactionCombatCell(int X, int Y, int Z);

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
            if (state == TrafficEncounterState.AttackingPlayer && targetPosition.HasValue)
            {
                ship.SetPlayerTarget(targetPosition.Value, NpcPlayerTargetReason.FactionDisposition);
            }
            else
            {
                ship.SetEncounterState(state, targetPosition, escapePosition);
            }
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

                bool hostile = reputationManager?.IsFactionCurrentlyHostile(ship.FactionId) == true;
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

                bool hostile = reputationManager?.IsFactionCurrentlyHostile(ship.FactionId) == true;
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

        private bool TrySpawnTraffic(
            TrafficZoneRuntime runtime,
            Action<string> log,
            Vector3? responseOrigin = null,
            string responseEncounterId = null,
            bool isDistressReinforcement = false,
            bool isEscalationReinforcement = false)
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

            ShipConfig baseShipConfig = _config.GetAllShipConfigs().FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.Description, zone.ShipDescription, StringComparison.OrdinalIgnoreCase));

            if (baseShipConfig == null)
            {
                log?.Invoke($"[TRAFFIC] ERROR: Ship config '{zone.ShipDescription}' not found for zone {zone.Name}.");
                return false;
            }

            ShipConfig shipConfig = isEscalationReinforcement
                ? SelectEscalationShipConfig(zone, baseShipConfig)
                : baseShipConfig;

            int targetMax = Math.Max(Math.Max(0, zone.MinShips), zone.MaxShips);
            if (!isDistressReinforcement && !isEscalationReinforcement && runtime.ActiveShips.Count >= targetMax)
            {
                return false;
            }

            int spawnSerial = runtime.SpawnSerial++;
            Vector3 spawnPosition;
            if (responseOrigin.HasValue && isEscalationReinforcement)
            {
                if (!TryDetermineDistressSpawnPosition(responseOrigin.Value, spawnSerial, out spawnPosition))
                {
                    log?.Invoke($"[TRAFFIC] Escalation spawn skipped: no safe local placement near {zone.Name}.");
                    return false;
                }
            }
            else
            {
                spawnPosition = responseOrigin.HasValue
                    ? DetermineDistressSpawnPosition(responseOrigin.Value, spawnSerial)
                    : DetermineSpawnPosition(zone, spawnSerial);
            }
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
            if (isEscalationReinforcement)
            {
                npc.MarkEscalationReinforcement(responseEncounterId ?? string.Empty);
            }
            else if (isDistressReinforcement)
            {
                npc.MarkDistressReinforcement(responseEncounterId ?? string.Empty);
            }

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
            _combatDisengagement.RegisterShip(npc);
            log?.Invoke($"[TRAFFIC] Spawned {npc.Name} in {zone.Name} ({zone.BehaviorType})");
            return true;
        }

        private IReadOnlyList<NpcShip> SpawnDistressReinforcements(
            string factionId,
            Vector3 battlePosition,
            string encounterId,
            int requestedCount)
        {
            if (requestedCount <= 0 || string.IsNullOrWhiteSpace(factionId))
                return Array.Empty<NpcShip>();

            string normalizedFaction = FactionManager.NormalizeFactionId(factionId);
            TrafficZoneRuntime selectedRuntime = _zonesById.Values
                .Where(runtime => runtime.Zone != null &&
                    string.Equals(FactionManager.NormalizeFactionId(runtime.Zone.FactionId), normalizedFaction, StringComparison.OrdinalIgnoreCase))
                .OrderBy(runtime => Vector3.DistanceSquared(runtime.Zone.Center, battlePosition))
                .FirstOrDefault();

            if (selectedRuntime?.Zone == null)
                return Array.Empty<NpcShip>();

            List<NpcShip> spawned = new();
            int safeRequest = Math.Min(FactionDistressResponseService.ReinforcementWaveSize, requestedCount);
            for (int i = 0; i < safeRequest; i++)
            {
                if (!TrySpawnTraffic(selectedRuntime, Console.WriteLine, battlePosition, encounterId, isDistressReinforcement: true))
                    break;

                NpcShip reinforcement = selectedRuntime.ActiveShips.LastOrDefault(ship =>
                    ship != null && ship.IsDistressReinforcement &&
                    string.Equals(ship.DistressReinforcementEncounterId, encounterId, StringComparison.Ordinal));
                if (reinforcement != null && !spawned.Contains(reinforcement))
                    spawned.Add(reinforcement);
            }

            return spawned;
        }

        private IReadOnlyList<NpcShip> SpawnEscalationReinforcements(
            string factionId,
            Vector3 battlePosition,
            string encounterId,
            int requestedCount)
        {
            if (requestedCount <= 0 || string.IsNullOrWhiteSpace(factionId))
                return Array.Empty<NpcShip>();

            string normalizedFaction = FactionManager.NormalizeFactionId(factionId);
            TrafficZoneRuntime selectedRuntime = _zonesById.Values
                .Where(runtime => runtime.Zone != null &&
                    string.Equals(FactionManager.NormalizeFactionId(runtime.Zone.FactionId), normalizedFaction, StringComparison.OrdinalIgnoreCase))
                .OrderBy(runtime => Vector3.DistanceSquared(runtime.Zone.Center, battlePosition))
                .FirstOrDefault();

            if (selectedRuntime?.Zone == null)
                return Array.Empty<NpcShip>();

            List<NpcShip> spawned = new();
            int safeRequest = Math.Min(FactionCombatEscalationService.EscalationWaveSize, requestedCount);
            for (int i = 0; i < safeRequest; i++)
            {
                if (!TrySpawnTraffic(selectedRuntime, Console.WriteLine, battlePosition, encounterId, isEscalationReinforcement: true))
                    break;

                NpcShip reinforcement = selectedRuntime.ActiveShips.LastOrDefault(ship =>
                    ship != null && ship.IsEscalationReinforcement &&
                    string.Equals(ship.EscalationReinforcementEncounterId, encounterId, StringComparison.Ordinal));
                if (reinforcement != null && !spawned.Contains(reinforcement))
                    spawned.Add(reinforcement);
            }

            return spawned;
        }

        private ShipConfig SelectEscalationShipConfig(TrafficZoneConfig zone, ShipConfig fallback)
        {
            string factionId = FactionManager.NormalizeFactionId(zone?.FactionId);
            return _config.GetAllShipConfigs()
                .Where(candidate => candidate != null &&
                    (string.Equals(candidate.Description, zone?.ShipDescription, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(FactionManager.NormalizeFactionId(candidate.FactionId), factionId, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(GetEscalationShipPriority)
                .ThenBy(candidate => candidate.Description, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? fallback;
        }

        private static int GetEscalationShipPriority(ShipConfig shipConfig)
        {
            string description = shipConfig?.Description ?? string.Empty;
            int priority = 0;
            if (description.Contains("heavy", StringComparison.OrdinalIgnoreCase))
                priority += 30;
            if (description.Contains("fighter", StringComparison.OrdinalIgnoreCase))
                priority += 20;
            if (description.Contains("patrol", StringComparison.OrdinalIgnoreCase))
                priority += 10;
            return priority;
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

        private Vector3 DetermineDistressSpawnPosition(Vector3 battlePosition, int sequence)
        {
            if (TryDetermineDistressSpawnPosition(battlePosition, sequence, out Vector3 safePosition))
                return safePosition;

            return battlePosition + new Vector3(2400f, 0f, 0f);
        }

        private bool TryDetermineDistressSpawnPosition(Vector3 battlePosition, int sequence, out Vector3 spawnPosition)
        {
            const float minimumBattleDistance = 1800f;
            const float baseSpawnDistance = 2400f;
            const float collisionDistance = 500f;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = sequence * 2.3999631f + attempt * (MathHelper.Pi / 4f);
                float distance = baseSpawnDistance + (sequence % 3) * 180f;
                Vector3 candidate = battlePosition + new Vector3(
                    (float)Math.Cos(angle) * distance,
                    ((sequence + attempt) % 3 - 1) * 120f,
                    (float)Math.Sin(angle) * distance);

                if (Vector3.DistanceSquared(candidate, battlePosition) < minimumBattleDistance * minimumBattleDistance)
                    continue;

                bool collides = false;
                for (int i = 0; i < _npcShips.Count; i++)
                {
                    NpcShip other = _npcShips[i];
                    if (other == null || other.IsDestroyed)
                        continue;

                    if (Vector3.DistanceSquared(candidate, other.Position) < collisionDistance * collisionDistance)
                    {
                        collides = true;
                        break;
                    }
                }

                if (!collides)
                {
                    spawnPosition = candidate;
                    return true;
                }
            }

            spawnPosition = Vector3.Zero;
            return false;
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
            foreach (NpcShip other in _npcShips)
            {
                if (other != null && other.FactionCombatTarget == ship)
                    other.ClearFactionCombatTarget();
            }

            _shipRuntimes.Remove(ship);
            _combatDisengagement.NotifyNpcDespawned(ship);
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
