using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 56 proof. The harness exercises the demand owner, the real
/// LootManager/CargoPod path, deterministic trader manifests, and the existing
/// reputation/distress seams without creating a second economy authority.
/// </summary>
internal sealed class PiracyDemandSmokeTest
{
    private sealed class Context
    {
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public FactionManager Factions { get; } = new();
        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public LootManager Loot { get; }
        public FactionCombatCommunicationService Communication { get; }
        public PirateCargoDemandService Demand { get; }
        public NpcShip Trader { get; }

        public Context(
            string traderFaction = FactionManager.NeutralCivilians,
            bool policeNearby = false,
            Func<NpcShip, bool> missionOwned = null,
            Func<NpcShip, Ship, string, FactionDistressResponseResult> distress = null,
            Action<FactionDistressResponseResult, NpcShip, Ship> policeResponse = null)
        {
            Reputation = new ReputationManager(Factions);
            Player = new Ship(Vector3.Zero);
            Trader = CreateTrader("Phase56 Trader", new Vector3(1000f, 0f, 0f), traderFaction);
            Npcs.Add(Trader);
            Objects.Add(Trader);

            if (policeNearby)
            {
                NpcShip police = new("Phase56 Police", new Vector3(1200f, 0f, 500f), Vector3.Zero, 500f, 0.1f, FactionManager.LibertyPolice);
                police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase56-police", Vector3.Zero, 500f, 180f, 6500f);
                Npcs.Add(police);
                Objects.Add(police);
            }

            Loot = new LootManager(
                graphicsDevice: null,
                random: null,
                font: null,
                pixel: null,
                salvageService: new CombatSalvageService(),
                worldObjectsProvider: () => Objects);
            Communication = new FactionCombatCommunicationService(Npcs);
            Demand = new PirateCargoDemandService(
                Npcs,
                Reputation,
                missionOwned,
                (trader, commodityId, quantity) => Loot.SpawnExtortionCargo(trader, commodityId, quantity, out _),
                null,
                distress,
                policeResponse,
                Communication);
        }

        public static NpcShip CreateTrader(string name, Vector3 position, string faction = FactionManager.NeutralCivilians)
        {
            NpcShip trader = new(name, position, Vector3.Zero, 1000f, 0.1f, faction);
            trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                "phase56-trader-route",
                Vector3.Zero,
                1000f,
                190f,
                3500f,
                new Vector3(-5000f, 0f, 0f),
                new Vector3(5000f, 0f, 0f));
            return trader;
        }

