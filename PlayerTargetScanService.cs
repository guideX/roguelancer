using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Roguelancer;

public enum PlayerTargetScanState
{
    Idle,
    Scanning
}

public sealed class PlayerTargetScanCargoEntry
{
    public Commodity Commodity { get; }
    public string CommodityId => Commodity?.Id ?? string.Empty;
    public string DisplayName => Commodity?.Name ?? "Unknown Cargo";
    public int Quantity { get; }
    public bool IsContraband => Commodity?.IsContraband == true;
    public int EstimatedValue { get; }

    public PlayerTargetScanCargoEntry(Commodity commodity, int quantity)
    {
        Commodity = commodity;
        Quantity = Math.Max(0, quantity);
        EstimatedValue = CalculateBaseValue(commodity, Quantity);
    }

    private static int CalculateBaseValue(Commodity commodity, int quantity)
    {
        if (commodity == null || quantity <= 0)
            return 0;

        long value = (long)Math.Max(0, commodity.BasePrice) * quantity;
        return (int)Math.Clamp(value, 0L, int.MaxValue);
    }
}

public sealed class PlayerTargetScanRouteInfo
{
    public string RouteId { get; }
    public string OriginStationId { get; }
    public string OriginStationName { get; }
    public string DestinationStationId { get; }
    public string DestinationStationName { get; }
    public int RouteRisk { get; }
    public string RouteRiskLabel { get; }
    public int SecurityEscortCount { get; }
    public string SecurityLabel => SecurityEscortCount > 0
        ? $"{SecurityEscortCount} escort(s)"
        : "none";
    public string RouteLabel => string.IsNullOrWhiteSpace(OriginStationName) || string.IsNullOrWhiteSpace(DestinationStationName)
        ? string.Empty
        : $"{OriginStationName} -> {DestinationStationName}";

    public PlayerTargetScanRouteInfo(
        string routeId,
        string originStationId,
        string originStationName,
        string destinationStationId,
        string destinationStationName,
        int routeRisk = 0,
        string routeRiskLabel = null,
        int securityEscortCount = 0)
    {
        RouteId = routeId ?? string.Empty;
        OriginStationId = originStationId ?? string.Empty;
        OriginStationName = originStationName ?? string.Empty;
        DestinationStationId = destinationStationId ?? string.Empty;
        DestinationStationName = destinationStationName ?? string.Empty;
        RouteRisk = Math.Clamp(routeRisk, 0, TradeRouteRiskManager.MaximumRiskScore);
        RouteRiskLabel = string.IsNullOrWhiteSpace(routeRiskLabel)
            ? TradeRouteRiskManager.GetRiskLabel(RouteRisk)
            : routeRiskLabel.Trim();
        SecurityEscortCount = Math.Clamp(securityEscortCount, 0, ShipmentSecurityManager.MaximumEscortsPerShipment);
    }
}

public sealed class PlayerTargetScanResult
{
    private readonly List<PlayerTargetScanCargoEntry> _cargo;

    /// <summary>Transient runtime reference; never serialized.</summary>
    public NpcShip Target { get; }
    public string TargetIdentity { get; }
    public string TargetName { get; }
    public string FactionId { get; }
    public string FactionLabel { get; }
    public string ShipTypeLabel { get; }
    public float HullPercentage { get; }
    public float ShieldPercentage { get; }
    public bool HasRegisteredCargo { get; }
    public IReadOnlyList<PlayerTargetScanCargoEntry> Cargo => _cargo;
    public PlayerTargetScanRouteInfo Route { get; }
    public bool HasRoute => Route != null && !string.IsNullOrWhiteSpace(Route.RouteLabel);
    public string OriginStationName => Route?.OriginStationName ?? string.Empty;
    public string DestinationStationName => Route?.DestinationStationName ?? string.Empty;
    public string RouteLabel => Route?.RouteLabel ?? string.Empty;
    public int SecurityEscortCount => Route?.SecurityEscortCount ?? 0;
    public string SecurityLabel => Route?.SecurityLabel ?? "unknown";
    public int EstimatedCargoValue =>
        (int)Math.Clamp(_cargo.Sum(entry => (long)Math.Max(0, entry.EstimatedValue)), 0L, int.MaxValue);
    public bool IsCargoHoldEmpty => HasRegisteredCargo && _cargo.Count == 0;

    public string CargoStatusLabel => !HasRegisteredCargo
        ? "No registered cargo"
        : IsCargoHoldEmpty
            ? "Cargo hold empty"
            : "Cargo manifest available";

