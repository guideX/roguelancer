using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// One member of a real shipment security detail. The runtime ship is a
/// normal NpcShip; the durable fields are also sufficient to reconstruct it
/// without serializing object references.
/// </summary>
public sealed class ShipmentSecurityMember
{
    internal ShipmentSecurityMember(
        string stableIdentity,
        string factionId,
        string archetypeName,
        string modelPath,
        NpcLoadoutTier loadoutTier,
        Vector3 formationOffset)
    {
        StableIdentity = stableIdentity ?? string.Empty;
        FactionId = FactionManager.NormalizeFactionId(factionId);
        ArchetypeName = archetypeName ?? string.Empty;
        ModelPath = modelPath ?? string.Empty;
        LoadoutTier = loadoutTier;
        FormationOffset = formationOffset;
        IsAlive = true;
    }

    public string StableIdentity { get; }
    public string FactionId { get; }
    public string ArchetypeName { get; }
    public string ModelPath { get; internal set; }
    public NpcLoadoutTier LoadoutTier { get; }
    public Vector3 FormationOffset { get; }
    public bool IsAlive { get; internal set; }
    public NpcShip Ship { get; internal set; }
    internal Vector3 LastPosition { get; set; }
    internal Vector3 LastVelocity { get; set; }

    public bool IsActive => IsAlive && Ship != null && !Ship.IsDestroyed;
}

/// <summary>
/// Bounded ambient protection attached to one EconomicShipment. It is not a
/// mission and owns no cargo; the shipment remains the sole economic owner.
/// </summary>
public sealed class ShipmentSecurityDetail
{
    private readonly List<ShipmentSecurityMember> _members = new();

    internal ShipmentSecurityDetail(EconomicShipment shipment)
    {
        ShipmentIdentity = shipment?.TraderIdentity ?? string.Empty;
        Shipment = shipment;
    }

    public string ShipmentIdentity { get; }
    public EconomicShipment Shipment { get; internal set; }
    public IReadOnlyList<ShipmentSecurityMember> Members => _members;
    public int ActiveEscortCount => _members.Count(member => member?.IsActive == true);
    public int MemberCount => _members.Count;
    public bool IsComplete { get; internal set; }

    internal void Add(ShipmentSecurityMember member)
    {
        if (member != null && !_members.Any(existing =>
                string.Equals(existing.StableIdentity, member.StableIdentity, StringComparison.Ordinal)))
        {
            _members.Add(member);
        }
    }
}

/// <summary>
/// Deterministic, one-shot autonomous security policy. The manager deliberately
/// does not update weapons or damage: those responsibilities remain with the
/// existing TrafficManager, NpcShip, NpcFactionCombatTargeting, and
/// NpcWeaponSystem paths.
/// </summary>
public sealed class ShipmentSecurityManager
{
    public const int MinimumRouteRisk = TradeRouteRiskManager.DangerousRiskThreshold;
    public const int MinimumManifestValue = 4_000;
    public const int MaximumEscortsPerShipment = 2;
    public const int SevereRiskThreshold = TradeRouteRiskManager.SevereRiskThreshold;
    public const float ThreatRange = 7_000f;

    public delegate NpcShip SecuritySpawnFactory(
        EconomicShipment shipment,
        ShipmentSecurityMember member,
        Vector3 position);

    private readonly Dictionary<string, ShipmentSecurityDetail> _details = new(StringComparer.Ordinal);
    private SecuritySpawnFactory _spawnFactory;
    private Action<NpcShip> _registerFactory;
    private Action<NpcShip, string> _retireFactory;
    private Func<EconomicShipment, string> _factionResolver;
    private Func<EconomicShipment, Commodity, bool> _criticalShortageResolver;

    /// <summary>
    /// Configurable default-value gate for a one-shot security assignment.
    /// Runtime callers may tune the policy, but the value remains bounded to
    /// the intended Phase 65 3,000-5,000 CR band.
    /// </summary>
    public int ManifestValueThreshold { get; set; } = MinimumManifestValue;

    public IReadOnlyList<ShipmentSecurityDetail> ActiveDetails => _details.Values
        .Where(detail => detail != null && !detail.IsComplete)
        .ToList();

    public int ActiveDetailCount => ActiveDetails.Count;

    public void Configure(
        SecuritySpawnFactory spawnFactory,
        Action<NpcShip> registerFactory,
        Action<NpcShip, string> retireFactory,
        Func<EconomicShipment, string> factionResolver = null,
        Func<EconomicShipment, Commodity, bool> criticalShortageResolver = null)
    {
        _spawnFactory = spawnFactory;
        _registerFactory = registerFactory;
        _retireFactory = retireFactory;
        _factionResolver = factionResolver;
        _criticalShortageResolver = criticalShortageResolver;
    }

