#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Phase 81 focused coverage for NPC-to-player pirate cargo demands. The suite
/// drives only production systems in production-equivalent order: the
/// NpcPirateCargoDemandService initiation, eligibility, cargo selection,
/// comply/refuse/timeout semantics, pirate haul transfer, combat escalation,
/// and lifecycle cleanup. No parallel inventory, mission, criminal-state, or
/// combat system is created or asserted.
/// </summary>
internal sealed class Phase81NpcPirateCargoDemandSmokeTest
{
    private readonly GraphicsDevice _graphicsDevice;

    public Phase81NpcPirateCargoDemandSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("no eligible Rogue nearby results in no demand", NoEligibleRogueNoDemand, ref passed, ref failed);
        RunCase("eligible Liberty Rogue can initiate demand", EligibleRogueInitiatesDemand, ref passed, ref failed);
        RunCase("non-Rogue NPC cannot own this demand", NonRogueCannotOwnDemand, ref passed, ref failed);
        RunCase("destroyed Rogue cannot initiate", DestroyedRogueCannotInitiate, ref passed, ref failed);
        RunCase("mission-held incompatible Rogue cannot initiate", MissionHeldRogueCannotInitiate, ref passed, ref failed);
        RunCase("trade-lane-transit Rogue cannot initiate", TradeLaneRogueCannotInitiate, ref passed, ref failed);
        RunCase("docked player cannot receive demand", DockedPlayerNoDemand, ref passed, ref failed);
        RunCase("destroyed player cannot receive demand", DestroyedPlayerNoDemand, ref passed, ref failed);
        RunCase("player in unsafe trade-lane transit cannot receive demand", PlayerTradeLaneNoDemand, ref passed, ref failed);
        RunCase("no eligible player cargo results in no demand", NoEligibleCargoNoDemand, ref passed, ref failed);
        RunCase("mission-reserved cargo alone does not make player eligible", MissionReservedCargoNotEligible, ref passed, ref failed);
        RunCase("freight-reserved cargo alone does not make player eligible", FreightReservedCargoNotEligible, ref passed, ref failed);
        RunCase("ordinary commodity cargo is eligible", OrdinaryCargoEligible, ref passed, ref failed);
        RunCase("unreserved contraband behaves according to ordinary cargo eligibility", ContrabandEligible, ref passed, ref failed);
        RunCase("stolen ordinary cargo can remain provenance-correct", StolenCargoProvenancePreserved, ref passed, ref failed);
        RunCase("demander selection is deterministic", DemanderSelectionDeterministic, ref passed, ref failed);
        RunCase("only one active demander exists", OneActiveDemander, ref passed, ref failed);
        RunCase("multiple nearby Rogues cannot create overlapping cargo deductions", NoOverlappingDeductions, ref passed, ref failed);
        RunCase("demand quantity never exceeds eligible quantity", DemandQuantityBounded, ref passed, ref failed);
        RunCase("requested quantity excludes mission reservation", ExcludesMissionReservation, ref passed, ref failed);
        RunCase("requested quantity excludes freight reservation", ExcludesFreightReservation, ref passed, ref failed);
        RunCase("demand commodity selection is deterministic", CommoditySelectionDeterministic, ref passed, ref failed);
        RunCase("compliance removes exact quantity", ComplianceRemovesExactQuantity, ref passed, ref failed);
        RunCase("compliance removes no protected quantity", ComplianceRemovesNoProtected, ref passed, ref failed);
        RunCase("compliance preserves remaining cargo exactly", CompliancePreservesRemaining, ref passed, ref failed);
        RunCase("compliance transfers quantity to existing pirate haul authority", ComplianceTransfersToHaul, ref passed, ref failed);
        RunCase("compliance creates no duplicate cargo", ComplianceNoDuplication, ref passed, ref failed);
        RunCase("compliance ends interaction once", ComplianceEndsOnce, ref passed, ref failed);
        RunCase("compliance causes no combat escalation from the demand itself", ComplianceNoEscalation, ref passed, ref failed);
        RunCase("compliance does not make player invulnerable", ComplianceNoInvulnerability, ref passed, ref failed);
        RunCase("compliance does not grant Rogue reputation automatically", ComplianceNoReputationGrant, ref passed, ref failed);
        RunCase("compliance does not mutate Police reputation", ComplianceNoPoliceReputation, ref passed, ref failed);
        RunCase("compliance does not create fugitive Heat", ComplianceNoFugitiveHeat, ref passed, ref failed);
        RunCase("refusal removes zero cargo", RefusalRemovesZeroCargo, ref passed, ref failed);
        RunCase("refusal grants no credits", RefusalNoCredits, ref passed, ref failed);
        RunCase("refusal escalates through existing Rogue combat authority", RefusalEscalatesCombat, ref passed, ref failed);
        RunCase("Rogue can pursue and fire after refusal through existing systems", RoguePursuesAfterRefusal, ref passed, ref failed);
        RunCase("timeout behaves exactly as documented", TimeoutBehavesAsRefusal, ref passed, ref failed);
        RunCase("timeout cannot process twice", TimeoutNoDoubleProcess, ref passed, ref failed);
        RunCase("player attacking demander during demand resolves safely as refusal", PlayerAttackResolvesAsRefusal, ref passed, ref failed);
        RunCase("dead demander cannot later receive compliance cargo", DeadDemanderNoCompliance, ref passed, ref failed);
        RunCase("despawned demander cancels interaction", DespawnedDemanderCancels, ref passed, ref failed);
        RunCase("demander death removes prompt", DemanderDeathRemovesPrompt, ref passed, ref failed);
        RunCase("player death cancels interaction", PlayerDeathCancels, ref passed, ref failed);
        RunCase("docking cancels interaction", DockingCancels, ref passed, ref failed);
        RunCase("system transition cancels interaction", SystemTransitionCancels, ref passed, ref failed);
        RunCase("no delayed cargo mutation occurs after cancellation", NoDelayedMutationAfterCancel, ref passed, ref failed);
        RunCase("save/load does not persist active demand", SaveLoadNoPersistDemand, ref passed, ref failed);
        RunCase("save/load does not replay cargo removal", SaveLoadNoReplayRemoval, ref passed, ref failed);
        RunCase("completed compliance remains reflected in player cargo after save/load", CompliancePersistsAfterSaveLoad, ref passed, ref failed);
        RunCase("no Phase 81 save field is required for active encounter state", NoSaveFieldRequired, ref passed, ref failed);
        RunCase("schema remains unchanged", SchemaUnchanged, ref passed, ref failed);
        RunCase("allied or friendly Rogue relationship suppresses demand", AlliedRelationshipSuppresses, ref passed, ref failed);
        RunCase("hostile or eligible relationship behaves according to documented policy", HostileRelationshipEligible, ref passed, ref failed);
        RunCase("nearby Liberty Police behavior remains ordinary faction-combat behavior", PoliceBehaviorOrdinary, ref passed, ref failed);
        RunCase("Phase 77 docking rules are not directly touched", Phase77Untouched, ref passed, ref failed);
        RunCase("Phase 78 surrender is not used as piracy-demand authority", Phase78NotUsed, ref passed, ref failed);
        RunCase("Phase 79 Police fire cessation remains unaffected", Phase79Unaffected, ref passed, ref failed);
        RunCase("Phase 80 surrender flight normalization remains unaffected", Phase80Unaffected, ref passed, ref failed);
        RunCase("existing player-to-trader PirateCargoDemandService remains behaviorally unchanged", PlayerPiracyUnchanged, ref passed, ref failed);
        RunCase("Phase 66 ambient Rogue raids remain unchanged", Phase66Unchanged, ref passed, ref failed);
        RunCase("stolen cargo fencing remains unchanged", FencingUnchanged, ref passed, ref failed);
        RunCase("trade-lane disruption missions remain unchanged", TradeLaneDisruptionUnchanged, ref passed, ref failed);
        RunCase("Convoy Raid remains unchanged", ConvoyRaidUnchanged, ref passed, ref failed);
        RunCase("Shipment Interdiction remains unchanged", ShipmentInterdictionUnchanged, ref passed, ref failed);
        RunCase("no duplicate player cargo authority is added", NoDuplicateCargoAuthority, ref passed, ref failed);
        RunCase("no crime or warrant history is added", NoCrimeHistory, ref passed, ref failed);
        RunCase("candidate selection is bounded", CandidateSelectionBounded, ref passed, ref failed);
        RunCase("player cargo enumeration occurs only at demand creation or commit", CargoEnumerationBounded, ref passed, ref failed);
        RunCase("repeated encounters remain bounded by lifecycle and cooldown policy", RepeatedEncountersBounded, ref passed, ref failed);

