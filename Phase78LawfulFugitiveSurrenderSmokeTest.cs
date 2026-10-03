#nullable enable

using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 78 focused coverage for lawful Liberty Police fugitive surrender.
/// The suite drives only production systems: the single fugitive authority
/// (PoliceFugitiveManager), the Phase 78 surrender transaction
/// (PoliceFugitiveSurrenderService), the existing enforcement/scan path, the
/// shared docking access policy (FactionAccessService), the Phase 75
/// presentation, the Phase 76 transition observer, and SaveGameManager
/// persistence. No arrest, warrant, criminal-history, detention, Heat 3, or
/// persisted surrender state is created or asserted here.
/// </summary>
internal sealed class Phase78LawfulFugitiveSurrenderSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        Check("surrender unavailable when not fugitive", SurrenderUnavailableWhenNotFugitive);
        Check("Heat 1 active pursuit can enter surrender flow under lawful conditions", HeatOneCanSurrender);
        Check("Heat 2 active pursuit can enter surrender flow under lawful conditions", HeatTwoCanSurrender);
        Check("valid Police presence is required", ValidPolicePresenceRequired);
        Check("unrelated faction NPC cannot accept surrender", UnrelatedFactionCannotAccept);
        Check("Liberty Navy cannot accept Police surrender", LibertyNavyCannotAccept);
        Check("Liberty Rogue cannot accept Police surrender", LibertyRogueCannotAccept);
        Check("canonical faction ids are used, not names", CanonicalFactionIdsUsed);
        Check("successful Heat 1 surrender clears active fugitive state", HeatOneSurrenderClearsState);
        Check("successful Heat 2 surrender clears active fugitive state", HeatTwoSurrenderClearsState);
        Check("successful surrender does not increase Police reputation", SurrenderDoesNotIncreaseReputation);
        Check("successful surrender does not reset durable standing", SurrenderDoesNotResetStanding);
        Check("successful surrender produces one bounded notification", SurrenderProducesOneNotification);
        Check("successful surrender makes Phase 77 Police docking available again immediately", SurrenderRestoresPoliceDocking);
        Check("surrender does not directly mutate Phase 77 state", SurrenderDoesNotMutatePhase77State);
        Check("second surrender attempt cannot charge twice", SecondSurrenderCannotChargeTwice);
        Check("second surrender attempt cannot confiscate twice", SecondSurrenderCannotConfiscateTwice);
        Check("second surrender attempt cannot alter reputation twice", SecondSurrenderCannotAlterReputationTwice);
        Check("failed eligibility leaves fugitive state unchanged", FailedEligibilityLeavesFugitiveState);
        Check("failed eligibility leaves credits unchanged", FailedEligibilityLeavesCreditsUnchanged);
        Check("failed eligibility leaves cargo unchanged", FailedEligibilityLeavesCargoUnchanged);
        Check("insufficient-credit failure is atomic", InsufficientCreditFailureIsAtomic);
        Check("no negative credits", NoNegativeCredits);
        Check("no partial fee", NoPartialFee);
        Check("Heat 1 and Heat 2 consequences use only canonical current fugitive state", ConsequencesUseCanonicalState);
        Check("no Heat 3", NoHeatThree);
        Check("Pursued behavior is correct", PursuedBehaviorCorrect);
        Check("Evading behavior is correct", EvadingBehaviorCorrect);
        Check("fully escaped non-fugitive state cannot surrender", EscapedCannotSurrender);
        Check("post-escape reacquisition grace is not treated as surrender eligibility", PostEscapeGraceNotEligibility);
        Check("surrender does not create escape grace", SurrenderDoesNotCreateEscapeGrace);
        Check("severe lawful reputation does not prevent surrender", SevereLawfulReputationAllowsSurrender);
        Check("elevated lawful reputation follows fugitive authority", ElevatedReputationFollowsFugitiveAuthority);
        Check("hostile Police relationship is not silently reset by surrender", HostileStandingNotReset);
        Check("hostile plus fugitive follows documented bounded policy", HostilePlusFugitiveBoundedPolicy);
        Check("unrelated transient combat hostility is not improperly cleared", TransientHostilityPreserved);
        Check("contraband is not fabricated", ContrabandNotFabricated);
        Check("legal cargo is never confiscated", LegalCargoNeverConfiscated);
        Check("current authoritative illegal cargo is handled correctly", AuthoritativeCargoHandledCorrectly);
        Check("ordinary contraband fugitive path can surrender", OrdinaryContrabandPathCanSurrender);
        Check("mission-bound smuggling fugitive path can surrender", SmugglingPathCanSurrender);
        Check("mission cargo consequences use existing mission cargo authority", MissionCargoUsesExistingAuthority);
        Check("death and reset makes surrender unavailable", DeathResetMakesSurrenderUnavailable);
        Check("system transition follows existing fugitive lifecycle", SystemTransitionFollowsLifecycle);
        Check("save and load does not persist active surrender state", SaveLoadDoesNotPersistSurrender);
        Check("save and load does not replay a surrender consequence", SaveLoadDoesNotReplayConsequence);
        Check("schema remains unchanged", SchemaUnchanged);
        Check("no Phase 78 persisted field exists", NoPhase78PersistedField);
        Check("Police attention presentation remains read-only", AttentionPresentationReadOnly);
        Check("Phase 76 transition notifier is not misused as surrender authority", Phase76NotifierNotMisused);
        Check("Phase 77 docking policy continues to depend only on IsActive", Phase77DependsOnlyOnIsActive);
        Check("repeated eligibility reads have no side effects", RepeatedEligibilityReadsNoSideEffects);
        Check("no notification spam from passive checks", NoNotificationSpamFromPassiveChecks);
        Check("successful surrender is atomic across credits cargo and fugitive state", SurrenderIsAtomic);
        Check("no new criminal-history authority exists", NoCriminalHistoryAuthority);

        Console.WriteLine($"[PHASE 78 SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private int _passed;
    private int _failed;

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[PHASE 78 SMOKE] PASS {label}");
            }
            else
            {
                Fail(label, "assertion returned false");
            }
        }
        catch (Exception ex)
        {
            Fail(label, ex.Message);
        }
    }

    private void Fail(string label, string reason)
    {
        _failed++;
        Console.WriteLine($"[PHASE 78 SMOKE] FAIL {label}: {reason}");
    }

    private static bool SurrenderUnavailableWhenNotFugitive()
    {
        Context ctx = CreateContext();
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.NotFugitive;
    }

    private static bool HeatOneCanSurrender()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return assessment.IsEligible &&
            assessment.FeeAmount == PoliceFugitiveSurrenderService.Heat1SurrenderFee &&
            assessment.AcceptingOfficer != null;
    }

    private static bool HeatTwoCanSurrender()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.HotPursuit);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return assessment.IsEligible &&
            assessment.FeeAmount == PoliceFugitiveSurrenderService.Heat2SurrenderFee &&
            assessment.AcceptingOfficer != null;
    }

    private static bool ValidPolicePresenceRequired()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.NoPolicePresence;
    }

    private static bool UnrelatedFactionCannotAccept()
    {
        Context ctx = CreateContext();
        ctx.Npcs.Add(new NpcShip("Bounty Hunter", new Vector3(1_000f, 0f, 0f), Vector3.Zero, 1f, 0f, FactionManager.BountyHunters));
        ctx.Fugitive.BeginPursuit(ctx.Player);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.NoPolicePresence;
    }

    private static bool LibertyNavyCannotAccept()
    {
        Context ctx = CreateContext();
        ctx.Npcs.Add(new NpcShip("Liberty Navy", new Vector3(1_000f, 0f, 0f), Vector3.Zero, 1f, 0f, FactionManager.LibertyNavy));
        ctx.Fugitive.BeginPursuit(ctx.Player);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.NoPolicePresence;
    }

    private static bool LibertyRogueCannotAccept()
    {
        Context ctx = CreateContext();
        ctx.Npcs.Add(new NpcShip("Liberty Rogue", new Vector3(1_000f, 0f, 0f), Vector3.Zero, 1f, 0f, FactionManager.LibertyRogues));
        ctx.Fugitive.BeginPursuit(ctx.Player);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.NoPolicePresence;
    }

    private static bool CanonicalFactionIdsUsed()
    {
        Context ctx = CreateContext();
        ctx.Npcs.Add(new NpcShip("Liberty Police", new Vector3(1_000f, 0f, 0f), Vector3.Zero, 1f, 0f, FactionManager.LibertyNavy));
        ctx.Npcs.Add(new NpcShip("Patrol", new Vector3(2_000f, 0f, 0f), Vector3.Zero, 1f, 0f, FactionManager.LibertyPolice));
        ctx.Fugitive.BeginPursuit(ctx.Player);
        NpcShip? accepted = ctx.Fugitive.FindSurrenderPresence(ctx.Npcs, ctx.Player);
        return accepted != null &&
            string.Equals(accepted.FactionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accepted.FactionId, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HeatOneSurrenderClearsState()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        bool success = TrySurrender(ctx, out _, out _);
        return success && !ctx.Fugitive.IsActive && ctx.Fugitive.Heat == PoliceHeatLevel.None &&
            ctx.Fugitive.State == PoliceFugitiveState.None;
    }

    private static bool HeatTwoSurrenderClearsState()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.HotPursuit);
        bool success = TrySurrender(ctx, out _, out _);
        return success && !ctx.Fugitive.IsActive && ctx.Fugitive.Heat == PoliceHeatLevel.None &&
            ctx.Fugitive.State == PoliceFugitiveState.None;
    }

    private static bool SurrenderDoesNotIncreaseReputation()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        return TrySurrender(ctx, out _, out _) &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), before);
    }

    private static bool SurrenderDoesNotResetStanding()
    {
        Context ctx = CreateContext(standing: -0.45f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        return TrySurrender(ctx, out _, out _) &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), before) &&
            ctx.Reputation.GetStanding(FactionManager.LibertyPolice) < 0f;
    }

    private static bool SurrenderProducesOneNotification()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        ctx.Notifications.Clear();
        bool success = TrySurrender(ctx, out PoliceSurrenderResult? result, out _);
        return success && ctx.Notifications.Count == 1 &&
            ctx.Notifications[0].Contains("Surrender accepted", StringComparison.Ordinal) &&
            result != null && result.Message == ctx.Notifications[0];
    }

    private static bool SurrenderRestoresPoliceDocking()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        if (FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed)
            return false;
        return TrySurrender(ctx, out _, out _) &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
    }

    private static bool SurrenderDoesNotMutatePhase77State()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        return TrySurrender(ctx, out _, out _) &&
            !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
    }

    private static bool SecondSurrenderCannotChargeTwice()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        if (!TrySurrender(ctx, out _, out _))
            return false;
        int creditsAfterFirst = ctx.Credits.Credits;
        bool second = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return !second && ctx.Credits.Credits == creditsAfterFirst;
    }

    private static bool SecondSurrenderCannotConfiscateTwice()
    {
        Context ctx = CreateContrabandPursuedContext();
        int cargoBefore = ctx.Cargo.GetCommodityQuantity(SideArmsName);
        if (!TrySurrender(ctx, out _, out _))
            return false;
        bool second = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return !second && ctx.Cargo.GetCommodityQuantity(SideArmsName) == cargoBefore;
    }

    private static bool SecondSurrenderCannotAlterReputationTwice()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        if (!TrySurrender(ctx, out _, out _))
            return false;
        float standingAfterFirst = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        bool second = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return !second &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), standingAfterFirst);
    }

    private static bool FailedEligibilityLeavesFugitiveState()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        bool success = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return !success && ctx.Fugitive.IsActive && ctx.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static bool FailedEligibilityLeavesCreditsUnchanged()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        int before = ctx.Credits.Credits;
        bool success = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return !success && ctx.Credits.Credits == before;
    }

    private static bool FailedEligibilityLeavesCargoUnchanged()
    {
        Context ctx = CreateContrabandPursuedContext();
        int before = ctx.Cargo.GetCommodityQuantity(SideArmsName);
        bool success = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, new List<NpcShip>(), ctx.Credits, null, out _, out _);
        return !success && ctx.Cargo.GetCommodityQuantity(SideArmsName) == before;
    }

    private static bool InsufficientCreditFailureIsAtomic()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit, credits: 100);
        int before = ctx.Credits.Credits;
        bool success = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out string failure);
        return !success && ctx.Credits.Credits == before && ctx.Fugitive.IsActive &&
            failure.Contains("insufficient credits", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NoNegativeCredits()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit, credits: 100);
        PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return ctx.Credits.Credits >= 0;
    }

    private static bool NoPartialFee()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit, credits: 500);
        bool success = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return !success && ctx.Credits.Credits == 500 && ctx.Fugitive.IsActive;
    }

    private static bool ConsequencesUseCanonicalState()
    {
        return PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.Pursuit) == PoliceFugitiveSurrenderService.Heat1SurrenderFee &&
            PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.HotPursuit) == PoliceFugitiveSurrenderService.Heat2SurrenderFee &&
            PoliceFugitiveSurrenderService.Heat1SurrenderFee < PoliceFugitiveSurrenderService.Heat2SurrenderFee;
    }

    private static bool NoHeatThree()
    {
        foreach (PoliceHeatLevel level in Enum.GetValues(typeof(PoliceHeatLevel)))
        {
            if ((int)level >= 3)
                return false;
        }

        Context ctx = CreatePursuedContext(PoliceHeatLevel.HotPursuit);
        NpcShip police = ctx.Npcs[0];
        police.MarkDamagedByPlayer(5f);
        ctx.Fugitive.NotifyPlayerDamage(police, ctx.Player);
        return ctx.Fugitive.Heat == PoliceHeatLevel.HotPursuit;
    }

    private static bool PursuedBehaviorCorrect()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        return ctx.Fugitive.IsPursued && ctx.Fugitive.IsActive && TrySurrender(ctx, out _, out _);
    }

    private static bool EvadingBehaviorCorrect()
    {
        Context ctx = CreateEvadingContext();
        if (!ctx.Fugitive.IsEvading)
            return false;
        PoliceSurrenderAssessment absent = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        if (absent.IsEligible)
            return false;

        ctx.Npcs[0].Position = new Vector3(1_000f, 0f, 0f);
        PoliceSurrenderAssessment present = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return present.IsEligible && TrySurrender(ctx, out _, out _);
    }

    private static bool EscapedCannotSurrender()
    {
        Context ctx = CreateEvadingContext();
        ctx.Fugitive.ResolveEscape();
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !ctx.Fugitive.IsActive && !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.NotFugitive;
    }

    private static bool PostEscapeGraceNotEligibility()
    {
        Context ctx = CreateEvadingContext();
        ctx.Fugitive.ResolveEscape();
        return !ctx.Fugitive.IsActive &&
            ctx.Fugitive.IsContrabandReacquisitionGraceActive &&
            !PoliceFugitiveSurrenderService.Assess(
                ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits).IsEligible;
    }

    private static bool SurrenderDoesNotCreateEscapeGrace()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        bool success = TrySurrender(ctx, out _, out _);
        return success && !ctx.Fugitive.IsActive &&
            !ctx.Fugitive.IsContrabandReacquisitionGraceActive;
    }

    private static bool SevereLawfulReputationAllowsSurrender()
    {
        Context ctx = CreateContext(standing: -0.55f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return assessment.IsEligible && TrySurrender(ctx, out _, out _);
    }

    private static bool ElevatedReputationFollowsFugitiveAuthority()
    {
        Context ctx = CreateContext(standing: -0.45f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        return TrySurrender(ctx, out _, out _) &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), -0.45f);
    }

    private static bool HostileStandingNotReset()
    {
        Context ctx = CreateContext(standing: -0.70f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        bool success = PoliceFugitiveSurrenderService.TrySurrender(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits, null, out _, out _);
        return !success && ctx.Reputation.IsHostile(FactionManager.LibertyPolice) &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), -0.70f) &&
            ctx.Fugitive.IsActive;
    }

    private static bool HostilePlusFugitiveBoundedPolicy()
    {
        Context ctx = CreateContext(standing: -0.70f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.PermanentlyHostile &&
            ctx.Fugitive.IsActive;
    }

    private static bool TransientHostilityPreserved()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        ctx.Reputation.TemporaryHostility.RecordHostileAction(
            FactionManager.LibertyPolice,
            TemporaryHostilityManager.PlayerAggressionReason,
            60f,
            causedByPlayerAggression: true);
        bool success = TrySurrender(ctx, out _, out _);
        return success && !ctx.Fugitive.IsActive &&
            ctx.Reputation.TemporaryHostility.HasReason(
                FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason);
    }

    private static bool ContrabandNotFabricated()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        bool success = TrySurrender(ctx, out _, out _);
        return success && ctx.Cargo.GetAllCommodities().Count == 0;
    }

    private static bool LegalCargoNeverConfiscated()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        Commodity legal = CommodityCatalog.GetById("food-rations")!;
        if (!ctx.Cargo.AddCommodity(legal, 3))
            return false;
        bool success = TrySurrender(ctx, out _, out _);
        return success && ctx.Cargo.GetCommodityQuantity("Food Rations") == 3;
    }

    private static bool AuthoritativeCargoHandledCorrectly()
    {
        Context ctx = CreateContrabandPursuedContext();
        int before = ctx.Cargo.GetCommodityQuantity(SideArmsName);
        bool success = TrySurrender(ctx, out _, out _);
        return success && ctx.Cargo.GetCommodityQuantity(SideArmsName) == before && before > 0;
    }

    private static bool OrdinaryContrabandPathCanSurrender()
    {
        Context ctx = CreateContrabandPursuedContext();
        return TrySurrender(ctx, out _, out _) && !ctx.Fugitive.IsActive;
    }

    private static bool SmugglingPathCanSurrender()
    {
        Context ctx = CreateContext();
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        if (!ctx.Cargo.AddMissionCargo(9001, contraband, 2))
            return false;
        ctx.Fugitive.BeginPursuit(ctx.Player, "smuggling contract refusal");
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        return TrySurrender(ctx, out _, out _) && !ctx.Fugitive.IsActive;
    }

    private static bool MissionCargoUsesExistingAuthority()
    {
        Context ctx = CreateContext();
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        if (!ctx.Cargo.AddMissionCargo(9001, contraband, 2))
            return false;
        ctx.Fugitive.BeginPursuit(ctx.Player, "smuggling contract refusal");
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        int before = ctx.Cargo.GetMissionCargoQuantity(9001);
        bool success = TrySurrender(ctx, out _, out _);
        return success && ctx.Cargo.GetMissionCargoQuantity(9001) == before;
    }

    private static bool DeathResetMakesSurrenderUnavailable()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        ctx.Player.Hull.TakeDamage(100_000f);
        ctx.Fugitive.Update(0.5f, ctx.Player, Array.Empty<NpcShip>());
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !ctx.Fugitive.IsActive && !assessment.IsEligible;
    }

    private static bool SystemTransitionFollowsLifecycle()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        ctx.Fugitive.Reset(reason: "system transition");
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !ctx.Fugitive.IsActive && !assessment.IsEligible;
    }

    private static bool SaveLoadDoesNotPersistSurrender()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        TrySurrender(ctx, out _, out _);
        SaveGameManager save = new(TempPath("phase78"));
        SaveGameData data = new()
        {
            FactionReputation = save.CaptureReputation(ctx.Reputation),
            TemporaryHostility = save.CaptureTemporaryHostility(ctx.Reputation)
        };
        if (!save.TrySave(data, out _))
            return false;
        if (!save.TryLoad(out SaveGameData loaded, out _))
            return false;

        ReputationManager restored = new(new FactionManager());
        save.ApplyReputation(restored, loaded);
        save.ApplyTemporaryHostility(restored, loaded);
        return !restored.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            FactionAccessService.EvaluateDocking(
                restored, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool SaveLoadDoesNotReplayConsequence()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        int creditsBefore = ctx.Credits.Credits;
        TrySurrender(ctx, out _, out _);
        int creditsAfter = ctx.Credits.Credits;
        SaveGameManager save = new(TempPath("phase78-replay"));
        SaveGameData data = new() { FactionReputation = save.CaptureReputation(ctx.Reputation) };
        if (!save.TrySave(data, out _) || !save.TryLoad(out SaveGameData loaded, out _))
            return false;
        ReputationManager restored = new(new FactionManager());
        save.ApplyReputation(restored, loaded);
        return creditsAfter < creditsBefore &&
            Nearly(restored.GetStanding(FactionManager.LibertyPolice), ctx.Reputation.GetStanding(FactionManager.LibertyPolice));
    }

    private static bool SchemaUnchanged()
    {
        return new SaveGameData().SchemaVersion == SaveGameData.CurrentSchemaVersion &&
            SaveGameData.CurrentSchemaVersion == 13;
    }

    private static bool NoPhase78PersistedField()
    {
        foreach (PropertyInfo property in typeof(SaveGameData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = property.Name.ToLowerInvariant();
            if (name.Contains("surrender"))
                return false;
        }

        return true;
    }

    private static bool AttentionPresentationReadOnly()
    {
        Context ctx = CreateContext(standing: -0.45f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        float standingBefore = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        PoliceHeatLevel heatBefore = ctx.Fugitive.Heat;
        for (int i = 0; i < 100; i++)
        {
            _ = PoliceAttentionPresentation.GetLevel(ctx.Reputation);
            _ = PoliceAttentionPresentation.GetReadout(ctx.Reputation);
            _ = PoliceAttentionPresentation.GetOverviewLine(ctx.Reputation);
        }

        return Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), standingBefore) &&
            ctx.Fugitive.IsActive && ctx.Fugitive.Heat == heatBefore;
    }

    private static bool Phase76NotifierNotMisused()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        List<string> notifications = new();
        PoliceAttentionTransitionNotifier notifier = new(ctx.Reputation, notifications.Add);
        bool success = TrySurrender(ctx, out _, out _);
        return success && notifications.Count == 0 &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), 0.30f);
    }

    private static bool Phase77DependsOnlyOnIsActive()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        bool deniedBefore = !FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        if (!TrySurrender(ctx, out _, out _))
            return false;
        return deniedBefore && !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
    }

    private static bool RepeatedEligibilityReadsNoSideEffects()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        int creditsBefore = ctx.Credits.Credits;
        float standingBefore = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        for (int i = 0; i < 1000; i++)
        {
            _ = PoliceFugitiveSurrenderService.Assess(
                ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        }

        return ctx.Credits.Credits == creditsBefore &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), standingBefore) &&
            ctx.Fugitive.IsActive && ctx.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static bool NoNotificationSpamFromPassiveChecks()
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit);
        ctx.Notifications.Clear();
        for (int i = 0; i < 1000; i++)
        {
            _ = PoliceFugitiveSurrenderService.Assess(
                ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        }

        return ctx.Notifications.Count == 0;
    }

    private static bool SurrenderIsAtomic()
    {
        Context ctx = CreateContrabandPursuedContext();
        int creditsBefore = ctx.Credits.Credits;
        int cargoBefore = ctx.Cargo.GetCommodityQuantity(SideArmsName);
        ctx.Notifications.Clear();
        bool success = TrySurrender(ctx, out PoliceSurrenderResult? result, out _);
        int expectedFee = PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.Pursuit);
        return success && result != null &&
            ctx.Credits.Credits == creditsBefore - expectedFee &&
            ctx.Cargo.GetCommodityQuantity(SideArmsName) == cargoBefore &&
            !ctx.Fugitive.IsActive && ctx.Notifications.Count == 1;
    }

    private static bool NoCriminalHistoryAuthority()
    {
        foreach (PropertyInfo property in typeof(PoliceFugitiveSurrenderService).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            string name = property.Name.ToLowerInvariant();
            if (name.Contains("criminal") || name.Contains("warrant") || name.Contains("arrest") ||
                name.Contains("detention") || name.Contains("history"))
                return false;
        }

        foreach (FieldInfo field in typeof(PoliceFugitiveSurrenderService).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            string name = field.Name.ToLowerInvariant();
            if (name.Contains("criminal") || name.Contains("warrant") || name.Contains("arrest") ||
                name.Contains("detention") || name.Contains("history"))
                return false;
        }

        return true;
    }

    private static bool TrySurrender(Context context, out PoliceSurrenderResult? result, out string failureReason) =>
        PoliceFugitiveSurrenderService.TrySurrender(
            context.Fugitive, context.Reputation, context.Player, context.Npcs, context.Credits,
            null, out result, out failureReason);

    private static Context CreatePursuedContext(PoliceHeatLevel heat, int credits = 10_000)
    {
        Context ctx = CreateContext(credits: credits);
        ctx.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        ctx.Fugitive.BeginPursuit(ctx.Player);
        if (heat == PoliceHeatLevel.HotPursuit)
        {
            ctx.Npcs[0].MarkDamagedByPlayer(1f);
            ctx.Fugitive.NotifyPlayerDamage(ctx.Npcs[0], ctx.Player);
        }

        return ctx;
    }

    private static Context CreateEvadingContext()
    {
        Context ctx = CreateContext();
        NpcShip police = CreatePolice(new Vector3(1_000f, 0f, 0f));
        ctx.Npcs.Add(police);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        police.Position = new Vector3(20_000f, 0f, 0f);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        return ctx;
    }

    private static Context CreateContrabandPursuedContext(int credits = 10_000)
    {
        Context ctx = CreatePursuedContext(PoliceHeatLevel.Pursuit, credits);
        ctx.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        return ctx;
    }

    private static Context CreateContext(float standing = 0.30f, int credits = 10_000)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "Phase 78 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "Phase 78 smoke setup");
        reputation.SetReputation(FactionManager.LibertyCorporations, 0.30f, "Phase 78 smoke setup");
        reputation.SetReputation(FactionManager.NeutralCivilians, 0.30f, "Phase 78 smoke setup");
        Ship player = new(Vector3.Zero);
        List<string> notifications = new();
        PoliceFugitiveManager fugitive = new(reputation, notifications.Add);
        return new Context(
            reputation,
            player,
            new CargoHold(100),
            new PlayerCredits(credits),
            new List<NpcShip>(),
            fugitive,
            notifications);
    }

    private static NpcShip CreatePolice(Vector3 position) =>
        new("Phase 78 Police", position, Vector3.Zero, 1f, 0f, FactionManager.LibertyPolice);

    private static string SideArmsName => CommodityCatalog.GetById("side-arms")!.Name;

    private static string TempPath(string label) =>
        Path.Combine(Path.GetTempPath(), $"roguelancer-phase78-{label}-{Guid.NewGuid():N}.json");

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static bool RunSilenced(Func<bool> assertion)
    {
        TextWriter original = Console.Out;
        try
        {
            using StringWriter writer = new();
            Console.SetOut(writer);
            return assertion();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private sealed class Context
    {
        public Context(
            ReputationManager reputation,
            Ship player,
            CargoHold cargo,
            PlayerCredits credits,
            List<NpcShip> npcs,
            PoliceFugitiveManager fugitive,
            List<string> notifications)
        {
            Reputation = reputation;
            Player = player;
            Cargo = cargo;
            Credits = credits;
            Npcs = npcs;
            Fugitive = fugitive;
            Notifications = notifications;
        }

        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public CargoHold Cargo { get; }
        public PlayerCredits Credits { get; }
        public List<NpcShip> Npcs { get; }
        public PoliceFugitiveManager Fugitive { get; }
        public List<string> Notifications { get; }
    }
}
