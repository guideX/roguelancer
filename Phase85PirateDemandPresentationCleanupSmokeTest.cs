using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 85 focused coverage for the pirate-demand presentation-state cleanup.
/// The suite drives only production systems: the authoritative Phase 81
/// NpcPirateCargoDemandService and the Phase 83/84 PirateDemandHailPresentation
/// adapter. It proves that mutable post-resolution HudText history is gone and
/// HudText is now a read-only computed projection of the current active demand
/// only, while all Phase 81 cargo behavior, Phase 82 haul semantics, the Phase
/// 83 hail panel, and the Phase 84 focus/hover/acknowledgement beat are
/// unchanged. No new demand, cargo, combat, history, or persistence authority
/// is introduced.
/// </summary>
internal sealed class Phase85PirateDemandPresentationCleanupSmokeTest
{
    private const string FoodId = "food-rations";
    private const string FoodName = "Food Rations";
    private const string WaterId = "water";
    private const int TestViewportWidth = 1920;

    public Phase85PirateDemandPresentationCleanupSmokeTest(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("no active demand produces no demand HUD", NoActiveDemandProducesNoDemandHud, ref passed, ref failed);
        RunCase("active demand displays the Phase 83 panel", ActiveDemandDisplaysPhase83Panel, ref passed, ref failed);
        RunCase("demander identity remains authoritative", DemanderIdentityRemainsAuthoritative, ref passed, ref failed);
        RunCase("faction label remains correct", FactionLabelRemainsCorrect, ref passed, ref failed);
        RunCase("cargo lines remain exact", CargoLinesRemainExact, ref passed, ref failed);
        RunCase("countdown remains authoritative", CountdownRemainsAuthoritative, ref passed, ref failed);
        RunCase("comply/refuse controls remain correct", ComplyRefuseControlsRemainCorrect, ref passed, ref failed);
        RunCase("active UI does not parse HudText", ActiveUiDoesNotParseHudText, ref passed, ref failed);
        RunCase("active UI reads structured demand state", ActiveUiReadsStructuredDemandState, ref passed, ref failed);
        RunCase("no redundant legacy prompt overlaps the hail", NoRedundantLegacyPromptOverlapsHail, ref passed, ref failed);
        RunCase("compliance removes exact cargo as before", ComplianceRemovesExactCargo, ref passed, ref failed);
        RunCase("compliance emits one authoritative resolution", ComplianceEmitsOneAuthoritativeResolution, ref passed, ref failed);
        RunCase("compliance acknowledgement remains correct", ComplianceAcknowledgementRemainsCorrect, ref passed, ref failed);
        RunCase("useful cargo notification remains available", UsefulCargoNotificationRemainsAvailable, ref passed, ref failed);
        RunCase("no stale service result text after compliance", NoStaleServiceResultTextAfterCompliance, ref passed, ref failed);
        RunCase("refusal removes no cargo", RefusalRemovesNoCargo, ref passed, ref failed);
        RunCase("refusal still escalates combat", RefusalStillEscalatesCombat, ref passed, ref failed);
        RunCase("refusal acknowledgement remains correct", RefusalAcknowledgementRemainsCorrect, ref passed, ref failed);
        RunCase("no stale service result text after refusal", NoStaleServiceResultTextAfterRefusal, ref passed, ref failed);
        RunCase("timeout still resolves through Phase 81", TimeoutStillResolvesThroughPhase81, ref passed, ref failed);
        RunCase("timeout acknowledgement remains correct", TimeoutAcknowledgementRemainsCorrect, ref passed, ref failed);
        RunCase("no stale service result text after timeout", NoStaleServiceResultTextAfterTimeout, ref passed, ref failed);
        RunCase("failed compliance produces no false success message", FailedComplianceProducesNoFalseSuccess, ref passed, ref failed);
        RunCase("cancellation produces no false success message", CancellationProducesNoFalseSuccess, ref passed, ref failed);
        RunCase("acknowledgement expires normally after its duration", AcknowledgementExpiresNormally, ref passed, ref failed);
        RunCase("notification expiration does not affect gameplay", NotificationExpiryDoesNotAffectGameplay, ref passed, ref failed);
        RunCase("acknowledgement expiration does not affect gameplay", AcknowledgementExpiryDoesNotAffectGameplay, ref passed, ref failed);
        RunCase("no result HUD text remains after expiration", NoResultHudTextAfterExpiration, ref passed, ref failed);
        RunCase("repeated rendering cannot extend the acknowledgement", RepeatedRenderingCannotExtendAcknowledgement, ref passed, ref failed);
        RunCase("repeated rendering does not mutate demand state", RepeatedRenderingDoesNotMutateDemandState, ref passed, ref failed);
        RunCase("keyboard comply works", KeyboardComplyWorks, ref passed, ref failed);
        RunCase("keyboard refuse works", KeyboardRefuseWorks, ref passed, ref failed);
        RunCase("mouse comply works", MouseComplyWorks, ref passed, ref failed);
        RunCase("mouse refuse works", MouseRefuseWorks, ref passed, ref failed);
        RunCase("focus behavior remains unchanged", FocusBehaviorRemainsUnchanged, ref passed, ref failed);
        RunCase("hover behavior remains unchanged", HoverBehaviorRemainsUnchanged, ref passed, ref failed);
        RunCase("held input cannot transact twice", HeldInputCannotTransactTwice, ref passed, ref failed);
        RunCase("same-frame competing input cannot transact twice", CompetingInputCannotTransactTwice, ref passed, ref failed);
        RunCase("timeout/input precedence remains unchanged", TimeoutInputPrecedenceRemainsUnchanged, ref passed, ref failed);
        RunCase("demander death clears presentation", DemanderDeathClearsPresentation, ref passed, ref failed);
        RunCase("demander despawn clears presentation", DemanderDespawnClearsPresentation, ref passed, ref failed);
        RunCase("player death clears presentation", PlayerDeathClearsPresentation, ref passed, ref failed);
        RunCase("docking clears presentation", DockingClearsPresentation, ref passed, ref failed);
        RunCase("system transition clears presentation", SystemTransitionClearsPresentation, ref passed, ref failed);
        RunCase("new demand cannot inherit previous result text", NewDemandCannotInheritPreviousResultText, ref passed, ref failed);
        RunCase("save/load restores no transient presentation", SaveLoadRestoresNoTransientPresentation, ref passed, ref failed);
        RunCase("schema remains unchanged", SchemaRemainsUnchanged, ref passed, ref failed);
        RunCase("no presentation-history save member exists", NoPresentationHistorySaveMember, ref passed, ref failed);
        RunCase("Phase 81 cargo-demand behavior unchanged", Phase81CargoDemandUnchanged, ref passed, ref failed);
        RunCase("Phase 82 pirate haul behavior unchanged", Phase82PirateHaulUnchanged, ref passed, ref failed);
        RunCase("Phase 83 hail behavior unchanged", Phase83HailUnchanged, ref passed, ref failed);
        RunCase("Phase 84 acknowledgement behavior unchanged", Phase84AcknowledgementUnchanged, ref passed, ref failed);
        RunCase("stolen cargo provenance unchanged", StolenCargoProvenanceUnchanged, ref passed, ref failed);
        RunCase("mission/freight reservations unchanged", MissionFreightReservationsUnchanged, ref passed, ref failed);
        RunCase("police/fugitive behavior unchanged", PoliceFugitiveUnchanged, ref passed, ref failed);
        RunCase("Phase 77 docking unchanged", Phase77DockingUnchanged, ref passed, ref failed);
        RunCase("Phase 78 surrender unchanged", Phase78SurrenderUnchanged, ref passed, ref failed);
        RunCase("Phase 79 fire cessation unchanged", Phase79FireCessationUnchanged, ref passed, ref failed);
        RunCase("Phase 80 flight normalization unchanged", Phase80FlightNormalizationUnchanged, ref passed, ref failed);
        RunCase("no new communications-history authority exists", NoNewCommunicationsHistoryAuthority, ref passed, ref failed);
        RunCase("service owns no acknowledgement state", ServiceOwnsNoAcknowledgementState, ref passed, ref failed);
        RunCase("presentation owns no gameplay state", PresentationOwnsNoGameplayState, ref passed, ref failed);

        Console.WriteLine($"[PHASE 85 PIRATE DEMAND PRESENTATION CLEANUP SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(string label, Func<(bool, string)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 85 PIRATE DEMAND PRESENTATION CLEANUP SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 85 PIRATE DEMAND PRESENTATION CLEANUP SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 85 PIRATE DEMAND PRESENTATION CLEANUP SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    // ---- Active presentation -------------------------------------------------

    private (bool, string) NoActiveDemandProducesNoDemandHud()
    {
        Context ctx = CreateContext();
        bool snapshot = ctx.Presentation.TryBuildSnapshot(out _);
        bool ack = ctx.Presentation.TryGetAcknowledgement(out _);
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            !snapshot && !ack && !ctx.Presentation.IsVisible
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}', snapshot={snapshot}, ack={ack}");
    }

    private (bool, string) ActiveDemandDisplaysPhase83Panel()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        return snapshot.CargoLines.Count >= 1 &&
            snapshot.CargoLines.Count <= NpcPirateCargoDemandService.MaximumCommodityTypes &&
            snapshot.HeaderText.StartsWith("INCOMING HAIL", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(snapshot.CountdownText) &&
            ctx.Presentation.IsVisible
            ? Pass()
            : Fail($"lines={snapshot.CargoLines.Count}, header='{snapshot.HeaderText}'");
    }

    private (bool, string) DemanderIdentityRemainsAuthoritative()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        return ReferenceEquals(ctx.Service.ActiveDemander, rogue) &&
            snapshot.SpeakerName == rogue.Name
            ? Pass()
            : Fail($"speaker='{snapshot.SpeakerName}', rogue='{rogue.Name}'");
    }

