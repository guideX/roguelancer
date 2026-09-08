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
/// Deterministic Phase 34 coverage for the second-stage local response.
/// The harness composes the real Phase 33 service with the new escalation
/// service, so every positive case proves the ordinary-response prerequisite.
/// </summary>
internal sealed class FactionCombatEscalationSmokeTest
{
    private sealed class Scenario
    {
        public List<NpcShip> Ships { get; } = new();
        public List<NpcShip> DistressSpawned { get; } = new();
        public List<NpcShip> EscalationSpawned { get; } = new();
        public ReputationManager Reputation { get; } = NewReputation();
        public Ship Player { get; } = new(new Vector3(300f, 0f, 0f));
        public FactionDistressResponseService Distress { get; }
        public FactionCombatEscalationService Escalation { get; }

        public Scenario()
        {
            Distress = new FactionDistressResponseService(
                Ships,
                Reputation,
                (factionId, battlePosition, encounterId, count) =>
                    CreateResponders(factionId, battlePosition, encounterId, count, isEscalation: false));
            Escalation = new FactionCombatEscalationService(
                Ships,
                Distress,
                Reputation,
                (factionId, battlePosition, encounterId, count) =>
                    CreateResponders(factionId, battlePosition, encounterId, count, isEscalation: true));
        }

        private IReadOnlyList<NpcShip> CreateResponders(
            string factionId,
            Vector3 battlePosition,
            string encounterId,
            int count,
            bool isEscalation)
        {
            List<NpcShip> created = new();
            for (int i = 0; i < count; i++)
            {
                NpcShip ship = CreateNpc(
                    $"{FactionManager.GetFactionDisplayName(factionId)} {(isEscalation ? "heavy" : "ordinary")} responder {i + 1}",
                    factionId,
                    battlePosition + new Vector3(2400f + i * 350f, 0f, 0f));
                Ships.Add(ship);
                created.Add(ship);
                if (isEscalation)
                    EscalationSpawned.Add(ship);
                else
                    DistressSpawned.Add(ship);
            }

            return created;
        }
    }

    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("ordinary damage alone does not escalate", OrdinaryDamageAloneDoesNotEscalate);
        Check("escalation requires an ordinary response", EscalationRequiresOrdinaryResponse);
        Check("continued serious combat qualifies", ContinuedSeriousCombatQualifies);
        Check("Police encounter spawns exactly two escalation ships", PoliceEncounterEscalates);
        Check("Rogue encounter spawns exactly two escalation ships", RogueEncounterEscalates);
        Check("only one escalation wave is allowed per encounter", OneEscalationPerEncounter);
        Check("escalation cap is independent and bounded", EscalationCapIsIndependent);
        Check("escalation cooldown blocks an immediate repeat", EscalationCooldownBlocksRepeat);
        Check("escalation cooldown is faction-specific", EscalationCooldownIsFactionSpecific);
        Check("local contexts remain isolated", LocalContextsRemainIsolated);
        Check("neutral and invalid factions do not escalate", NeutralAndInvalidFactionsDoNotEscalate);
        Check("zero damage does not contribute", ZeroDamageDoesNotContribute);
        Check("environmental damage does not contribute", EnvironmentalDamageDoesNotContribute);
        Check("self damage does not contribute", SelfDamageDoesNotContribute);
        Check("invalid attackers do not contribute", InvalidAttackersDoNotContribute);
        Check("dead victims do not create escalation", DeadVictimsDoNotCreateEscalation);
        Check("duplicate evidence is deduplicated", DuplicateEvidenceIsDeduplicated);
        Check("NPC-only escalation does not change reputation", NpcOnlyEscalationLeavesReputationUnchanged);
        Check("player aggression can cause escalation", PlayerAggressionCanEscalate);
        Check("player destruction preserves Phase 30 consequences", PlayerDestructionPreservesPhase30Consequences);
        Check("player kill preserves Phase 31 ripple", PlayerKillPreservesPhase31Ripple);
        Check("Phase 28-style enforcement combat uses the normal path", EnforcementCombatUsesNormalPath);
        Check("Phase 33 responders cannot recurse", Phase33RespondersCannotRecurse);
        Check("Phase 34 responders cannot recurse", Phase34RespondersCannotRecurse);
        Check("live escalation cap is respected", LiveEscalationCapIsRespected);
        Check("encounter TTL clears stale evidence", EncounterTtlClearsEvidence);
        Check("attacker loss clears escalation targets", AttackerLossClearsTargets);
        Check("responders resume ordinary behavior", RespondersResumeOrdinaryBehavior);
        Check("temporary hostility expiry clears player targets", TemporaryHostilityExpiryClearsTargets);
        Check("permanent hostility still permits targeting", PermanentHostilityStillWorks);
        Check("reset clears Phase 34 state", ResetClearsState);
        Check("save schema remains unchanged", SaveSchemaRemainsUnchanged);
        Check("Phase 33 ordinary response remains unchanged", Phase33ResponseRemainsUnchanged);
        Check("traffic lifecycle and spawn placement are safe", TrafficLifecycleAndPlacementAreSafe);

