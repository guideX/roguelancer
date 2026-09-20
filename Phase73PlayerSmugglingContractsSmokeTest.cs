using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Phase 73 focused coverage for intentional player smuggling contracts.
/// The harness drives only production systems: the existing mission board
/// and MissionManager acceptance authority, the authoritative CargoHold,
/// the production Police chain (ContrabandEnforcementPolicy,
/// TrafficManager detection, PoliceScanSystem scan/demand,
/// PoliceEnforcementService confiscation, PoliceFugitiveManager pursuit),
/// the physical LootManager cargo pods, MissionWorldManager docking
/// delivery, and SaveGameManager persistence. No smoke-only mission,
/// cargo, market, or police implementation exists here.
/// </summary>
internal sealed class Phase73PlayerSmugglingContractsSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(ValidateBoardOffersAreBounded, "smuggling offers are bounded board offers", ref passed, ref failed);
        RunCase(ValidateOfferUsesCanonicalContraband, "offer uses canonical contraband commodity", ref passed, ref failed);
        RunCase(ValidateOfferIdentifiesValidSource, "offer identifies a valid Rogue source", ref passed, ref failed);
        RunCase(ValidateOfferIdentifiesValidDestination, "offer identifies a valid destination", ref passed, ref failed);
        RunCase(ValidateRewardPositiveAndBounded, "reward is positive, bounded, and formula-exact", ref passed, ref failed);
        RunCase(ValidateOfferTextIdentifiesIllegalCargo, "offer text clearly identifies illegal cargo", ref passed, ref failed);
        RunCase(ValidateHostileStandingBlocksSmugglingWork, "hostile standing blocks smuggling work", ref passed, ref failed);
        RunCase(ValidateAcceptanceCreatesExactlyOneMission, "accepting creates exactly one mission", ref passed, ref failed);
        RunCase(ValidateAcceptanceLoadsExactCargo, "accepting loads the exact required cargo", ref passed, ref failed);
        RunCase(ValidateCargoUsesExistingMissionMechanism, "cargo is reserved through the existing mission mechanism", ref passed, ref failed);
        RunCase(ValidateInsufficientCapacityRejectsTransactionally, "insufficient capacity rejects acceptance transactionally", ref passed, ref failed);
        RunCase(ValidateRejectedAcceptanceCreatesNoMission, "rejected acceptance creates no mission", ref passed, ref failed);
        RunCase(ValidateRejectedAcceptanceCreatesNoCargo, "rejected acceptance creates no cargo", ref passed, ref failed);
        RunCase(ValidateMissionContrabandVisibleToPolicePolicy, "mission contraband is visible to normal Police detection", ref passed, ref failed);
        RunCase(ValidateLawfulStopInitiatesForMissionCargo, "lawful Police initiate a stop for mission cargo", ref passed, ref failed);
        RunCase(ValidateCompletedScanDetectsMissionContraband, "completed scan detects the mission contraband", ref passed, ref failed);
        RunCase(ValidateComplianceConfiscatesRealMissionCargo, "compliance confiscates real mission cargo", ref passed, ref failed);
        RunCase(ValidateConfiscationInvokesMissionConsequence, "confiscation invokes the existing mission consequence path", ref passed, ref failed);
        RunCase(ValidateNoPhantomQuantityRemains, "no phantom mission quantity remains", ref passed, ref failed);
        RunCase(ValidateRefusalUsesFugitiveSystem, "refusal uses the existing fugitive system", ref passed, ref failed);
        RunCase(ValidateJettisonCreatesPhysicalPods, "jettison creates physical cargo pods", ref passed, ref failed);
        RunCase(ValidateJettisonRecoveryKeepsContractCompletable, "jettison recovery keeps the contract completable", ref passed, ref failed);
        RunCase(ValidateWrongDestinationCannotComplete, "wrong destination cannot complete the contract", ref passed, ref failed);
        RunCase(ValidateInsufficientCargoCannotComplete, "insufficient cargo cannot complete the contract", ref passed, ref failed);
        RunCase(ValidateCorrectDestinationCompletes, "correct destination with complete cargo succeeds", ref passed, ref failed);
        RunCase(ValidateDeliveryRemovesExactCargo, "successful delivery removes the exact required cargo", ref passed, ref failed);
        RunCase(ValidateDeliveryPaysExactRewardOnce, "successful delivery pays the exact reward once", ref passed, ref failed);
        RunCase(ValidateRepeatCompletionCannotDuplicatePayout, "repeat completion cannot duplicate payout", ref passed, ref failed);
        RunCase(ValidateActiveContractSurvivesSaveLoad, "active contract survives save/load", ref passed, ref failed);
        RunCase(ValidateLoadedCargoQuantityCorrect, "loaded mission cargo quantity is correct", ref passed, ref failed);
        RunCase(ValidateLoadedReservationCorrect, "loaded reservation state is correct", ref passed, ref failed);
        RunCase(ValidateLoadedCargoPoliceDetectable, "loaded cargo remains Police-detectable contraband", ref passed, ref failed);
        RunCase(ValidateSaveLoadDuplicatesNothing, "save/load duplicates neither cargo nor mission", ref passed, ref failed);
        RunCase(ValidateCompletionAfterLoadPaysOnce, "completion after load pays once", ref passed, ref failed);
        RunCase(ValidateConfiscationAfterLoadFailsMission, "confiscation after load produces the correct mission consequence", ref passed, ref failed);
        RunCase(ValidateLegalFreightUnaffected, "legal freight remains unaffected", ref passed, ref failed);
        RunCase(ValidateLegalMissionCargoNotContraband, "legal mission cargo is not treated as contraband", ref passed, ref failed);
        RunCase(ValidateStolenContrabandDistinctionsIntact, "stolen and contraband distinctions remain intact", ref passed, ref failed);
        RunCase(ValidateResetDoesNotCorruptMissionState, "reset does not corrupt mission state", ref passed, ref failed);
        RunCase(ValidateBoardRefreshIssuesNoCargo, "board refresh issues no cargo", ref passed, ref failed);

        Console.WriteLine($"[PHASE 73 SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 73 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 73 SMOKE] FAIL {label}: {failureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 73 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidateBoardOffersAreBounded()
    {
        SmugglingContext context = CreateContext();
        List<Mission> first = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
        List<Mission> second = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
        if (first.Count != 3 || second.Count != 3)
            return Fail("expected easy, medium, and hard offers without accumulation");
        List<Mission> board = context.Manager.CreateBoardMissions(context.Origin);
        int boardSmuggling = board.Count(mission => mission?.Type == MissionType.ContrabandSmuggling);
        if (boardSmuggling != 3)
            return Fail("smuggling offers did not ride the existing mission board");
        if (context.Player.CargoHold.UsedCapacity != 0 || context.Manager.ActiveMission != null)
            return Fail("offer generation mutated cargo or mission state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateOfferUsesCanonicalContraband()
    {
        SmugglingContext context = CreateContext();
        List<Mission> offers = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
        foreach (Mission offer in offers)
        {
            Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
            if (commodity == null || !commodity.IsContraband || commodity.IsMissionCargo || commodity.VolumePerUnit <= 0)
                return Fail($"offer used non-canonical cargo '{offer.CommodityId}'");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateOfferIdentifiesValidSource()
    {
        SmugglingContext context = CreateContext();
        List<Mission> offers = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
        foreach (Mission offer in offers)
        {
            if (!string.Equals(offer.OriginStationId, Mission.BuildStationIdentity(context.Origin), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(FactionManager.NormalizeFactionId(offer.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(offer.OfferedBy))
                return Fail("offer did not identify its Rogue source station");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateOfferIdentifiesValidDestination()
    {
        SmugglingContext context = CreateContext();
        List<Mission> offers = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
        foreach (Mission offer in offers)
        {
            Station destination = context.Stations.FirstOrDefault(station =>
                string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
            if (destination == null || string.IsNullOrWhiteSpace(offer.Destination) ||
                string.Equals(offer.DestinationStationId, offer.OriginStationId, StringComparison.OrdinalIgnoreCase))
                return Fail("offer did not identify a valid reachable destination");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRewardPositiveAndBounded()
    {
        SmugglingContext context = CreateContext();
        List<Mission> offers = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
        foreach (Mission offer in offers)
        {
            Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
            Station destination = context.Stations.First(station =>
                string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
            int expected = MissionManager.CalculateSmugglingReward(commodity, offer.RequiredQuantity, context.Origin, destination, offer.Difficulty);
            if (offer.Reward != expected)
                return Fail($"reward {offer.Reward} did not match the documented calculation {expected}");
            if (offer.Reward < MissionManager.SmugglingMinimumReward || offer.Reward > MissionManager.SmugglingMaximumReward)
                return Fail($"reward {offer.Reward} escaped the documented bounds");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateOfferTextIdentifiesIllegalCargo()
    {
        SmugglingContext context = CreateContext();
        List<Mission> offers = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
        foreach (Mission offer in offers)
        {
            if (!offer.Description.Contains("Illegal cargo", StringComparison.OrdinalIgnoreCase) ||
                !offer.Description.Contains("confiscate", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(offer.GetTypeLabel(), "CONTRABAND SMUGGLING", StringComparison.Ordinal) ||
                !offer.GetObjectiveText().Contains("police seizure", StringComparison.OrdinalIgnoreCase))
                return Fail("offer text did not clearly identify illegal cargo and Police risk");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostileStandingBlocksSmugglingWork()
    {
        SmugglingContext context = CreateContext();
        context.Reputation.AdjustReputationDirect(
            FactionManager.LibertyRogues,
            -1.0f,
            ReputationChangeReason.ManualDebug);
        if (!context.Reputation.IsHostile(FactionManager.LibertyRogues))
            return Fail("could not stage hostile Rogue standing");
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).FirstOrDefault();
        if (offer == null)
            return Fail("no offer to gate");
        if (context.Manager.CanPlayerAcceptMission(offer, out string reason, context.Origin) ||
            !reason.Contains("HOSTILE", StringComparison.OrdinalIgnoreCase))
            return Fail("hostile standing did not block smuggling acceptance");

        SmugglingContext lawful = CreateContext();
        if (lawful.Manager.GenerateContrabandSmugglingMissions(lawful.Destination).Count != 0)
            return Fail("a lawful station produced criminal freight offers");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateAcceptanceCreatesExactlyOneMission()
    {
        SmugglingContext context = CreateContext();
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).First();
        if (!context.Manager.AcceptMission(offer, context.Origin))
            return Fail($"acceptance was rejected: {context.Manager.LastAcceptanceFailureReason}");
        if (!ReferenceEquals(context.Manager.ActiveMission, offer) || context.Manager.ActiveMissions.Count != 1)
            return Fail("acceptance did not create exactly one active mission");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateAcceptanceLoadsExactCargo()
    {
        SmugglingContext context = CreateContext();
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).First();
        if (!context.Manager.AcceptMission(offer, context.Origin))
            return Fail("acceptance was rejected");
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        if (context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != offer.RequiredQuantity ||
            context.Player.CargoHold.GetCommodityQuantity(commodity.Name) != offer.RequiredQuantity)
            return Fail("acceptance did not load the exact required cargo");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateCargoUsesExistingMissionMechanism()
    {
        SmugglingContext context = CreateContext();
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).First();
        if (!context.Manager.AcceptMission(offer, context.Origin))
            return Fail("acceptance was rejected");
        if (!context.Player.CargoHold.HasMissionCargo(offer.Id, offer.CommodityId, offer.RequiredQuantity))
            return Fail("mission cargo was not tracked by the existing reservation mechanism");
        if (!context.Player.CargoHold.GetMissionCargoReservations().Any(entry => entry.MissionId == offer.Id))
            return Fail("no mission reservation entry exists for the contract");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateInsufficientCapacityRejectsTransactionally()
    {
        SmugglingContext context = CreateContext();
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).First();
        Commodity contraband = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        Commodity filler = CommodityCatalog.GetById("water");
        while (context.Player.CargoHold.CanFit(contraband, offer.RequiredQuantity))
        {
            if (!context.Player.CargoHold.AddCommodity(filler, 1))
                break;
        }

        if (context.Player.CargoHold.CanFit(contraband, offer.RequiredQuantity))
            return Fail("could not stage insufficient capacity");
        int usedBefore = context.Player.CargoHold.UsedCapacity;
        bool accepted = RunSilenced(() => context.Manager.AcceptMission(offer, context.Origin));
        if (accepted || context.Player.CargoHold.UsedCapacity != usedBefore)
            return Fail("capacity rejection was not transactional");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRejectedAcceptanceCreatesNoMission()
    {
        SmugglingContext context = CreateContext();
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).First();
        Commodity contraband = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        Commodity filler = CommodityCatalog.GetById("water");
        while (context.Player.CargoHold.CanFit(contraband, offer.RequiredQuantity))
        {
            if (!context.Player.CargoHold.AddCommodity(filler, 1))
                break;
        }

        RunSilenced(() => context.Manager.AcceptMission(offer, context.Origin));
        if (context.Manager.ActiveMission != null || context.Manager.ActiveMissions.Count != 0 ||
            offer.Status != MissionStatus.Available)
            return Fail("rejected acceptance left mission state behind");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRejectedAcceptanceCreatesNoCargo()
    {
        SmugglingContext context = CreateContext();
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).First();
        Commodity contraband = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        Commodity filler = CommodityCatalog.GetById("water");
        while (context.Player.CargoHold.CanFit(contraband, offer.RequiredQuantity))
        {
            if (!context.Player.CargoHold.AddCommodity(filler, 1))
                break;
        }

        int usedBefore = context.Player.CargoHold.UsedCapacity;
        RunSilenced(() => context.Manager.AcceptMission(offer, context.Origin));
        if (context.Player.CargoHold.UsedCapacity != usedBefore ||
            context.Player.CargoHold.GetCommodityQuantity(contraband.Name) != 0 ||
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != 0)
            return Fail("rejected acceptance left cargo behind");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateMissionContrabandVisibleToPolicePolicy()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        if (!ContrabandEnforcementPolicy.HasPlayerContraband(context.Player.CargoHold))
            return Fail("mission contraband was hidden from the production detection policy");
        PoliceEnforcementOffer quote = new PoliceEnforcementService().Evaluate(
            FactionManager.LibertyPolice,
            context.Player.CargoHold,
            context.Credits);
        if (!quote.HasContraband || quote.TotalContrabandQuantity != offer.RequiredQuantity)
            return Fail("normal Police evaluation did not see the mission contraband");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateLawfulStopInitiatesForMissionCargo()
    {
        SmugglingContext context = CreateTrafficContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(context.Player);
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail($"detection did not intercept mission cargo: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateCompletedScanDetectsMissionContraband()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        PoliceScanSystem scan = new();
        StepScan(scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (scan.State != PoliceScanState.ContrabandDetected)
            return Fail("scan did not enter the contraband demand");
        if (scan.CurrentOffer?.TotalContrabandQuantity != offer.RequiredQuantity)
            return Fail("completed scan did not detect the exact mission quantity");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateComplianceConfiscatesRealMissionCargo()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        PoliceScanSystem scan = new();
        scan.SetMissionManager(context.Manager);
        StepScan(scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        int fine = scan.CurrentOffer?.FineAmount ?? 0;
        int creditsBefore = context.Credits.Credits;
        if (!scan.TryAcceptEnforcement(context.Player, context.Credits, context.Reputation))
            return Fail("compliance did not resolve the demand");
        if (context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != 0 ||
            context.Player.CargoHold.GetCommodityQuantity(commodity.Name) != 0)
            return Fail("compliance did not confiscate the real mission cargo");
        if (fine <= 0 || context.Credits.Credits != creditsBefore - fine)
            return Fail("compliance did not charge the quoted fine");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateConfiscationInvokesMissionConsequence()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        PoliceScanSystem scan = new();
        scan.SetMissionManager(context.Manager);
        StepScan(scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (!scan.TryAcceptEnforcement(context.Player, context.Credits, context.Reputation))
            return Fail("compliance did not resolve the demand");
        if (offer.Status != MissionStatus.Failed || context.Manager.ActiveMission != null)
            return Fail("confiscation did not fail the contract through the existing consequence path");
        if (string.IsNullOrWhiteSpace(offer.FailureReason) || offer.RewardPaid)
            return Fail("failed contract has no reason or stayed payable");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNoPhantomQuantityRemains()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        PoliceScanSystem scan = new();
        scan.SetMissionManager(context.Manager);
        StepScan(scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        scan.TryAcceptEnforcement(context.Player, context.Credits, context.Reputation);
        if (context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != 0 ||
            context.Player.CargoHold.GetMissionCargoReservations().Any(entry => entry.MissionId == offer.Id) ||
            context.Player.CargoHold.HasMissionCargo(offer.Id, offer.CommodityId, 1))
            return Fail("a phantom mission quantity survived confiscation");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRefusalUsesFugitiveSystem()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        PoliceScanSystem scan = new();
        scan.SetMissionManager(context.Manager);
        PoliceFugitiveManager fugitive = new(context.Reputation);
        scan.SetFugitiveManager(fugitive);
        StepScan(scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (!scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation))
            return Fail("refusal did not resolve the demand");
        if (scan.State != PoliceScanState.Enforcement || !fugitive.IsActive)
            return Fail("refusal did not enter the existing fugitive pursuit");
        if (context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != offer.RequiredQuantity || !offer.IsActive)
            return Fail("refusal destroyed cargo or the contract it should preserve");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateJettisonCreatesPhysicalPods()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        LootManager loot = new();
        bool jettisoned = loot.TryJettisonMissionCargo(
            context.Player.CargoHold,
            offer.Id,
            commodity,
            1,
            context.Player.Position + context.Player.Forward * 120f,
            context.Player.Velocity,
            out CargoPod pod);
        if (!jettisoned || pod == null || loot.ActivePods.Count != 1)
            return Fail("jettison did not create exactly one physical pod");
        if (!pod.IsMissionCargo || pod.MissionId != offer.Id)
            return Fail("jettisoned pod lost its mission attribution");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateJettisonRecoveryKeepsContractCompletable()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        LootManager loot = new();
        if (!loot.TryJettisonMissionCargo(
                context.Player.CargoHold,
                offer.Id,
                commodity,
                1,
                context.Player.Position + context.Player.Forward * 120f,
                context.Player.Velocity,
                out _))
            return Fail("jettison did not launch a pod");
        if (context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != offer.RequiredQuantity - 1)
            return Fail("jettison did not update the authoritative hold");
        if (context.World.NotifyStationDocked(context.Destination))
            return Fail("a short-loaded contract completed");

        for (int i = 0; i < 10 && loot.ActivePods.Count > 0; i++)
            StepLoot(loot, context.Player, 0.1f);
        if (loot.ActivePods.Count != 0 ||
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != offer.RequiredQuantity)
            return Fail("the physical pod was not legitimately recoverable with attribution");

        Station destination = context.Stations.First(station =>
            string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
        if (!context.World.NotifyStationDocked(destination) || offer.Status != MissionStatus.Rewarded)
            return Fail("the recovered contract could not complete");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateWrongDestinationCannotComplete()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Station wrong = context.Stations.First(station => !string.Equals(
            Mission.BuildStationIdentity(station),
            offer.DestinationStationId,
            StringComparison.OrdinalIgnoreCase));
        int creditsBefore = context.Credits.Credits;
        bool completed = context.World.NotifyStationDocked(wrong);
        if (completed || offer.Status == MissionStatus.Rewarded || offer.Status == MissionStatus.Completed)
            return Fail("the wrong destination completed the contract");
        if (!offer.IsActive || context.Credits.Credits != creditsBefore ||
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != offer.RequiredQuantity)
            return Fail("the wrong-destination attempt disturbed the live contract");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateInsufficientCargoCannotComplete()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        LootManager loot = new();
        if (!loot.TryJettisonMissionCargo(
                context.Player.CargoHold,
                offer.Id,
                commodity,
                1,
                context.Player.Position + context.Player.Forward * 120f,
                context.Player.Velocity,
                out _))
            return Fail("could not stage short cargo");
        Station destination = context.Stations.First(station =>
            string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
        int creditsBefore = context.Credits.Credits;
        if (context.World.NotifyStationDocked(destination) ||
            offer.Status == MissionStatus.Rewarded ||
            context.Credits.Credits != creditsBefore)
            return Fail("partial cargo completed a full-quantity contract");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateCorrectDestinationCompletes()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Station destination = context.Stations.First(station =>
            string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
        if (!context.World.NotifyStationDocked(destination) || offer.Status != MissionStatus.Rewarded)
            return Fail("destination docking did not complete the contract");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateDeliveryRemovesExactCargo()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        Station destination = context.Stations.First(station =>
            string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
        context.World.NotifyStationDocked(destination);
        if (context.Player.CargoHold.GetCommodityQuantity(commodity.Name) != 0 ||
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != 0 ||
            context.Player.CargoHold.HasMissionCargo(offer.Id, offer.CommodityId, 1))
            return Fail("delivery did not remove the exact required cargo");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateDeliveryPaysExactRewardOnce()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        int creditsBefore = context.Credits.Credits;
        Station destination = context.Stations.First(station =>
            string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
        context.World.NotifyStationDocked(destination);
        if (!offer.RewardPaid || context.Credits.Credits != creditsBefore + offer.Reward)
            return Fail("delivery did not pay the exact reward once");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRepeatCompletionCannotDuplicatePayout()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Station destination = context.Stations.First(station =>
            string.Equals(Mission.BuildStationIdentity(station), offer.DestinationStationId, StringComparison.OrdinalIgnoreCase));
        context.World.NotifyStationDocked(destination);
        int afterFirst = context.Credits.Credits;
        bool second = context.World.NotifyStationDocked(destination);
        if (second || context.Credits.Credits != afterFirst)
            return Fail("repeat docking duplicated the payout");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateActiveContractSurvivesSaveLoad()
    {
        (SmugglingContext source, SaveGameData data, string path) = BuildSavedContext();
        try
        {
            SmugglingContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            Mission original = source.Manager.ActiveMission;
            if (mission?.Type != MissionType.ContrabandSmuggling || original == null)
                return Fail("active smuggling contract did not survive save/load");
            if (!string.Equals(mission.CommodityId, original.CommodityId, StringComparison.OrdinalIgnoreCase) ||
                mission.RequiredQuantity != original.RequiredQuantity ||
                !string.Equals(mission.DestinationStationId, original.DestinationStationId, StringComparison.OrdinalIgnoreCase) ||
                mission.Reward != original.Reward || !mission.IsActive)
                return Fail("loaded contract identity, quantity, destination, or reward changed");
            return Pass();
        }
        finally { Cleanup(path); }
    }

    private (bool Success, string FailureReason) ValidateLoadedCargoQuantityCorrect()
    {
        (SmugglingContext source, SaveGameData data, string path) = BuildSavedContext();
        try
        {
            SmugglingContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            if (mission == null || commodity == null)
                return Fail("loaded mission was unavailable");
            if (resumed.Player.CargoHold.GetMissionCargoQuantity(mission.Id) != mission.RequiredQuantity ||
                resumed.Player.CargoHold.GetCommodityQuantity(commodity.Name) != mission.RequiredQuantity)
                return Fail("loaded mission cargo quantity is wrong");
            return Pass();
        }
        finally { Cleanup(path); }
    }

    private (bool Success, string FailureReason) ValidateLoadedReservationCorrect()
    {
        (SmugglingContext source, SaveGameData data, string path) = BuildSavedContext();
        try
        {
            SmugglingContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            if (mission == null ||
                !resumed.Player.CargoHold.HasMissionCargo(mission.Id, mission.CommodityId, mission.RequiredQuantity) ||
                !resumed.Player.CargoHold.GetMissionCargoReservations().Any(entry => entry.MissionId == mission.Id))
                return Fail("loaded reservation state is wrong");
            return Pass();
        }
        finally { Cleanup(path); }
    }

    private (bool Success, string FailureReason) ValidateLoadedCargoPoliceDetectable()
    {
        (SmugglingContext source, SaveGameData data, string path) = BuildSavedContext();
        try
        {
            SmugglingContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            if (mission == null || !ContrabandEnforcementPolicy.HasPlayerContraband(resumed.Player.CargoHold))
                return Fail("loaded mission cargo is invisible to Police policy");
            PoliceScanSystem scan = new();
            StepScan(scan, resumed, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
            if (scan.State != PoliceScanState.ContrabandDetected ||
                scan.CurrentOffer?.TotalContrabandQuantity != mission.RequiredQuantity)
                return Fail("a lawful scan did not detect the loaded mission cargo");
            return Pass();
        }
        finally { Cleanup(path); }
    }

    private (bool Success, string FailureReason) ValidateSaveLoadDuplicatesNothing()
    {
        (SmugglingContext source, SaveGameData data, string path) = BuildSavedContext();
        try
        {
            SmugglingContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            if (mission == null || commodity == null)
                return Fail("loaded mission was unavailable");
            if (resumed.Manager.ActiveMissions.Count != 1)
                return Fail("save/load duplicated the mission");
            if (resumed.Player.CargoHold.GetCommodityQuantity(commodity.Name) != mission.RequiredQuantity)
                return Fail("save/load duplicated the cargo");
            return Pass();
        }
        finally { Cleanup(path); }
    }

    private (bool Success, string FailureReason) ValidateCompletionAfterLoadPaysOnce()
    {
        (SmugglingContext source, SaveGameData data, string path) = BuildSavedContext();
        try
        {
            SmugglingContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            resumed.Credits.SetCredits(data.PlayerCredits);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            Mission mission = resumed.Manager.ActiveMission;
            if (mission == null)
                return Fail("loaded mission was unavailable");
            Station destination = resumed.Stations.First(station =>
                string.Equals(Mission.BuildStationIdentity(station), mission.DestinationStationId, StringComparison.OrdinalIgnoreCase));
            int creditsBefore = resumed.Credits.Credits;
            if (!resumed.World.NotifyStationDocked(destination) || mission.Status != MissionStatus.Rewarded)
                return Fail("completion after load did not succeed");
            if (!mission.RewardPaid || resumed.Credits.Credits != creditsBefore + mission.Reward)
                return Fail("completion after load did not pay exactly once");
            resumed.World.NotifyStationDocked(destination);
            if (resumed.Credits.Credits != creditsBefore + mission.Reward)
                return Fail("repeat docking after load duplicated the payout");
            return Pass();
        }
        finally { Cleanup(path); }
    }

    private (bool Success, string FailureReason) ValidateConfiscationAfterLoadFailsMission()
    {
        (SmugglingContext source, SaveGameData data, string path) = BuildSavedContext();
        try
        {
            SmugglingContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            resumed.Credits.SetCredits(data.PlayerCredits);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            Mission mission = resumed.Manager.ActiveMission;
            if (mission == null)
                return Fail("loaded mission was unavailable");
            PoliceScanSystem scan = new();
            scan.SetMissionManager(resumed.Manager);
            StepScan(scan, resumed, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
            if (!scan.TryAcceptEnforcement(resumed.Player, resumed.Credits, resumed.Reputation))
                return Fail("post-load compliance did not resolve");
            if (mission.Status != MissionStatus.Failed || resumed.Manager.ActiveMission != null)
                return Fail("post-load confiscation did not fail the contract");
            if (resumed.Player.CargoHold.GetMissionCargoReservations().Any(entry => entry.MissionId == mission.Id))
                return Fail("a stale reservation survived post-load confiscation");
            Station destination = resumed.Stations.First(station =>
                string.Equals(Mission.BuildStationIdentity(station), mission.DestinationStationId, StringComparison.OrdinalIgnoreCase));
            int creditsBefore = resumed.Credits.Credits;
            resumed.World.NotifyStationDocked(destination);
            if (mission.Status == MissionStatus.Rewarded || resumed.Credits.Credits != creditsBefore)
                return Fail("delivery succeeded without legitimately restored cargo");
            return Pass();
        }
        finally { Cleanup(path); }
    }

    private (bool Success, string FailureReason) ValidateLegalFreightUnaffected()
    {
        SmugglingContext context = CreateContext();
        Commodity legal = CommodityCatalog.GetById("water");
        if (legal == null || !context.Player.CargoHold.AddCommodity(legal, 3))
            return Fail("could not stage legal cargo");
        if (ContrabandEnforcementPolicy.HasPlayerContraband(context.Player.CargoHold))
            return Fail("legal cargo was flagged as contraband");
        PoliceScanSystem scan = new();
        StepScan(scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (scan.State != PoliceScanState.Cleared || context.Credits.Credits != 20_000)
            return Fail("a clean scan of legal cargo did not clear");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateLegalMissionCargoNotContraband()
    {
        SmugglingContext context = CreateContext();
        Commodity legal = CommodityCatalog.GetById("food-rations");
        if (legal == null || !context.Player.CargoHold.AddMissionCargo(9001, legal, 2))
            return Fail("could not stage legal mission cargo");
        PoliceEnforcementOffer quote = new PoliceEnforcementService().Evaluate(
            FactionManager.LibertyPolice,
            context.Player.CargoHold,
            context.Credits);
        if (quote.HasContraband)
            return Fail("legal mission cargo was treated as contraband merely because it is reserved");
        PoliceScanSystem scan = new();
        StepScan(scan, context, CreateScanner(new Vector3(1_500f, 0f, 0f)), 0.5f, 8);
        if (scan.State != PoliceScanState.Cleared)
            return Fail("a scan of legal mission cargo did not clear");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateStolenContrabandDistinctionsIntact()
    {
        SmugglingContext mixed = CreateContext();
        Commodity stolenLegal = CommodityCatalog.GetById("water");
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (stolenLegal == null || contraband == null ||
            !mixed.Player.CargoHold.AddStolenCommodity(stolenLegal, 1) ||
            !mixed.Player.CargoHold.AddCommodity(contraband, 1))
            return Fail("could not stage mixed violation cargo");
        PoliceEnforcementOffer mixedQuote = new PoliceEnforcementService().Evaluate(
            FactionManager.LibertyPolice,
            mixed.Player.CargoHold,
            mixed.Credits);
        if (!mixedQuote.HasContraband || !mixedQuote.HasStolenGoods)
            return Fail("mixed stolen/contraband cargo lost one violation type");

        SmugglingContext stolenOnly = CreateContext();
        if (!stolenOnly.Player.CargoHold.AddStolenCommodity(stolenLegal, 1))
            return Fail("could not stage stolen-only cargo");
        if (ContrabandEnforcementPolicy.HasPlayerContraband(stolenOnly.Player.CargoHold))
            return Fail("stolen legal cargo was classified as contraband");
        PoliceEnforcementOffer stolenQuote = new PoliceEnforcementService().Evaluate(
            FactionManager.LibertyPolice,
            stolenOnly.Player.CargoHold,
            stolenOnly.Credits);
        if (!stolenQuote.HasStolenGoods || stolenQuote.HasContraband)
            return Fail("stolen-only cargo did not keep its distinct violation type");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateResetDoesNotCorruptMissionState()
    {
        SmugglingContext context = CreateContext();
        Mission offer = AcceptFirstOffer(context);
        if (offer == null)
            return Fail("acceptance was rejected");
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer.CommodityId);
        int quantity = offer.RequiredQuantity;
        RunSilenced(() => { context.Manager.ClearState(); return true; });
        if (context.Manager.ActiveMission != null || context.Manager.ActiveMissions.Count != 0)
            return Fail("reset left an active mission behind");
        if (context.Player.CargoHold.GetMissionCargoReservations().Count != 0 ||
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != 0)
            return Fail("reset left stale mission attribution behind");
        if (context.Player.CargoHold.GetCommodityQuantity(commodity.Name) != quantity)
            return Fail("reset destroyed authoritative cargo instead of releasing attribution");
        if (context.Manager.GenerateContrabandSmugglingMissions(context.Origin).Count != 3)
            return Fail("the mission board did not survive the reset");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateBoardRefreshIssuesNoCargo()
    {
        SmugglingContext context = CreateContext();
        Mission first = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).FirstOrDefault();
        Mission second = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).FirstOrDefault();
        if (first == null || second == null || context.Manager.ActiveMission != null)
            return Fail("board refresh was unavailable");
        if (context.Player.CargoHold.UsedCapacity != 0)
            return Fail("board refresh issued cargo before acceptance");
        return Pass();
    }

    private static Mission AcceptFirstOffer(SmugglingContext context)
    {
        Mission offer = context.Manager.GenerateContrabandSmugglingMissions(context.Origin).First();
        return context.Manager.AcceptMission(offer, context.Origin) ? offer : null;
    }

    private static (SmugglingContext Source, SaveGameData Data, string Path) BuildSavedContext()
    {
        SmugglingContext source = CreateContext();
        if (AcceptFirstOffer(source) == null)
            throw new InvalidOperationException("could not accept smuggling contract for save fixture");

        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-phase73-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "phase73.json");
        SaveGameManager saver = new(path);
        SaveGameData data = new()
        {
            PlayerCredits = source.Credits.Credits,
            Cargo = saver.CaptureCargo(source.Player.CargoHold),
            ActiveMissions = saver.CaptureMissions(source.Manager.ActiveMissions)
        };
        if (!saver.TrySave(data, out string saveFailure))
            throw new InvalidOperationException(saveFailure);
        if (!saver.TryLoad(out data, out string loadFailure) || data == null)
            throw new InvalidOperationException(loadFailure);
        return (source, data, path);
    }

    private static void Cleanup(string path)
    {
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch { }
    }

    private static SmugglingContext CreateContext()
    {
        Ship player = new(Vector3.Zero);
        PlayerCredits credits = new(20_000);
        ReputationManager reputation = new(new FactionManager());
        MissionManager manager = new(credits, null, reputation, null, player.CargoHold);
        MissionWaypointSystem waypoints = new();
        Station origin = CreateStation("Rogue Haven", FactionManager.LibertyRogues, new Vector3(-30_000f, 0f, 0f));
        Station destination = CreateStation("Fort Bush", FactionManager.LibertyCorporations, new Vector3(42_000f, 0f, 0f));
        Station third = CreateStation("Trenton Outpost", FactionManager.LibertyCorporations, new Vector3(0f, 20_000f, 0f));
        List<Station> stations = new() { origin, destination, third };
        List<NpcShip> npcs = new();
        List<SpaceObject> objects = stations.Cast<SpaceObject>().ToList();
        MissionWorldManager world = new(manager, waypoints, player, npcs, objects, () => stations);
        manager.SetWaypointSystem(waypoints);
        manager.SetWorldManager(world);
        return new SmugglingContext(player, credits, reputation, manager, world, origin, destination, stations, npcs, objects);
    }

    private static SmugglingContext CreateTrafficContext()
    {
        SmugglingContext context = CreateContext();
        context.Traffic = new TrafficManager(new ConfigurationManager(), context.Npcs, context.Objects);
        context.Traffic.ConfigureContrabandEnforcement(
            _ => NpcCargoManifestSnapshot.NoRegisteredCargo(),
            () => context.Loot.ActivePods,
            (enforcer, pod) => context.Loot.TrySeizeContrabandPodForNpc(enforcer, pod));
        context.Fugitive = new PoliceFugitiveManager(context.Reputation);
        context.Traffic.FugitiveManager = context.Fugitive;
        context.Scan.SetFugitiveManager(context.Fugitive);
        return context;
    }

    private static Station CreateStation(string name, string factionId, Vector3 position)
    {
        return new Station(new StationConfig
        {
            Description = name,
            FactionId = factionId,
            SystemIndex = 1,
            StartupPositionX = position.X,
            StartupPositionY = position.Y,
            StartupPositionZ = position.Z,
            Radius = 200f,
            DockingRange = 500f
        }, null);
    }

    private static NpcShip CreateScanner(Vector3 position, string name = "Phase 73 Police Scan")
    {
        NpcShip scanner = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice)
        {
            Position = position,
            Velocity = Vector3.Zero
        };
        return scanner;
    }

    private static void StepScan(
        PoliceScanSystem scan,
        SmugglingContext context,
        NpcShip scanner,
        float seconds,
        int frameCount)
    {
        List<NpcShip> scanners = new() { scanner };
        TimeSpan total = TimeSpan.Zero;
        for (int i = 0; i < frameCount; i++)
        {
            TimeSpan previous = total;
            total += TimeSpan.FromSeconds(seconds);
            scan.Update(new GameTime(previous, TimeSpan.FromSeconds(seconds)), context.Player, scanners, context.Credits, context.Reputation);
        }
    }

    private static void StepLoot(LootManager loot, Ship player, float seconds)
    {
        loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)), player, false);
    }

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            return action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);

    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private sealed class SmugglingContext
    {
        public Ship Player { get; }
        public PlayerCredits Credits { get; }
        public ReputationManager Reputation { get; }
        public MissionManager Manager { get; }
        public MissionWorldManager World { get; }
        public Station Origin { get; }
        public Station Destination { get; }
        public List<Station> Stations { get; }
        public List<NpcShip> Npcs { get; }
        public List<SpaceObject> Objects { get; }
        public LootManager Loot { get; } = new();
        public PoliceScanSystem Scan { get; } = new();
        public PoliceFugitiveManager Fugitive { get; set; }
        public TrafficManager Traffic { get; set; }
        public PoliceContrabandStopCoordinator Coordinator { get; } = new();
        public List<string> Log { get; } = new();

        public SmugglingContext(
            Ship player,
            PlayerCredits credits,
            ReputationManager reputation,
            MissionManager manager,
            MissionWorldManager world,
            Station origin,
            Station destination,
            List<Station> stations,
            List<NpcShip> npcs,
            List<SpaceObject> objects)
        {
            Player = player;
            Credits = credits;
            Reputation = reputation;
            Manager = manager;
            World = world;
            Origin = origin;
            Destination = destination;
            Stations = stations;
            Npcs = npcs;
            Objects = objects;
        }

        public NpcShip AddPolice(Vector3 position, string name = "Phase 73 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase73-police",
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

        public void StepFull(Ship playerShip, float seconds = 0.1f)
        {
            Traffic.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                playerShip,
                Reputation,
                Log.Add);
            GameTime frame = new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
            foreach (NpcShip npc in Npcs.ToList())
                npc?.Update(frame, null, playerShip, Reputation);
            Scan.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                playerShip,
                Npcs,
                Credits,
                Reputation,
                notificationManager: null,
                log: Log.Add);
            Coordinator.Update(playerShip, Npcs, Traffic, Scan, Fugitive, Reputation, Log.Add);
        }
    }
}
