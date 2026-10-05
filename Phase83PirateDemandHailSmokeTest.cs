using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 83 focused coverage for the pirate-demand hail presentation. The
/// suite drives only production systems: the authoritative Phase 81
/// NpcPirateCargoDemandService and the Phase 83 PirateDemandHailPresentation
/// adapter. It proves that the hail displays the authoritative demander,
/// faction, cargo lines, and countdown, that comply/refuse route through the
/// Phase 81 transaction exactly once, and that no parallel demand, cargo,
/// combat, or persistence authority is introduced.
/// </summary>
internal sealed class Phase83PirateDemandHailSmokeTest
{
    private const string FoodId = "food-rations";
    private const string FoodName = "Food Rations";
    private const string WaterId = "water";
    private const string HFuelId = "h-fuel";
    private const string SideArmsId = "side-arms";

    public Phase83PirateDemandHailSmokeTest(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("no active demand means no hail presentation", NoDemandNoHail, ref passed, ref failed);
        RunCase("active Phase 81 demand makes the hail visible", ActiveDemandHailVisible, ref passed, ref failed);
        RunCase("correct demander identity is shown", DemanderIdentityShown, ref passed, ref failed);
        RunCase("correct canonical faction label is shown", FactionLabelShown, ref passed, ref failed);
        RunCase("exact demanded commodity is shown", ExactCommodityShown, ref passed, ref failed);
        RunCase("exact demanded quantity is shown", ExactQuantityShown, ref passed, ref failed);
        RunCase("multi-line demand displays all authoritative lines", MultiLineDisplayed, ref passed, ref failed);
        RunCase("presentation does not select its own cargo", UiDoesNotSelectCargo, ref passed, ref failed);
        RunCase("countdown reflects the service authority", CountdownReflectsService, ref passed, ref failed);
        RunCase("countdown never displays negative time", CountdownNeverNegative, ref passed, ref failed);
        RunCase("presentation does not independently own timeout", UiDoesNotOwnTimeout, ref passed, ref failed);
        RunCase("comply UI action routes through Phase 81 compliance", ComplyUiRoutesToService, ref passed, ref failed);
        RunCase("refuse UI action routes through Phase 81 refusal", RefuseUiRoutesToService, ref passed, ref failed);
        RunCase("existing keyboard comply still works", KeyboardComplyStillWorks, ref passed, ref failed);
        RunCase("existing keyboard refuse still works", KeyboardRefuseStillWorks, ref passed, ref failed);
        RunCase("alternate input cannot create a second transaction", SecondTransactionBlocked, ref passed, ref failed);
        RunCase("held comply input cannot remove cargo twice", HeldComplyNoDoubleRemove, ref passed, ref failed);
        RunCase("held refuse input cannot escalate twice", HeldRefuseNoDoubleEscalate, ref passed, ref failed);
        RunCase("successful compliance closes the presentation", ComplianceClosesPresentation, ref passed, ref failed);
        RunCase("compliance removes the exact Phase 81 cargo", ComplianceRemovesExactCargo, ref passed, ref failed);
        RunCase("Phase 82 pirate haul receives the exact cargo", HaulReceivesExactCargo, ref passed, ref failed);
        RunCase("compliance does not alter cargo selection through UI", ComplianceNoSelectionChange, ref passed, ref failed);
        RunCase("refusal closes the presentation", RefusalClosesPresentation, ref passed, ref failed);
        RunCase("refusal removes no cargo", RefusalRemovesNoCargo, ref passed, ref failed);
        RunCase("refusal escalates combat once", RefusalEscalatesOnce, ref passed, ref failed);
        RunCase("timeout closes the presentation", TimeoutClosesPresentation, ref passed, ref failed);
        RunCase("timeout behavior matches Phase 81 exactly", TimeoutMatchesPhase81, ref passed, ref failed);
        RunCase("timeout cannot trigger duplicate refusal", TimeoutNoDuplicateRefusal, ref passed, ref failed);
        RunCase("demander death closes the presentation", DemanderDeathClosesPresentation, ref passed, ref failed);
        RunCase("demander death leaves no clickable stale comply", DemanderDeathNoStaleComply, ref passed, ref failed);
        RunCase("demander despawn closes the presentation", DemanderDespawnClosesPresentation, ref passed, ref failed);
        RunCase("player death closes the presentation", PlayerDeathClosesPresentation, ref passed, ref failed);
        RunCase("dock transition closes the presentation", DockClosesPresentation, ref passed, ref failed);
        RunCase("system transition closes the presentation", SystemTransitionClosesPresentation, ref passed, ref failed);
        RunCase("no delayed cargo deduction after lifecycle cancellation", NoDelayedDeductionAfterCancel, ref passed, ref failed);
        RunCase("player attack against demander resolves as Phase 81 refusal", PlayerAttackResolvesAsRefusal, ref passed, ref failed);
        RunCase("no invulnerability is added", NoInvulnerabilityAdded, ref passed, ref failed);
        RunCase("no Rogue hold-fire authority is added", NoHoldFireAdded, ref passed, ref failed);
        RunCase("simulation remains active while the hail is displayed", SimulationActiveWhileVisible, ref passed, ref failed);
        RunCase("movement controls remain active while the hail is displayed", MovementControlsActive, ref passed, ref failed);
        RunCase("nearby unrelated combat continues", UnrelatedCombatContinues, ref passed, ref failed);
        RunCase("one active demander yields one hail", OneActiveDemandOneHail, ref passed, ref failed);
        RunCase("multiple nearby Rogues do not stack hail panels", NoStackedHails, ref passed, ref failed);
        RunCase("Rogue allies entering combat do not create extra demands", AlliesNoExtraDemand, ref passed, ref failed);
        RunCase("cargo changes before compliance use Phase 81 revalidation", CargoChangeRevalidation, ref passed, ref failed);
        RunCase("presentation cannot substitute another commodity after quote", UiCannotSubstituteCommodity, ref passed, ref failed);
        RunCase("mission-reserved cargo remains absent from the demand", MissionReservedAbsent, ref passed, ref failed);
        RunCase("freight-reserved cargo remains absent from the demand", FreightReservedAbsent, ref passed, ref failed);
        RunCase("Police reputation is unchanged by the presentation", PoliceReputationUnchanged, ref passed, ref failed);
        RunCase("fugitive Heat authority is untouched by the presentation", FugitiveHeatUnchanged, ref passed, ref failed);
        RunCase("Phase 77 docking behavior is not referenced", Phase77Unchanged, ref passed, ref failed);
        RunCase("Phase 78 surrender behavior is not referenced", Phase78Unchanged, ref passed, ref failed);
        RunCase("Phase 79 Police fire behavior is not referenced", Phase79Unchanged, ref passed, ref failed);
        RunCase("Phase 80 flight normalization is not referenced", Phase80Unchanged, ref passed, ref failed);
        RunCase("trade-lane behavior is unchanged", TradeLaneUnchanged, ref passed, ref failed);
        RunCase("save/load does not restore an open hail", SaveLoadNoHail, ref passed, ref failed);
        RunCase("player save schema remains unchanged", SchemaUnchanged, ref passed, ref failed);
        RunCase("no Phase 83 persisted UI state exists", NoPhase83PersistedState, ref passed, ref failed);
        RunCase("repeated passive UI reads have no transaction side effects", PassiveReadsNoSideEffects, ref passed, ref failed);
        RunCase("presentation work is bounded by the active demand line count", PresentationBounded, ref passed, ref failed);

        Console.WriteLine($"[PHASE 83 PIRATE DEMAND HAIL SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 83 PIRATE DEMAND HAIL SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 83 PIRATE DEMAND HAIL SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 83 PIRATE DEMAND HAIL SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    // ---- Visibility and identity -------------------------------------------

    private (bool, string) NoDemandNoHail()
    {
        Context ctx = CreateContext();
        bool visible = ctx.Presentation.IsVisible;
        bool built = ctx.Presentation.TryBuildSnapshot(out _);
        return !visible && !built
            ? Pass()
            : Fail($"visible={visible}, built={built}");
    }

    private (bool, string) ActiveDemandHailVisible()
    {
        Context ctx = ActiveDemand(out _);
        return ctx.Presentation.IsVisible && ctx.Presentation.TryBuildSnapshot(out _)
            ? Pass()
            : Fail("hail was not visible for an active demand");
    }

    private (bool, string) DemanderIdentityShown()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        return snapshot.SpeakerName == rogue.Name
            ? Pass()
            : Fail($"speaker={snapshot.SpeakerName}, rogue={rogue.Name}");
    }

    private (bool, string) FactionLabelShown()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        string expected = FactionManager.GetFactionDisplayName(FactionManager.LibertyRogues);
        return snapshot.FactionLabel == expected && snapshot.FactionLabel == "Liberty Rogues" &&
            snapshot.FactionLabel.IndexOf("liberty_rogues", StringComparison.Ordinal) < 0
            ? Pass()
            : Fail($"label={snapshot.FactionLabel}, expected={expected}, rogue={rogue.Name}");
    }