        Console.WriteLine($"[FACTION COMBAT ESCALATION SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION COMBAT ESCALATION SMOKE] PASS {label}");
            }
            else
            {
                _failed++;
                Console.WriteLine($"[FACTION COMBAT ESCALATION SMOKE] FAIL {label}: assertion returned false");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"[FACTION COMBAT ESCALATION SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static bool OrdinaryDamageAloneDoesNotEscalate()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        scenario.Escalation.Update(9.9f);
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        return scenario.EscalationSpawned.Count == 0;
    }

    private static bool EscalationRequiresOrdinaryResponse()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        scenario.Escalation.NotifyNpcDestroyed(victim);
        scenario.Escalation.Update(FactionCombatEscalationService.MinimumPostResponseDurationSeconds + 1f);
        return scenario.EscalationSpawned.Count == 0 && scenario.Escalation.ActiveEncounterCount == 0;
    }

    private static bool ContinuedSeriousCombatQualifies()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, attacker, victim);
        return scenario.EscalationSpawned.Count == FactionCombatEscalationService.EscalationWaveSize;
    }

    private static bool PoliceEncounterEscalates()
    {
        Scenario scenario = CreateScenario();
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, rogue, police);
        return scenario.EscalationSpawned.Count == 2 &&
            scenario.EscalationSpawned.All(ship => ship.FactionId == FactionManager.LibertyPolice) &&
            scenario.EscalationSpawned.All(ship => ship.IsEscalationReinforcement && ship.FactionCombatTarget == rogue);
    }

    private static bool RogueEncounterEscalates()
    {
        Scenario scenario = CreateScenario();
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, Vector3.Zero);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, police, rogue);
        return scenario.EscalationSpawned.Count == 2 &&
            scenario.EscalationSpawned.All(ship => ship.FactionId == FactionManager.LibertyRogues) &&
            scenario.EscalationSpawned.All(ship => ship.FactionCombatTarget == police);
    }

    private static bool OneEscalationPerEncounter()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, attacker, victim);
        int firstCount = scenario.EscalationSpawned.Count;
        for (int i = 0; i < 6; i++)
        {
            scenario.Escalation.Update(1f);
            scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f + i * 0.1f);
        }

        return firstCount == 2 && scenario.EscalationSpawned.Count == firstCount;
    }

    private static bool EscalationCapIsIndependent()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, attacker, victim);
        return scenario.DistressSpawned.Count == FactionDistressResponseService.ReinforcementWaveSize &&
            scenario.EscalationSpawned.Count == FactionCombatEscalationService.EscalationWaveSize &&
            scenario.Escalation.CountActiveEscalationReinforcements(FactionManager.LibertyPolice, Vector3.Zero) == 2;
    }

    private static bool EscalationCooldownBlocksRepeat()
    {
        Scenario scenario = CreateScenario();
        NpcShip firstVictim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip firstAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, firstAttacker, firstVictim);

        NpcShip secondVictim = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(100f, 0f, 0f));
        NpcShip secondAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(400f, 0f, 0f));
        scenario.Escalation.Update(1f);
        ApplySeriousNpcDamage(scenario, secondAttacker, secondVictim);
        return scenario.EscalationSpawned.Count == 2 &&
            scenario.Escalation.IsEscalationCooldownActive(FactionManager.LibertyPolice, Vector3.Zero);
    }

    private static bool EscalationCooldownIsFactionSpecific()
    {
        Scenario scenario = CreateScenario();
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogueAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, rogueAttacker, police);

        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, Vector3.Zero);
        NpcShip policeAttacker = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
        scenario.Escalation.Update(1f);
        ApplySeriousNpcDamage(scenario, policeAttacker, rogue);
        return scenario.EscalationSpawned.Count == 4 &&
            scenario.EscalationSpawned.Skip(2).All(ship => ship.FactionId == FactionManager.LibertyRogues) &&
            scenario.Escalation.IsEscalationCooldownActive(FactionManager.LibertyPolice, Vector3.Zero);
    }

    private static bool LocalContextsRemainIsolated()
    {
        Scenario scenario = CreateScenario();
        NpcShip firstVictim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip firstAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, firstAttacker, firstVictim);

        Vector3 distantPosition = new(FactionCombatEscalationService.LocalContextRadius * 2f, 0f, 0f);
        NpcShip secondVictim = AddNpc(scenario, FactionManager.LibertyPolice, distantPosition);
        NpcShip secondAttacker = AddNpc(scenario, FactionManager.LibertyRogues, distantPosition + new Vector3(300f, 0f, 0f));
        scenario.Escalation.Update(1f);
        ApplySeriousNpcDamage(scenario, secondAttacker, secondVictim);
        return scenario.EscalationSpawned.Count == 4 &&
            scenario.EscalationSpawned.Skip(2).All(ship => Vector3.Distance(ship.Position, distantPosition) >= 1800f);
    }

    private static bool NeutralAndInvalidFactionsDoNotEscalate()
    {
        Scenario neutralScenario = CreateScenario();
        NpcShip civilian = AddNpc(neutralScenario, FactionManager.NeutralCivilians, Vector3.Zero);
        NpcShip rogue = AddNpc(neutralScenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        neutralScenario.Escalation.ProcessNpcDamage(rogue, civilian, 100f);

        Scenario invalidScenario = CreateScenario();
        NpcShip police = AddNpc(invalidScenario, FactionManager.LibertyPolice, Vector3.Zero);
        invalidScenario.Escalation.NotifyNpcDestroyed(police);
        invalidScenario.Escalation.Update(20f);
        return neutralScenario.EscalationSpawned.Count == 0 && invalidScenario.EscalationSpawned.Count == 0;
    }

    private static bool ZeroDamageDoesNotContribute()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        bool marked = scenario.Escalation.ProcessNpcDamage(attacker, victim, 0f).Accepted;
        scenario.Escalation.Update(30f);
        return !marked && scenario.EscalationSpawned.Count == 0;
    }

    private static bool EnvironmentalDamageDoesNotContribute()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        victim.Hull.TakeDamage(5f);
        scenario.Escalation.Update(30f);
        return scenario.EscalationSpawned.Count == 0 && scenario.DistressSpawned.Count == 0;
    }

    private static bool SelfDamageDoesNotContribute()
    {
        Scenario scenario = CreateScenario();
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        FactionDistressResponseResult result = scenario.Escalation.ProcessNpcDamage(police, police, 25f);
        return !result.Accepted && scenario.EscalationSpawned.Count == 0;
    }

    private static bool InvalidAttackersDoNotContribute()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        FactionDistressResponseResult result = scenario.Escalation.ProcessNpcDamage(null, victim, 25f);
        return !result.Accepted && scenario.EscalationSpawned.Count == 0;
    }

    private static bool DeadVictimsDoNotCreateEscalation()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        victim.Hull.TakeDamage(victim.Hull.MaxHull);
        FactionDistressResponseResult result = scenario.Escalation.ProcessNpcDamage(attacker, victim, 25f);
        return !result.Accepted && scenario.EscalationSpawned.Count == 0;
    }

    private static bool DuplicateEvidenceIsDeduplicated()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        scenario.Escalation.Update(FactionCombatEscalationService.MinimumPostResponseDurationSeconds);
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        return scenario.EscalationSpawned.Count == 0;
    }

    private static bool NpcOnlyEscalationLeavesReputationUnchanged()
    {
        Scenario scenario = CreateScenario();
        float policeBefore = scenario.Reputation.GetStanding(FactionManager.LibertyPolice);
        float rogueBefore = scenario.Reputation.GetStanding(FactionManager.LibertyRogues);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, rogue, police);
        return Nearly(policeBefore, scenario.Reputation.GetStanding(FactionManager.LibertyPolice)) &&
            Nearly(rogueBefore, scenario.Reputation.GetStanding(FactionManager.LibertyRogues));
    }

    private static bool PlayerAggressionCanEscalate()
    {
        Scenario scenario = CreateScenario();
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        for (int i = 0; i < 5; i++)
        {
            police.MarkDamagedByPlayer(5f + i * 0.1f);
            scenario.Reputation.CombatConsequences.RecordPlayerDamage(police);
            scenario.Escalation.ProcessPlayerDamage(police, scenario.Player);
            if (i == 0)
                scenario.Escalation.Update(FactionCombatEscalationService.MinimumPostResponseDurationSeconds);
            else
                scenario.Escalation.Update(0.1f);
        }

        return scenario.EscalationSpawned.Count == 2 &&
            scenario.EscalationSpawned.All(ship => ship.HasPlayerTarget && ship.HasValidPlayerTarget(scenario.Reputation));
    }

    private static bool PlayerDestructionPreservesPhase30Consequences()
    {
        ReputationManager reputation = NewReputation();
        NpcShip police = CreateNpc("police", FactionManager.LibertyPolice, Vector3.Zero);
        police.MarkDamagedByPlayer(5f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        police.Hull.TakeDamage(police.Hull.MaxHull);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.15f);
    }

    private static bool PlayerKillPreservesPhase31Ripple()
    {
        ReputationManager reputation = NewReputation();
        NpcShip rogue = CreateNpc("rogue", FactionManager.LibertyRogues, Vector3.Zero);
        rogue.MarkDamagedByPlayer(5f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionCombatConsequenceService.EnemyKillReputationReward);
    }

    private static bool EnforcementCombatUsesNormalPath()
    {
        Scenario scenario = CreateScenario();
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        bool hostile = NpcFactionCombatTargeting.IsHostileFactionPair(rogue.FactionId, police.FactionId);
        ApplySeriousNpcDamage(scenario, rogue, police);
        return hostile && scenario.EscalationSpawned.Count == 2;
    }

    private static bool Phase33RespondersCannotRecurse()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        NpcShip distressResponder = scenario.DistressSpawned[0];
        FactionDistressResponseResult result = scenario.Escalation.ProcessNpcDamage(attacker, distressResponder, 5f);
        return !result.Accepted && scenario.Escalation.ActiveEncounterCount == 1 && scenario.EscalationSpawned.Count == 0;
    }

    private static bool Phase34RespondersCannotRecurse()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, attacker, victim);
        int encounters = scenario.Escalation.ActiveEncounterCount;
        NpcShip escalationResponder = scenario.EscalationSpawned[0];
        FactionDistressResponseResult result = scenario.Escalation.ProcessNpcDamage(escalationResponder, attacker, 5f);
        return !result.Accepted && scenario.Escalation.ActiveEncounterCount == encounters && scenario.EscalationSpawned.Count == 2;
    }

    private static bool LiveEscalationCapIsRespected()
    {
        Scenario scenario = CreateScenario();
        NpcShip firstVictim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip firstAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, firstAttacker, firstVictim);
        scenario.Escalation.Update(FactionCombatEscalationService.EscalationCooldownSeconds);

        NpcShip secondVictim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip secondAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, secondAttacker, secondVictim);
        return scenario.EscalationSpawned.Count == 2 &&
            scenario.Escalation.CountActiveEscalationReinforcements(FactionManager.LibertyPolice, Vector3.Zero) == 2;
    }

    private static bool EncounterTtlClearsEvidence()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        scenario.Escalation.Update(FactionCombatEscalationService.EncounterExpirySeconds + 1f);
        return scenario.Escalation.ActiveEncounterCount == 0 && scenario.Distress.ActiveEncounterCount == 0;
    }

    private static bool AttackerLossClearsTargets()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, attacker, victim);
        attacker.Hull.TakeDamage(attacker.Hull.MaxHull);
        foreach (NpcShip responder in scenario.EscalationSpawned)
            responder.Update(Frame(), null, scenario.Player, scenario.Reputation);
        return scenario.EscalationSpawned.All(ship => ship.FactionCombatTarget == null && ship.EncounterState == TrafficEncounterState.Cruising);
    }

    private static bool RespondersResumeOrdinaryBehavior()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        ApplySeriousNpcDamage(scenario, attacker, victim);
        attacker.Hull.TakeDamage(attacker.Hull.MaxHull);
        NpcShip responder = scenario.EscalationSpawned[0];
        Vector3 before = responder.Position;
        responder.Update(Frame(), null, scenario.Player, scenario.Reputation);
        return responder.EncounterState == TrafficEncounterState.Cruising && responder.Position != before;
    }

    private static bool TemporaryHostilityExpiryClearsTargets()
    {
        Scenario scenario = CreateScenario();
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        for (int i = 0; i < 4; i++)
        {
            police.MarkDamagedByPlayer(5f + i * 0.1f);
            scenario.Reputation.CombatConsequences.RecordPlayerDamage(police);
            scenario.Escalation.ProcessPlayerDamage(police, scenario.Player);
            scenario.Escalation.Update(i == 0 ? 10f : 0.1f);
        }

        bool targeted = scenario.EscalationSpawned.All(ship => ship.HasPlayerTarget);
        scenario.Reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        foreach (NpcShip responder in scenario.EscalationSpawned)
            responder.Update(Frame(), null, scenario.Player, scenario.Reputation);
        return targeted && scenario.EscalationSpawned.All(ship => !ship.HasPlayerTarget && ship.EncounterState == TrafficEncounterState.Cruising);
    }

    private static bool PermanentHostilityStillWorks()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "Phase 34 smoke");
        NpcShip police = CreateNpc("police", FactionManager.LibertyPolice, Vector3.Zero);
        police.Update(Frame(), null, new Ship(new Vector3(100f, 0f, 0f)), reputation);
        return police.HasPlayerTarget && police.HasValidPlayerTarget(reputation);
    }

    private static bool ResetClearsState()
    {
        Scenario scenario = CreateScenario();
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        scenario.Escalation.Reset();
        return scenario.Escalation.ActiveEncounterCount == 0 &&
            scenario.Escalation.CooldownRecordCount == 0 &&
            scenario.Distress.ActiveEncounterCount == 0 &&
            scenario.DistressSpawned.All(ship => !ship.IsFactionTransientReinforcement && ship.FactionCombatTarget == null);
    }

    private static bool SaveSchemaRemainsUnchanged()
    {
        string save = JsonSerializer.Serialize(new SaveGameData());
        return new SaveGameData().SchemaVersion == SaveGameData.CurrentSchemaVersion &&
            !save.Contains("Escalation", StringComparison.OrdinalIgnoreCase) &&
            !save.Contains("Reinforcement", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Phase33ResponseRemainsUnchanged()
    {
        Scenario scenario = CreateScenario();
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Distress.ProcessNpcDamage(rogue, police, 5f);
        return result.WaveSpawned && result.SpawnedShipCount == FactionDistressResponseService.ReinforcementWaveSize &&
            scenario.DistressSpawned.All(ship => ship.IsDistressReinforcement) && scenario.EscalationSpawned.Count == 0;
    }

    private static bool TrafficLifecycleAndPlacementAreSafe()
    {
        ConfigurationManager config = new();
        RunSilenced(config.LoadAll);
        List<NpcShip> ships = new();
        List<SpaceObject> objects = new();
        TrafficManager traffic = RunSilenced(() => new TrafficManager(config, ships, objects));
        RunSilenced(() => traffic.LoadZonesForSystem(1));

        NpcShip police = AddNpc(ships, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(ships, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        traffic.NotifyNpcDamage(rogue, police, 5f);
        traffic.CombatEscalation.Update(10f);
        for (int i = 0; i < 4; i++)
        {
            traffic.NotifyNpcDamage(rogue, police, 5f + i * 0.1f);
            traffic.CombatEscalation.Update(0.1f);
        }

        List<NpcShip> escalation = ships.Where(ship => ship.IsEscalationReinforcement).ToList();
        return escalation.Count == 2 &&
            escalation.All(ship => Vector3.Distance(ship.Position, police.Position) >= 1800f) &&
            escalation.All(ship => !ships.Any(other => other != ship && !other.IsDestroyed &&
                Vector3.Distance(other.Position, ship.Position) < 500f));
    }

    private static void ApplySeriousNpcDamage(
        Scenario scenario,
        NpcShip attacker,
        NpcShip victim)
    {
        scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f);
        scenario.Escalation.Update(FactionCombatEscalationService.MinimumPostResponseDurationSeconds);

        for (int i = 0; i < 4; i++)
        {
            scenario.Escalation.ProcessNpcDamage(attacker, victim, 5f + i * 0.1f);
            scenario.Escalation.Update(0.1f);
        }
    }

    private static Scenario CreateScenario() => new();

    private static NpcShip AddNpc(Scenario scenario, string factionId, Vector3 position) =>
        AddNpc(scenario.Ships, factionId, position);

    private static NpcShip AddNpc(List<NpcShip> ships, string factionId, Vector3 position)
    {
        NpcShip ship = CreateNpc("smoke npc", factionId, position);
        ships.Add(ship);
        return ship;
    }

    private static NpcShip CreateNpc(string name, string factionId, Vector3 position) =>
        new(name, position, position, 1f, 0f, factionId);

    private static ReputationManager NewReputation()
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, 0f, "Phase 34 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0f, "Phase 34 smoke setup");
        return reputation;
    }

    private static GameTime Frame(float seconds = 0.1f) =>
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