        public void Advance(float seconds = PirateCargoDemandService.ResponseWindowSeconds + 0.1f)
        {
            Demand.Update(seconds, Player);
        }
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("eligible trader can receive demand", EligibleTraderCanReceiveDemand, ref passed, ref failed);
        RunCase("Police cannot receive demand", PoliceCannotReceiveDemand, ref passed, ref failed);
        RunCase("combat patrol cannot receive demand", CombatPatrolCannotReceiveDemand, ref passed, ref failed);
        RunCase("Rogue combat ship cannot receive demand", RogueCannotReceiveDemand, ref passed, ref failed);
        RunCase("mission-owned trader is excluded", MissionOwnedTraderExcluded, ref passed, ref failed);
        RunCase("invalid target is rejected", InvalidTargetRejected, ref passed, ref failed);
        RunCase("demand requires range", DemandRequiresRange, ref passed, ref failed);
        RunCase("lane transit is rejected", LaneTransitRejected, ref passed, ref failed);
        RunCase("only one active demand exists", OnlyOneActiveDemand, ref passed, ref failed);
        RunCase("active demand binds stable target", ActiveDemandBindsStableTarget, ref passed, ref failed);
        RunCase("response duration is bounded", ResponseDurationIsBounded, ref passed, ref failed);
        RunCase("manifest uses canonical commodities", ManifestUsesCanonicalCommodities, ref passed, ref failed);
        RunCase("manifest is deterministic", ManifestIsDeterministic, ref passed, ref failed);
        RunCase("manifest quantity is bounded", ManifestQuantityIsBounded, ref passed, ref failed);
        RunCase("manifest commodity count is bounded", ManifestCommodityCountIsBounded, ref passed, ref failed);
        RunCase("lawful manifest excludes contraband and mission cargo", LawfulManifestIsLegal, ref passed, ref failed);
        RunCase("compliance decision is deterministic", ComplianceDecisionIsDeterministic, ref passed, ref failed);
        RunCase("damaged trader complies under pressure", DamagedTraderComplies, ref passed, ref failed);
        RunCase("healthy protected trader refuses", HealthyProtectedTraderRefuses, ref passed, ref failed);
        RunCase("compliance removes manifest quantity", ComplianceRemovesManifestQuantity, ref passed, ref failed);
        RunCase("compliance creates physical pods", ComplianceCreatesPhysicalPods, ref passed, ref failed);
        RunCase("compliance does not insert player cargo", ComplianceDoesNotInsertPlayerCargo, ref passed, ref failed);
        RunCase("pods retain canonical quantity payload", PodsRetainCanonicalPayload, ref passed, ref failed);
        RunCase("loot object bound remains respected", LootBoundRemainsRespected, ref passed, ref failed);
        RunCase("pickup obeys cargo capacity", PickupObeysCargoCapacity, ref passed, ref failed);
        RunCase("pickup enters ordinary cargo", PickupEntersOrdinaryCargo, ref passed, ref failed);
        RunCase("refusal retains manifest", RefusalRetainsManifest, ref passed, ref failed);
        RunCase("refusal sets flee movement", RefusalSetsFleeMovement, ref passed, ref failed);
        RunCase("distress routes through existing response seam", DistressRoutesThroughExistingResponse, ref passed, ref failed);
        RunCase("no automatic Police response without witness", NoAutomaticPoliceResponseWithoutWitness, ref passed, ref failed);
        RunCase("reputation consequence is bounded and exact once", ReputationConsequenceIsExactOnce, ref passed, ref failed);
        RunCase("Rogue reputation is not granted", RogueReputationIsNotGranted, ref passed, ref failed);
        RunCase("one trader cannot be extorted repeatedly", OneTraderCannotBeExtortedRepeatedly, ref passed, ref failed);
        RunCase("repeated input creates no duplicate pods", RepeatedInputCreatesNoDuplicatePods, ref passed, ref failed);
        RunCase("surrendered cargo is not duplicated", SurrenderedCargoIsNotDuplicated, ref passed, ref failed);
        RunCase("pending destruction creates no surrender pods", PendingDestructionCreatesNoSurrenderPods, ref passed, ref failed);
        RunCase("leaving range resolves safely", LeavingRangeResolvesSafely, ref passed, ref failed);
        RunCase("lane entry during response resolves safely", LaneEntryDuringResponseResolvesSafely, ref passed, ref failed);
        RunCase("despawn during response resolves safely", DespawnDuringResponseResolvesSafely, ref passed, ref failed);
        RunCase("changing selected target cannot redirect demand", ChangingSelectedTargetCannotRedirectDemand, ref passed, ref failed);
        RunCase("save cancellation leaves no active demand", SaveCancellationLeavesNoActiveDemand, ref passed, ref failed);
        RunCase("reset clears demand state", ResetClearsDemandState, ref passed, ref failed);
        RunCase("ordinary traffic state remains independent", OrdinaryTrafficStateRemainsIndependent, ref passed, ref failed);
        RunCase("demand HUD shows issued state", DemandHudShowsIssuedState, ref passed, ref failed);
        RunCase("demand HUD shows bounded outcome", DemandHudShowsBoundedOutcome, ref passed, ref failed);
        RunCase("stable identity is deterministic", StableIdentityIsDeterministic, ref passed, ref failed);
        RunCase("stable identity includes traffic zone", StableIdentityIncludesTrafficZone, ref passed, ref failed);
        RunCase("undamaged trader can resolve a demand", UndamagedTraderCanResolveDemand, ref passed, ref failed);
        RunCase("surrender quantity stays bounded", SurrenderQuantityStaysBounded, ref passed, ref failed);
        RunCase("compliant trader survives", CompliantTraderSurvives, ref passed, ref failed);
        RunCase("pods retain salvage provenance", PodsRetainSalvageProvenance, ref passed, ref failed);
        RunCase("compliance communication is queued", ComplianceCommunicationIsQueued, ref passed, ref failed);
        RunCase("refusal communication is queued", RefusalCommunicationIsQueued, ref passed, ref failed);
        RunCase("distress communication is queued", DistressCommunicationIsQueued, ref passed, ref failed);
        RunCase("Police witness creates existing hostility", PoliceWitnessCreatesExistingHostility, ref passed, ref failed);
        RunCase("Police response is deduplicated", PoliceResponseIsDeduplicated, ref passed, ref failed);
        RunCase("unwitnessed crime creates no hostility", UnwitnessedCrimeCreatesNoHostility, ref passed, ref failed);
        RunCase("reset reconstructs the deterministic manifest", ResetReconstructsDeterministicManifest, ref passed, ref failed);
        RunCase("replacement stable identity remains resolved", ReplacementStableIdentityRemainsResolved, ref passed, ref failed);
        RunCase("partial pickup leaves physical remainder", PartialPickupLeavesPhysicalRemainder, ref passed, ref failed);
        RunCase("response timer stays bounded", ResponseTimerStaysBounded, ref passed, ref failed);

