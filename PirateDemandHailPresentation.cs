using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Roguelancer;

/// <summary>
/// One bounded demanded-commodity line in a pirate hail. The commodity name
/// and quantity are copied verbatim from the authoritative Phase 81 demand;
/// this presentation never selects, re-rolls, or re-prices cargo.
/// </summary>
public sealed class PirateDemandHailCargoLine
{
    public PirateDemandHailCargoLine(string commodityName, int quantity)
    {
        CommodityName = commodityName ?? string.Empty;
        Quantity = quantity;
    }

    public string CommodityName { get; }
    public int Quantity { get; }
    public string DisplayText => $"{CommodityName} x{Quantity}";
}

/// <summary>
/// Immutable snapshot of the active Phase 81 demand for rendering. It carries
/// only presentation-ready values read from the service at snapshot time; it
/// is never retained across frames and never owns demand state.
/// </summary>
public sealed class PirateDemandHailSnapshot
{
    public PirateDemandHailSnapshot(
        string factionLabel,
        string factionId,
        string speakerName,
        IReadOnlyList<PirateDemandHailCargoLine> cargoLines,
        float responseRemainingSeconds,
        Keys complyKey,
        Keys refuseKey)
    {
        FactionLabel = factionLabel ?? string.Empty;
        FactionId = factionId ?? string.Empty;
        SpeakerName = speakerName ?? string.Empty;
        CargoLines = cargoLines ?? Array.Empty<PirateDemandHailCargoLine>();
        ResponseRemainingSeconds = Math.Max(0f, responseRemainingSeconds);
        ComplyKey = complyKey;
        RefuseKey = refuseKey;
    }

    public string FactionLabel { get; }
    public string FactionId { get; }
    public string SpeakerName { get; }
    public IReadOnlyList<PirateDemandHailCargoLine> CargoLines { get; }
    public float ResponseRemainingSeconds { get; }
    public Keys ComplyKey { get; }
    public Keys RefuseKey { get; }

    public string HeaderText => $"INCOMING HAIL — {FactionLabel}";
    public string SpeakerText => $"{SpeakerName} ({FactionLabel})";
    public string TitleText => "CARGO DEMAND";
    public string CountdownText => $"Respond within {ResponseRemainingSeconds:0.0} s";
    public string ComplyText => $"[{ComplyKey}] COMPLY";
    public string RefuseText => $"[{RefuseKey}] REFUSE";
}

/// <summary>
/// Screen-space layout for the hail panel. Shared by rendering and mouse hit
/// testing so a click can only ever land on the same control that was drawn.
/// </summary>
public readonly struct PirateDemandHailLayout
{
    public PirateDemandHailLayout(Rectangle panel, Rectangle complyButton, Rectangle refuseButton)
    {
        Panel = panel;
        ComplyButton = complyButton;
        RefuseButton = refuseButton;
    }

    public Rectangle Panel { get; }
    public Rectangle ComplyButton { get; }
    public Rectangle RefuseButton { get; }
}

/// <summary>
/// Presentation-only adapter for the authoritative Phase 81 NPC pirate cargo
/// demand. It reads active-demand state and delegates comply/refuse back to
/// <see cref="NpcPirateCargoDemandService"/> exactly once per response. It
/// owns no demand, cargo, timer, combat, or persistence state and creates no
/// parallel transaction path.
/// </summary>
public sealed class PirateDemandHailPresentation
{
    public const int PanelWidth = 470;
    public const int PanelPadding = 12;
    public const int HeaderLineHeight = 20;
    public const int CargoLineHeight = 18;
    public const int ButtonHeight = 26;
    public const int ButtonWidth = 150;
    public const int ButtonGap = 12;

    private readonly NpcPirateCargoDemandService _demandService;

    public PirateDemandHailPresentation(NpcPirateCargoDemandService demandService)
    {
        _demandService = demandService ?? throw new ArgumentNullException(nameof(demandService));
    }

