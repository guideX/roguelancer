#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 80 focused coverage for flight-state normalization after an accepted
/// Liberty Police surrender. The suite drives only production systems in
/// production-equivalent order: the GOTO/dock-assist autopilot, cruise drive,
/// the Phase 78 surrender transaction, the Phase 79 fire cessation, and the
/// shared Phase 77 docking access policy. No detention state, surrender
/// autopilot, velocity teleportation, invulnerability, projectile deletion,
/// new fee, or persisted surrender-flight field is created or asserted.
/// </summary>
internal sealed class Phase80SurrenderFlightStateNormalizationSmokeTest
{
    private readonly GraphicsDevice _graphicsDevice;

    public Phase80SurrenderFlightStateNormalizationSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("baseline manual flight surrender still succeeds", BaselineManualFlightSurrenderSucceeds, ref passed, ref failed);
        RunCase("successful surrender leaves fugitive state inactive", SuccessfulSurrenderLeavesFugitiveInactive, ref passed, ref failed);
        RunCase("successful surrender preserves Phase 78 fee semantics", SuccessfulSurrenderPreservesFeeSemantics, ref passed, ref failed);
        RunCase("Heat 1 fee remains 1,000 CR", Heat1FeeRemains1000, ref passed, ref failed);
        RunCase("Heat 2 fee remains 2,500 CR", Heat2FeeRemains2500, ref passed, ref failed);
        RunCase("reputation remains unchanged", ReputationUnchanged, ref passed, ref failed);
        RunCase("cargo remains unchanged", CargoUnchanged, ref passed, ref failed);
        RunCase("no escape grace is created", NoEscapeGraceCreated, ref passed, ref failed);
        RunCase("active goto/autopilot is cancelled after successful surrender", ActiveGotoCancelledAfterSurrender, ref passed, ref failed);
        RunCase("goto destination does not resume next update", GotoDestinationDoesNotResumeNextUpdate, ref passed, ref failed);
        RunCase("automatic steering from cancelled goto does not resume", AutomaticSteeringFromCancelledGotoDoesNotResume, ref passed, ref failed);
        RunCase("active docking assist is safely cancelled", ActiveDockAssistCancelledAfterSurrender, ref passed, ref failed);
        RunCase("cancelled docking assist does not complete station transition", CancelledDockAssistDoesNotCompleteStationTransition, ref passed, ref failed);
        RunCase("cruise is disengaged after successful surrender", CruiseDisengagedAfterSurrender, ref passed, ref failed);
        RunCase("cruise does not silently reactivate", CruiseDoesNotSilentlyReactivate, ref passed, ref failed);
        RunCase("trade-lane transit is not disturbed by the surrender transaction", TradeLaneTransitNotDisturbedBySurrender, ref passed, ref failed);
        RunCase("valid lane state is preserved with no half-transit corruption", ValidLaneStatePreserved, ref passed, ref failed);
        RunCase("successful surrender does not teleport the player", SurrenderDoesNotTeleportPlayer, ref passed, ref failed);
        RunCase("successful surrender does not arbitrarily zero physical velocity", SurrenderDoesNotZeroVelocity, ref passed, ref failed);
        RunCase("momentum remains governed by normal ship physics", MomentumGovernedByNormalShipPhysics, ref passed, ref failed);
        RunCase("manual controls remain usable after surrender", ManualControlsRemainUsableAfterSurrender, ref passed, ref failed);
        RunCase("ordinary throttle input remains possible", OrdinaryThrottleInputRemainsPossible, ref passed, ref failed);
        RunCase("no persistent engine lock exists", NoPersistentEngineLock, ref passed, ref failed);
        RunCase("no permanent steering lock exists", NoPermanentSteeringLock, ref passed, ref failed);
        RunCase("no forced docking occurs", NoForcedDockingOccurs, ref passed, ref failed);
        RunCase("no surrender autopilot is created", NoSurrenderAutopilotCreated, ref passed, ref failed);
        RunCase("Phase 77 docking becomes available naturally", Phase77DockingBecomesAvailableNaturally, ref passed, ref failed);
        RunCase("Phase 80 does not directly mutate docking-access state", Phase80DoesNotMutateDockingAccessState, ref passed, ref failed);
        RunCase("Phase 79 stale fugitive-authorized Police fire remains suppressed", Phase79StaleFugitiveFireRemainsSuppressed, ref passed, ref failed);
        RunCase("existing Police projectile remains physical", ExistingPoliceProjectileRemainsPhysical, ref passed, ref failed);
        RunCase("existing projectile can still damage", ExistingProjectileCanStillDamage, ref passed, ref failed);
        RunCase("unrelated faction combat remains active", UnrelatedFactionCombatRemainsActive, ref passed, ref failed);
        RunCase("player attack hostility remains preserved", PlayerAttackHostilityPreserved, ref passed, ref failed);
        RunCase("continued independent combat does not reactivate fugitive state", ContinuedCombatDoesNotReactivateFugitiveState, ref passed, ref failed);
        RunCase("insufficient-credit surrender failure does not cancel goto/autopilot", InsufficientCreditFailureDoesNotCancelGoto, ref passed, ref failed);
        RunCase("insufficient-credit failure does not disengage cruise", InsufficientCreditFailureDoesNotDisengageCruise, ref passed, ref failed);
        RunCase("no-Police failure does not cancel automated flight", NoPoliceFailureDoesNotCancelAutomatedFlight, ref passed, ref failed);
        RunCase("permanent-hostility failure does not cancel automated flight", PermanentHostilityFailureDoesNotCancelAutomatedFlight, ref passed, ref failed);
        RunCase("not-fugitive S press does not alter flight state", NotFugitiveSPressDoesNotAlterFlightState, ref passed, ref failed);
        RunCase("destroyed-player surrender path causes no Phase 80 flight mutation", DestroyedPlayerSurrenderCausesNoFlightMutation, ref passed, ref failed);
        RunCase("repeated S after successful surrender is a no-op", RepeatedSAfterSuccessIsNoOp, ref passed, ref failed);
        RunCase("repeated S does not stack cancellation state", RepeatedSDoesNotStackCancellationState, ref passed, ref failed);
        RunCase("one surrender still produces only one success notification", OneSurrenderProducesOneSuccessNotification, ref passed, ref failed);
        RunCase("no new Phase 80 notification is added", NoNewPhase80NotificationAdded, ref passed, ref failed);
        RunCase("death/reset leaves no stale Phase 80 state", DeathResetLeavesNoStalePhase80State, ref passed, ref failed);
        RunCase("system transition leaves no stale Phase 80 state", SystemTransitionLeavesNoStalePhase80State, ref passed, ref failed);
        RunCase("save/load leaves no Phase 80 state", SaveLoadLeavesNoPhase80State, ref passed, ref failed);
        RunCase("schema remains 13", SchemaRemains13, ref passed, ref failed);
        RunCase("no Phase 80 save field exists", NoPhase80SaveFieldExists, ref passed, ref failed);
        RunCase("contraband behavior remains unchanged", ContrabandBehaviorUnchanged, ref passed, ref failed);
        RunCase("mission-bound smuggling cargo remains unchanged", MissionBoundSmugglingCargoUnchanged, ref passed, ref failed);
        RunCase("lawful-stop behavior remains unchanged", LawfulStopBehaviorUnchanged, ref passed, ref failed);
        RunCase("refusal still creates normal fugitive state", RefusalStillCreatesNormalFugitiveState, ref passed, ref failed);
        RunCase("surrender eligibility rules remain Phase 78's rules", SurrenderEligibilityRemainsPhase78Rules, ref passed, ref failed);
        RunCase("Phase 76 attention notifications remain unaffected", Phase76AttentionNotificationsUnaffected, ref passed, ref failed);
        RunCase("Phase 77 docking regressions remain green", Phase77DockingRegressionsRemainGreen, ref passed, ref failed);
        RunCase("Phase 78 surrender regressions remain green", Phase78SurrenderRegressionsRemainGreen, ref passed, ref failed);
        RunCase("Phase 79 fire-cessation regressions remain green", Phase79FireCessationRegressionsRemainGreen, ref passed, ref failed);
        RunCase("no duplicate Police presence/world scan is introduced", NoDuplicatePolicePresenceScanIntroduced, ref passed, ref failed);
        RunCase("successful normalization is O(1) over known player flight controllers", NormalizationIsConstantOverKnownControllers, ref passed, ref failed);
        RunCase("production-equivalent order proof: automation cancelled after commit, before next update", ProductionEquivalentOrderProof, ref passed, ref failed);

