using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Phase 82 focused coverage for pirate haul recovery and provenance. The suite
/// drives only production systems: the Phase 81 NpcPirateCargoDemandService,
/// the authoritative CargoHold, the physical CargoPod/LootManager loot
/// authority, the Phase 57 stolen-provenance save seam, and the existing
/// CommodityDealer/MarketManager fence policy. It proves that cargo taken from
/// the player becomes stolen pirate haul and drops through the ordinary loot
/// path instead of vanishing because it began clean.
/// </summary>
internal sealed class Phase82PirateHaulRecoverySmokeTest
{
    private const string FoodId = "food-rations";
    private const string FoodName = "Food Rations";
    private const string WaterId = "water";
    private const string HFuelId = "h-fuel";
    private const string SideArmsId = "side-arms";

    private static readonly IReadOnlyList<Station> Stations = LoadFixtureStations();

    public Phase82PirateHaulRecoverySmokeTest(GraphicsDevice graphicsDevice)
    {
        // The focused suite exercises headless cargo/loot authorities; the
        // graphics device is accepted only to match the other Phase runners.
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("Phase 81 clean cargo compliance still succeeds", CleanComplianceSucceeds, ref passed, ref failed);
        RunCase("exact clean quantity leaves the player", ExactCleanQuantityLeavesPlayer, ref passed, ref failed);
        RunCase("clean-origin cargo enters the pirate haul", CleanOriginEntersHaul, ref passed, ref failed);
        RunCase("clean-origin pirate haul provenance is stolen", CleanOriginHaulProvenanceStolen, ref passed, ref failed);
        RunCase("already-stolen player cargo enters haul as stolen", StolenPlayerCargoEntersHaul, ref passed, ref failed);
        RunCase("no double-stolen provenance state exists", NoDoubleStolenState, ref passed, ref failed);
        RunCase("legal commodity stays legal after theft", LegalCommodityStaysLegal, ref passed, ref failed);
        RunCase("contraband commodity stays contraband after theft", ContrabandStaysContraband, ref passed, ref failed);
        RunCase("mixed commodity demand records each line", MixedCommodityLinesRecorded, ref passed, ref failed);
        RunCase("mixed player provenance keeps correct remaining buckets", MixedPlayerProvenanceBuckets, ref passed, ref failed);
        RunCase("mission reservation stays protected", MissionReservationProtected, ref passed, ref failed);
        RunCase("freight reservation stays protected", FreightReservationProtected, ref passed, ref failed);
        RunCase("refusal creates no haul", RefusalCreatesNoHaul, ref passed, ref failed);
        RunCase("timeout creates no haul", TimeoutCreatesNoHaul, ref passed, ref failed);
        RunCase("failed compliance creates no haul", FailedComplianceCreatesNoHaul, ref passed, ref failed);
        RunCase("dead demander before commit creates no haul", DeadDemanderBeforeCommitNoHaul, ref passed, ref failed);
        RunCase("immediate death after compliance retains committed haul", ImmediateDeathRetainsHaul, ref passed, ref failed);
        RunCase("pirate destruction routes haul through existing loot authority", DestructionRoutesThroughLoot, ref passed, ref failed);
        RunCase("clean-origin surrendered cargo is no longer silently consumed", CleanOriginNotSilentlyConsumed, ref passed, ref failed);
        RunCase("physical pod uses existing CargoPod", PodUsesExistingCargoPod, ref passed, ref failed);
        RunCase("physical pod provenance is stolen", PodProvenanceStolen, ref passed, ref failed);
        RunCase("pod commodity id matches haul", PodCommodityMatchesHaul, ref passed, ref failed);
        RunCase("pod quantity follows existing haul drop policy", PodQuantityFollowsDropPolicy, ref passed, ref failed);
        RunCase("no duplicate pod is emitted for one haul line", NoDuplicatePod, ref passed, ref failed);
        RunCase("player pickup of recovered pod preserves stolen provenance", PickupPreservesStolen, ref passed, ref failed);
        RunCase("recovered cargo is not automatically restored clean", RecoveredNotRestoredClean, ref passed, ref failed);
        RunCase("lawful dealer refuses recovered stolen cargo", LawfulDealerRefusesRecovered, ref passed, ref failed);
        RunCase("existing black market accepts recovered stolen cargo", BlackMarketAcceptsRecovered, ref passed, ref failed);
        RunCase("existing fence price remains unchanged", FencePriceUnchanged, ref passed, ref failed);
        RunCase("no victim or original-owner history is stored", NoVictimHistory, ref passed, ref failed);
        RunCase("no theft timestamp is stored", NoTheftTimestamp, ref passed, ref failed);
        RunCase("no new criminal-state authority is added", NoNewCriminalState, ref passed, ref failed);
        RunCase("player-to-trader PirateCargoDemandService remains unchanged", PlayerToTraderPiracyUnchanged, ref passed, ref failed);
        RunCase("Phase 66 ambient pirate raid manager remains unchanged", Phase66AmbientUnchanged, ref passed, ref failed);
        RunCase("ordinary pod creation defaults to clean provenance", NormalSalvageProvenanceUnchanged, ref passed, ref failed);
        RunCase("ordinary non-haul destruction produces no Phase 82 cargo", OrdinaryNonHaulDestructionNoCargo, ref passed, ref failed);
        RunCase("pirate destruction without haul produces no Phase 82 cargo", PirateDestructionWithoutHaulNoCargo, ref passed, ref failed);
        RunCase("destruction drop has no killer attribution gate", DestructionAttributionAgnostic, ref passed, ref failed);
        RunCase("pirate despawn does not refund cargo into space", DespawnDoesNotRefund, ref passed, ref failed);
        RunCase("system transition does not teleport haul", SystemTransitionNoTeleport, ref passed, ref failed);
        RunCase("player death does not duplicate pirate haul", PlayerDeathNoDuplicate, ref passed, ref failed);
        RunCase("dock and undock do not duplicate haul", DockUndockNoDuplicate, ref passed, ref failed);
        RunCase("one compliance creates one authoritative transfer", OneComplianceOneTransfer, ref passed, ref failed);
        RunCase("player quantity plus haul obeys transfer conservation", TransferConservation, ref passed, ref failed);
        RunCase("quantities never become negative", QuantitiesNeverNegative, ref passed, ref failed);
        RunCase("commodity totals do not duplicate", CommodityTotalsDoNotDuplicate, ref passed, ref failed);
        RunCase("multiple demand lines remain bounded", MultipleDemandLinesBounded, ref passed, ref failed);
        RunCase("repeated resolved demand cannot add haul twice", RepeatedResolvedDemandBounded, ref passed, ref failed);
        RunCase("one-shot Phase 81 target semantics remain intact", OneShotTargetSemantics, ref passed, ref failed);
        RunCase("Rogue flee behavior after compliance remains intact", RogueFleeAfterCompliance, ref passed, ref failed);
        RunCase("compliance remains non-combat for the interaction", ComplianceNonCombat, ref passed, ref failed);
        RunCase("refusal combat behavior remains intact", RefusalCombatIntact, ref passed, ref failed);
        RunCase("Police systems remain uninvolved directly", PoliceUninvolved, ref passed, ref failed);
        RunCase("fugitive and surrender systems remain unchanged", FugitiveUnchanged, ref passed, ref failed);
        RunCase("trade-lane exclusions remain unchanged", TradeLaneExclusionUnchanged, ref passed, ref failed);
        RunCase("cargo provenance save behavior remains Phase 57 authority", ProvenanceSaveRoundTrips, ref passed, ref failed);
        RunCase("player save schema is unchanged", SchemaUnchanged, ref passed, ref failed);
        RunCase("haul processing is bounded and reset clears transient haul", HaulProcessingBounded, ref passed, ref failed);
        RunCase("no per-frame provenance reconciliation is introduced", NoPerFrameReconciliation, ref passed, ref failed);
        RunCase("pirate haul entry reports stolen provenance", HaulEntryProvenanceStolen, ref passed, ref failed);

        Console.WriteLine($"[PHASE 82 PIRATE HAUL RECOVERY SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 82 PIRATE HAUL RECOVERY SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 82 PIRATE HAUL RECOVERY SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 82 PIRATE HAUL RECOVERY SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    // ---- Core transfer and provenance --------------------------------------

    private (bool, string) CleanComplianceSucceeds()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        if (!ctx.Service.TryInitiateDemand(ctx.Player, out string failure))
            return Fail($"demand not initiated: {failure}");
        bool complied = ctx.Service.TryComply(ctx.Player, out NpcPirateCargoDemandResult result);
        return complied && result != null && result.State == NpcPirateDemandState.Complying
            ? Pass()
            : Fail($"complied={complied}, state={result?.State}");
    }

    private (bool, string) ExactCleanQuantityLeavesPlayer()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Service.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int haul = ctx.Service.GetPirateHaulQuantity(ctx.Npcs[0]);
        return before - after == haul && haul > 0
            ? Pass()
            : Fail($"before={before}, after={after}, haul={haul}");
    }