    internal PlayerTargetScanResult(
        NpcShip target,
        string targetIdentity,
        FactionManager factionManager,
        NpcCargoManifestSnapshot cargoSnapshot,
        PlayerTargetScanRouteInfo routeInfo = null)
    {
        Target = target;
        TargetIdentity = targetIdentity ?? string.Empty;
        TargetName = target?.Name ?? "Unknown Target";
        FactionId = FactionManager.NormalizeFactionId(target?.FactionId);
        FactionLabel = (factionManager ?? new FactionManager()).GetFaction(FactionId).DisplayName;
        ShipTypeLabel = ResolveShipTypeLabel(target);
        HullPercentage = ReadPercentage(target?.Hull?.CurrentHull ?? 0f, target?.Hull?.MaxHull ?? 0f);
        ShieldPercentage = ReadPercentage(target?.Shields?.CurrentShields ?? 0f, target?.Shields?.MaxShields ?? 0f);
        HasRegisteredCargo = cargoSnapshot?.HasRegisteredCargo == true;
        Route = routeInfo;
        _cargo = cargoSnapshot?.Stacks?
            .Where(stack => stack?.Commodity != null && stack.Quantity > 0)
            .Select(stack => new PlayerTargetScanCargoEntry(stack.Commodity, stack.Quantity))
            .ToList()
            ?? new List<PlayerTargetScanCargoEntry>();
    }

    private static float ReadPercentage(float current, float maximum)
    {
        if (maximum <= 0f || float.IsNaN(current) || float.IsInfinity(current))
            return 0f;

        return Math.Clamp(current / maximum, 0f, 1f);
    }

    private static string ResolveShipTypeLabel(NpcShip target)
    {
        if (target == null || string.IsNullOrWhiteSpace(target.ModelPath))
            return "NPC Ship";

        string filename = Path.GetFileNameWithoutExtension(target.ModelPath.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(filename))
            return "NPC Ship";

        string[] words = filename
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? "NPC Ship"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(string.Join(" ", words));
    }
}

/// <summary>
/// Owns exactly one bounded, transient player-initiated scan. It reads NPC
/// state only at validation and completion; it never mutates cargo, markets,
/// reputation, Police state, or mission state.
/// </summary>
public sealed class PlayerTargetScanService
{
    public const float ScannerRange = 4000f;
    public const float ScanDurationSeconds = 3f;

    private sealed class ActiveScan
    {
        public NpcShip Target { get; init; }
        public string TargetIdentity { get; init; } = string.Empty;
        public float ElapsedSeconds { get; set; }
    }

    private readonly IReadOnlyList<NpcShip> _npcShips;
    private readonly FactionManager _factionManager;
    private readonly Func<NpcShip, NpcCargoManifestSnapshot> _cargoResolver;
    private readonly Func<NpcShip, PlayerTargetScanRouteInfo> _routeResolver;
    private ActiveScan _activeScan;
    private PlayerTargetScanResult _lastResult;
    private string _feedbackText = string.Empty;
    private float _feedbackRemainingSeconds;

    public PlayerTargetScanService(
        IReadOnlyList<NpcShip> npcShips,
        FactionManager factionManager,
        Func<NpcShip, NpcCargoManifestSnapshot> cargoResolver = null,
        Func<NpcShip, PlayerTargetScanRouteInfo> routeResolver = null)
    {
        _npcShips = npcShips ?? throw new ArgumentNullException(nameof(npcShips));
        _factionManager = factionManager ?? new FactionManager();
        _cargoResolver = cargoResolver;
        _routeResolver = routeResolver;
    }

    public PlayerTargetScanState State => _activeScan == null
        ? PlayerTargetScanState.Idle
        : PlayerTargetScanState.Scanning;
    public bool HasActiveScan => _activeScan != null;
    public NpcShip ActiveTarget => _activeScan?.Target;
    public string ActiveTargetIdentity => _activeScan?.TargetIdentity ?? string.Empty;
    public float ProgressSeconds => Math.Clamp(_activeScan?.ElapsedSeconds ?? 0f, 0f, ScanDurationSeconds);
    public float ProgressRatio => ScanDurationSeconds <= 0f
        ? 0f
        : Math.Clamp(ProgressSeconds / ScanDurationSeconds, 0f, 1f);
    public PlayerTargetScanResult LastResult => _lastResult;
    public string FeedbackText => _feedbackRemainingSeconds > 0f ? _feedbackText : string.Empty;

    public bool TryStartScan(
        Ship playerShip,
        NpcShip target,
        bool playerInTradeLaneTransit,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (_activeScan != null)
        {
            failureReason = "a target scan is already active";
            SetFeedback(failureReason, 2f);
            return false;
        }

        if (!IsValidTarget(target))
        {
            failureReason = target?.IsDestroyed == true ? "target is destroyed" : "no valid target to scan";
            SetFeedback(failureReason, 2f);
            return false;
        }

        if (playerShip == null || playerShip.Hull?.IsDestroyed == true)
        {
            failureReason = "player ship is invalid";
            SetFeedback(failureReason, 2f);
            return false;
        }

        if (playerInTradeLaneTransit || playerShip.IsTradeLaneTransit)
        {
            failureReason = "player trade-lane transit prevents scanning";
            SetFeedback(failureReason, 2f);
            return false;
        }

        if (target.IsTradeLaneTransit)
        {
            failureReason = "target is in trade-lane transit";
            SetFeedback(failureReason, 2f);
            return false;
        }

        if (Vector3.Distance(playerShip.Position, target.Position) > ScannerRange)
        {
            failureReason = $"target outside scanner range ({ScannerRange:0} m)";
            SetFeedback(failureReason, 2f);
            return false;
        }

        _activeScan = new ActiveScan
        {
            Target = target,
            TargetIdentity = NpcIdentity.GetStableIdentity(target),
            ElapsedSeconds = 0f
        };
        ClearFeedback();
        return true;
    }

