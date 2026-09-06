#nullable enable

using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 53 coverage. All hostility expiry is advanced through
/// the injected simulation delta; no wall-clock waits or scan randomness are
/// used.
/// </summary>
internal sealed class PoliceEnforcementSmokeTest
{
    private readonly PoliceEnforcementService _service = new();
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("clean cargo produces no violation", CleanCargoIsIgnored);
        Check("Liberty Police recognizes existing contraband", PoliceRecognizesContraband);
        Check("legal cargo is ignored", LegalCargoIsIgnored);
        Check("fine calculation is deterministic", FineCalculationIsDeterministic);
        Check("fine minimum is bounded", FineMinimumIsBounded);
        Check("fine maximum is bounded", FineMaximumIsBounded);
        Check("affordable compliance deducts exact credits", AffordableComplianceChargesExactFine);
        Check("compliance confiscates exact illegal cargo", ComplianceConfiscatesExactCargo);
        Check("mixed mission and ordinary cargo confiscates atomically", MixedCargoConfiscationUpdatesReservations);
        Check("post-detection jettison does not erase the fine", PostDetectionJettisonStillChargesFine);
        Check("repeated compliance is one-shot", RepeatedComplianceCannotDoubleCharge);
        Check("compliance does not create temporary hostility", ComplianceDoesNotCreateHostility);
        Check("stale offer cannot leave a partial transaction", StaleOfferIsAtomic);
        Check("insufficient credits never go negative", InsufficientCreditsStayNonNegative);
        Check("insufficient-credit policy leaves demand unresolved", InsufficientCreditPolicyIsDeterministic);
        Check("refusal preserves contraband", RefusalPreservesCargo);
        Check("refusal creates Liberty Police hostility", RefusalCreatesHostility);
        Check("refusal applies the exact police penalty", RefusalPenaltyIsExact);
        Check("Liberty Rogue reputation is unaffected", RefusalDoesNotChangeRogueStanding);
        Check("refusal immediately denies police docking", RefusalDeniesPoliceDocking);
        Check("unrelated Rogue docking remains available", RogueDockingRemainsAvailable);
        Check("temporary expiry restores police docking", TemporaryExpiryRestoresPoliceDocking);
        Check("repeated refusals reach permanent hostility", RepeatedRefusalsReachHostileStanding);
        Check("permanent hostility survives timer expiry", PermanentHostilityStaysDenied);
        Check("bribery recovers recoverable police standing", BriberyRecoversRecoverableStanding);
        Check("bribery does not clear active police hostility", BriberyDoesNotClearHostility);
        Check("bribery ceiling does not unlock Friendly equipment", BriberyCeilingKeepsEquipmentLocked);
        Check("save/load preserves enforcement mutations", SaveLoadPreservesMutations);
        Check("already-docked player remains safe", AlreadyDockedPlayerRemainsSafe);
        Check("reset clears enforcement consequences", ResetClearsEnforcementState);

