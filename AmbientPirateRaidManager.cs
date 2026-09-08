using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Read-only explanation of one bounded autonomous piracy opportunity.
/// </summary>
public sealed class AmbientPirateRaidOpportunity
{
    public string ShipmentIdentity { get; internal set; } = string.Empty;
    public int RemainingManifestValue { get; internal set; }
    public int RemainingQuantity { get; internal set; }
    public int RouteRisk { get; internal set; }
    public int SecurityEscortCount { get; internal set; }
    public int RoguePopulation { get; internal set; }
    public bool DestinationShortage { get; internal set; }
    public int Score { get; internal set; }
    public int RaiderCount { get; internal set; }
    public float DelaySeconds { get; internal set; }
}

/// <summary>One physical bounded haul carried by a living ambient raider.</summary>
public sealed class AmbientPirateHaulEntry
{
    public string CommodityId { get; internal set; } = string.Empty;
    public int Quantity { get; internal set; }
}

/// <summary>Runtime identity and durable state for an assigned raider.</summary>
public sealed class AmbientPirateRaider
{
    internal AmbientPirateRaider(
        string stableIdentity,
        string archetypeName,
        string modelPath,
        NpcLoadoutTier loadoutTier)
    {
        StableIdentity = stableIdentity ?? string.Empty;
        ArchetypeName = archetypeName ?? string.Empty;
        ModelPath = modelPath ?? string.Empty;
        LoadoutTier = loadoutTier;
    }

    public string StableIdentity { get; }
    public string ArchetypeName { get; }
    public string ModelPath { get; }
    public NpcLoadoutTier LoadoutTier { get; }
    public NpcShip Ship { get; internal set; }
    public bool IsAlive { get; internal set; }
    public bool HasEscaped { get; internal set; }
    public IReadOnlyList<AmbientPirateHaulEntry> Haul => _haul;
    internal List<AmbientPirateHaulEntry> MutableHaul => _haul;
    private readonly List<AmbientPirateHaulEntry> _haul = new();
}

/// <summary>
/// One non-mission ambient Rogue raid against one real economic shipment.
/// This class owns only encounter state; combat, movement, manifests, loot,
/// and route settlement remain owned by their existing systems.
/// </summary>
public sealed class AmbientPirateRaid
{
    internal AmbientPirateRaid(EconomicShipment shipment, AmbientPirateRaidOpportunity opportunity)
    {
        Shipment = shipment;
        ShipmentIdentity = shipment?.TraderIdentity ?? string.Empty;
        Opportunity = opportunity;
    }

    public string ShipmentIdentity { get; }
    public EconomicShipment Shipment { get; }
    public AmbientPirateRaidOpportunity Opportunity { get; }
    public AmbientPirateRaidState State { get; internal set; }
    public float DelayRemainingSeconds { get; internal set; }
    public float ElapsedSeconds { get; internal set; }
    public float PressureSeconds { get; internal set; }
    public float RecentPressureSeconds { get; internal set; }
    public bool SurrenderEvaluated { get; internal set; }
    public int SurrenderedQuantity { get; internal set; }
    public int RecoveredQuantity { get; internal set; }
    public IReadOnlyList<AmbientPirateRaider> Raiders => _raiders;
    internal List<AmbientPirateRaider> MutableRaiders => _raiders;
    private readonly List<AmbientPirateRaider> _raiders = new();
}

/// <summary>
/// Bounded autonomous pirate opportunity coordinator. It schedules at most
/// two raids in the current traffic manager, assigns at most three real Rogue
/// ships to each, and then lets the ordinary NPC combat/movement pass run.
/// </summary>
public sealed class AmbientPirateRaidManager
{
    public const int MinimumRemainingManifestValue = 4_000;
    public const int ExceptionalRemainingManifestValue = 2_500;
    public const int OpportunityThreshold = 50;
    public const int MaximumActiveRaidsPerSystem = 2;
    public const int MaximumRaidersPerRaid = 3;
    public const int MaximumHaulStacksPerRaider = 3;
    public const int MaximumTrackedRaids = EconomicShipmentManager.MaximumActiveShipments;
    public const float EvaluationCadenceSeconds = 5f;
    public const float MinimumDelaySeconds = 10f;
    public const float MaximumDelaySeconds = 30f;
    public const float MinimumPressureSeconds = 4f;
    public const float RaidTimeoutSeconds = 70f;
    public const float RaiderSpawnDistance = 2_200f;
    public const float RaiderSpawnSpacing = 620f;
    public const float MaximumCargoSeekDistance = 6_000f;
    public const float RaiderCargoPickupRadius = 180f;

    public delegate NpcShip RaiderSpawnFactory(
        EconomicShipment shipment,
        string raidIdentity,
        int index,
        NpcLoadoutTier loadoutTier,
        Vector3 position,
        string stableIdentity);

    public delegate bool CargoPodCollector(
        NpcShip collector,
        CargoPod pod,
        int maximumQuantity,
        out string commodityId,
        out int collectedQuantity);