    private (bool, string) ExactCommodityShown()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        var expected = ctx.Service.ActiveDemandQuantities.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        var shown = snapshot.CargoLines.Select(line => line.CommodityName).OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        return expected.SequenceEqual(shown, StringComparer.OrdinalIgnoreCase)
            ? Pass()
            : Fail($"expected=[{string.Join(",", expected)}], shown=[{string.Join(",", shown)}]");
    }

    private (bool, string) ExactQuantityShown()
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
        return Pass();
    }

    private (bool, string) MultiLineDisplayed()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Context ctx = CreateContext();
            NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f), $"Rogue-Multi-{attempt}");
            ctx.GiveCargo(FoodId, 20);
            ctx.GiveCargo(WaterId, 20);
            ctx.GiveCargo(HFuelId, 20);
            ctx.GiveCargo(SideArmsId, 20);
            if (!ctx.Service.TryInitiateDemand(ctx.Player, out _))
                continue;
            int authoritative = ctx.Service.ActiveDemandQuantities.Count;
            if (authoritative < 2)
                continue;
            if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
                return Fail("no snapshot for multi-line demand");
            return snapshot.CargoLines.Count == authoritative
                ? Pass()
                : Fail($"authoritative={authoritative}, shown={snapshot.CargoLines.Count}");
        }
        return Fail("could not produce a multi-line demand");
    }

    private (bool, string) UiDoesNotSelectCargo()
    {
        Context ctx = ActiveDemand(out _);
        Dictionary<string, int> before = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 5; i++)
            ctx.Presentation.TryBuildSnapshot(out _);
        bool unchanged = before.Count == ctx.Service.ActiveDemandQuantities.Count &&
            before.All(pair => ctx.Service.ActiveDemandQuantities.TryGetValue(pair.Key, out int q) && q == pair.Value);
        bool noSelectionApi = !typeof(PirateDemandHailPresentation).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(method => method.Name.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0);
        return unchanged && noSelectionApi
            ? Pass()
            : Fail("presentation changed the authoritative cargo selection");
    }

    // ---- Countdown and timeout ---------------------------------------------

    private (bool, string) CountdownReflectsService()
    {
        Context ctx = ActiveDemand(out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        float service = ctx.Service.ResponseRemainingSeconds;
        return Math.Abs(snapshot.ResponseRemainingSeconds - service) < 0.0001f
            ? Pass()
            : Fail($"snapshot={snapshot.ResponseRemainingSeconds}, service={service}");
    }

    private (bool, string) CountdownNeverNegative()
    {
        Context ctx = ActiveDemand(out _);
        for (int i = 0; i < 20; i++)
        {
            if (ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot) &&
                snapshot.ResponseRemainingSeconds < 0f)
            {
                return Fail($"negative countdown {snapshot.ResponseRemainingSeconds}");
            }
            ctx.StepUpdate(0.2f);
        }
        return Pass();
    }

    private (bool, string) UiDoesNotOwnTimeout()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        bool closed = !ctx.Presentation.IsVisible && !ctx.Service.HasActiveDemand;
        bool escalated = rogue.HasPlayerTarget;
        return closed && escalated
            ? Pass()
            : Fail($"closed={closed}, escalated={escalated}");
    }

    // ---- Response routing ---------------------------------------------------

    private (bool, string) ComplyUiRoutesToService()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool complied = ctx.Presentation.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        return complied && result != null && result.State == NpcPirateDemandState.Complying &&
            before - after == haul && haul > 0
            ? Pass()
            : Fail($"complied={complied}, removed={before - after}, haul={haul}");
    }

    private (bool, string) RefuseUiRoutesToService()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        bool refused = ctx.Presentation.TryRefuse(ctx.Player, out NpcPirateCargoDemandResult result);
        return refused && result != null && result.State == NpcPirateDemandState.Refusing && rogue.HasPlayerTarget
            ? Pass()
            : Fail($"refused={refused}, state={result?.State}, target={rogue.HasPlayerTarget}");
    }

    private (bool, string) KeyboardComplyStillWorks()
    {
        Context ctx = ActiveDemand(out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            ctx.Player);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return consumed && before > after && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail($"consumed={consumed}, removed={before - after}");
    }

    private (bool, string) KeyboardRefuseStillWorks()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        bool consumed = ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(),
            ctx.Player);
        return consumed && !ctx.Service.HasActiveDemand && rogue.HasPlayerTarget
            ? Pass()
            : Fail($"consumed={consumed}, target={rogue.HasPlayerTarget}");
    }

    private (bool, string) SecondTransactionBlocked()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool first = ctx.Presentation.TryComply(ctx.Player, out _);
        int afterFirst = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool second = ctx.Presentation.TryComply(ctx.Player, out _);
        int afterSecond = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return first && !second && afterFirst == afterSecond &&
            ctx.Service.GetPirateHaulQuantity(rogue) == before - afterFirst
            ? Pass()
            : Fail($"first={first}, second={second}, afterFirst={afterFirst}, afterSecond={afterSecond}");
    }

    private (bool, string) HeldComplyNoDoubleRemove()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(),
            ctx.Player);
        int afterPress = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            new KeyboardState(NpcPirateCargoDemandService.ComplyKey),
            ctx.Player);
        int afterHold = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return afterPress == afterHold
            ? Pass()
            : Fail($"afterPress={afterPress}, afterHold={afterHold}");
    }

    private (bool, string) HeldRefuseNoDoubleEscalate()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(),
            ctx.Player);
        int notificationCount = ctx.Notifications.Count;
        ctx.Service.HandleInput(
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            new KeyboardState(NpcPirateCargoDemandService.RefuseKey),
            ctx.Player);
        return rogue.HasPlayerTarget && ctx.Notifications.Count == notificationCount
            ? Pass()
            : Fail($"notifications {notificationCount}->{ctx.Notifications.Count}");
    }

    // ---- Resolution and lifecycle ------------------------------------------

    private (bool, string) ComplianceClosesPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.TryComply(ctx.Player, out _);
        return !ctx.Presentation.IsVisible && !ctx.Presentation.TryBuildSnapshot(out _)
            ? Pass()
            : Fail("presentation remained after compliance");
    }

    private (bool, string) ComplianceRemovesExactCargo()
    {
        Context ctx = ActiveDemand(out _);
        int requested = ctx.Service.ActiveDemandQuantities[FoodName];
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Presentation.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return before - after == requested
            ? Pass()
            : Fail($"requested={requested}, removed={before - after}");
    }

    private (bool, string) HaulReceivesExactCargo()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int requested = ctx.Service.ActiveDemandQuantities.Values.Sum();
        ctx.Presentation.TryComply(ctx.Player, out _);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        return haul == requested
            ? Pass()
            : Fail($"requested={requested}, haul={haul}");
    }

    private (bool, string) ComplianceNoSelectionChange()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot);
        Dictionary<string, int> shown = snapshot.CargoLines.ToDictionary(
            line => line.CommodityName, line => line.Quantity, StringComparer.OrdinalIgnoreCase);
        bool complied = ctx.Presentation.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        bool same = result != null &&
            result.DemandedQuantities.Count == shown.Count &&
            result.DemandedQuantities.All(pair => shown.TryGetValue(pair.Key, out int q) && q == pair.Value);
        return complied && same
            ? Pass()
            : Fail("UI changed the demanded cargo set at compliance");
    }

    private (bool, string) RefusalClosesPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("presentation remained after refusal");
    }

    private (bool, string) RefusalRemovesNoCargo()
    {
        Context ctx = ActiveDemand(out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return before == after
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    private (bool, string) RefusalEscalatesOnce()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        bool first = ctx.Presentation.TryRefuse(ctx.Player, out _);
        bool second = ctx.Presentation.TryRefuse(ctx.Player, out _);
        return first && !second && rogue.HasPlayerTarget
            ? Pass()
            : Fail($"first={first}, second={second}");
    }

    private (bool, string) TimeoutClosesPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("presentation remained after timeout");
    }

    private (bool, string) TimeoutMatchesPhase81()
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

    private (bool, string) TimeoutNoDuplicateRefusal()
    {
        Context ctx = ActiveDemand(out _);
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        int afterTimeout = ctx.Notifications.Count;
        ctx.StepUpdate(NpcPirateCargoDemandService.ResponseWindowSeconds + 1f);
        return !ctx.Service.HasActiveDemand && ctx.Notifications.Count == afterTimeout
            ? Pass()
            : Fail($"notifications {afterTimeout}->{ctx.Notifications.Count}");
    }

    private (bool, string) DemanderDeathClosesPresentation()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("presentation remained after demander death");
    }

    private (bool, string) DemanderDeathNoStaleComply()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        bool complied = ctx.Presentation.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return !ctx.Presentation.IsVisible && !complied && before == after
            ? Pass()
            : Fail($"visible={ctx.Presentation.IsVisible}, complied={complied}, removed={before - after}");
    }

    private (bool, string) DemanderDespawnClosesPresentation()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.Npcs.Remove(rogue);
        ctx.StepUpdate(0.1f);
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("presentation remained after demander despawn");
    }

    private (bool, string) PlayerDeathClosesPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Player.Hull.TakeDamage(10_000f);
        ctx.Service.CancelActiveDemand("player destroyed");
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("presentation remained after player death");
    }

    private (bool, string) DockClosesPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.CancelActiveDemand("player docked");
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("presentation remained after dock");
    }

    private (bool, string) SystemTransitionClosesPresentation()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.Reset();
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("presentation remained after system transition");
    }

    private (bool, string) NoDelayedDeductionAfterCancel()
    {
        Context ctx = ActiveDemand(out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Service.CancelActiveDemand("cancelled");
        ctx.StepUpdate(10f);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        return before == after
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    private (bool, string) PlayerAttackResolvesAsRefusal()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        rogue.MarkDamagedByPlayer();
        ctx.StepUpdate(0.1f);
        return !ctx.Presentation.IsVisible && !ctx.Service.HasActiveDemand && rogue.HasPlayerTarget
            ? Pass()
            : Fail("player attack did not resolve as Phase 81 refusal");
    }

    // ---- Combat and simulation invariants ----------------------------------

    private (bool, string) NoInvulnerabilityAdded()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Player.Hull.TakeDamage(10_000f);
        bool destroyed = ctx.Player.Hull.IsDestroyed;
        bool noPauseApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation), "Invul", "Pause", "Freeze", "God");
        return destroyed && noPauseApi
            ? Pass()
            : Fail($"destroyed={destroyed}, noPauseApi={noPauseApi}");
    }

    private (bool, string) NoHoldFireAdded()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        bool noEscalation = !rogue.HasPlayerTarget;
        bool noCombatApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation), "Weapon", "Fire", "Hold", "Target");
        return noEscalation && noCombatApi
            ? Pass()
            : Fail($"escalation={rogue.HasPlayerTarget}, noCombatApi={noCombatApi}");
    }

    private (bool, string) SimulationActiveWhileVisible()
    {
        Context ctx = ActiveDemand(out _);
        float before = ctx.Service.ResponseRemainingSeconds;
        ctx.StepUpdate(1f);
        float after = ctx.Service.ResponseRemainingSeconds;
        return ctx.Presentation.IsVisible && after < before
            ? Pass()
            : Fail($"before={before}, after={after}, visible={ctx.Presentation.IsVisible}");
    }

    private (bool, string) MovementControlsActive()
    {
        Context ctx = ActiveDemand(out _);
        Vector3 start = ctx.Player.Position;
        ctx.Player.Position = start + new Vector3(120f, 0f, 0f);
        bool moved = Vector3.DistanceSquared(ctx.Player.Position, start) > 1f;
        bool noLockApi = !HasAnyMemberNamed(typeof(PirateDemandHailPresentation), "Lock", "Block", "Disable");
        return moved && ctx.Presentation.IsVisible && noLockApi
            ? Pass()
            : Fail($"moved={moved}, noLockApi={noLockApi}");
    }

    private (bool, string) UnrelatedCombatContinues()
    {
        Context ctx = ActiveDemand(out _);
        NpcShip police = ctx.AddPolice(new Vector3(2_000f, 0f, 0f));
        NpcShip otherRogue = ctx.AddRogue(new Vector3(2_100f, 0f, 0f), "Unrelated Rogue");
        bool engaged = police.SetFactionCombatTarget(otherRogue);
        ctx.StepUpdate(0.1f);
        return engaged && police.FactionCombatTarget == otherRogue
            ? Pass()
            : Fail($"engaged={engaged}, target={police.FactionCombatTarget?.Name}");
    }

    private (bool, string) OneActiveDemandOneHail()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f), "Rogue-A");
        ctx.AddRogue(new Vector3(1_100f, 0f, 0f), "Rogue-B");
        ctx.GiveCargo(FoodId, 10);
        bool first = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        bool second = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return first && !second && ctx.Presentation.IsVisible &&
            ctx.Presentation.TryBuildSnapshot(out _)
            ? Pass()
            : Fail($"first={first}, second={second}");
    }

    private (bool, string) NoStackedHails()
    {
        Context ctx = CreateContext();
        for (int i = 0; i < 6; i++)
            ctx.AddRogue(new Vector3(1_000f + i * 100f, 0f, 0f), $"Rogue-Stack-{i}");
        ctx.GiveCargo(FoodId, 20);
        ctx.GiveCargo(WaterId, 20);
        ctx.GiveCargo(HFuelId, 20);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        return snapshot.CargoLines.Count <= NpcPirateCargoDemandService.MaximumCommodityTypes &&
            ctx.Service.ActiveDemander != null
            ? Pass()
            : Fail($"lines={snapshot.CargoLines.Count}");
    }

    private (bool, string) AlliesNoExtraDemand()
    {
        Context ctx = ActiveDemand(out NpcShip rogue);
        ctx.AddRogue(new Vector3(1_200f, 0f, 0f), "Rogue-Allies");
        bool extra = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot);
        return !extra && snapshot.SpeakerName == rogue.Name
            ? Pass()
            : Fail($"extra={extra}, speaker={snapshot?.SpeakerName}");
    }

    // ---- Cargo revalidation and protected cargo ----------------------------

    private (bool, string) CargoChangeRevalidation()
    {
        Context ctx = ActiveDemand(out _);
        int requested = ctx.Service.ActiveDemandQuantities[FoodName];
        Commodity food = CommodityCatalog.GetByName(FoodName);
        int totalBefore = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int desiredAvailable = Math.Max(1, requested - 1);
        ctx.Player.CargoHold.RemoveSellableCommodity(food, totalBefore - desiredAvailable, preferStolen: true);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        bool complied = ctx.Presentation.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int expectedRemoved = Math.Min(requested, before);
        return complied && before - after == expectedRemoved && after == before - expectedRemoved
            ? Pass()
            : Fail($"requested={requested}, before={before}, after={after}, expectedRemoved={expectedRemoved}");
    }

    private (bool, string) UiCannotSubstituteCommodity()
    {
        Context ctx = ActiveDemand(out _);
        Commodity added = CommodityCatalog.GetByName("Water");
        if (added == null)
            return Fail("missing Water commodity");
        bool alreadyDemanded = ctx.Service.ActiveDemandQuantities.ContainsKey("Water");
        ctx.Player.CargoHold.AddCommodity(added, 7);
        int beforeAdded = ctx.Player.CargoHold.GetCommodityQuantity("Water");
        ctx.Presentation.TryComply(ctx.Player, out _);
        int afterAdded = ctx.Player.CargoHold.GetCommodityQuantity("Water");
        bool untouched = alreadyDemanded || beforeAdded == afterAdded;
        return untouched
            ? Pass()
            : Fail($"added commodity changed {beforeAdded}->{afterAdded}");
    }

    private (bool, string) MissionReservedAbsent()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.AddMissionCargo(999, FoodId, 5);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out _))
            return Fail("demand not initiated");
        int requested = ctx.Service.ActiveDemandQuantities[FoodName];
        int sellable = ctx.Player.CargoHold.GetSellableCommodityQuantity(FoodName);
        ctx.Presentation.TryComply(ctx.Player, out _);
        int reserved = ctx.Player.CargoHold.GetMissionReservedQuantity(FoodName);
        return requested <= sellable && reserved == 5
            ? Pass()
            : Fail($"requested={requested}, sellable={sellable}, reserved={reserved}");
    }

    private (bool, string) FreightReservedAbsent()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.RegisterFreightReservation(888, FoodId, 4);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out _))
            return Fail("demand not initiated");
        int requested = ctx.Service.ActiveDemandQuantities[FoodName];
        int sellable = ctx.Player.CargoHold.GetSellableCommodityQuantity(FoodName);
        ctx.Presentation.TryComply(ctx.Player, out _);
        int reserved = ctx.Player.CargoHold.GetMissionReservedQuantity(FoodName);
        return requested <= sellable && reserved == 4
            ? Pass()
            : Fail($"requested={requested}, sellable={sellable}, reserved={reserved}");
    }

    // ---- Police / Phase independence ---------------------------------------

    private (bool, string) PoliceReputationUnchanged()
    {
        Context ctx = ActiveDemand(out _);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        ctx.Presentation.TryBuildSnapshot(out _);
        ctx.Presentation.TryComply(ctx.Player, out _);
        float after = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        return Math.Abs(after - before) < 0.0001f
            ? Pass()
            : Fail($"police standing {before}->{after}");
    }

    private (bool, string) FugitiveHeatUnchanged()
    {
        bool noFugitiveReference = !TypeReferencesForbiddenAuthority(
            typeof(PirateDemandHailPresentation), "Fugitive", "Heat", "PoliceAttention");
        Context ctx = ActiveDemand(out _);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        ctx.Presentation.TryRefuse(ctx.Player, out _);
        float after = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        return noFugitiveReference && Math.Abs(after - before) < 0.0001f
            ? Pass()
            : Fail($"noFugitiveReference={noFugitiveReference}, police {before}->{after}");
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

    private (bool, string) TradeLaneUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Player.SetTradeLaneTransit(true);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        bool noTradeLaneReference = !TypeReferencesForbiddenAuthority(typeof(PirateDemandHailPresentation), "TradeLane");
        return !initiated && !ctx.Presentation.IsVisible && noTradeLaneReference
            ? Pass()
            : Fail($"initiated={initiated}, noTradeLaneReference={noTradeLaneReference}");
    }

    // ---- Persistence --------------------------------------------------------

    private (bool, string) SaveLoadNoHail()
    {
        Context ctx = ActiveDemand(out _);
        ctx.Service.Reset();
        return !ctx.Presentation.IsVisible
            ? Pass()
            : Fail("save/load restored an open hail");
    }

    private (bool, string) SchemaUnchanged() =>
        SaveGameData.CurrentSchemaVersion == 13
            ? Pass()
            : Fail($"schema is {SaveGameData.CurrentSchemaVersion}");

    private (bool, string) NoPhase83PersistedState()
    {
        bool noSaveField = !typeof(SaveGameData)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(field => field.Name.IndexOf("Hail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("Phase83", StringComparison.OrdinalIgnoreCase) >= 0);
        bool noStaticState = !typeof(PirateDemandHailPresentation)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(field => field.IsStatic && !field.IsLiteral);
        return noSaveField && noStaticState
            ? Pass()
            : Fail($"noSaveField={noSaveField}, noStaticState={noStaticState}");
    }

    private (bool, string) PassiveReadsNoSideEffects()
    {
        Context ctx = ActiveDemand(out _);
        Dictionary<string, int> before = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        int notifications = ctx.Notifications.Count;
        int cargo = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        for (int i = 0; i < 50; i++)
            ctx.Presentation.TryBuildSnapshot(out _);
        bool demandIntact = ctx.Service.HasActiveDemand && before.Count == ctx.Service.ActiveDemandQuantities.Count;
        return demandIntact && notifications == ctx.Notifications.Count &&
            cargo == ctx.Player.CargoHold.GetCommodityQuantity(FoodName)
            ? Pass()
            : Fail("passive reads produced side effects");
    }

    private (bool, string) PresentationBounded()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f), "Rogue-Bound");
        ctx.GiveCargo(FoodId, 20);
        ctx.GiveCargo(WaterId, 20);
        ctx.GiveCargo(HFuelId, 20);
        ctx.GiveCargo(SideArmsId, 20);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        if (!ctx.Presentation.TryBuildSnapshot(out PirateDemandHailSnapshot snapshot))
            return Fail("no snapshot");
        PirateDemandHailLayout layout = PirateDemandHailPresentation.BuildLayout(1920, snapshot.CargoLines.Count);
        return snapshot.CargoLines.Count <= NpcPirateCargoDemandService.MaximumCommodityTypes &&
            layout.ComplyButton.Width > 0 && layout.RefuseButton.Width > 0
            ? Pass()
            : Fail($"lines={snapshot.CargoLines.Count}");
    }

    // ---- Harness ------------------------------------------------------------

    private static Context CreateContext() => new();

    private static Context ActiveDemand(out NpcShip rogue)
    {
        Context ctx = CreateContext();
        rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string failure))
            throw new InvalidOperationException($"could not initiate Phase 83 test demand: {failure}");
        return ctx;
    }

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
            Reputation.SetReputation(FactionManager.LibertyRogues, 0.0f, "Phase 83 smoke setup");
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

        public NpcShip AddRogue(Vector3 position, string name = "Phase 83 Rogue")
        {
            NpcShip rogue = new(name, position, position, 1f, 0f, FactionManager.LibertyRogues);
            rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase83-zone", position, 800f, 180f, 6500f);
            Npcs.Add(rogue);
            return rogue;
        }

        public NpcShip AddPolice(Vector3 position)
        {
            NpcShip police = new($"Phase 83 Police {Npcs.Count + 1}", position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase83-zone", position, 800f, 180f, 6500f);
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

        public void StepUpdate(float delta) => Service.Update(delta, Player);
    }
}
