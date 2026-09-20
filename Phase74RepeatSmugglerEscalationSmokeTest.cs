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
/// Phase 74 focused coverage for repeat-smuggler enforcement escalation.
/// The harness drives only production systems: the standing authority
/// (ReputationManager), the tier policy
/// (PoliceEnforcementEscalationPolicy), the authoritative violation/fine
/// path (PoliceEnforcementService), the scan/demand lifecycle
/// (PoliceScanSystem), the single pursuit owner
/// (PoliceFugitiveManager), the transient stop coordinator,
/// TrafficManager detection, canonical CargoHold/CommodityCatalog state,
/// and SaveGameManager persistence. No smoke-only enforcement, crime,
/// cargo, or pursuit implementation exists here.
/// </summary>
internal sealed class Phase74RepeatSmugglerEscalationSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(ValidateStandardStandingProducesTierZero, "standard standing produces Tier 0", ref passed, ref failed);
        RunCase(ValidateModerateNegativeStandingProducesElevatedTier, "moderate negative Police standing produces the elevated tier", ref passed, ref failed);
        RunCase(ValidateSevereNonHostileStandingProducesHighestTier, "severe negative non-hostile standing produces the highest lawful tier", ref passed, ref failed);
        RunCase(ValidateHostileRelationshipUsesNoPeacefulStop, "genuinely hostile Police relationship does not use peaceful stop behavior", ref passed, ref failed);
        RunCase(ValidateTierUsesAuthoritativeCurrentStanding, "tier calculation uses authoritative current standing", ref passed, ref failed);
        RunCase(ValidateNoPersistedTierFieldExists, "no separate persisted tier field is required", ref passed, ref failed);
        RunCase(ValidateTierZeroFineRemainsCompatible, "Tier 0 fine remains Phase 73-compatible", ref passed, ref failed);
        RunCase(ValidateTierOneFineExceedsTierZero, "Tier 1 fine is greater than the equivalent Tier 0 fine", ref passed, ref failed);
        RunCase(ValidateTierTwoFineMeetsOrExceedsTierOne, "Tier 2 fine is greater than or equal to Tier 1", ref passed, ref failed);
        RunCase(ValidateFineRemainsWithinSafetyBounds, "fine remains within existing safety bounds", ref passed, ref failed);
        RunCase(ValidateDemandUsesSameCalculatedTier, "UI/demand uses the same calculated tier", ref passed, ref failed);
        RunCase(ValidateDemandCreatesNoDuplicateFineCalculation, "UI does not create a duplicate fine calculation", ref passed, ref failed);
        RunCase(ValidateElevatedOffenderMayStillComply, "elevated offender may still comply", ref passed, ref failed);
        RunCase(ValidateComplianceConfiscatesExactLiveCargo, "compliance confiscates exact live illegal cargo", ref passed, ref failed);
        RunCase(ValidateComplianceChargesEscalatedFineOnce, "compliance charges the exact escalated fine once", ref passed, ref failed);
        RunCase(ValidateLegalCargoRemainsUntouched, "legal cargo remains untouched", ref passed, ref failed);
        RunCase(ValidateInsufficientCreditBehaviorRemainsSafe, "insufficient-credit behavior remains safe", ref passed, ref failed);
        RunCase(ValidateTierZeroRefusalUsesExistingBehavior, "Tier 0 refusal uses existing fugitive behavior", ref passed, ref failed);
        RunCase(ValidateElevatedRefusalEscalatesThroughFugitiveManager, "elevated refusal escalates through the existing fugitive manager", ref passed, ref failed);
        RunCase(ValidateHighestTierUsesStrongestBoundedHeat, "highest lawful tier uses existing strongest bounded heat behavior", ref passed, ref failed);
        RunCase(ValidateNoSecondPursuitSystemExists, "no second pursuit system exists", ref passed, ref failed);
        RunCase(ValidateElevatedPursuitUsesNormalAcquisition, "elevated pursuit still uses normal Police acquisition", ref passed, ref failed);
        RunCase(ValidateHoldFireRemainsDuringLawfulStop, "hold-fire remains during lawful stop", ref passed, ref failed);
        RunCase(ValidateHoldFireCeasesOnFugitiveEscalation, "hold-fire ceases on fugitive escalation", ref passed, ref failed);
        RunCase(ValidateHostilePoliceMayFireRegardless, "hostile Police may fire regardless of lawful-stop flag", ref passed, ref failed);
        RunCase(ValidateGraceUsesAuthoritativeTierPolicy, "reacquisition grace uses authoritative tier policy", ref passed, ref failed);
        RunCase(ValidateTierZeroGraceRetainsExistingSemantics, "Tier 0 grace retains existing Phase 72 semantics", ref passed, ref failed);
        RunCase(ValidateElevatedGraceRemainsBounded, "elevated grace is bounded and avoids same-tick restop", ref passed, ref failed);
        RunCase(ValidateFutureDetectionRemainsPossible, "future detection remains possible after grace expires", ref passed, ref failed);
        RunCase(ValidateComplianceKeepsDurableStanding, "successful compliance does not silently erase durable standing", ref passed, ref failed);
        RunCase(ValidateImprovingStandingLowersTier, "improving Police standing lowers the derived tier", ref passed, ref failed);
        RunCase(ValidateMissionCargoUsesSameTierLogic, "smuggling mission cargo uses the same tier logic", ref passed, ref failed);
        RunCase(ValidateOrdinaryContrabandUsesSameTierLogic, "ordinary contraband uses the same tier logic", ref passed, ref failed);
        RunCase(ValidateLegalMissionCargoRemainsUnaffected, "legal mission cargo remains unaffected", ref passed, ref failed);
        RunCase(ValidateMultipleStacksDoNotMultiplyEscalation, "multiple illegal stacks do not multiply escalation incorrectly", ref passed, ref failed);
        RunCase(ValidateClassificationRemainsUnchanged, "stolen/contraband classification remains unchanged", ref passed, ref failed);
        RunCase(ValidateSaveLoadReproducesTier, "save/load preserves standing and reproduces tier", ref passed, ref failed);
        RunCase(ValidateSaveLoadDuplicatesNothing, "save/load does not duplicate fines or cargo", ref passed, ref failed);
        RunCase(ValidateSystemResetClearsTransientState, "system reset clears transient state but not durable standing", ref passed, ref failed);
        RunCase(ValidateDeathLeavesNoStaleEscalationState, "player death leaves no stale active-stop/fugitive escalation state", ref passed, ref failed);

        Console.WriteLine($"[PHASE 74 SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string failureReason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 74 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 74 SMOKE] FAIL {label}: {failureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 74 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidateStandardStandingProducesTierZero()
    {
        if (PoliceEnforcementEscalationPolicy.GetTier(0f) != PoliceEnforcementTier.Standard)
            return Fail("neutral standing did not produce Tier 0");
        Context context = CreateContext(standing: -0.25f);
        if (PoliceEnforcementEscalationPolicy.GetTier(context.Reputation) != PoliceEnforcementTier.Standard)
            return Fail("new-game standing (-0.25) did not produce Tier 0");
        PoliceEnforcementOffer offer = EvaluateTiered(context, quantity: 2);
        if (offer.EnforcementTier != PoliceEnforcementTier.Standard)
            return Fail("evaluation at standard standing did not carry Tier 0");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateModerateNegativeStandingProducesElevatedTier()
    {
        if (PoliceEnforcementEscalationPolicy.GetTier(-0.45f) != PoliceEnforcementTier.Elevated)
            return Fail("standing -0.45 did not produce Tier 1");
        Context context = CreateContext(standing: -0.45f);
        PoliceEnforcementOffer offer = EvaluateTiered(context, quantity: 2);
        if (offer.EnforcementTier != PoliceEnforcementTier.Elevated)
            return Fail("evaluation at -0.45 did not carry the elevated tier");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSevereNonHostileStandingProducesHighestTier()
    {
        Context context = CreateContext(standing: -0.55f);
        if (context.Reputation.IsFactionCurrentlyHostile(FactionManager.LibertyPolice))
            return Fail("standing -0.55 should still be lawful test setup");
        if (PoliceEnforcementEscalationPolicy.GetTier(context.Reputation) != PoliceEnforcementTier.Severe)
            return Fail("standing -0.55 did not produce Tier 2");
        PoliceEnforcementOffer offer = EvaluateTiered(context, quantity: 2);
        if (offer.EnforcementTier != PoliceEnforcementTier.Severe)
            return Fail("evaluation at -0.55 did not carry the severe tier");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostileRelationshipUsesNoPeacefulStop()
    {
        Context context = CreateContext(standing: -0.70f);
        if (!context.Reputation.IsFactionCurrentlyHostile(FactionManager.LibertyPolice))
            return Fail("could not stage genuine Police hostility");
        GiveContraband(context.Player, 2);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (context.Scan.State != PoliceScanState.Idle || context.Scan.IsEnforcementDemandActive)
            return Fail("a hostile relationship opened a peaceful scan/demand");
        if (context.Scan.CurrentOffer != null || !string.IsNullOrWhiteSpace(context.Scan.StatusText))
            return Fail("a hostile relationship produced demand state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateTierUsesAuthoritativeCurrentStanding()
    {
        Context context = CreateContext(standing: 0f);
        if (EvaluateTiered(context, 1).EnforcementTier != PoliceEnforcementTier.Standard)
            return Fail("neutral baseline was not Tier 0");
        context.Reputation.SetReputation(FactionManager.LibertyPolice, -0.45f, "phase 74 escalation setup");
        if (EvaluateTiered(context, 1).EnforcementTier != PoliceEnforcementTier.Elevated)
            return Fail("tier did not follow the updated standing");
        context.Reputation.SetReputation(FactionManager.LibertyPolice, -0.55f, "phase 74 escalation setup");
        if (EvaluateTiered(context, 1).EnforcementTier != PoliceEnforcementTier.Severe)
            return Fail("tier did not follow the severe standing");
        PoliceEnforcementOffer nullAuthority = new PoliceEnforcementService().Evaluate(
            FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits, reputationManager: null);
        if (nullAuthority.EnforcementTier != PoliceEnforcementTier.Standard)
            return Fail("null reputation authority did not default to Tier 0");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNoPersistedTierFieldExists()
    {
        foreach (PropertyInfo property in typeof(SaveGameData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = property.Name.ToLowerInvariant();
            if (name.Contains("tier") || name.Contains("enforcement") || name.Contains("heat") || name.Contains("escalation"))
                return Fail($"SaveGameData carries a persisted escalation field '{property.Name}'");
        }

        Context context = CreateContext(standing: -0.45f);
        if (context.Reputation.GetStandingsSnapshot().Any(entry => entry.Key.Contains("tier", StringComparison.OrdinalIgnoreCase)))
            return Fail("standing snapshot carries a tier entry");
        int schemaVersion = new SaveGameData().SchemaVersion;
        if (schemaVersion != 13)
            return Fail($"save schema changed unexpectedly (version {schemaVersion})");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateTierZeroFineRemainsCompatible()
    {
        Context context = CreateContext(standing: 0.30f, credits: 20_000);
        GiveContraband(context.Player, 2);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer legacy = service.Evaluate(FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits);
        PoliceEnforcementOffer tiered = service.Evaluate(
            FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits, context.Reputation);
        if (legacy.FineAmount != 1_250 || tiered.FineAmount != 1_250)
            return Fail($"Tier 0 fine drifted from Phase 73 (legacy {legacy.FineAmount}, tiered {tiered.FineAmount})");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateTierOneFineExceedsTierZero()
    {
        Context standard = CreateContext(standing: 0f, credits: 20_000);
        Context elevated = CreateContext(standing: -0.45f, credits: 20_000);
        GiveContraband(standard.Player, 2);
        GiveContraband(elevated.Player, 2);
        int standardFine = EvaluateTiered(standard, 0).FineAmount;
        int elevatedFine = EvaluateTiered(elevated, 0).FineAmount;
        if (standardFine != 1_250)
            return Fail($"Tier 0 baseline was {standardFine}, expected 1250");
        if (elevatedFine <= standardFine)
            return Fail($"Tier 1 fine {elevatedFine} did not exceed Tier 0 fine {standardFine}");
        if (elevatedFine != PoliceEnforcementEscalationPolicy.ApplyFineEscalation(standardFine, PoliceEnforcementTier.Elevated))
            return Fail("Tier 1 fine did not come from the single central escalation path");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateTierTwoFineMeetsOrExceedsTierOne()
    {
        Context elevated = CreateContext(standing: -0.45f, credits: 20_000);
        Context severe = CreateContext(standing: -0.55f, credits: 20_000);
        GiveContraband(elevated.Player, 2);
        GiveContraband(severe.Player, 2);
        int tierOne = EvaluateTiered(elevated, 0).FineAmount;
        int tierTwo = EvaluateTiered(severe, 0).FineAmount;
        if (tierTwo < tierOne)
            return Fail($"Tier 2 fine {tierTwo} fell below Tier 1 fine {tierOne}");
        if (tierTwo != PoliceEnforcementEscalationPolicy.ApplyFineEscalation(1_250, PoliceEnforcementTier.Severe))
            return Fail("Tier 2 fine did not come from the single central escalation path");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateFineRemainsWithinSafetyBounds()
    {
        PoliceEnforcementService service = new();
        int escalatedMinimum = PoliceEnforcementEscalationPolicy.ApplyFineEscalation(
            PoliceEnforcementService.MinimumFineCredits, PoliceEnforcementTier.Severe);
        if (escalatedMinimum < PoliceEnforcementService.MinimumFineCredits ||
            escalatedMinimum > PoliceEnforcementService.MaximumFineCredits)
            return Fail("escalation pushed the minimum fine outside the safety clamp");
        if (PoliceEnforcementEscalationPolicy.ApplyFineEscalation(
                PoliceEnforcementService.MaximumFineCredits, PoliceEnforcementTier.Severe) != PoliceEnforcementService.MaximumFineCredits)
            return Fail("escalation escaped the maximum clamp");
        if (service.CalculateFine(long.MaxValue) != PoliceEnforcementService.MaximumFineCredits)
            return Fail("base calculation lost its maximum clamp");
        Context clean = CreateContext(standing: -0.55f);
        PoliceEnforcementOffer offer = EvaluateTiered(clean, 0);
        if (offer.HasViolation || offer.FineAmount != 0)
            return Fail("a clean hold produced a violation fine at Tier 2");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateDemandUsesSameCalculatedTier()
    {
        Context context = CreateContext(standing: -0.45f, credits: 20_000);
        GiveContraband(context.Player, 2);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (context.Scan.State != PoliceScanState.ContrabandDetected || context.Scan.CurrentOffer == null)
            return Fail("scan did not open the contraband demand");
        PoliceEnforcementOffer offer = context.Scan.CurrentOffer;
        if (offer.EnforcementTier != PoliceEnforcementTier.Elevated)
            return Fail("the live demand did not carry the elevated tier");
        int expectedFine = EvaluateTiered(context, 0).FineAmount;
        if (offer.FineAmount != expectedFine)
            return Fail($"demand fine {offer.FineAmount} differs from the authoritative tiered fine {expectedFine}");
        if (!context.Scan.StatusText.Contains("Repeat offense", StringComparison.OrdinalIgnoreCase))
            return Fail("the HUD status line did not name the elevated tier");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateDemandCreatesNoDuplicateFineCalculation()
    {
        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        GiveContraband(context.Player, 2);
        List<string> log = new();
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8, log.Add);
        PoliceEnforcementOffer offer = context.Scan.CurrentOffer;
        if (offer == null)
            return Fail("scan did not open the demand");
        string expectedAmount = $"{offer.FineAmount:N0}";
        if (!context.Scan.StatusText.Contains(expectedAmount, StringComparison.Ordinal))
            return Fail("the HUD status line did not show the exact quoted fine");
        int violationLines = log.Count(line => line.Contains("enforcement demand opened", StringComparison.OrdinalIgnoreCase));
        if (violationLines != 1)
            return Fail($"demand creation logged {violationLines} violation lines instead of exactly one");
        if (!log.Any(line => line.Contains(expectedAmount, StringComparison.Ordinal)))
            return Fail("the demand log did not carry the exact quoted fine");
        PoliceEnforcementOffer repeat = EvaluateTiered(context, 0);
        if (repeat.FineAmount != offer.FineAmount || repeat.EnforcementTier != offer.EnforcementTier)
            return Fail("re-evaluation diverged from the live demand quote");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedOffenderMayStillComply()
    {
        Context context = CreateContext(standing: -0.45f, credits: 20_000);
        GiveContraband(context.Player, 2);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (!context.Scan.TryAcceptEnforcement(context.Player, context.Credits, context.Reputation))
            return Fail("compliance was refused for an elevated offender");
        if (context.Scan.State != PoliceScanState.Cleared || context.Fugitive.IsActive)
            return Fail("compliance did not clear the stop peacefully");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateComplianceConfiscatesExactLiveCargo()
    {
        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        Commodity legal = CommodityCatalog.GetById("food-rations")!;
        context.Player.CargoHold.AddCommodity(contraband, 2);
        context.Player.CargoHold.AddCommodity(legal, 3);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (!context.Scan.TryAcceptEnforcement(context.Player, context.Credits, context.Reputation))
            return Fail("compliance did not resolve");
        if (context.Player.CargoHold.GetCommodityQuantity(contraband.Name) != 0)
            return Fail("illegal cargo survived compliance");
        if (context.Player.CargoHold.GetCommodityQuantity(legal.Name) != 3)
            return Fail("legal cargo was disturbed by compliance");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateComplianceChargesEscalatedFineOnce()
    {
        Context context = CreateContext(standing: -0.45f, credits: 20_000);
        GiveContraband(context.Player, 2);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(
            FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits, context.Reputation);
        int creditsBefore = context.Credits.Credits;
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Player.CargoHold,
                context.Credits, context.Reputation, out PoliceEnforcementResult? result, out _))
            return Fail("tiered compliance did not resolve");
        if (result?.CreditsCharged != offer.FineAmount || context.Credits.Credits != creditsBefore - offer.FineAmount)
            return Fail("compliance did not charge the exact escalated fine");
        if (offer.FineAmount != 1_563)
            return Fail($"expected escalated fine 1563, quoted {offer.FineAmount}");
        int afterFirst = context.Credits.Credits;
        bool second = service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Player.CargoHold,
            context.Credits, context.Reputation, out _, out _);
        if (second || context.Credits.Credits != afterFirst)
            return Fail("compliance charged twice");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateLegalCargoRemainsUntouched()
    {
        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        Commodity legal = CommodityCatalog.GetById("food-rations")!;
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        context.Player.CargoHold.AddCommodity(legal, 4);
        context.Player.CargoHold.AddCommodity(contraband, 1);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        context.Scan.TryAcceptEnforcement(context.Player, context.Credits, context.Reputation);
        if (context.Player.CargoHold.GetCommodityQuantity(legal.Name) != 4)
            return Fail("legal cargo was touched by tiered compliance");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateInsufficientCreditBehaviorRemainsSafe()
    {
        Context context = CreateContext(standing: -0.55f, credits: 1);
        GiveContraband(context.Player, 1);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(
            FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits, context.Reputation);
        if (offer.CanAffordFine || offer.AvailableResolutions.Contains(PoliceEnforcementResolution.Comply))
            return Fail("an unaffordable escalated fine still offered compliance");
        bool resolved = service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Player.CargoHold,
            context.Credits, context.Reputation, out _, out string reason);
        if (resolved || !reason.Contains("Insufficient credits", StringComparison.OrdinalIgnoreCase))
            return Fail("insufficient escalated credits did not fail deterministically");
        if (context.Credits.Credits != 1 || context.Credits.Credits < 0)
            return Fail("credits went inconsistent on failed tiered compliance");
        if (context.Player.CargoHold.GetCommodityQuantity(SideArmsName) != 1)
            return Fail("cargo was disturbed by failed tiered compliance");
        if (context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice))
            return Fail("failed compliance created hostility");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateTierZeroRefusalUsesExistingBehavior()
    {
        Context context = CreateContext(standing: 0f, credits: 20_000);
        GiveContraband(context.Player, 1);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (context.Scan.CurrentOffer?.EnforcementTier != PoliceEnforcementTier.Standard)
            return Fail("neutral demand was not Tier 0");
        if (!context.Scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation))
            return Fail("refusal did not resolve");
        if (!context.Fugitive.IsActive || context.Fugitive.Heat != PoliceHeatLevel.Pursuit)
            return Fail("Tier 0 refusal did not start the existing Heat 1 pursuit");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedRefusalEscalatesThroughFugitiveManager()
    {
        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        GiveContraband(context.Player, 1);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (context.Scan.CurrentOffer?.EnforcementTier != PoliceEnforcementTier.Severe)
            return Fail("severe demand was not Tier 2");
        if (!context.Scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation))
            return Fail("severe refusal did not resolve");
        if (!ReferenceEquals(context.Scan.FugitiveManager, context.Fugitive) || !context.Fugitive.IsActive)
            return Fail("severe refusal did not enter the single existing fugitive manager");
        if (context.Fugitive.Heat != PoliceHeatLevel.HotPursuit)
            return Fail($"severe refusal produced {context.Fugitive.Heat} instead of HotPursuit");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHighestTierUsesStrongestBoundedHeat()
    {
        if (PoliceEnforcementEscalationPolicy.GetInitialHeat(PoliceEnforcementTier.Severe) != PoliceHeatLevel.HotPursuit)
            return Fail("Tier 2 did not map to HotPursuit");
        int strongestSupportedHeat = Enum.GetValues(typeof(PoliceHeatLevel)).Cast<int>().Max();
        if (strongestSupportedHeat != 2)
            return Fail("heat scale changed underneath the escalation policy");
        Context context = CreateContext(standing: -0.55f);
        if (!context.Fugitive.BeginPursuit(context.Player, "phase 74 severe refusal", null, PoliceHeatLevel.HotPursuit))
            return Fail("severe pursuit did not start");
        if (context.Fugitive.Heat != PoliceHeatLevel.HotPursuit ||
            context.Fugitive.AcquisitionRadius != PoliceFugitiveManager.Heat2AcquisitionRadius ||
            context.Fugitive.EscapeDurationSeconds != PoliceFugitiveManager.Heat2EscapeDurationSeconds)
            return Fail("severe heat did not use the existing strongest bounded parameters");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNoSecondPursuitSystemExists()
    {
        foreach (System.Type type in typeof(PoliceFugitiveManager).Assembly.GetTypes())
        {
            string name = type.Name.ToLowerInvariant();
            if ((name.Contains("pursuit") || name.Contains("wanted") || name.Contains("crime")) &&
                !string.Equals(type.FullName, typeof(PoliceFugitiveManager).FullName, StringComparison.Ordinal) &&
                !string.Equals(type.FullName, typeof(PoliceFugitiveSmokeTest).FullName, StringComparison.Ordinal) &&
                !string.Equals(type.FullName, typeof(Phase74RepeatSmugglerEscalationSmokeTest).FullName, StringComparison.Ordinal))
                return Fail($"a second pursuit/crime type exists: {type.FullName}");
        }

        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        GiveContraband(context.Player, 1);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        context.Scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation);
        if (!context.Fugitive.IsActive)
            return Fail("refusal did not use the fugitive manager");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedPursuitUsesNormalAcquisition()
    {
        Context context = CreateContext(standing: -0.55f);
        NpcShip near = CreatePolice(new Vector3(2_000f, 0f, 0f));
        NpcShip far = CreatePolice(new Vector3(15_000f, 0f, 0f));
        context.Npcs.Add(near);
        context.Npcs.Add(far);
        context.Fugitive.BeginPursuit(context.Player, "phase 74 severe refusal", null, PoliceHeatLevel.HotPursuit);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        if (!near.HasPlayerTarget || near.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit)
            return Fail("nearby Police did not acquire the severe fugitive normally");
        if (far.HasPlayerTarget)
            return Fail("distant Police acquired omnisciently beyond the bounded Heat 2 radius");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHoldFireRemainsDuringLawfulStop()
    {
        TrafficContext traffic = CreateTrafficContext(standing: -0.25f);
        GiveContraband(traffic.Player, 2);
        NpcShip police = traffic.AddPolice(new Vector3(300f, 0f, 0f));
        traffic.StepFull();
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("lawful detection did not intercept the contraband hold");
        if (!police.IsLawfulStopHoldFire)
            return Fail("hold-fire was not set during the lawful stop");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHoldFireCeasesOnFugitiveEscalation()
    {
        TrafficContext traffic = CreateTrafficContext(standing: -0.25f);
        GiveContraband(traffic.Player, 2);
        NpcShip police = traffic.AddPolice(new Vector3(300f, 0f, 0f));
        for (int i = 0; i < 40 && traffic.Scan.State != PoliceScanState.ContrabandDetected; i++)
            traffic.StepFull();
        if (traffic.Scan.State != PoliceScanState.ContrabandDetected)
            return Fail("lawful demand never opened for the hold-fire escape proof");
        if (!traffic.Scan.TryRefuseEnforcement(traffic.Player, traffic.Credits, traffic.Reputation))
            return Fail("refusal did not resolve");
        traffic.StepFull();
        if (!traffic.Fugitive.IsActive)
            return Fail("refusal did not start the fugitive incident");
        if (police.IsLawfulStopHoldFire)
            return Fail("hold-fire survived fugitive escalation");
        if (police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit)
            return Fail("the interceptor was not converted to fugitive pursuit");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostilePoliceMayFireRegardless()
    {
        Context context = CreateContext(standing: -0.70f);
        NpcShip police = CreatePolice(new Vector3(500f, 0f, 0f));
        police.SetPlayerTarget(context.Player.Position, NpcPlayerTargetReason.ContrabandEnforcement);
        police.SetLawfulStopHoldFire(true);
        // This mirrors the NpcWeaponSystem lawful-stop gate: hold-fire
        // suppresses fire only while no valid hostility holds.
        bool gateSuppressesFire = police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement &&
            police.IsLawfulStopHoldFire &&
            !context.Reputation.IsFactionCurrentlyHostile(police.FactionId);
        if (gateSuppressesFire)
            return Fail("hostile Police were still suppressed by the lawful-stop flag");
        if (!context.Reputation.IsFactionCurrentlyHostile(police.FactionId))
            return Fail("hostility setup did not hold");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateGraceUsesAuthoritativeTierPolicy()
    {
        if (PoliceEnforcementEscalationPolicy.GetReacquisitionGraceSeconds(PoliceEnforcementTier.Standard) !=
            PoliceFugitiveManager.ContrabandReacquisitionGraceSeconds)
            return Fail("Tier 0 grace diverged from the Phase 72 constant");
        if (PoliceEnforcementEscalationPolicy.GetReacquisitionGraceSeconds(PoliceEnforcementTier.Elevated) != 12f)
            return Fail("Tier 1 grace was not 12s");
        if (PoliceEnforcementEscalationPolicy.GetReacquisitionGraceSeconds(PoliceEnforcementTier.Severe) != 6f)
            return Fail("Tier 2 grace was not 6s");

        Context elevated = CreateContext(standing: -0.45f);
        ForceEscape(elevated, PoliceHeatLevel.Pursuit);
        if (!Nearly(elevated.Fugitive.ContrabandReacquisitionGraceRemainingSeconds, 12f))
            return Fail($"Tier 1 escape produced {elevated.Fugitive.ContrabandReacquisitionGraceRemainingSeconds:0.0}s instead of 12s");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateTierZeroGraceRetainsExistingSemantics()
    {
        Context context = CreateContext(standing: 0f);
        ForceEscape(context, PoliceHeatLevel.Pursuit);
        if (!context.Fugitive.IsContrabandReacquisitionGraceActive)
            return Fail("Tier 0 escape did not arm the reacquisition grace");
        if (!Nearly(context.Fugitive.ContrabandReacquisitionGraceRemainingSeconds,
                PoliceFugitiveManager.ContrabandReacquisitionGraceSeconds))
            return Fail("Tier 0 grace diverged from the 20-second Phase 72 value");
        context.Fugitive.Update(PoliceFugitiveManager.ContrabandReacquisitionGraceSeconds + 1f, context.Player, context.Npcs);
        if (context.Fugitive.IsContrabandReacquisitionGraceActive)
            return Fail("Tier 0 grace did not expire");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedGraceRemainsBounded()
    {
        Context context = CreateContext(standing: -0.55f);
        ForceEscape(context, PoliceHeatLevel.HotPursuit);
        float grace = context.Fugitive.ContrabandReacquisitionGraceRemainingSeconds;
        if (!context.Fugitive.IsContrabandReacquisitionGraceActive)
            return Fail("severe escape left no separation grace");
        if (grace < PoliceEnforcementEscalationPolicy.MinimumReacquisitionGraceSeconds - 0.05f)
            return Fail($"severe grace {grace:0.0}s undercut the anti-restop floor");
        if (grace >= PoliceFugitiveManager.ContrabandReacquisitionGraceSeconds)
            return Fail("severe grace was not shorter than standard grace");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateFutureDetectionRemainsPossible()
    {
        TrafficContext traffic = CreateTrafficContext(standing: -0.45f);
        GiveContraband(traffic.Player, 2);
        // The incident itself never touches standing, so the escape grace
        // below is genuinely the Tier 1 policy value.
        traffic.Fugitive.BeginPursuit(traffic.Player, "phase 74 grace proof");
        ForceEscape(traffic.Fugitive, traffic.Player, traffic.Npcs, PoliceHeatLevel.Pursuit);
        traffic.Fugitive.Update(13f, traffic.Player, traffic.Npcs);
        if (traffic.Fugitive.IsContrabandReacquisitionGraceActive)
            return Fail("Tier 1 grace survived past its 12-second policy");
        NpcShip police = traffic.AddPolice(new Vector3(300f, 0f, 0f));
        traffic.StepFull();
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("Police could not re-detect contraband after grace expiry");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateComplianceKeepsDurableStanding()
    {
        Context context = CreateContext(standing: -0.45f, credits: 20_000);
        GiveContraband(context.Player, 2);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(
            FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits, context.Reputation);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Player.CargoHold,
                context.Credits, context.Reputation, out _, out _))
            return Fail("compliance did not resolve");
        if (!Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), -0.48f))
            return Fail("compliance erased or skipped the durable standing consequence");
        if (PoliceEnforcementEscalationPolicy.GetTier(context.Reputation) != PoliceEnforcementTier.Elevated)
            return Fail("post-compliance standing did not keep future enforcement elevated");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateImprovingStandingLowersTier()
    {
        Context context = CreateContext(standing: -0.55f, credits: 100_000);
        if (EvaluateTiered(context, 1).EnforcementTier != PoliceEnforcementTier.Severe)
            return Fail("severe baseline was not Tier 2");
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Elena Vasquez",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            context.Reputation,
            context.Credits);
        if (offer?.IsValid != true ||
            !FactionBribeService.TryPurchase(offer, context.Reputation, context.Credits, out _, out _))
            return Fail("the existing bribery recovery path rejected the purchase");
        if (PoliceEnforcementEscalationPolicy.GetTier(context.Reputation) != PoliceEnforcementTier.Standard)
            return Fail("recovered standing did not de-escalate the derived tier");
        GiveContraband(context.Player, 2);
        PoliceEnforcementService tieredService = new();
        PoliceEnforcementOffer recovered = tieredService.Evaluate(
            FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits, context.Reputation);
        if (recovered.EnforcementTier != PoliceEnforcementTier.Standard ||
            recovered.FineAmount != tieredService.CalculateFine(recovered.TotalViolationValue))
            return Fail("recovered standing did not restore the standard fine path");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateMissionCargoUsesSameTierLogic()
    {
        Context context = CreateContext(standing: -0.45f, credits: 20_000);
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        if (!context.Player.CargoHold.AddMissionCargo(9001, contraband, 2))
            return Fail("could not stage mission-bound contraband");
        PoliceEnforcementOffer offer = EvaluateTiered(context, 0);
        if (!offer.HasContraband || offer.EnforcementTier != PoliceEnforcementTier.Elevated)
            return Fail("mission contraband did not receive the standing-derived tier");
        if (offer.FineAmount != 1_563)
            return Fail($"mission cargo fine {offer.FineAmount} diverged from the ordinary Tier 1 fine 1563");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateOrdinaryContrabandUsesSameTierLogic()
    {
        Context context = CreateContext(standing: -0.45f, credits: 20_000);
        GiveContraband(context.Player, 2);
        PoliceEnforcementOffer offer = EvaluateTiered(context, 0);
        if (!offer.HasContraband || offer.EnforcementTier != PoliceEnforcementTier.Elevated)
            return Fail("ordinary contraband did not receive the standing-derived tier");
        if (offer.FineAmount != 1_563)
            return Fail($"ordinary cargo fine {offer.FineAmount} diverged from 1563");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateLegalMissionCargoRemainsUnaffected()
    {
        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        Commodity legal = CommodityCatalog.GetById("food-rations")!;
        if (!context.Player.CargoHold.AddMissionCargo(9001, legal, 2))
            return Fail("could not stage legal mission cargo");
        PoliceEnforcementOffer offer = EvaluateTiered(context, 0);
        if (offer.HasViolation || offer.FineAmount != 0)
            return Fail("legal mission cargo was treated as a violation at Tier 2");
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (context.Scan.State != PoliceScanState.Cleared)
            return Fail("a scan of legal mission cargo did not clear");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateMultipleStacksDoNotMultiplyEscalation()
    {
        Context context = CreateContext(standing: -0.45f, credits: 50_000);
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        Commodity legal = CommodityCatalog.GetById("water")!;
        context.Player.CargoHold.AddCommodity(contraband, 1);
        context.Player.CargoHold.AddStolenCommodity(legal, 1);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(
            FactionManager.LibertyPolice, context.Player.CargoHold, context.Credits, context.Reputation);
        if (!offer.HasContraband || !offer.HasStolenGoods)
            return Fail("mixed violation cargo lost a violation type");
        int expected = PoliceEnforcementEscalationPolicy.ApplyFineEscalation(
            service.CalculateFine(offer.TotalViolationValue), PoliceEnforcementTier.Elevated);
        if (offer.FineAmount != expected)
            return Fail($"escalation was not applied exactly once (fine {offer.FineAmount}, expected {expected})");
        if (offer.FineAmount <= service.CalculateFine(offer.TotalViolationValue))
            return Fail("the elevated multiplier did not lift the fine above the base violation value");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateClassificationRemainsUnchanged()
    {
        Context context = CreateContext(standing: -0.55f, credits: 50_000);
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        Commodity legal = CommodityCatalog.GetById("water")!;
        context.Player.CargoHold.AddCommodity(contraband, 1);
        context.Player.CargoHold.AddStolenCommodity(legal, 1);
        PoliceEnforcementOffer offer = EvaluateTiered(context, 0);
        ContrabandFinding? arms = offer.Contraband.FirstOrDefault(finding => finding.IsContraband);
        ContrabandFinding? stolen = offer.Contraband.FirstOrDefault(finding => finding.IsStolen && !finding.IsContraband);
        if (arms == null || stolen == null)
            return Fail("violation classification changed under escalation");
        if (arms.ViolationType != "contraband" || stolen.ViolationType != "stolen goods")
            return Fail("violation type labels changed under escalation");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSaveLoadReproducesTier()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-phase74-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "phase74.json");
        try
        {
            Context source = CreateContext(standing: -0.55f, credits: 20_000);
            GiveContraband(source.Player, 2);
            int fineBefore = EvaluateTiered(source, 0).FineAmount;
            SaveGameManager saver = new(path);
            SaveGameData data = new()
            {
                PlayerCredits = source.Credits.Credits,
                Cargo = saver.CaptureCargo(source.Player.CargoHold),
                FactionReputation = saver.CaptureReputation(source.Reputation)
            };
            if (!saver.TrySave(data, out string saveFailure))
                return Fail($"save failed ({saveFailure})");
            if (!saver.TryLoad(out SaveGameData loaded, out string loadFailure) || loaded == null)
                return Fail($"load failed ({loadFailure})");

            Context restored = CreateContext();
            restored.Credits.SetCredits(loaded.PlayerCredits);
            saver.ApplyCargo(restored.Cargo, loaded, out _);
            saver.ApplyReputation(restored.Reputation, loaded);
            if (!Nearly(restored.Reputation.GetStanding(FactionManager.LibertyPolice), -0.55f))
                return Fail("Police standing did not survive save/load");
            if (PoliceEnforcementEscalationPolicy.GetTier(restored.Reputation) != PoliceEnforcementTier.Severe)
                return Fail("the derived tier was not recomputed after load");
            if (EvaluateTiered(restored, 0).FineAmount != fineBefore)
                return Fail("the elevated fine behavior did not survive save/load");

            // An old compatible save (no escalation fields anywhere) loads normally.
            SaveGameData legacy = new()
            {
                PlayerCredits = 5_000,
                Cargo = new List<SaveCargoItemData>(),
                FactionReputation = new List<SaveFactionReputationData>
                {
                    new() { FactionId = FactionManager.LibertyPolice, Standing = -0.20f }
                }
            };
            if (!saver.TrySave(legacy, out _) || !saver.TryLoad(out SaveGameData legacyLoaded, out _))
                return Fail("a compatible legacy-shaped save did not load");
            Context legacyContext = CreateContext();
            saver.ApplyReputation(legacyContext.Reputation, legacyLoaded);
            if (PoliceEnforcementEscalationPolicy.GetTier(legacyContext.Reputation) != PoliceEnforcementTier.Standard)
                return Fail("an old save did not default to standard enforcement");
            return Pass();
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private (bool Success, string FailureReason) ValidateSaveLoadDuplicatesNothing()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-phase74-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "phase74.json");
        try
        {
            Context source = CreateContext(standing: -0.45f, credits: 20_000);
            GiveContraband(source.Player, 2);
            SaveGameManager saver = new(path);
            SaveGameData data = new()
            {
                PlayerCredits = source.Credits.Credits,
                Cargo = saver.CaptureCargo(source.Player.CargoHold),
                FactionReputation = saver.CaptureReputation(source.Reputation)
            };
            if (!saver.TrySave(data, out _) || !saver.TryLoad(out SaveGameData loaded, out _))
                return Fail("save/load round trip failed");
            Context restored = CreateContext();
            restored.Credits.SetCredits(loaded.PlayerCredits);
            saver.ApplyCargo(restored.Cargo, loaded, out _);
            saver.ApplyReputation(restored.Reputation, loaded);
            if (restored.Cargo.GetCommodityQuantity(SideArmsName) != 2 || restored.Credits.Credits != 20_000)
                return Fail("save/load duplicated or lost cargo or credits");
            PoliceEnforcementService service = new();
            PoliceEnforcementOffer offer = service.Evaluate(
                FactionManager.LibertyPolice, restored.Cargo, restored.Credits, restored.Reputation);
            int before = restored.Credits.Credits;
            if (!service.TryResolve(offer, PoliceEnforcementResolution.Comply, restored.Cargo,
                    restored.Credits, restored.Reputation, out _, out _))
                return Fail("post-load compliance did not resolve");
            if (restored.Cargo.GetCommodityQuantity(SideArmsName) != 0 ||
                restored.Credits.Credits != before - offer.FineAmount)
                return Fail("post-load compliance duplicated or skipped the fine/cargo");
            return Pass();
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private (bool Success, string FailureReason) ValidateSystemResetClearsTransientState()
    {
        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        GiveContraband(context.Player, 2);
        StepScan(context.Scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        context.Scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation);
        if (!context.Fugitive.IsActive)
            return Fail("refusal did not stage the transient incident");
        context.Scan.Reset();
        context.Fugitive.Reset(reason: "system transition");
        if (context.Scan.State != PoliceScanState.Idle || context.Fugitive.IsActive ||
            context.Fugitive.IsContrabandReacquisitionGraceActive ||
            context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice))
            return Fail("system reset left transient stop/fugitive state behind");
        if (!Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), -0.75f))
            return Fail("system reset disturbed the durable standing");
        if (PoliceEnforcementEscalationPolicy.GetTier(context.Reputation) != PoliceEnforcementTier.Severe)
            return Fail("durable standing did not keep its derived tier after reset");
        if (context.Player.CargoHold.GetCommodityQuantity(SideArmsName) != 2)
            return Fail("system reset disturbed authoritative cargo");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateDeathLeavesNoStaleEscalationState()
    {
        Context context = CreateContext(standing: -0.55f, credits: 20_000);
        GiveContraband(context.Player, 1);
        NpcShip scanner = CreateScanner(new Vector3(1_500f, 0f, 0f));
        context.Npcs.Add(scanner);
        StepScan(context.Scan, context, scanner, 0.5f, 4);
        if (context.Scan.State != PoliceScanState.Scanning)
            return Fail("scan did not stage for the death proof");
        context.Fugitive.BeginPursuit(context.Player, "phase 74 death proof", null, PoliceHeatLevel.HotPursuit);
        context.Player.Hull.TakeDamage(100_000f);
        context.Scan.Update(
            new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5)),
            context.Player,
            context.Npcs,
            context.Credits,
            context.Reputation);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        if (context.Scan.State != PoliceScanState.Idle || context.Scan.CurrentOffer != null)
            return Fail("death left a stale active stop behind");
        if (context.Fugitive.IsActive || context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice))
            return Fail("death left stale fugitive escalation state behind");
        if (!Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), -0.55f))
            return Fail("death disturbed the durable standing");
        return Pass();
    }

    private static PoliceEnforcementOffer EvaluateTiered(Context context, int quantity = 0)
    {
        if (quantity > 0)
            GiveContraband(context.Player, quantity);
        return new PoliceEnforcementService().Evaluate(
            FactionManager.LibertyPolice,
            context.Player.CargoHold,
            context.Credits,
            context.Reputation);
    }

    private static void ForceEscape(Context context, PoliceHeatLevel heat)
    {
        ForceEscape(context.Fugitive, context.Player, context.Npcs, heat);
    }

    private static void ForceEscape(PoliceFugitiveManager fugitive, Ship player, List<NpcShip> npcs, PoliceHeatLevel heat)
    {
        if (!fugitive.IsActive)
            fugitive.BeginPursuit(player, "phase 74 escape proof", null,
                heat == PoliceHeatLevel.HotPursuit ? PoliceHeatLevel.HotPursuit : PoliceHeatLevel.Pursuit);
        foreach (NpcShip npc in npcs)
        {
            if (npc != null)
                npc.Position = new Vector3(20_000f, 0f, 0f);
        }

        fugitive.Update(0.5f, player, npcs);
        float escapeDuration = heat == PoliceHeatLevel.HotPursuit
            ? PoliceFugitiveManager.Heat2EscapeDurationSeconds
            : PoliceFugitiveManager.Heat1EscapeDurationSeconds;
        fugitive.Update(escapeDuration, player, npcs);
    }

    private static Context CreateContext(float standing = 0.00f, int credits = 10_000)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "phase 74 smoke setup");
        Ship player = new(Vector3.Zero);
        Context context = new(reputation, player, player.CargoHold, new PlayerCredits(credits), new List<NpcShip>());
        context.Scan.SetFugitiveManager(context.Fugitive);
        return context;
    }

    private static TrafficContext CreateTrafficContext(float standing = 0.00f, int credits = 20_000)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "phase 74 smoke setup");
        Ship player = new(Vector3.Zero);
        PlayerCredits playerCredits = new(credits);
        List<NpcShip> npcs = new();
        List<SpaceObject> objects = new();
        TrafficManager traffic = new(new ConfigurationManager(), npcs, objects);
        LootManager loot = new();
        traffic.ConfigureContrabandEnforcement(
            _ => NpcCargoManifestSnapshot.NoRegisteredCargo(),
            () => loot.ActivePods,
            (enforcer, pod) => loot.TrySeizeContrabandPodForNpc(enforcer, pod));
        PoliceScanSystem scan = new();
        PoliceFugitiveManager fugitive = new(reputation);
        traffic.FugitiveManager = fugitive;
        scan.SetFugitiveManager(fugitive);
        PoliceContrabandStopCoordinator coordinator = new();
        return new TrafficContext(player, playerCredits, reputation, traffic, scan, fugitive, coordinator, loot, npcs, objects);
    }

    private static bool GiveContraband(Ship player, int quantity)
    {
        if (quantity <= 0)
            return true;

        return player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms")!, quantity);
    }

    private static string SideArmsName => CommodityCatalog.GetById("side-arms")!.Name;

    private static NpcShip CreateScanner(Vector3 position) =>
        new("Phase 74 Police Scan", position, position, 1f, 0f, FactionManager.LibertyPolice)
        {
            Position = position,
            Velocity = Vector3.Zero
        };

    private static NpcShip CreatePolice(Vector3 position) =>
        new("Phase 74 Police", position, position, 1f, 0f, FactionManager.LibertyPolice);

    private static void StepScan(
        PoliceScanSystem scan,
        Context context,
        NpcShip scanner,
        float seconds,
        int frameCount,
        Action<string>? log = null)
    {
        List<NpcShip> scanners = new() { scanner };
        TimeSpan total = TimeSpan.Zero;
        for (int i = 0; i < frameCount; i++)
        {
            TimeSpan previous = total;
            total += TimeSpan.FromSeconds(seconds);
            scan.Update(new GameTime(previous, TimeSpan.FromSeconds(seconds)), context.Player, scanners, context.Credits, context.Reputation,
                notificationManager: null, log: log);
        }
    }

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);

    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private sealed class Context
    {
        public Context(
            ReputationManager reputation,
            Ship player,
            CargoHold cargo,
            PlayerCredits credits,
            List<NpcShip> npcs)
        {
            Reputation = reputation;
            Player = player;
            Cargo = cargo;
            Credits = credits;
            Npcs = npcs;
            Fugitive = new PoliceFugitiveManager(reputation);
            Scan = new PoliceScanSystem();
        }

        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public CargoHold Cargo { get; }
        public PlayerCredits Credits { get; }
        public List<NpcShip> Npcs { get; }
        public PoliceFugitiveManager Fugitive { get; }
        public PoliceScanSystem Scan { get; }
    }

    private sealed class TrafficContext
    {
        public TrafficContext(
            Ship player,
            PlayerCredits credits,
            ReputationManager reputation,
            TrafficManager traffic,
            PoliceScanSystem scan,
            PoliceFugitiveManager fugitive,
            PoliceContrabandStopCoordinator coordinator,
            LootManager loot,
            List<NpcShip> npcs,
            List<SpaceObject> objects)
        {
            Player = player;
            Credits = credits;
            Reputation = reputation;
            Traffic = traffic;
            Scan = scan;
            Fugitive = fugitive;
            Coordinator = coordinator;
            Loot = loot;
            Npcs = npcs;
            Objects = objects;
        }

        public Ship Player { get; }
        public PlayerCredits Credits { get; }
        public ReputationManager Reputation { get; }
        public TrafficManager Traffic { get; }
        public PoliceScanSystem Scan { get; }
        public PoliceFugitiveManager Fugitive { get; }
        public PoliceContrabandStopCoordinator Coordinator { get; }
        public LootManager Loot { get; }
        public List<NpcShip> Npcs { get; }
        public List<SpaceObject> Objects { get; }
        public List<string> Log { get; } = new();

        public NpcShip AddPolice(Vector3 position, string name = "Phase 74 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase74-police",
                position,
                800f,
                180f,
                ContrabandEnforcementPolicy.DefaultDetectionRange);
            police.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                "Smoke Fighter",
                FactionManager.LibertyPolice,
                "SMOKE/fighter",
                TrafficZoneBehaviorType.LawfulPatrol,
                NpcLoadoutTier.Standard));
            Npcs.Add(police);
            Objects.Add(police);
            return police;
        }

        public void StepFull(float seconds = 0.1f)
        {
            Traffic.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                Player,
                Reputation,
                Log.Add);
            GameTime frame = new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
            foreach (NpcShip npc in Npcs.ToList())
                npc?.Update(frame, null, Player, Reputation);
            Scan.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                Player,
                Npcs,
                Credits,
                Reputation,
                notificationManager: null,
                log: Log.Add);
            Coordinator.Update(Player, Npcs, Traffic, Scan, Fugitive, Reputation, Log.Add);
        }
    }
}