    private readonly List<NpcShip> _npcShips;
    private readonly List<SpaceObject> _spaceObjects;
    private readonly Dictionary<string, AmbientPirateRaid> _raids = new(StringComparer.Ordinal);
    private readonly Func<NpcShip, bool> _isMissionOwned;
    private readonly Func<IEnumerable<TrafficZoneConfig>> _routesProvider;
    private EconomicShipmentManager _economicShipments;
    private Func<EconomicShipment, bool> _shortageResolver;
    private RaiderSpawnFactory _spawnRaider;
    private Action<NpcShip, string> _retireRaider;
    private Func<NpcShip, string, int, int> _spawnCargo;
    private CargoPodCollector _collectCargo;
    private Func<IEnumerable<CargoPod>> _cargoPodsProvider;
    private float _evaluationTimer;

    public AmbientPirateRaidManager(
        List<NpcShip> npcShips,
        List<SpaceObject> spaceObjects,
        EconomicShipmentManager economicShipments = null,
        Func<IEnumerable<TrafficZoneConfig>> routesProvider = null,
        Func<NpcShip, bool> isMissionOwned = null)
    {
        _npcShips = npcShips ?? new List<NpcShip>();
        _spaceObjects = spaceObjects ?? new List<SpaceObject>();
        _economicShipments = economicShipments;
        _routesProvider = routesProvider ?? (() => Array.Empty<TrafficZoneConfig>());
        _isMissionOwned = isMissionOwned ?? (_ => false);
    }

    public IReadOnlyList<AmbientPirateRaid> Raids => _raids.Values.ToList();
    public IReadOnlyList<AmbientPirateRaid> ActiveRaids => _raids.Values
        .Where(raid => raid?.State == AmbientPirateRaidState.Active)
        .ToList();
    public int ActiveRaidCount => ActiveRaids.Count;
    public int TrackedRaidCount => _raids.Count;

    public void ConfigureEconomicShipments(
        EconomicShipmentManager economicShipments,
        Func<EconomicShipment, bool> shortageResolver = null)
    {
        _economicShipments = economicShipments;
        _shortageResolver = shortageResolver;
    }

    public void ConfigureRuntime(
        RaiderSpawnFactory spawnRaider,
        Action<NpcShip, string> retireRaider,
        Func<NpcShip, string, int, int> spawnCargo,
        CargoPodCollector collectCargo,
        Func<IEnumerable<CargoPod>> cargoPodsProvider = null)
    {
        _spawnRaider = spawnRaider;
        _retireRaider = retireRaider;
        _spawnCargo = spawnCargo;
        _collectCargo = collectCargo;
        _cargoPodsProvider = cargoPodsProvider;
    }

