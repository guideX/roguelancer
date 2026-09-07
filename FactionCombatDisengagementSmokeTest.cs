#nullable enable

using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 35 coverage for transient pursuit lifetime, evidence,
/// responder cleanup, reacquisition, and preservation of prior faction rules.
/// </summary>
internal sealed class FactionCombatDisengagementSmokeTest
{
    private sealed class Scenario
    {
        public List<NpcShip> Ships { get; } = new();
        public ReputationManager Reputation { get; } = NewReputation();
        public Ship Player { get; } = new(new Vector3(300f, 0f, 0f));
        public FactionCombatDisengagementService Service { get; }
        public NpcShip Police { get; }
        public NpcShip Rogue { get; }

        public Scenario(float separation = 300f)
        {
            Police = CreateNpc("Phase 35 police", FactionManager.LibertyPolice, Vector3.Zero);
            Rogue = CreateNpc("Phase 35 rogue", FactionManager.LibertyRogues, new Vector3(separation, 0f, 0f));
            Ships.Add(Police);
            Ships.Add(Rogue);
            Service = new FactionCombatDisengagementService(Ships, Reputation);
            Service.Update(0f, Player);
        }
    }

    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("nearby Police to Rogue pursuit remains engaged", NearbyPolicePursuitRemainsEngaged);
        Check("nearby Rogue to Police pursuit remains engaged", NearbyRoguePursuitRemainsEngaged);
        Check("destroyed target clears immediately", DestroyedTargetClears);
        Check("despawned target clears safely", DespawnedTargetClears);
        Check("non-hostile target clears", NonHostileTargetClears);
        Check("temporary player hostility expiry clears target", TemporaryPlayerHostilityExpiryClears);
        Check("permanent player hostility remains authoritative", PermanentPlayerHostilityRemains);
        Check("permanent hostility still allows stale long pursuit to end", PermanentHostilityDoesNotForceInfinitePursuit);
        Check("target inside normal pursuit distance remains engaged", NormalDistanceRemainsEngaged);
        Check("recent evidence may extend soft-radius pursuit", RecentEvidenceExtendsSoftPursuit);
        Check("stale hard-radius pursuit disengages", StaleHardRadiusDisengages);
        Check("stale pursuit expires outside ordinary range", StalePursuitExpires);
        Check("meaningful evidence refreshes pursuit", MeaningfulEvidenceRefreshesPursuit);
        Check("zero damage does not refresh pursuit", ZeroDamageDoesNotRefresh);
        Check("environmental damage does not refresh pursuit", EnvironmentalDamageDoesNotRefresh);
        Check("self damage does not refresh pursuit", SelfDamageDoesNotRefresh);
        Check("invalid attacker does not refresh pursuit", InvalidAttackerDoesNotRefresh);
        Check("Phase 33 responder clears dead assigned attacker", Phase33ResponderClearsDeadTarget);
        Check("Phase 33 responder clears escaped attacker", Phase33ResponderClearsEscapedTarget);
        Check("Phase 33 responder resumes ordinary traffic", Phase33ResponderResumesTraffic);
        Check("Phase 34 responder clears dead assigned attacker", Phase34ResponderClearsDeadTarget);
        Check("Phase 34 responder clears escaped attacker", Phase34ResponderClearsEscapedTarget);
        Check("Phase 34 responder resumes ordinary traffic", Phase34ResponderResumesTraffic);
        Check("cleared responder can acquire a new local hostile", ClearedResponderAcquiresNewHostile);
        Check("exact stale target is briefly suppressed", ExactStaleTargetIsSuppressed);
        Check("unrelated hostile target is not suppressed", UnrelatedHostileIsNotSuppressed);
        Check("disengagement does not mutate player reputation", DisengagementPreservesPlayerReputation);
        Check("disengagement does not alter Phase 30 consequences", DisengagementPreservesPhase30Consequences);
        Check("disengagement does not alter Phase 31 relationship ripple", DisengagementPreservesPhase31Ripple);
        Check("target clearing preserves reinforcement provenance", TargetClearingPreservesProvenance);
        Check("Phase 33 non-recursion remains intact", Phase33NonRecursionRemainsIntact);
        Check("Phase 34 non-recursion remains intact", Phase34NonRecursionRemainsIntact);
        Check("expired encounter evidence cannot force combat", ExpiredEncounterDoesNotForceCombat);
        Check("reset clears Phase 35 transient state", ResetClearsTransientState);
        Check("save schema remains unchanged", SaveSchemaRemainsUnchanged);
        Check("normal targeting reacquires a valid local enemy", NormalTargetingReacquiresLocalEnemy);

