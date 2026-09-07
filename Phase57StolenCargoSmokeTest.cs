using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 57 proof for the clean/stolen distinction. The cases use
/// CargoHold, CargoPod, LootManager, CommodityDealer, and Police authorities
/// directly so a passing result cannot come from presentation-only metadata.
/// </summary>
internal sealed class Phase57StolenCargoSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("legal commodity remains non-contraband", LegalCommodityIsCleanCatalogData, ref passed, ref failed);
        RunCase("clean and stolen quantities coexist", CleanAndStolenCoexist, ref passed, ref failed);
        RunCase("stolen cargo uses ordinary capacity", StolenUsesNormalCapacity, ref passed, ref failed);
        RunCase("Phase 56 surrendered pod is stolen", ExtortionPodIsStolen, ref passed, ref failed);
        RunCase("stolen pod pickup preserves provenance", StolenPodPickupPreservesProvenance, ref passed, ref failed);
        RunCase("jettison and re-pickup preserve provenance", JettisonCannotLaunder, ref passed, ref failed);
        RunCase("save/load preserves clean/stolen split", SaveLoadPreservesSplit, ref passed, ref failed);
        RunCase("physical stolen pod save/load preserves provenance", SaveLoadPreservesPodProvenance, ref passed, ref failed);
        RunCase("legitimate purchase remains clean", LegitimatePurchaseIsClean, ref passed, ref failed);
        RunCase("mission smuggling cargo remains clean provenance", MissionCargoDefaultsClean, ref passed, ref failed);
        RunCase("reserved stolen cargo is protected", ReservedStolenCargoIsProtected, ref passed, ref failed);
        RunCase("lawful dealer rejects stolen quantity", LawfulDealerRejectsStolen, ref passed, ref failed);
        RunCase("black market fences stolen legal cargo", BlackMarketFencesStolenLegalCargo, ref passed, ref failed);
        RunCase("fence price is deterministic and discounted", FencePriceIsBounded, ref passed, ref failed);
        RunCase("fence access gate remains live", FenceAccessGateRemainsLive, ref passed, ref failed);
        RunCase("Police detects legal stolen cargo", PoliceDetectsStolenCargo, ref passed, ref failed);
        RunCase("Police confiscates stolen and preserves clean", PoliceConfiscatesStolenOnly, ref passed, ref failed);
        RunCase("stolen contraband is counted once", StolenContrabandIsCountedOnce, ref passed, ref failed);
        RunCase("fine includes stolen value and stays bounded", FineIncludesStolenValue, ref passed, ref failed);

        Console.WriteLine($"[PHASE 57 SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 57 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 57 SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 57 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) LegalCommodityIsCleanCatalogData()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        return Check(food != null && !food.IsContraband, "food-rations catalog legality changed");
    }

    private static (bool, string) CleanAndStolenCoexist()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(100);
        bool added = hold.AddCommodity(food, 5) && hold.AddStolenCommodity(food, 3);
        return Check(added && hold.GetCommodityQuantity(food.Name) == 8 &&
            hold.GetCleanCommodityQuantity(food.Name) == 5 && hold.GetStolenCommodityQuantity(food.Name) == 3 &&
            hold.UsedCapacity == food.VolumePerUnit * 8, "clean/stolen aggregates were not independent");
    }

    private static (bool, string) StolenUsesNormalCapacity()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(food.VolumePerUnit * 2);
        bool first = hold.AddStolenCommodity(food, 2);
        bool third = hold.AddStolenCommodity(food, 1);
        return Check(first && !third && hold.UsedCapacity == hold.MaxCapacity, "stolen cargo changed capacity semantics");
    }

    private static (bool, string) ExtortionPodIsStolen()
    {
        LootManager loot = CreateLoot();
        NpcShip trader = CreateTrader("Phase57 Extortion Trader");
        int released = loot.SpawnExtortionCargo(trader, "food-rations", 2, out _);
        CargoPod pod = loot.ActivePods.SingleOrDefault();
        return Check(released == 2 && pod?.IsStolen == true && !pod.IsMissionCargo, "extortion cargo was not marked stolen at the source");
    }

    private static (bool, string) StolenPodPickupPreservesProvenance()
    {
        LootManager loot = CreateLoot();
        Ship player = new(Vector3.Zero);
        NpcShip trader = CreateTrader("Phase57 Pickup Trader");
        loot.SpawnExtortionCargo(trader, "food-rations", 2, out _);
        CargoPod pod = loot.ActivePods.Single();
        pod.Position = player.Position;
        pod.Velocity = Vector3.Zero;
        loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), player, false);
        return Check(!loot.ActivePods.Contains(pod) && player.CargoHold.GetStolenCommodityQuantity("Food Rations") == 2,
            "pod pickup lost stolen provenance");
    }

    private static (bool, string) JettisonCannotLaunder()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(100);
        hold.AddStolenCommodity(food, 2);
        LootManager loot = CreateLoot();
        int removed = loot.TryJettisonContraband(hold, Vector3.Zero, Vector3.Zero, out int podCount);
        CargoPod pod = loot.ActivePods.SingleOrDefault();
        return Check(removed == 2 && podCount == 1 && pod?.IsStolen == true &&
            hold.GetCommodityQuantity(food.Name) == 0, "jettison did not retain stolen provenance");
    }

    private static (bool, string) SaveLoadPreservesSplit()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold source = new(100);
        source.AddCommodity(food, 5);
        source.AddStolenCommodity(food, 3);
        SaveGameManager manager = new(Path.Combine(Path.GetTempPath(), $"phase57-{Guid.NewGuid():N}.json"));
        SaveGameData data = new() { Cargo = manager.CaptureCargo(source) };
        if (!manager.TrySave(data, out _) || !manager.TryLoad(out SaveGameData loaded, out _))
            return Fail("save/load failed");
        CargoHold restored = new(100);
        manager.ApplyCargo(restored, loaded, out List<string> warnings);
        return Check(warnings.Count == 0 && restored.GetCleanCommodityQuantity(food.Name) == 5 &&
            restored.GetStolenCommodityQuantity(food.Name) == 3 && restored.GetCommodityQuantity(food.Name) == 8,
            "save/load changed the clean/stolen split");
    }

    private static (bool, string) SaveLoadPreservesPodProvenance()
    {
        LootManager source = CreateLoot();
        source.SpawnExtortionCargo(CreateTrader("Phase57 Save Trader"), "food-rations", 2, out _);
        List<SaveCargoPodData> saved = source.CaptureCargoPods();
        LootManager restored = CreateLoot();
        int count = restored.RestoreCargoPods(saved);
        return Check(count == 1 && restored.ActivePods.Single().IsStolen && restored.ActivePods.Single().Quantity == 2,
            "physical stolen pod did not survive save/load");
    }

    private static (bool, string) LegitimatePurchaseIsClean()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(100);
        return Check(hold.AddCommodity(food, 2) && hold.GetStolenCommodityQuantity(food.Name) == 0,
            "ordinary purchase path gained stolen provenance");
    }

    private static (bool, string) MissionCargoDefaultsClean()
    {
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        CargoHold hold = new(100);
        return Check(hold.AddMissionCargo(5701, contraband, 2) &&
            hold.GetMissionCargoReservations().Single().IsStolen == false &&
            hold.GetStolenCommodityQuantity(contraband.Name) == 0, "mission smuggling cargo changed provenance");
    }

    private static (bool, string) ReservedStolenCargoIsProtected()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(100);
        hold.AddMissionCargo(5702, food, 5, CargoProvenance.Stolen);
        hold.AddStolenCommodity(food, 3);
        return Check(hold.GetSellableStolenCommodityQuantity(food.Name) == 3 &&
            hold.GetMissionReservedStolenQuantity(food.Name) == 5, "reservation/provenance availability was not separated");
    }

    private static (bool, string) LawfulDealerRejectsStolen()
    {
        Context context = CreateMarketContext();
        Commodity food = CommodityCatalog.GetById("food-rations");
        context.Hold.AddStolenCommodity(food, 2);
        int beforeCredits = context.Credits.Credits;
        bool sold = context.Dealer.TrySellCommodity(food, 2, context.Credits, context.Hold, out string message);
        return Check(!sold && beforeCredits == context.Credits.Credits && context.Hold.GetStolenCommodityQuantity(food.Name) == 2 &&
            message.Contains("stolen", StringComparison.OrdinalIgnoreCase), "lawful dealer accepted or removed stolen cargo");
    }

    private static (bool, string) BlackMarketFencesStolenLegalCargo()
    {
        Context context = CreateMarketContext();
        Commodity food = CommodityCatalog.GetById("food-rations");
        context.Hold.AddStolenCommodity(food, 2);
        context.Dealer.SetPlayerCargoHold(context.Hold);
        if (!context.Dealer.TryOpenBlackMarket(out _))
            return Fail("black market access was denied");
        StationMarketListing listing = context.Dealer.CurrentMarketListings.SingleOrDefault(candidate => candidate.Commodity.Id == food.Id);
        int beforeCredits = context.Credits.Credits;
        bool sold = context.Dealer.TrySellCommodity(food, 2, context.Credits, context.Hold, out _);
        return Check(listing != null && sold && context.Hold.GetCommodityQuantity(food.Name) == 0 &&
            context.Credits.Credits == beforeCredits + listing.SellPrice * 2, "black market did not fence exact stolen quantity");
    }

    private static (bool, string) FencePriceIsBounded()
    {
        Context context = CreateMarketContext();
        Commodity food = CommodityCatalog.GetById("food-rations");
        StationMarketListing clean = context.Dealer.MarketManager.GetListingForCommodity(context.Station, food, MarketSurface.Ordinary);
        StationMarketListing fence = context.Dealer.MarketManager.GetFenceListing(context.Station, food);
        StationMarketListing fenceAgain = context.Dealer.MarketManager.GetFenceListing(context.Station, food);
        return Check(clean != null && fence != null && fence.SellPrice < clean.SellPrice && fence.SellPrice == fenceAgain.SellPrice &&
            fence.SellPrice >= (int)Math.Floor(clean.SellPrice * 0.45m), "fence pricing was not deterministic and bounded");
    }

    private static (bool, string) FenceAccessGateRemainsLive()
    {
        Context context = CreateMarketContext();
        Commodity food = CommodityCatalog.GetById("food-rations");
        context.Hold.AddStolenCommodity(food, 1);
        context.Dealer.SetPlayerCargoHold(context.Hold);
        if (!context.Dealer.TryOpenBlackMarket(out _))
            return Fail("initial fence access was denied");
        context.Reputation.SetReputation(FactionManager.LibertyRogues, -0.70f);
        bool sold = context.Dealer.TrySellCommodity(food, 1, context.Credits, context.Hold, out _);
        return Check(!sold && context.Hold.GetStolenCommodityQuantity(food.Name) == 1, "live black-market gate was bypassed");
    }

    private static (bool, string) PoliceDetectsStolenCargo()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(100);
        hold.AddCommodity(food, 1);
        hold.AddStolenCommodity(food, 2);
        PoliceEnforcementOffer offer = new PoliceEnforcementService().Evaluate(
            FactionManager.LibertyPolice, hold, new PlayerCredits(10_000));
        return Check(!offer.HasContraband && offer.HasStolenGoods && offer.HasViolation &&
            offer.Contraband.Single().Quantity == 2, "legal stolen cargo was not a distinct Police violation");
    }

    private static (bool, string) PoliceConfiscatesStolenOnly()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(100);
        hold.AddCommodity(food, 2);
        hold.AddStolenCommodity(food, 3);
        PlayerCredits credits = new(10_000);
        ReputationManager reputation = CreateReputation();
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, hold, credits);
        bool resolved = service.TryResolve(offer, PoliceEnforcementResolution.Comply, hold, credits, reputation,
            out PoliceEnforcementResult result, out _);
        return Check(resolved && result.ConfiscatedQuantity == 3 && hold.GetCleanCommodityQuantity(food.Name) == 2 &&
            hold.GetStolenCommodityQuantity(food.Name) == 0, "Police confiscated clean cargo or left stolen cargo aboard");
    }

    private static (bool, string) StolenContrabandIsCountedOnce()
    {
        Commodity sideArms = CommodityCatalog.GetById("side-arms");
        CargoHold hold = new(100);
        hold.AddCommodity(sideArms, 1);
        hold.AddStolenCommodity(sideArms, 2);
        PoliceEnforcementOffer offer = new PoliceEnforcementService().Evaluate(FactionManager.LibertyPolice, hold, new PlayerCredits(10_000));
        return Check(offer.HasContraband && offer.HasStolenGoods && offer.TotalViolationQuantity == 3 &&
            offer.Contraband.Count == 1 && offer.Contraband[0].Quantity == 3, "contraband and stolen quantity was double-counted");
    }

    private static (bool, string) FineIncludesStolenValue()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold hold = new(100);
        hold.AddStolenCommodity(food, 4);
        PoliceEnforcementOffer offer = new PoliceEnforcementService().Evaluate(FactionManager.LibertyPolice, hold, new PlayerCredits(10_000));
        int expected = new PoliceEnforcementService().CalculateFine(food.BasePrice * 4L);
        return Check(offer.FineAmount == expected && offer.FineAmount is >= PoliceEnforcementService.MinimumFineCredits and <= PoliceEnforcementService.MaximumFineCredits,
            "stolen value did not enter the bounded fine formula");
    }

    private static LootManager CreateLoot() => new(
        graphicsDevice: null,
        salvageService: new CombatSalvageService(),
        worldObjectsProvider: () => Array.Empty<SpaceObject>());

    private static NpcShip CreateTrader(string name)
    {
        NpcShip trader = new(name, Vector3.Zero, Vector3.Zero, 1f, 0f, FactionManager.LibertyCorporations);
        trader.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "phase57", Vector3.Zero, 500f, 100f);
        return trader;
    }

    private static ReputationManager CreateReputation()
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyRogues, 0f, "phase57 smoke");
        reputation.SetReputation(FactionManager.LibertyPolice, 0f, "phase57 smoke");
        return reputation;
    }

    private static Context CreateMarketContext()
    {
        Station station = new(new Roguelancer.Configuration.StationConfig
        {
            Description = "Buffalo Base",
            FactionId = FactionManager.LibertyRogues,
            SystemIndex = 1,
            StartupPositionX = 0f,
            StartupPositionY = 0f,
            StartupPositionZ = 0f,
            Radius = 700f,
            DockingRange = 700f
        }, null);
        CommodityDealer dealer = new();
        ReputationManager reputation = CreateReputation();
        dealer.SetReputationManager(reputation);
        dealer.SetDockedStation(station);
        CargoHold hold = new(100);
        dealer.SetPlayerCargoHold(hold);
        return new Context(dealer, reputation, station, hold, new PlayerCredits(10_000));
    }

    private static (bool, string) Check(bool success, string reason) => success ? (true, string.Empty) : Fail(reason);
    private static (bool, string) Fail(string reason) => (false, reason);

    private sealed record Context(
        CommodityDealer Dealer,
        ReputationManager Reputation,
        Station Station,
        CargoHold Hold,
        PlayerCredits Credits);
}