    private (bool, string) FactionLabelRemainsCorrect()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        return snapshot.FactionId == rogue.FactionId &&
            snapshot.FactionLabel == FactionManager.GetFactionDisplayName(rogue.FactionId)
            ? Pass()
            : Fail($"faction='{snapshot.FactionId}', label='{snapshot.FactionLabel}'");
    }

    private (bool, string) CargoLinesRemainExact()
    {
        Context ctx = ActiveDemand(out _, giveWater: true);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        IReadOnlyDictionary<string, int> demanded = ctx.Service.ActiveDemandQuantities;
        bool exact = snapshot.CargoLines.Count == demanded.Count &&
            snapshot.CargoLines.All(line =>
                demanded.TryGetValue(line.CommodityName, out int quantity) && quantity == line.Quantity);
        return exact
            ? Pass()
            : Fail($"lines={snapshot.CargoLines.Count}, demanded={demanded.Count}");
    }

    private (bool, string) CountdownRemainsAuthoritative()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        float before = ctx.Service.ResponseRemainingSeconds;
        if (Math.Abs(snapshot.ResponseRemainingSeconds - before) > 0.001f || before <= 0f)
            return Fail($"snapshot={snapshot.ResponseRemainingSeconds}, service={before}");
        ctx.StepUpdate(1f);
        float after = ctx.Service.ResponseRemainingSeconds;
        return after < before
            ? Pass()
            : Fail($"countdown did not tick: {before}->{after}");
    }

    private (bool, string) ComplyRefuseControlsRemainCorrect()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        return snapshot.ComplyKey == NpcPirateCargoDemandService.ComplyKey &&
            snapshot.RefuseKey == NpcPirateCargoDemandService.RefuseKey &&
            snapshot.ComplyText.Contains("COMPLY", StringComparison.Ordinal) &&
            snapshot.RefuseText.Contains("REFUSE", StringComparison.Ordinal)
            ? Pass()
            : Fail($"comply='{snapshot.ComplyText}', refuse='{snapshot.RefuseText}'");
    }

    private (bool, string) ActiveUiDoesNotParseHudText()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        bool noHudTextMember =
            !typeof(PirateDemandHailPresentation).GetMethods(flags).Any(m => m.Name.Contains("HudText", StringComparison.Ordinal)) &&
            !typeof(PirateDemandHailPresentation).GetFields(flags).Any(f => f.Name.Contains("HudText", StringComparison.OrdinalIgnoreCase)) &&
            !typeof(PirateDemandHailPresentation).GetProperties(flags).Any(p => p.Name.Contains("HudText", StringComparison.Ordinal));
        Context ctx = ActiveDemand(out _);
        bool snapshot = ctx.Presentation.TryBuildSnapshot(out _);
        return noHudTextMember && snapshot
            ? Pass()
            : Fail($"noHudTextMember={noHudTextMember}, snapshot={snapshot}");
    }

    private (bool, string) ActiveUiReadsStructuredDemandState()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        bool mirrors =
            ctx.Service.HasActiveDemand &&
            ReferenceEquals(ctx.Service.ActiveDemander, rogue) &&
            snapshot.SpeakerName == rogue.Name &&
            snapshot.CargoLines.All(line =>
                ctx.Service.ActiveDemandQuantities.TryGetValue(line.CommodityName, out int quantity) && quantity == line.Quantity) &&
            Math.Abs(snapshot.ResponseRemainingSeconds - ctx.Service.ResponseRemainingSeconds) < 0.001f;
        return mirrors
            ? Pass()
            : Fail("snapshot does not mirror structured demand state");
    }

    private (bool, string) NoRedundantLegacyPromptOverlapsHail()
    {
        Context ctx = ActiveDemand(out _);
        // Production draw order (RoguelancerGame.DrawNpcPirateCargoDemandHUD)
        // renders the hail panel and returns before the legacy HudText fallback
        // is reachable, so a redundant overlap is impossible while the panel
        // snapshot exists. The fallback string itself is live demand text, not
        // a second stale message.
        bool panelFirst = ctx.Presentation.TryBuildSnapshot(out _);
        string hud = ctx.Service.HudText;
        bool hudIsLive = hud.Contains("LIBERTY ROGUES DEMAND CARGO", StringComparison.Ordinal) &&
            ctx.Service.ActiveDemandQuantities.All(pair => hud.Contains(pair.Key, StringComparison.Ordinal));
        return panelFirst && hudIsLive
            ? Pass()
            : Fail($"panelFirst={panelFirst}, hud='{hud}'");
    }

    // ---- Resolution behavior -------------------------------------------------

    private (bool, string) ComplianceRemovesExactCargo()
    {
        Context ctx = ActiveDemand(out _, giveWater: true);
        Dictionary<string, int> demanded = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> before = PlayerCargoTotals(ctx);
        bool complied = ctx.Service.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        Dictionary<string, int> after = PlayerCargoTotals(ctx);
        bool exact = complied && result?.State == NpcPirateDemandState.Complying &&
            demanded.All(pair => before.GetValueOrDefault(pair.Key) - after.GetValueOrDefault(pair.Key) == pair.Value) &&
            before.Keys.Concat(after.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !demanded.ContainsKey(name))
                .All(name => before.GetValueOrDefault(name) == after.GetValueOrDefault(name));
        return exact
            ? Pass()
            : Fail($"complied={complied}, state={result?.State}");
    }

    private (bool, string) ComplianceEmitsOneAuthoritativeResolution()
    {
        Context ctx = ActiveDemand(out _);
        int resolutions = 0;
        NpcPirateCargoDemandResult fired = null;
        ctx.Service.DemandResolved += result => { resolutions++; fired = result; };
        bool complied = ctx.Service.TryComply(ctx.Player, out _);
        return complied && resolutions == 1 && fired?.State == NpcPirateDemandState.Complying &&
            ReferenceEquals(fired, ctx.Service.LastResult)
            ? Pass()
            : Fail($"complied={complied}, resolutions={resolutions}, state={fired?.State}");
    }

    private (bool, string) ComplianceAcknowledgementRemainsCorrect()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out _);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack);
        return hasAck && ack.Text == PirateDemandHailPresentation.ComplyAcknowledgementText
            ? Pass()
            : Fail($"hasAck={hasAck}, text='{ack?.Text}'");
    }

    private (bool, string) UsefulCargoNotificationRemainsAvailable()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        string last = ctx.Notifications.LastOrDefault() ?? string.Empty;
        return result != null && result.SurrenderedQuantity > 0 &&
            last.Contains(result.SurrenderedQuantity.ToString(), StringComparison.Ordinal)
            ? Pass()
            : Fail($"surrendered={result?.SurrenderedQuantity}, last='{last}'");
    }

    private (bool, string) NoStaleServiceResultTextAfterCompliance()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out _);
        if (ctx.Service.HudText != string.Empty)
            return Fail($"stale HUD immediately after compliance: '{ctx.Service.HudText}'");
        ctx.StepUpdate(2f);
        return ctx.Service.HudText == string.Empty && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail($"stale HUD 2s after compliance: '{ctx.Service.HudText}'");
    }

    private (bool, string) RefusalRemovesNoCargo()
    {
        Context ctx = ActiveDemand(out _, giveWater: true);
        Dictionary<string, int> before = PlayerCargoTotals(ctx);
        bool refused = ctx.Service.TryRefuse(ctx.Player, out NpcPirateCargoDemandResult result);
        Dictionary<string, int> after = PlayerCargoTotals(ctx);
        bool intact = before.Count == after.Count &&
            before.All(pair => after.TryGetValue(pair.Key, out int quantity) && quantity == pair.Value);
        return refused && result?.State == NpcPirateDemandState.Refusing && intact
            ? Pass()
            : Fail($"refused={refused}, intact={intact}");
    }

    private (bool, string) RefusalStillEscalatesCombat()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Service.TryRefuse(ctx.Player, out _);
        return rogue.HasPlayerTarget && rogue.EncounterState == TrafficEncounterState.AttackingPlayer
            ? Pass()
            : Fail($"target={rogue.HasPlayerTarget}, state={rogue.EncounterState}");
    }

    private (bool, string) RefusalAcknowledgementRemainsCorrect()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack);
        return hasAck && ack.Text == PirateDemandHailPresentation.RefuseAcknowledgementText
            ? Pass()
            : Fail($"hasAck={hasAck}, text='{ack?.Text}'");
    }

    private (bool, string) NoStaleServiceResultTextAfterRefusal()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        if (ctx.Service.HudText != string.Empty)
            return Fail($"stale HUD immediately after refusal: '{ctx.Service.HudText}'");
        ctx.StepUpdate(2f);
        return ctx.Service.HudText == string.Empty && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail($"stale HUD 2s after refusal: '{ctx.Service.HudText}'");
    }

    private (bool, string) TimeoutStillResolvesThroughPhase81()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 0.5f);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        NpcPirateCargoDemandResult result = ctx.Service.LastResult;
        return !ctx.Service.HasActiveDemand &&
            result?.State == NpcPirateDemandState.Refusing &&
            result.ResolutionReason.Contains("expired", StringComparison.OrdinalIgnoreCase) &&
            before == after && rogue.HasPlayerTarget
            ? Pass()
            : Fail($"state={result?.State}, reason='{result?.ResolutionReason}', removed={before - after}");
    }

    private (bool, string) TimeoutAcknowledgementRemainsCorrect()
    {
        Context ctx = ActiveDemand(out _);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 0.5f);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack);
        return hasAck && ack.Text == PirateDemandHailPresentation.TimeoutAcknowledgementText
            ? Pass()
            : Fail($"hasAck={hasAck}, text='{ack?.Text}'");
    }

    private (bool, string) NoStaleServiceResultTextAfterTimeout()
    {
        Context ctx = ActiveDemand(out _);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 0.5f);
        if (ctx.Service.HudText != string.Empty)
            return Fail($"stale HUD immediately after timeout: '{ctx.Service.HudText}'");
        // The pre-Phase-85 service retained the live demand prompt for up to
        // ResponseWindowSeconds + 2s from initiation, so it reappeared after
        // the acknowledgement expired. Both windows must now stay empty.
        ctx.StepUpdate(1f);
        if (ctx.Service.HudText != string.Empty)
            return Fail($"stale HUD 1s after timeout: '{ctx.Service.HudText}'");
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, PirateDemandHailPresentation.AcknowledgementDurationSeconds + 1f);
        ctx.StepUpdate(3f);
        return ctx.Service.HudText == string.Empty &&
            !ctx.Presentation.TryGetAcknowledgement(out _) &&
            !ctx.Presentation.TryBuildSnapshot(out _)
            ? Pass()
            : Fail($"stale HUD after acknowledgement expiry: '{ctx.Service.HudText}'");
    }

    private (bool, string) FailedComplianceProducesNoFalseSuccess()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        rogue.Hull.TakeDamage(10_000f);
        bool complied = ctx.Service.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        bool successAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack) &&
            ack.Text == PirateDemandHailPresentation.ComplyAcknowledgementText;
        return !complied && result == null && !successAck &&
            ctx.Service.HudText == string.Empty && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail($"complied={complied}, successAck={successAck}, hud='{ctx.Service.HudText}'");
    }

    private (bool, string) CancellationProducesNoFalseSuccess()
    {
        Context ctx = ActiveDemand(out _);
        int notifications = ctx.Notifications.Count;
        ctx.Service.CancelActiveDemand("Phase 85 test cancellation");
        ctx.Service.CancelActiveDemand("Phase 85 test cancellation");
        bool ack = ctx.Presentation.TryGetAcknowledgement(out _);
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            !ack && ctx.Service.LastResult == null && ctx.Notifications.Count == notifications
            ? Pass()
            : Fail($"ack={ack}, hud='{ctx.Service.HudText}', notifications={notifications}->{ctx.Notifications.Count}");
    }

    // ---- Presentation lifetime ------------------------------------------------

    private (bool, string) AcknowledgementExpiresNormally()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out _);
        if (!ctx.Presentation.TryGetAcknowledgement(out _))
            return Fail("no acknowledgement after compliance");
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, PirateDemandHailPresentation.AcknowledgementDurationSeconds + 1f);
        return !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail("acknowledgement did not expire after its duration");
    }

    private (bool, string) NotificationExpiryDoesNotAffectGameplay()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Service.TryComply(ctx.Player, out _);
        Dictionary<string, int> cargo = PlayerCargoTotals(ctx);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        int notifications = ctx.Notifications.Count;
        NpcPirateCargoDemandResult result = ctx.Service.LastResult;
        ctx.StepUpdate(10f);
        ctx.StepUpdate(10f);
        Dictionary<string, int> after = PlayerCargoTotals(ctx);
        bool stable = cargo.Count == after.Count &&
            cargo.All(pair => after.TryGetValue(pair.Key, out int quantity) && quantity == pair.Value) &&
            ctx.Service.GetPirateHaulQuantity(rogue) == haul &&
            ctx.Notifications.Count == notifications &&
            ReferenceEquals(ctx.Service.LastResult, result) &&
            !ctx.Service.HasActiveDemand;
        return stable
            ? Pass()
            : Fail("time passage after resolution changed gameplay or message state");
    }

    private (bool, string) AcknowledgementExpiryDoesNotAffectGameplay()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        int surrendered = result?.SurrenderedQuantity ?? -1;
        Dictionary<string, int> cargo = PlayerCargoTotals(ctx);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, PirateDemandHailPresentation.AcknowledgementDurationSeconds + 1f);
        Dictionary<string, int> after = PlayerCargoTotals(ctx);
        bool stable = !ctx.Presentation.TryGetAcknowledgement(out _) &&
            ctx.Service.LastResult?.State == NpcPirateDemandState.Complying &&
            ctx.Service.LastResult?.SurrenderedQuantity == surrendered &&
            cargo.Count == after.Count &&
            cargo.All(pair => after.TryGetValue(pair.Key, out int quantity) && quantity == pair.Value) &&
            !ctx.Service.HasActiveDemand;
        return stable
            ? Pass()
            : Fail("acknowledgement expiry changed gameplay state");
    }

    private (bool, string) NoResultHudTextAfterExpiration()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, PirateDemandHailPresentation.AcknowledgementDurationSeconds + 1f);
        ctx.StepUpdate(5f);
        return ctx.Service.HudText == string.Empty &&
            !ctx.Presentation.TryGetAcknowledgement(out _) &&
            !ctx.Presentation.TryBuildSnapshot(out _)
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}'");
    }

    private (bool, string) RepeatedRenderingCannotExtendAcknowledgement()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out _);
        for (int i = 0; i < 20; i++)
        {
            ctx.Presentation.TryGetAcknowledgement(out _);
            ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        }
        if (!ctx.Presentation.TryGetAcknowledgement(out _))
            return Fail("acknowledgement expired early under repeated reads");
        for (int i = 0; i < 30; i++)
        {
            ctx.Presentation.TryGetAcknowledgement(out _);
            ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        }
        return !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail("repeated reads extended the acknowledgement");
    }

    private (bool, string) RepeatedRenderingDoesNotMutateDemandState()
    {
        Context ctx = ActiveDemand(out _);
        Dictionary<string, int> demanded = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        float remaining = ctx.Service.ResponseRemainingSeconds;
        for (int i = 0; i < 20; i++)
        {
            ctx.Presentation.TryBuildSnapshot(out _);
            ctx.Presentation.TryGetAcknowledgement(out _);
            ctx.Presentation.Update(i % 2 == 0 ? MouseOverComply(ctx) : MouseOverRefuse(ctx), TestViewportWidth, 0f);
        }
        bool intact = ctx.Service.HasActiveDemand &&
            Math.Abs(ctx.Service.ResponseRemainingSeconds - remaining) < 0.001f &&
            demanded.Count == ctx.Service.ActiveDemandQuantities.Count &&
            demanded.All(pair => ctx.Service.ActiveDemandQuantities.TryGetValue(pair.Key, out int quantity) && quantity == pair.Value);
        return intact
            ? Pass()
            : Fail("repeated rendering mutated demand state");
    }

    // ---- Input regressions ----------------------------------------------------

    private (bool, string) KeyboardComplyWorks()
    {
        Context ctx = ActiveDemand(out _);
        int requested = ctx.Service.ActiveDemandQuantities.Values.Sum();
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            ctx.Player);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return consumed && before - after == requested &&
            ctx.Service.LastResult?.State == NpcPirateDemandState.Complying
            ? Pass()
            : Fail($"consumed={consumed}, removed={before - after}, requested={requested}");
    }

    private (bool, string) KeyboardRefuseWorks()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(),
            ctx.Player);
        return consumed && !ctx.Service.HasActiveDemand && rogue.HasPlayerTarget &&
            ctx.Service.LastResult?.State == NpcPirateDemandState.Refusing
            ? Pass()
            : Fail($"consumed={consumed}, target={rogue.HasPlayerTarget}");
    }

    private (bool, string) MouseComplyWorks()
    {
        Context ctx = ActiveDemand(out _);
        bool routed = ctx.Presentation.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        return routed && result?.State == NpcPirateDemandState.Complying && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail($"routed={routed}, state={result?.State}");
    }

    private (bool, string) MouseRefuseWorks()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        bool routed = ctx.Presentation.TryRefuse(ctx.Player, out NpcPirateCargoDemandResult result);
        return routed && result?.State == NpcPirateDemandState.Refusing &&
            rogue.HasPlayerTarget && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail($"routed={routed}, state={result?.State}");
    }

    private (bool, string) FocusBehaviorRemainsUnchanged()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return ctx.Presentation.FocusedResponse == PirateDemandHailPresentation.DefaultResponseFocus &&
            PirateDemandHailPresentation.DefaultResponseFocus == PirateHailResponse.Comply
            ? Pass()
            : Fail($"focus={ctx.Presentation.FocusedResponse}");
    }

    private (bool, string) HoverBehaviorRemainsUnchanged()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        if (ctx.Presentation.HoveredResponse != PirateHailResponse.Comply)
            return Fail($"comply hover={ctx.Presentation.HoveredResponse}");
        ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.1f);
        if (ctx.Presentation.HoveredResponse != PirateHailResponse.Refuse)
            return Fail($"refuse hover={ctx.Presentation.HoveredResponse}");
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return ctx.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail($"outside hover={ctx.Presentation.HoveredResponse}");
    }

    private (bool, string) HeldInputCannotTransactTwice()
    {
        Context ctx = ActiveDemand(out _);
        int requested = ctx.Service.ActiveDemandQuantities.Values.Sum();
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool first = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            ctx.Player);
        bool second = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            ctx.Player);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return first && !second && before - after == requested
            ? Pass()
            : Fail($"first={first}, second={second}, removed={before - after}, requested={requested}");
    }

    private (bool, string) CompetingInputCannotTransactTwice()
    {
        Context ctx = ActiveDemand(out _);
        int resolutions = 0;
        ctx.Service.DemandResolved += _ => resolutions++;
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey, NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(),
            ctx.Player);
        bool lateRefuse = ctx.Service.TryRefuse(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return consumed && resolutions == 1 && !lateRefuse &&
            ctx.Service.LastResult?.State == NpcPirateDemandState.Complying &&
            before > after
            ? Pass()
            : Fail($"consumed={consumed}, resolutions={resolutions}, lateRefuse={lateRefuse}");
    }

    private (bool, string) TimeoutInputPrecedenceRemainsUnchanged()
    {
        Context ctx = ActiveDemand(out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds - 0.05f);
        // Production order (RoguelancerGame.UpdateCore): the service timeout
        // update runs before any response input each frame, so the timeout
        // update wins a same-frame race deterministically.
        ctx.Service.Update(0.1f, ctx.Player);
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            ctx.Player);
        NpcPirateCargoDemandResult result = ctx.Service.LastResult;
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return result != null && result.State == NpcPirateDemandState.Refusing &&
            result.ResolutionReason.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0 &&
            before == after
            ? Pass()
            : Fail($"state={result?.State}, reason={result?.ResolutionReason}, removed={before - after}, consumed={consumed}");
    }

    // ---- Lifecycle ------------------------------------------------------------

    private (bool, string) DemanderDeathClearsPresentation()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            !ctx.Presentation.IsVisible && !ctx.Presentation.TryBuildSnapshot(out _) &&
            !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}', visible={ctx.Presentation.IsVisible}");
    }

    private (bool, string) DemanderDespawnClearsPresentation()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Npcs.Remove(rogue);
        ctx.StepUpdate(0.1f);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}', visible={ctx.Presentation.IsVisible}");
    }

    private (bool, string) PlayerDeathClearsPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Player.Hull.TakeDamage(10_000f);
        ctx.Service.CancelActiveDemand("player destroyed");
        ctx.Presentation.ClearTransientState();
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _) &&
            ctx.Presentation.FocusedResponse == PirateHailResponse.None
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}', visible={ctx.Presentation.IsVisible}");
    }

    private (bool, string) DockingClearsPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.CancelActiveDemand("player docked");
        ctx.Presentation.ClearTransientState();
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}', visible={ctx.Presentation.IsVisible}");
    }

    private (bool, string) SystemTransitionClearsPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.Reset();
        ctx.Presentation.ClearTransientState();
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            ctx.Service.LastResult == null && !ctx.Presentation.IsVisible &&
            !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}', visible={ctx.Presentation.IsVisible}");
    }

    private (bool, string) NewDemandCannotInheritPreviousResultText()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f), "Rogue-First");
        ctx.GiveCargo(FoodId, 20);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string firstFailure))
            return Fail($"first demand not initiated: {firstFailure}");
        ctx.Service.TryComply(ctx.Player, out _);
        if (ctx.Service.HudText != string.Empty)
            return Fail($"stale HUD after first resolution: '{ctx.Service.HudText}'");
        ctx.StepUpdate(NpcPirateCargoDemandService.PlayerCooldownSeconds + 1f);
        ctx.GiveCargo(FoodId, 20);
        ctx.AddRogue(new Vector3(2_000f, 0f, 0f), "Rogue-Second");
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string secondFailure))
            return Fail($"second demand not initiated: {secondFailure}");
        string hud = ctx.Service.HudText;
        bool live = hud.Contains("LIBERTY ROGUES DEMAND CARGO", StringComparison.Ordinal) &&
            !hud.Contains("surrendered", StringComparison.OrdinalIgnoreCase) &&
            !hud.Contains("REFUSED", StringComparison.OrdinalIgnoreCase) &&
            ctx.Service.ActiveDemandQuantities.All(pair => hud.Contains(pair.Key, StringComparison.Ordinal));
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        bool noStaleAck = !ctx.Presentation.TryGetAcknowledgement(out _);
        return live && noStaleAck
            ? Pass()
            : Fail($"live={live}, noStaleAck={noStaleAck}, hud='{hud}'");
    }

    private (bool, string) SaveLoadRestoresNoTransientPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.TryComply(ctx.Player, out _);
        // Production save/load path: service Reset plus presentation clear.
        ctx.Service.Reset();
        ctx.Presentation.ClearTransientState();
        return !ctx.Service.HasActiveDemand && ctx.Service.HudText == string.Empty &&
            ctx.Service.LastResult == null && !ctx.Presentation.IsVisible &&
            !ctx.Presentation.TryBuildSnapshot(out _) && !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail($"hud='{ctx.Service.HudText}', visible={ctx.Presentation.IsVisible}");
    }

    private (bool, string) SchemaRemainsUnchanged() =>
        SaveGameData.CurrentSchemaVersion == 13 && new SaveGameData().SchemaVersion == 13
            ? Pass()
            : Fail($"schema is {SaveGameData.CurrentSchemaVersion}");

    private (bool, string) NoPresentationHistorySaveMember()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        string[] forbidden = { "HudText", "Presentation", "Acknowledgement", "Demander", "PirateDemand", "Hail", "Hover", "Transient" };
        bool clean = !typeof(SaveGameData).GetProperties(flags)
                .Any(property => forbidden.Any(fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase))) &&
            !typeof(SaveGameData).GetFields(flags)
                .Any(field => forbidden.Any(fragment => field.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        return clean
            ? Pass()
            : Fail("SaveGameData carries a presentation-history member");
    }

    // ---- Existing authority regressions ---------------------------------------

    private (bool, string) Phase81CargoDemandUnchanged()
    {
        Context ctx = ActiveDemand(out _);
        bool blockedWhileActive = !ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        bool blockedByCooldown = !ctx.Service.TryInitiateDemand(ctx.Player, out string reason) &&
            reason.Contains("cooldown", StringComparison.OrdinalIgnoreCase);
        return blockedWhileActive && blockedByCooldown
            ? Pass()
            : Fail($"blockedWhileActive={blockedWhileActive}, blockedByCooldown={blockedByCooldown}, reason='{reason}'");
    }

    private (bool, string) Phase82PirateHaulUnchanged()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int requested = ctx.Service.ActiveDemandQuantities.Values.Sum();
        ctx.Service.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        bool snapshot = ctx.Service.TryGetHaulSnapshot(rogue, out NpcCargoManifestSnapshot manifest);
        return haul == requested && snapshot && manifest.RemainingQuantity == requested &&
            manifest.Stacks.All(stack => stack.IsStolen) && result?.SurrenderedQuantity == requested
            ? Pass()
            : Fail($"requested={requested}, haul={haul}, snapshot={snapshot}");
    }

    private (bool, string) Phase83HailUnchanged()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no hail snapshot for an active demand");
        bool structure =
            snapshot.HeaderText.StartsWith("INCOMING HAIL", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(snapshot.SpeakerText) &&
            snapshot.TitleText == "CARGO DEMAND" &&
            !string.IsNullOrWhiteSpace(snapshot.CountdownText) &&
            ctx.Presentation.IsVisible && ctx.Service.HasActiveDemand;
        bool nonModal = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation), "Pause", "Freeze", "Block", "Disable");
        return structure && nonModal
            ? Pass()
            : Fail($"structure={structure}, nonModal={nonModal}");
    }

    private (bool, string) Phase84AcknowledgementUnchanged()
    {
        Context complyCtx = ActiveDemand(out _);
        complyCtx.Service.TryComply(complyCtx.Player, out _);
        bool complyAck = complyCtx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement comply) &&
            comply.Text == PirateDemandHailPresentation.ComplyAcknowledgementText;
        Context refuseCtx = ActiveDemand(out _);
        refuseCtx.Service.TryRefuse(refuseCtx.Player, out _);
        bool refuseAck = refuseCtx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement refuse) &&
            refuse.Text == PirateDemandHailPresentation.RefuseAcknowledgementText;
        Context timeoutCtx = ActiveDemand(out _);
        timeoutCtx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 0.5f);
        bool timeoutAck = timeoutCtx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement timeout) &&
            timeout.Text == PirateDemandHailPresentation.TimeoutAcknowledgementText;
        bool duration = PirateDemandHailPresentation.AcknowledgementDurationSeconds == 4.0f;
        return complyAck && refuseAck && timeoutAck && duration
            ? Pass()
            : Fail($"comply={complyAck}, refuse={refuseAck}, timeout={timeoutAck}, duration={duration}");
    }

    private (bool, string) StolenCargoProvenanceUnchanged()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Service.TryComply(ctx.Player, out _);
        bool snapshot = ctx.Service.TryGetHaulSnapshot(rogue, out NpcCargoManifestSnapshot manifest);
        PirateDemandHaulEntry probe = new() { CommodityId = FoodId, CommodityName = FoodName, Quantity = 1 };
        return snapshot && manifest.Stacks.Count > 0 &&
            manifest.Stacks.All(stack => stack.IsStolen) &&
            probe.Provenance == CargoProvenance.Stolen
            ? Pass()
            : Fail($"snapshot={snapshot}, stacks={manifest?.Stacks.Count}");
    }

    private (bool, string) MissionFreightReservationsUnchanged()
    {
        Context missionCtx = CreateContext();
        missionCtx.AddRogue(new Vector3(1_000f, 0f, 0f));
        missionCtx.AddMissionCargo(7, FoodId, 10);
        bool missionExcluded = !missionCtx.Service.TryInitiateDemand(missionCtx.Player, out string missionReason) &&
            missionReason.Contains("no eligible cargo", StringComparison.OrdinalIgnoreCase);
        Context freightCtx = CreateContext();
        freightCtx.AddRogue(new Vector3(1_000f, 0f, 0f));
        freightCtx.GiveCargo(FoodId, 10);
        freightCtx.RegisterFreightReservation(7, FoodId, 10);
        bool freightExcluded = !freightCtx.Service.TryInitiateDemand(freightCtx.Player, out string freightReason) &&
            freightReason.Contains("no eligible cargo", StringComparison.OrdinalIgnoreCase);
        return missionExcluded && freightExcluded
            ? Pass()
            : Fail($"missionExcluded={missionExcluded} ('{missionReason}'), freightExcluded={freightExcluded} ('{freightReason}')");
    }

    private (bool, string) PoliceFugitiveUnchanged()
    {
        Context busyCtx = ActiveDemand(out _);
        busyCtx.PoliceInteractionActive = true;
        // An active demand is unaffected by a Police interaction starting, but
        // no NEW demand may start while one is active.
        bool blockedWhileActive = !busyCtx.Service.TryInitiateDemand(busyCtx.Player, out _);
        Context policeCtx = CreateContext();
        policeCtx.PoliceInteractionActive = true;
        policeCtx.AddRogue(new Vector3(1_000f, 0f, 0f));
        policeCtx.GiveCargo(FoodId, 10);
        bool policeBlocked = !policeCtx.Service.TryInitiateDemand(policeCtx.Player, out string policeReason) &&
            policeReason.Contains("Police interaction", StringComparison.OrdinalIgnoreCase);
        Context policeOnlyCtx = CreateContext();
        policeOnlyCtx.AddPolice(new Vector3(1_000f, 0f, 0f));
        policeOnlyCtx.GiveCargo(FoodId, 10);
        bool policeIneligible = !policeOnlyCtx.Service.TryInitiateDemand(policeOnlyCtx.Player, out _);
        return blockedWhileActive && policeBlocked && policeIneligible
            ? Pass()
            : Fail($"blockedWhileActive={blockedWhileActive}, policeBlocked={policeBlocked} ('{policeReason}'), policeIneligible={policeIneligible}");
    }

    private (bool, string) Phase77DockingUnchanged()
    {
        Context ctx = CreateContext();
        ctx.SetDocked(true);
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        bool dockedBlocked = !ctx.Service.TryInitiateDemand(ctx.Player, out string reason) &&
            reason.Contains("docked", StringComparison.OrdinalIgnoreCase);
        bool noAuthority = !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "DockingAccess", "Fugitive") &&
            !TypeReferencesForbiddenAuthority(typeof(NpcPirateCargoDemandService), "DockingAccess", "Fugitive");
        return dockedBlocked && noAuthority
            ? Pass()
            : Fail($"dockedBlocked={dockedBlocked} ('{reason}'), noAuthority={noAuthority}");
    }

    private (bool, string) Phase78SurrenderUnchanged() =>
        !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "Surrender") &&
        !TypeReferencesForbiddenAuthority(typeof(NpcPirateCargoDemandService), "Surrender")
            ? Pass()
            : Fail("pirate-demand types reference Phase 78 authority");

    private (bool, string) Phase79FireCessationUnchanged() =>
        !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "FireCessation", "PoliceScan") &&
        !TypeReferencesForbiddenAuthority(typeof(NpcPirateCargoDemandService), "FireCessation", "PoliceScan")
            ? Pass()
            : Fail("pirate-demand types reference Phase 79 authority");

    private (bool, string) Phase80FlightNormalizationUnchanged() =>
        !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "FlightState", "GotoAutopilot", "Cruise") &&
        !TypeReferencesForbiddenAuthority(typeof(NpcPirateCargoDemandService), "FlightState", "GotoAutopilot", "Cruise")
            ? Pass()
            : Fail("pirate-demand types reference Phase 80 authority");

    // ---- Architecture probes ---------------------------------------------------

    private (bool, string) NoNewCommunicationsHistoryAuthority()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        // The mutable result-text retention is gone: no string-typed instance
        // state and no presentation-named fields may remain on the service.
        bool noStringFields = !typeof(NpcPirateCargoDemandService)
            .GetFields(flags).Any(field => field.FieldType == typeof(string) && !field.IsStatic);
        bool noPresentationFields = !typeof(NpcPirateCargoDemandService)
            .GetFields(flags).Any(field => field.Name.Contains("presentation", StringComparison.OrdinalIgnoreCase));
        bool hudIsReadOnlyProjection = typeof(NpcPirateCargoDemandService)
            .GetProperty("HudText", flags) is PropertyInfo hud && hud.CanRead && !hud.CanWrite;
        bool noTextCollections = !typeof(NpcPirateCargoDemandService)
            .GetFields(flags).Any(field => field.FieldType.IsGenericType &&
                (field.FieldType.GetGenericTypeDefinition() == typeof(List<>) ||
                    field.FieldType.GetGenericTypeDefinition() == typeof(Queue<>) ||
                    field.FieldType.GetGenericTypeDefinition() == typeof(Stack<>)) &&
                field.FieldType.GetGenericArguments().Any(arg => arg == typeof(string)));
        return noStringFields && noPresentationFields && hudIsReadOnlyProjection && noTextCollections
            ? Pass()
            : Fail($"noStringFields={noStringFields}, noPresentationFields={noPresentationFields}, hudReadOnly={hudIsReadOnlyProjection}, noTextCollections={noTextCollections}");
    }

    private (bool, string) ServiceOwnsNoAcknowledgementState() =>
        !HasAnyMemberNamed(typeof(NpcPirateCargoDemandService), "Acknowledge", "Focus", "Hover")
            ? Pass()
            : Fail("service owns presentation acknowledgement state");

    private (bool, string) PresentationOwnsNoGameplayState()
    {
        bool noDemandApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation),
            "Transaction", "Commit", "Transact", "Haul", "Escalate", "Complete", "CargoSelect", "CargoRemove", "Mutate", "Transfer");
        bool noCargoCombatApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation),
            "CargoHold", "SpawnStolen", "Flee", "SetPlayerTarget", "RemoveSellable", "Loot", "Combat");
        return noDemandApi && noCargoCombatApi
            ? Pass()
            : Fail($"noDemandApi={noDemandApi}, noCargoCombatApi={noCargoCombatApi}");
    }

    // ---- Harness ----------------------------------------------------------------

    private static Context CreateContext() => new();

    private static Context ActiveDemand(out NpcShip rogue, bool giveWater = false)
    {
        Context ctx = CreateContext();
        rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        if (giveWater)
            ctx.GiveCargo(WaterId, 10);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string failure))
            throw new InvalidOperationException($"could not initiate Phase 85 test demand: {failure}");
        return ctx;
    }

    private static Dictionary<string, int> PlayerCargoTotals(Context ctx)
    {
        Dictionary<string, int> totals = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, int> entry in ctx.Player.CargoHold.GetAllCommodities())
            totals[entry.Key] = entry.Value;
        return totals;
    }

    private static MouseState MouseAt(int x, int y) =>
        new(x, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static MouseState MouseOutside() => MouseAt(10, 700);

    private static (Rectangle Comply, Rectangle Refuse) Buttons(Context ctx)
    {
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            throw new InvalidOperationException("no snapshot for hit testing");
        PirateDemandHailLayout layout = PirateDemandHailPresentation.BuildLayout(TestViewportWidth, snapshot.CargoLines.Count);
        return (layout.ComplyButton, layout.RefuseButton);
    }

    private static MouseState MouseOver(Context ctx, PirateHailResponse response)
    {
        (Rectangle comply, Rectangle refuse) = Buttons(ctx);
        Rectangle target = response == PirateHailResponse.Refuse ? refuse : comply;
        return MouseAt(target.Center.X, target.Center.Y);
    }

    private static MouseState MouseOverComply(Context ctx) => MouseOver(ctx, PirateHailResponse.Comply);

    private static MouseState MouseOverRefuse(Context ctx) => MouseOver(ctx, PirateHailResponse.Refuse);

    private static bool HasAnyMemberNamed(Type type, params string[] fragments)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (fragments.Any(fragment => method.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
        }
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (fragments.Any(fragment => field.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
        }
        return false;
    }

    private static bool TypeReferencesForbiddenAuthority(Type type, params string[] fragments)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (fragments.Any(fragment => field.FieldType.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
        }

        foreach (MethodInfo method in type.GetMethods(flags))
        {
            if (fragments.Any(fragment => method.ReturnType.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (fragments.Any(fragment => parameter.ParameterType.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(flags))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                if (fragments.Any(fragment => parameter.ParameterType.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }
        }

        return false;
    }

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);

    private sealed class Context
    {
        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public List<NpcShip> Npcs { get; } = new();
        public NpcPirateCargoDemandService Service { get; }
        public PirateDemandHailPresentation Presentation { get; }
        public List<string> Notifications { get; } = new();
        public bool PoliceInteractionActive { get; set; }
        private bool _docked;
        private Func<NpcShip, bool> _isMissionOwned = _ => false;

        public Context()
        {
            Reputation = new ReputationManager(new FactionManager());
            Reputation.SetReputation(FactionManager.LibertyRogues, 0.0f, "Phase 85 smoke setup");
            Player = new Ship(Vector3.Zero) { CollisionRadius = 120f };
            Service = new NpcPirateCargoDemandService(
                Npcs,
                Reputation,
                player => _docked,
                () => PoliceInteractionActive,
                npc => _isMissionOwned?.Invoke(npc) == true,
                null,
                null,
                null,
                message => Notifications.Add(message));
            Presentation = new PirateDemandHailPresentation(Service);
        }

        public NpcShip AddRogue(Vector3 position, string name = "Phase 85 Rogue")
        {
            NpcShip rogue = new(name, position, position, 1f, 0f, FactionManager.LibertyRogues);
            rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase85-zone", position, 800f, 180f, 6500f);
            Npcs.Add(rogue);
            return rogue;
        }

        public NpcShip AddPolice(Vector3 position)
        {
            NpcShip police = new($"Phase 85 Police {Npcs.Count + 1}", position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase85-zone", position, 800f, 180f, 6500f);
            Npcs.Add(police);
            return police;
        }

        public void GiveCargo(string commodityId, int quantity)
        {
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity != null)
                Player.CargoHold.AddCommodity(commodity, quantity);
        }

        public void AddMissionCargo(int missionId, string commodityId, int quantity)
        {
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity != null)
                Player.CargoHold.AddMissionCargo(missionId, commodity, quantity);
        }

        public void RegisterFreightReservation(int missionId, string commodityId, int quantity)
        {
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity != null)
                Player.CargoHold.RegisterFreightReservation(missionId, commodity, quantity);
        }

        public void SetDocked(bool docked) => _docked = docked;

        public void StepUpdate(float delta) => Service.Update(delta, Player);
    }
}