    public bool TryGetDetail(EconomicShipment shipment, out ShipmentSecurityDetail detail)
    {
        detail = null;
        return shipment != null && TryGetDetail(shipment.TraderIdentity, out detail);
    }

    public bool TryGetDetail(string shipmentIdentity, out ShipmentSecurityDetail detail)
    {
        detail = null;
        return !string.IsNullOrWhiteSpace(shipmentIdentity) &&
            _details.TryGetValue(shipmentIdentity.Trim(), out detail) &&
            detail != null && !detail.IsComplete;
    }

    public bool IsSecurityEscort(NpcShip ship) =>
        ship != null && ship.IsShipmentSecurityEscort &&
        _details.Values.Any(detail => detail?.Members.Any(member => member?.Ship == ship) == true);

    /// <summary>
    /// Evaluates exactly once for a shipment. A non-qualifying shipment still
    /// records the decision on EconomicShipment, preventing save/load or a
    /// future update from repeatedly reconsidering it.
    /// </summary>
    public bool TryEvaluateAndAssign(EconomicShipment shipment, Action<string> log = null)
    {
        if (shipment == null || shipment.Settlement != EconomicShipmentSettlement.Active ||
            string.IsNullOrWhiteSpace(shipment.TraderIdentity) || shipment.SecurityAssignmentDecided)
        {
            return false;
        }

        shipment.SecurityAssignmentDecided = true;
        if (!Qualifies(shipment, out int requestedCount))
            return false;

        ShipmentSecurityDetail detail = new(shipment);
        _details[shipment.TraderIdentity] = detail;
        shipment.SecurityDetail = detail;
        string factionId = FactionManager.NormalizeFactionId(
            _factionResolver?.Invoke(shipment) ?? FactionManager.LibertyCorporations);

        for (int index = 0; index < requestedCount; index++)
        {
            Vector3 offset = index == 0
                ? new Vector3(-320f, 0f, 420f)
                : new Vector3(320f, 0f, 420f);
            NpcLoadoutTier tier = requestedCount > 1 && index == 1
                ? NpcLoadoutTier.High
                : NpcLoadoutTier.Standard;
            ShipmentSecurityMember member = new(
                BuildMemberIdentity(shipment.TraderIdentity, index),
                factionId,
                "Patrol Fighter 1",
                string.Empty,
                tier,
                offset);
            member.LastPosition = GetFormationPosition(shipment.Trader, offset);
            member.LastVelocity = Vector3.Zero;

            if (!TrySpawnMember(shipment, detail, member, member.LastPosition, log))
                break;
        }

        if (detail.MemberCount == 0)
        {
            _details.Remove(shipment.TraderIdentity);
            shipment.SecurityDetail = null;
        }

        return detail.MemberCount > 0;
    }