    /// <summary>
    /// Deterministic bounded candidate evaluation. This method has no side
    /// effects and is intentionally useful to focused smoke coverage.
    /// </summary>
    public bool TryEvaluateOpportunity(
        EconomicShipment shipment,
        out AmbientPirateRaidOpportunity opportunity)
    {
        opportunity = null;
        if (!IsEligibleShipment(shipment))
            return false;

        int routeRisk = GetRouteRisk(shipment);
        int roguePopulation = _npcShips.Count(ship => ship != null && !ship.IsDestroyed &&
            string.Equals(FactionManager.NormalizeFactionId(ship.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase));
        int securityCount = Math.Clamp(shipment.ActiveSecurityEscortCount, 0, ShipmentSecurityManager.MaximumEscortsPerShipment);
        bool shortage = _shortageResolver?.Invoke(shipment) == true;
        int value = Math.Max(0, shipment.RemainingManifestValue);
        bool valueGate = value >= MinimumRemainingManifestValue ||
            value >= ExceptionalRemainingManifestValue && (routeRisk >= TradeRouteRiskManager.SevereRiskThreshold || shortage);
        if (!valueGate)
            return false;

        int score = CalculateOpportunityScore(value, shipment.RemainingQuantity, routeRisk,
            securityCount, roguePopulation, shortage);
        if (score < OpportunityThreshold)
            return false;

        int raiderCount = score >= 140 ? 3 : score >= 82 ? 2 : 1;
        uint hash = StableHash($"{shipment.TraderIdentity}|{shipment.RouteId}|{shipment.RouteTowardEnd}|{value}|{routeRisk}");
        float delay = MinimumDelaySeconds + (hash % (uint)(MaximumDelaySeconds - MinimumDelaySeconds + 1f));
        opportunity = new AmbientPirateRaidOpportunity
        {
            ShipmentIdentity = shipment.TraderIdentity,
            RemainingManifestValue = value,
            RemainingQuantity = shipment.RemainingQuantity,
            RouteRisk = routeRisk,
            SecurityEscortCount = securityCount,
            RoguePopulation = roguePopulation,
            DestinationShortage = shortage,
            Score = score,
            RaiderCount = Math.Clamp(raiderCount, 1, MaximumRaidersPerRaid),
            DelaySeconds = Math.Clamp(delay, MinimumDelaySeconds, MaximumDelaySeconds)
        };
        return true;
    }

    public static int CalculateOpportunityScore(
        int remainingManifestValue,
        int remainingQuantity,
        int routeRisk,
        int securityEscortCount,
        int roguePopulation,
        bool destinationShortage)
    {
        int valueContribution = Math.Clamp(Math.Max(0, remainingManifestValue) / 100, 0, 400);
        int quantityContribution = Math.Clamp(Math.Max(0, remainingQuantity) * 2, 0, 30);
        int riskContribution = Math.Clamp(Math.Max(0, routeRisk), 0, TradeRouteRiskManager.MaximumRiskScore) / 2;
        int shortageContribution = destinationShortage ? 18 : 0;
        int securityDeterrence = Math.Clamp(Math.Max(0, securityEscortCount), 0,
            ShipmentSecurityManager.MaximumEscortsPerShipment) * 12;
        int populationPressure = Math.Clamp(Math.Max(0, roguePopulation), 0, TrafficManager.MaximumNpcPopulation);
        return Math.Max(0, valueContribution + quantityContribution + riskContribution +
            shortageContribution - securityDeterrence - populationPressure);
    }

    public static int CalculateSurrenderScore(
        EconomicShipment shipment,
        float pressureSeconds,
        int activeRaiderCount,
        int activeSecurityCount)
    {
        NpcShip trader = shipment?.Trader;
        if (trader == null)
            return 0;

        float hull = trader.Hull?.MaxHull > 0f
            ? Math.Clamp(trader.Hull.CurrentHull / trader.Hull.MaxHull, 0f, 1f)
            : 0f;
        float shields = trader.Shields?.MaxShields > 0f
            ? Math.Clamp(trader.Shields.CurrentShields / trader.Shields.MaxShields, 0f, 1f)
            : 0f;
        int score = 20 + (int)Math.Round((1f - hull) * 50f) +
            (int)Math.Round((1f - shields) * 18f) +
            Math.Clamp(activeRaiderCount, 0, MaximumRaidersPerRaid) * 8 +
            (int)Math.Round(Math.Clamp(pressureSeconds, 0f, 12f) * 3f) -
            Math.Clamp(activeSecurityCount, 0, ShipmentSecurityManager.MaximumEscortsPerShipment) * 16;
        return Math.Clamp(score, 0, 100);
    }

    public static int GetSurrenderThreshold(EconomicShipment shipment)
    {
        return 45 + (int)(StableHash($"{shipment?.TraderIdentity}|ambient-surrender") % 31u);
    }

    public void Update(float deltaSeconds, Action<string> log = null)
    {
        float delta = NormalizeDelta(deltaSeconds);
        if (_economicShipments == null)
            return;

        _evaluationTimer -= delta;
        if (_evaluationTimer <= 0f)
        {
            _evaluationTimer = EvaluationCadenceSeconds;
            EvaluateNewOpportunities(log);
        }

        foreach (AmbientPirateRaid raid in _raids.Values.ToList())
        {
            if (raid == null)
                continue;

            if (raid.State == AmbientPirateRaidState.Delayed)
            {
                if (!IsStillValidBeforeSpawn(raid.Shipment))
                {
                    Resolve(raid, "shipment invalidated before raid spawn", log);
                    continue;
                }

                raid.DelayRemainingSeconds = Math.Max(0f, raid.DelayRemainingSeconds - delta);
                if (raid.DelayRemainingSeconds <= 0f)
                    Activate(raid, log);
                continue;
            }

            if (raid.State == AmbientPirateRaidState.Active)
                UpdateActiveRaid(raid, delta, log);
        }
    }

    public void NotifyNpcDamage(NpcShip attacker, NpcShip damagedShip, float damage)
    {
        if (attacker == null || damagedShip == null || damage <= 0f ||
            float.IsNaN(damage) || float.IsInfinity(damage))
            return;

        foreach (AmbientPirateRaid raid in _raids.Values)
        {
            if (raid?.State != AmbientPirateRaidState.Active || raid.Shipment?.Trader == null)
                continue;

            bool assignedAttacker = raid.Raiders.Any(raider => raider?.Ship == attacker && raider.IsAlive);
            bool assignedTarget = raid.Raiders.Any(raider => raider?.Ship == damagedShip && raider.IsAlive);
            bool merchantHit = damagedShip == raid.Shipment.Trader && assignedAttacker;
            bool raiderHit = assignedTarget && (attacker == raid.Shipment.Trader ||
                raid.Shipment.SecurityDetail?.Members.Any(member => member?.Ship == attacker) == true);
            if (merchantHit || raiderHit)
                raid.RecentPressureSeconds = Math.Max(raid.RecentPressureSeconds, 2.5f);
        }
    }

    public void NotifyNpcDestroyed(NpcShip destroyedShip, Action<string> log = null)
    {
        if (destroyedShip == null)
            return;

        foreach (AmbientPirateRaid raid in _raids.Values.ToList())
        {
            if (raid == null)
                continue;

            if (raid.Shipment?.Trader == destroyedShip)
            {
                Resolve(raid, "merchant destroyed", log);
                continue;
            }

            AmbientPirateRaider raider = raid.MutableRaiders.FirstOrDefault(candidate => candidate?.Ship == destroyedShip);
            if (raider == null)
                continue;

            raider.IsAlive = false;
            raider.Ship = null;
            DropRaiderHaul(raid, raider, destroyedShip, log);
            if (raid.State == AmbientPirateRaidState.Active &&
                !raid.Raiders.Any(candidate => candidate?.IsAlive == true))
            {
                Resolve(raid, "all assigned raiders destroyed", log);
            }
        }
    }

    public List<SaveAmbientPirateRaidData> CaptureState()
    {
        return _raids.Values
            .Where(raid => raid?.Shipment?.Settlement == EconomicShipmentSettlement.Active &&
                raid.Shipment.AmbientRaidAttempted)
            .OrderBy(raid => raid.ShipmentIdentity, StringComparer.Ordinal)
            .Take(MaximumTrackedRaids)
            .Select(CaptureRaid)
            .ToList();
    }

    public void RestoreState(IEnumerable<SaveAmbientPirateRaidData> savedStates, Action<string> log = null)
    {
        foreach (AmbientPirateRaid raid in _raids.Values.ToList())
            Resolve(raid, "save/load rebind", log);
        _raids.Clear();

        foreach (SaveAmbientPirateRaidData saved in (savedStates ?? Array.Empty<SaveAmbientPirateRaidData>()).Take(MaximumTrackedRaids))
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.ShipmentIdentity) ||
                !Enum.IsDefined(typeof(AmbientPirateRaidState), saved.State) ||
                _economicShipments == null ||
                !_economicShipments.TryGetShipmentByIdentity(saved.ShipmentIdentity, out EconomicShipment shipment))
                continue;

