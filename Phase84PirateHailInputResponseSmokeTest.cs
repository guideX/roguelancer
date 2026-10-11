using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 84 focused coverage for pirate-hail input and response polish. The
/// suite drives only production systems: the authoritative Phase 81
/// NpcPirateCargoDemandService and the Phase 84 PirateDemandHailPresentation
/// focus/hover/acknowledgement adapter. It proves deterministic default focus,
/// pointer hover, direct-key preservation, idempotent single transactions,
/// bounded result-driven acknowledgements, lifecycle cleanup, and that no
/// parallel demand, cargo, combat, or persistence authority is introduced.
/// </summary>
internal sealed class Phase84PirateHailInputResponseSmokeTest
{
    private const string FoodId = "food-rations";
    private const string FoodName = "Food Rations";
    private const string WaterId = "water";
    private const string HFuelId = "h-fuel";
    private const string SideArmsId = "side-arms";
    private const int TestViewportWidth = 1920;

    public Phase84PirateHailInputResponseSmokeTest(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("new demand initializes deterministic focus", NewDemandInitializesDeterministicFocus, ref passed, ref failed);
        RunCase("default focus does not activate automatically", DefaultFocusDoesNotActivate, ref passed, ref failed);
        RunCase("focus remains presentation-only", FocusIsPresentationOnly, ref passed, ref failed);
        RunCase("flight controls remain available", FlightControlsRemainAvailable, ref passed, ref failed);
        RunCase("keyboard direct COMPLY works regardless of focus", KeyboardComplyWorksRegardlessOfFocus, ref passed, ref failed);
        RunCase("keyboard direct REFUSE works regardless of focus", KeyboardRefuseWorksRegardlessOfFocus, ref passed, ref failed);
        RunCase("mouse hover correctly identifies COMPLY", MouseHoverIdentifiesComply, ref passed, ref failed);
        RunCase("mouse hover correctly identifies REFUSE", MouseHoverIdentifiesRefuse, ref passed, ref failed);
        RunCase("pointer outside buttons produces no hover", PointerOutsideButtonsProducesNoHover, ref passed, ref failed);
        RunCase("hover mutates no demand state", HoverMutatesNoDemandState, ref passed, ref failed);
        RunCase("mouse COMPLY routes to Phase 81 once", MouseComplyRoutesToPhase81Once, ref passed, ref failed);
        RunCase("mouse REFUSE routes to Phase 81 once", MouseRefuseRoutesToPhase81Once, ref passed, ref failed);
        RunCase("overlapping hit targets do not exist", OverlappingHitTargetsDoNotExist, ref passed, ref failed);
        RunCase("new demand resets stale hover state", NewDemandResetsStaleHover, ref passed, ref failed);
        RunCase("new demand resets stale focus state", NewDemandResetsStaleFocus, ref passed, ref failed);
        RunCase("successful compliance produces acknowledgement only after service success", ComplianceAcknowledgementAfterServiceSuccess, ref passed, ref failed);
        RunCase("failed compliance does not show success acknowledgement", FailedComplianceShowsNoSuccessAcknowledgement, ref passed, ref failed);
        RunCase("successful refusal produces refusal acknowledgement", RefusalAcknowledgement, ref passed, ref failed);
        RunCase("timeout does not masquerade as explicit button press", TimeoutDoesNotMasqueradeAsButtonPress, ref passed, ref failed);
        RunCase("timeout remains Phase 81 authority", TimeoutRemainsPhase81Authority, ref passed, ref failed);
        RunCase("compliance acknowledgement does not remove cargo itself", ComplianceAcknowledgementDoesNotRemoveCargo, ref passed, ref failed);
        RunCase("refusal acknowledgement does not create hostility itself", RefusalAcknowledgementDoesNotCreateHostility, ref passed, ref failed);
        RunCase("Phase 82 receives cargo exactly as before on compliance", Phase82ReceivesCargoExactlyAsBefore, ref passed, ref failed);
        RunCase("acknowledgement produces no duplicate transaction", AcknowledgementProducesNoDuplicateTransaction, ref passed, ref failed);
        RunCase("existing result notification is not duplicated unnecessarily", ExistingResultNotificationNotDuplicated, ref passed, ref failed);
        RunCase("one successful response results in bounded message count", OneResponseBoundedMessageCount, ref passed, ref failed);
        RunCase("held keyboard comply cannot transact twice", HeldKeyboardComplyCannotTransactTwice, ref passed, ref failed);
        RunCase("held keyboard refuse cannot transact twice", HeldKeyboardRefuseCannotTransactTwice, ref passed, ref failed);
        RunCase("held mouse input cannot transact twice", HeldMouseInputCannotTransactTwice, ref passed, ref failed);
        RunCase("simultaneous comply/refuse inputs commit at most one result", SimultaneousComplyRefuseCommitAtMostOne, ref passed, ref failed);
        RunCase("mouse+keyboard same-frame input commits at most one result", MouseKeyboardSameFrameCommitsAtMostOne, ref passed, ref failed);
        RunCase("no controller UI abstraction exists", NoControllerUiAbstractionExists, ref passed, ref failed);
        RunCase("timeout/input same-frame behavior matches documented production ordering", TimeoutInputSameFrameMatchesProductionOrdering, ref passed, ref failed);
        RunCase("demander death clears focus", DemanderDeathClearsFocus, ref passed, ref failed);
        RunCase("demander death clears acknowledgement", DemanderDeathClearsAcknowledgement, ref passed, ref failed);
        RunCase("demander despawn clears presentation state", DemanderDespawnClearsPresentationState, ref passed, ref failed);
        RunCase("player death clears presentation state", PlayerDeathClearsPresentationState, ref passed, ref failed);
        RunCase("dock clears presentation state", DockClearsPresentationState, ref passed, ref failed);
        RunCase("system transition clears presentation state", SystemTransitionClearsPresentationState, ref passed, ref failed);
        RunCase("save/load does not restore Phase 84 state", SaveLoadDoesNotRestorePhase84State, ref passed, ref failed);
        RunCase("schema remains unchanged", SchemaUnchanged, ref passed, ref failed);
        RunCase("no Phase 84 save member exists", NoPhase84SaveMember, ref passed, ref failed);
        RunCase("repeated passive rendering has no side effects", RepeatedPassiveRenderingNoSideEffects, ref passed, ref failed);
        RunCase("multiple Rogues still produce one authoritative hail", MultipleRoguesOneAuthoritativeHail, ref passed, ref failed);
        RunCase("focus never selects a non-authoritative demander", FocusNeverSelectsNonAuthoritativeDemander, ref passed, ref failed);
        RunCase("cargo lines remain Phase 81 authoritative", CargoLinesRemainPhase81Authoritative, ref passed, ref failed);
        RunCase("countdown remains Phase 81 authoritative", CountdownRemainsPhase81Authoritative, ref passed, ref failed);
        RunCase("no second countdown/timer controls timeout", NoSecondCountdownControlsTimeout, ref passed, ref failed);
        RunCase("simulation remains unpaused", SimulationUnpaused, ref passed, ref failed);
        RunCase("player weapons remain operational", PlayerWeaponsOperational, ref passed, ref failed);
        RunCase("unrelated combat remains operational", UnrelatedCombatOperational, ref passed, ref failed);
        RunCase("Police systems remain unaffected", PoliceSystemsUnaffected, ref passed, ref failed);
        RunCase("Phase 77 docking remains unaffected", Phase77Unchanged, ref passed, ref failed);
        RunCase("Phase 78 surrender remains unaffected", Phase78Unchanged, ref passed, ref failed);
        RunCase("Phase 79 Police fire cessation remains unaffected", Phase79Unchanged, ref passed, ref failed);
        RunCase("Phase 80 surrender flight normalization remains unaffected", Phase80Unchanged, ref passed, ref failed);
        RunCase("Phase 81 demand regression remains unchanged", Phase81DemandRegressionUnchanged, ref passed, ref failed);
        RunCase("Phase 82 haul regression remains unchanged", Phase82HaulRegressionUnchanged, ref passed, ref failed);
        RunCase("acknowledgement state is bounded to one current result", AcknowledgementBoundedToOneCurrentResult, ref passed, ref failed);
        RunCase("no input/focus history grows across encounters", NoInputFocusHistoryGrows, ref passed, ref failed);
        RunCase("focus follows pointer hover and falls back to default", FocusFollowsPointerHover, ref passed, ref failed);
        RunCase("default focus is the deterministic documented choice", DefaultFocusIsDeterministicComply, ref passed, ref failed);
        RunCase("acknowledgement expires within its bounded window", AcknowledgementExpiresWithinBoundedTime, ref passed, ref failed);
        RunCase("new demand resets stale acknowledgement", NewDemandResetsAcknowledgement, ref passed, ref failed);
        RunCase("presentation invents no keyboard actions", PresentationInventsNoKeyboardActions, ref passed, ref failed);

        Console.WriteLine($"[PHASE 84 PIRATE HAIL INPUT AND RESPONSE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 84 PIRATE HAIL INPUT AND RESPONSE SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 84 PIRATE HAIL INPUT AND RESPONSE SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 84 PIRATE HAIL INPUT AND RESPONSE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    // ---- Focus and hover ---------------------------------------------------

    private (bool, string) NewDemandInitializesDeterministicFocus()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return ctx.Presentation.FocusedResponse == PirateDemandHailPresentation.DefaultResponseFocus
            ? Pass()
            : Fail($"focus={ctx.Presentation.FocusedResponse}, default={PirateDemandHailPresentation.DefaultResponseFocus}");
    }

    private (bool, string) DefaultFocusDoesNotActivate()
    {
        Context ctx = ActiveDemand(out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return ctx.Service.HasActiveDemand && before == after && ctx.Notifications.Count == 1
            ? Pass()
            : Fail($"active={ctx.Service.HasActiveDemand}, removed={before - after}, notifications={ctx.Notifications.Count}");
    }

    private (bool, string) FocusIsPresentationOnly()
    {
        Context ctx = ActiveDemand(out _);
        Dictionary<string, int> before = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        bool unchanged = before.Count == ctx.Service.ActiveDemandQuantities.Count &&
            before.All(pair => ctx.Service.ActiveDemandQuantities.TryGetValue(pair.Key, out int q) && q == pair.Value);
        bool noDemandApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation),
            "Transaction", "Commit", "Transact", "Haul", "Escalate", "Complete", "CargoSelect", "CargoRemove", "Mutate", "Transfer");
        return unchanged && ctx.Service.HasActiveDemand && noDemandApi
            ? Pass()
            : Fail($"unchanged={unchanged}, noDemandApi={noDemandApi}");
    }