    /// <summary>
    /// True only while the Phase 81 service reports an active demand. There is
    /// no cached visibility flag, so a cancelled, timed-out, or destroyed
    /// demand disappears immediately.
    /// </summary>
    public bool IsVisible => _demandService.HasActiveDemand;

    /// <summary>
    /// Builds a bounded snapshot of the active demand. Returns false when there
    /// is no active demand, the demander is gone, or the authoritative demand
    /// has no positive cargo lines.
    /// </summary>
    public bool TryBuildSnapshot(out PirateDemandHailSnapshot snapshot)
    {
        snapshot = null;
        if (!_demandService.HasActiveDemand)
            return false;

        NpcShip demander = _demandService.ActiveDemander;
        if (demander == null || demander.IsDestroyed)
            return false;

        List<PirateDemandHailCargoLine> cargoLines = new();
        foreach (KeyValuePair<string, int> entry in _demandService.ActiveDemandQuantities)
        {
            if (cargoLines.Count >= NpcPirateCargoDemandService.MaximumCommodityTypes)
                break;
            if (entry.Value <= 0 || string.IsNullOrWhiteSpace(entry.Key))
                continue;
            cargoLines.Add(new PirateDemandHailCargoLine(entry.Key, entry.Value));
        }

        if (cargoLines.Count == 0)
            return false;

        cargoLines.Sort((left, right) =>
            string.Compare(left.CommodityName, right.CommodityName, StringComparison.OrdinalIgnoreCase));

        string factionLabel = FactionManager.GetFactionDisplayName(demander.FactionId);
        string speaker = string.IsNullOrWhiteSpace(demander.Name) ? factionLabel : demander.Name;

        snapshot = new PirateDemandHailSnapshot(
            factionLabel,
            demander.FactionId,
            speaker,
            cargoLines,
            _demandService.ResponseRemainingSeconds,
            NpcPirateCargoDemandService.ComplyKey,
            NpcPirateCargoDemandService.RefuseKey);
        return true;
    }

    /// <summary>
    /// Routes a comply UI action through the authoritative Phase 81 compliance
    /// path. Reuses the service's idempotent edge-triggered authority, so a
    /// repeated or held click cannot remove cargo twice.
    /// </summary>
    public bool TryComply(Ship playerShip, out NpcPirateCargoDemandResult result) =>
        _demandService.TryComply(playerShip, out result);

    /// <summary>
    /// Routes a refuse UI action through the authoritative Phase 81 refusal
    /// path. A repeated or held click cannot escalate combat twice.
    /// </summary>
    public bool TryRefuse(Ship playerShip, out NpcPirateCargoDemandResult result) =>
        _demandService.TryRefuse(playerShip, out result);

    /// <summary>
    /// Computes the shared panel/button layout for a given cargo-line count.
    /// Bounded by <see cref="NpcPirateCargoDemandService.MaximumCommodityTypes"/>.
    /// </summary>
    public static PirateDemandHailLayout BuildLayout(int viewportWidth, int cargoLineCount)
    {
        int lines = Math.Clamp(cargoLineCount, 0, NpcPirateCargoDemandService.MaximumCommodityTypes);

        // header + speaker + title + cargo lines + countdown + button row.
        int contentHeight =
            HeaderLineHeight * 3 +
            CargoLineHeight * lines +
            HeaderLineHeight +
            ButtonHeight;
        int panelHeight = contentHeight + PanelPadding * 2;

        int panelX = Math.Max(0, (viewportWidth - PanelWidth) / 2);
        Rectangle panel = new(panelX, 96, PanelWidth, panelHeight);

        int buttonY = panel.Bottom - PanelPadding - ButtonHeight;
        int buttonX = panel.X + PanelPadding;
        Rectangle comply = new(buttonX, buttonY, ButtonWidth, ButtonHeight);
        Rectangle refuse = new(buttonX + ButtonWidth + ButtonGap, buttonY, ButtonWidth, ButtonHeight);

        return new PirateDemandHailLayout(panel, comply, refuse);
    }
}