        Console.WriteLine($"[FACTION COMBAT DISENGAGEMENT SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION COMBAT DISENGAGEMENT SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION COMBAT DISENGAGEMENT SMOKE] FAIL {label}: {reason}");
    }

    private static bool NearbyPolicePursuitRemainsEngaged()
    {
        Scenario scenario = new();
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(5f, scenario.Player);
        return scenario.Police.FactionCombatTarget == scenario.Rogue &&
            scenario.Police.EncounterState == TrafficEncounterState.AttackingFactionNpc;
    }

    private static bool NearbyRoguePursuitRemainsEngaged()
    {
        Scenario scenario = new();
        scenario.Rogue.SetFactionCombatTarget(scenario.Police);
        scenario.Service.Update(5f, scenario.Player);
        return scenario.Rogue.FactionCombatTarget == scenario.Police &&
            scenario.Rogue.EncounterState == TrafficEncounterState.AttackingFactionNpc;
    }

    private static bool DestroyedTargetClears()
    {
        Scenario scenario = new();
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Rogue.Hull.TakeDamage(scenario.Rogue.Hull.MaxHull);
        scenario.Service.Update(0f, scenario.Player);
        return scenario.Police.FactionCombatTarget == null &&
            scenario.Service.TryGetLastDisengagementReason(scenario.Police, out FactionCombatDisengagementReason reason) &&
            reason == FactionCombatDisengagementReason.TargetDestroyed;
    }

    private static bool DespawnedTargetClears()
    {
        Scenario scenario = new();
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Ships.Remove(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        return scenario.Police.FactionCombatTarget == null &&
            scenario.Police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool NonHostileTargetClears()
    {
        Scenario scenario = new();
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Rogue.FactionId = FactionManager.NeutralCivilians;
        scenario.Service.Update(0f, scenario.Player);
        return scenario.Police.FactionCombatTarget == null &&
            scenario.Police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool TemporaryPlayerHostilityExpiryClears()
    {
        Scenario scenario = new();
        scenario.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice, "Phase 35 smoke", 30f);
        scenario.Police.SetPlayerTarget(scenario.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Reputation.UpdateTemporaryHostility(30f);
        scenario.Service.Update(0f, scenario.Player);
        return scenario.Police.FactionCombatTarget == null &&
            !scenario.Police.HasPlayerTarget &&
            scenario.Police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool PermanentPlayerHostilityRemains()
    {
        Scenario scenario = new();
        scenario.Reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "Phase 35 smoke");
        scenario.Police.SetPlayerTarget(scenario.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        scenario.Service.Update(20f, scenario.Player);
        return scenario.Police.HasPlayerTarget && scenario.Police.HasValidPlayerTarget(scenario.Reputation);
    }

    private static bool PermanentHostilityDoesNotForceInfinitePursuit()
    {
        Scenario scenario = new();
        scenario.Reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "Phase 35 smoke");
        scenario.Player.Position = new Vector3(15_000f, 0f, 0f);
        scenario.Police.SetPlayerTarget(scenario.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.Update(FactionCombatDisengagementService.RecentHostileEvidenceGraceSeconds + 0.1f, scenario.Player);
        return !scenario.Police.HasPlayerTarget &&
            scenario.Police.EncounterState == TrafficEncounterState.Cruising &&
            scenario.Police.HasValidPlayerTarget(scenario.Reputation) == false;
    }

    private static bool NormalDistanceRemainsEngaged()
    {
        Scenario scenario = new(5_000f);
        scenario.Police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase35", Vector3.Zero, 100f, 100f, 6_500f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(31f, scenario.Player);
        return scenario.Police.FactionCombatTarget == scenario.Rogue;
    }

    private static bool RecentEvidenceExtendsSoftPursuit()
    {
        Scenario scenario = new(8_500f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Rogue.SetFactionCombatTarget(scenario.Police);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.RecordNpcDamage(scenario.Rogue, scenario.Police, 5f);
        scenario.Service.Update(9f, scenario.Player);
        return scenario.Police.FactionCombatTarget == scenario.Rogue;
    }

    private static bool StaleHardRadiusDisengages()
    {
        Scenario scenario = new(11_000f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.Update(11f, scenario.Player);
        return scenario.Police.FactionCombatTarget == null;
    }

    private static bool StalePursuitExpires()
    {
        Scenario scenario = new(5_000f);
        scenario.Police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase35", Vector3.Zero, 100f, 100f, 3_000f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.Update(FactionCombatDisengagementService.MaximumStalePursuitSeconds + 0.1f, scenario.Player);
        return scenario.Police.FactionCombatTarget == null &&
            scenario.Service.TryGetLastDisengagementReason(scenario.Police, out FactionCombatDisengagementReason reason) &&
            reason == FactionCombatDisengagementReason.StalePursuit;
    }

    private static bool MeaningfulEvidenceRefreshesPursuit()
    {
        Scenario scenario = new(8_500f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Rogue.SetFactionCombatTarget(scenario.Police);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.Update(9f, scenario.Player);
        bool accepted = scenario.Service.RecordNpcDamage(scenario.Rogue, scenario.Police, 5f);
        scenario.Service.Update(9f, scenario.Player);
        return accepted && scenario.Police.FactionCombatTarget == scenario.Rogue;
    }

    private static bool ZeroDamageDoesNotRefresh()
    {
        Scenario scenario = new(8_500f);
        ArmNpcPair(scenario);
        scenario.Service.Update(9f, scenario.Player);
        bool accepted = scenario.Service.RecordNpcDamage(scenario.Rogue, scenario.Police, 0f);
        scenario.Service.Update(2f, scenario.Player);
        return !accepted && scenario.Police.FactionCombatTarget == null;
    }

    private static bool EnvironmentalDamageDoesNotRefresh()
    {
        Scenario scenario = new(8_500f);
        ArmNpcPair(scenario);
        scenario.Service.Update(9f, scenario.Player);
        bool accepted = scenario.Service.RecordNpcDamage(null, scenario.Police, 5f);
        scenario.Service.Update(2f, scenario.Player);
        return !accepted && scenario.Police.FactionCombatTarget == null;
    }

    private static bool SelfDamageDoesNotRefresh()
    {
        Scenario scenario = new(8_500f);
        ArmNpcPair(scenario);
        scenario.Service.Update(9f, scenario.Player);
        bool accepted = scenario.Service.RecordNpcDamage(scenario.Police, scenario.Police, 5f);
        scenario.Service.Update(2f, scenario.Player);
        return !accepted && scenario.Police.FactionCombatTarget == null;
    }

    private static bool InvalidAttackerDoesNotRefresh()
    {
        Scenario scenario = new(8_500f);
        NpcShip neutral = CreateNpc("Phase 35 neutral", FactionManager.NeutralCivilians, new Vector3(400f, 0f, 0f));
        scenario.Ships.Add(neutral);
        ArmNpcPair(scenario);
        scenario.Service.Update(9f, scenario.Player);
        bool accepted = scenario.Service.RecordNpcDamage(neutral, scenario.Police, 5f);
        scenario.Service.Update(2f, scenario.Player);
        return !accepted && scenario.Police.FactionCombatTarget == null;
    }

    private static bool Phase33ResponderClearsDeadTarget() => ResponderClearsDeadTarget(FactionCombatTargetOrigin.DistressResponse);

    private static bool Phase33ResponderClearsEscapedTarget() => ResponderClearsEscapedTarget(FactionCombatTargetOrigin.DistressResponse);

    private static bool Phase33ResponderResumesTraffic() => ResponderResumesTraffic(FactionCombatTargetOrigin.DistressResponse);

    private static bool Phase34ResponderClearsDeadTarget() => ResponderClearsDeadTarget(FactionCombatTargetOrigin.EscalationResponse);

    private static bool Phase34ResponderClearsEscapedTarget() => ResponderClearsEscapedTarget(FactionCombatTargetOrigin.EscalationResponse);

    private static bool Phase34ResponderResumesTraffic() => ResponderResumesTraffic(FactionCombatTargetOrigin.EscalationResponse);

    private static bool ResponderClearsDeadTarget(FactionCombatTargetOrigin origin)
    {
        (Scenario scenario, NpcShip responder) = CreateResponderScenario(origin);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Rogue.Hull.TakeDamage(scenario.Rogue.Hull.MaxHull);
        scenario.Service.Update(0f, scenario.Player);
        return responder.FactionCombatTarget == null && responder.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool ResponderClearsEscapedTarget(FactionCombatTargetOrigin origin)
    {
        (Scenario scenario, NpcShip responder) = CreateResponderScenario(origin);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Rogue.Position = new Vector3(8_500f, 0f, 0f);
        scenario.Service.Update(11f, scenario.Player);
        return responder.FactionCombatTarget == null && responder.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool ResponderResumesTraffic(FactionCombatTargetOrigin origin)
    {
        (Scenario scenario, NpcShip responder) = CreateResponderScenario(origin);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Rogue.Position = new Vector3(8_500f, 0f, 0f);
        scenario.Service.Update(11f, scenario.Player);
        Vector3 before = responder.Position;
        responder.Update(Frame(0.5f), null, null, scenario.Reputation);
        return responder.FactionCombatTarget == null &&
            responder.EncounterState == TrafficEncounterState.Cruising &&
            responder.Position != before;
    }

    private static bool ClearedResponderAcquiresNewHostile()
    {
        IntegrationScene scene = CreateIntegrationScene(includeReplacement: true);
        scene.Traffic.Update(Frame(0f), scene.Player, scene.Reputation);
        scene.AttackerA.Position = new Vector3(1_008_500f, 0f, 0f);
        scene.AttackerB.Position = new Vector3(1_000_300f, 0f, 0f);
        scene.Traffic.Update(Frame(11f), scene.Player, scene.Reputation);
        return scene.Source.FactionCombatTarget == scene.AttackerB;
    }

    private static bool ExactStaleTargetIsSuppressed()
    {
        IntegrationScene scene = CreateIntegrationScene(includeReplacement: false);
        scene.Traffic.Update(Frame(0f), scene.Player, scene.Reputation);
        scene.AttackerA.Position = new Vector3(1_008_500f, 0f, 0f);
        scene.Traffic.Update(Frame(11f), scene.Player, scene.Reputation);
        bool suppressed = scene.Source.FactionCombatTarget == null &&
            scene.Traffic.CombatDisengagement.IsTargetAcquisitionSuppressed(scene.Source, scene.AttackerA);
        scene.Traffic.Update(Frame(0f), scene.Player, scene.Reputation);
        bool notImmediatelyReacquired = scene.Source.FactionCombatTarget == null;
        scene.Traffic.Update(Frame(FactionCombatDisengagementService.TargetReacquisitionSuppressionSeconds + 0.1f), scene.Player, scene.Reputation);
        return suppressed && notImmediatelyReacquired && scene.Source.FactionCombatTarget == scene.AttackerA;
    }

    private static bool UnrelatedHostileIsNotSuppressed()
    {
        IntegrationScene scene = CreateIntegrationScene(includeReplacement: true);
        scene.Traffic.Update(Frame(0f), scene.Player, scene.Reputation);
        scene.AttackerA.Position = new Vector3(1_008_500f, 0f, 0f);
        scene.AttackerB.Position = new Vector3(1_000_300f, 0f, 0f);
        scene.Traffic.Update(Frame(11f), scene.Player, scene.Reputation);
        return scene.Source.FactionCombatTarget == scene.AttackerB &&
            !scene.Traffic.CombatDisengagement.IsTargetAcquisitionSuppressed(scene.Source, scene.AttackerB);
    }

    private static bool DisengagementPreservesPlayerReputation()
    {
        Scenario scenario = new(11_000f);
        float before = scenario.Reputation.GetStanding(FactionManager.LibertyPolice);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.Update(11f, scenario.Player);
        return Nearly(before, scenario.Reputation.GetStanding(FactionManager.LibertyPolice));
    }

    private static bool DisengagementPreservesPhase30Consequences()
    {
        Scenario scenario = new(11_000f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        float beforePolice = scenario.Reputation.GetStanding(FactionManager.LibertyPolice);
        float beforeRogue = scenario.Reputation.GetStanding(FactionManager.LibertyRogues);
        scenario.Service.Update(11f, scenario.Player);
        return Nearly(beforePolice, scenario.Reputation.GetStanding(FactionManager.LibertyPolice)) &&
            Nearly(beforeRogue, scenario.Reputation.GetStanding(FactionManager.LibertyRogues));
    }

    private static bool DisengagementPreservesPhase31Ripple()
    {
        Scenario scenario = new(11_000f);
        FactionRelationshipKind before = FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.Update(11f, scenario.Player);
        return before == FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues);
    }

    private static bool TargetClearingPreservesProvenance()
    {
        (Scenario scenario, NpcShip responder) = CreateResponderScenario(FactionCombatTargetOrigin.DistressResponse);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Rogue.Position = new Vector3(8_500f, 0f, 0f);
        scenario.Service.Update(11f, scenario.Player);
        return responder.FactionCombatTarget == null && responder.IsDistressReinforcement &&
            responder.DistressReinforcementEncounterId == "phase35-encounter";
    }

    private static bool Phase33NonRecursionRemainsIntact()
    {
        List<NpcShip> ships = new();
        NpcShip police = CreateNpc("Phase 35 police", FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc("Phase 35 rogue", FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ships.Add(police);
        ships.Add(rogue);
        FactionDistressResponseService distress = new(
            ships,
            NewReputation(),
            (factionId, position, encounterId, count) =>
            {
                NpcShip responder = CreateNpc("Phase 35 responder", factionId, position + new Vector3(200f, 0f, 0f));
                responder.MarkDistressReinforcement(encounterId);
                ships.Add(responder);
                return new[] { responder };
            });
        FactionDistressResponseResult first = distress.ProcessNpcDamage(rogue, police, 5f);
        NpcShip spawned = ships.Last();
        FactionDistressResponseResult recursive = distress.ProcessNpcDamage(spawned, rogue, 5f);
        return first.WaveSpawned && !recursive.Accepted && spawned.IsDistressReinforcement;
    }

    private static bool Phase34NonRecursionRemainsIntact()
    {
        List<NpcShip> ships = new();
        NpcShip police = CreateNpc("Phase 35 police", FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc("Phase 35 rogue", FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ships.Add(police);
        ships.Add(rogue);
        ReputationManager reputation = NewReputation();
        FactionDistressResponseService distress = new(
            ships,
            reputation,
            (factionId, position, encounterId, count) =>
            {
                NpcShip responder = CreateNpc("Phase 35 ordinary", factionId, position + new Vector3(200f, 0f, 0f));
                responder.MarkDistressReinforcement(encounterId);
                ships.Add(responder);
                return new[] { responder };
            });
        FactionCombatEscalationService escalation = new(
            ships,
            distress,
            reputation,
            (factionId, position, encounterId, count) =>
            {
                NpcShip responder = CreateNpc("Phase 35 heavy", factionId, position + new Vector3(300f, 0f, 0f));
                responder.MarkEscalationReinforcement(encounterId);
                ships.Add(responder);
                return new[] { responder };
            });
        FactionDistressResponseResult first = escalation.ProcessNpcDamage(rogue, police, 5f);
        NpcShip ordinary = ships.Last();
        FactionDistressResponseResult recursive = escalation.ProcessNpcDamage(ordinary, rogue, 5f);
        return first.WaveSpawned && !recursive.Accepted && ordinary.IsDistressReinforcement;
    }

    private static bool ExpiredEncounterDoesNotForceCombat()
    {
        Scenario scenario = new(8_500f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        FactionDistressResponseService distress = new(
            scenario.Ships,
            scenario.Reputation,
            (_, _, _, _) => Array.Empty<NpcShip>());
        distress.ProcessNpcDamage(scenario.Rogue, scenario.Police, 5f);
        distress.Update(FactionDistressResponseService.EncounterExpirySeconds + 1f);
        scenario.Service.Update(11f, scenario.Player);
        return scenario.Police.FactionCombatTarget == null && distress.ActiveEncounterCount == 0;
    }

    private static bool ResetClearsTransientState()
    {
        Scenario scenario = new(8_500f);
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.Update(0f, scenario.Player);
        scenario.Service.RecordNpcDamage(scenario.Rogue, scenario.Police, 5f);
        scenario.Service.Update(11f, scenario.Player);
        scenario.Service.Reset();
        return scenario.Police.FactionCombatTarget == null &&
            scenario.Service.ActiveEngagementCount == 0 &&
            scenario.Service.SuppressedTargetCount == 0 &&
            Nearly(scenario.Service.SimulationTime, 0f);
    }

    private static bool SaveSchemaRemainsUnchanged()
    {
        string save = JsonSerializer.Serialize(new SaveGameData());
        return SaveGameData.CurrentSchemaVersion == 11 &&
            !save.Contains("Disengagement", StringComparison.OrdinalIgnoreCase) &&
            !save.Contains("CombatTarget", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NormalTargetingReacquiresLocalEnemy()
    {
        IntegrationScene scene = CreateIntegrationScene(includeReplacement: true);
        scene.Traffic.Update(Frame(0f), scene.Player, scene.Reputation);
        scene.AttackerA.Position = new Vector3(1_008_500f, 0f, 0f);
        scene.AttackerB.Position = new Vector3(1_000_300f, 0f, 0f);
        scene.Traffic.Update(Frame(11f), scene.Player, scene.Reputation);
        return scene.Source.FactionCombatTarget == scene.AttackerB &&
            NpcFactionCombatTargeting.IsValidHostileTarget(scene.Source, scene.AttackerB, scene.Source.TrafficActivationRange);
    }

    private static void ArmNpcPair(Scenario scenario)
    {
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Rogue.SetFactionCombatTarget(scenario.Police);
        scenario.Service.Update(0f, scenario.Player);
    }

    private static (Scenario Scenario, NpcShip Responder) CreateResponderScenario(FactionCombatTargetOrigin origin)
    {
        Scenario scenario = new();
        NpcShip responder = CreateNpc("Phase 35 responder", FactionManager.LibertyPolice, Vector3.Zero);
        responder.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase35", Vector3.Zero, 100f, 100f, 6_500f);
        if (origin == FactionCombatTargetOrigin.DistressResponse)
        {
            responder.MarkDistressReinforcement("phase35-encounter");
        }
        else
        {
            responder.MarkEscalationReinforcement("phase35-encounter");
        }

        scenario.Ships.Add(responder);
        responder.SetFactionCombatTarget(
            scenario.Rogue,
            targetOrigin: origin);
        return (scenario, responder);
    }

    private sealed class IntegrationScene
    {
        public TrafficManager Traffic { get; init; } = null!;
        public List<NpcShip> Ships { get; init; } = null!;
        public Ship Player { get; init; } = null!;
        public ReputationManager Reputation { get; init; } = null!;
        public NpcShip Source { get; init; } = null!;
        public NpcShip AttackerA { get; init; } = null!;
        public NpcShip AttackerB { get; init; } = null!;
    }

    private static IntegrationScene CreateIntegrationScene(bool includeReplacement)
    {
        ConfigurationManager config = new();
        RunSilenced(config.LoadAll);
        List<NpcShip> ships = new();
        TrafficManager traffic = RunSilenced(() =>
        {
            TrafficManager manager = new(config, ships, new List<SpaceObject>());
            manager.LoadZonesForSystem(1);
            return manager;
        });
        // Keep the configured zones alive for the manager update path while
        // isolating this deterministic combat scene from their initial ships.
        ships.Clear();

        Vector3 origin = new(1_000_000f, 0f, 0f);
        NpcShip source = CreateNpc("Phase 35 source", FactionManager.LibertyPolice, origin);
        NpcShip attackerA = CreateNpc("Phase 35 rogue A", FactionManager.LibertyRogues, origin + new Vector3(300f, 0f, 0f));
        NpcShip attackerB = CreateNpc("Phase 35 rogue B", FactionManager.LibertyRogues, origin + new Vector3(13_000f, 0f, 0f));
        source.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase35", origin, 100f, 100f, 12_000f);
        attackerA.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase35", origin, 100f, 100f, 12_000f);
        attackerB.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase35", origin, 100f, 100f, 12_000f);
        ships.Add(source);
        ships.Add(attackerA);
        if (includeReplacement)
            ships.Add(attackerB);
        source.SetFactionCombatTarget(attackerA);

        return new IntegrationScene
        {
            Traffic = traffic,
            Ships = ships,
            Player = new Ship(new Vector3(2_000_000f, 0f, 0f)),
            Reputation = NewReputation(),
            Source = source,
            AttackerA = attackerA,
            AttackerB = attackerB
        };
    }

    private static ReputationManager NewReputation()
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, 0f, "Phase 35 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0f, "Phase 35 smoke setup");
        return reputation;
    }

    private static NpcShip CreateNpc(string name, string factionId, Vector3 position) =>
        new(name, position, position, 1f, 0.5f, factionId);

    private static GameTime Frame(float seconds) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter previous = Console.Out;
        using StringWriter sink = new();
        Console.SetOut(sink);
        try
        {
            return action();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    private static bool RunSilenced(Func<bool> assertion) => RunSilenced<bool>(assertion);

    private static void RunSilenced(Action action)
    {
        TextWriter previous = Console.Out;
        using StringWriter sink = new();
        Console.SetOut(sink);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }
}
