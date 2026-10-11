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
/// The two bounded response choices a player can give an active pirate hail,
/// plus None while no hail is active. Focus and hover are presentation-only
/// selection states; they never own demand, cargo, timer, or combat state.
/// </summary>
public enum PirateHailResponse
{
    None,
    Comply,
    Refuse
}

/// <summary>
/// One bounded, presentation-only acknowledgement beat shown after the
/// authoritative Phase 81 resolution commits. The wording is derived from the
/// actual service result, so no optimistic text is ever displayed. The beat
/// never removes cargo, escalates combat, or creates a transaction.
/// </summary>
public sealed class PirateDemandHailAcknowledgement
{
    public PirateDemandHailAcknowledgement(string text, float remainingSeconds, NpcPirateCargoDemandResult result)
    {
        Text = text ?? string.Empty;
        RemainingSeconds = Math.Max(0f, remainingSeconds);
        Result = result;
    }

    public string Text { get; }
    public float RemainingSeconds { get; }
    public NpcPirateCargoDemandResult Result { get; }
}

/// <summary>
/// Presentation-only adapter for the authoritative Phase 81 NPC pirate cargo
/// demand. It reads active-demand state and delegates comply/refuse back to
/// <see cref="NpcPirateCargoDemandService"/> exactly once per response. It
/// owns no demand, cargo, timer, combat, or persistence state and creates no
/// parallel transaction path.
///
/// Phase 84 adds presentation-only interaction polish: a deterministic default
/// response focus, pointer hover tracking, and a bounded acknowledgement beat
/// observed from the authoritative <see cref="NpcPirateCargoDemandService.DemandResolved"/>
/// event. None of these states can activate a response or mutate demand state;
/// the existing direct keys and mouse clicks remain the only response paths.
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

    /// <summary>
    /// Bounded acknowledgement panel height: padding plus a single bounded
    /// response line plus padding. The panel never grows with demand content.
    /// </summary>
    public const int AcknowledgementPanelHeight = 48;

    /// <summary>
    /// Bounded acknowledgement beat length. Matches the service's existing
    /// result-presentation window (<c>SetPresentation(text, 4f)</c>) so the
    /// in-world acknowledgement fully replaces the old single-line result text
    /// instead of overlapping it. Non-modal: flight, weapons, and combat stay
    /// live throughout; the beat blocks nothing.
    /// </summary>
    public const float AcknowledgementDurationSeconds = 4.0f;

    public const string ComplyAcknowledgementText = "Rogue: Smart choice. We're taking the cargo.";
    public const string RefuseAcknowledgementText = "Rogue: Then we'll take it ourselves.";
    public const string TimeoutAcknowledgementText = "Rogue: Time's up.";

    private const string PlayerRefusedReason = "player refused the demand";
    private const string TimeoutReason = "response window expired";

    private readonly NpcPirateCargoDemandService _demandService;
    private PirateHailResponse _focus = PirateHailResponse.None;
    private PirateHailResponse _hovered = PirateHailResponse.None;
    private PirateDemandHailAcknowledgement _acknowledgement;
    private float _acknowledgementRemainingSeconds;
    private NpcShip _lastDemandDemander;

    public PirateDemandHailPresentation(NpcPirateCargoDemandService demandService)
    {
        _demandService = demandService ?? throw new ArgumentNullException(nameof(demandService));
        _demandService.DemandResolved += OnDemandResolved;
    }

    /// <summary>
    /// Deterministic initial response focus for every new demand. COMPLY is
    /// the first control in the Phase 83 layout, Enter is the existing
    /// universal confirm key and already means comply in Phase 81/83, and
    /// compliance is the non-combat resolution. The default focus never
    /// activates an action by itself.
    /// </summary>
    public const PirateHailResponse DefaultResponseFocus = PirateHailResponse.Comply;

    /// <summary>
    /// Which response the presentation currently shows as selected. This is a
    /// visual selection state only: the existing direct keys (Enter/N) and
    /// mouse clicks always invoke their matching authoritative actions
    /// regardless of focus.
    /// </summary>
    public PirateHailResponse FocusedResponse => _focus;

    /// <summary>
    /// Which response the pointer currently hovers, or None when the pointer
    /// is outside both hit regions. Hover never activates a response and never
    /// alters demand state.
    /// </summary>
    public PirateHailResponse HoveredResponse => _hovered;

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
    /// Advances presentation-only interaction state once per frame. Ticks the
    /// acknowledgement beat, derives hover from the pointer position against
    /// the shared layout, and resets focus/hover whenever a new authoritative
    /// demand appears or the active demand ends. Never touches demand state.
    /// </summary>
    public void Update(Microsoft.Xna.Framework.Input.MouseState mouse, int viewportWidth, float deltaSeconds)
    {
        _acknowledgementRemainingSeconds = Math.Max(0f, _acknowledgementRemainingSeconds - Math.Max(0f, deltaSeconds));
        if (_acknowledgementRemainingSeconds <= 0f)
            _acknowledgement = null;

        if (_acknowledgement != null && _acknowledgement.Result != null &&
            (_acknowledgement.Result.Demander == null || _acknowledgement.Result.Demander.IsDestroyed))
        {
            _acknowledgement = null;
            _acknowledgementRemainingSeconds = 0f;
        }

        NpcShip demander = _demandService.ActiveDemander;
        if (demander == null)
        {
            if (_lastDemandDemander != null || _focus != PirateHailResponse.None || _hovered != PirateHailResponse.None)
            {
                _lastDemandDemander = null;
                _focus = PirateHailResponse.None;
                _hovered = PirateHailResponse.None;
            }
            return;
        }

        if (!ReferenceEquals(demander, _lastDemandDemander))
        {
            _lastDemandDemander = demander;
            _focus = DefaultResponseFocus;
            _hovered = PirateHailResponse.None;
            _acknowledgement = null;
            _acknowledgementRemainingSeconds = 0f;
        }

        _hovered = PirateHailResponse.None;
        if (_demandService.HasActiveDemand && TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
        {
            PirateDemandHailLayout layout = BuildLayout(viewportWidth, snapshot.CargoLines.Count);
            Point pointer = new(mouse.X, mouse.Y);
            if (layout.ComplyButton.Contains(pointer))
                _hovered = PirateHailResponse.Comply;
            else if (layout.RefuseButton.Contains(pointer))
                _hovered = PirateHailResponse.Refuse;
        }

        _focus = _hovered != PirateHailResponse.None ? _hovered : DefaultResponseFocus;
    }

    /// <summary>
    /// Returns the current bounded acknowledgement beat, or false once it has
    /// expired or was cleared. The beat is driven only by the authoritative
    /// Phase 81 resolution result.
    /// </summary>
    public bool TryGetAcknowledgement(out PirateDemandHailAcknowledgement acknowledgement)
    {
        if (_acknowledgement != null && _acknowledgementRemainingSeconds > 0f)
        {
            acknowledgement = _acknowledgement;
            return true;
        }

        acknowledgement = null;
        return false;
    }

    /// <summary>
    /// Clears every Phase 84 presentation-only state: focus, hover,
    /// acknowledgement timer, and acknowledged text. Called by the game at
    /// every demand-cancelling boundary (player death, dock, system transition,
    /// save/load) so no transient hail state survives a lifecycle reset. No
    /// Phase 84 state is persisted.
    /// </summary>
    public void ClearTransientState()
    {
        _focus = PirateHailResponse.None;
        _hovered = PirateHailResponse.None;
        _acknowledgement = null;
        _acknowledgementRemainingSeconds = 0f;
        _lastDemandDemander = null;
    }

    private void OnDemandResolved(NpcPirateCargoDemandResult result)
    {
        if (result == null)
            return;

        string text = null;
        if (result.State == NpcPirateDemandState.Complying)
        {
            text = ComplyAcknowledgementText;
        }
        else if (result.State == NpcPirateDemandState.Refusing)
        {
            if (result.ResolutionReason == PlayerRefusedReason)
                text = RefuseAcknowledgementText;
            else if (result.ResolutionReason == TimeoutReason)
                text = TimeoutAcknowledgementText;
        }

        if (text != null)
        {
            _acknowledgement = new PirateDemandHailAcknowledgement(text, AcknowledgementDurationSeconds, result);
            _acknowledgementRemainingSeconds = AcknowledgementDurationSeconds;
        }
        else
        {
            _acknowledgement = null;
            _acknowledgementRemainingSeconds = 0f;
        }
    }

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