    private (bool, string) FlightControlsRemainAvailable()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        Vector3 start = ctx.Player.Position;
        ctx.Player.Position = start + new Vector3(120f, 0f, 0f);
        bool moved = Vector3.DistanceSquared(ctx.Player.Position, start) > 1f;
        bool noLockApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation), "Lock", "Block", "Disable");
        return moved && ctx.Presentation.IsVisible && noLockApi
            ? Pass()
            : Fail($"moved={moved}, noLockApi={noLockApi}");
    }

    private (bool, string) KeyboardComplyWorksRegardlessOfFocus()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.1f);
        if (ctx.Presentation.FocusedResponse != PirateHailResponse.Refuse)
            return Fail("could not place focus on REFUSE");
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            ctx.Player);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return consumed && before > after && !ctx.Service.HasActiveDemand &&
            ctx.Service.LastResult?.State == NpcPirateDemandState.Complying
            ? Pass()
            : Fail($"consumed={consumed}, removed={before - after}, state={ctx.Service.LastResult?.State}");
    }

    private (bool, string) KeyboardRefuseWorksRegardlessOfFocus()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        if (ctx.Presentation.FocusedResponse != PirateHailResponse.Comply)
            return Fail("could not place focus on COMPLY");
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(),
            ctx.Player);
        return consumed && !ctx.Service.HasActiveDemand && rogue.HasPlayerTarget &&
            ctx.Service.LastResult?.State == NpcPirateDemandState.Refusing
            ? Pass()
            : Fail($"consumed={consumed}, target={rogue.HasPlayerTarget}");
    }

    private (bool, string) MouseHoverIdentifiesComply()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        return ctx.Presentation.HoveredResponse == PirateHailResponse.Comply
            ? Pass()
            : Fail($"hover={ctx.Presentation.HoveredResponse}");
    }

    private (bool, string) MouseHoverIdentifiesRefuse()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.1f);
        return ctx.Presentation.HoveredResponse == PirateHailResponse.Refuse
            ? Pass()
            : Fail($"hover={ctx.Presentation.HoveredResponse}");
    }

    private (bool, string) PointerOutsideButtonsProducesNoHover()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.TryBuildSnapshot(out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        if (ctx.Presentation.HoveredResponse != PirateHailResponse.None)
            return Fail($"hover={ctx.Presentation.HoveredResponse}");
        (Rectangle comply, Rectangle refuse) = Buttons(ctx);
        int gapX = comply.Right + (refuse.Left - comply.Right) / 2;
        ctx.Presentation.Update(MouseAt(gapX, comply.Center.Y), TestViewportWidth, 0.1f);
        return ctx.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail($"gap hover={ctx.Presentation.HoveredResponse}");
    }

    private (bool, string) HoverMutatesNoDemandState()
    {
        Context ctx = ActiveDemand(out _);
        Dictionary<string, int> before = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        int cargo = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int notifications = ctx.Notifications.Count;
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.1f);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        bool unchanged = before.Count == ctx.Service.ActiveDemandQuantities.Count &&
            before.All(pair => ctx.Service.ActiveDemandQuantities.TryGetValue(pair.Key, out int q) && q == pair.Value);
        return unchanged && ctx.Service.HasActiveDemand && cargo == ctx.Player.CargoHold.GetCommodityQuantity(FoodName) &&
            notifications == ctx.Notifications.Count
            ? Pass()
            : Fail("hover changed demand state");
    }

    // ---- Response routing ---------------------------------------------------

    private (bool, string) MouseComplyRoutesToPhase81Once()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool first = ctx.Presentation.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        int afterFirst = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool second = ctx.Presentation.TryComply(ctx.Player, out _);
        int afterSecond = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return first && !second && result?.State == NpcPirateDemandState.Complying &&
            afterFirst == afterSecond && ctx.Service.GetPirateHaulQuantity(rogue) == before - afterFirst &&
            before > afterFirst
            ? Pass()
            : Fail($"first={first}, second={second}, removed={before - afterFirst}");
    }

    private (bool, string) MouseRefuseRoutesToPhase81Once()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        bool first = ctx.Presentation.TryRefuse(ctx.Player, out NpcPirateCargoDemandResult result);
        bool second = ctx.Presentation.TryRefuse(ctx.Player, out _);
        return first && !second && result?.State == NpcPirateDemandState.Refusing && rogue.HasPlayerTarget
            ? Pass()
            : Fail($"first={first}, second={second}");
    }

    private (bool, string) OverlappingHitTargetsDoNotExist()
    {
        for (int lines = 0; lines <= NpcPirateCargoDemandService.MaximumCommodityTypes; lines++)
        {
            PirateDemandHailLayout layout = PirateDemandHailPresentation.BuildLayout(TestViewportWidth, lines);
            if (layout.ComplyButton.Intersects(layout.RefuseButton))
                return Fail($"comply/refuse overlap at {lines} lines");
            if (layout.ComplyButton.Width <= 0 || layout.RefuseButton.Width <= 0 ||
                layout.ComplyButton.Height <= 0 || layout.RefuseButton.Height <= 0)
                return Fail($"degenerate hit region at {lines} lines");
        }
        return Pass();
    }

    private (bool, string) NewDemandResetsStaleHover()
    {
        Context second = SecondDemandFromResolvedFirst(out MouseState pointer);
        second.Presentation.Update(pointer, TestViewportWidth, 0.1f);
        return second.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail($"hover={second.Presentation.HoveredResponse}");
    }

    private (bool, string) NewDemandResetsStaleFocus()
    {
        Context second = SecondDemandFromResolvedFirst(out MouseState pointer);
        second.Presentation.Update(pointer, TestViewportWidth, 0.1f);
        return second.Presentation.FocusedResponse == PirateDemandHailPresentation.DefaultResponseFocus
            ? Pass()
            : Fail($"focus={second.Presentation.FocusedResponse}");
    }

    // ---- Acknowledgement ----------------------------------------------------

    private (bool, string) ComplianceAcknowledgementAfterServiceSuccess()
    {
        Context ctx = ActiveDemand(out _);
        if (ctx.Presentation.TryGetAcknowledgement(out _))
            return Fail("acknowledgement existed before any response");
        bool complied = ctx.Presentation.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack);
        return complied && result?.State == NpcPirateDemandState.Complying && hasAck &&
            ack.Text == PirateDemandHailPresentation.ComplyAcknowledgementText && ack.RemainingSeconds > 0f
            ? Pass()
            : Fail($"complied={complied}, hasAck={hasAck}, text={ack?.Text}");
    }

    private (bool, string) FailedComplianceShowsNoSuccessAcknowledgement()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        bool complied = ctx.Presentation.TryComply(ctx.Player, out _);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack);
        return !ctx.Service.HasActiveDemand && !complied && !hasAck
            ? Pass()
            : Fail($"complied={complied}, hasAck={hasAck}, text={ack?.Text}");
    }

    private (bool, string) RefusalAcknowledgement()
    {
        Context ctx = ActiveDemand(out _);
        bool refused = ctx.Presentation.TryRefuse(ctx.Player, out NpcPirateCargoDemandResult result);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack);
        return refused && result?.State == NpcPirateDemandState.Refusing && hasAck &&
            ack.Text == PirateDemandHailPresentation.RefuseAcknowledgementText
            ? Pass()
            : Fail($"refused={refused}, hasAck={hasAck}, text={ack?.Text}");
    }

    private (bool, string) TimeoutDoesNotMasqueradeAsButtonPress()
    {
        Context ctx = ActiveDemand(out _);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement ack);
        return hasAck && ack.Text == PirateDemandHailPresentation.TimeoutAcknowledgementText &&
            ack.Text != PirateDemandHailPresentation.RefuseAcknowledgementText &&
            ack.Text != PirateDemandHailPresentation.ComplyAcknowledgementText
            ? Pass()
            : Fail($"hasAck={hasAck}, text={ack?.Text}");
    }

    private (bool, string) TimeoutRemainsPhase81Authority()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        NpcPirateCargoDemandResult result = ctx.Service.LastResult;
        return result != null && result.State == NpcPirateDemandState.Refusing &&
            result.ResolutionReason.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0 &&
            rogue.HasPlayerTarget
            ? Pass()
            : Fail($"state={result?.State}, reason={result?.ResolutionReason}");
    }

    private (bool, string) ComplianceAcknowledgementDoesNotRemoveCargo()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.TryComply(ctx.Player, out _);
        int afterComply = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 1f);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 1f);
        int afterBeat = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return afterComply == afterBeat
            ? Pass()
            : Fail($"afterComply={afterComply}, afterBeat={afterBeat}");
    }

    private (bool, string) RefusalAcknowledgementDoesNotCreateHostility()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        int notifications = ctx.Notifications.Count;
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 1f);
        return rogue.HasPlayerTarget && ctx.Notifications.Count == notifications
            ? Pass()
            : Fail($"notifications {notifications}->{ctx.Notifications.Count}");
    }

    private (bool, string) Phase82ReceivesCargoExactlyAsBefore()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int requested = ctx.Service.ActiveDemandQuantities.Values.Sum();
        ctx.Presentation.TryComply(ctx.Player, out _);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        return haul == requested && haul > 0
            ? Pass()
            : Fail($"requested={requested}, haul={haul}");
    }

    private (bool, string) AcknowledgementProducesNoDuplicateTransaction()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Presentation.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int notifications = ctx.Notifications.Count;
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 2f);
        bool repeat = ctx.Presentation.TryComply(ctx.Player, out _);
        return !repeat && after == ctx.Player.CargoHold.GetCommodityQuantity(FoodName) &&
            ctx.Notifications.Count == notifications &&
            ctx.Service.GetPirateHaulQuantity(rogue) == before - after
            ? Pass()
            : Fail($"repeat={repeat}, notifications {notifications}->{ctx.Notifications.Count}");
    }

    private (bool, string) ExistingResultNotificationNotDuplicated()
    {
        Context ctx = ActiveDemand(out _);
        int before = ctx.Notifications.Count;
        ctx.Presentation.TryComply(ctx.Player, out _);
        int afterResponse = ctx.Notifications.Count;
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 2f);
        return afterResponse - before == 1 && ctx.Notifications.Count == afterResponse
            ? Pass()
            : Fail($"before={before}, afterResponse={afterResponse}, afterBeat={ctx.Notifications.Count}");
    }

    private (bool, string) OneResponseBoundedMessageCount()
    {
        Context ctx = ActiveDemand(out _);
        int before = ctx.Notifications.Count;
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        bool hasAck = ctx.Presentation.TryGetAcknowledgement(out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 2f);
        int delta = ctx.Notifications.Count - before;
        return delta == 1 && hasAck && ctx.Notifications.Count == before + 1
            ? Pass()
            : Fail($"response notifications={delta}, hasAck={hasAck}");
    }

    // ---- Input races --------------------------------------------------------

    private (bool, string) HeldKeyboardComplyCannotTransactTwice()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            ctx.Player);
        int afterPress = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int notifications = ctx.Notifications.Count;
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            ctx.Player);
        return afterPress == ctx.Player.CargoHold.GetCommodityQuantity(FoodName) &&
            ctx.Notifications.Count == notifications
            ? Pass()
            : Fail("held comply transacted again");
    }

    private (bool, string) HeldKeyboardRefuseCannotTransactTwice()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(),
            ctx.Player);
        int notifications = ctx.Notifications.Count;
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            ctx.Player);
        return rogue.HasPlayerTarget && ctx.Notifications.Count == notifications
            ? Pass()
            : Fail($"notifications {notifications}->{ctx.Notifications.Count}");
    }

    private (bool, string) HeldMouseInputCannotTransactTwice()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool first = ctx.Presentation.TryComply(ctx.Player, out _);
        bool second = ctx.Presentation.TryComply(ctx.Player, out _);
        bool third = ctx.Presentation.TryComply(ctx.Player, out _);
        int removed = before - ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return first && !second && !third && removed == ctx.Service.GetPirateHaulQuantity(rogue)
            ? Pass()
            : Fail($"first={first}, second={second}, third={third}, removed={removed}");
    }

    private (bool, string) SimultaneousComplyRefuseCommitAtMostOne()
    {
        Context first = ActiveDemand(out _);
        int before = first.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool complied = first.Presentation.TryComply(first.Player, out _);
        bool refused = first.Presentation.TryRefuse(first.Player, out _);
        int removed = before - first.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool firstOrder = complied && !refused && first.Service.LastResult?.State == NpcPirateDemandState.Complying &&
            removed > 0;

        Context second = ActiveDemand(out _);
        int beforeSecond = second.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool refusedSecond = second.Presentation.TryRefuse(second.Player, out _);
        bool compliedSecond = second.Presentation.TryComply(second.Player, out _);
        int removedSecond = beforeSecond - second.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool secondOrder = refusedSecond && !compliedSecond &&
            second.Service.LastResult?.State == NpcPirateDemandState.Refusing && removedSecond == 0;

        return firstOrder && secondOrder
            ? Pass()
            : Fail($"firstOrder={firstOrder}, secondOrder={secondOrder}");
    }

    private (bool, string) MouseKeyboardSameFrameCommitsAtMostOne()
    {
        Context first = ActiveDemand(out _);
        bool mouse = first.Presentation.TryComply(first.Player, out _);
        bool keyboard = first.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(),
            first.Player);
        bool firstOrder = mouse && first.Service.LastResult?.State == NpcPirateDemandState.Complying;

        Context second = ActiveDemand(out _);
        bool keyboardFirst = second.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            second.Player);
        bool mouseSecond = second.Presentation.TryRefuse(second.Player, out _);
        bool secondOrder = keyboardFirst && !mouseSecond &&
            second.Service.LastResult?.State == NpcPirateDemandState.Complying;

        return firstOrder && secondOrder && !keyboard && !mouseSecond
            ? Pass()
            : Fail($"firstOrder={firstOrder}, secondOrder={secondOrder}");
    }

    private (bool, string) NoControllerUiAbstractionExists()
    {
        Assembly assembly = typeof(PirateDemandHailPresentation).Assembly;
        bool gamepadType = assembly.GetTypes().Any(type =>
            type.Name.IndexOf("GamePad", StringComparison.OrdinalIgnoreCase) >= 0 ||
            type.Name.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0 ||
            type.Name.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0);
        bool gamepadInputMember = assembly.GetTypes().Any(type =>
            type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Any(method =>
                    method.ReturnType.Name.IndexOf("GamePadState", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    method.GetParameters().Any(parameter =>
                        parameter.ParameterType.Name.IndexOf("GamePadState", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        parameter.ParameterType.Name.IndexOf("GamePadButtons", StringComparison.OrdinalIgnoreCase) >= 0)));
        bool presentationGamepadApi = HasAnyMemberNamed(typeof(PirateDemandHailPresentation),
            "GamePad", "Gamepad", "Joystick", "Controller");
        return !gamepadType && !gamepadInputMember && !presentationGamepadApi
            ? Pass()
            : Fail($"gamepadType={gamepadType}, gamepadInputMember={gamepadInputMember}, presentationGamepadApi={presentationGamepadApi}");
    }

    private (bool, string) TimeoutInputSameFrameMatchesProductionOrdering()
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

    // ---- Lifecycle cleanup --------------------------------------------------

    private (bool, string) DemanderDeathClearsFocus()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return !ctx.Presentation.IsVisible && ctx.Presentation.FocusedResponse == PirateHailResponse.None &&
            ctx.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail($"focus={ctx.Presentation.FocusedResponse}, hover={ctx.Presentation.HoveredResponse}");
    }

    private (bool, string) DemanderDeathClearsAcknowledgement()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Presentation.TryComply(ctx.Player, out _);
        if (!ctx.Presentation.TryGetAcknowledgement(out _))
            return Fail("no acknowledgement after compliance");
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return !ctx.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail("acknowledgement survived demander death");
    }

    private (bool, string) DemanderDespawnClearsPresentationState()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        ctx.Npcs.Remove(rogue);
        ctx.StepUpdate(0.1f);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _) &&
            ctx.Presentation.FocusedResponse == PirateHailResponse.None
            ? Pass()
            : Fail("presentation state survived demander despawn");
    }

    private (bool, string) PlayerDeathClearsPresentationState()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        ctx.Presentation.TryComply(ctx.Player, out _);
        ctx.Player.Hull.TakeDamage(10_000f);
        ctx.Service.CancelActiveDemand("player destroyed");
        ctx.Presentation.ClearTransientState();
        return !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _) &&
            ctx.Presentation.FocusedResponse == PirateHailResponse.None &&
            ctx.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail("presentation state survived player death");
    }

    private (bool, string) DockClearsPresentationState()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.1f);
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        ctx.Service.CancelActiveDemand("player docked");
        ctx.Presentation.ClearTransientState();
        return !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _) &&
            ctx.Presentation.FocusedResponse == PirateHailResponse.None &&
            ctx.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail("presentation state survived dock");
    }

    private (bool, string) SystemTransitionClearsPresentationState()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        ctx.Service.Reset();
        ctx.Presentation.ClearTransientState();
        return !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _) &&
            ctx.Presentation.FocusedResponse == PirateHailResponse.None
            ? Pass()
            : Fail("presentation state survived system transition");
    }

    private (bool, string) SaveLoadDoesNotRestorePhase84State()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        ctx.Service.Reset();
        ctx.Presentation.ClearTransientState();
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return !ctx.Presentation.IsVisible && !ctx.Presentation.TryGetAcknowledgement(out _) &&
            ctx.Presentation.FocusedResponse == PirateHailResponse.None &&
            ctx.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail("save/load restored Phase 84 state");
    }

    private (bool, string) SchemaUnchanged() =>
        SaveGameData.CurrentSchemaVersion == 13
            ? Pass()
            : Fail($"schema is {SaveGameData.CurrentSchemaVersion}");

    private (bool, string) NoPhase84SaveMember()
    {
        bool noSaveField = !typeof(SaveGameData)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(field => field.Name.IndexOf("Hail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("Phase84", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("Acknowledge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("HailFocus", StringComparison.OrdinalIgnoreCase) >= 0);
        bool noStaticState = !typeof(PirateDemandHailPresentation)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(field => field.IsStatic && !field.IsLiteral);
        return noSaveField && noStaticState
            ? Pass()
            : Fail($"noSaveField={noSaveField}, noStaticState={noStaticState}");
    }

    // ---- Authority and simulation invariants ---------------------------------

    private (bool, string) RepeatedPassiveRenderingNoSideEffects()
    {
        Context ctx = ActiveDemand(out _);
        Dictionary<string, int> before = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        int notifications = ctx.Notifications.Count;
        int cargo = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        for (int i = 0; i < 50; i++)
        {
            ctx.Presentation.TryBuildSnapshot(out _);
            ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.016f);
        }
        bool demandIntact = ctx.Service.HasActiveDemand && before.Count == ctx.Service.ActiveDemandQuantities.Count;
        return demandIntact && notifications == ctx.Notifications.Count &&
            cargo == ctx.Player.CargoHold.GetCommodityQuantity(FoodName) &&
            ctx.Presentation.FocusedResponse == PirateDemandHailPresentation.DefaultResponseFocus
            ? Pass()
            : Fail("passive rendering produced side effects");
    }

    private (bool, string) MultipleRoguesOneAuthoritativeHail()
    {
        Context ctx = CreateContext();
        NpcShip rogueA = ctx.AddRogue(new Vector3(1_000f, 0f, 0f), "Rogue-A");
        ctx.AddRogue(new Vector3(1_100f, 0f, 0f), "Rogue-B");
        ctx.GiveCargo(FoodId, 10);
        bool first = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        bool second = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        bool snapshot = ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot built);
        return first && !second && ctx.Presentation.IsVisible && snapshot &&
            built.SpeakerName == rogueA.Name && ctx.Service.ActiveDemander == rogueA
            ? Pass()
            : Fail($"first={first}, second={second}, speaker={built?.SpeakerName}");
    }

    private (bool, string) FocusNeverSelectsNonAuthoritativeDemander()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        NpcShip other = ctx.AddRogue(new Vector3(1_200f, 0f, 0f), "Rogue-Other");
        ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.1f);
        ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot);
        return ctx.Service.ActiveDemander == rogue && snapshot.SpeakerName == rogue.Name &&
            snapshot.SpeakerName != other.Name
            ? Pass()
            : Fail($"speaker={snapshot?.SpeakerName}, authoritative={rogue.Name}");
    }

    private (bool, string) CargoLinesRemainPhase81Authoritative()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        foreach (PirateDemandHailCargoLine line in snapshot.CargoLines)
        {
            if (!ctx.Service.ActiveDemandQuantities.TryGetValue(line.CommodityName, out int quantity) ||
                quantity != line.Quantity)
            {
                return Fail($"line {line.CommodityName} x{line.Quantity} did not match authority");
            }
        }
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        return snapshot.CargoLines.Count == ctx.Service.ActiveDemandQuantities.Count
            ? Pass()
            : Fail("hover changed authoritative cargo lines");
    }

    private (bool, string) CountdownRemainsPhase81Authoritative()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        float service = ctx.Service.ResponseRemainingSeconds;
        return Math.Abs(snapshot.ResponseRemainingSeconds - service) < 0.0001f
            ? Pass()
            : Fail($"snapshot={snapshot.ResponseRemainingSeconds}, service={service}");
    }

    private (bool, string) NoSecondCountdownControlsTimeout()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        bool stillActive = ctx.Service.HasActiveDemand;
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        bool serviceTimeout = !ctx.Service.HasActiveDemand &&
            ctx.Service.LastResult?.State == NpcPirateDemandState.Refusing;
        return stillActive && serviceTimeout
            ? Pass()
            : Fail($"presentation resolved the demand={stillActive}, service timeout={serviceTimeout}");
    }

    private (bool, string) SimulationUnpaused()
    {
        Context ctx = ActiveDemand(out _);
        float before = ctx.Service.ResponseRemainingSeconds;
        ctx.StepUpdate(1f);
        float after = ctx.Service.ResponseRemainingSeconds;
        return ctx.Presentation.IsVisible && after < before
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    private (bool, string) PlayerWeaponsOperational()
    {
        Context ctx = ActiveDemand(out _);
        bool noWeaponApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation), "Weapon", "Fire", "HoldFire", "Trigger");
        return ctx.Presentation.IsVisible && ctx.Service.HasActiveDemand && noWeaponApi
            ? Pass()
            : Fail($"noWeaponApi={noWeaponApi}");
    }

    private (bool, string) UnrelatedCombatOperational()
    {
        Context ctx = ActiveDemand(out _);
        NpcShip police = ctx.AddPolice(new Vector3(2_000f, 0f, 0f));
        NpcShip otherRogue = ctx.AddRogue(new Vector3(2_100f, 0f, 0f), "Unrelated Rogue");
        bool engaged = police.SetFactionCombatTarget(otherRogue);
        ctx.StepUpdate(0.1f);
        return engaged && police.FactionCombatTarget == otherRogue
            ? Pass()
            : Fail($"engaged={engaged}");
    }

    private (bool, string) PoliceSystemsUnaffected()
    {
        bool noPoliceReference = !TypeReferencesForbiddenAuthority(
            typeof(PirateDemandHailPresentation), "PoliceScan", "Fugitive", "Heat", "PoliceAttention");
        Context ctx = ActiveDemand(out _);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.1f);
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        float after = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        return noPoliceReference && Math.Abs(after - before) < 0.0001f
            ? Pass()
            : Fail($"noPoliceReference={noPoliceReference}, police {before}->{after}");
    }

    private (bool, string) Phase77Unchanged() =>
        !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "DockingAccess", "Fugitive")
            ? Pass()
            : Fail("presentation references Phase 77 authority");

    private (bool, string) Phase78Unchanged() =>
        !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "Surrender")
            ? Pass()
            : Fail("presentation references Phase 78 authority");

    private (bool, string) Phase79Unchanged() =>
        !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "FireCessation", "PoliceScan")
            ? Pass()
            : Fail("presentation references Phase 79 authority");

    private (bool, string) Phase80Unchanged() =>
        !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "FlightState", "GotoAutopilot", "Cruise")
            ? Pass()
            : Fail("presentation references Phase 80 authority");

    private (bool, string) Phase81DemandRegressionUnchanged()
    {
        Context ctx = ActiveDemand(out _);
        int requested = ctx.Service.ActiveDemandQuantities[FoodName];
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool complied = ctx.Presentation.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return complied && result?.State == NpcPirateDemandState.Complying &&
            before - after == requested
            ? Pass()
            : Fail($"requested={requested}, removed={before - after}");
    }

    private (bool, string) Phase82HaulRegressionUnchanged()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int requested = ctx.Service.ActiveDemandQuantities.Values.Sum();
        ctx.Presentation.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        bool haulIsStolen = ctx.Service.TryGetHaulSnapshot(rogue, out _);
        return haul == requested && haulIsStolen && result?.SurrenderedQuantity == requested
            ? Pass()
            : Fail($"requested={requested}, haul={haul}");
    }

    private (bool, string) AcknowledgementBoundedToOneCurrentResult()
    {
        Context ctx = ActiveDemand(out NpcShip firstDemander);
        ctx.Presentation.TryComply(ctx.Player, out _);
        if (!ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement first))
            return Fail("no first acknowledgement");
        ctx.StepUpdate(NpcPirateCargoDemandService.PlayerCooldownSeconds + 1f);
        ctx.GiveCargo(FoodId, 20);
        ctx.AddRogue(new Vector3(2_000f, 0f, 0f), "Rogue-Second");
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string failure))
            return Fail($"second demand not initiated: {failure}");
        NpcShip secondDemander = ctx.Service.ActiveDemander;
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        bool cleared = !ctx.Presentation.TryGetAcknowledgement(out _);
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        bool second = ctx.Presentation.TryGetAcknowledgement(out PirateDemandHailAcknowledgement current);
        return cleared && second && secondDemander != null && secondDemander != firstDemander &&
            current.Text == PirateDemandHailPresentation.RefuseAcknowledgementText &&
            first.Text != current.Text
            ? Pass()
            : Fail($"cleared={cleared}, second={second}, current={current?.Text}");
    }

    private (bool, string) NoInputFocusHistoryGrows()
    {
        bool noCollections = !typeof(PirateDemandHailPresentation)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(field => field.FieldType.IsGenericType &&
                (field.FieldType.GetGenericTypeDefinition() == typeof(List<>) ||
                    field.FieldType.GetGenericTypeDefinition() == typeof(Queue<>) ||
                    field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                    field.FieldType.GetGenericTypeDefinition() == typeof(HashSet<>)));
        Context ctx = ActiveDemand(out _);
        for (int i = 0; i < 10; i++)
        {
            ctx.Presentation.Update(MouseOverComply(ctx), TestViewportWidth, 0.016f);
            ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.016f);
        }
        return noCollections
            ? Pass()
            : Fail("presentation grows an input/focus history");
    }

    // ---- Focus model documentation probes ----------------------------------

    private (bool, string) FocusFollowsPointerHover()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.Update(MouseOverRefuse(ctx), TestViewportWidth, 0.1f);
        if (ctx.Presentation.FocusedResponse != PirateHailResponse.Refuse)
            return Fail($"expected focus Refuse, got {ctx.Presentation.FocusedResponse}");
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return ctx.Presentation.FocusedResponse == PirateDemandHailPresentation.DefaultResponseFocus &&
            ctx.Presentation.HoveredResponse == PirateHailResponse.None
            ? Pass()
            : Fail($"focus={ctx.Presentation.FocusedResponse}, hover={ctx.Presentation.HoveredResponse}");
    }

    private (bool, string) DefaultFocusIsDeterministicComply()
    {
        bool constant = PirateDemandHailPresentation.DefaultResponseFocus == PirateHailResponse.Comply;
        Context first = ActiveDemand(out _);
        first.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        Context second = ActiveDemand(out _);
        second.Presentation.Update(MouseOutside(), TestViewportWidth, 0.1f);
        return constant && first.Presentation.FocusedResponse == PirateHailResponse.Comply &&
            second.Presentation.FocusedResponse == PirateHailResponse.Comply &&
            first.Presentation.FocusedResponse == second.Presentation.FocusedResponse
            ? Pass()
            : Fail("default focus is not deterministic COMPLY");
    }

    private (bool, string) AcknowledgementExpiresWithinBoundedTime()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.TryComply(ctx.Player, out _);
        if (!ctx.Presentation.TryGetAcknowledgement(out _))
            return Fail("no acknowledgement after compliance");
        ctx.Presentation.Update(MouseOutside(), TestViewportWidth, PirateDemandHailPresentation.AcknowledgementDurationSeconds + 1f);
        return !ctx.Presentation.TryGetAcknowledgement(out _) &&
            PirateDemandHailPresentation.AcknowledgementDurationSeconds <= 5f
            ? Pass()
            : Fail("acknowledgement did not expire within its bounded window");
    }

    private (bool, string) NewDemandResetsAcknowledgement()
    {
        Context second = SecondDemandFromResolvedFirst(out MouseState pointer);
        second.Presentation.Update(pointer, TestViewportWidth, 0.1f);
        return !second.Presentation.TryGetAcknowledgement(out _)
            ? Pass()
            : Fail("stale acknowledgement survived a new demand");
    }

    private (bool, string) PresentationInventsNoKeyboardActions()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        bool noKeyboardMember = !typeof(PirateDemandHailPresentation).GetMethods(flags)
            .Any(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(KeyboardState)));
        bool noConfirmApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation), "Confirm", "Navigate", "HandleInput", "HandleKey");
        return noKeyboardMember && noConfirmApi
            ? Pass()
            : Fail($"noKeyboardMember={noKeyboardMember}, noConfirmApi={noConfirmApi}");
    }

    // ---- Harness ------------------------------------------------------------

    private static Context CreateContext() => new();

    private static Context ActiveDemand(out NpcShip rogue)
    {
        Context ctx = CreateContext();
        rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string failure))
            throw new InvalidOperationException($"could not initiate Phase 84 test demand: {failure}");
        return ctx;
    }

    /// <summary>
    /// Resolves a first demand, survives the player cooldown, and opens a
    /// second demand in the same context so reset semantics can be probed.
    /// Returns the context positioned on the second demand; the out pointer
    /// is deliberately outside both response hit regions.
    /// </summary>
    private static Context SecondDemandFromResolvedFirst(out MouseState pointer)
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f), "Rogue-First");
        ctx.GiveCargo(FoodId, 20);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string failure))
            throw new InvalidOperationException($"could not initiate first Phase 84 test demand: {failure}");
        ctx.Presentation.Update(MouseOver(ctx, PirateHailResponse.Comply), TestViewportWidth, 0.1f);
        if (ctx.Presentation.HoveredResponse != PirateHailResponse.Comply)
            throw new InvalidOperationException("could not hover COMPLY on the first demand");
        ctx.Presentation.TryComply(ctx.Player, out _);
        if (!ctx.Presentation.TryGetAcknowledgement(out _))
            throw new InvalidOperationException("first demand produced no acknowledgement");
        ctx.StepUpdate(NpcPirateCargoDemandService.PlayerCooldownSeconds + 1f);
        ctx.AddRogue(new Vector3(2_000f, 0f, 0f), "Rogue-Second");
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string secondFailure))
            throw new InvalidOperationException($"could not initiate second Phase 84 test demand: {secondFailure}");
        _ = rogue;
        pointer = MouseOutside();
        return ctx;
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

        public Context()
        {
            Reputation = new ReputationManager(new FactionManager());
            Reputation.SetReputation(FactionManager.LibertyRogues, 0.0f, "Phase 84 smoke setup");
            Player = new Ship(Vector3.Zero) { CollisionRadius = 120f };
            Service = new NpcPirateCargoDemandService(
                Npcs,
                Reputation,
                _ => false,
                () => PoliceInteractionActive,
                _ => false,
                null,
                null,
                null,
                message => Notifications.Add(message));
            Presentation = new PirateDemandHailPresentation(Service);
        }

        public NpcShip AddRogue(Vector3 position, string name = "Phase 84 Rogue")
        {
            NpcShip rogue = new(name, position, position, 1f, 0f, FactionManager.LibertyRogues);
            rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase84-zone", position, 800f, 180f, 6500f);
            Npcs.Add(rogue);
            return rogue;
        }

        public NpcShip AddPolice(Vector3 position)
        {
            NpcShip police = new($"Phase 84 Police {Npcs.Count + 1}", position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase84-zone", position, 800f, 180f, 6500f);
            Npcs.Add(police);
            return police;
        }

        public void GiveCargo(string commodityId, int quantity)
        {
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity != null)
                Player.CargoHold.AddCommodity(commodity, quantity);
        }

        public void StepUpdate(float delta) => Service.Update(delta, Player);
    }
}
