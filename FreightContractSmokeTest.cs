using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 15 proof of the live shortage -> freight -> delivery
/// loop. The test uses the production MarketManager, CargoHold, MissionManager,
/// MissionWorldManager, and SaveGameManager authorities.
/// </summary>
internal sealed class FreightContractSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(ValidateNormalStockHasNoOffer, "normal stock has no offer", ref passed, ref failed);
        RunCase(ValidateShortageThreshold, "shortage threshold", ref passed, ref failed);
        RunCase(ValidateMissionOnlyCommodityExcluded, "mission-only commodity excluded", ref passed, ref failed);
        RunCase(ValidateInvalidCommodityExcluded, "invalid commodity excluded", ref passed, ref failed);
        RunCase(ValidateQuantityPositive, "quantity positive", ref passed, ref failed);
        RunCase(ValidateQuantityBounded, "quantity bounded", ref passed, ref failed);
        RunCase(ValidateRewardPositive, "reward positive", ref passed, ref failed);
        RunCase(ValidateRewardDeterministic, "reward deterministic", ref passed, ref failed);
        RunCase(ValidateSevereShortageScales, "severe shortage scaling", ref passed, ref failed);
        RunCase(ValidateDuplicateOfferSuppression, "duplicate offer suppression", ref passed, ref failed);
        RunCase(ValidateAcceptedTermsStable, "accepted terms stable", ref passed, ref failed);
        RunCase(ValidateExistingCargoReserved, "existing cargo reserved", ref passed, ref failed);
        RunCase(ValidateInsufficientCargoReservesAvailableOnly, "insufficient cargo reserves available only", ref passed, ref failed);
        RunCase(ValidateProgressiveReservation, "progressive reservation", ref passed, ref failed);
        RunCase(ValidateReservationCap, "reservation cap", ref passed, ref failed);
        RunCase(ValidateExcessRemainsSellable, "excess remains sellable", ref passed, ref failed);
        RunCase(ValidateProtectedCargoCannotSell, "protected cargo cannot sell", ref passed, ref failed);
        RunCase(ValidateWrongCommodityCannotComplete, "wrong commodity cannot complete", ref passed, ref failed);
        RunCase(ValidateInsufficientQuantityCannotComplete, "insufficient quantity cannot complete", ref passed, ref failed);
        RunCase(ValidateWrongDestinationCannotComplete, "wrong destination cannot complete", ref passed, ref failed);
        RunCase(ValidateSuccessfulDeliveryRemovesCargo, "delivery removes cargo", ref passed, ref failed);
        RunCase(ValidateSuccessfulDeliveryReleasesReservation, "delivery releases reservation", ref passed, ref failed);
        RunCase(ValidateRewardGrantedOnce, "reward granted once", ref passed, ref failed);
        RunCase(ValidateDestinationStockIncreases, "destination stock increases", ref passed, ref failed);
        RunCase(ValidateDestinationPriceImproves, "destination price improves", ref passed, ref failed);
        RunCase(ValidateMissionCompletesOnce, "mission completes once", ref passed, ref failed);
        RunCase(ValidateRepeatedCompletionCannotPay, "repeated completion cannot pay", ref passed, ref failed);
        RunCase(ValidateCancellationReleasesReservation, "cancellation releases reservation", ref passed, ref failed);
        RunCase(ValidateCancellationLeavesMarketUnchanged, "cancellation leaves market unchanged", ref passed, ref failed);
        RunCase(ValidateSaveLoadPreservesMission, "save/load preserves mission", ref passed, ref failed);
        RunCase(ValidateSaveLoadPreservesReservation, "save/load preserves reservation", ref passed, ref failed);
        RunCase(ValidateSaveLoadPreservesExcess, "save/load preserves excess", ref passed, ref failed);
        RunCase(ValidateAcceptedMissionSurvivesRecovery, "accepted mission survives recovery", ref passed, ref failed);
        RunCase(ValidateStaleOfferDisappears, "stale offer disappears", ref passed, ref failed);
        RunCase(ValidateCourierRegression, "courier regression", ref passed, ref failed);
        RunCase(ValidateSealedPackageExcluded, "sealed package excluded", ref passed, ref failed);
        RunCase(ValidateShipPreservesReservation, "ship preserves reservation", ref passed, ref failed);
        RunCase(ValidateShipRejectsInsufficientCapacity, "ship rejects insufficient capacity", ref passed, ref failed);
        RunCase(ValidateUnrelatedMarketUnchanged, "unrelated market unchanged", ref passed, ref failed);
        RunCase(ValidateFailedDeliveryAtomic, "failed delivery atomic", ref passed, ref failed);

        string scenario = RunSilenced(BuildRepresentativeReport);
        Console.WriteLine($"[FREIGHT SMOKE] REPRESENTATIVE: {scenario}");
        Console.WriteLine($"[FREIGHT SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[FREIGHT SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[FREIGHT SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[FREIGHT SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidateNormalStockHasNoOffer()
    {
        FreightContext context = CreateContext();
        return FindOffer(context) == null ? Pass() : Fail("normal Newark stock generated a freight offer");
    }

    private (bool Success, string FailureReason) ValidateShortageThreshold()
    {
        FreightContext context = CreateContext();
        if (!BuyAt(context, context.Newark, context.Food, 100, context.SinkCredits, context.SinkCargo))
            return Fail("could not create below-threshold fixture");
        return FindOffer(context) == null ? Pass() : Fail("offer appeared above the configured 40% stock threshold");
    }

    private (bool Success, string FailureReason) ValidateMissionOnlyCommodityExcluded()
    {
        FreightContext context = CreateShortageContext();
        return context.Manager.GenerateJobBoardMissions(10, context.Newark.FactionId, context.Newark)
            .All(mission => mission.Type != MissionType.FreightContract ||
                !string.Equals(mission.CommodityId, "sealed-data-package", StringComparison.OrdinalIgnoreCase))
            ? Pass()
            : Fail("sealed-data-package was offered as freight");
    }

    private (bool Success, string FailureReason) ValidateInvalidCommodityExcluded()
    {
        FreightContext context = CreateShortageContext();
        return context.Manager.GenerateJobBoardMissions(10, context.Newark.FactionId, context.Newark)
            .Where(mission => mission.Type == MissionType.FreightContract)
            .All(mission => CommodityCatalog.GetByIdOrName(mission.CommodityId) != null)
            ? Pass()
            : Fail("freight offer referenced an invalid commodity");
    }

    private (bool Success, string FailureReason) ValidateQuantityPositive()
    {
        Mission offer = FindOffer(CreateShortageContext());
        return offer != null && offer.RequiredQuantity > 0 ? Pass() : Fail("generated quantity was not positive");
    }

    private (bool Success, string FailureReason) ValidateQuantityBounded()
    {
        FreightContext context = CreateShortageContext();
        Mission offer = FindOffer(context);
        Commodity commodity = CommodityCatalog.GetByIdOrName(offer?.CommodityId);
        return offer != null && commodity != null &&
            offer.RequiredQuantity <= MissionManager.FreightMaximumUnits &&
            (long)offer.RequiredQuantity * commodity.VolumePerUnit <= MissionManager.FreightMaximumCargoVolume
            ? Pass()
            : Fail("generated freight volume exceeded the gameplay bound");
    }

    private (bool Success, string FailureReason) ValidateRewardPositive()
    {
        Mission offer = FindOffer(CreateShortageContext());
        return offer != null && offer.Reward > 0 && offer.Reward <= MissionManager.FreightMaximumReward
            ? Pass()
            : Fail("generated reward was outside positive bounded range");
    }

    private (bool Success, string FailureReason) ValidateRewardDeterministic()
    {
        Mission first = FindOffer(CreateShortageContext());
        Mission second = FindOffer(CreateShortageContext());
        return first != null && second != null &&
            first.RequiredQuantity == second.RequiredQuantity &&
            first.Reward == second.Reward &&
            first.CommodityId == second.CommodityId
            ? Pass()
            : Fail("same market state produced different freight terms");
    }

    private (bool Success, string FailureReason) ValidateSevereShortageScales()
    {
        FreightContext mild = CreateContext();
        FreightContext severe = CreateContext();
        if (!BuyAt(mild, mild.Newark, mild.Food, 140, mild.SinkCredits, mild.SinkCargo) ||
            !BuyAt(severe, severe.Newark, severe.Food, 180, severe.SinkCredits, severe.SinkCargo))
            return Fail("could not create severity fixtures");
        Mission mildOffer = FindOffer(mild);
        Mission severeOffer = FindOffer(severe);
        return mildOffer != null && severeOffer != null &&
            severeOffer.RequiredQuantity >= mildOffer.RequiredQuantity &&
            severeOffer.Reward >= mildOffer.Reward
            ? Pass()
            : Fail("severe shortage did not scale quantity/reward sensibly");
    }

    private (bool Success, string FailureReason) ValidateDuplicateOfferSuppression()
    {
        FreightContext context = CreateShortageContext();
        Mission first = FindOffer(context);
        Mission second = FindOffer(context);
        if (first == null || second == null)
            return Fail("shortage did not produce an offer");
        if (!ReferenceEquals(first, second) || first.Id != second.Id)
            return Fail("refresh generated a near-identical duplicate offer");

        if (!context.Manager.AcceptMission(first, context.Newark))
            return Fail("offer could not be accepted");
        return context.Manager.GenerateJobBoardMissions(10, context.Newark.FactionId, context.Newark)
            .All(mission => mission.Type != MissionType.FreightContract ||
                !string.Equals(mission.CommodityId, first.CommodityId, StringComparison.OrdinalIgnoreCase))
            ? Pass()
            : Fail("matching active freight contract was regenerated");
    }

    private (bool Success, string FailureReason) ValidateAcceptedTermsStable()
    {
        FreightContext context = CreateShortageContext();
        Mission offer = FindOffer(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Newark))
            return Fail("shortage contract was not accepted");
        int quantity = offer.RequiredQuantity;
        int reward = offer.Reward;
        context.Market.AdvanceTime(3_600);
        return offer.RequiredQuantity == quantity && offer.Reward == reward && offer.Status == MissionStatus.InProgress
            ? Pass()
            : Fail("accepted freight terms changed after market recovery");
    }

    private (bool Success, string FailureReason) ValidateExistingCargoReserved()
    {
        FreightContext context = CreateShortageContext();
        if (!context.Player.CargoHold.AddCommodity(context.Food, 10))
            return Fail("could not stage existing food");
        Mission offer = FindOffer(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Newark))
            return Fail("could not accept freight with existing cargo");
        return context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == 10 &&
            context.Player.CargoHold.GetSellableCommodityQuantity(context.Food.Name) == 0
            ? Pass()
            : Fail("existing ordinary units were not reserved exactly once");
    }

    private (bool Success, string FailureReason) ValidateInsufficientCargoReservesAvailableOnly()
    {
        FreightContext context = CreateShortageContext();
        Mission offer = FindOffer(context);
        return offer != null && context.Manager.AcceptMission(offer, context.Newark) &&
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == 0 &&
            context.Player.CargoHold.GetMissionReservationTargetQuantity(offer.Id) == offer.RequiredQuantity
            ? Pass()
            : Fail("empty hold did not create a pending reservation target");
    }

    private (bool Success, string FailureReason) ValidateProgressiveReservation()
    {
        FreightContext context = CreateShortageContext();
        Mission offer = FindOffer(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Newark))
            return Fail("could not accept freight");
        if (!context.Player.CargoHold.AddCommodity(context.Food, 10) ||
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != 10)
            return Fail("first purchase did not reserve newly acquired units");
        if (!context.Player.CargoHold.AddCommodity(context.Food, offer.RequiredQuantity) ||
            context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != offer.RequiredQuantity)
            return Fail("second purchase did not stop at the required quantity");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateReservationCap()
    {
        FreightContext context = CreateShortageContext();
        Mission offer = FindOffer(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Newark))
            return Fail("could not accept freight");
        context.Player.CargoHold.AddCommodity(context.Food, offer.RequiredQuantity + 5);
        return context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == offer.RequiredQuantity
            ? Pass()
            : Fail("reservation exceeded required quantity");
    }

    private (bool Success, string FailureReason) ValidateExcessRemainsSellable()
    {
        FreightContext context = CreateShortageContext();
        Mission offer = FindOffer(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Newark) ||
            !context.Player.CargoHold.AddCommodity(context.Food, offer.RequiredQuantity + 5))
            return Fail("could not stage freight plus excess");
        return context.Player.CargoHold.GetSellableCommodityQuantity(context.Food.Name) == 5
            ? Pass()
            : Fail("excess ordinary cargo was not left sellable");
    }

    private (bool Success, string FailureReason) ValidateProtectedCargoCannotSell()
    {
        FreightContext context = CreateShortageContext();
        Mission offer = FindOffer(context);
        if (offer == null || !context.Manager.AcceptMission(offer, context.Newark) ||
            !context.Player.CargoHold.AddCommodity(context.Food, offer.RequiredQuantity))
            return Fail("could not stage protected food");
        CommodityDealer dealer = new();
        dealer.SetDockedStation(context.Newark);
        return !dealer.TrySellCommodity(context.Food, 1, context.PlayerCredits, context.Player.CargoHold, out _)
            ? Pass()
            : Fail("trader sold protected freight");
    }

    private (bool Success, string FailureReason) ValidateWrongCommodityCannotComplete()
    {
        FreightContext context = PrepareAccepted(out Mission mission);
        Commodity water = CommodityCatalog.GetById("water");
        int before = context.Player.CargoHold.GetMissionCargoQuantity(mission.Id);
        bool removed = context.Player.CargoHold.RemoveMissionCargo(mission.Id, water, before);
        return !removed && context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == before
            ? Pass()
            : Fail("wrong commodity affected freight reservation");
    }

    private (bool Success, string FailureReason) ValidateInsufficientQuantityCannotComplete()
    {
        FreightContext context = CreateShortageContext();
        Mission mission = FindOffer(context);
        if (mission == null || !context.Manager.AcceptMission(mission, context.Newark) ||
            !context.Player.CargoHold.AddCommodity(context.Food, Math.Max(1, mission.RequiredQuantity - 1)))
            return Fail("could not stage partial freight");
        int stock = context.Market.GetListingForCommodity(context.Newark, context.Food).Stock;
        return !context.World.NotifyStationDocked(context.Newark) &&
            mission.Status == MissionStatus.InProgress &&
            context.Market.GetListingForCommodity(context.Newark, context.Food).Stock == stock
            ? Pass()
            : Fail("insufficient delivery partially mutated mission or market");
    }

    private (bool Success, string FailureReason) ValidateWrongDestinationCannotComplete()
    {
        FreightContext context = PrepareAccepted(out Mission mission);
        int reserved = context.Player.CargoHold.GetMissionCargoQuantity(mission.Id);
        return !context.World.NotifyStationDocked(context.FortBush) &&
            mission.Status == MissionStatus.InProgress &&
            context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == reserved
            ? Pass()
            : Fail("wrong destination changed freight state");
    }

    private (bool Success, string FailureReason) ValidateSuccessfulDeliveryRemovesCargo()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        int before = context.Player.CargoHold.GetCommodityQuantity(context.Food.Name);
        if (!context.World.NotifyStationDocked(context.Newark))
            return Fail("correct destination did not complete freight");
        return context.Player.CargoHold.GetCommodityQuantity(context.Food.Name) == before - mission.RequiredQuantity
            ? Pass()
            : Fail("delivery did not consume the reserved quantity");
    }

    private (bool Success, string FailureReason) ValidateSuccessfulDeliveryReleasesReservation()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        if (!context.World.NotifyStationDocked(context.Newark))
            return Fail("freight delivery failed");
        return context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == 0 &&
            context.Player.CargoHold.GetMissionReservedQuantity(context.Food.Name) == 0
            ? Pass()
            : Fail("delivery left a mission reservation behind");
    }

    private (bool Success, string FailureReason) ValidateRewardGrantedOnce()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        int before = context.PlayerCredits.Credits;
        return context.World.NotifyStationDocked(context.Newark) &&
            context.PlayerCredits.Credits == before + mission.Reward
            ? Pass()
            : Fail("freight reward was not paid exactly at delivery");
    }

    private (bool Success, string FailureReason) ValidateDestinationStockIncreases()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        int before = context.Market.GetListingForCommodity(context.Newark, context.Food).Stock;
        if (!context.World.NotifyStationDocked(context.Newark))
            return Fail("freight delivery failed");
        return context.Market.GetListingForCommodity(context.Newark, context.Food).Stock == before + mission.RequiredQuantity
            ? Pass()
            : Fail("destination market did not receive actual delivered units");
    }

    private (bool Success, string FailureReason) ValidateDestinationPriceImproves()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        int before = context.Market.GetListingForCommodity(context.Newark, context.Food).BuyPrice;
        if (!context.World.NotifyStationDocked(context.Newark))
            return Fail("freight delivery failed");
        int after = context.Market.GetListingForCommodity(context.Newark, context.Food).BuyPrice;
        return after < before ? Pass() : Fail($"destination buy price did not improve: {before} -> {after}");
    }

    private (bool Success, string FailureReason) ValidateMissionCompletesOnce()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        return context.World.NotifyStationDocked(context.Newark) &&
            mission.Status == MissionStatus.Completed &&
            mission.ObjectiveComplete &&
            mission.RewardPaid
            ? Pass()
            : Fail("freight mission did not complete and pay exactly once");
    }

    private (bool Success, string FailureReason) ValidateRepeatedCompletionCannotPay()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        if (!context.World.NotifyStationDocked(context.Newark))
            return Fail("freight delivery failed");
        int after = context.PlayerCredits.Credits;
        return !context.World.NotifyStationDocked(context.Newark) &&
            context.PlayerCredits.Credits == after
            ? Pass()
            : Fail("repeated docking paid freight twice");
    }

    private (bool Success, string FailureReason) ValidateCancellationReleasesReservation()
    {
        FreightContext context = PrepareAccepted(out Mission mission);
        if (!context.Player.CargoHold.AddCommodity(context.Food, 10))
            return Fail("could not stage cancellation cargo");
        int reserved = context.Player.CargoHold.GetMissionCargoQuantity(mission.Id);
        if (reserved == 0 || !context.Player.CargoHold.GetMissionReservationTargetQuantity(mission.Id).Equals(mission.RequiredQuantity))
            return Fail("fixture did not create a partial reservation");
        context.Manager.FailMission(mission, "smoke cancellation");
        return context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == 0 &&
            context.Player.CargoHold.GetSellableCommodityQuantity(context.Food.Name) == reserved
            ? Pass()
            : Fail("cancellation did not release freight reservation");
    }

    private (bool Success, string FailureReason) ValidateCancellationLeavesMarketUnchanged()
    {
        FreightContext context = PrepareAccepted(out Mission mission);
        int stock = context.Market.GetListingForCommodity(context.Newark, context.Food).Stock;
        context.Manager.FailMission(mission, "smoke cancellation");
        return context.Market.GetListingForCommodity(context.Newark, context.Food).Stock == stock
            ? Pass()
            : Fail("cancellation changed the destination market");
    }

    private (bool Success, string FailureReason) ValidateSaveLoadPreservesMission()
    {
        (FreightContext source, SaveGameData data, string path) = BuildSavedPartialContext();
        try
        {
            FreightContext resumed = CreateContext();
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out List<string> cargoWarnings);
            saver.ApplyMissions(resumed.Manager, data, out List<string> missionWarnings);
            return cargoWarnings.Count == 0 && missionWarnings.Count == 0 &&
                resumed.Manager.ActiveMission != null &&
                resumed.Manager.ActiveMission.Id == source.Manager.ActiveMission.Id
                ? Pass()
                : Fail("active freight mission did not survive JSON save/load");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private (bool Success, string FailureReason) ValidateSaveLoadPreservesReservation()
    {
        (_, SaveGameData data, string path) = BuildSavedPartialContext();
        try
        {
            FreightContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            return mission != null &&
                resumed.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == mission.RequiredQuantity
                ? Pass()
                : Fail("reserved freight quantity did not survive load");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private (bool Success, string FailureReason) ValidateSaveLoadPreservesExcess()
    {
        (_, SaveGameData data, string path) = BuildSavedPartialContext();
        try
        {
            FreightContext resumed = CreateContext();
            SaveGameManager saver = new(path);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            Mission mission = resumed.Manager.ActiveMission;
            return mission != null &&
                resumed.Player.CargoHold.GetCommodityQuantity(resumed.Food.Name) == mission.RequiredQuantity + 5 &&
                resumed.Player.CargoHold.GetSellableCommodityQuantity(resumed.Food.Name) == 5
                ? Pass()
                : Fail("unreserved excess cargo did not survive load");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private (bool Success, string FailureReason) ValidateAcceptedMissionSurvivesRecovery()
    {
        FreightContext context = PrepareAccepted(out Mission mission);
        context.Market.AdvanceTime(3_600);
        return mission.Status == MissionStatus.InProgress && context.Manager.ActiveMission == mission
            ? Pass()
            : Fail("passive recovery invalidated an accepted freight contract");
    }

    private (bool Success, string FailureReason) ValidateStaleOfferDisappears()
    {
        FreightContext context = CreateShortageContext();
        if (FindOffer(context) == null)
            return Fail("shortage offer did not appear");
        context.Market.AdvanceTime(3_600);
        return FindOffer(context) == null ? Pass() : Fail("recovered market kept a stale unaccepted offer");
    }

    private (bool Success, string FailureReason) ValidateCourierRegression()
    {
        MissionDefinition courier = MissionCatalog.GetById(MissionCatalog.PriorityDispatchId);
        return courier != null && courier.Type == MissionType.CourierDelivery &&
            courier.PackageId == "sealed-data-package" && courier.PackageQuantity == 1
            ? Pass()
            : Fail("courier catalog metadata changed");
    }

    private (bool Success, string FailureReason) ValidateSealedPackageExcluded()
    {
        Commodity package = CommodityCatalog.GetById("sealed-data-package");
        return package != null && package.IsMissionCargo &&
            FindOffer(CreateShortageContext())?.CommodityId != package.Id
            ? Pass()
            : Fail("sealed package was eligible for ordinary freight");
    }

    private (bool Success, string FailureReason) ValidateShipPreservesReservation()
    {
        FreightContext context = PrepareAccepted(out Mission mission);
        if (!context.Player.CargoHold.AddCommodity(context.Food, 10))
            return Fail("could not stage ship cargo");
        ShipDealer dealer = new();
        ShipDefinition transport = dealer.GetShipByName("Pirate Transport");
        if (transport == null || !dealer.TryPurchaseShip(transport, context.PlayerCredits, context.Player, out _))
            return Fail("larger ship purchase failed");
        return context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) > 0 &&
            context.Player.CargoHold.GetMissionReservationTargetQuantity(mission.Id) == mission.RequiredQuantity
            ? Pass()
            : Fail("ship purchase lost protected freight");
    }

    private (bool Success, string FailureReason) ValidateShipRejectsInsufficientCapacity()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold cargo = new(20);
        if (!cargo.RegisterFreightReservation(7001, food, 4) || !cargo.AddCommodity(food, 4))
            return Fail("could not stage protected cargo");
        Ship player = new(Vector3.Zero);
        cargo.TransferTo(player.CargoHold, CommodityCatalog.BuildRegistry());
        ShipDealer dealer = new();
        ShipDefinition tiny = new("Tiny Freight", "fixture", "SHIPS/scimitar/Scimitar2", 1000) { CargoCapacity = 1 };
        PlayerCredits credits = new(10_000);
        return !dealer.TryPurchaseShip(tiny, credits, player, out _) &&
            credits.Credits == 10_000
            ? Pass()
            : Fail("insufficient replacement capacity mutated the purchase");
    }

    private (bool Success, string FailureReason) ValidateUnrelatedMarketUnchanged()
    {
        FreightContext context = CreateShortageContext();
        StationMarketListing before = context.Market.GetListingForCommodity(context.Newark, context.Water);
        if (before == null)
            return Fail("water listing unavailable");
        int stock = before.Stock;
        int price = before.BuyPrice;
        return context.Market.GetListingForCommodity(context.Newark, context.Water).Stock == stock &&
            context.Market.GetListingForCommodity(context.Newark, context.Water).BuyPrice == price
            ? Pass()
            : Fail("food shortage changed an unrelated Newark market");
    }

    private (bool Success, string FailureReason) ValidateFailedDeliveryAtomic()
    {
        FreightContext context = PrepareFullFreight(out Mission mission);
        StationMarketListing listing = context.Market.GetListingForCommodity(context.Newark, context.Food);
        int beforeStock = listing.Stock;
        int beforeCargo = context.Player.CargoHold.GetCommodityQuantity(context.Food.Name);
        int beforeCredits = context.PlayerCredits.Credits;
        while (listing.Stock < listing.MaximumStock)
        {
            int step = Math.Min(100, listing.MaximumStock - listing.Stock);
            if (!context.Market.TryAddSupply(context.Newark, context.Food, step, out _))
                return Fail("could not fill destination market for atomicity fixture");
            listing = context.Market.GetListingForCommodity(context.Newark, context.Food);
        }

        bool completed = context.World.NotifyStationDocked(context.Newark);
        return !completed &&
            mission.Status == MissionStatus.InProgress &&
            context.Player.CargoHold.GetCommodityQuantity(context.Food.Name) == beforeCargo &&
            context.PlayerCredits.Credits == beforeCredits &&
            context.Market.GetListingForCommodity(context.Newark, context.Food).Stock == listing.Stock
            ? Pass()
            : Fail("failed delivery partially mutated mission, cargo, credits, or market");
    }

    private string BuildRepresentativeReport()
    {
        FreightContext context = CreateShortageContext();
        Mission mission = FindOffer(context);
        StationMarketListing shortage = context.Market.GetListingForCommodity(context.Newark, context.Food);
        int shortageStock = shortage.Stock;
        int shortageBuyPrice = shortage.BuyPrice;
        int sourceBuyPrice = context.Market.GetListingForCommodity(context.FortBush, context.Food).BuyPrice;
        context.Manager.AcceptMission(mission, context.Newark);
        BuyAt(context, context.FortBush, context.Food, mission.RequiredQuantity, context.PlayerCredits, context.Player.CargoHold);
        int creditsAfterSource = context.PlayerCredits.Credits;
        context.World.NotifyStationDocked(context.Newark);
        StationMarketListing recovered = context.Market.GetListingForCommodity(context.Newark, context.Food);
        return $"Newark Food Rations stock {shortageStock}->{recovered.Stock}, buy price {shortageBuyPrice}->{recovered.BuyPrice}, " +
            $"Fort Bush buy {sourceBuyPrice}, quantity {mission.RequiredQuantity}, reward {mission.Reward:N0} CR, " +
            $"credits after source {creditsAfterSource:N0}, credits after delivery {context.PlayerCredits.Credits:N0}";
    }

    private Mission FindOffer(FreightContext context)
    {
        return context.Manager.GenerateJobBoardMissions(10, context.Newark.FactionId, context.Newark)
            .FirstOrDefault(mission => mission.Type == MissionType.FreightContract &&
                string.Equals(mission.CommodityId, context.Food.Id, StringComparison.OrdinalIgnoreCase));
    }

    private FreightContext PrepareAccepted(out Mission mission)
    {
        FreightContext context = CreateShortageContext();
        mission = FindOffer(context);
        if (mission == null || !context.Manager.AcceptMission(mission, context.Newark))
            throw new InvalidOperationException("could not accept shortage freight");
        return context;
    }

    private FreightContext PrepareFullFreight(out Mission mission)
    {
        FreightContext context = PrepareAccepted(out mission);
        int quantityToBuy = mission.RequiredQuantity - context.Player.CargoHold.GetMissionCargoQuantity(mission.Id);
        if (quantityToBuy > 0 && !BuyAt(context, context.FortBush, context.Food, quantityToBuy, context.PlayerCredits, context.Player.CargoHold))
            throw new InvalidOperationException("could not source food at Fort Bush");
        return context;
    }

    private (FreightContext Context, SaveGameData Data, string Path) BuildSavedPartialContext()
    {
        FreightContext source = CreateShortageContext();
        Mission mission = FindOffer(source);
        if (mission == null ||
            !source.Player.CargoHold.AddCommodity(source.Food, mission.RequiredQuantity + 5) ||
            !source.Manager.AcceptMission(mission, source.Newark))
            throw new InvalidOperationException("could not stage saved freight");

        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-freight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "freight.json");
        SaveGameManager saver = new(path);
        SaveGameData data = new()
        {
            PlayerCredits = source.PlayerCredits.Credits,
            Cargo = saver.CaptureCargo(source.Player.CargoHold),
            ActiveMissions = saver.CaptureMissions(source.Manager.ActiveMissions),
            StationMarkets = source.Market.CaptureRuntimeState()
        };
        string saveFailure;
        string loadFailure = string.Empty;
        if (!saver.TrySave(data, out saveFailure) || !saver.TryLoad(out data, out loadFailure))
            throw new InvalidOperationException($"save/load failed: {saveFailure} {loadFailure}");
        return (source, data, path);
    }

    private static FreightContext CreateShortageContext()
    {
        FreightContext context = CreateContext();
        if (!BuyAt(context, context.Newark, context.Food, 140, context.SinkCredits, context.SinkCargo))
            throw new InvalidOperationException("could not create Newark food shortage");
        return context;
    }

    private static FreightContext CreateContext()
    {
        FreightContext context = new()
        {
            Market = new MarketManager(),
            PlayerCredits = new PlayerCredits(1_000_000),
            Player = new Ship(Vector3.Zero),
            SinkCredits = new PlayerCredits(1_000_000),
            SinkCargo = new CargoHold(1_000),
            Food = CommodityCatalog.GetById("food-rations"),
            Water = CommodityCatalog.GetById("water"),
            Newark = CreateStation("Newark Station", 1),
            FortBush = CreateStation("Fort Bush", 1)
        };
        context.Stations.Add(context.Newark);
        context.Stations.Add(context.FortBush);
        context.Manager = new MissionManager(
            context.PlayerCredits,
            null,
            null,
            context.Market,
            context.Player.CargoHold);
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

    private static bool BuyAt(FreightContext context, Station station, Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargo)
    {
        return context.Market.TryBuy(station, commodity, quantity, credits, cargo, out _);
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

    private static void Cleanup(string path)
    {
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch
        {
        }
    }

    private sealed class FreightContext
    {
        public MarketManager Market { get; set; }
        public MissionManager Manager { get; set; }
        public MissionWorldManager World { get; set; }
        public PlayerCredits PlayerCredits { get; set; }
        public PlayerCredits SinkCredits { get; set; }
        public CargoHold SinkCargo { get; set; }
        public Ship Player { get; set; }
        public Commodity Food { get; set; }
        public Commodity Water { get; set; }
        public Station Newark { get; set; }
        public Station FortBush { get; set; }
        public List<Station> Stations { get; } = new();
        public List<NpcShip> NpcShips { get; } = new();
        public List<SpaceObject> SpaceObjects { get; } = new();
    }
}