    /// <summary>
    /// Reconstructs only the persisted members. This path never calls the
    /// qualification policy and therefore cannot hire replacements on load.
    /// Dead records remain in the detail as durable no-replacement evidence.
    /// </summary>
    public bool RestoreShipmentSecurity(EconomicShipment shipment, SaveEconomicShipmentData state, Action<string> log = null)
    {
        if (shipment == null || state == null || !state.SecurityAssignmentDecided ||
            shipment.Settlement != EconomicShipmentSettlement.Active)
            return false;

        shipment.SecurityAssignmentDecided = true;
        _details.Remove(shipment.TraderIdentity);
        shipment.SecurityDetail = null;
        if (state.SecurityMembers == null || state.SecurityMembers.Count == 0)
            return false;

        ShipmentSecurityDetail detail = new(shipment);
        _details[shipment.TraderIdentity] = detail;
        shipment.SecurityDetail = detail;
        foreach (SaveShipmentSecurityMemberData saved in state.SecurityMembers.Take(MaximumEscortsPerShipment))
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.StableIdentity))
                continue;

            Vector3 offset = saved.FormationOffset?.ToVector3(Vector3.Zero) ?? Vector3.Zero;
            if (!TradeLaneStateSanitizer.IsFinite(offset))
                offset = Vector3.Zero;
            NpcLoadoutTier tier = Enum.IsDefined(typeof(NpcLoadoutTier), saved.LoadoutTier)
                ? saved.LoadoutTier
                : NpcLoadoutTier.Standard;
            ShipmentSecurityMember member = new(
                saved.StableIdentity.Trim(),
                string.IsNullOrWhiteSpace(saved.FactionId) ? FactionManager.LibertyCorporations : saved.FactionId,
                string.IsNullOrWhiteSpace(saved.ArchetypeName) ? "Patrol Fighter 1" : saved.ArchetypeName,
                saved.ModelPath ?? string.Empty,
                tier,
                offset);
            member.IsAlive = saved.Alive;
            member.LastPosition = saved.Position?.ToVector3(GetFormationPosition(shipment.Trader, offset)) ??
                GetFormationPosition(shipment.Trader, offset);
            member.LastVelocity = saved.Velocity?.ToVector3(Vector3.Zero) ?? Vector3.Zero;
            detail.Add(member);

            if (!member.IsAlive)
                continue;

            if (!TrySpawnMember(shipment, detail, member, member.LastPosition, log))
                member.IsAlive = false;
        }

        if (detail.MemberCount == 0)
        {
            _details.Remove(shipment.TraderIdentity);
            shipment.SecurityDetail = null;
        }
        return detail.MemberCount > 0;
    }

    public IReadOnlyList<SaveShipmentSecurityMemberData> CaptureState(EconomicShipment shipment)
    {
        if (!TryGetDetail(shipment, out ShipmentSecurityDetail detail))
            return Array.Empty<SaveShipmentSecurityMemberData>();

        return detail.Members
            .Take(MaximumEscortsPerShipment)
            .Select(member => new SaveShipmentSecurityMemberData
            {
                StableIdentity = member.StableIdentity,
                FactionId = member.FactionId,
                ArchetypeName = member.ArchetypeName,
                ModelPath = member.ModelPath,
                LoadoutTier = member.LoadoutTier,
                Alive = member.IsAlive && member.Ship != null && !member.Ship.IsDestroyed,
                FormationOffset = SaveVector3Data.From(member.FormationOffset),
                Position = SaveVector3Data.From(member.Ship?.Position ?? member.LastPosition),
                Velocity = SaveVector3Data.From(member.Ship?.Velocity ?? member.LastVelocity)
            })
            .ToList();
    }

    /// <summary>
    /// Returns the merchant's credible current attacker for the shared faction
    /// combat pass. It is deliberately local and requires a target relationship
    /// to the protected trader; distant unrelated Rogues are ignored.
    /// </summary>
    public NpcShip GetPriorityThreat(NpcShip securityShip, IReadOnlyList<NpcShip> candidates)
    {
        if (securityShip == null || candidates == null || !TryGetMember(securityShip, out ShipmentSecurityMember member))
            return null;
        EconomicShipment shipment = _details.Values
            .FirstOrDefault(detail => detail?.Members.Contains(member) == true)
            ?.Shipment;
        NpcShip trader = shipment?.Trader;
        if (trader == null || trader.IsDestroyed)
            return null;

        NpcShip preferred = candidates
            .Where(candidate => IsCredibleMerchantThreat(candidate, trader) &&
                NpcFactionCombatTargeting.IsHostileFactionPair(securityShip.FactionId, candidate?.FactionId))
            .OrderBy(candidate => Vector3.DistanceSquared(securityShip.Position, candidate.Position))
            .ThenBy(candidate => candidate.Name ?? string.Empty, StringComparer.Ordinal)
            .FirstOrDefault();
        return preferred != null && Vector3.DistanceSquared(securityShip.Position, preferred.Position) <=
            ThreatRange * ThreatRange ? preferred : null;
    }

    public void NotifyNpcDestroyed(NpcShip destroyedShip)
    {
        if (destroyedShip == null)
            return;

        foreach (ShipmentSecurityDetail detail in _details.Values.ToList())
        {
            if (detail == null || detail.IsComplete)
                continue;

            if (detail.Shipment?.Trader == destroyedShip)
            {
                CompleteDetail(detail, "merchant destroyed");
                continue;
            }

            ShipmentSecurityMember member = detail.Members.FirstOrDefault(candidate => candidate?.Ship == destroyedShip);
            if (member == null)
                continue;

            member.LastPosition = destroyedShip.Position;
            member.LastVelocity = destroyedShip.Velocity;
            member.IsAlive = false;
            member.Ship = null;
            _retireFactory?.Invoke(destroyedShip, "security destroyed");
        }
    }

    public void NotifyShipmentSettled(EconomicShipment shipment, string reason)
    {
        if (shipment != null && TryGetDetail(shipment, out ShipmentSecurityDetail detail))
            CompleteDetail(detail, reason ?? "shipment settled");
    }

    public void Reset()
    {
        foreach (ShipmentSecurityDetail detail in _details.Values.ToList())
            CompleteDetail(detail, "world reset");
        _details.Clear();
    }

    private bool Qualifies(EconomicShipment shipment, out int escortCount)
    {
        escortCount = 0;
        if (shipment?.Trader == null || shipment.Trader.IsDestroyed ||
            shipment.RemainingQuantity < EconomicShipmentManager.MinimumShipmentQuantity ||
            GetEffectiveRisk(shipment) < MinimumRouteRisk ||
            shipment.Manifest?.Stacks == null ||
            shipment.Manifest.Stacks.All(stack => stack?.Commodity == null ||
                stack.Commodity.IsContraband || stack.Commodity.IsMissionCargo))
        {
            return false;
        }

        int valueThreshold = Math.Clamp(ManifestValueThreshold, 3_000, 5_000);
        bool valuable = shipment.RemainingManifestValue >= valueThreshold;
        bool strategicShortage = IsStrategicShortage(shipment);
        if (!valuable && !strategicShortage)
            return false;

        escortCount = GetEffectiveRisk(shipment) >= SevereRiskThreshold ? 2 : 1;
        return Math.Clamp(escortCount, 0, MaximumEscortsPerShipment) > 0;
    }

    private bool IsStrategicShortage(EconomicShipment shipment)
    {
        return shipment != null && shipment.Manifest?.Stacks != null &&
            shipment.Manifest.Stacks.Any(stack => stack?.Commodity != null &&
                !stack.Commodity.IsContraband && !stack.Commodity.IsMissionCargo &&
                _criticalShortageResolver?.Invoke(shipment, stack.Commodity) == true);
    }

    private static int GetEffectiveRisk(EconomicShipment shipment) =>
        shipment == null ? 0 : shipment.EffectiveRouteRisk;

    private bool TrySpawnMember(
        EconomicShipment shipment,
        ShipmentSecurityDetail detail,
        ShipmentSecurityMember member,
        Vector3 position,
        Action<string> log)
    {
        if (_spawnFactory == null || shipment?.Trader == null || member == null ||
            !TradeLaneStateSanitizer.IsFinite(position))
            return false;

        NpcShip securityShip = null;
        try
        {
            securityShip = _spawnFactory(shipment, member, position);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[SECURITY] Spawn failed for {member.StableIdentity}: {ex.Message}");
        }

        if (securityShip == null)
            return false;

        member.Ship = securityShip;
        member.IsAlive = true;
        member.LastPosition = securityShip.Position;
        member.LastVelocity = securityShip.Velocity;
        if (!string.IsNullOrWhiteSpace(securityShip.ModelPath))
            member.ModelPath = securityShip.ModelPath;
        detail.Add(member);
        securityShip.MarkShipmentSecurityEscort(shipment.TraderIdentity);
        securityShip.ConfigureFormationFollower(shipment.Trader, member.FormationOffset);
        try
        {
            _registerFactory?.Invoke(securityShip);
        }
        catch (Exception ex)
        {
            securityShip.ClearShipmentSecurityEscort();
            member.Ship = null;
            member.IsAlive = false;
            _retireFactory?.Invoke(securityShip, "security registration failed");
            log?.Invoke($"[SECURITY] Registration failed for {member.StableIdentity}: {ex.Message}");
            return false;
        }
        log?.Invoke($"[SECURITY] Hired {securityShip.Name} for {shipment.Trader.Name} ({shipment.TraderIdentity}).");
        return true;
    }

    private void CompleteDetail(ShipmentSecurityDetail detail, string reason)
    {
        if (detail == null || detail.IsComplete)
            return;

        detail.IsComplete = true;
        foreach (ShipmentSecurityMember member in detail.Members)
        {
            if (member?.Ship == null)
                continue;
            member.Ship.ClearShipmentSecurityEscort();
            _retireFactory?.Invoke(member.Ship, reason ?? "shipment settled");
            member.Ship = null;
        }
        if (!string.IsNullOrWhiteSpace(detail.ShipmentIdentity))
            _details.Remove(detail.ShipmentIdentity);
        if (detail.Shipment != null)
            detail.Shipment.SecurityDetail = null;
    }

    private bool TryGetMember(NpcShip ship, out ShipmentSecurityMember member)
    {
        member = null;
        if (ship == null)
            return false;
        foreach (ShipmentSecurityDetail detail in _details.Values)
        {
            member = detail?.Members.FirstOrDefault(candidate => candidate?.Ship == ship);
            if (member != null)
                return true;
        }
        return false;
    }

    private bool IsCredibleMerchantThreat(NpcShip candidate, NpcShip trader)
    {
        if (candidate == null || candidate.IsDestroyed || candidate == trader)
            return false;
        if (candidate.FactionCombatTarget == trader)
            return true;
        if (candidate.EncounterState != TrafficEncounterState.AttackingTrader)
            return false;
        if (!candidate.EncounterTargetPosition.HasValue)
            return false;
        return Vector3.DistanceSquared(candidate.EncounterTargetPosition.Value, trader.Position) <=
            ThreatRange * ThreatRange;
    }

    private static Vector3 GetFormationPosition(NpcShip trader, Vector3 offset)
    {
        if (trader == null)
            return Vector3.Zero;
        return trader.Position + trader.Right * offset.X + trader.Up * offset.Y - trader.Forward * offset.Z;
    }

    private static string BuildMemberIdentity(string shipmentIdentity, int index) =>
        $"{shipmentIdentity}|security:{index + 1}";
}