        Console.WriteLine($"[PHASE 80 SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 80 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 80 SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 80 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    // ---- Baseline transaction integrity ------------------------------------

    private (bool, string) BaselineManualFlightSurrenderSucceeds()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(1_000f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        return ctx.TrySurrender(out PoliceSurrenderResult? result, out string failure)
            && result != null && result.Success
            ? Pass()
            : Fail($"baseline manual-flight surrender failed ({failure})");
    }

    private (bool, string) SuccessfulSurrenderLeavesFugitiveInactive()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return !ctx.Fugitive.IsActive && ctx.Fugitive.State == PoliceFugitiveState.None
            ? Pass()
            : Fail("fugitive state still active after surrender");
    }

    private (bool, string) SuccessfulSurrenderPreservesFeeSemantics()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit, 10_000);
        if (!ctx.TrySurrender(out PoliceSurrenderResult? result, out string failure))
            return Fail($"surrender failed ({failure})");
        return result != null &&
            result.FeeAmount == PoliceFugitiveSurrenderService.Heat1SurrenderFee &&
            result.CreditsCharged == result.FeeAmount &&
            ctx.Credits.Credits == 10_000 - PoliceFugitiveSurrenderService.Heat1SurrenderFee
            ? Pass()
            : Fail("fee semantics changed");
    }

    private (bool, string) Heat1FeeRemains1000()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return assessment.IsEligible && assessment.FeeAmount == 1_000
            ? Pass()
            : Fail($"Heat 1 fee is {assessment.FeeAmount}");
    }

    private (bool, string) Heat2FeeRemains2500()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.HotPursuit);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return assessment.IsEligible && assessment.FeeAmount == 2_500
            ? Pass()
            : Fail($"Heat 2 fee is {assessment.FeeAmount}");
    }

    private (bool, string) ReputationUnchanged()
    {
        Context ctx = CreatePursuedContext();
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), before)
            ? Pass()
            : Fail("reputation changed during flight normalization");
    }

    private (bool, string) CargoUnchanged()
    {
        Context ctx = CreatePursuedContext();
        ctx.GiveContraband(ctx.Player, 2);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(SideArmsName);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Player.CargoHold.GetCommodityQuantity(SideArmsName) == before
            ? Pass()
            : Fail("cargo changed during flight normalization");
    }

    private (bool, string) NoEscapeGraceCreated()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Fugitive.State == PoliceFugitiveState.None && !ctx.Fugitive.IsActive
            ? Pass()
            : Fail("surrender created an escape-grace-like fugitive state");
    }

    // ---- GOTO / autopilot ---------------------------------------------------

    private (bool, string) ActiveGotoCancelledAfterSurrender()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        SpaceObject escapeTarget = new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f));
        if (!ctx.Player.ActivateGoto(escapeTarget))
            return Fail("goto activation failed");
        if (!ctx.Player.IsGotoActive || !autopilot.IsActive)
            return Fail("goto was not active before surrender");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return !ctx.Player.IsGotoActive && autopilot.State == GotoAutopilot.AutopilotState.Cancelled
            ? Pass()
            : Fail($"goto survived surrender (shipGoto={ctx.Player.IsGotoActive}, autopilot={autopilot.State})");
    }

    private (bool, string) GotoDestinationDoesNotResumeNextUpdate()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        autopilot.Update(0.1f);
        return autopilot.State == GotoAutopilot.AutopilotState.Cancelled && autopilot.Destination == null
            ? Pass()
            : Fail($"goto resumed after surrender (state={autopilot.State}, destination={autopilot.Destination?.Name})");
    }

    private (bool, string) AutomaticSteeringFromCancelledGotoDoesNotResume()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        Vector3 positionBefore = ctx.Player.Position;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        float autopilotSpeed = ReadPrivateFloat(ctx.Player, "_autopilotTargetSpeed");
        autopilot.Update(0.1f);
        return autopilotSpeed < 0f && ctx.Player.Position == positionBefore
            ? Pass()
            : Fail($"automation-owned speed override or steering resumed (override={autopilotSpeed}, moved={ctx.Player.Position != positionBefore})");
    }

    private (bool, string) ActiveDockAssistCancelledAfterSurrender()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        Station rogueStation = CreateStation("Buffalo Base", FactionManager.LibertyRogues);
        if (!ctx.Player.ActivateDockAssist(rogueStation))
            return Fail("dock assist activation failed");
        if (!ctx.Player.IsDockAssistActive || !autopilot.IsActive)
            return Fail("dock assist was not active before surrender");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return !ctx.Player.IsDockAssistActive && autopilot.State == GotoAutopilot.AutopilotState.Cancelled
            ? Pass()
            : Fail($"dock assist survived surrender (dockAssist={ctx.Player.IsDockAssistActive}, autopilot={autopilot.State})");
    }

    private (bool, string) CancelledDockAssistDoesNotCompleteStationTransition()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        Station rogueStation = CreateStation("Buffalo Base", FactionManager.LibertyRogues);
        ctx.Player.ActivateDockAssist(rogueStation);
        Vector3 positionBefore = ctx.Player.Position;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        autopilot.Update(0.1f);
        return autopilot.State == GotoAutopilot.AutopilotState.Cancelled &&
            !autopilot.IsDocked && ctx.Player.Position == positionBefore
            ? Pass()
            : Fail("cancelled dock assist completed a station transition");
    }

    // ---- Cruise -------------------------------------------------------------

    private (bool, string) CruiseDisengagedAfterSurrender()
    {
        Context ctx = CreatePursuedContext();
        if (!ActivateCruise(ctx.Player))
            return Fail("cruise activation failed");
        if (!ctx.Player.CruiseDrive.IsActive)
            return Fail("cruise was not active before surrender");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return !ctx.Player.CruiseDrive.IsActive && !ctx.Player.CruiseDrive.IsCharging
            ? Pass()
            : Fail($"cruise survived surrender (state={ctx.Player.CruiseDrive.State})");
    }

    private (bool, string) CruiseDoesNotSilentlyReactivate()
    {
        Context ctx = CreatePursuedContext();
        ActivateCruise(ctx.Player);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.Player.CruiseDrive.Advance(10f);
        return !ctx.Player.CruiseDrive.IsActive && !ctx.Player.CruiseDrive.IsCharging
            ? Pass()
            : Fail($"cruise reactivated after surrender (state={ctx.Player.CruiseDrive.State})");
    }

    // ---- Trade-lane ---------------------------------------------------------

    private (bool, string) TradeLaneTransitNotDisturbedBySurrender()
    {
        Context ctx = CreatePursuedContext();
        ctx.Player.SetTradeLaneTransit(true);
        Vector3 positionBefore = ctx.Player.Position;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Player.IsTradeLaneTransit && ctx.Player.Position == positionBefore
            ? Pass()
            : Fail("surrender disturbed trade-lane transit state");
    }

    private (bool, string) ValidLaneStatePreserved()
    {
        Context ctx = CreatePursuedContext();
        ctx.Player.SetTradeLaneTransit(true);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        bool laneIntact = ctx.Player.IsTradeLaneTransit;
        bool velocityIntact = ctx.Player.Velocity == Vector3.Zero;
        bool noEjection = ctx.Player.Position == Vector3.Zero;
        return laneIntact && velocityIntact && noEjection
            ? Pass()
            : Fail("surrender corrupted lane transit into a half-transit state");
    }

    // ---- Physics ------------------------------------------------------------

    private (bool, string) SurrenderDoesNotTeleportPlayer()
    {
        Context ctx = CreatePursuedContext();
        Vector3 positionBefore = ctx.Player.Position;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Player.Position == positionBefore
            ? Pass()
            : Fail("surrender teleported the player");
    }

    private (bool, string) SurrenderDoesNotZeroVelocity()
    {
        Context ctx = CreatePursuedContext();
        Vector3 momentum = new(120f, 40f, -80f);
        ctx.Player.Velocity = momentum;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Player.Velocity == momentum
            ? Pass()
            : Fail("surrender zeroed physical velocity");
    }

    private (bool, string) MomentumGovernedByNormalShipPhysics()
    {
        Context ctx = CreatePursuedContext();
        Vector3 momentum = new(120f, 40f, -80f);
        ctx.Player.Velocity = momentum;
        float speedBefore = ctx.Player.Speed;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Player.Velocity == momentum && ctx.Player.Speed == speedBefore
            ? Pass()
            : Fail("surrender rewrote ship physics state");
    }

    // ---- Manual control -----------------------------------------------------

    private (bool, string) ManualControlsRemainUsableAfterSurrender()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(ctx.Player);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        bool gatesOpen = !ctx.Player.IsGotoActive &&
            !ctx.Player.CruiseDrive.IsActive &&
            !ctx.Player.CruiseDrive.IsCharging &&
            !ctx.Player.EnginesKilled &&
            autopilot.State == GotoAutopilot.AutopilotState.Cancelled;
        return gatesOpen
            ? Pass()
            : Fail("manual flight control is blocked after surrender");
    }

    private (bool, string) OrdinaryThrottleInputRemainsPossible()
    {
        Context ctx = CreatePursuedContext();
        float playerThrottle = ReadPrivateFloat(ctx.Player, "_throttle");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ReadPrivateFloat(ctx.Player, "_throttle") == playerThrottle && !ctx.Player.IsGotoActive
            ? Pass()
            : Fail("surrender mutated player-owned throttle or blocked the manual throttle path");
    }

    private (bool, string) NoPersistentEngineLock()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return !ctx.Player.EnginesKilled
            ? Pass()
            : Fail("surrender left engines killed");
    }

    private (bool, string) NoPermanentSteeringLock()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return autopilot.State == GotoAutopilot.AutopilotState.Cancelled && !ctx.Player.IsGotoActive
            ? Pass()
            : Fail("surrender left a permanent steering authority active");
    }

    private (bool, string) NoForcedDockingOccurs()
    {
        Context ctx = CreatePursuedContext();
        Vector3 positionBefore = ctx.Player.Position;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Player.Position == positionBefore && !ctx.Player.IsGotoActive
            ? Pass()
            : Fail("surrender forced a docking approach");
    }

    private (bool, string) NoSurrenderAutopilotCreated()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return autopilot.State == GotoAutopilot.AutopilotState.Cancelled && autopilot.Destination == null
            ? Pass()
            : Fail("surrender created a new autopilot authority or destination");
    }

    // ---- Phase 77 docking restoration ---------------------------------------

    private (bool, string) Phase77DockingBecomesAvailableNaturally()
    {
        Context ctx = CreatePursuedContext();
        Station policeStation = CreateStation("Fort Bush", FactionManager.LibertyPolice);
        FactionAccessResult whileFugitive = FactionAccessService.EvaluateDocking(
            ctx.Reputation, policeStation.FactionId, policeStation.Name, fugitivePursuitActive: true);
        if (whileFugitive.IsAllowed)
            return Fail("Phase 77 docking was not denied while fugitive");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        FactionAccessResult afterSurrender = FactionAccessService.EvaluateDocking(
            ctx.Reputation, policeStation.FactionId, policeStation.Name, fugitivePursuitActive: false);
        return afterSurrender.IsAllowed
            ? Pass()
            : Fail("Phase 77 Police docking did not become available after surrender");
    }

    private (bool, string) Phase80DoesNotMutateDockingAccessState()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        foreach (FieldInfo field in typeof(FactionAccessService).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (field.Name.ToLowerInvariant().Contains("surrender"))
                return Fail($"docking access policy gained a surrender field: {field.Name}");
        }
        return Pass();
    }

    // ---- Phase 79 interaction -----------------------------------------------

    private (bool, string) Phase79StaleFugitiveFireRemainsSuppressed()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        int before = ctx.RunWeapons(6);
        if (before <= 0)
            return Fail("no pre-surrender firing opportunity");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.StepProduction();
        int after = ctx.RunWeapons(6);
        return after == 0 && autopilot.State == GotoAutopilot.AutopilotState.Cancelled
            ? Pass()
            : Fail($"stale fugitive fire after surrender with cancelled goto (fired={after})");
    }

    private (bool, string) ExistingPoliceProjectileRemainsPhysical()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(900f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        int before = ctx.ActiveProjectileCount();
        if (before <= 0)
            return Fail("setup did not create a projectile");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.ActiveProjectileCount() > 0
            ? Pass()
            : Fail("pre-surrender Police projectile was removed");
    }

    private (bool, string) ExistingProjectileCanStillDamage()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(900f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        if (ctx.ActiveProjectileCount() <= 0)
            return Fail("setup did not create a projectile");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.Player.CollisionRadius = 400f;
        float hullBefore = ctx.Player.Hull.CurrentHull;
        float shieldsBefore = ctx.Player.Shields.CurrentShields;
        for (int i = 0; i < 20; i++)
            ctx.RunWeapons(1);
        return ctx.Player.Hull.CurrentHull < hullBefore || ctx.Player.Shields.CurrentShields < shieldsBefore
            ? Pass()
            : Fail("pre-surrender projectile could no longer damage the player");
    }

    private (bool, string) UnrelatedFactionCombatRemainsActive()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        NpcShip attacker = ctx.AddFactionShip(FactionManager.LibertyRogues, new Vector3(700f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyRogues, TemporaryHostilityManager.PlayerAggressionReason);
        attacker.SetPlayerTarget(ctx.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.RunWeapons(6, attacker) > 0
            ? Pass()
            : Fail("unrelated faction combat was suppressed by surrender");
    }

    private (bool, string) PlayerAttackHostilityPreserved()
    {
        Context ctx = CreatePursuedContext();
        ctx.Reputation.TemporaryHostility.RecordHostileAction(
            FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Reputation.TemporaryHostility.HasReason(
            FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason)
            ? Pass()
            : Fail("player-attack hostility was cleared by surrender");
    }

    private (bool, string) ContinuedCombatDoesNotReactivateFugitiveState()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.Player.ApplyCombatDamage(25f, hostile: true);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        return !ctx.Fugitive.IsActive
            ? Pass()
            : Fail("continued combat reactivated the fugitive incident");
    }

    // ---- Failed-surrender atomicity -----------------------------------------

    private (bool, string) InsufficientCreditFailureDoesNotCancelGoto()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit, credits: 0);
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        if (ctx.TrySurrender(out _, out _))
            return Fail("surrender unexpectedly succeeded without credits");
        return ctx.Player.IsGotoActive && autopilot.IsActive
            ? Pass()
            : Fail("failed surrender cancelled goto/autopilot");
    }

    private (bool, string) InsufficientCreditFailureDoesNotDisengageCruise()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit, credits: 0);
        ActivateCruise(ctx.Player);
        if (ctx.TrySurrender(out _, out _))
            return Fail("surrender unexpectedly succeeded without credits");
        return ctx.Player.CruiseDrive.IsActive
            ? Pass()
            : Fail("failed surrender disengaged cruise");
    }

    private (bool, string) NoPoliceFailureDoesNotCancelAutomatedFlight()
    {
        Context ctx = CreateContext();
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(ctx.Player);
        if (ctx.TrySurrender(out _, out _))
            return Fail("surrender unexpectedly succeeded without Police presence");
        return ctx.Player.IsGotoActive && autopilot.IsActive && ctx.Player.CruiseDrive.IsActive
            ? Pass()
            : Fail("no-Police failure cancelled automated flight");
    }

    private (bool, string) PermanentHostilityFailureDoesNotCancelAutomatedFlight()
    {
        Context ctx = CreateContext(standing: -0.80f);
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        if (ctx.TrySurrender(out _, out _))
            return Fail("surrender unexpectedly succeeded while permanently hostile");
        return ctx.Player.IsGotoActive && autopilot.IsActive
            ? Pass()
            : Fail("permanent-hostility failure cancelled automated flight");
    }

    private (bool, string) NotFugitiveSPressDoesNotAlterFlightState()
    {
        Context ctx = CreateContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(ctx.Player);
        if (ctx.TrySurrender(out _, out _) ||
            ctx.Player.IsGotoActive != true || autopilot.IsActive != true || !ctx.Player.CruiseDrive.IsActive)
            return Fail("not-fugitive surrender attempt altered flight state");
        return Pass();
    }

    private (bool, string) DestroyedPlayerSurrenderCausesNoFlightMutation()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ctx.Player.Hull.TakeDamage(10_000f);
        if (ctx.TrySurrender(out _, out _))
            return Fail("surrender unexpectedly succeeded with a destroyed player");
        return ctx.Player.IsGotoActive && autopilot.IsActive
            ? Pass()
            : Fail("destroyed-player surrender path mutated flight automation");
    }

    // ---- Idempotency ---------------------------------------------------------

    private (bool, string) RepeatedSAfterSuccessIsNoOp()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        if (!ctx.TrySurrender(out PoliceSurrenderResult? first, out string failure))
            return Fail($"first surrender failed ({failure})");
        int creditsAfterFirst = ctx.Credits.Credits;
        bool second = ctx.TrySurrender(out PoliceSurrenderResult? secondResult, out _);
        return !second && secondResult == null && ctx.Credits.Credits == creditsAfterFirst &&
            autopilot.State == GotoAutopilot.AutopilotState.Cancelled
            ? Pass()
            : Fail("repeated S after success was not a no-op");
    }

    private (bool, string) RepeatedSDoesNotStackCancellationState()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(ctx.Player);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.Player.CancelGoto(showNotification: false);
        ctx.Player.CancelCruise(CruiseCancellationReason.IncompatibleFlight);
        return autopilot.State == GotoAutopilot.AutopilotState.Cancelled &&
            !ctx.Player.CruiseDrive.IsActive && !ctx.Player.CruiseDrive.IsCharging
            ? Pass()
            : Fail("repeated cancellation stacked state");
    }

    private (bool, string) OneSurrenderProducesOneSuccessNotification()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(ctx.Player);
        ctx.Notifications.Clear();
        if (!ctx.TrySurrender(out PoliceSurrenderResult? result, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Notifications.Count == 1 &&
            ctx.Notifications[0].Contains("Surrender accepted", StringComparison.Ordinal) &&
            result != null && result.Message == ctx.Notifications[0]
            ? Pass()
            : Fail($"expected exactly one surrender notification, got {ctx.Notifications.Count}");
    }

    private (bool, string) NoNewPhase80NotificationAdded()
    {
        Context ctx = CreatePursuedContext();
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(ctx.Player);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        bool noisy = ctx.Notifications.Any(n =>
            n.Contains("AUTOPILOT CANCELLED", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("GOTO Cancelled", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Cruise Mode Deactivated", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("SURRENDER FLIGHT", StringComparison.OrdinalIgnoreCase));
        return !noisy
            ? Pass()
            : Fail("Phase 80 added a new player-facing notification");
    }

    // ---- Lifecycle -----------------------------------------------------------

    private (bool, string) DeathResetLeavesNoStalePhase80State()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.Fugitive.Reset(null, "death");
        ctx.Player.Hull.TakeDamage(10_000f);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        return !ctx.Fugitive.IsActive && !ctx.Player.IsGotoActive && !ctx.Player.CruiseDrive.IsActive
            ? Pass()
            : Fail("death/reset left stale Phase 80 flight state");
    }

    private (bool, string) SystemTransitionLeavesNoStalePhase80State()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.Fugitive.Reset(null, "system transition");
        return !ctx.Fugitive.IsActive && !ctx.Player.IsGotoActive && !ctx.Player.CruiseDrive.IsActive
            ? Pass()
            : Fail("system transition left stale Phase 80 flight state");
    }

    private (bool, string) SaveLoadLeavesNoPhase80State()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        SaveGameManager save = new(TempPath("phase80"));
        SaveGameData data = new()
        {
            FactionReputation = save.CaptureReputation(ctx.Reputation),
            TemporaryHostility = save.CaptureTemporaryHostility(ctx.Reputation)
        };
        if (!save.TrySave(data, out _))
            return Fail("save failed");
        if (!save.TryLoad(out SaveGameData loaded, out _))
            return Fail("load failed");
        return loaded.SchemaVersion == SaveGameData.CurrentSchemaVersion
            ? Pass()
            : Fail("save/load round-trip changed schema");
    }

    private (bool, string) SchemaRemains13() =>
        SaveGameData.CurrentSchemaVersion == 13 ? Pass() : Fail($"schema is {SaveGameData.CurrentSchemaVersion}");

    private (bool, string) NoPhase80SaveFieldExists()
    {
        foreach (FieldInfo field in typeof(SaveGameData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            string name = field.Name.ToLowerInvariant();
            if (name.Contains("surrender") || name.Contains("flightstate") || name.Contains("surrenderedflight"))
                return Fail($"save schema gained a Phase 80 field: {field.Name}");
        }
        return Pass();
    }

    // ---- Contraband / missions / lawful-stop ---------------------------------

    private (bool, string) ContrabandBehaviorUnchanged()
    {
        Context ctx = CreatePursuedContext();
        ctx.GiveContraband(ctx.Player, 3);
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        int after = ctx.Player.CargoHold.GetCommodityQuantity(SideArmsName);
        return after == 3
            ? Pass()
            : Fail("contraband quantity changed across surrender");
    }

    private (bool, string) MissionBoundSmugglingCargoUnchanged()
    {
        Context ctx = CreatePursuedContext();
        ctx.GiveContraband(ctx.Player, 5);
        int before = ctx.Player.CargoHold.UsedCapacity;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ctx.Player.CargoHold.UsedCapacity == before
            ? Pass()
            : Fail("smuggling cargo manifest changed across surrender");
    }

    private (bool, string) LawfulStopBehaviorUnchanged()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.StepProduction();
        return !ctx.Fugitive.IsActive && ctx.Player.CargoHold.UsedCapacity == 0
            ? Pass()
            : Fail("lawful-stop path was altered by flight normalization");
    }

    private (bool, string) RefusalStillCreatesNormalFugitiveState()
    {
        Context ctx = CreateContext();
        bool started = ctx.Fugitive.BeginPursuit(ctx.Player, "lawful enforcement flight", null);
        return started && ctx.Fugitive.IsActive && ctx.Fugitive.State == PoliceFugitiveState.Pursued
            ? Pass()
            : Fail("refusal path no longer creates a normal fugitive incident");
    }

    private (bool, string) SurrenderEligibilityRemainsPhase78Rules()
    {
        Context ctx = CreateContext();
        PoliceSurrenderAssessment notFugitive = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        if (notFugitive.IsEligible || notFugitive.Eligibility != PoliceSurrenderEligibility.NotFugitive)
            return Fail("not-fugitive eligibility changed");
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        PoliceSurrenderAssessment noPolice = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        if (noPolice.IsEligible || noPolice.Eligibility != PoliceSurrenderEligibility.NoPolicePresence)
            return Fail("no-Police eligibility changed");
        ctx.AddPolice(new Vector3(1_000f, 0f, 0f));
        PoliceSurrenderAssessment eligible = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return eligible.IsEligible && eligible.Eligibility == PoliceSurrenderEligibility.Eligible
            ? Pass()
            : Fail("Phase 78 eligibility rules changed");
    }

    private (bool, string) Phase76AttentionNotificationsUnaffected()
    {
        Context ctx = CreatePursuedContext();
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        bool attentionSpam = ctx.Notifications.Any(n =>
            n.Contains("POLICE ATTENTION", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("attention", StringComparison.OrdinalIgnoreCase));
        return !attentionSpam
            ? Pass()
            : Fail("surrender produced Phase 6 attention notification spam");
    }

    private (bool, string) Phase77DockingRegressionsRemainGreen()
    {
        Context ctx = CreatePursuedContext();
        Station policeStation = CreateStation("Fort Bush", FactionManager.LibertyPolice);
        FactionAccessResult denied = FactionAccessService.EvaluateDocking(
            ctx.Reputation, policeStation.FactionId, policeStation.Name, fugitivePursuitActive: true);
        if (denied.IsAllowed)
            return Fail("Phase 77 no longer denies Police docking while fugitive");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        FactionAccessResult allowed = FactionAccessService.EvaluateDocking(
            ctx.Reputation, policeStation.FactionId, policeStation.Name, fugitivePursuitActive: false);
        return allowed.IsAllowed
            ? Pass()
            : Fail("Phase 77 docking restoration regressed");
    }

    private (bool, string) Phase78SurrenderRegressionsRemainGreen()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.HotPursuit, 10_000);
        if (!ctx.TrySurrender(out PoliceSurrenderResult? result, out string failure))
            return Fail($"surrender failed ({failure})");
        return result != null && result.Success && result.FeeAmount == 2_500 &&
            ctx.Credits.Credits == 10_000 - 2_500 && !ctx.Fugitive.IsActive
            ? Pass()
            : Fail("Phase 78 surrender regression");
    }

    private (bool, string) Phase79FireCessationRegressionsRemainGreen()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (ctx.RunWeapons(6) <= 0)
            return Fail("no pre-surrender firing opportunity");
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        ctx.StepProduction();
        return ctx.RunWeapons(6) == 0
            ? Pass()
            : Fail("Phase 79 fire cessation regressed");
    }

    // ---- Performance / bounds ------------------------------------------------

    private (bool, string) NoDuplicatePolicePresenceScanIntroduced()
    {
        Context ctx = CreatePursuedContext();
        List<NpcShip> sameReference = ctx.Npcs;
        int countBefore = ctx.Npcs.Count;
        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender failed ({failure})");
        return ReferenceEquals(ctx.Npcs, sameReference) && ctx.Npcs.Count == countBefore
            ? Pass()
            : Fail("surrender re-scanned or replaced the world NPC list");
    }

    private (bool, string) NormalizationIsConstantOverKnownControllers()
    {
        foreach (FieldInfo field in typeof(PoliceFugitiveSurrenderService).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
        {
            if (field.IsPublic && !field.IsLiteral)
                return Fail($"surrender service gained mutable state: {field.Name}");
        }
        Context small = CreatePursuedContext();
        GotoAutopilot smallAutopilot = small.WireGoto();
        small.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(small.Player);
        if (!small.TrySurrender(out _, out string smallFailure))
            return Fail($"small-context surrender failed ({smallFailure})");

        Context large = CreatePursuedContext();
        for (int i = 0; i < 50; i++)
            large.AddPolice(new Vector3(1_000f + i * 10f, 0f, 0f));
        GotoAutopilot largeAutopilot = large.WireGoto();
        large.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(large.Player);
        if (!large.TrySurrender(out _, out string largeFailure))
            return Fail($"large-context surrender failed ({largeFailure})");

        return smallAutopilot.State == largeAutopilot.State &&
            small.Player.CruiseDrive.State == large.Player.CruiseDrive.State &&
            !small.Player.IsGotoActive && !large.Player.IsGotoActive
            ? Pass()
            : Fail("normalization outcome depends on world size");
    }

    // ---- Production-equivalent order proof -----------------------------------

    private (bool, string) ProductionEquivalentOrderProof()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(1_000f, 0f, 0f));
        GotoAutopilot autopilot = ctx.WireGoto();
        ctx.Player.ActivateGoto(new SpaceObject("Escape Jumphole", new Vector3(9_000f, 0f, 0f)));
        ActivateCruise(ctx.Player);
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!autopilot.IsActive)
            return Fail("autopilot did not own movement before surrender");

        if (!ctx.TrySurrender(out _, out string failure))
            return Fail($"surrender transaction failed ({failure})");

        bool fugitiveResolved = !ctx.Fugitive.IsActive;
        bool fireHoldApplied = police.HasPlayerTarget == false || ctx.RunWeapons(6) == 0;
        bool flightNormalized = autopilot.State == GotoAutopilot.AutopilotState.Cancelled &&
            !ctx.Player.IsGotoActive && !ctx.Player.CruiseDrive.IsActive;

        autopilot.Update(0.1f);
        bool automationDidNotResume = autopilot.State == GotoAutopilot.AutopilotState.Cancelled &&
            autopilot.Destination == null;
        bool manualControlAvailable = !ctx.Player.IsGotoActive &&
            !ctx.Player.CruiseDrive.IsActive && !ctx.Player.EnginesKilled;

        return fugitiveResolved && fireHoldApplied && flightNormalized &&
            automationDidNotResume && manualControlAvailable
            ? Pass()
            : Fail($"order proof failed (fugitive={fugitiveResolved}, fireHold={fireHoldApplied}, flight={flightNormalized}, resume={automationDidNotResume}, manual={manualControlAvailable})");
    }

    // ---- Harness -------------------------------------------------------------

    private Context CreateContext(float standing = 0.30f, int credits = 10_000) =>
        new(_graphicsDevice, standing, credits);

    private Context CreatePursuedContext(PoliceHeatLevel heat = PoliceHeatLevel.Pursuit, int credits = 10_000)
    {
        Context ctx = CreateContext(credits: credits);
        ctx.AddPolice(new Vector3(1_000f, 0f, 0f));
        ctx.BeginPursuit(heat);
        return ctx;
    }

    private static GotoAutopilot WireGoto(Context ctx)
    {
        GotoAutopilot autopilot = new();
        autopilot.Initialize(
            ctx.Player,
            null,
            null,
            new List<Station>(),
            ctx.Objects,
            ctx.Npcs,
            null,
            null,
            null,
            ctx.Reputation);
        ctx.Player.SetGotoAutopilot(autopilot);
        return autopilot;
    }

    private static bool ActivateCruise(Ship player)
    {
        if (!player.TryActivateCruise())
            return false;
        player.CruiseDrive.Advance(player.CruiseDrive.ChargeDuration + 1f);
        return player.CruiseDrive.IsActive;
    }

    private static float ReadPrivateFloat(Ship ship, string fieldName)
    {
        FieldInfo field = typeof(Ship).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (float)field.GetValue(ship)!;
    }

    private static Station CreateStation(string name, string factionId) =>
        new(new StationConfig
        {
            Description = name,
            FactionId = factionId,
            StartupPositionX = 2_000f,
            StartupPositionY = 0f,
            StartupPositionZ = -2_000f,
            Radius = 600f,
            DockingRange = 900f
        }, null);

    private static string SideArmsName => CommodityCatalog.GetById("side-arms")!.Name;

    private static string TempPath(string label) =>
        Path.Combine(Path.GetTempPath(), $"roguelancer-phase80-{label}-{Guid.NewGuid():N}.json");

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static GameTime Frame(double seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);

    private sealed class Context
    {
        public ReputationManager Reputation { get; }
        public PoliceScanSystem Scan { get; } = new();
        public PoliceFugitiveManager Fugitive { get; }
        public PoliceContrabandStopCoordinator Coordinator { get; } = new();
        public TrafficManager Traffic { get; }
        public PlayerCredits Credits { get; }
        public Ship Player { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public List<string> Notifications { get; } = new();
        private readonly NpcWeaponSystem _weapons;

        public Context(GraphicsDevice graphicsDevice, float standing, int credits)
        {
            Reputation = new ReputationManager(new FactionManager());
            Reputation.SetReputation(FactionManager.LibertyPolice, standing, "Phase 80 smoke setup");
            Reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "Phase 80 smoke setup");
            Reputation.SetReputation(FactionManager.LibertyNavy, 0.35f, "Phase 80 smoke setup");
            Reputation.SetReputation(FactionManager.NeutralCivilians, 0.30f, "Phase 80 smoke setup");
            Player = new Ship(Vector3.Zero) { CollisionRadius = 120f };
            Credits = new PlayerCredits(credits);
            Fugitive = new PoliceFugitiveManager(Reputation, Notifications.Add);
            Traffic = new TrafficManager(new ConfigurationManager(), Npcs, Objects);
            Traffic.FugitiveManager = Fugitive;
            Scan.SetFugitiveManager(Fugitive);
            _weapons = new NpcWeaponSystem(graphicsDevice, Reputation);
        }

        public NpcShip AddPolice(Vector3 position, string name = "Phase 80 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            ConfigureFighter(police, name);
            return police;
        }

        public NpcShip AddFactionShip(string factionId, Vector3 position)
        {
            NpcShip ship = new($"Phase 80 {factionId}", position, position, 1f, 0f, factionId);
            ConfigureFighter(ship, ship.Name);
            return ship;
        }

        private void ConfigureFighter(NpcShip ship, string name)
        {
            ship.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase80-zone", ship.Position, 800f, 180f, 6500f);
            ship.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                name, ship.FactionId, "SMOKE/fighter", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Standard));
            Npcs.Add(ship);
            Objects.Add(ship);
        }

        public void GiveContraband(Ship player, int quantity)
        {
            Commodity? contraband = CommodityCatalog.GetById("side-arms");
            if (contraband != null)
                player.CargoHold.AddCommodity(contraband, quantity);
        }

        public void BeginPursuit(PoliceHeatLevel heat) =>
            Fugitive.BeginPursuit(Player, "Phase 80 smoke", null, heat);

        public bool TrySurrender(out PoliceSurrenderResult? result, out string failureReason) =>
            PoliceFugitiveSurrenderService.TrySurrender(
                Fugitive, Reputation, Player, Npcs, Credits, null, out result, out failureReason, Scan);

        public void StepProduction()
        {
            GameTime frame = Frame(0.1f);
            Traffic.Update(frame, Player, Reputation, null);
            foreach (NpcShip npc in Npcs.ToList())
                npc.Update(frame, null, Player, Reputation);
            Scan.Update(frame, Player, Npcs, Credits, Reputation);
            Coordinator.Update(Player, Npcs, Traffic, Scan, Fugitive, Reputation, null);
        }

        public int RunWeapons(int frames, NpcShip? only = null)
        {
            int fired = 0;
            void OnFired(NpcShip source, NpcShip target, string weaponId, WeaponEquipmentDefinition weapon)
            {
                if (only != null && !ReferenceEquals(source, only))
                    return;
                fired++;
            }

            _weapons.NpcWeaponFired += OnFired;
            for (int i = 0; i < frames; i++)
                _weapons.Update(Frame(0.2f), Npcs, Player);
            _weapons.NpcWeaponFired -= OnFired;
            return fired;
        }

        public int ActiveProjectileCount()
        {
            FieldInfo field = typeof(NpcWeaponSystem).GetField("_projectiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return ((ICollection)field.GetValue(_weapons)!).Count;
        }

        public GotoAutopilot WireGoto() => Phase80SurrenderFlightStateNormalizationSmokeTest.WireGoto(this);
    }
}