            AmbientPirateRaidOpportunity opportunity = new()
            {
                ShipmentIdentity = shipment.TraderIdentity,
                RemainingManifestValue = shipment.RemainingManifestValue,
                RemainingQuantity = shipment.RemainingQuantity,
                RouteRisk = _economicShipments.GetEffectiveRouteRisk(shipment),
                SecurityEscortCount = shipment.ActiveSecurityEscortCount,
                Score = OpportunityThreshold,
                RaiderCount = Math.Clamp(saved.Raiders?.Count ?? 0, 1, MaximumRaidersPerRaid),
                DelaySeconds = Math.Clamp(saved.DelayRemainingSeconds, MinimumDelaySeconds, MaximumDelaySeconds)
            };
            AmbientPirateRaid raid = new(shipment, opportunity)
            {
                State = saved.State,
                DelayRemainingSeconds = Math.Max(0f, saved.DelayRemainingSeconds),
                ElapsedSeconds = Math.Clamp(saved.ElapsedSeconds, 0f, RaidTimeoutSeconds),
                PressureSeconds = Math.Clamp(saved.PressureSeconds, 0f, RaidTimeoutSeconds),
                RecentPressureSeconds = Math.Clamp(saved.RecentPressureSeconds, 0f, 10f),
                SurrenderEvaluated = saved.SurrenderEvaluated,
                SurrenderedQuantity = Math.Clamp(saved.SurrenderedQuantity, 0, shipment.InitialQuantity),
                RecoveredQuantity = Math.Clamp(saved.RecoveredQuantity, 0, shipment.InitialQuantity)
            };
            shipment.AmbientRaidAttempted = true;
            shipment.AmbientRaidState = raid.State;
            shipment.AmbientRaidSurrendered = raid.SurrenderedQuantity > 0;
            _raids[raid.ShipmentIdentity] = raid;