    private (bool, string) CleanOriginEntersHaul()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        return ctx.Service.GetPirateHaulQuantity(demander) > 0
            ? Pass()
            : Fail("clean-origin cargo did not enter the haul");
    }

    private (bool, string) CleanOriginHaulProvenanceStolen()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        if (!ctx.Service.TryGetHaulSnapshot(demander, out NpcCargoManifestSnapshot snapshot))
            return Fail("no haul snapshot");
        return snapshot.Stacks.All(stack => stack.IsStolen) && snapshot.Stacks.Any()
            ? Pass()
            : Fail("clean-origin haul was not marked stolen");
    }

    private (bool, string) StolenPlayerCargoEntersHaul()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveStolenCargo(FoodId, 8);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        int stolenBefore = ctx.Player.CargoHold.GetStolenCommodityQuantity(FoodName);
        ctx.Service.TryComply(ctx.Player, out _);
        int stolenAfter = ctx.Player.CargoHold.GetStolenCommodityQuantity(FoodName);
        if (!ctx.Service.TryGetHaulSnapshot(demander, out NpcCargoManifestSnapshot snapshot))
            return Fail("no haul snapshot");
        return stolenAfter < stolenBefore && snapshot.Stacks.All(stack => stack.IsStolen) &&
            snapshot.RemainingQuantity == stolenBefore - stolenAfter
            ? Pass()
            : Fail($"before={stolenBefore}, after={stolenAfter}, haul={snapshot.RemainingQuantity}");
    }

    private (bool, string) NoDoubleStolenState()
    {
        string[] names = Enum.GetNames(typeof(CargoProvenance));
        return names.Length == 2 && names.Contains("Clean") && names.Contains("Stolen")
            ? Pass()
            : Fail($"provenance states: {string.Join(",", names)}");
    }

    private (bool, string) LegalCommodityStaysLegal()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(demander);
        CargoPod pod = ctx.Loot.ActivePods.FirstOrDefault();
        Commodity food = CommodityCatalog.GetById(FoodId);
        return food.IsContraband == false && pod?.GetCommodity()?.IsContraband == false && pod?.IsStolen == true
            ? Pass()
            : Fail($"food contraband={food.IsContraband}, pod={pod?.IsStolen}");
    }

    private (bool, string) ContrabandStaysContraband()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(SideArmsId, 6);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(demander);
        CargoPod pod = ctx.Loot.ActivePods.FirstOrDefault();
        Commodity sideArms = CommodityCatalog.GetById(SideArmsId);
        return sideArms.IsContraband && pod?.GetCommodity()?.IsContraband == true && pod?.IsStolen == true
            ? Pass()
            : Fail($"contraband={sideArms.IsContraband}, pod={pod?.IsStolen}");
    }

    private (bool, string) MixedCommodityLinesRecorded()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.GiveCargo(WaterId, 10);
        ctx.GiveCargo(HFuelId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        Dictionary<string, int> requested = new(ctx.Service.ActiveDemandQuantities, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> before = new(ctx.Player.CargoHold.GetAllCommodities(), StringComparer.OrdinalIgnoreCase);
        ctx.Service.TryComply(ctx.Player, out _);
        if (!ctx.Service.TryGetHaulSnapshot(demander, out NpcCargoManifestSnapshot snapshot))
            return Fail("no haul snapshot");
        foreach (KeyValuePair<string, int> entry in requested)
        {
            Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
            if (commodity == null)
                return Fail($"could not resolve demanded commodity '{entry.Key}'");
            int haulQuantity = snapshot.Stacks
                .Where(stack => string.Equals(stack.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase))
                .Sum(stack => stack.Quantity);
            int playerAfter = ctx.Player.CargoHold.GetCommodityQuantity(commodity.Name);
            if (haulQuantity != entry.Value || before[entry.Key] - playerAfter != entry.Value ||
                !snapshot.Stacks.Where(stack => string.Equals(stack.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase)).All(stack => stack.IsStolen))
                return Fail($"{entry.Key}: requested={entry.Value}, haul={haulQuantity}, before={before[entry.Key]}, after={playerAfter}");
        }
        return Pass();
    }

    private (bool, string) MixedPlayerProvenanceBuckets()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 5);
        ctx.GiveStolenCargo(FoodId, 3);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int demand = ctx.Service.ActiveDemandQuantities.TryGetValue(FoodName, out int value) ? value : 0;
        ctx.Service.TryComply(ctx.Player, out _);
        int total = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int stolen = ctx.Player.CargoHold.GetStolenCommodityQuantity(FoodName);
        int clean = ctx.Player.CargoHold.GetCleanCommodityQuantity(FoodName);
        int expectedStolen = Math.Max(0, 3 - demand);
        int expectedTotal = 8 - demand;
        return total == expectedTotal && clean + stolen == total && stolen == expectedStolen && clean >= 0
            ? Pass()
            : Fail($"demand={demand}, total={total}/{expectedTotal}, stolen={stolen}/{expectedStolen}, clean={clean}");
    }

    // ---- Reservations -------------------------------------------------------

    private (bool, string) MissionReservationProtected()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.AddMissionCargo(999, FoodId, 4);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return ctx.Player.CargoHold.GetMissionCargoQuantity(999) == 4
            ? Pass()
            : Fail($"mission cargo after={ctx.Player.CargoHold.GetMissionCargoQuantity(999)}");
    }

    private (bool, string) FreightReservationProtected()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.RegisterFreightReservation(888, FoodId, 3);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return ctx.Player.CargoHold.GetMissionReservedQuantity(FoodName) == 3
            ? Pass()
            : Fail($"freight reserved after={ctx.Player.CargoHold.GetMissionReservedQuantity(FoodName)}");
    }

    // ---- Refusal, timeout, failure -----------------------------------------

    private (bool, string) RefusalCreatesNoHaul()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        return ctx.Service.GetPirateHaulQuantity(rogue) == 0 && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail($"haul={ctx.Service.GetPirateHaulQuantity(rogue)}, pods={ctx.Loot.ActivePods.Count}");
    }

    private (bool, string) TimeoutCreatesNoHaul()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.StepUpdate(6f);
        return !ctx.Service.HasActiveDemand && ctx.Service.GetPirateHaulQuantity(rogue) == 0 && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail("timeout created haul");
    }

    private (bool, string) FailedComplianceCreatesNoHaul()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Player.Hull.TakeDamage(10_000f);
        bool complied = ctx.Service.TryComply(ctx.Player, out _);
        return !complied && ctx.Service.GetPirateHaulQuantity(rogue) == 0 && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail($"complied={complied}, pods={ctx.Loot.ActivePods.Count}");
    }

    private (bool, string) DeadDemanderBeforeCommitNoHaul()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        rogue.Hull.TakeDamage(10_000f);
        bool complied = ctx.Service.TryComply(ctx.Player, out _);
        return !complied && ctx.Loot.ActivePods.Count == 0 && ctx.Player.CargoHold.GetCommodityQuantity(FoodName) == 10
            ? Pass()
            : Fail($"complied={complied}, cargo={ctx.Player.CargoHold.GetCommodityQuantity(FoodName)}");
    }

    private (bool, string) ImmediateDeathRetainsHaul()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int committed = ctx.Service.GetPirateHaulQuantity(rogue);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        int podded = ctx.Loot.ActivePods.Sum(pod => pod.Quantity);
        return committed > 0 && podded == committed
            ? Pass()
            : Fail($"committed={committed}, podded={podded}");
    }

    // ---- Destruction and physical loot -------------------------------------

    private (bool, string) DestructionRoutesThroughLoot()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        CargoPod pod = ctx.Loot.ActivePods.FirstOrDefault();
        return pod != null && pod.PayloadType == CargoPodPayloadType.Commodity && pod.IsStolen &&
            string.Equals(pod.SourceNpcName, rogue.Name, StringComparison.OrdinalIgnoreCase)
            ? Pass()
            : Fail($"pod={pod?.GetPayloadName()}, stolen={pod?.IsStolen}");
    }

    private (bool, string) CleanOriginNotSilentlyConsumed()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int removed = ctx.Service.ActiveDemandQuantities.Values.Sum();
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        int podded = ctx.Loot.ActivePods.Sum(pod => pod.Quantity);
        return removed > 0 && podded == removed
            ? Pass()
            : Fail($"removed={removed}, podded={podded}");
    }

    private (bool, string) PodUsesExistingCargoPod()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        return ctx.Loot.ActivePods.Count > 0 && ctx.Loot.ActivePods.All(pod => pod is CargoPod)
            ? Pass()
            : Fail("no canonical CargoPod was produced");
    }

    private (bool, string) PodProvenanceStolen()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        return ctx.Loot.ActivePods.All(pod => pod.IsStolen) && ctx.Loot.ActivePods.Count > 0
            ? Pass()
            : Fail("pod provenance was not stolen");
    }

    private (bool, string) PodCommodityMatchesHaul()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.TryGetHaulSnapshot(demander, out NpcCargoManifestSnapshot snapshot);
        ctx.Service.NotifyNpcDestroyed(rogue);
        CargoPod pod = ctx.Loot.ActivePods.FirstOrDefault();
        string haulCommodity = snapshot.Stacks.FirstOrDefault()?.Commodity?.Id;
        return pod != null && string.Equals(pod.CommodityId, haulCommodity, StringComparison.OrdinalIgnoreCase)
            ? Pass()
            : Fail($"pod={pod?.CommodityId}, haul={haulCommodity}");
    }

    private (bool, string) PodQuantityFollowsDropPolicy()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        int haul = ctx.Service.GetPirateHaulQuantity(demander);
        ctx.Service.NotifyNpcDestroyed(rogue);
        int podded = ctx.Loot.ActivePods.Sum(pod => pod.Quantity);
        return podded == Math.Min(haul, 40)
            ? Pass()
            : Fail($"haul={haul}, podded={podded}");
    }

    private (bool, string) NoDuplicatePod()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.TryGetHaulSnapshot(demander, out NpcCargoManifestSnapshot snapshot);
        int lines = snapshot.Stacks.Count;
        ctx.Service.NotifyNpcDestroyed(rogue);
        int firstCount = ctx.Loot.ActivePods.Count;
        ctx.Service.NotifyNpcDestroyed(rogue);
        return firstCount == lines && ctx.Loot.ActivePods.Count == lines
            ? Pass()
            : Fail($"lines={lines}, first={firstCount}, afterSecond={ctx.Loot.ActivePods.Count}");
    }

    private (bool, string) PickupPreservesStolen()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        int stolenBefore = ctx.Player.CargoHold.GetStolenCommodityQuantity(FoodName);
        int collected = ctx.CollectAllPods();
        int stolenAfter = ctx.Player.CargoHold.GetStolenCommodityQuantity(FoodName);
        return collected > 0 && stolenAfter == stolenBefore + collected
            ? Pass()
            : Fail($"collected={collected}, before={stolenBefore}, after={stolenAfter}");
    }

    private (bool, string) RecoveredNotRestoredClean()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveStolenCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        int cleanBefore = ctx.Player.CargoHold.GetSellableCleanCommodityQuantity(FoodName);
        ctx.CollectAllPods();
        int cleanAfter = ctx.Player.CargoHold.GetSellableCleanCommodityQuantity(FoodName);
        int stolenAfter = ctx.Player.CargoHold.GetSellableStolenCommodityQuantity(FoodName);
        return cleanAfter == cleanBefore && stolenAfter > 0
            ? Pass()
            : Fail($"clean {cleanBefore}->{cleanAfter}, stolen={stolenAfter}");
    }

    // ---- Fencing integration -----------------------------------------------

    private (bool, string) LawfulDealerRefusesRecovered()
    {
        CargoHold hold = RecoveredStolenFood(out Commodity commodity, out int quantity);
        FenceContext fence = CreateFenceContext("Fort Bush");
        bool sold = fence.Dealer.TrySellCommodity(commodity, quantity, new PlayerCredits(0), hold, out string message);
        return !sold && hold.GetStolenCommodityQuantity(commodity.Name) == quantity &&
            message.IndexOf("stolen", StringComparison.OrdinalIgnoreCase) >= 0
            ? Pass()
            : Fail($"sold={sold}, message={message}");
    }

    private (bool, string) BlackMarketAcceptsRecovered()
    {
        CargoHold hold = RecoveredStolenFood(out Commodity commodity, out int quantity);
        FenceContext fence = CreateFenceContext("Buffalo Base");
        if (!fence.Dealer.TryOpenBlackMarket(out string openMessage))
            return Fail($"black market did not open: {openMessage}");
        bool sold = fence.Dealer.TrySellCommodity(commodity, quantity, new PlayerCredits(0), hold, out string message);
        return sold && hold.GetStolenCommodityQuantity(commodity.Name) == 0
            ? Pass()
            : Fail($"sold={sold}, message={message}");
    }

    private (bool, string) FencePriceUnchanged()
    {
        CargoHold hold = RecoveredStolenFood(out Commodity commodity, out int quantity);
        FenceContext fence = CreateFenceContext("Buffalo Base");
        if (!fence.Dealer.TryOpenBlackMarket(out _))
            return Fail("black market did not open");
        StationMarketListing fenceListing = fence.Dealer.MarketManager.GetFenceListing(fence.Station, commodity);
        StationMarketListing cleanListing = fence.Dealer.MarketManager.GetListingForCommodity(fence.Station, commodity, MarketSurface.Ordinary);
        int expectedPrice = Math.Max(1, (int)Math.Floor(cleanListing.SellPrice * 0.60m));
        PlayerCredits credits = new(0);
        bool sold = fence.Dealer.TrySellCommodity(commodity, quantity, credits, hold, out _);
        return sold && fenceListing != null && fenceListing.SellPrice == expectedPrice &&
            credits.Credits == expectedPrice * quantity
            ? Pass()
            : Fail($"fence={fenceListing?.SellPrice}, expected={expectedPrice}, credits={credits.Credits}");
    }

    // ---- Architecture guards -----------------------------------------------

    private (bool, string) NoVictimHistory()
    {
        PropertyInfo[] properties = typeof(PirateDemandHaulEntry).GetProperties();
        string[] names = properties.Select(property => property.Name).ToArray();
        bool onlyExpected = names.All(name => name is "CommodityId" or "CommodityName" or "Quantity" or "Provenance");
        bool noHistory = !names.Any(name =>
            name.IndexOf("Victim", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Owner", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Original", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Clean", StringComparison.OrdinalIgnoreCase) >= 0);
        return onlyExpected && noHistory
            ? Pass()
            : Fail($"properties: {string.Join(",", names)}");
    }

    private (bool, string) NoTheftTimestamp()
    {
        PropertyInfo[] properties = typeof(PirateDemandHaulEntry).GetProperties();
        bool noTimestamp = !properties.Any(property =>
            property.Name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0 ||
            property.Name.IndexOf("Date", StringComparison.OrdinalIgnoreCase) >= 0 ||
            property.Name.IndexOf("Theft", StringComparison.OrdinalIgnoreCase) >= 0 ||
            property.Name.IndexOf("Count", StringComparison.OrdinalIgnoreCase) >= 0);
        return noTimestamp ? Pass() : Fail("theft history property was found");
    }

    private (bool, string) NoNewCriminalState()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyRogues);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        float after = ctx.Reputation.GetStanding(FactionManager.LibertyRogues);
        bool noCrimeText = !ctx.Notifications.Any(message =>
            message.IndexOf("crime", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("wanted", StringComparison.OrdinalIgnoreCase) >= 0);
        return Math.Abs(after - before) < 0.0001f && !ctx.PoliceInteractionActive && noCrimeText
            ? Pass()
            : Fail($"standing {before}->{after}, police={ctx.PoliceInteractionActive}");
    }

    private (bool, string) PlayerToTraderPiracyUnchanged()
    {
        FactionManager factions = new();
        ReputationManager reputation = new(factions);
        Ship player = new(Vector3.Zero);
        NpcShip trader = new("Phase82 Trader", new Vector3(500f, 0f, 0f), new Vector3(500f, 0f, 0f),
            900f, 0.2f, FactionManager.NeutralCivilians);
        trader.ConfigureTrafficBehavior(
            TrafficZoneBehaviorType.TraderRoute,
            "phase82-trade-zone",
            new Vector3(500f, 0f, 0f),
            900f,
            190f,
            7_000f,
            new Vector3(-10_000f, 0f, 0f),
            new Vector3(10_000f, 0f, 0f));
        List<NpcShip> npcs = new() { trader };
        PirateCargoDemandService service = new(npcs, reputation);
        bool issued = service.TryIssueDemand(player, trader, out string failure);
        int playerCargo = player.CargoHold.GetAllCommodities().Values.Sum();
        service.Reset();
        return issued && playerCargo == 0
            ? Pass()
            : Fail($"issued={issued}, failure={failure}, playerCargo={playerCargo}");
    }

    private (bool, string) Phase66AmbientUnchanged()
    {
        AmbientPirateRaidManager manager = new(new List<NpcShip>(), new List<SpaceObject>());
        manager.Update(0f);
        manager.NotifyNpcDestroyed(null);
        NpcShip trader = new("Phase82 Ambient Trader", Vector3.Zero, Vector3.Zero, 900f, 0.2f, FactionManager.NeutralCivilians);
        bool snapshot = manager.TryGetHaulSnapshot(trader, out _);
        return !snapshot && AmbientPirateRaidManager.MaximumHaulStacksPerRaider == 3
            ? Pass()
            : Fail("ambient pirate raid manager behavior changed");
    }

    private (bool, string) NormalSalvageProvenanceUnchanged()
    {
        bool created = CargoPod.TryCreate(FoodId, 2, Vector3.Zero, Vector3.Zero, 300f, 200f, out CargoPod pod);
        return created && !pod.IsStolen
            ? Pass()
            : Fail($"created={created}, stolen={pod?.IsStolen}");
    }

    private (bool, string) OrdinaryNonHaulDestructionNoCargo()
    {
        Context ctx = CreateContext();
        NpcShip plain = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.Service.NotifyNpcDestroyed(plain);
        return ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail($"ordinary destruction produced {ctx.Loot.ActivePods.Count} pod(s)");
    }

    private (bool, string) PirateDestructionWithoutHaulNoCargo()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        return ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail("refused pirate destruction produced cargo");
    }

    private (bool, string) DestructionAttributionAgnostic()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        // Destruction attribution is not part of the service seam: an NPC or
        // player final blow both route through the same NotifyNpcDestroyed.
        MethodInfo method = typeof(NpcPirateCargoDemandService).GetMethod("NotifyNpcDestroyed");
        bool singleParameter = method != null && method.GetParameters().Length == 1;
        ctx.Service.NotifyNpcDestroyed(rogue);
        return singleParameter && ctx.Loot.ActivePods.Count > 0
            ? Pass()
            : Fail("destruction required a killer attribution parameter");
    }

    private (bool, string) DespawnDoesNotRefund()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Npcs.Remove(rogue);
        ctx.StepUpdate(1f);
        int podsAfterDespawn = ctx.Loot.ActivePods.Count;
        ctx.Service.Reset();
        return podsAfterDespawn == 0 && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail($"pods after despawn={podsAfterDespawn}");
    }

    private (bool, string) SystemTransitionNoTeleport()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.Reset();
        return ctx.Service.GetPirateHaulQuantity(rogue) == 0 && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail("system transition teleported or refunded haul");
    }

    private (bool, string) PlayerDeathNoDuplicate()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int haulBefore = ctx.Service.GetPirateHaulQuantity(rogue);
        ctx.Service.CancelActiveDemand("player destroyed");
        return ctx.Service.GetPirateHaulQuantity(rogue) == haulBefore && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail("player death duplicated or dropped haul");
    }

    private (bool, string) DockUndockNoDuplicate()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int haulBefore = ctx.Service.GetPirateHaulQuantity(rogue);
        ctx.Service.CancelActiveDemand("player docked");
        ctx.Service.CancelActiveDemand("player undocked");
        return ctx.Service.GetPirateHaulQuantity(rogue) == haulBefore && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail("dock/undock duplicated haul");
    }

    private (bool, string) OneComplianceOneTransfer()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        bool second = ctx.Service.TryComply(ctx.Player, out _);
        return !second && ctx.Service.GetPirateHaulQuantity(rogue) == haul
            ? Pass()
            : Fail($"second={second}, haul={ctx.Service.GetPirateHaulQuantity(rogue)}/{haul}");
    }

    private (bool, string) TransferConservation()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        ctx.Service.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        return before == after + haul
            ? Pass()
            : Fail($"before={before}, after={after}, haul={haul}");
    }

    private (bool, string) QuantitiesNeverNegative()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return ctx.Player.CargoHold.GetAllCommodities().Values.All(quantity => quantity >= 0) &&
            ctx.Player.CargoHold.UsedCapacity >= 0 && ctx.Service.GetPirateHaulQuantity(rogue) >= 0
            ? Pass()
            : Fail("negative quantity observed");
    }

    private (bool, string) CommodityTotalsDoNotDuplicate()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.TryGetHaulSnapshot(demander, out NpcCargoManifestSnapshot snapshot);
        int player = ctx.Player.CargoHold.GetCommodityQuantity(FoodName);
        int haul = snapshot.RemainingQuantity;
        return player + haul == 10
            ? Pass()
            : Fail($"player={player}, haul={haul}");
    }

    private (bool, string) MultipleDemandLinesBounded()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.GiveCargo(WaterId, 10);
        ctx.GiveCargo(HFuelId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int lines = ctx.Service.ActiveDemandQuantities.Count;
        int total = ctx.Service.ActiveDemandQuantities.Values.Sum();
        return lines >= NpcPirateCargoDemandService.MinimumCommodityTypes &&
            lines <= NpcPirateCargoDemandService.MaximumCommodityTypes && total <= 30
            ? Pass()
            : Fail($"lines={lines}, total={total}");
    }

    private (bool, string) RepeatedResolvedDemandBounded()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        bool second = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !second && ctx.Service.GetPirateHaulQuantity(rogue) == haul
            ? Pass()
            : Fail($"second demand initiated={second}");
    }

    private (bool, string) OneShotTargetSemantics()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.StepUpdate(NpcPirateCargoDemandService.PlayerCooldownSeconds + 1f);
        bool reDemand = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !reDemand
            ? Pass()
            : Fail("resolved one-shot target was allowed to demand again");
    }

    private (bool, string) RogueFleeAfterCompliance()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return rogue.EncounterState == TrafficEncounterState.Fleeing
            ? Pass()
            : Fail($"encounter state={rogue.EncounterState}");
    }

    private (bool, string) ComplianceNonCombat()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return !rogue.HasPlayerTarget
            ? Pass()
            : Fail("compliance escalated to combat");
    }

    private (bool, string) RefusalCombatIntact()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        return rogue.HasPlayerTarget && rogue.EncounterState == TrafficEncounterState.AttackingPlayer
            ? Pass()
            : Fail($"target={rogue.HasPlayerTarget}, state={rogue.EncounterState}");
    }

    private (bool, string) PoliceUninvolved()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(2_000f, 0f, 0f));
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return !ctx.PoliceInteractionActive
            ? Pass()
            : Fail("Police interaction became active");
    }

    private (bool, string) FugitiveUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        float after = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        return Math.Abs(after - before) < 0.0001f
            ? Pass()
            : Fail($"police standing changed {before}->{after}");
    }

    private (bool, string) TradeLaneExclusionUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Player.SetTradeLaneTransit(true);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated
            ? Pass()
            : Fail("trade-lane player received a demand");
    }

    private (bool, string) ProvenanceSaveRoundTrips()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        List<SaveCargoPodData> saved = ctx.Loot.CaptureCargoPods();
        using LootManager restored = new(salvageService: new CombatSalvageService());
        int count = restored.RestoreCargoPods(saved);
        return saved.Count > 0 && count == saved.Count &&
            restored.ActivePods.All(pod => pod.IsStolen) &&
            restored.ActivePods.Sum(pod => pod.Quantity) == saved.Sum(pod => pod.Quantity)
            ? Pass()
            : Fail($"saved={saved.Count}, restored={count}");
    }

    private (bool, string) SchemaUnchanged() =>
        SaveGameData.CurrentSchemaVersion == 13 ? Pass() : Fail($"schema is {SaveGameData.CurrentSchemaVersion}");

    private (bool, string) HaulProcessingBounded()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.GiveCargo(WaterId, 10);
        ctx.GiveCargo(HFuelId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int haulLines = ctx.Service.TryGetHaulSnapshot(rogue, out NpcCargoManifestSnapshot snapshot)
            ? snapshot.Stacks.Count
            : 0;
        ctx.Service.Reset();
        return haulLines <= NpcPirateCargoDemandService.MaximumCommodityTypes &&
            ctx.Service.GetPirateHaulQuantity(rogue) == 0
            ? Pass()
            : Fail($"haulLines={haulLines}");
    }

    private (bool, string) NoPerFrameReconciliation()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int haul = ctx.Service.GetPirateHaulQuantity(rogue);
        for (int i = 0; i < 10; i++)
            ctx.StepUpdate(0.016f);
        return ctx.Service.GetPirateHaulQuantity(rogue) == haul && ctx.Loot.ActivePods.Count == 0
            ? Pass()
            : Fail("per-frame update mutated haul or emitted cargo");
    }

    private (bool, string) HaulEntryProvenanceStolen()
    {
        PirateDemandHaulEntry entry = new() { CommodityId = FoodId, CommodityName = FoodName, Quantity = 3 };
        return entry.Provenance == CargoProvenance.Stolen
            ? Pass()
            : Fail($"provenance={entry.Provenance}");
    }

    // ---- Fencing helpers ----------------------------------------------------

    private static CargoHold RecoveredStolenFood(out Commodity commodity, out int quantity)
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveStolenCargo(FoodId, 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        ctx.Service.NotifyNpcDestroyed(rogue);
        int collected = ctx.CollectAllPods();
        commodity = CommodityCatalog.GetById(FoodId);
        quantity = ctx.Player.CargoHold.GetSellableStolenCommodityQuantity(commodity.Name);
        if (collected <= 0 || quantity <= 0)
            throw new InvalidOperationException("recovered stolen food was not available for fencing");
        return ctx.Player.CargoHold;
    }

    private sealed class FenceContext
    {
        public FenceContext(Station station, CommodityDealer dealer, ReputationManager reputation)
        {
            Station = station;
            Dealer = dealer;
            Reputation = reputation;
        }

        public Station Station { get; }
        public CommodityDealer Dealer { get; }
        public ReputationManager Reputation { get; }
    }

    private static FenceContext CreateFenceContext(string stationName)
    {
        Station station = Stations.FirstOrDefault(candidate => NormalizeKey(candidate?.Name) == NormalizeKey(stationName));
        if (station == null)
            throw new InvalidOperationException($"station fixture '{stationName}' was not found");
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyRogues, 0f, "Phase 82 fence setup");
        CommodityDealer dealer = new();
        dealer.SetReputationManager(reputation);
        dealer.SetDockedStation(station);
        return new FenceContext(station, dealer, reputation);
    }

    private static IReadOnlyList<Station> LoadFixtureStations()
    {
        string directory = Path.Combine("Configuration", "stations");
        if (!Directory.Exists(directory))
            return Array.Empty<Station>();
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        List<Station> result = new();
        foreach (string file in Directory.GetFiles(directory, "station_*.json"))
        {
            StationConfig config = JsonSerializer.Deserialize<StationConfig>(File.ReadAllText(file), options);
            if (config != null)
                result.Add(new Station(config, null));
        }
        return result;
    }

    private static string NormalizeKey(string value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    // ---- Harness -------------------------------------------------------------

    private static Context CreateContext() => new();

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);

    private sealed class Context
    {
        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public List<NpcShip> Npcs { get; } = new();
        public LootManager Loot { get; }
        public NpcPirateCargoDemandService Service { get; }
        public List<string> Notifications { get; } = new();
        public bool PoliceInteractionActive { get; set; }
        private Func<NpcShip, bool> _isMissionOwned = _ => false;

        public Context()
        {
            Reputation = new ReputationManager(new FactionManager());
            Reputation.SetReputation(FactionManager.LibertyRogues, 0f, "Phase 82 smoke setup");
            Player = new Ship(Vector3.Zero) { CollisionRadius = 120f };
            Loot = new LootManager(salvageService: new CombatSalvageService());
            Service = new NpcPirateCargoDemandService(
                Npcs,
                Reputation,
                _ => false,
                () => PoliceInteractionActive,
                npc => _isMissionOwned?.Invoke(npc) == true,
                null,
                (demander, commodityId, quantity) =>
                    Loot.SpawnStolenCargo(demander, commodityId, quantity, out _, null),
                null,
                message => Notifications.Add(message));
        }

        public NpcShip AddRogue(Vector3 position, string name = "Phase 82 Rogue")
        {
            NpcShip rogue = new(name ?? $"Phase 82 Rogue {Npcs.Count + 1}", position, position, 1f, 0f, FactionManager.LibertyRogues);
            rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase82-zone", position, 800f, 180f, 6500f);
            Npcs.Add(rogue);
            return rogue;
        }

        public NpcShip AddPolice(Vector3 position)
        {
            NpcShip police = new($"Phase 82 Police {Npcs.Count + 1}", position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase82-zone", position, 800f, 180f, 6500f);
            Npcs.Add(police);
            return police;
        }

        public void GiveCargo(string commodityId, int quantity)
        {
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity != null)
                Player.CargoHold.AddCommodity(commodity, quantity);
        }

        public void GiveStolenCargo(string commodityId, int quantity)
        {
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity != null)
                Player.CargoHold.AddStolenCommodity(commodity, quantity);
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

        public int CollectAllPods()
        {
            int collected = 0;
            foreach (CargoPod pod in Loot.ActivePods.ToList())
            {
                Player.Position = pod.Position;
                int before = Player.CargoHold.GetAllCommodities().Values.Sum();
                Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), Player, false);
                collected += Math.Max(0, Player.CargoHold.GetAllCommodities().Values.Sum() - before);
            }
            return collected;
        }

        public void StepUpdate(float delta) => Service.Update(delta, Player);
    }
}
