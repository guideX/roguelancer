using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 16 proof of the two-sided surplus -> export -> delivery
/// loop. The fixture uses production market, cargo, mission, world, and save
/// authorities rather than a parallel economy.
/// </summary>
internal sealed class ExportContractSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase(ValidateBaselineNoExport, "baseline has no export", ref passed, ref failed);
        RunCase(ValidateSmallSurplusNoExport, "small surplus suppressed", ref passed, ref failed);
        RunCase(ValidateSurplusOfferAppears, "surplus offer appears", ref passed, ref failed);
        RunCase(ValidateMissionOnlyCommodityExcluded, "mission-only commodity excluded", ref passed, ref failed);
        RunCase(ValidateInvalidCommodityExcluded, "invalid commodity excluded", ref passed, ref failed);
        RunCase(ValidateQuantityPositive, "quantity positive", ref passed, ref failed);
        RunCase(ValidateQuantityBounded, "quantity bounded", ref passed, ref failed);
        RunCase(ValidateQuantityDoesNotExceedSurplus, "quantity does not exceed surplus", ref passed, ref failed);
        RunCase(ValidateOriginFloorPreserved, "origin baseline floor preserved", ref passed, ref failed);
        RunCase(ValidateDestinationDiffers, "destination differs", ref passed, ref failed);
        RunCase(ValidateDestinationEligible, "destination is eligible", ref passed, ref failed);
        RunCase(ValidateStrongerDemandPreferred, "stronger demand preferred", ref passed, ref failed);
        RunCase(ValidateNoUsefulDestinationSuppressesOffer, "no useful destination suppresses", ref passed, ref failed);
        RunCase(ValidateDeterministicGeneration, "deterministic generation", ref passed, ref failed);
        RunCase(ValidateDuplicateSuppression, "duplicate suppression", ref passed, ref failed);
        RunCase(ValidateAcceptedTermsStable, "accepted terms stable", ref passed, ref failed);
        RunCase(ValidateInsufficientCapacityRejects, "insufficient capacity rejects", ref passed, ref failed);
        RunCase(ValidateFailedAcceptanceAtomic, "failed acceptance atomic", ref passed, ref failed);
        RunCase(ValidateAcceptanceRemovesOriginStock, "acceptance removes origin stock", ref passed, ref failed);
        RunCase(ValidateAcceptanceChangesOriginPrice, "acceptance changes origin price", ref passed, ref failed);
        RunCase(ValidateAcceptanceAddsCargo, "acceptance adds cargo", ref passed, ref failed);
        RunCase(ValidateExactQuantityReserved, "exact quantity reserved", ref passed, ref failed);
        RunCase(ValidatePreExistingOrdinaryCargoUnreserved, "pre-existing cargo unreserved", ref passed, ref failed);
        RunCase(ValidateReservedCargoCannotSell, "reserved cargo cannot sell", ref passed, ref failed);
        RunCase(ValidateOrdinaryCargoRemainsSellable, "ordinary cargo remains sellable", ref passed, ref failed);
        RunCase(ValidateWrongDestinationCannotComplete, "wrong destination cannot complete", ref passed, ref failed);
        RunCase(ValidateMissingReservedCargoCannotComplete, "missing reserved cargo cannot complete", ref passed, ref failed);
        RunCase(ValidateDeliveryRemovesCargo, "delivery removes cargo", ref passed, ref failed);
        RunCase(ValidateDeliveryAddsDestinationStock, "delivery adds destination stock", ref passed, ref failed);
        RunCase(ValidateDeliveryChangesDestinationPrice, "delivery changes destination price", ref passed, ref failed);
        RunCase(ValidateRewardGrantedOnce, "reward granted once", ref passed, ref failed);
        RunCase(ValidateMissionCompletesOnce, "mission completes once", ref passed, ref failed);
        RunCase(ValidateRepeatedCompletionCannotPay, "repeated completion cannot pay", ref passed, ref failed);
        RunCase(ValidateCancellationRemovesCargo, "cancellation removes cargo", ref passed, ref failed);
        RunCase(ValidateCancellationRestoresOriginStock, "cancellation restores origin stock", ref passed, ref failed);
        RunCase(ValidateCancellationRestoresOriginPrice, "cancellation restores origin price", ref passed, ref failed);
        RunCase(ValidateCancellationPaysNothing, "cancellation pays nothing", ref passed, ref failed);
        RunCase(ValidateSaveLoadPreservesMission, "save/load preserves active export", ref passed, ref failed);
        RunCase(ValidateSaveLoadPreservesReservation, "save/load preserves reservation", ref passed, ref failed);
        RunCase(ValidateSaveLoadPreservesOriginMarket, "save/load preserves origin market", ref passed, ref failed);
        RunCase(ValidatePostLoadDelivery, "post-load delivery works", ref passed, ref failed);
        RunCase(ValidateFreightRegression, "Phase 15 freight remains functional", ref passed, ref failed);
        RunCase(ValidateCourierRegression, "courier remains functional", ref passed, ref failed);
        RunCase(ValidateSealedPackageExcluded, "sealed package excluded", ref passed, ref failed);
        RunCase(ValidateCargoTransferPreservesExport, "ship transfer preserves export cargo", ref passed, ref failed);
        RunCase(ValidateInsufficientShipCapacityRejects, "ship capacity rejection preserved", ref passed, ref failed);
        RunCase(ValidateUnrelatedMarketUnchanged, "unrelated market unchanged", ref passed, ref failed);
        RunCase(ValidateBoardRefreshDoesNotIssueCargo, "board refresh does not issue cargo", ref passed, ref failed);
        RunCase(ValidateFortBushNewarkPairing, "Fort Bush/Newark pairing", ref passed, ref failed);
        RunCase(ValidateFailurePathsAtomic, "failure paths atomic", ref passed, ref failed);

        RunCase(ValidateNormalOpportunitiesQuiet, "opportunities normal markets quiet", ref passed, ref failed);
        RunCase(ValidateShortageOpportunity, "shortage opportunity", ref passed, ref failed);
        RunCase(ValidateSurplusOpportunity, "surplus opportunity", ref passed, ref failed);
        RunCase(ValidateOpportunityRanking, "opportunity ranking", ref passed, ref failed);
        RunCase(ValidateOpportunityDeterministic, "opportunity ranking deterministic", ref passed, ref failed);
        RunCase(ValidateOpportunityBounded, "opportunity list bounded", ref passed, ref failed);
        RunCase(ValidateOpportunityExcludesInvalid, "opportunities exclude invalid cargo", ref passed, ref failed);
        RunCase(ValidateOpportunityPairing, "opportunity pairing visible", ref passed, ref failed);
        RunCase(ValidateOpportunityRefreshes, "opportunities refresh after market change", ref passed, ref failed);
        RunCase(ValidateOpportunityReadDoesNotMutate, "opportunity read does not mutate stock", ref passed, ref failed);

        string representative = RunSilenced(BuildRepresentativeReport);
        Console.WriteLine($"[EXPORT SMOKE] REPRESENTATIVE: {representative}");
        Console.WriteLine($"[EXPORT SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[EXPORT SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[EXPORT SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[EXPORT SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool Success, string FailureReason) ValidateBaselineNoExport()
    {
        ExportContext context = CreateContext();
        return FindExport(context) == null ? Pass() : Fail("baseline stock generated an export");
    }

    private static (bool Success, string FailureReason) ValidateSmallSurplusNoExport()
    {
        ExportContext context = CreateContext();
        if (!StageOriginSurplus(context, 100)) return Fail("could not stage small surplus");
        return FindExport(context) == null ? Pass() : Fail("small surplus generated an export");
    }

    private static (bool Success, string FailureReason) ValidateSurplusOfferAppears()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        return offer != null && offer.Type == MissionType.ExportContract ? Pass() : Fail("surplus offer missing");
    }

    private static (bool Success, string FailureReason) ValidateMissionOnlyCommodityExcluded()
    {
        ExportContext context = CreatePairedContext();
        return context.Manager.GenerateJobBoardMissions(10, context.Origin.FactionId, context.Origin)
            .All(mission => mission.Type != MissionType.ExportContract || mission.CommodityId != "sealed-data-package")
            ? Pass() : Fail("sealed data appeared as export cargo");
    }

    private static (bool Success, string FailureReason) ValidateInvalidCommodityExcluded()
    {
        ExportContext context = CreatePairedContext();
        return context.Manager.GenerateJobBoardMissions(10, context.Origin.FactionId, context.Origin)
            .Where(mission => mission.Type == MissionType.ExportContract)
            .All(mission => CommodityCatalog.GetByIdOrName(mission.CommodityId) != null)
            ? Pass() : Fail("invalid export commodity appeared");
    }

    private static (bool Success, string FailureReason) ValidateQuantityPositive()
    {
        Mission offer = FindExport(CreatePairedContext());
        return offer != null && offer.RequiredQuantity > 0 ? Pass() : Fail("quantity was not positive");
    }

    private static (bool Success, string FailureReason) ValidateQuantityBounded()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer?.CommodityId);
        return offer != null && commodity != null && offer.RequiredQuantity <= MissionManager.ExportMaximumUnits &&
            (long)offer.RequiredQuantity * commodity.VolumePerUnit <= MissionManager.ExportMaximumCargoVolume
            ? Pass() : Fail("export quantity exceeded gameplay bounds");
    }

    private static (bool Success, string FailureReason) ValidateQuantityDoesNotExceedSurplus()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        StationMarketListing listing = context.Market.GetListingForCommodity(context.Origin, context.Food);
        long surplus = listing.Stock - listing.BaselineStock;
        return offer != null && offer.RequiredQuantity <= surplus ? Pass() : Fail("quantity exceeded real surplus");
    }

    private static (bool Success, string FailureReason) ValidateOriginFloorPreserved()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        StationMarketListing before = context.Market.GetListingForCommodity(context.Origin, context.Food);
        return offer != null && before.Stock - offer.RequiredQuantity >= before.BaselineStock
            ? Pass() : Fail("contract could pull origin below baseline");
    }

    private static (bool Success, string FailureReason) ValidateDestinationDiffers()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        return offer != null && !string.Equals(offer.OriginStationId, offer.DestinationStationId, StringComparison.OrdinalIgnoreCase)
            ? Pass() : Fail("origin and destination matched");
    }

    private static (bool Success, string FailureReason) ValidateDestinationEligible()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        StationMarketListing destination = offer == null ? null : context.Market.GetListingForCommodity(context.Newark, context.Food);
        return offer != null && destination != null && destination.Stock < destination.BaselineStock && destination.IsAvailable
            ? Pass() : Fail("destination was not an eligible shortage market");
    }

    private static (bool Success, string FailureReason) ValidateStrongerDemandPreferred()
    {
        ExportContext context = CreatePairedContext();
        if (!CreateShortage(context, context.Rochester, 320)) return Fail("could not stage stronger Rochester demand");
        Mission offer = FindExport(context);
        return offer != null && string.Equals(offer.DestinationStationId, Mission.BuildStationIdentity(context.Rochester), StringComparison.OrdinalIgnoreCase)
            ? Pass() : Fail("stronger shortage destination was not selected");
    }

    private static (bool Success, string FailureReason) ValidateNoUsefulDestinationSuppressesOffer()
    {
        ExportContext context = CreateContext();
        return StageOriginSurplus(context, 300) && FindExport(context) == null
            ? Pass() : Fail("export was generated without a useful destination");
    }

    private static (bool Success, string FailureReason) ValidateDeterministicGeneration()
    {
        Mission first = FindExport(CreatePairedContext());
        Mission second = FindExport(CreatePairedContext());
        return first != null && second != null && first.CommodityId == second.CommodityId &&
            first.RequiredQuantity == second.RequiredQuantity && first.Reward == second.Reward &&
            first.DestinationStationId == second.DestinationStationId ? Pass() : Fail("export terms were not deterministic");
    }

    private static (bool Success, string FailureReason) ValidateDuplicateSuppression()
    {
        ExportContext context = CreatePairedContext();
        Mission first = FindExport(context);
        Mission second = FindExport(context);
        return first != null && ReferenceEquals(first, second) && first.Id == second.Id ? Pass() : Fail("board refresh duplicated export offer");
    }

    private static (bool Success, string FailureReason) ValidateAcceptedTermsStable()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        int quantity = offer.RequiredQuantity;
        int reward = offer.Reward;
        context.Market.AdvanceTime(3_600);
        return offer.RequiredQuantity == quantity && offer.Reward == reward && offer.Status == MissionStatus.InProgress
            ? Pass() : Fail("accepted export terms changed");
    }

    private static (bool Success, string FailureReason) ValidateInsufficientCapacityRejects()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        StationMarketListing before = context.Market.GetListingForCommodity(context.Origin, context.Food);
        context.Player.CargoHold.SetMaxCapacity(offer.RequiredQuantity * context.Food.VolumePerUnit - 1);
        bool accepted = offer != null && context.Manager.AcceptMission(offer, context.Origin);
        StationMarketListing after = context.Market.GetListingForCommodity(context.Origin, context.Food);
        return !accepted && context.Manager.ActiveMission == null && context.Player.CargoHold.UsedCapacity == 0 &&
            before.Stock == after.Stock ? Pass() : Fail("insufficient capacity changed state");
    }

    private static (bool Success, string FailureReason) ValidateFailedAcceptanceAtomic()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        StationMarketListing before = context.Market.GetListingForCommodity(context.Origin, context.Food);
        int credits = context.PlayerCredits.Credits;
        context.Player.CargoHold.SetMaxCapacity(0);
        bool accepted = offer != null && context.Manager.AcceptMission(offer, context.Origin);
        StationMarketListing after = context.Market.GetListingForCommodity(context.Origin, context.Food);
        return !accepted && context.PlayerCredits.Credits == credits && before.Stock == after.Stock && offer.Status == MissionStatus.Available
            ? Pass() : Fail("failed acceptance was not atomic");
    }

    private static (bool Success, string FailureReason) ValidateAcceptanceRemovesOriginStock()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        int before = context.Market.GetListingForCommodity(context.Origin, context.Food).Stock;
        return offer != null && context.Manager.AcceptMission(offer, context.Origin) &&
            context.Market.GetListingForCommodity(context.Origin, context.Food).Stock == before - offer.RequiredQuantity
            ? Pass() : Fail("origin stock did not decrease exactly");
    }

    private static (bool Success, string FailureReason) ValidateAcceptanceChangesOriginPrice()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        StationMarketListing before = context.Market.GetListingForCommodity(context.Origin, context.Food);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        StationMarketListing after = context.Market.GetListingForCommodity(context.Origin, context.Food);
        return after.BuyPrice > before.BuyPrice && after.SellPrice > before.SellPrice ? Pass() : Fail("origin prices did not respond");
    }

    private static (bool Success, string FailureReason) ValidateAcceptanceAddsCargo()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        return offer != null && context.Manager.AcceptMission(offer, context.Origin) &&
            context.Player.CargoHold.GetCommodityQuantity(context.Food.Name) == offer.RequiredQuantity
            ? Pass() : Fail("issued cargo was not added");
    }

    private static (bool Success, string FailureReason) ValidateExactQuantityReserved()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        return offer != null && context.Manager.AcceptMission(offer, context.Origin) &&
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == offer.RequiredQuantity &&
            context.Player.CargoHold.GetMissionReservedQuantity(context.Food.Name) == offer.RequiredQuantity
            ? Pass() : Fail("issued quantity was not reserved exactly");
    }

    private static (bool Success, string FailureReason) ValidatePreExistingOrdinaryCargoUnreserved()
    {
        ExportContext context = CreatePairedContext();
        if (!context.Player.CargoHold.AddCommodity(context.Food, 10)) return Fail("could not stage ordinary cargo");
        Mission offer = FindExport(context);
        return offer != null && context.Manager.AcceptMission(offer, context.Origin) &&
            context.Player.CargoHold.GetCommodityQuantity(context.Food.Name) == offer.RequiredQuantity + 10 &&
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == offer.RequiredQuantity &&
            context.Player.CargoHold.GetSellableCommodityQuantity(context.Food.Name) == 10
            ? Pass() : Fail("ordinary cargo was over-reserved");
    }

    private static (bool Success, string FailureReason) ValidateReservedCargoCannotSell()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        bool sold = context.Market.TrySell(context.Origin, context.Food, offer.RequiredQuantity, context.PlayerCredits, context.Player.CargoHold, out _);
        return !sold && context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == offer.RequiredQuantity ? Pass() : Fail("reserved cargo was sold");
    }

    private static (bool Success, string FailureReason) ValidateOrdinaryCargoRemainsSellable()
    {
        ExportContext context = CreatePairedContext();
        if (!context.Player.CargoHold.AddCommodity(context.Food, 10)) return Fail("could not stage ordinary cargo");
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        bool sold = context.Market.TrySell(context.Origin, context.Food, 10, context.PlayerCredits, context.Player.CargoHold, out _);
        return sold && context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == offer.RequiredQuantity ? Pass() : Fail("ordinary cargo was not sellable");
    }

    private static (bool Success, string FailureReason) ValidateWrongDestinationCannotComplete()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.World.NotifyStationDocked(context.Rochester);
        return context.Manager.ActiveMission == offer && offer.Status == MissionStatus.InProgress ? Pass() : Fail("wrong destination completed export");
    }

    private static (bool Success, string FailureReason) ValidateMissingReservedCargoCannotComplete()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.Player.CargoHold.RemoveMissionCargo(offer.Id, context.Food, offer.RequiredQuantity);
        context.World.NotifyStationDocked(context.Newark);
        return context.Manager.ActiveMission == offer && !offer.ObjectiveComplete ? Pass() : Fail("missing cargo completed export");
    }

    private static (bool Success, string FailureReason) ValidateDeliveryRemovesCargo()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.World.NotifyStationDocked(context.Newark);
        return context.Player.CargoHold.GetCommodityQuantity(context.Food.Name) == 0 ? Pass() : Fail("delivery left cargo behind");
    }

    private static (bool Success, string FailureReason) ValidateDeliveryAddsDestinationStock()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        int before = context.Market.GetListingForCommodity(context.Newark, context.Food).Stock;
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.World.NotifyStationDocked(context.Newark);
        int after = context.Market.GetListingForCommodity(context.Newark, context.Food).Stock;
        return after == before + offer.RequiredQuantity ? Pass() : Fail("destination stock did not increase exactly");
    }

    private static (bool Success, string FailureReason) ValidateDeliveryChangesDestinationPrice()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        int before = context.Market.GetListingForCommodity(context.Newark, context.Food).BuyPrice;
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.World.NotifyStationDocked(context.Newark);
        int after = context.Market.GetListingForCommodity(context.Newark, context.Food).BuyPrice;
        return after < before ? Pass() : Fail("destination price did not improve");
    }

    private static (bool Success, string FailureReason) ValidateRewardGrantedOnce()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        int before = context.PlayerCredits.Credits;
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.World.NotifyStationDocked(context.Newark);
        return context.PlayerCredits.Credits == before + offer.Reward ? Pass() : Fail("reward was not paid exactly once");
    }

    private static (bool Success, string FailureReason) ValidateMissionCompletesOnce()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.World.NotifyStationDocked(context.Newark);
        return offer.Status == MissionStatus.Completed && offer.ObjectiveComplete && context.Manager.ActiveMission == null ? Pass() : Fail("export did not complete");
    }

    private static (bool Success, string FailureReason) ValidateRepeatedCompletionCannotPay()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        context.World.NotifyStationDocked(context.Newark);
        int after = context.PlayerCredits.Credits;
        context.World.NotifyStationDocked(context.Newark);
        return context.PlayerCredits.Credits == after ? Pass() : Fail("repeated delivery paid again");
    }

    private static (bool Success, string FailureReason) ValidateCancellationRemovesCargo()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        bool cancelled = context.Manager.CancelMission(offer, out _);
        return cancelled && context.Player.CargoHold.GetCommodityQuantity(context.Food.Name) == 0 ? Pass() : Fail("cancel kept issued cargo");
    }

    private static (bool Success, string FailureReason) ValidateCancellationRestoresOriginStock()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        int before = context.Market.GetListingForCommodity(context.Origin, context.Food).Stock;
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin) || !context.Manager.CancelMission(offer, out _)) return Fail("export could not be cancelled");
        int after = context.Market.GetListingForCommodity(context.Origin, context.Food).Stock;
        return after == before ? Pass() : Fail("cancellation did not restore origin stock");
    }

    private static (bool Success, string FailureReason) ValidateCancellationRestoresOriginPrice()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        int before = context.Market.GetListingForCommodity(context.Origin, context.Food).BuyPrice;
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        int issued = context.Market.GetListingForCommodity(context.Origin, context.Food).BuyPrice;
        if (!context.Manager.CancelMission(offer, out _)) return Fail("export could not be cancelled");
        int after = context.Market.GetListingForCommodity(context.Origin, context.Food).BuyPrice;
        return issued > before && after == before ? Pass() : Fail($"cancellation did not restore origin pricing ({before}->{issued}->{after})");
    }

    private static (bool Success, string FailureReason) ValidateCancellationPaysNothing()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        int before = context.PlayerCredits.Credits;
        return offer != null && context.Manager.AcceptMission(offer, context.Origin) && context.Manager.CancelMission(offer, out _) &&
            context.PlayerCredits.Credits == before && offer.Status == MissionStatus.Failed ? Pass() : Fail("cancellation paid a reward");
    }

    private static (bool Success, string FailureReason) ValidateSaveLoadPreservesMission()
    {
        (ExportContext source, SaveGameData data, string path) = BuildSavedExportContext();
        try
        {
            ExportContext resumed = CreateContext();
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            return resumed.Manager.ActiveMission?.Type == MissionType.ExportContract ? Pass() : Fail("active export did not survive save/load");
        }
        finally { Cleanup(path); }
    }

    private static (bool Success, string FailureReason) ValidateSaveLoadPreservesReservation()
    {
        (ExportContext source, SaveGameData data, string path) = BuildSavedExportContext();
        try
        {
            ExportContext resumed = CreateContext();
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            return mission != null && resumed.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == mission.RequiredQuantity
                ? Pass() : Fail("export reservation did not survive save/load");
        }
        finally { Cleanup(path); }
    }

    private static (bool Success, string FailureReason) ValidateSaveLoadPreservesOriginMarket()
    {
        (ExportContext source, SaveGameData data, string path) = BuildSavedExportContext();
        try
        {
            int expected = source.Market.GetListingForCommodity(source.Origin, source.Food).Stock;
            ExportContext resumed = CreateContext();
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            return resumed.Market.GetListingForCommodity(resumed.Origin, resumed.Food).Stock == expected
                ? Pass() : Fail("post-issuance origin stock changed across save/load");
        }
        finally { Cleanup(path); }
    }

    private static (bool Success, string FailureReason) ValidatePostLoadDelivery()
    {
        (ExportContext source, SaveGameData data, string path) = BuildSavedExportContext();
        try
        {
            ExportContext resumed = CreateContext();
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            Mission mission = resumed.Manager.ActiveMission;
            int before = resumed.Market.GetListingForCommodity(resumed.Newark, resumed.Food).Stock;
            resumed.World.NotifyStationDocked(resumed.Newark);
            return mission != null && mission.Status == MissionStatus.Completed &&
                resumed.Market.GetListingForCommodity(resumed.Newark, resumed.Food).Stock == before + mission.RequiredQuantity
                ? Pass() : Fail("post-load delivery did not update destination");
        }
        finally { Cleanup(path); }
    }

    private static (bool Success, string FailureReason) ValidateFreightRegression()
    {
        ExportContext context = CreateContext();
        if (!CreateShortage(context, context.Newark, 140)) return Fail("could not stage Newark shortage");
        Mission freight = context.Manager.GenerateJobBoardMissions(10, context.Newark.FactionId, context.Newark)
            .FirstOrDefault(mission => mission.Type == MissionType.FreightContract);
        return freight != null && freight.RequiredQuantity > 0 ? Pass() : Fail("Phase 15 freight offer disappeared");
    }

    private static (bool Success, string FailureReason) ValidateCourierRegression()
    {
        ExportContext context = CreateContext();
        Mission courier = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PriorityDispatchId), "Smoke", context.Newark.FactionId);
        bool accepted = courier != null && context.Manager.AcceptMission(courier, context.Newark);
        return accepted && courier.Type == MissionType.CourierDelivery && context.Player.CargoHold.HasMissionCargo(courier.Id, courier.PackageId, 1)
            ? Pass() : Fail("courier acceptance regressed");
    }

    private static (bool Success, string FailureReason) ValidateSealedPackageExcluded()
    {
        return CommodityCatalog.GetById("sealed-data-package")?.IsMissionCargo == true &&
            !CommodityCatalog.GetById("sealed-data-package").BasePrice.Equals(120)
            ? Pass() : Fail("sealed package metadata changed unexpectedly");
    }

    private static (bool Success, string FailureReason) ValidateCargoTransferPreservesExport()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        CargoHold transferred = new(100);
        bool moved = context.Player.CargoHold.TransferTo(transferred, CommodityCatalog.BuildRegistry());
        return moved && transferred.GetMissionCargoQuantity(offer.Id) == offer.RequiredQuantity &&
            transferred.GetSellableCommodityQuantity(context.Food.Name) == 0 ? Pass() : Fail("ship transfer lost export reservation");
    }

    private static (bool Success, string FailureReason) ValidateInsufficientShipCapacityRejects()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        context.Player.CargoHold.SetMaxCapacity(200);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin) || !context.Player.CargoHold.AddCommodity(context.Food, 15))
            return Fail("could not stage mixed export cargo");
        ShipDealer dealer = new();
        ShipDefinition transport = dealer.GetShipByName("Pirate Transport");
        ShipDefinition fighter = dealer.GetShipByName("Scimitar");
        dealer.SetCurrentShip(transport);
        context.Player.CargoHold.SetMaxCapacity(transport.CargoCapacity);
        bool canBuySmaller = dealer.CanPurchaseShip(fighter, context.PlayerCredits, context.Player, out _);
        return !canBuySmaller ? Pass() : Fail("ship dealer allowed insufficient-capacity change");
    }

    private static (bool Success, string FailureReason) ValidateUnrelatedMarketUnchanged()
    {
        ExportContext context = CreatePairedContext();
        StationMarketListing before = context.Market.GetListingForCommodity(context.Rochester, context.Food);
        Mission offer = FindExport(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Origin)) return Fail("offer could not be accepted");
        StationMarketListing after = context.Market.GetListingForCommodity(context.Rochester, context.Food);
        return before.Stock == after.Stock && before.BuyPrice == after.BuyPrice ? Pass() : Fail("unrelated market changed");
    }

    private static (bool Success, string FailureReason) ValidateBoardRefreshDoesNotIssueCargo()
    {
        ExportContext context = CreatePairedContext();
        Mission first = FindExport(context);
        Mission second = FindExport(context);
        return first != null && second != null && context.Manager.ActiveMission == null &&
            context.Player.CargoHold.GetCommodityQuantity(context.Food.Name) == 0 ? Pass() : Fail("board refresh issued cargo");
    }

    private static (bool Success, string FailureReason) ValidateFortBushNewarkPairing()
    {
        ExportContext context = CreatePairedContext();
        Mission offer = FindExport(context);
        return offer != null && offer.CommodityId == context.Food.Id &&
            offer.OriginStationName == context.Origin.Name && offer.Destination == context.Newark.Name
            ? Pass() : Fail("Fort Bush surplus did not pair with Newark shortage");
    }

    private static (bool Success, string FailureReason) ValidateFailurePathsAtomic()
    {
        ExportContext capacity = CreatePairedContext();
        Mission offer = FindExport(capacity);
        int originStock = capacity.Market.GetListingForCommodity(capacity.Origin, capacity.Food).Stock;
        capacity.Player.CargoHold.SetMaxCapacity(0);
        bool accepted = offer != null && capacity.Manager.AcceptMission(offer, capacity.Origin);
        bool acceptanceAtomic = !accepted && capacity.Market.GetListingForCommodity(capacity.Origin, capacity.Food).Stock == originStock;

        ExportContext delivery = CreatePairedContext();
        Mission deliveryOffer = FindExport(delivery);
        if (deliveryOffer == null || !delivery.Manager.AcceptMission(deliveryOffer, delivery.Origin)) return Fail("delivery fixture failed");
        delivery.Player.CargoHold.RemoveMissionCargo(deliveryOffer.Id, delivery.Food, deliveryOffer.RequiredQuantity);
        int destinationStock = delivery.Market.GetListingForCommodity(delivery.Newark, delivery.Food).Stock;
        delivery.World.NotifyStationDocked(delivery.Newark);
        return acceptanceAtomic && deliveryOffer.Status == MissionStatus.InProgress &&
            delivery.Market.GetListingForCommodity(delivery.Newark, delivery.Food).Stock == destinationStock
            ? Pass() : Fail("failure path partially mutated state");
    }

    private static (bool Success, string FailureReason) ValidateNormalOpportunitiesQuiet()
    {
        ExportContext context = CreateContext();
        return context.Manager.GetMarketOpportunities().Count == 0 ? Pass() : Fail("normal markets produced a strong signal");
    }

    private static (bool Success, string FailureReason) ValidateShortageOpportunity()
    {
        ExportContext context = CreateContext();
        if (!CreateShortage(context, context.Newark, 140)) return Fail("could not stage shortage");
        return context.Manager.GetMarketOpportunities().Any(opportunity => opportunity.Type == MarketOpportunityType.Shortage && opportunity.StationName == context.Newark.Name)
            ? Pass() : Fail("shortage was not surfaced");
    }

    private static (bool Success, string FailureReason) ValidateSurplusOpportunity()
    {
        ExportContext context = CreateContext();
        if (!StageOriginSurplus(context, 300)) return Fail("could not stage surplus");
        return context.Manager.GetMarketOpportunities().Any(opportunity => opportunity.Type == MarketOpportunityType.Surplus && opportunity.StationName == context.Origin.Name)
            ? Pass() : Fail("surplus was not surfaced");
    }

    private static (bool Success, string FailureReason) ValidateOpportunityRanking()
    {
        ExportContext context = CreatePairedContext();
        IReadOnlyList<MarketOpportunity> opportunities = context.Manager.GetMarketOpportunities();
        return opportunities.Count > 0 && opportunities[0].Score >= opportunities[^1].Score ? Pass() : Fail("opportunity ranking was not strongest-first");
    }

    private static (bool Success, string FailureReason) ValidateOpportunityDeterministic()
    {
        ExportContext first = CreatePairedContext();
        ExportContext second = CreatePairedContext();
        string a = string.Join("|", first.Manager.GetMarketOpportunities().Select(opportunity => opportunity.GetDisplayText()));
        string b = string.Join("|", second.Manager.GetMarketOpportunities().Select(opportunity => opportunity.GetDisplayText()));
        return a == b ? Pass() : Fail("opportunity ranking changed between identical states");
    }

    private static (bool Success, string FailureReason) ValidateOpportunityBounded()
    {
        ExportContext context = CreatePairedContext();
        return context.Manager.GetMarketOpportunities(5).Count <= 5 ? Pass() : Fail("opportunity list exceeded requested bound");
    }

    private static (bool Success, string FailureReason) ValidateOpportunityExcludesInvalid()
    {
        ExportContext context = CreatePairedContext();
        return context.Manager.GetMarketOpportunities().All(opportunity =>
            opportunity.CommodityId != "sealed-data-package" && CommodityCatalog.GetByIdOrName(opportunity.CommodityId) != null)
            ? Pass() : Fail("invalid or mission cargo appeared in opportunities");
    }

    private static (bool Success, string FailureReason) ValidateOpportunityPairing()
    {
        ExportContext context = CreatePairedContext();
        return context.Manager.GetMarketOpportunities().Any(opportunity => opportunity.Type == MarketOpportunityType.Pairing &&
            opportunity.OriginStationName == context.Origin.Name && opportunity.DestinationStationName == context.Newark.Name)
            ? Pass() : Fail("live surplus-to-shortage pairing was not surfaced");
    }

    private static (bool Success, string FailureReason) ValidateOpportunityRefreshes()
    {
        ExportContext context = CreateContext();
        IReadOnlyList<MarketOpportunity> before = context.Manager.GetMarketOpportunities();
        if (!CreateShortage(context, context.Newark, 140)) return Fail("could not change market state");
        IReadOnlyList<MarketOpportunity> after = context.Manager.GetMarketOpportunities();
        return before.Count != after.Count || after.Any(opportunity => opportunity.StationName == context.Newark.Name)
            ? Pass() : Fail("opportunity refresh ignored market change");
    }

    private static (bool Success, string FailureReason) ValidateOpportunityReadDoesNotMutate()
    {
        ExportContext context = CreatePairedContext();
        StationMarketListing beforeOrigin = context.Market.GetListingForCommodity(context.Origin, context.Food);
        StationMarketListing beforeDestination = context.Market.GetListingForCommodity(context.Newark, context.Food);
        context.Manager.GetMarketOpportunities();
        StationMarketListing afterOrigin = context.Market.GetListingForCommodity(context.Origin, context.Food);
        StationMarketListing afterDestination = context.Market.GetListingForCommodity(context.Newark, context.Food);
        return beforeOrigin.Stock == afterOrigin.Stock && beforeDestination.Stock == afterDestination.Stock ? Pass() : Fail("opportunity read mutated stock");
    }

    private static Mission FindExport(ExportContext context)
    {
        return context.Manager.GenerateJobBoardMissions(10, context.Origin.FactionId, context.Origin)
            .FirstOrDefault(mission => mission.Type == MissionType.ExportContract);
    }

    private static bool StageOriginSurplus(ExportContext context, int quantity)
    {
        if (quantity <= 0 || !context.SourceCargo.AddCommodity(context.Food, quantity)) return false;
        return context.Market.TrySell(context.Origin, context.Food, quantity, context.SourceCredits, context.SourceCargo, out _);
    }

    private static bool CreateShortage(ExportContext context, Station station, int quantity)
    {
        return quantity > 0 && context.Market.TryBuy(station, context.Food, quantity, context.SinkCredits, context.SinkCargo, out _);
    }

    private static ExportContext CreatePairedContext()
    {
        ExportContext context = CreateContext();
        if (!StageOriginSurplus(context, 300) || !CreateShortage(context, context.Newark, 140))
            throw new InvalidOperationException("could not stage Fort Bush/Newark market pair");
        return context;
    }

    private static ExportContext CreateContext()
    {
        ExportContext context = new()
        {
            Market = new MarketManager(),
            PlayerCredits = new PlayerCredits(1_000_000),
            Player = new Ship(Vector3.Zero),
            SourceCredits = new PlayerCredits(1_000_000),
            SourceCargo = new CargoHold(1_000),
            SinkCredits = new PlayerCredits(1_000_000),
            SinkCargo = new CargoHold(1_000),
            Food = CommodityCatalog.GetById("food-rations"),
            SealedPackage = CommodityCatalog.GetById("sealed-data-package"),
            Origin = CreateStation("Fort Bush", 1),
            Newark = CreateStation("Newark Station", 1),
            Rochester = CreateStation("Rochester Base", 1),
            Buffalo = CreateStation("Buffalo Base", 1)
        };
        context.Stations.AddRange(new[] { context.Origin, context.Newark, context.Rochester, context.Buffalo });
        context.Manager = new MissionManager(context.PlayerCredits, null, null, context.Market, context.Player.CargoHold);
        context.World = new MissionWorldManager(
            context.Manager,
            new MissionWaypointSystem(),
            context.Player,
            context.NpcShips,
            context.SpaceObjects,
            () => context.Stations,
            null,
            context.Market);
        context.Manager.SetWorldManager(context.World);
        return context;
    }

    private static (ExportContext Source, SaveGameData Data, string Path) BuildSavedExportContext()
    {
        ExportContext source = CreatePairedContext();
        Mission offer = FindExport(source);
        if (offer == null || !source.Manager.AcceptMission(offer, source.Origin))
            throw new InvalidOperationException("could not accept export for save fixture");

        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "export.json");
        SaveGameManager saver = new(path);
        SaveGameData data = new()
        {
            PlayerCredits = source.PlayerCredits.Credits,
            Cargo = saver.CaptureCargo(source.Player.CargoHold),
            ActiveMissions = saver.CaptureMissions(source.Manager.ActiveMissions),
            StationMarkets = source.Market.CaptureRuntimeState()
        };
        if (!saver.TrySave(data, out string failure)) throw new InvalidOperationException(failure);
        if (!saver.TryLoad(out data, out failure)) throw new InvalidOperationException(failure);
        return (source, data, path);
    }

    private static Station CreateStation(string name, int systemIndex)
    {
        return new Station(new StationConfig
        {
            Description = name,
            SystemIndex = systemIndex,
            StartupPositionX = 0f,
            StartupPositionY = 0f,
            StartupPositionZ = 0f,
            Radius = 1_000f,
            DockingRange = 800f,
            FactionId = FactionManager.LibertyCorporations
        }, null);
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

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

    private static string BuildRepresentativeReport()
    {
        ExportContext context = CreatePairedContext();
        StationMarketListing originBefore = context.Market.GetListingForCommodity(context.Origin, context.Food);
        StationMarketListing destinationBefore = context.Market.GetListingForCommodity(context.Newark, context.Food);
        Mission offer = FindExport(context);
        int cargoVolume = offer.RequiredQuantity * context.Food.VolumePerUnit;
        int creditsBefore = context.PlayerCredits.Credits;
        if (!context.Manager.AcceptMission(offer, context.Origin)) throw new InvalidOperationException("representative acceptance failed");
        StationMarketListing originAfter = context.Market.GetListingForCommodity(context.Origin, context.Food);
        context.World.NotifyStationDocked(context.Newark);
        StationMarketListing destinationAfter = context.Market.GetListingForCommodity(context.Newark, context.Food);
        return $"{context.Origin.Name} Food Rations stock {originBefore.Stock}->{originAfter.Stock}, buy {originBefore.BuyPrice}->{originAfter.BuyPrice}; {context.Newark.Name} stock {destinationBefore.Stock}->{destinationAfter.Stock}, buy {destinationBefore.BuyPrice}->{destinationAfter.BuyPrice}; quantity {offer.RequiredQuantity}, volume {cargoVolume}, reward {offer.Reward:N0} CR, credits {creditsBefore}->{context.PlayerCredits.Credits}, status {offer.Status}";
    }

    private static void Cleanup(string path)
    {
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        catch { }
    }

    private sealed class ExportContext
    {
        public MarketManager Market { get; set; }
        public MissionManager Manager { get; set; }
        public MissionWorldManager World { get; set; }
        public Ship Player { get; set; }
        public PlayerCredits PlayerCredits { get; set; }
        public PlayerCredits SourceCredits { get; set; }
        public CargoHold SourceCargo { get; set; }
        public PlayerCredits SinkCredits { get; set; }
        public CargoHold SinkCargo { get; set; }
        public Commodity Food { get; set; }
        public Commodity SealedPackage { get; set; }
        public Station Origin { get; set; }
        public Station Newark { get; set; }
        public Station Rochester { get; set; }
        public Station Buffalo { get; set; }
        public List<Station> Stations { get; } = new();
        public List<NpcShip> NpcShips { get; } = new();
        public List<SpaceObject> SpaceObjects { get; } = new();
    }
}