            if (raid.State == AmbientPirateRaidState.Active)
            {
                RestoreActiveRaiders(raid, saved.Raiders, log);
                if (!raid.Raiders.Any(raider => raider?.IsAlive == true))
                    Resolve(raid, "saved raid had no surviving raiders", log);
                else
                    SetMerchantUnderRaid(raid.Shipment);
            }
        }
    }

    public void Reset(Action<string> log = null)
    {
        foreach (AmbientPirateRaid raid in _raids.Values.ToList())
            Resolve(raid, "world reset", log);
        _raids.Clear();
        _evaluationTimer = 0f;
    }

    private void EvaluateNewOpportunities(Action<string> log)
    {
        if (_economicShipments == null || _raids.Values.Count(raid => raid != null &&
            raid.State is AmbientPirateRaidState.Delayed or AmbientPirateRaidState.Active) >= MaximumActiveRaidsPerSystem)
            return;

        foreach (EconomicShipment shipment in _economicShipments.ActiveShipments
                     .Where(candidate => candidate != null)
                     .OrderByDescending(candidate => candidate.RemainingManifestValue)
                     .ThenBy(candidate => candidate.TraderIdentity, StringComparer.Ordinal))
        {
            int pendingOrActive = _raids.Values.Count(raid => raid != null &&
                raid.State is AmbientPirateRaidState.Delayed or AmbientPirateRaidState.Active);
            if (_raids.Count >= MaximumTrackedRaids || pendingOrActive >= MaximumActiveRaidsPerSystem)
                break;
            if (shipment.AmbientRaidAttempted ||
                _raids.ContainsKey(shipment.TraderIdentity) ||
                !TryEvaluateOpportunity(shipment, out AmbientPirateRaidOpportunity opportunity))
                continue;

            shipment.AmbientRaidAttempted = true;
            shipment.AmbientRaidState = AmbientPirateRaidState.Delayed;
            AmbientPirateRaid raid = new(shipment, opportunity)
            {
                State = AmbientPirateRaidState.Delayed,
                DelayRemainingSeconds = opportunity.DelaySeconds
            };
            _raids[raid.ShipmentIdentity] = raid;
            log?.Invoke($"[PIRACY] Ambient Rogue raid scheduled for {shipment.Trader.Name} in {opportunity.DelaySeconds:0}s (score={opportunity.Score}).");
        }
    }

    private void Activate(AmbientPirateRaid raid, Action<string> log)
    {
        if (raid?.Shipment == null || !IsStillValidBeforeSpawn(raid.Shipment))
        {
            Resolve(raid, "shipment invalidated before activation", log);
            return;
        }

        raid.State = AmbientPirateRaidState.Active;
        raid.Shipment.AmbientRaidState = AmbientPirateRaidState.Active;
        int count = Math.Clamp(raid.Opportunity?.RaiderCount ?? 1, 1, MaximumRaidersPerRaid);
        IReadOnlyList<Vector3> positions = BuildInterceptPositions(raid.Shipment.Trader, count);
        for (int index = 0; index < count; index++)
        {
            string stableIdentity = $"{raid.ShipmentIdentity}|ambient-raider:{index + 1}";
            NpcLoadoutTier tier = raid.Opportunity.Score >= 115 && index == count - 1
                ? NpcLoadoutTier.High
                : NpcLoadoutTier.Standard;
            Vector3 position = positions[index];
            NpcShip ship = null;
            try
            {
                ship = _spawnRaider?.Invoke(raid.Shipment, raid.ShipmentIdentity, index, tier, position, stableIdentity);
            }
            catch (Exception ex)
            {
                log?.Invoke($"[PIRACY] Ambient raider spawn failed: {ex.Message}");
            }

            if (ship == null)
                continue;

            ship.MarkAmbientPirateRaider(raid.ShipmentIdentity);
            ship.TrafficLifetimeSeconds = RaidTimeoutSeconds + 20f;
            ship.SetFactionCombatTarget(raid.Shipment.Trader, targetOrigin: FactionCombatTargetOrigin.AmbientPiracy);
            raid.MutableRaiders.Add(new AmbientPirateRaider(
                stableIdentity,
                ship.Name,
                ship.ModelPath,
                tier)
            {
                Ship = ship,
                IsAlive = true
            });
        }

        if (raid.Raiders.Count == 0)
        {
            Resolve(raid, "no raider could be spawned", log);
            return;
        }

        SetMerchantUnderRaid(raid.Shipment);
        log?.Invoke($"[PIRACY] Ambient Rogue raid started: {raid.Shipment.Trader.Name} with {raid.Raiders.Count} raider(s).");
    }

    private void RestoreActiveRaiders(
        AmbientPirateRaid raid,
        IEnumerable<SaveAmbientPirateRaiderData> savedRaiders,
        Action<string> log)
    {
        foreach (SaveAmbientPirateRaiderData saved in (savedRaiders ?? Array.Empty<SaveAmbientPirateRaiderData>()).Take(MaximumRaidersPerRaid))
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.StableIdentity))
                continue;

            NpcLoadoutTier tier = Enum.IsDefined(typeof(NpcLoadoutTier), saved.LoadoutTier)
                ? saved.LoadoutTier
                : NpcLoadoutTier.Standard;
            AmbientPirateRaider raider = new(
                saved.StableIdentity.Trim(),
                saved.ArchetypeName,
                saved.ModelPath,
                tier)
            {
                HasEscaped = saved.Escaped,
                IsAlive = saved.Alive
            };
            foreach (SaveAmbientPirateHaulData haul in (saved.Haul ?? new List<SaveAmbientPirateHaulData>()).Take(MaximumHaulStacksPerRaider))
            {
                if (haul == null || string.IsNullOrWhiteSpace(haul.CommodityId) || haul.Quantity <= 0)
                    continue;
                Commodity commodity = CommodityCatalog.GetById(haul.CommodityId);
                if (commodity == null)
                    continue;
                raider.MutableHaul.Add(new AmbientPirateHaulEntry
                {
                    CommodityId = commodity.Id,
                    Quantity = Math.Clamp(haul.Quantity, 1, 40)
                });
            }

            if (raider.IsAlive)
            {
                Vector3 position = saved.Position?.ToVector3(raid.Shipment.Trader.Position) ?? raid.Shipment.Trader.Position;
                NpcShip ship = null;
                try
                {
                    ship = _spawnRaider?.Invoke(raid.Shipment, raid.ShipmentIdentity,
                        raid.MutableRaiders.Count, tier, position, raider.StableIdentity);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[PIRACY] Saved ambient raider restore failed: {ex.Message}");
                }

                if (ship != null)
                {
                    ship.MarkAmbientPirateRaider(raid.ShipmentIdentity);
                    ship.TrafficLifetimeSeconds = RaidTimeoutSeconds + 20f;
                    ship.Velocity = saved.Velocity?.ToVector3(Vector3.Zero) ?? Vector3.Zero;
                    ship.SetFactionCombatTarget(raid.Shipment.Trader, targetOrigin: FactionCombatTargetOrigin.AmbientPiracy);
                    raider.Ship = ship;
                }
                else
                {
                    raider.IsAlive = false;
                }
            }

            raid.MutableRaiders.Add(raider);
        }
    }

    private void UpdateActiveRaid(AmbientPirateRaid raid, float delta, Action<string> log)
    {
        if (raid.Shipment?.Settlement != EconomicShipmentSettlement.Active ||
            raid.Shipment.Trader == null || raid.Shipment.Trader.IsDestroyed)
        {
            Resolve(raid, "shipment settled or merchant destroyed", log);
            return;
        }

        if (raid.Shipment.Trader.IsTradeLaneTransit)
        {
            Resolve(raid, "merchant entered trade lane", log);
            return;
        }

        raid.ElapsedSeconds = Math.Min(RaidTimeoutSeconds, raid.ElapsedSeconds + delta);
        raid.RecentPressureSeconds = Math.Max(0f, raid.RecentPressureSeconds - delta);
        UpdateRaiderObjectives(raid, log);
        UpdateRaiderTargetPriority(raid);

        int activeRaiders = raid.Raiders.Count(raider => raider?.IsAlive == true && raider.Ship != null && !raider.Ship.IsDestroyed);
        if (activeRaiders == 0)
        {
            Resolve(raid, "all assigned raiders destroyed", log);
            return;
        }

        bool crediblePressure = raid.RecentPressureSeconds > 0f && raid.Raiders.Any(raider =>
            raider?.IsAlive == true && raider.Ship != null &&
            raider.Ship.FactionCombatTarget == raid.Shipment.Trader);
        raid.PressureSeconds = crediblePressure
            ? Math.Min(RaidTimeoutSeconds, raid.PressureSeconds + delta)
            : Math.Max(0f, raid.PressureSeconds - delta * 0.5f);

        if (!raid.SurrenderEvaluated && raid.PressureSeconds >= MinimumPressureSeconds)
        {
            raid.SurrenderEvaluated = true;
            int securityCount = raid.Shipment.ActiveSecurityEscortCount;
            int score = CalculateSurrenderScore(raid.Shipment, raid.PressureSeconds, activeRaiders, securityCount);
            int threshold = GetSurrenderThreshold(raid.Shipment);
            if (score >= threshold && _economicShipments.TrySurrenderCargo(
                    raid.Shipment.Trader,
                    (commodityId, quantity) => _spawnCargo?.Invoke(raid.Shipment.Trader, commodityId, quantity) ?? 0,
                    out int surrendered,
                    out int surrenderedValue))
            {
                raid.SurrenderedQuantity = surrendered;
                raid.Shipment.AmbientRaidSurrendered = surrendered > 0;
                _economicShipments.RecordAmbientRaidSurrender(raid.Shipment, surrenderedValue);
                SetMerchantUnderRaid(raid.Shipment);
                log?.Invoke($"[PIRACY] Trader surrendered {surrendered} unit(s) to ambient Rogue raiders.");
            }
            else
            {
                log?.Invoke($"[PIRACY] Trader refused ambient surrender pressure (score={score}, threshold={threshold}).");
            }
        }

        if (raid.SurrenderedQuantity > 0 && raid.RecoveredQuantity >= raid.SurrenderedQuantity / 2f)
        {
            Resolve(raid, "raiders recovered meaningful cargo", log);
            return;
        }

        if (raid.ElapsedSeconds >= RaidTimeoutSeconds)
        {
            Resolve(raid, "raid timeout", log);
            return;
        }

        if (raid.SurrenderedQuantity > 0 && raid.Raiders.All(raider =>
            raider?.IsAlive != true || raider.MutableHaul.Sum(entry => entry?.Quantity ?? 0) > 0))
        {
            // Every surviving raider has either a recovered haul or has been
            // removed. Their cargo is an economic sink until a later phase
            // adds a Rogue delivery economy.
            Resolve(raid, "raiders disengaged with stolen haul", log);
        }
    }

    private void UpdateRaiderObjectives(AmbientPirateRaid raid, Action<string> log)
    {
        if (raid?.SurrenderedQuantity <= 0 || _collectCargo == null)
            return;

        foreach (AmbientPirateRaider raider in raid.MutableRaiders.Where(candidate => candidate?.IsAlive == true && candidate.Ship != null).ToList())
        {
            NpcShip ship = raider.Ship;
            CargoPod targetPod = FindNearestRaidPod(raid, ship);
            if (targetPod == null)
            {
                ship.ClearAmbientCargoObjective();
                if (ship.FactionCombatTarget == null || ship.FactionCombatTarget.IsDestroyed)
                    ship.SetFactionCombatTarget(raid.Shipment.Trader, targetOrigin: FactionCombatTargetOrigin.AmbientPiracy);
                continue;
            }

            if (!targetPod.IsWithinPickupRange(ship.Position))
            {
                ship.SetAmbientCargoObjective(targetPod.Position);
                continue;
            }

            if (!CanAcceptHaul(raider, targetPod))
            {
                ship.ClearAmbientCargoObjective();
                continue;
            }

            if (_collectCargo(ship, targetPod, Math.Max(1, targetPod.Quantity), out string commodityId, out int quantity) &&
                quantity > 0 && !string.IsNullOrWhiteSpace(commodityId))
            {
                AddHaul(raider, commodityId, quantity);
                raid.RecoveredQuantity += quantity;
                ship.ClearAmbientCargoObjective();
                log?.Invoke($"[PIRACY] Raider recovered stolen cargo: {commodityId} x{quantity}.");
            }
        }
    }

    private CargoPod FindNearestRaidPod(AmbientPirateRaid raid, NpcShip raider)
    {
        if (raid?.Shipment?.Trader == null || raider == null)
            return null;

        return (_cargoPodsProvider?.Invoke() ?? Array.Empty<CargoPod>())
            .OfType<CargoPod>()
            .Where(pod => pod != null && !pod.IsExpired && !pod.IsDepleted && pod.IsStolen &&
                pod.PayloadType == CargoPodPayloadType.Commodity &&
                string.Equals(pod.SourceNpcName, raid.Shipment.Trader.Name, StringComparison.OrdinalIgnoreCase) &&
                Vector3.DistanceSquared(pod.Position, raider.Position) <= MaximumCargoSeekDistance * MaximumCargoSeekDistance)
            .OrderBy(pod => Vector3.DistanceSquared(pod.Position, raider.Position))
            .FirstOrDefault();
    }

    private bool CanAcceptHaul(AmbientPirateRaider raider, CargoPod pod)
    {
        if (raider == null || pod == null || pod.PayloadType != CargoPodPayloadType.Commodity || !pod.IsStolen)
            return false;
        if (raider.MutableHaul.Any(entry => string.Equals(entry?.CommodityId, pod.CommodityId, StringComparison.OrdinalIgnoreCase)))
            return true;
        return raider.MutableHaul.Count < MaximumHaulStacksPerRaider;
    }

    private static void AddHaul(AmbientPirateRaider raider, string commodityId, int quantity)
    {
        AmbientPirateHaulEntry existing = raider.MutableHaul.FirstOrDefault(entry =>
            string.Equals(entry?.CommodityId, commodityId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Quantity = Math.Clamp(existing.Quantity + Math.Max(0, quantity), 0, 40);
            return;
        }

        if (raider.MutableHaul.Count >= MaximumHaulStacksPerRaider || quantity <= 0)
            return;
        raider.MutableHaul.Add(new AmbientPirateHaulEntry
        {
            CommodityId = commodityId ?? string.Empty,
            Quantity = Math.Clamp(quantity, 1, 40)
        });
    }

    private void DropRaiderHaul(AmbientPirateRaid raid, AmbientPirateRaider raider, NpcShip destroyedShip, Action<string> log)
    {
        if (raider == null || destroyedShip == null || _spawnCargo == null)
            return;

        foreach (AmbientPirateHaulEntry entry in raider.MutableHaul.ToList())
        {
            int dropped = Math.Clamp(_spawnCargo(destroyedShip, entry.CommodityId, entry.Quantity), 0, entry.Quantity);
            entry.Quantity -= dropped;
            if (entry.Quantity <= 0)
                raider.MutableHaul.Remove(entry);
            if (dropped > 0)
                log?.Invoke($"[PIRACY] Destroyed raider dropped stolen haul: {entry.CommodityId} x{dropped}.");
        }
    }

    private void UpdateRaiderTargetPriority(AmbientPirateRaid raid)
    {
        foreach (AmbientPirateRaider raider in raid?.Raiders ?? Array.Empty<AmbientPirateRaider>())
        {
            NpcShip ship = raider?.Ship;
            if (ship == null || ship.IsDestroyed || ship.HasAmbientCargoObjective)
                continue;

            NpcShip securityAttacker = raid.Shipment.SecurityDetail?.Members
                .Where(member => member?.Ship != null && !member.Ship.IsDestroyed && member.Ship.FactionCombatTarget == ship)
                .OrderBy(member => Vector3.DistanceSquared(member.Ship.Position, ship.Position))
                .Select(member => member.Ship)
                .FirstOrDefault();
            if (securityAttacker != null)
            {
                ship.SetFactionCombatTarget(securityAttacker, preserveExistingEncounterState: true);
                continue;
            }

            if (ship.HasPlayerTarget)
                continue;
            if (ship.FactionCombatTarget == null || ship.FactionCombatTarget.IsDestroyed)
                ship.SetFactionCombatTarget(raid.Shipment.Trader, targetOrigin: FactionCombatTargetOrigin.AmbientPiracy);
        }
    }

    private void SetMerchantUnderRaid(EconomicShipment shipment)
    {
        if (shipment?.Trader == null || shipment.Trader.IsDestroyed || !shipment.Trader.TrafficRouteStart.HasValue ||
            !shipment.Trader.TrafficRouteEnd.HasValue)
            return;

        Vector3 threat = shipment.Trader.Position;
        AmbientPirateRaid raid = _raids.TryGetValue(shipment.TraderIdentity, out AmbientPirateRaid current) ? current : null;
        NpcShip nearest = raid?.Raiders.FirstOrDefault(raider => raider?.Ship != null && !raider.Ship.IsDestroyed)?.Ship;
        if (nearest != null)
            threat = nearest.Position;
        Vector3 escape = Vector3.DistanceSquared(threat, shipment.Trader.TrafficRouteStart.Value) >=
            Vector3.DistanceSquared(threat, shipment.Trader.TrafficRouteEnd.Value)
            ? shipment.Trader.TrafficRouteStart.Value
            : shipment.Trader.TrafficRouteEnd.Value;
        shipment.Trader.SetEncounterState(TrafficEncounterState.Fleeing, threat, escape);
    }

    private void Resolve(AmbientPirateRaid raid, string reason, Action<string> log)
    {
        if (raid == null || raid.State == AmbientPirateRaidState.Resolved)
            return;

        raid.State = AmbientPirateRaidState.Resolved;
        if (raid.Shipment != null)
        {
            raid.Shipment.AmbientRaidState = AmbientPirateRaidState.Resolved;
            raid.Shipment.AmbientRaidAttempted = true;
        }

        foreach (AmbientPirateRaider raider in raid.MutableRaiders.ToList())
        {
            if (raider?.Ship == null || raider.Ship.IsDestroyed || !raider.IsAlive)
                continue;
            raider.HasEscaped = true;
            raider.IsAlive = false;
            raider.Ship.ClearAmbientPirateRaider();
            _retireRaider?.Invoke(raider.Ship, reason ?? "ambient raid resolved");
            raider.Ship = null;
        }
        log?.Invoke($"[PIRACY] Ambient Rogue raid resolved for {raid.ShipmentIdentity}: {reason}.");
    }

    private bool IsEligibleShipment(EconomicShipment shipment)
    {
        if (shipment == null || shipment.Settlement != EconomicShipmentSettlement.Active ||
            shipment.Trader == null || shipment.Trader.IsDestroyed ||
            shipment.Trader.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute ||
            !IsLawfulCommercialTrader(shipment.Trader) ||
            shipment.Trader.IsTradeLaneTransit || shipment.RemainingQuantity < EconomicShipmentManager.MinimumShipmentQuantity ||
            shipment.Manifest?.Stacks == null || shipment.Manifest.Stacks.Count == 0 ||
            shipment.Manifest.Stacks.Any(stack => stack?.Commodity == null || stack.Commodity.IsContraband || stack.Commodity.IsMissionCargo) ||
            shipment.EscortMissionId > 0 || shipment.InterdictionMissionId > 0 ||
            _isMissionOwned(shipment.Trader))
            return false;

        TrafficZoneConfig currentRoute = (_routesProvider?.Invoke() ?? Array.Empty<TrafficZoneConfig>())
            .FirstOrDefault(route => string.Equals(route?.Id, shipment.RouteId, StringComparison.OrdinalIgnoreCase));
        return currentRoute == null || currentRoute.BehaviorType == TrafficZoneBehaviorType.TraderRoute;
    }

    private static bool IsLawfulCommercialTrader(NpcShip trader)
    {
        string faction = FactionManager.NormalizeFactionId(trader?.FactionId);
        return string.Equals(faction, FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(faction, FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsStillValidBeforeSpawn(EconomicShipment shipment) =>
        IsEligibleShipment(shipment) && !shipment.AmbientRaidSurrendered;

    private int GetRouteRisk(EconomicShipment shipment) =>
        _economicShipments?.GetEffectiveRouteRisk(shipment) ?? shipment?.EffectiveRouteRisk ?? 0;

    private IReadOnlyList<Vector3> BuildInterceptPositions(NpcShip trader, int count)
    {
        List<Vector3> positions = new();
        if (trader == null)
            return positions;

        Vector3 forward = trader.TrafficRouteEnd.HasValue
            ? trader.TrafficRouteEnd.Value - trader.Position
            : trader.Forward;
        if (forward.LengthSquared() < 0.0001f || !TradeLaneStateSanitizer.IsFinite(forward))
            forward = Vector3.Forward;
        forward.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.Up, forward);
        if (lateral.LengthSquared() < 0.0001f)
            lateral = Vector3.Right;
        else
            lateral.Normalize();

        for (int index = 0; index < Math.Clamp(count, 1, MaximumRaidersPerRaid); index++)
        {
            float centered = index - ((Math.Clamp(count, 1, MaximumRaidersPerRaid) - 1) * 0.5f);
            positions.Add(trader.Position + forward * RaiderSpawnDistance + lateral * centered * RaiderSpawnSpacing);
        }
        return positions;
    }

    private SaveAmbientPirateRaidData CaptureRaid(AmbientPirateRaid raid)
    {
        return new SaveAmbientPirateRaidData
        {
            ShipmentIdentity = raid.ShipmentIdentity,
            State = raid.State,
            DelayRemainingSeconds = Math.Clamp(raid.DelayRemainingSeconds, 0f, MaximumDelaySeconds),
            ElapsedSeconds = Math.Clamp(raid.ElapsedSeconds, 0f, RaidTimeoutSeconds),
            PressureSeconds = Math.Clamp(raid.PressureSeconds, 0f, RaidTimeoutSeconds),
            RecentPressureSeconds = Math.Clamp(raid.RecentPressureSeconds, 0f, 10f),
            SurrenderEvaluated = raid.SurrenderEvaluated,
            SurrenderedQuantity = Math.Max(0, raid.SurrenderedQuantity),
            RecoveredQuantity = Math.Max(0, raid.RecoveredQuantity),
            Raiders = raid.Raiders.Take(MaximumRaidersPerRaid).Select(raider => new SaveAmbientPirateRaiderData
            {
                StableIdentity = raider.StableIdentity,
                ArchetypeName = raider.ArchetypeName,
                ModelPath = raider.ModelPath,
                LoadoutTier = raider.LoadoutTier,
                Alive = raider.IsAlive && raider.Ship != null && !raider.Ship.IsDestroyed,
                Escaped = raider.HasEscaped,
                Position = SaveVector3Data.From(raider.Ship?.Position ?? Vector3.Zero),
                Velocity = SaveVector3Data.From(raider.Ship?.Velocity ?? Vector3.Zero),
                Haul = raider.Haul.Select(entry => new SaveAmbientPirateHaulData
                {
                    CommodityId = entry.CommodityId,
                    Quantity = Math.Clamp(entry.Quantity, 1, 40)
                }).Take(MaximumHaulStacksPerRaider).ToList()
            }).ToList()
        };
    }

    private static float NormalizeDelta(float delta) =>
        float.IsNaN(delta) || float.IsInfinity(delta) ? 0f : Math.Clamp(delta, 0f, 60f);

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
