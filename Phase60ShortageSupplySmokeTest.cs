using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 60 integration coverage. Every shortage assertion reads the
/// production MarketManager listing; the mission is only the agreement that
/// consumes clean fungible cargo and replenishes that same listing.
/// </summary>
internal sealed class Phase60ShortageSupplySmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(HealthyStockIsNormal, "healthy stock is normal", ref passed, ref failed);
        RunCase(ShortageCrossesEnterThreshold, "shortage crosses enter threshold", ref passed, ref failed);
        RunCase(RecoveryUsesHysteresis, "recovery uses hysteresis", ref passed, ref failed);
        RunCase(ContrabandIsExcluded, "contraband is excluded", ref passed, ref failed);
        RunCase(InvalidCommodityIsExcluded, "invalid commodity is excluded", ref passed, ref failed);
        RunCase(NoMarketStationIsExcluded, "station without market is excluded", ref passed, ref failed);
        RunCase(PlayerPurchaseChangesShortage, "player purchase changes stock", ref passed, ref failed);
        RunCase(PlayerSaleImprovesShortage, "player sale improves stock", ref passed, ref failed);
        RunCase(MaturityBlocksImmediateOffer, "maturity blocks immediate offer", ref passed, ref failed);
        RunCase(MatureShortageCreatesOffer, "mature shortage creates offer", ref passed, ref failed);
        RunCase(HealthyCommodityCreatesNoOffer, "healthy commodity creates no offer", ref passed, ref failed);
        RunCase(OfferEmployerMatchesMarketFaction, "employer matches market faction", ref passed, ref failed);
        RunCase(OfferDestinationIsShortageStation, "destination is shortage station", ref passed, ref failed);
        RunCase(QuantityIsBoundedAndUseful, "quantity is bounded and useful", ref passed, ref failed);
        RunCase(RewardIsDeterministic, "reward is deterministic", ref passed, ref failed);
        RunCase(SuggestedSourceIsValid, "suggested source is valid", ref passed, ref failed);
        RunCase(OfferCountIsBoundedAndOrdered, "offer count and ordering are bounded", ref passed, ref failed);
        RunCase(StaleOfferCannotBeAccepted, "stale offer cannot be accepted", ref passed, ref failed);
        RunCase(AcceptedContractSurvivesRecovery, "accepted contract survives recovery", ref passed, ref failed);
        RunCase(PreOwnedCleanCargoCounts, "pre-owned clean cargo counts", ref passed, ref failed);
        RunCase(StolenCargoDoesNotCount, "stolen cargo does not count", ref passed, ref failed);
        RunCase(MixedCargoReportsEligibleAmount, "mixed cargo reports eligible amount", ref passed, ref failed);
        RunCase(PartialDeliveryDoesNotMutate, "partial delivery does not mutate", ref passed, ref failed);
        RunCase(DeliveryConsumesExactCleanQuantity, "delivery consumes exact clean quantity", ref passed, ref failed);
        RunCase(DestinationStockIncreasesExactly, "destination stock increases exactly", ref passed, ref failed);
        RunCase(RewardAndReputationApplyOnce, "reward and reputation apply once", ref passed, ref failed);
        RunCase(NormalSaleCannotDoubleUseCargo, "ordinary sale cannot double-use cargo", ref passed, ref failed);
        RunCase(DeliveryCanClearShortage, "delivery can clear shortage", ref passed, ref failed);
        RunCase(SmallDeliveryMayLeaveShortage, "small delivery may leave shortage", ref passed, ref failed);
        RunCase(AcceptedCapacitySurvivesAmbientSupply, "accepted capacity survives ambient supply", ref passed, ref failed);
        RunCase(MarketPriceUsesExistingRules, "price uses existing market rules", ref passed, ref failed);
        RunCase(MarketBoundsRemainSafe, "market bounds remain safe", ref passed, ref failed);
        RunCase(Phase59LossIsObservedFromStock, "Phase 59 loss is observed from stock", ref passed, ref failed);
        RunCase(Phase59DeliveryCanPreventOffer, "Phase 59 delivery can prevent offer", ref passed, ref failed);
        RunCase(SaveLoadPreservesMaturityAndMission, "save/load preserves maturity and mission", ref passed, ref failed);
        RunCase(SaveBeforeDeliveryDoesNotDuplicate, "save before delivery does not duplicate", ref passed, ref failed);
        RunCase(SaveAfterDeliveryCannotRepeat, "save after delivery cannot repeat", ref passed, ref failed);
        RunCase(ResetClearsAcceptedContract, "reset clears accepted contract", ref passed, ref failed);

        Console.WriteLine($"[PHASE 60 SHORTAGE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 60 SHORTAGE SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 60 SHORTAGE SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 60 SHORTAGE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) HealthyStockIsNormal()
    {
        Context c = CreateContext();
        MarketShortageState state = c.Market.GetShortageState(c.Newark, c.Food);
        return state != null && !state.IsShortage && state.Level == MarketShortageLevel.Normal
            ? Pass() : Fail("healthy baseline listing was classified as a shortage");
    }

    private (bool Success, string FailureReason) ShortageCrossesEnterThreshold()
    {
        Context c = CreateContext();
        StageShortage(c);
        MarketShortageState state = c.Market.GetShortageState(c.Newark, c.Food);
        return state?.IsShortage == true && state.StockPercent <= MarketShortagePolicy.EnterThresholdPercent
            ? Pass() : Fail("real market stock did not enter shortage");
    }

    private (bool Success, string FailureReason) RecoveryUsesHysteresis()
    {
        Context c = CreateContext();
        StageShortage(c);
        if (c.Market.TryAddSupply(c.Newark, c.Food, 35, out _) == false)
            return Fail("could not move stock into the hysteresis band");
        MarketShortageState latched = c.Market.GetShortageState(c.Newark, c.Food);
        if (latched?.IsShortage != true)
            return Fail("shortage cleared below recovery threshold");
        if (!c.Market.TryAddSupply(c.Newark, c.Food, 45, out _))
            return Fail("could not move stock above recovery threshold");
        return c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage == false
            ? Pass() : Fail("shortage did not clear at recovery threshold");
    }

    private (bool Success, string FailureReason) ContrabandIsExcluded()
    {
        Context c = CreateContext();
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        return contraband != null && contraband.IsContraband && c.Market.GetShortageState(c.Newark, contraband) == null
            ? Pass() : Fail("contraband received a lawful shortage state");
    }

    private (bool Success, string FailureReason) InvalidCommodityIsExcluded()
    {
        Context c = CreateContext();
        Commodity invalid = new()
        {
            Id = "invalid-phase60",
            Name = "Invalid Phase 60 Good",
            BasePrice = 1,
            VolumePerUnit = 1,
            Category = "Ordinary"
        };
        return c.Market.GetShortageState(c.Newark, invalid) == null
            ? Pass() : Fail("invalid commodity resolved to a market shortage");
    }

    private (bool Success, string FailureReason) NoMarketStationIsExcluded()
    {
        Context c = CreateContext();
        return !c.Market.HasMarketConfigForStation(c.NoMarket) &&
            c.Market.GetShortageStatesForStation(c.NoMarket).Count == 0 &&
            c.Manager.GenerateJobBoardMissions(10, c.NoMarket.FactionId, c.NoMarket)
                .All(mission => mission.Type != MissionType.EmergencySupply)
            ? Pass() : Fail("station without a configured market generated supply work");
    }

    private (bool Success, string FailureReason) PlayerPurchaseChangesShortage()
    {
        Context c = CreateContext();
        int before = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
        if (!c.Market.TryBuy(c.Newark, c.Food, 180, c.SinkCredits, c.SinkCargo, out _))
            return Fail("ordinary market purchase failed");
        int after = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
        return after == before - 180 && c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage == true
            ? Pass() : Fail("player purchase did not drive the authoritative listing into shortage");
    }

    private (bool Success, string FailureReason) PlayerSaleImprovesShortage()
    {
        Context c = CreateContext();
        StageShortage(c);
        int before = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
        if (!c.Player.CargoHold.AddCommodity(c.Food, 45) ||
            !c.Market.TrySell(c.Newark, c.Food, 45, c.PlayerCredits, c.Player.CargoHold, out _))
            return Fail("ordinary player sale failed");
        int after = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
        return after == before + 45 && after > before
            ? Pass() : Fail("player sale did not improve real station stock");
    }

    private (bool Success, string FailureReason) MaturityBlocksImmediateOffer()
    {
        Context c = CreateContext();
        StageShortage(c);
        return FindOffer(c) == null && c.Market.GetShortageState(c.Newark, c.Food)?.IsMature == false
            ? Pass() : Fail("fresh shortage generated an immediate contract");
    }

    private (bool Success, string FailureReason) MatureShortageCreatesOffer()
    {
        Context c = CreateMatureShortageContext();
        Mission offer = FindOffer(c);
        return offer != null && offer.Type == MissionType.EmergencySupply &&
            offer.CommodityId == c.Food.Id && offer.DestinationStationId == Mission.BuildStationIdentity(c.Newark)
            ? Pass() : Fail("mature real shortage did not create Emergency Supply");
    }

    private (bool Success, string FailureReason) HealthyCommodityCreatesNoOffer()
    {
        Context c = CreateContext();
        return FindOffer(c) == null && c.Manager.GenerateJobBoardMissions(10, c.Newark.FactionId, c.Newark)
            .All(mission => mission.Type != MissionType.EmergencySupply)
            ? Pass() : Fail("healthy stock created an emergency offer");
    }

    private (bool Success, string FailureReason) OfferEmployerMatchesMarketFaction()
    {
        Context c = CreateMatureShortageContext();
        Mission offer = FindOffer(c);
        return offer != null && string.Equals(offer.FactionId, c.Market.GetMarketFactionId(c.Newark), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(offer.OfferedBy, $"{c.Newark.Name} Authority", StringComparison.Ordinal)
            ? Pass() : Fail("emergency employer did not match the shortage station authority");
    }

    private (bool Success, string FailureReason) OfferDestinationIsShortageStation()
    {
        Context c = CreateMatureShortageContext();
        Mission offer = FindOffer(c);
        return offer != null && offer.Destination == c.Newark.Name &&
            offer.DestinationStationId == Mission.BuildStationIdentity(c.Newark) &&
            offer.Target == c.Food.Name
            ? Pass() : Fail("emergency destination or commodity did not match shortage listing");
    }

    private (bool Success, string FailureReason) QuantityIsBoundedAndUseful()
    {
        Context c = CreateMatureShortageContext();
        Mission offer = FindOffer(c);
        MarketShortageState state = c.Market.GetShortageState(c.Newark, c.Food);
        return offer != null && state != null && offer.RequiredQuantity >= 2 &&
            offer.RequiredQuantity <= MissionManager.FreightMaximumUnits &&
            offer.RequiredQuantity <= state.Deficit &&
            offer.RequiredQuantity * c.Food.VolumePerUnit <= MissionManager.FreightMaximumCargoVolume
            ? Pass() : Fail("quantity was outside useful deficit/cargo bounds");
    }

    private (bool Success, string FailureReason) RewardIsDeterministic()
    {
        Mission first = FindOffer(CreateMatureShortageContext());
        Mission second = FindOffer(CreateMatureShortageContext());
        return first != null && second != null && first.RequiredQuantity == second.RequiredQuantity && first.Reward == second.Reward
            ? Pass() : Fail("same real market state produced different reward terms");
    }

    private (bool Success, string FailureReason) SuggestedSourceIsValid()
    {
        Context c = CreateMatureShortageContext();
        Mission offer = FindOffer(c);
        Station source = c.Stations.FirstOrDefault(station =>
            string.Equals(station.Name, offer?.SourceStationName, StringComparison.OrdinalIgnoreCase));
        StationMarketListing listing = source == null ? null : c.Market.GetListingForCommodity(source, c.Food);
        return offer != null && source != null && !ReferenceEquals(source, c.Newark) && listing != null &&
            listing.IsAvailable && listing.Stock - listing.MinimumStock >= offer.RequiredQuantity
            ? Pass() : Fail("suggested source was not a real stocked ordinary listing");
    }

    private (bool Success, string FailureReason) OfferCountIsBoundedAndOrdered()
    {
        Context c = CreateContext();
        StageShortage(c, c.Food, 195, mature: false);
        StageShortage(c, c.Water, 180, mature: false);
        StageShortage(c, c.HFuel, 165, mature: false);
        c.Market.AdvanceTime(MarketShortagePolicy.MaturitySeconds + 1);
        IReadOnlyList<MarketShortageState> states = c.Market.GetShortageStatesForStation(c.Newark, true);
        List<Mission> offers = c.Manager.GenerateJobBoardMissions(10, c.Newark.FactionId, c.Newark)
            .Where(mission => mission.Type == MissionType.EmergencySupply).ToList();
        string actualOrder = string.Join(",", states.Select(state => state.Commodity.Id));
        string expectedOrder = string.Join(",", states.OrderByDescending(state => state.Level)
            .ThenByDescending(state => state.Deficit)
            .ThenBy(state => state.Commodity.Id, StringComparer.OrdinalIgnoreCase)
            .Select(state => state.Commodity.Id));
        return states.Count <= MarketShortagePolicy.MaximumOffersPerStation &&
            offers.Count <= MarketShortagePolicy.MaximumOffersPerStation &&
            actualOrder == expectedOrder
            ? Pass() : Fail("shortage/offer selection exceeded or ignored deterministic bounds");
    }

    private (bool Success, string FailureReason) StaleOfferCannotBeAccepted()
    {
        Context c = CreateMatureShortageContext();
        Mission offer = FindOffer(c);
        if (offer == null || !c.Market.TryAddSupply(c.Newark, c.Food, 60, out _))
            return Fail("could not prepare a stale offer fixture");
        return !c.Manager.AcceptMission(offer, c.Newark) && offer.Status == MissionStatus.Available
            ? Pass() : Fail("recovered shortage offer was accepted");
    }

    private (bool Success, string FailureReason) AcceptedContractSurvivesRecovery()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Market.TryAddSupply(c.Newark, c.Food, 60, out _))
            return Fail("accepted emergency contract could not be staged");
        return mission.Status == MissionStatus.InProgress && c.Manager.ActiveMission == mission &&
            c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage == false
            ? Pass() : Fail("ambient recovery invalidated the accepted agreement");
    }

    private (bool Success, string FailureReason) PreOwnedCleanCargoCounts()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        return mission != null && c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity) &&
            c.Manager.AcceptMission(mission, c.Newark) && c.Manager.GetEmergencySupplyEligibleQuantity(mission) == mission.RequiredQuantity
            ? Pass() : Fail("pre-owned clean commodity did not count toward emergency supply");
    }

    private (bool Success, string FailureReason) StolenCargoDoesNotCount()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Player.CargoHold.AddStolenCommodity(c.Food, mission.RequiredQuantity))
            return Fail("could not stage stolen-only fixture");
        int stock = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
        return !c.World.NotifyStationDocked(c.Newark) && mission.Status == MissionStatus.InProgress &&
            c.Player.CargoHold.GetStolenCommodityQuantity(c.Food.Name) == mission.RequiredQuantity &&
            c.Market.GetListingForCommodity(c.Newark, c.Food).Stock == stock
            ? Pass() : Fail("stolen supply was accepted or mutated");
    }

    private (bool Success, string FailureReason) MixedCargoReportsEligibleAmount()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity - 1) ||
            !c.Player.CargoHold.AddStolenCommodity(c.Food, mission.RequiredQuantity))
            return Fail("could not stage mixed provenance fixture");
        return c.Manager.GetEmergencySupplyEligibleQuantity(mission) == mission.RequiredQuantity - 1
            ? Pass() : Fail("eligible clean quantity included stolen copies");
    }

    private (bool Success, string FailureReason) PartialDeliveryDoesNotMutate()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity - 1))
            return Fail("could not stage partial fixture");
        int stock = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
        int credits = c.PlayerCredits.Credits;
        return !c.World.NotifyStationDocked(c.Newark) && mission.Status == MissionStatus.InProgress &&
            c.Player.CargoHold.GetCommodityQuantity(c.Food.Name) == mission.RequiredQuantity - 1 &&
            c.Market.GetListingForCommodity(c.Newark, c.Food).Stock == stock && c.PlayerCredits.Credits == credits
            ? Pass() : Fail("partial delivery mutated mission, cargo, market, or credits");
    }

    private (bool Success, string FailureReason) DeliveryConsumesExactCleanQuantity()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity) ||
            !c.Player.CargoHold.AddStolenCommodity(c.Food, 2))
            return Fail("could not stage clean/stolen delivery fixture");
        if (!c.World.NotifyStationDocked(c.Newark))
            return Fail("clean emergency delivery failed");
        return c.Player.CargoHold.GetCleanCommodityQuantity(c.Food.Name) == 0 &&
            c.Player.CargoHold.GetStolenCommodityQuantity(c.Food.Name) == 2
            ? Pass() : Fail("delivery removed stolen or incorrect cargo quantity");
    }

    private (bool Success, string FailureReason) DestinationStockIncreasesExactly()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity))
            return Fail("could not stage exact delivery fixture");
        int before = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
        if (!c.World.NotifyStationDocked(c.Newark))
            return Fail("exact delivery failed");
        return c.Market.GetListingForCommodity(c.Newark, c.Food).Stock == before + mission.RequiredQuantity
            ? Pass() : Fail("destination stock did not increase by exact delivery quantity");
    }

    private (bool Success, string FailureReason) RewardAndReputationApplyOnce()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity))
            return Fail("could not stage reward fixture");
        int credits = c.PlayerCredits.Credits;
        float standing = c.Reputation.GetStanding(mission.FactionId);
        if (!c.World.NotifyStationDocked(c.Newark))
            return Fail("reward delivery failed");
        float afterStanding = c.Reputation.GetStanding(mission.FactionId);
        int afterCredits = c.PlayerCredits.Credits;
        bool repeated = c.World.NotifyStationDocked(c.Newark);
        return !repeated && mission.Status == MissionStatus.Completed && mission.RewardPaid &&
            afterCredits == credits + mission.Reward &&
            Math.Abs(afterStanding - standing - MissionManager.GetMissionReputationReward(mission)) < 0.0001f
            ? Pass() : Fail("reward or employer reputation was not applied exactly once");
    }

    private (bool Success, string FailureReason) NormalSaleCannotDoubleUseCargo()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
            !c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity))
            return Fail("could not stage sale/delivery fixture");
        if (!c.Market.TrySell(c.Newark, c.Food, 1, c.PlayerCredits, c.Player.CargoHold, out _))
            return Fail("ordinary sale failed");
        return !c.World.NotifyStationDocked(c.Newark) && mission.Status == MissionStatus.InProgress &&
            c.Player.CargoHold.GetCleanCommodityQuantity(c.Food.Name) == mission.RequiredQuantity - 1
            ? Pass() : Fail("sold cargo was also accepted by emergency delivery");
    }

    private (bool Success, string FailureReason) DeliveryCanClearShortage()
    {
        Context c = CreateMatureShortageContext();
        for (int delivery = 0; delivery < 10; delivery++)
        {
            if (c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage != true)
                break;
            Mission mission = FindOffer(c);
            if (mission == null || !c.Manager.AcceptMission(mission, c.Newark) ||
                !c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity) ||
                !c.World.NotifyStationDocked(c.Newark))
                return Fail("delivery did not replenish destination");
        }
        return c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage == false
            ? Pass() : Fail("delivery did not naturally clear the configured shortage");
    }

    private (bool Success, string FailureReason) SmallDeliveryMayLeaveShortage()
    {
        Context c = CreateContext();
        StageShortage(c, c.Food, 195, mature: false);
        c.Market.AdvanceTime(MarketShortagePolicy.MaturitySeconds + 1);
        Mission mission = FindOffer(c);
        if (mission == null)
            return Fail("small shortage offer was not generated");
        // This direct policy check keeps the test independent of reward terms:
        // the agreed quantity is bounded, while a larger deficit remains.
        return c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage == true && mission.RequiredQuantity < 77
            ? Pass() : Fail("bounded delivery unexpectedly forced a large shortage to clear");
    }

    private (bool Success, string FailureReason) AcceptedCapacitySurvivesAmbientSupply()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark))
            return Fail("could not accept capacity-guaranteed mission");
        int ambientCapacity = c.Market.GetAvailableSupplyCapacity(c.Newark, c.Food);
        if (ambientCapacity > 0 && !c.Market.TryAddSupply(c.Newark, c.Food, ambientCapacity, out _))
            return Fail("ambient supply could not use unreserved capacity");
        if (!c.Player.CargoHold.AddCommodity(c.Food, mission.RequiredQuantity) || !c.World.NotifyStationDocked(c.Newark))
            return Fail("accepted mission became impossible after ambient supply");
        return mission.Status == MissionStatus.Completed && c.Market.GetListingForCommodity(c.Newark, c.Food).Stock <=
            c.Market.GetListingForCommodity(c.Newark, c.Food).MaximumStock
            ? Pass() : Fail("accepted delivery exceeded destination capacity");
    }

    private (bool Success, string FailureReason) MarketPriceUsesExistingRules()
    {
        Context c = CreateMatureShortageContext();
        int before = c.Market.GetListingForCommodity(c.Newark, c.Food).BuyPrice;
        if (!c.Market.TryAddSupply(c.Newark, c.Food, 60, out _))
            return Fail("could not add normal market supply");
        int after = c.Market.GetListingForCommodity(c.Newark, c.Food).BuyPrice;
        return after < before ? Pass() : Fail($"stock recovery did not use existing price response: {before}->{after}");
    }

    private (bool Success, string FailureReason) MarketBoundsRemainSafe()
    {
        Context c = CreateContext();
        StationMarketListing listing = c.Market.GetListingForCommodity(c.Newark, c.Food);
        bool overBuy = !c.Market.TryBuy(c.Newark, c.Food, listing.Stock + 1, c.SinkCredits, c.SinkCargo, out _);
        bool overAdd = !c.Market.TryAddSupply(c.Newark, c.Food, listing.MaximumStock + 1, out _);
        return overBuy && overAdd && listing.Stock >= listing.MinimumStock && listing.Stock <= listing.MaximumStock
            ? Pass() : Fail("market accepted negative or overflowing stock");
    }

    private (bool Success, string FailureReason) Phase59LossIsObservedFromStock()
    {
        Context c = CreateMatureShortageContext();
        EconomicShipmentManager economy = CreateEconomy(c, out NpcShip trader, out TrafficZoneConfig route);
        StationMarketListing before = c.Market.GetListingForCommodity(c.Newark, c.Food);
        if (!economy.TryAttachTrader(trader, route, out EconomicShipment shipment))
            return Fail("Phase 59 shipment could not be attached");
        economy.NotifyTraderDestroyed(trader);
        economy.FinalizeDestroyedTrader(trader);
        economy.NotifyRouteEndpointReached(trader, true);
        return c.Market.GetListingForCommodity(c.Newark, c.Food).Stock == before.Stock &&
            c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage == true && shipment.Settlement == EconomicShipmentSettlement.Lost
            ? Pass() : Fail("lost economic shipment changed destination stock or bypassed shortage derivation");
    }

    private (bool Success, string FailureReason) Phase59DeliveryCanPreventOffer()
    {
        Context c = CreateMatureShortageContext();
        if (FindOffer(c) == null || !c.Market.TryAddSupply(c.Newark, c.Food, 60, out _))
            return Fail("could not model successful shared-market delivery");
        return c.Market.GetShortageState(c.Newark, c.Food)?.IsShortage == false && FindOffer(c) == null
            ? Pass() : Fail("recovered real market still generated an emergency offer");
    }

    private (bool Success, string FailureReason) SaveLoadPreservesMaturityAndMission()
    {
        Context source = CreateMatureShortageContext();
        Mission mission = FindOffer(source);
        if (mission == null || !source.Manager.AcceptMission(mission, source.Newark))
            return Fail("could not stage save/load mission");
        string path = CreateTempSavePath("maturity");
        try
        {
            SaveGameManager saver = new(path);
            SaveGameData data = Capture(source, saver, includeCompleted: false);
            if (!saver.TrySave(data, out string saveFailure))
                return Fail($"save failed: {saveFailure}");
            if (!saver.TryLoad(out data, out string loadFailure))
                return Fail($"save/load failed: {saveFailure} {loadFailure}");
            Context resumed = CreateContext();
            resumed.Market.RestoreElapsedMilliseconds(data.MarketElapsedMilliseconds);
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out List<string> cargoWarnings);
            saver.ApplyMissions(resumed.Manager, data, out List<string> missionWarnings);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            MarketShortageState state = resumed.Market.GetShortageState(resumed.Newark, resumed.Food);
            return cargoWarnings.Count == 0 && missionWarnings.Count == 0 && state?.IsMature == true &&
                resumed.Manager.ActiveMission?.Id == mission.Id && resumed.Manager.ActiveMission.RequiredQuantity == mission.RequiredQuantity
                ? Pass() : Fail("save/load did not preserve shortage maturity or accepted terms");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private (bool Success, string FailureReason) SaveBeforeDeliveryDoesNotDuplicate()
    {
        Context source = CreateMatureShortageContext();
        Mission mission = FindOffer(source);
        if (mission == null || !source.Manager.AcceptMission(mission, source.Newark) ||
            !source.Player.CargoHold.AddCommodity(source.Food, mission.RequiredQuantity))
            return Fail("could not stage pre-delivery save");
        string path = CreateTempSavePath("before-delivery");
        try
        {
            SaveGameManager saver = new(path);
            SaveGameData data = Capture(source, saver, false);
            if (!saver.TrySave(data, out _) || !saver.TryLoad(out data, out _))
                return Fail("pre-delivery save/load failed");
            Context resumed = CreateContext();
            resumed.Market.RestoreElapsedMilliseconds(data.MarketElapsedMilliseconds);
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            Mission loaded = resumed.Manager.ActiveMission;
            int loadedCargo = resumed.Player.CargoHold.GetCleanCommodityQuantity(resumed.Food.Name);
            int beforeStock = resumed.Market.GetListingForCommodity(resumed.Newark, resumed.Food).Stock;
            if (loaded == null || loadedCargo != loaded.RequiredQuantity || !resumed.World.NotifyStationDocked(resumed.Newark))
                return Fail("loaded mission was not completable with one exact cargo copy");
            return resumed.Player.CargoHold.GetCommodityQuantity(resumed.Food.Name) == 0 &&
                resumed.Market.GetListingForCommodity(resumed.Newark, resumed.Food).Stock == beforeStock + loaded.RequiredQuantity
                ? Pass() : Fail("pre-delivery save/load duplicated or lost cargo/stock");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private (bool Success, string FailureReason) SaveAfterDeliveryCannotRepeat()
    {
        Context source = CreateMatureShortageContext();
        Mission mission = FindOffer(source);
        if (mission == null || !source.Manager.AcceptMission(mission, source.Newark) ||
            !source.Player.CargoHold.AddCommodity(source.Food, mission.RequiredQuantity) ||
            !source.World.NotifyStationDocked(source.Newark))
            return Fail("could not complete mission before post-delivery save");
        int credits = source.PlayerCredits.Credits;
        int stock = source.Market.GetListingForCommodity(source.Newark, source.Food).Stock;
        string path = CreateTempSavePath("after-delivery");
        try
        {
            SaveGameManager saver = new(path);
            SaveGameData data = Capture(source, saver, includeCompleted: true);
            if (!saver.TrySave(data, out _) || !saver.TryLoad(out data, out _))
                return Fail("post-delivery save/load failed");
            Context resumed = CreateContext();
            resumed.PlayerCredits.AddCredits(data.PlayerCredits - resumed.PlayerCredits.Credits);
            resumed.Market.RestoreRuntimeState(data.StationMarkets);
            saver.ApplyCargo(resumed.Player.CargoHold, data, out _);
            saver.ApplyMissions(resumed.Manager, data, out _);
            return resumed.Manager.ActiveMission == null && resumed.PlayerCredits.Credits == credits &&
                resumed.Market.GetListingForCommodity(resumed.Newark, resumed.Food).Stock == stock && credits > 1_000_000 &&
                resumed.World.NotifyStationDocked(resumed.Newark) == false
                ? Pass() : Fail("post-delivery load made the reward or stock repeatable");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private (bool Success, string FailureReason) ResetClearsAcceptedContract()
    {
        Context c = CreateMatureShortageContext();
        Mission mission = FindOffer(c);
        if (mission == null || !c.Manager.AcceptMission(mission, c.Newark))
            return Fail("could not stage reset fixture");
        c.Manager.ClearState();
        return c.Manager.ActiveMission == null && c.Market.GetAvailableSupplyCapacity(c.Newark, c.Food) >=
            c.Market.GetListingForCommodity(c.Newark, c.Food).MaximumStock - c.Market.GetListingForCommodity(c.Newark, c.Food).Stock
            ? Pass() : Fail("reset retained the accepted contract or its capacity reservation");
    }

    private static Context CreateMatureShortageContext()
    {
        Context c = CreateContext();
        StageShortage(c);
        c.Market.AdvanceTime(MarketShortagePolicy.MaturitySeconds + 1);
        return c;
    }

    private static void StageShortage(Context c, Commodity commodity = null, int purchaseQuantity = 195, bool mature = false)
    {
        commodity ??= c.Food;
        if (!c.Market.TryBuy(c.Newark, commodity, purchaseQuantity, c.SinkCredits, c.SinkCargo, out string failure))
            throw new InvalidOperationException($"could not stage shortage: {failure}");
        c.Market.GetShortageState(c.Newark, commodity);
        if (mature)
            c.Market.AdvanceTime(MarketShortagePolicy.MaturitySeconds + 1);
    }

    private static Mission FindOffer(Context c)
    {
        return c.Manager.GenerateJobBoardMissions(10, c.Newark.FactionId, c.Newark)
            .FirstOrDefault(mission => mission.Type == MissionType.EmergencySupply &&
                string.Equals(mission.CommodityId, c.Food.Id, StringComparison.OrdinalIgnoreCase));
    }

    private static EconomicShipmentManager CreateEconomy(Context c, out NpcShip trader, out TrafficZoneConfig route)
    {
        route = new TrafficZoneConfig
        {
            Id = "phase60-shortage-route",
            Name = "Phase 60 Shortage Route",
            BehaviorType = TrafficZoneBehaviorType.TraderRoute,
            OriginStationId = "fort_bush",
            DestinationStationId = "newark_station",
            RouteStartX = c.FortBush.Position.X,
            RouteStartY = c.FortBush.Position.Y,
            RouteStartZ = c.FortBush.Position.Z,
            RouteEndX = c.Newark.Position.X,
            RouteEndY = c.Newark.Position.Y,
            RouteEndZ = c.Newark.Position.Z
        };
        trader = new NpcShip("Phase 60 Trader", c.FortBush.Position, Vector3.Zero, 1000f, 0.1f, FactionManager.NeutralCivilians);
        trader.ConfigureTrafficBehavior(
            TrafficZoneBehaviorType.TraderRoute,
            route.Id,
            Vector3.Zero,
            1000f,
            190f,
            3500f,
            route.RouteStart,
            route.RouteEnd);
        c.Npcs.Add(trader);
        c.Objects.Add(trader);
        return new EconomicShipmentManager(c.Market, () => c.Stations);
    }

    private static SaveGameData Capture(Context c, SaveGameManager saver, bool includeCompleted)
    {
        return new SaveGameData
        {
            PlayerCredits = c.PlayerCredits.Credits,
            Cargo = saver.CaptureCargo(c.Player.CargoHold),
            ActiveMissions = saver.CaptureMissions(c.Manager.ActiveMissions),
            CompletedMissions = includeCompleted ? saver.CaptureMissions(c.Manager.CompletedMissions) : new List<SaveMissionData>(),
            StationMarkets = c.Market.CaptureRuntimeState(),
            MarketElapsedMilliseconds = c.Market.ElapsedMilliseconds
        };
    }

    private static Context CreateContext()
    {
        Context c = new();
        c.Reputation.SetReputation(FactionManager.LibertyCorporations, 0.50f);
        c.Manager.SetReputationManager(c.Reputation);
        c.Stations.Add(c.Newark);
        c.Stations.Add(c.FortBush);
        c.Stations.Add(c.Rochester);
        c.Stations.Add(c.NoMarket);
        c.World = new MissionWorldManager(
            c.Manager,
            new MissionWaypointSystem(),
            c.Player,
            c.Npcs,
            c.Objects,
            () => c.Stations,
            null,
            c.Market);
        c.Manager.SetWorldManager(c.World);
        return c;
    }

    private static Station CreateStation(string name, int systemIndex, string factionId = FactionManager.LibertyCorporations)
    {
        return new Station(new StationConfig
        {
            Description = name,
            SystemIndex = systemIndex,
            StartupPositionX = systemIndex * 5_000f,
            StartupPositionY = 0f,
            StartupPositionZ = systemIndex * -3_000f,
            Radius = 1_000f,
            DockingRange = 800f,
            FactionId = factionId
        }, null);
    }

    private static string CreateTempSavePath(string label)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-phase60-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "phase60.json");
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

    private sealed class Context
    {
        public MarketManager Market { get; } = RunSilenced(() => new MarketManager());
        public PlayerCredits PlayerCredits { get; } = new(1_000_000);
        public PlayerCredits SinkCredits { get; } = new(2_000_000);
        public CargoHold SinkCargo { get; } = new(10_000);
        public Ship Player { get; } = new(Vector3.Zero);
        public ReputationManager Reputation { get; } = new(new FactionManager());
        public Commodity Food { get; } = CommodityCatalog.GetById("food-rations");
        public Commodity Water { get; } = CommodityCatalog.GetById("water");
        public Commodity HFuel { get; } = CommodityCatalog.GetById("h-fuel");
        public Station Newark { get; } = CreateStation("Newark Station", 1);
        public Station FortBush { get; } = CreateStation("Fort Bush", 2);
        public Station Rochester { get; } = CreateStation("Rochester Base", 3);
        public Station NoMarket { get; } = CreateStation("Phase 60 No Market", 4);
        public MissionManager Manager { get; }
        public MissionWorldManager World { get; set; }
        public List<Station> Stations { get; } = new();
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();

        public Context()
        {
            Manager = new MissionManager(PlayerCredits, null, Reputation, Market, Player.CargoHold);
        }
    }
}