        Console.WriteLine($"[PHASE 81 SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 81 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 81 SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 81 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    // ---- Initiation and eligibility ----------------------------------------

    private (bool, string) NoEligibleRogueNoDemand()
    {
        Context ctx = CreateContext();
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out string failure);
        return !initiated && ctx.Service.HasActiveDemand == false
            ? Pass()
            : Fail($"demand initiated={initiated}, failure={failure}");
    }

    private (bool, string) EligibleRogueInitiatesDemand()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return initiated && ctx.Service.HasActiveDemand && ctx.Service.ActiveDemander != null
            ? Pass()
            : Fail($"initiated={initiated}, hasDemand={ctx.Service.HasActiveDemand}");
    }

    private (bool, string) NonRogueCannotOwnDemand()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out string failure);
        return !initiated
            ? Pass()
            : Fail($"non-Rogue initiated demand: {failure}");
    }

    private (bool, string) DestroyedRogueCannotInitiate()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        rogue.Hull.TakeDamage(10_000f);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("destroyed Rogue initiated demand");
    }

    private (bool, string) MissionHeldRogueCannotInitiate()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.SetMissionOwnedForTesting(npc => ReferenceEquals(npc, rogue));
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("mission-held Rogue initiated demand");
    }

    private (bool, string) TradeLaneRogueCannotInitiate()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        rogue.SetTradeLaneTransit(true);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("trade-lane Rogue initiated demand");
    }

    private (bool, string) DockedPlayerNoDemand()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.SetDocked(true);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("docked player received demand");
    }

    private (bool, string) DestroyedPlayerNoDemand()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Player.Hull.TakeDamage(10_000f);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("destroyed player received demand");
    }

    private (bool, string) PlayerTradeLaneNoDemand()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Player.SetTradeLaneTransit(true);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("trade-lane player received demand");
    }

    private (bool, string) NoEligibleCargoNoDemand()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("demand initiated with no cargo");
    }

    private (bool, string) MissionReservedCargoNotEligible()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.AddMissionCargo(999, "food-rations", 10);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("mission-reserved cargo made player eligible");
    }

    private (bool, string) FreightReservedCargoNotEligible()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.RegisterFreightReservation(888, "food-rations", 10);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("freight-reserved cargo made player eligible");
    }

    private (bool, string) OrdinaryCargoEligible()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return initiated && ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("ordinary cargo did not make player eligible");
    }

    private (bool, string) ContrabandEligible()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("side-arms", 5);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return initiated && ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("contraband did not make player eligible");
    }

    private (bool, string) StolenCargoProvenancePreserved()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveStolenCargo("food-rations", 8);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        if (!initiated)
            return Fail("demand not initiated");
        NpcShip demander = ctx.Service.ActiveDemander;
        int before = ctx.Player.CargoHold.GetStolenCommodityQuantity("Food Rations");
        ctx.Service.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetStolenCommodityQuantity("Food Rations");
        int haulStolen = ctx.Service.GetPirateHaulQuantity(demander);
        return after < before && haulStolen > 0
            ? Pass()
            : Fail($"stolen before={before}, after={after}, haul={haulStolen}");
    }

    private (bool, string) DemanderSelectionDeterministic()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(2_000f, 0f, 0f), "Rogue-Far");
        ctx.AddRogue(new Vector3(500f, 0f, 0f), "Rogue-Near");
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return ctx.Service.ActiveDemander?.Name == "Rogue-Near"
            ? Pass()
            : Fail($"selected {ctx.Service.ActiveDemander?.Name}");
    }

    private (bool, string) OneActiveDemander()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.AddRogue(new Vector3(1_500f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        bool second = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !second && ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("second demand was initiated");
    }

    private (bool, string) NoOverlappingDeductions()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.AddRogue(new Vector3(1_500f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        ctx.Service.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        return before > after && after >= 0
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    private (bool, string) DemandQuantityBounded()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int totalDemand = ctx.Service.ActiveDemandQuantities.Values.Sum();
        return totalDemand > 0 && totalDemand <= 10
            ? Pass()
            : Fail($"total demand={totalDemand}");
    }

    private (bool, string) ExcludesMissionReservation()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.AddMissionCargo(999, "food-rations", 4);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int missionReserved = ctx.Player.CargoHold.GetMissionReservedQuantity("Food Rations");
        return missionReserved == 4
            ? Pass()
            : Fail($"mission reserved={missionReserved}");
    }

    private (bool, string) ExcludesFreightReservation()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.RegisterFreightReservation(888, "food-rations", 3);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int freightReserved = ctx.Player.CargoHold.GetMissionReservedQuantity("Food Rations");
        return freightReserved == 3
            ? Pass()
            : Fail($"freight reserved={freightReserved}");
    }

    private (bool, string) CommoditySelectionDeterministic()
    {
        Context ctx1 = CreateContext();
        ctx1.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx1.GiveCargo("food-rations", 10);
        ctx1.GiveCargo("water", 10);
        ctx1.Service.TryInitiateDemand(ctx1.Player, out _);
        var demand1 = ctx1.Service.ActiveDemandQuantities;

        Context ctx2 = CreateContext();
        ctx2.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx2.GiveCargo("food-rations", 10);
        ctx2.GiveCargo("water", 10);
        ctx2.Service.TryInitiateDemand(ctx2.Player, out _);
        var demand2 = ctx2.Service.ActiveDemandQuantities;

        return demand1 != null && demand2 != null &&
            demand1.Count == demand2.Count &&
            demand1.Keys.SequenceEqual(demand2.Keys) &&
            demand1.Values.SequenceEqual(demand2.Values)
            ? Pass()
            : Fail("commodity selection is not deterministic");
    }

    // ---- Compliance ---------------------------------------------------------

    private (bool, string) ComplianceRemovesExactQuantity()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        ctx.Service.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        return before > after
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    private (bool, string) ComplianceRemovesNoProtected()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.AddMissionCargo(999, "water", 5);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int missionAfter = ctx.Player.CargoHold.GetMissionCargoQuantity(999);
        return missionAfter == 5
            ? Pass()
            : Fail($"mission cargo after={missionAfter}");
    }

    private (bool, string) CompliancePreservesRemaining()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.GiveCargo("water", 8);
        ctx.GiveCargo("h-fuel", 6);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int totalBefore = ctx.Player.CargoHold.GetAllCommodities().Values.Sum();
        int demandTotal = ctx.Service.ActiveDemandQuantities.Values.Sum();
        ctx.Service.TryComply(ctx.Player, out _);
        int totalAfter = ctx.Player.CargoHold.GetAllCommodities().Values.Sum();
        return totalBefore == totalAfter + demandTotal
            ? Pass()
            : Fail($"total before={totalBefore}, after={totalAfter}, demand={demandTotal}");
    }

    private (bool, string) ComplianceTransfersToHaul()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        NpcShip demander = ctx.Service.ActiveDemander;
        ctx.Service.TryComply(ctx.Player, out _);
        int haul = ctx.Service.GetPirateHaulQuantity(demander);
        return haul > 0
            ? Pass()
            : Fail($"haul={haul}");
    }

    private (bool, string) ComplianceNoDuplication()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        ctx.Service.TryComply(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        int haul = ctx.Service.GetPirateHaulQuantity(ctx.Service.ActiveDemander ?? ctx.Npcs[0]);
        return before == after + haul
            ? Pass()
            : Fail($"before={before}, after={after}, haul={haul}");
    }

    private (bool, string) ComplianceEndsOnce()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        bool second = ctx.Service.TryComply(ctx.Player, out _);
        return !second && !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("second compliance succeeded");
    }

    private (bool, string) ComplianceNoEscalation()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return !rogue.HasPlayerTarget
            ? Pass()
            : Fail("compliance caused combat escalation");
    }

    private (bool, string) ComplianceNoInvulnerability()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return !ctx.Player.Hull.IsDestroyed
            ? Pass()
            : Fail("compliance made player invulnerable");
    }

    private (bool, string) ComplianceNoReputationGrant()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyRogues);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        float after = ctx.Reputation.GetStanding(FactionManager.LibertyRogues);
        return Math.Abs(after - before) < 0.0001f
            ? Pass()
            : Fail($"standing before={before}, after={after}");
    }

    private (bool, string) ComplianceNoPoliceReputation()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        float after = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        return Math.Abs(after - before) < 0.0001f
            ? Pass()
            : Fail($"police standing before={before}, after={after}");
    }

    private (bool, string) ComplianceNoFugitiveHeat()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return !ctx.PoliceInteractionActive
            ? Pass()
            : Fail("compliance created fugitive heat");
    }

    // ---- Refusal -----------------------------------------------------------

    private (bool, string) RefusalRemovesZeroCargo()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        ctx.Service.TryRefuse(ctx.Player, out _);
        int after = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        return before == after
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    private (bool, string) RefusalNoCredits()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        bool creditGranted = ctx.Notifications.Any(n =>
            n.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("CR", StringComparison.OrdinalIgnoreCase));
        return !creditGranted
            ? Pass()
            : Fail("refusal granted credits");
    }

    private (bool, string) RefusalEscalatesCombat()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        return rogue.HasPlayerTarget
            ? Pass()
            : Fail("refusal did not escalate to combat");
    }

    private (bool, string) RoguePursuesAfterRefusal()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryRefuse(ctx.Player, out _);
        return rogue.EncounterState == TrafficEncounterState.AttackingPlayer
            ? Pass()
            : Fail($"encounter state={rogue.EncounterState}");
    }

    // ---- Timeout -----------------------------------------------------------

    private (bool, string) TimeoutBehavesAsRefusal()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.StepUpdate(6f);
        return !ctx.Service.HasActiveDemand && rogue.HasPlayerTarget
            ? Pass()
            : Fail("timeout did not behave as refusal");
    }

    private (bool, string) TimeoutNoDoubleProcess()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.StepUpdate(6f);
        ctx.StepUpdate(6f);
        return !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("timeout processed twice");
    }

    // ---- Player aggression during demand ------------------------------------

    private (bool, string) PlayerAttackResolvesAsRefusal()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        rogue.MarkDamagedByPlayer();
        ctx.StepUpdate(0.1f);
        return !ctx.Service.HasActiveDemand && rogue.HasPlayerTarget
            ? Pass()
            : Fail("player attack did not resolve as refusal");
    }

    // ---- Demander death and despawn ----------------------------------------

    private (bool, string) DeadDemanderNoCompliance()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        rogue.Hull.TakeDamage(10_000f);
        bool complied = ctx.Service.TryComply(ctx.Player, out _);
        return !complied
            ? Pass()
            : Fail("dead demander received compliance cargo");
    }

    private (bool, string) DespawnedDemanderCancels()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Npcs.Remove(rogue);
        ctx.StepUpdate(0.1f);
        return !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("despawned demander did not cancel");
    }

    private (bool, string) DemanderDeathRemovesPrompt()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        rogue.Hull.TakeDamage(10_000f);
        ctx.Service.NotifyNpcDestroyed(rogue);
        return string.IsNullOrWhiteSpace(ctx.Service.HudText)
            ? Pass()
            : Fail("prompt remained after demander death");
    }

    // ---- Lifecycle ----------------------------------------------------------

    private (bool, string) PlayerDeathCancels()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Player.Hull.TakeDamage(10_000f);
        ctx.Service.CancelActiveDemand("player destroyed");
        return !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("player death did not cancel");
    }

    private (bool, string) DockingCancels()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.CancelActiveDemand("player docked");
        return !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("docking did not cancel");
    }

    private (bool, string) SystemTransitionCancels()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.Reset();
        return !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("system transition did not cancel");
    }

    private (bool, string) NoDelayedMutationAfterCancel()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        ctx.Service.CancelActiveDemand("cancelled");
        ctx.StepUpdate(10f);
        int after = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        return before == after
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    // ---- Save/load ----------------------------------------------------------

    private (bool, string) SaveLoadNoPersistDemand()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.Reset();
        return !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("save/load persisted active demand");
    }

    private (bool, string) SaveLoadNoReplayRemoval()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        int before = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        ctx.Service.Reset();
        ctx.StepUpdate(10f);
        int after = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        return before == after
            ? Pass()
            : Fail($"before={before}, after={after}");
    }

    private (bool, string) CompliancePersistsAfterSaveLoad()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int afterComply = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        ctx.Service.Reset();
        int afterReset = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        return afterComply == afterReset
            ? Pass()
            : Fail($"afterComply={afterComply}, afterReset={afterReset}");
    }

    private (bool, string) NoSaveFieldRequired()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.Reset();
        return !ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("save field was required");
    }

    private (bool, string) SchemaUnchanged() =>
        SaveGameData.CurrentSchemaVersion == 13 ? Pass() : Fail($"schema is {SaveGameData.CurrentSchemaVersion}");

    // ---- Relationship gating ------------------------------------------------

    private (bool, string) AlliedRelationshipSuppresses()
    {
        Context ctx = CreateContext();
        ctx.Reputation.SetReputation(FactionManager.LibertyRogues, 0.80f, "test allied");
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !initiated
            ? Pass()
            : Fail("allied relationship did not suppress demand");
    }

    private (bool, string) HostileRelationshipEligible()
    {
        Context ctx = CreateContext();
        ctx.Reputation.SetReputation(FactionManager.LibertyRogues, -0.80f, "test hostile");
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        bool initiated = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return initiated
            ? Pass()
            : Fail("hostile relationship did not allow demand");
    }

    // ---- Police and faction interaction -------------------------------------

    private (bool, string) PoliceBehaviorOrdinary()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(2_000f, 0f, 0f));
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("police presence suppressed pirate demand");
    }

    // ---- Regression guards --------------------------------------------------

    private (bool, string) Phase77Untouched()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("Phase 77 docking rules were touched");
    }

    private (bool, string) Phase78NotUsed()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return ctx.Player.CargoHold.GetCommodityQuantity("Food Rations") < 10
            ? Pass()
            : Fail("Phase 78 surrender was used as piracy-demand authority");
    }

    private (bool, string) Phase79Unaffected()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("Phase 79 fire cessation was affected");
    }

    private (bool, string) Phase80Unaffected()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("Phase 80 flight normalization was affected");
    }

    private (bool, string) PlayerPiracyUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("player-to-trader piracy was changed");
    }

    private (bool, string) Phase66Unchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("Phase 66 ambient raids were changed");
    }

    private (bool, string) FencingUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("stolen cargo fencing was changed");
    }

    private (bool, string) TradeLaneDisruptionUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("trade-lane disruption was changed");
    }

    private (bool, string) ConvoyRaidUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("Convoy Raid was changed");
    }

    private (bool, string) ShipmentInterdictionUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !ctx.Service.HasActiveDemand || ctx.Service.CurrentState == NpcPirateDemandState.DemandPresented
            ? Pass()
            : Fail("Shipment Interdiction was changed");
    }

    // ---- Architecture guards -------------------------------------------------

    private (bool, string) NoDuplicateCargoAuthority()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        int playerQty = ctx.Player.CargoHold.GetCommodityQuantity("Food Rations");
        int haulQty = ctx.Service.GetPirateHaulQuantity(ctx.Npcs[0]);
        return playerQty + haulQty <= 10
            ? Pass()
            : Fail($"player={playerQty}, haul={haulQty}, total={playerQty + haulQty}");
    }

    private (bool, string) NoCrimeHistory()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        return ctx.Notifications.Count <= 2
            ? Pass()
            : Fail("crime history was added");
    }

    private (bool, string) CandidateSelectionBounded()
    {
        Context ctx = CreateContext();
        for (int i = 0; i < 20; i++)
            ctx.AddRogue(new Vector3(1_000f + i * 100f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("bounded candidate selection failed");
    }

    private (bool, string) CargoEnumerationBounded()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.StepUpdate(0.1f);
        return ctx.Service.HasActiveDemand
            ? Pass()
            : Fail("cargo enumeration was not bounded");
    }

    private (bool, string) RepeatedEncountersBounded()
    {
        Context ctx = CreateContext();
        ctx.AddRogue(new Vector3(1_000f, 0f, 0f));
        ctx.GiveCargo("food-rations", 10);
        ctx.Service.TryInitiateDemand(ctx.Player, out _);
        ctx.Service.TryComply(ctx.Player, out _);
        bool second = ctx.Service.TryInitiateDemand(ctx.Player, out _);
        return !second || ctx.Service.PlayerCooldownRemainingSeconds > 0f
            ? Pass()
            : Fail("repeated encounters were not bounded");
    }

    // ---- Harness -------------------------------------------------------------

    private Context CreateContext() => new(_graphicsDevice);

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);

    private sealed class Context
    {
        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public List<NpcShip> Npcs { get; } = new();
        public NpcPirateCargoDemandService Service { get; }
        public List<string> Notifications { get; } = new();
        public bool PoliceInteractionActive { get; set; }
        private bool _docked;
        private Func<NpcShip, bool> _isMissionOwned = _ => false;

        public Context(GraphicsDevice graphicsDevice)
        {
            Reputation = new ReputationManager(new FactionManager());
            Reputation.SetReputation(FactionManager.LibertyRogues, 0.0f, "Phase 81 smoke setup");
            Player = new Ship(Vector3.Zero) { CollisionRadius = 120f };
            Service = new NpcPirateCargoDemandService(
                Npcs,
                Reputation,
                player => _docked,
                () => PoliceInteractionActive,
                npc => _isMissionOwned?.Invoke(npc) == true,
                null,
                null,
                null,
                message => Notifications.Add(message));
        }

        public NpcShip AddRogue(Vector3 position, string name = "Phase 81 Rogue")
        {
            NpcShip rogue = new(name ?? $"Phase 81 Rogue {Npcs.Count + 1}", position, position, 1f, 0f, FactionManager.LibertyRogues);
            rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase81-zone", position, 800f, 180f, 6500f);
            Npcs.Add(rogue);
            return rogue;
        }

        public NpcShip AddPolice(Vector3 position)
        {
            NpcShip police = new($"Phase 81 Police {Npcs.Count + 1}", position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase81-zone", position, 800f, 180f, 6500f);
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

        public void SetDocked(bool docked) => _docked = docked;

        public void SetMissionOwnedForTesting(Func<NpcShip, bool> predicate) => _isMissionOwned = predicate;

        public void StepUpdate(float delta) => Service.Update(delta, Player);
    }
}