    public void Update(
        float deltaSeconds,
        Ship playerShip,
        NpcShip currentTarget,
        bool playerInTradeLaneTransit)
    {
        float delta = NormalizeDelta(deltaSeconds);
        _feedbackRemainingSeconds = Math.Max(0f, _feedbackRemainingSeconds - delta);
        if (_activeScan == null)
            return;

        ActiveScan scan = _activeScan;
        NpcShip target = scan.Target;
        if (target == null || currentTarget == null || !ReferenceEquals(currentTarget, target) ||
            !string.Equals(NpcIdentity.GetStableIdentity(target), scan.TargetIdentity, StringComparison.Ordinal) ||
            !_npcShips.Contains(target))
        {
            CancelActiveScan("target lost; scan canceled");
            return;
        }

        if (target.IsDestroyed)
        {
            CancelActiveScan("target destroyed; scan canceled");
            return;
        }

        if (playerShip == null || playerShip.Hull?.IsDestroyed == true)
        {
            CancelActiveScan("player state invalid; scan canceled");
            return;
        }

        if (playerInTradeLaneTransit || playerShip.IsTradeLaneTransit)
        {
            CancelActiveScan("player entered trade-lane transit; scan canceled");
            return;
        }

        if (target.IsTradeLaneTransit)
        {
            CancelActiveScan("target entered trade-lane transit; scan canceled");
            return;
        }

        if (Vector3.DistanceSquared(playerShip.Position, target.Position) > ScannerRange * ScannerRange)
        {
            CancelActiveScan("target left scanner range; scan canceled");
            return;
        }

        scan.ElapsedSeconds = Math.Min(ScanDurationSeconds, scan.ElapsedSeconds + delta);
        if (scan.ElapsedSeconds < ScanDurationSeconds)
            return;

        NpcCargoManifestSnapshot cargoSnapshot = _cargoResolver?.Invoke(target) ??
            NpcCargoManifestSnapshot.NoRegisteredCargo();
        PlayerTargetScanRouteInfo routeInfo = _routeResolver?.Invoke(target);
        _lastResult = new PlayerTargetScanResult(
            target,
            scan.TargetIdentity,
            _factionManager,
            cargoSnapshot,
            routeInfo);
        _activeScan = null;
        SetFeedback("CARGO SCAN COMPLETE", 4f);
    }

    public void NotifyTargetChanged(NpcShip currentTarget)
    {
        if (_activeScan != null && !ReferenceEquals(_activeScan.Target, currentTarget))
            CancelActiveScan("target changed; scan canceled");

        if (_lastResult != null && !ReferenceEquals(_lastResult.Target, currentTarget))
            _lastResult = null;
    }

    public void NotifyTargetDestroyed(NpcShip target)
    {
        if (target == null)
            return;

        if (_activeScan != null && ReferenceEquals(_activeScan.Target, target))
            CancelActiveScan("target destroyed; scan canceled");

        if (_lastResult != null && ReferenceEquals(_lastResult.Target, target))
            _lastResult = null;
    }

    public PlayerTargetScanResult GetResultFor(NpcShip target)
    {
        return target != null && _lastResult != null && ReferenceEquals(_lastResult.Target, target)
            ? _lastResult
            : null;
    }

    public void CancelActiveScan(string reason = "scan canceled")
    {
        if (_activeScan == null)
            return;

        _activeScan = null;
        SetFeedback(reason, 2f);
    }

    public void Reset()
    {
        _activeScan = null;
        _lastResult = null;
        ClearFeedback();
    }

    private bool IsValidTarget(NpcShip target) =>
        target != null && !target.IsDestroyed && _npcShips.Contains(target);

    private void SetFeedback(string text, float seconds)
    {
        _feedbackText = text ?? string.Empty;
        _feedbackRemainingSeconds = Math.Max(0f, seconds);
    }

    private void ClearFeedback()
    {
        _feedbackText = string.Empty;
        _feedbackRemainingSeconds = 0f;
    }

    private static float NormalizeDelta(float deltaSeconds) =>
        float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)
            ? 0f
            : Math.Clamp(deltaSeconds, 0f, 60f);
}