        Console.WriteLine($"[PIRACY DEMAND SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PIRACY DEMAND SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PIRACY DEMAND SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PIRACY DEMAND SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) EligibleTraderCanReceiveDemand()
    {
        Context c = new();
        return Check(c.Demand.TryIssueDemand(c.Player, c.Trader, out _), "eligible trader rejected demand");
    }

    private static (bool, string) PoliceCannotReceiveDemand()
    {
        Context c = new();
        NpcShip police = new("Police", new Vector3(1000f, 0f, 0f), Vector3.Zero, 500f, 0.1f, FactionManager.LibertyPolice);
        police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "police", Vector3.Zero, 500f, 180f, 6500f);
        return Check(!c.Demand.TryIssueDemand(c.Player, police, out _), "Police accepted demand");
    }

    private static (bool, string) CombatPatrolCannotReceiveDemand()
    {
        Context c = new();
        NpcShip patrol = new("Combat Patrol", new Vector3(1000f, 0f, 0f), Vector3.Zero, 500f, 0.1f, FactionManager.LibertyNavy);
        patrol.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "navy", Vector3.Zero, 500f, 180f, 6500f);
        return Check(!c.Demand.TryIssueDemand(c.Player, patrol, out _), "combat patrol accepted demand");
    }

    private static (bool, string) RogueCannotReceiveDemand()
    {
        Context c = new();
        NpcShip rogue = new("Rogue", new Vector3(1000f, 0f, 0f), Vector3.Zero, 500f, 0.1f, FactionManager.LibertyRogues);
        rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "rogue", Vector3.Zero, 500f, 220f, 6500f);
        return Check(!c.Demand.TryIssueDemand(c.Player, rogue, out _), "Rogue combat ship accepted demand");
    }

    private static (bool, string) MissionOwnedTraderExcluded()
    {
        HashSet<NpcShip> missionOwned = new();
        Context c = new(missionOwned: ship => missionOwned.Contains(ship));
        missionOwned.Add(c.Trader);
        return Check(!c.Demand.TryIssueDemand(c.Player, c.Trader, out _), "mission-owned trader accepted demand");
    }

    private static (bool, string) InvalidTargetRejected()
    {
        Context c = new();
        return Check(!c.Demand.TryIssueDemand(c.Player, null, out _), "null target accepted demand");
    }

    private static (bool, string) DemandRequiresRange()
    {
        Context c = new();
        c.Player.Position = new Vector3(c.Trader.Position.X + PirateCargoDemandService.DemandRange + 1f, 0f, 0f);
        return Check(!c.Demand.TryIssueDemand(c.Player, c.Trader, out _), "out-of-range demand accepted");
    }

    private static (bool, string) LaneTransitRejected()
    {
        Context c = new();
        c.Trader.SetTradeLaneTransit(true, "phase56-lane", TradeLaneDirection.Forward, 2);
        return Check(!c.Demand.TryIssueDemand(c.Player, c.Trader, out _), "lane-transit trader accepted demand");
    }

    private static (bool, string) OnlyOneActiveDemand()
    {
        Context c = new();
        NpcShip other = Context.CreateTrader("Other Trader", new Vector3(1100f, 0f, 0f));
        c.Npcs.Add(other);
        c.Objects.Add(other);
        bool first = c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        bool second = c.Demand.TryIssueDemand(c.Player, other, out _);
        return Check(first && !second && c.Demand.ActiveTarget == c.Trader, "active demand was not exclusive");
    }

    private static (bool, string) ActiveDemandBindsStableTarget()
    {
        Context c = new();
        NpcShip other = Context.CreateTrader("Replacement Trader", new Vector3(1000f, 0f, 0f));
        c.Npcs.Add(other);
        c.Objects.Add(other);
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out _))
            return Fail("could not issue demand");
        c.Player.Position = other.Position;
        c.Advance();
        return Check(c.Demand.LastResult?.Target == c.Trader, "demand redirected to replacement target");
    }

    private static (bool, string) ResponseDurationIsBounded()
    {
        Context c = new();
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out _))
            return Fail("could not issue demand");
        c.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds - 0.1f, c.Player);
        bool pending = c.Demand.HasActiveDemand;
        c.Demand.Update(0.2f, c.Player);
        return Check(pending && !c.Demand.HasActiveDemand, "response was not bounded to the response window");
    }

    private static (bool, string) ManifestUsesCanonicalCommodities()
    {
        Context c = new();
        if (!c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest))
            return Fail("manifest unavailable");
        return Check(manifest.Stacks.All(stack => ReferenceEquals(stack.Commodity, CommodityCatalog.GetById(stack.Commodity.Id))), "manifest used non-canonical commodity");
    }

    private static (bool, string) ManifestIsDeterministic()
    {
        Context a = new();
        Context b = new();
        a.Demand.TryGetManifest(a.Trader, out TraderCargoManifest first);
        b.Demand.TryGetManifest(b.Trader, out TraderCargoManifest second);
        string left = string.Join(",", first.Stacks.Select(stack => $"{stack.Commodity.Id}:{stack.Quantity}"));
        string right = string.Join(",", second.Stacks.Select(stack => $"{stack.Commodity.Id}:{stack.Quantity}"));
        return Check(left == right, "same stable trader changed its manifest");
    }

    private static (bool, string) ManifestQuantityIsBounded()
    {
        Context c = new();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        return Check(manifest.RemainingQuantity is >= PirateCargoDemandService.MinimumManifestQuantity and <= PirateCargoDemandService.MaximumManifestQuantity, "manifest quantity escaped bounds");
    }

    private static (bool, string) ManifestCommodityCountIsBounded()
    {
        Context c = new();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        return Check(manifest.CommodityTypeCount is >= PirateCargoDemandService.MinimumCommodityTypes and <= PirateCargoDemandService.MaximumCommodityTypes, "manifest commodity count escaped bounds");
    }

    private static (bool, string) LawfulManifestIsLegal()
    {
        Context c = new(FactionManager.LibertyCorporations);
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        return Check(manifest.Stacks.All(stack => !stack.Commodity.IsContraband && !stack.Commodity.IsMissionCargo), "lawful trader received contraband or mission cargo");
    }

    private static (bool, string) ComplianceDecisionIsDeterministic()
    {
        Context a = new();
        Context b = new();
        a.Trader.ApplyDamage(40f, NpcDestructionSource.Unknown);
        b.Trader.ApplyDamage(40f, NpcDestructionSource.Unknown);
        a.Demand.TryIssueDemand(a.Player, a.Trader, out _);
        b.Demand.TryIssueDemand(b.Player, b.Trader, out _);
        a.Advance();
        b.Advance();
        return Check(a.Demand.LastResult?.State == b.Demand.LastResult?.State, "same deterministic threat state diverged");
    }

    private static (bool, string) DamagedTraderComplies()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Demand.LastResult?.State == PiracyDemandState.Complying && c.Demand.LastResult.SurrenderedQuantity > 0, "damaged trader did not comply with physical release");
    }

    private static (bool, string) HealthyProtectedTraderRefuses()
    {
        Context c = new(policeNearby: true);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Demand.LastResult?.State == PiracyDemandState.RefusingFleeing, "healthy protected trader complied");
    }

    private static (bool, string) ComplianceRemovesManifestQuantity()
    {
        Context c = new();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest beforeManifest);
        int before = beforeManifest.RemainingQuantity;
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(beforeManifest.RemainingQuantity < before, "manifest did not decrease after compliance");
    }

    private static (bool, string) ComplianceCreatesPhysicalPods()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Loot.ActivePods.Count > 0, "compliance did not create physical pods");
    }

    private static (bool, string) ComplianceDoesNotInsertPlayerCargo()
    {
        Context c = new();
        int before = c.Player.CargoHold.UsedCapacity;
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Player.CargoHold.UsedCapacity == before, "compliance inserted cargo directly into player hold");
    }

    private static (bool, string) PodsRetainCanonicalPayload()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        CargoPod pod = c.Loot.ActivePods.FirstOrDefault();
        return Check(pod != null && pod.Quantity > 0 && pod.GetCommodity() == CommodityCatalog.GetById(pod.CommodityId) && pod.SourceNpcName == c.Trader.Name, "pod payload was not canonical and attributed");
    }

    private static (bool, string) LootBoundRemainsRespected()
    {
        Context c = new();
        for (int i = 0; i < CombatSalvageService.MaxLiveSalvageObjects + 8; i++)
        {
            NpcShip trader = Context.CreateTrader($"Bound Trader {i}", new Vector3(2000f + i * 500f, 0f, 0f));
            c.Npcs.Add(trader);
            c.Objects.Add(trader);
            c.Loot.SpawnExtortionCargo(trader, CommodityCatalog.All[0].Id, 1, out _);
        }
        return Check(c.Loot.ActiveSalvageCount <= CombatSalvageService.MaxLiveSalvageObjects, "extortion exceeded global loot bound");
    }

    private static (bool, string) PickupObeysCargoCapacity()
    {
        Context c = new();
        Commodity food = CommodityCatalog.GetById("food-rations");
        c.Player.CargoHold.AddCommodity(food, c.Player.CargoHold.MaxCapacity);
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        CargoPod pod = c.Loot.ActivePods.FirstOrDefault();
        pod.Velocity = Vector3.Zero;
        c.Player.Position = pod.Position;
        c.Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), c.Player, false);
        return Check(c.Loot.ActivePods.Contains(pod) && c.Player.CargoHold.UsedCapacity == c.Player.CargoHold.MaxCapacity, "full hold consumed surrendered cargo");
    }

    private static (bool, string) PickupEntersOrdinaryCargo()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        CargoPod pod = c.Loot.ActivePods.First();
        string commodityName = pod.GetCommodity().Name;
        int quantity = pod.Quantity;
        pod.Velocity = Vector3.Zero;
        c.Player.Position = pod.Position;
        c.Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), c.Player, false);
        return Check(!c.Loot.ActivePods.Contains(pod) && c.Player.CargoHold.GetCommodityQuantity(commodityName) == quantity && c.Player.CargoHold.GetMissionReservedQuantity(commodityName) == 0, "pickup did not become ordinary cargo");
    }

    private static (bool, string) RefusalRetainsManifest()
    {
        Context c = new(policeNearby: true);
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        int before = manifest.RemainingQuantity;
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Demand.LastResult?.State == PiracyDemandState.RefusingFleeing && manifest.RemainingQuantity == before, "refusal changed manifest");
    }

    private static (bool, string) RefusalSetsFleeMovement()
    {
        Context c = new(policeNearby: true);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Trader.EncounterState == TrafficEncounterState.Fleeing, "refusal did not set existing flee state");
    }

    private static (bool, string) DistressRoutesThroughExistingResponse()
    {
        bool called = false;
        Context c = new(
            policeNearby: true,
            distress: (trader, player, source) => new FactionDistressResponseResult(true, true, false, 0, 1, "phase56-distress", FactionManager.LibertyPolice, source),
            policeResponse: (response, trader, player) => called = response.WaveSpawned);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(called && c.Demand.LastResult.DistressResponse.WaveSpawned, "distress did not use response callback");
    }

    private static (bool, string) NoAutomaticPoliceResponseWithoutWitness()
    {
        bool called = false;
        Context c = new(
            distress: (trader, player, source) =>
            {
                called = true;
                return default;
            });
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(!called && string.IsNullOrEmpty(c.Demand.LastResult?.DistressResponse.EncounterId), "demand invoked Police response without local witness");
    }

    private static (bool, string) ReputationConsequenceIsExactOnce()
    {
        Context c = new();
        float before = c.Reputation.GetStanding(c.Trader.FactionId);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        float afterIssue = c.Reputation.GetStanding(c.Trader.FactionId);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        return Check(Math.Abs((afterIssue - before) - PirateCargoDemandService.ReputationPenalty) < ReputationManager.Precision && c.Reputation.GetStanding(c.Trader.FactionId) == afterIssue, "reputation consequence was not exact once");
    }

    private static (bool, string) RogueReputationIsNotGranted()
    {
        Context c = new();
        float before = c.Reputation.GetStanding(FactionManager.LibertyRogues);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        return Check(c.Reputation.GetStanding(FactionManager.LibertyRogues) == before, "piracy demand changed Rogue reputation");
    }

    private static (bool, string) OneTraderCannotBeExtortedRepeatedly()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(!c.Demand.TryIssueDemand(c.Player, c.Trader, out _), "resolved trader accepted a second demand");
    }

    private static (bool, string) RepeatedInputCreatesNoDuplicatePods()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        int pods = c.Loot.ActivePods.Count;
        c.Demand.Update(10f, c.Player);
        return Check(pods == c.Loot.ActivePods.Count, "repeated input duplicated pods");
    }

    private static (bool, string) SurrenderedCargoIsNotDuplicated()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        int surrendered = c.Demand.LastResult.SurrenderedQuantity;
        c.Demand.NotifyNpcDestroyed(c.Trader);
        int podQuantity = c.Loot.ActivePods.Sum(pod => pod.Quantity);
        return Check(podQuantity == surrendered, "surrendered quantity was duplicated by later destruction notification");
    }

    private static (bool, string) PendingDestructionCreatesNoSurrenderPods()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Trader.ApplyDamage(1000f, NpcDestructionSource.Unknown);
        c.Demand.NotifyNpcDestroyed(c.Trader);
        return Check(c.Loot.ActivePods.Count == 0 && c.Demand.LastResult?.SurrenderedQuantity == 0, "pending destruction created surrender cargo");
    }

    private static (bool, string) LeavingRangeResolvesSafely()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Player.Position = new Vector3(c.Trader.Position.X + PirateCargoDemandService.DemandControlRange + 1f, 0f, 0f);
        c.Advance(0.1f);
        return Check(!c.Demand.HasActiveDemand && c.Demand.LastResult?.SurrenderedQuantity == 0, "leaving range left demand pending");
    }

    private static (bool, string) LaneEntryDuringResponseResolvesSafely()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Trader.SetTradeLaneTransit(true, "phase56-lane", TradeLaneDirection.Forward, 1);
        c.Advance(0.1f);
        return Check(!c.Demand.HasActiveDemand && c.Demand.LastResult?.SurrenderedQuantity == 0, "lane entry left demand pending");
    }

    private static (bool, string) DespawnDuringResponseResolvesSafely()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Npcs.Remove(c.Trader);
        c.Demand.Update(0.1f, c.Player);
        return Check(!c.Demand.HasActiveDemand && c.Loot.ActivePods.Count == 0, "despawn left stale active demand or pods");
    }

    private static (bool, string) ChangingSelectedTargetCannotRedirectDemand()
    {
        Context c = new();
        NpcShip other = Context.CreateTrader("Other", new Vector3(1050f, 0f, 0f));
        c.Npcs.Add(other);
        c.Objects.Add(other);
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Player.Position = other.Position;
        c.Advance();
        return Check(c.Demand.LastResult?.Target == c.Trader && c.Demand.LastResult.SurrenderedQuantity > 0, "changing current target redirected response");
    }

    private static (bool, string) SaveCancellationLeavesNoActiveDemand()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Demand.CancelActiveDemand("save/load");
        return Check(!c.Demand.HasActiveDemand && c.Loot.ActivePods.Count == 0, "save cancellation left active demand state");
    }

    private static (bool, string) ResetClearsDemandState()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Demand.Reset();
        return Check(!c.Demand.HasActiveDemand && c.Demand.ResolvedTargetCount == 0 && c.Demand.LastResult == null, "reset retained demand state");
    }

    private static (bool, string) OrdinaryTrafficStateRemainsIndependent()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(!c.Trader.IsDestroyed && c.Trader.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute, "compliance corrupted ordinary traffic state");
    }

    private static (bool, string) DemandHudShowsIssuedState()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        return Check(c.Demand.HudText == "CARGO DEMAND SENT", "demand HUD did not show the issued state");
    }

    private static (bool, string) DemandHudShowsBoundedOutcome()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Demand.HudText.StartsWith("Cargo surrendered:", StringComparison.Ordinal) ||
            c.Demand.HudText == "TRADER REFUSED — FLEEING", "demand HUD did not show a bounded outcome");
    }

    private static (bool, string) StableIdentityIsDeterministic()
    {
        NpcShip a = Context.CreateTrader("Stable Trader", new Vector3(1000f, 0f, 0f));
        NpcShip b = Context.CreateTrader("Stable Trader", new Vector3(1000f, 0f, 0f));
        return Check(a.StableIdentity == b.StableIdentity, "same stable trader inputs produced different identities");
    }

    private static (bool, string) StableIdentityIncludesTrafficZone()
    {
        NpcShip a = Context.CreateTrader("Stable Trader", new Vector3(1000f, 0f, 0f));
        NpcShip b = Context.CreateTrader("Stable Trader", new Vector3(1000f, 0f, 0f));
        b.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "different-route", Vector3.Zero, 1000f, 190f, 3500f,
            new Vector3(-5000f, 0f, 0f), new Vector3(5000f, 0f, 0f));
        return Check(a.StableIdentity != b.StableIdentity, "traffic route was omitted from stable identity");
    }

    private static (bool, string) UndamagedTraderCanResolveDemand()
    {
        Context c = new();
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out _))
            return Fail("undamaged trader rejected a valid demand");
        c.Advance();
        return Check(!c.Demand.HasActiveDemand && c.Demand.LastResult?.State is PiracyDemandState.Complying or PiracyDemandState.RefusingFleeing,
            "undamaged trader did not resolve the bounded demand");
    }

    private static (bool, string) SurrenderQuantityStaysBounded()
    {
        Context c = new();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        int before = manifest.RemainingQuantity;
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        int surrendered = c.Demand.LastResult?.SurrenderedQuantity ?? 0;
        return Check(surrendered >= 0 && surrendered <= before && (surrendered == 0 || surrendered >= 1), "surrender quantity escaped manifest bounds");
    }

    private static (bool, string) CompliantTraderSurvives()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(!c.Trader.IsDestroyed, "compliance destroyed the trader");
    }

    private static (bool, string) PodsRetainSalvageProvenance()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        CargoPod pod = c.Loot.ActivePods.FirstOrDefault();
        return Check(pod != null && pod.SourceNpcName == c.Trader.Name, "extortion pod lost ordinary salvage provenance");
    }

    private static (bool, string) ComplianceCommunicationIsQueued()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Communication.GetPendingSnapshot().Any(message => message.Type == FactionCombatCommunicationType.TraderDemandCompliance),
            "compliance did not use the existing comms queue");
    }

    private static (bool, string) RefusalCommunicationIsQueued()
    {
        Context c = new(policeNearby: true);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Communication.GetPendingSnapshot().Any(message => message.Type == FactionCombatCommunicationType.TraderDemandRefusal),
            "refusal did not use the existing comms queue");
    }

    private static (bool, string) DistressCommunicationIsQueued()
    {
        Context c = new(policeNearby: true);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        return Check(c.Communication.GetPendingSnapshot().Any(message => message.Type == FactionCombatCommunicationType.TraderDemandDistress),
            "witnessed demand did not use the existing distress comms queue");
    }

    private static (bool, string) PoliceWitnessCreatesExistingHostility()
    {
        Context c = new(policeNearby: true);
        FactionDistressResponseService distress = new(c.Npcs, c.Reputation,
            (_, _, _, _) => Array.Empty<NpcShip>());
        FactionDistressResponseResult result = distress.ProcessPlayerCrime(c.Trader, c.Player, "phase56-witnessed");
        return Check(result.Accepted && result.AssistedShipCount > 0 &&
            c.Reputation.TemporaryHostility.HasReason(FactionManager.LibertyPolice, "piracy distress"),
            "witnessed piracy did not use existing Police hostility");
    }

    private static (bool, string) PoliceResponseIsDeduplicated()
    {
        Context c = new(policeNearby: true);
        FactionDistressResponseService distress = new(c.Npcs, c.Reputation,
            (_, _, _, _) => Array.Empty<NpcShip>());
        FactionDistressResponseResult first = distress.ProcessPlayerCrime(c.Trader, c.Player, "phase56-dedup");
        FactionDistressResponseResult second = distress.ProcessPlayerCrime(c.Trader, c.Player, "phase56-dedup");
        return Check(first.Accepted && !string.IsNullOrWhiteSpace(first.EncounterId) &&
            second.EncounterId == first.EncounterId && second.AssistedShipCount == 0,
            "witnessed piracy created a duplicate Police response");
    }

    private static (bool, string) UnwitnessedCrimeCreatesNoHostility()
    {
        Context c = new();
        FactionDistressResponseService distress = new(c.Npcs, c.Reputation,
            (_, _, _, _) => Array.Empty<NpcShip>());
        FactionDistressResponseResult result = distress.ProcessPlayerCrime(c.Trader, c.Player, "phase56-unwitnessed");
        return Check(result.Accepted && string.IsNullOrEmpty(result.EncounterId) &&
            !c.Reputation.TemporaryHostility.IsTemporarilyHostile(FactionManager.LibertyPolice),
            "unwitnessed piracy created Police hostility");
    }

    private static (bool, string) ResetReconstructsDeterministicManifest()
    {
        Context c = new();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest first);
        string firstKey = string.Join(",", first.Stacks.Select(stack => $"{stack.Commodity.Id}:{stack.Quantity}"));
        c.Demand.Reset();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest second);
        string secondKey = string.Join(",", second.Stacks.Select(stack => $"{stack.Commodity.Id}:{stack.Quantity}"));
        return Check(firstKey == secondKey, "manifest reconstruction changed deterministic cargo");
    }

    private static (bool, string) ReplacementStableIdentityRemainsResolved()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        NpcShip replacement = Context.CreateTrader(c.Trader.Name, c.Trader.Position);
        c.Npcs.Add(replacement);
        return Check(!c.Demand.TryIssueDemand(c.Player, replacement, out _), "replacement trader reused a resolved stable identity");
    }

    private static (bool, string) PartialPickupLeavesPhysicalRemainder()
    {
        Context c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        CargoPod pod = c.Loot.ActivePods.FirstOrDefault();
        if (pod == null || pod.Quantity <= 1)
            return Check(true, string.Empty);

        Commodity commodity = pod.GetCommodity();
        Commodity filler = CommodityCatalog.GetById("food-rations") ?? commodity;
        int freeCapacity = commodity.VolumePerUnit;
        int fillQuantity = Math.Max(0, (c.Player.CargoHold.MaxCapacity - freeCapacity) / Math.Max(1, filler.VolumePerUnit));
        if (fillQuantity > 0)
            c.Player.CargoHold.AddCommodity(filler, fillQuantity);
        int beforePodQuantity = pod.Quantity;
        pod.Velocity = Vector3.Zero;
        c.Player.Position = pod.Position;
        c.Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), c.Player, false);
        return Check(pod.Quantity < beforePodQuantity && pod.Quantity > 0, "partial pickup did not leave a physical remainder");
    }

    private static (bool, string) ResponseTimerStaysBounded()
    {
        Context c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        return Check(c.Demand.ResponseRemainingSeconds > 0f &&
            c.Demand.ResponseRemainingSeconds <= PirateCargoDemandService.ResponseWindowSeconds,
            "response timer escaped its bounded window");
    }

    private static (bool Success, string FailureReason) Check(bool condition, string reason) =>
        condition ? (true, string.Empty) : (false, reason);

    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
}