        Console.WriteLine($"[POLICE ENFORCEMENT SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[POLICE ENFORCEMENT SMOKE] PASS {label}");
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
        Console.WriteLine($"[POLICE ENFORCEMENT SMOKE] FAIL {label}: {reason}");
    }

    private bool CleanCargoIsIgnored()
    {
        Context context = CreateContext();
        context.Cargo.AddCommodity(CommodityCatalog.GetById("food-rations")!, 2);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return offer.IsInspectionApplicable && !offer.HasContraband && offer.FineAmount == 0 &&
            offer.AvailableResolutions.Count == 0;
    }

    private bool PoliceRecognizesContraband()
    {
        Context context = CreateContext();
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        context.Cargo.AddCommodity(contraband, 2);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return offer.HasContraband && offer.TotalContrabandQuantity == 2 &&
            offer.Contraband.Count == 1 && offer.Contraband[0].CommodityId == contraband.Id &&
            offer.Summary.Contains("contraband detected", StringComparison.OrdinalIgnoreCase);
    }

    private bool LegalCargoIsIgnored()
    {
        Context context = CreateContext();
        context.Cargo.AddCommodity(CommodityCatalog.GetById("diamonds")!, 3);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return !offer.HasContraband && offer.TotalContrabandValue == 0;
    }

    private bool FineCalculationIsDeterministic()
    {
        Context context = CreateContext(credits: 20_000);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 2);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("alien-organisms")!, 1);
        PoliceEnforcementOffer first = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        PoliceEnforcementOffer second = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return first.TotalContrabandValue == 6_000 && first.FineAmount == 2_000 &&
            first.FineAmount == second.FineAmount && first.CargoFingerprint == second.CargoFingerprint;
    }

    private bool FineMinimumIsBounded() => _service.CalculateFine(0) == PoliceEnforcementService.MinimumFineCredits;

    private bool FineMaximumIsBounded() => _service.CalculateFine(long.MaxValue) == PoliceEnforcementService.MaximumFineCredits;

    private bool AffordableComplianceChargesExactFine()
    {
        Context context = CreateContext(standing: 0.30f, credits: 10_000);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 2);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        if (!_service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out PoliceEnforcementResult? result, out _))
            return false;

        return result?.Outcome == PoliceEnforcementOutcome.PaidAndConfiscated &&
            result.CreditsCharged == 1_250 && context.Credits.Credits == 8_750;
    }

    private bool ComplianceConfiscatesExactCargo()
    {
        Context context = CreateContext(standing: 0.30f, credits: 20_000);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        Commodity aliens = CommodityCatalog.GetById("alien-organisms")!;
        Commodity food = CommodityCatalog.GetById("food-rations")!;
        context.Cargo.AddCommodity(sideArms, 2);
        context.Cargo.AddCommodity(aliens, 1);
        context.Cargo.AddCommodity(food, 3);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        if (!_service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out PoliceEnforcementResult? result, out _))
            return false;

        return result?.ConfiscatedQuantity == 3 &&
            context.Cargo.GetCommodityQuantity(sideArms.Name) == 0 &&
            context.Cargo.GetCommodityQuantity(aliens.Name) == 0 &&
            context.Cargo.GetCommodityQuantity(food.Name) == 3;
    }

    private bool ComplianceDoesNotCreateHostility()
    {
        Context context = CreateContext(standing: 0.30f, credits: 10_000);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return _service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out _) &&
            !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private bool MixedCargoConfiscationUpdatesReservations()
    {
        Context context = CreateContext(standing: 0.30f, credits: 50_000);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        Commodity aliens = CommodityCatalog.GetById("alien-organisms")!;
        Commodity food = CommodityCatalog.GetById("food-rations")!;
        context.Cargo.AddMissionCargo(9001, sideArms, 1);
        context.Cargo.AddCommodity(sideArms, 2);
        context.Cargo.AddCommodity(aliens, 1);
        context.Cargo.AddCommodity(food, 3);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        if (!_service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out PoliceEnforcementResult? result, out _))
            return false;

        return result?.MissionConfiscations.Count == 1 &&
            result.MissionConfiscations[0].MissionId == 9001 &&
            context.Cargo.GetMissionCargoQuantity(9001) == 0 &&
            context.Cargo.GetCommodityQuantity(sideArms.Name) == 0 &&
            context.Cargo.GetCommodityQuantity(aliens.Name) == 0 &&
            context.Cargo.GetCommodityQuantity(food.Name) == 3 &&
            context.Cargo.UsedCapacity == food.VolumePerUnit * 3;
    }

    private bool PostDetectionJettisonStillChargesFine()
    {
        Context context = CreateContext(standing: 0.30f, credits: 50_000);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        context.Cargo.AddCommodity(sideArms, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        if (!context.Cargo.RemoveCommodity(sideArms, 1))
            return false;

        return _service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out PoliceEnforcementResult? result, out _) &&
            result?.CreditsCharged == offer.FineAmount &&
            context.Credits.Credits == 50_000 - offer.FineAmount &&
            result.ConfiscatedQuantity == 0;
    }

    private bool RepeatedComplianceCannotDoubleCharge()
    {
        Context context = CreateContext(standing: 0.30f, credits: 50_000);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        context.Cargo.AddCommodity(sideArms, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        if (!_service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out _))
            return false;

        int afterFirst = context.Credits.Credits;
        bool second = _service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out _);
        return !second && context.Credits.Credits == afterFirst;
    }

    private bool StaleOfferIsAtomic()
    {
        Context context = CreateContext(standing: 0.30f, credits: 10_000);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        context.Cargo.AddCommodity(sideArms, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        context.Cargo.AddCommodity(sideArms, 1);
        int creditsBefore = context.Credits.Credits;
        bool resolved = _service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out _);
        return !resolved && context.Credits.Credits == creditsBefore && context.Cargo.GetCommodityQuantity(sideArms.Name) == 2;
    }

    private bool InsufficientCreditsStayNonNegative()
    {
        Context context = CreateContext(standing: 0.30f, credits: 1);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        context.Cargo.AddCommodity(sideArms, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        bool resolved = _service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out string reason);
        return !resolved && reason.Contains("Insufficient credits", StringComparison.OrdinalIgnoreCase) &&
            context.Credits.Credits == 1 && context.Credits.Credits >= 0 &&
            context.Cargo.GetCommodityQuantity(sideArms.Name) == 1;
    }

    private bool InsufficientCreditPolicyIsDeterministic()
    {
        Context context = CreateContext(standing: 0.30f, credits: 1);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        context.Cargo.AddCommodity(sideArms, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        bool resolved = _service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out string reason);
        return !resolved && reason.Contains("Insufficient credits", StringComparison.OrdinalIgnoreCase) &&
            context.Cargo.GetCommodityQuantity(sideArms.Name) == 1 &&
            !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), 0.30f);
    }

    private bool RefusalPreservesCargo()
    {
        Context context = CreateContext(standing: 0.30f, credits: 10_000);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        context.Cargo.AddCommodity(sideArms, 2);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return _service.TryResolve(offer, PoliceEnforcementResolution.Refuse, context.Cargo, context.Credits, context.Reputation, out PoliceEnforcementResult? result, out _) &&
            result?.Outcome == PoliceEnforcementOutcome.Refused &&
            context.Cargo.GetCommodityQuantity(sideArms.Name) == 2 && context.Credits.Credits == 10_000;
    }

    private bool RefusalCreatesHostility()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return _service.TryResolve(offer, PoliceEnforcementResolution.Refuse, context.Cargo, context.Credits, context.Reputation, out _, out _) &&
            context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            Nearly(context.Reputation.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice), PoliceEnforcementService.TemporaryHostilityDurationSeconds);
    }

    private bool RefusalPenaltyIsExact()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return _service.TryResolve(offer, PoliceEnforcementResolution.Refuse, context.Cargo, context.Credits, context.Reputation, out _, out _) &&
            Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), 0.10f);
    }

    private bool RefusalDoesNotChangeRogueStanding()
    {
        Context context = CreateContext(standing: 0.30f);
        float rogueBefore = context.Reputation.GetStanding(FactionManager.LibertyRogues);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        _service.TryResolve(offer, PoliceEnforcementResolution.Refuse, context.Cargo, context.Credits, context.Reputation, out _, out _);
        return Nearly(context.Reputation.GetStanding(FactionManager.LibertyRogues), rogueBefore);
    }

    private bool RefusalDeniesPoliceDocking()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        ResolveRefusal(context);
        return !DockingAccess(PoliceStation(), context.Reputation).IsAllowed;
    }

    private bool RogueDockingRemainsAvailable()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        ResolveRefusal(context);
        return DockingAccess(RogueStation(), context.Reputation).IsAllowed;
    }

    private bool TemporaryExpiryRestoresPoliceDocking()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        ResolveRefusal(context);
        context.Reputation.UpdateTemporaryHostility(PoliceEnforcementService.TemporaryHostilityDurationSeconds);
        return !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            DockingAccess(PoliceStation(), context.Reputation).IsAllowed;
    }

    private bool RepeatedRefusalsReachHostileStanding()
    {
        Context context = CreateContext(standing: 0.30f);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        for (int i = 0; i < 3; i++)
        {
            context.Cargo.AddCommodity(sideArms, 1);
            ResolveRefusal(context);
        }

        return Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), -0.30f) &&
            context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private bool PermanentHostilityStaysDenied()
    {
        Context context = CreateContext(standing: 0.30f);
        Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
        context.Reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "phase 53 permanent hostility setup");
        context.Reputation.UpdateTemporaryHostility(PoliceEnforcementService.TemporaryHostilityDurationSeconds);
        return context.Reputation.IsHostile(FactionManager.LibertyPolice) &&
            !DockingAccess(PoliceStation(), context.Reputation).IsAllowed;
    }

    private bool BriberyRecoversRecoverableStanding()
    {
        Context context = CreateContext(standing: -0.45f, credits: 100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Elena Vasquez",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            context.Reputation,
            context.Credits);
        return offer?.IsValid == true &&
            FactionBribeService.TryPurchase(offer, context.Reputation, context.Credits, out _, out _) &&
            Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), FactionBribeService.BriberyCeiling);
    }

    private bool BriberyDoesNotClearHostility()
    {
        Context context = CreateContext(standing: -0.45f, credits: 100_000);
        context.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Elena Vasquez", FactionManager.LibertyPolice, FactionManager.LibertyPolice,
            FactionManager.LibertyPolice, context.Reputation, context.Credits);
        return offer?.IsValid == true &&
            FactionBribeService.TryPurchase(offer, context.Reputation, context.Credits, out _, out _) &&
            context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private bool BriberyCeilingKeepsEquipmentLocked()
    {
        Context context = CreateContext(standing: -0.45f, credits: 100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Elena Vasquez", FactionManager.LibertyPolice, FactionManager.LibertyPolice,
            FactionManager.LibertyPolice, context.Reputation, context.Credits);
        if (offer?.IsValid != true || !FactionBribeService.TryPurchase(offer, context.Reputation, context.Credits, out _, out _))
            return false;

        EquipmentDealer dealer = new(context.Reputation);
        dealer.SetDockedStation(PoliceStation());
        EquipmentDefinition friendlyEquipment = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        FactionAccessResult access = dealer.GetEquipmentAccess(friendlyEquipment);
        return Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), 0.10f) && !access.IsAllowed;
    }

    private bool SaveLoadPreservesMutations()
    {
        string savePath = Path.Combine(Path.GetTempPath(), $"roguelancer-phase28-{Guid.NewGuid():N}.json");
        SaveGameManager saveManager = new(savePath);
        try
        {
            Context source = CreateContext(standing: 0.30f, credits: 10_000);
            Commodity sideArms = CommodityCatalog.GetById("side-arms")!;
            source.Cargo.AddCommodity(sideArms, 1);
            PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, source.Cargo, source.Credits);
            if (!_service.TryResolve(offer, PoliceEnforcementResolution.Comply, source.Cargo, source.Credits, source.Reputation, out _, out _))
                return false;

            SaveGameData data = new()
            {
                PlayerCredits = source.Credits.Credits,
                Cargo = saveManager.CaptureCargo(source.Cargo),
                FactionReputation = saveManager.CaptureReputation(source.Reputation),
                TemporaryHostility = saveManager.CaptureTemporaryHostility(source.Reputation)
            };
            if (!saveManager.TrySave(data, out _) || !saveManager.TryLoad(out SaveGameData loaded, out _))
                return false;

            Context restored = CreateContext();
            restored.Credits.SetCredits(loaded.PlayerCredits);
            saveManager.ApplyCargo(restored.Cargo, loaded, out _);
            saveManager.ApplyReputation(restored.Reputation, loaded);
            saveManager.ApplyTemporaryHostility(restored.Reputation, loaded);
            bool valid = restored.Credits.Credits == 9_125 &&
                restored.Cargo.GetCommodityQuantity(sideArms.Name) == 0 &&
                Nearly(restored.Reputation.GetStanding(FactionManager.LibertyPolice), 0.27f) &&
                !restored.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
            if (!valid)
                return false;

            Context refused = CreateContext(standing: 0.30f, credits: 10_000);
            refused.Cargo.AddCommodity(sideArms, 1);
            ResolveRefusal(refused);
            SaveGameData refusedData = new()
            {
                PlayerCredits = refused.Credits.Credits,
                Cargo = saveManager.CaptureCargo(refused.Cargo),
                FactionReputation = saveManager.CaptureReputation(refused.Reputation),
                TemporaryHostility = saveManager.CaptureTemporaryHostility(refused.Reputation)
            };
            if (!saveManager.TrySave(refusedData, out _) || !saveManager.TryLoad(out SaveGameData refusedLoaded, out _))
                return false;

            Context restoredRefusal = CreateContext();
            restoredRefusal.Credits.SetCredits(refusedLoaded.PlayerCredits);
            saveManager.ApplyCargo(restoredRefusal.Cargo, refusedLoaded, out _);
            saveManager.ApplyReputation(restoredRefusal.Reputation, refusedLoaded);
            saveManager.ApplyTemporaryHostility(restoredRefusal.Reputation, refusedLoaded);
            return restoredRefusal.Cargo.GetCommodityQuantity(sideArms.Name) == 1 &&
                Nearly(restoredRefusal.Reputation.GetStanding(FactionManager.LibertyPolice), 0.10f) &&
                restoredRefusal.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }
        finally
        {
            if (File.Exists(savePath))
                File.Delete(savePath);
        }
    }

    private bool AlreadyDockedPlayerRemainsSafe()
    {
        Context context = CreateContext(standing: 0.30f);
        StationDockUI dockUi = CreateDockUi(context.Reputation);
        Station station = PoliceStation();
        if (!dockUi.DockAtStation(station))
            return false;

        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        ResolveRefusal(context);
        if (!dockUi.IsDocked || !ReferenceEquals(dockUi.DockedStation, station))
            return false;

        dockUi.Undock();
        return !dockUi.IsDocked;
    }

    private bool ResetClearsEnforcementState()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        ResolveRefusal(context);
        context.Reputation.ResetToNewGame();
        context.Cargo.Clear();
        context.Credits.SetCredits(3_000);
        return Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), -0.25f) &&
            !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            DockingAccess(PoliceStation(), context.Reputation).IsAllowed &&
            context.Credits.Credits == 3_000 && context.Cargo.UsedCapacity == 0;
    }

    private void ResolveRefusal(Context context)
    {
        PoliceEnforcementOffer offer = _service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        _service.TryResolve(offer, PoliceEnforcementResolution.Refuse, context.Cargo, context.Credits, context.Reputation, out _, out _);
    }

    private static Context CreateContext(float standing = 0.00f, int credits = 10_000)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "phase 28 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "phase 28 smoke isolation setup");
        return new Context(new CargoHold(100), new PlayerCredits(credits), reputation);
    }

    private static StationDockUI CreateDockUi(ReputationManager reputation)
    {
        PlayerCredits credits = new(100_000);
        MissionManager missions = new(credits, null, reputation);
        return new StationDockUI(null, null, null, new CommodityDealer(), missions, reputation);
    }

    private static Station PoliceStation() => CreateStation("Fort Bush", FactionManager.LibertyPolice);
    private static Station RogueStation() => CreateStation("Buffalo Base", FactionManager.LibertyRogues);

    private static Station CreateStation(string name, string factionId) => new(new StationConfig
    {
        Description = name,
        FactionId = factionId,
        StartupPositionX = 2_000f,
        StartupPositionY = 0f,
        StartupPositionZ = -2_000f,
        Radius = 600f,
        DockingRange = 900f
    }, null);

    private static FactionAccessResult DockingAccess(Station station, ReputationManager reputation) =>
        FactionAccessService.EvaluateDocking(reputation, station.FactionId, station.Name);

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static TResult RunSilenced<TResult>(Func<TResult> function)
    {
        TextWriter original = Console.Out;
        try
        {
            using StringWriter writer = new();
            Console.SetOut(writer);
            return function();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private sealed record Context(CargoHold Cargo, PlayerCredits Credits, ReputationManager Reputation);
}
