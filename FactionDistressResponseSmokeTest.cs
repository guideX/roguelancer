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
/// Deterministic Phase 33 coverage for local assistance, bounded response
/// policy, provenance, targeting integration, lifecycle, and attribution.
/// </summary>
internal sealed class FactionDistressResponseSmokeTest
{
    private sealed class Scenario
    {
        public List<NpcShip> Ships { get; } = new();
        public List<NpcShip> Spawned { get; } = new();
        public ReputationManager Reputation { get; } = NewReputation();
        public Ship Player { get; } = new(new Vector3(300f, 0f, 0f));
        public FactionDistressResponseService Service { get; }

        public Scenario()
        {
            Service = new FactionDistressResponseService(
                Ships,
                Reputation,
                (factionId, battlePosition, encounterId, count) =>
                {
                    List<NpcShip> created = new();
                    for (int i = 0; i < count; i++)
                    {
                        NpcShip ship = CreateNpc(
                            $"{FactionManager.GetFactionDisplayName(factionId)} reinforcement {i + 1}",
                            factionId,
                            battlePosition + new Vector3(2400f + i * 350f, 0f, 0f));
                        Ships.Add(ship);
                        Spawned.Add(ship);
                        created.Add(ship);
                    }

                    return created;
                });
        }
    }

    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("valid player damage generates Police distress", ValidPlayerDamageGeneratesResponse);
        Check("zero damage does not generate distress", ZeroDamageDoesNotRespond);
        Check("environmental damage does not generate distress", EnvironmentalDamageDoesNotRespond);
        Check("invalid attribution does not generate distress", InvalidAttributionDoesNotRespond);
        Check("Rogue NPC damage generates Police distress", RogueDamageGeneratesPoliceResponse);
        Check("Police NPC damage generates Rogue distress", PoliceDamageGeneratesRogueResponse);
        Check("same-faction damage is rejected", SameFactionDamageIsRejected);
        Check("nearby same-faction NPC assists", NearbySameFactionNpcAssists);
        Check("unrelated faction does not assist", UnrelatedFactionDoesNotAssist);
        Check("response uses the distressed faction", ResponseUsesCorrectFaction);
        Check("wave size is exactly two", WaveSizeIsBounded);
        Check("active reinforcement cap is two", ActiveReinforcementCapIsEnforced);
        Check("one encounter receives at most one wave", EncounterReceivesOneWave);
        Check("repeated projectile damage is de-duplicated", RepeatedDamageIsDeduplicated);
        Check("cooldown blocks an immediate independent response", CooldownBlocksImmediateResponse);
        Check("cooldown advances on simulation time only", CooldownUsesSimulationTime);
        Check("independent encounter responds after cooldown", IndependentEncounterRespondsAfterCooldown);
        Check("reinforcement provenance is transient and explicit", ReinforcementProvenanceIsMarked);
        Check("reinforcement damage cannot recurse", ReinforcementDamageDoesNotRecurse);
        Check("reinforcements use faction targeting", ReinforcementUsesFactionTargeting);
        Check("Police reinforcement targets hostile player", PoliceReinforcementTargetsHostilePlayer);
        Check("Police reinforcement targets Rogue NPC", PoliceReinforcementTargetsRogueNpc);
        Check("Rogue reinforcement targets Police NPC", RogueReinforcementTargetsPoliceNpc);
        Check("neutral target remains invalid", NeutralTargetRemainsInvalid);
        Check("dead original attacker clears stale target", DeadAttackerClearsStaleTarget);
        Check("reinforcement cruises without a target", ReinforcementCruisesWithoutTarget);
        Check("temporary hostility expiry clears player targeting", TemporaryHostilityExpiryClearsTarget);
        Check("permanent hostility still permits player targeting", PermanentHostilityPermitsTarget);
        Check("NPC-only response leaves player reputation unchanged", NpcOnlyResponseLeavesReputationUnchanged);
        Check("player joining preserves Phase 30 consequences", PlayerJoiningPreservesConsequences);
        Check("player kill preserves Phase 31 ripple", PlayerKillPreservesRipple);
        Check("Police and Rogue responses remain isolated", PoliceRogueResponsesAreIsolated);
        Check("traffic spawn placement is bounded and non-intersecting", TrafficSpawnPlacementIsSafe);
        Check("destroyed reinforcement frees cap accounting", DestroyedReinforcementFreesCap);
        Check("despawned reinforcement frees cap accounting", DespawnedReinforcementFreesCap);
        Check("expired encounter and cooldown state is cleaned", TransientStateIsCleaned);
        Check("save data contains no distress state", SaveDataContainsNoDistressState);
        Check("reset clears transient response records", ResetClearsTransientState);
        Check("ordinary NPC traffic still cruises", OrdinaryTrafficStillCruises);
        Check("Phase 32 Police/Rogue combat remains intact", Phase32CombatRemainsIntact);
        Check("simultaneous local combat stays bounded", SimultaneousLocalCombatStaysBounded);

        Console.WriteLine($"[FACTION DISTRESS RESPONSE SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION DISTRESS RESPONSE SMOKE] PASS {label}");
            }
            else
            {
                _failed++;
                Console.WriteLine($"[FACTION DISTRESS RESPONSE SMOKE] FAIL {label}: assertion returned false");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"[FACTION DISTRESS RESPONSE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static bool ValidPlayerDamageGeneratesResponse()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        police.MarkDamagedByPlayer(5f);
        scenario.Reputation.CombatConsequences.RecordPlayerDamage(police);
        FactionDistressResponseResult result = scenario.Service.ProcessPlayerDamage(police, scenario.Player);
        return result.Accepted && result.WaveSpawned && result.SpawnedShipCount == 2 &&
            scenario.Spawned.All(ship => ship.FactionId == FactionManager.LibertyPolice);
    }

    private static bool ZeroDamageDoesNotRespond()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        bool marked = police.MarkDamagedByPlayer(0f);
        FactionDistressResponseResult result = scenario.Service.ProcessPlayerDamage(police, scenario.Player);
        return !marked && !result.Accepted && scenario.Spawned.Count == 0;
    }

    private static bool EnvironmentalDamageDoesNotRespond()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        police.Hull.TakeDamage(5f);
        FactionDistressResponseResult result = scenario.Service.ProcessPlayerDamage(police, scenario.Player);
        return !result.Accepted && scenario.Spawned.Count == 0;
    }

    private static bool InvalidAttributionDoesNotRespond()
    {
        Scenario scenario = CreateScenario(FactionManager.NeutralCivilians);
        NpcShip civilian = AddNpc(scenario, FactionManager.NeutralCivilians, Vector3.Zero);
        civilian.MarkDamagedByPlayer(5f);
        FactionDistressResponseResult result = scenario.Service.ProcessPlayerDamage(civilian, scenario.Player);
        return !result.Accepted && scenario.Spawned.Count == 0;
    }

    private static bool RogueDamageGeneratesPoliceResponse()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        return result.Accepted && result.WaveSpawned && scenario.Spawned.All(ship => ship.FactionId == FactionManager.LibertyPolice);
    }

    private static bool PoliceDamageGeneratesRogueResponse()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyRogues);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, Vector3.Zero);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(police, rogue, 5f);
        return result.Accepted && result.WaveSpawned && scenario.Spawned.All(ship => ship.FactionId == FactionManager.LibertyRogues);
    }

    private static bool SameFactionDamageIsRejected()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip first = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip second = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(first, second, 5f);
        return !result.Accepted && scenario.Spawned.Count == 0;
    }

    private static bool NearbySameFactionNpcAssists()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip helper = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(500f, 0f, 0f));
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        return result.AssistedShipCount == 1 && helper.FactionCombatTarget == rogue;
    }

    private static bool UnrelatedFactionDoesNotAssist()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        NpcShip civilian = AddNpc(scenario, FactionManager.NeutralCivilians, new Vector3(400f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        return civilian.FactionCombatTarget == null && civilian.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool ResponseUsesCorrectFaction()
    {
        Scenario policeScenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(policeScenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(policeScenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        policeScenario.Service.ProcessNpcDamage(rogue, police, 5f);
        bool policeCorrect = policeScenario.Spawned.Count == 2 && policeScenario.Spawned.All(ship => ship.FactionId == FactionManager.LibertyPolice);

        Scenario rogueScenario = CreateScenario(FactionManager.LibertyRogues);
        NpcShip rogueVictim = AddNpc(rogueScenario, FactionManager.LibertyRogues, Vector3.Zero);
        NpcShip policeAttacker = AddNpc(rogueScenario, FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
        rogueScenario.Service.ProcessNpcDamage(policeAttacker, rogueVictim, 5f);
        return policeCorrect && rogueScenario.Spawned.Count == 2 &&
            rogueScenario.Spawned.All(ship => ship.FactionId == FactionManager.LibertyRogues);
    }

    private static bool WaveSizeIsBounded()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        return FactionDistressResponseService.ReinforcementWaveSize == 2 && result.SpawnedShipCount == 2;
    }

    private static bool ActiveReinforcementCapIsEnforced()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip firstVictim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip firstAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(firstAttacker, firstVictim, 5f);
        scenario.Service.Update(FactionDistressResponseService.ReinforcementCooldownSeconds);

        NpcShip secondVictim = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(100f, 0f, 0f));
        NpcShip secondAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(400f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(secondAttacker, secondVictim, 5f);
        return scenario.Service.CountActiveReinforcements(FactionManager.LibertyPolice, Vector3.Zero) == 2 &&
            result.SpawnedShipCount == 0 && FactionDistressResponseService.ActiveReinforcementCap == 2;
    }

    private static bool EncounterReceivesOneWave()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult first = scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        FactionDistressResponseResult second = scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        return first.WaveSpawned && !second.WaveSpawned && second.Reason == "encounter already answered" && scenario.Spawned.Count == 2;
    }

    private static bool RepeatedDamageIsDeduplicated() => EncounterReceivesOneWave();

    private static bool CooldownBlocksImmediateResponse()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip firstVictim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip firstAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(firstAttacker, firstVictim, 5f);
        NpcShip secondVictim = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(100f, 0f, 0f));
        NpcShip secondAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(400f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(secondAttacker, secondVictim, 5f);
        return result.CooldownBlocked && result.SpawnedShipCount == 0;
    }

    private static bool CooldownUsesSimulationTime()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        scenario.Service.Update(59.9f);
        float before = scenario.Service.SimulationTime;
        scenario.Service.Update(0.1f);
        return Math.Abs(before - 59.9f) < 0.0001f &&
            Math.Abs(scenario.Service.SimulationTime - FactionDistressResponseService.ReinforcementCooldownSeconds) < 0.0001f;
    }

    private static bool IndependentEncounterRespondsAfterCooldown()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        foreach (NpcShip ship in scenario.Spawned)
            ship.Hull.TakeDamage(ship.Hull.MaxHull);
        scenario.Service.Update(FactionDistressResponseService.ReinforcementCooldownSeconds);

        NpcShip secondVictim = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(100f, 0f, 0f));
        NpcShip secondAttacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(400f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(secondAttacker, secondVictim, 5f);
        return result.WaveSpawned && result.SpawnedShipCount == 2;
    }

    private static bool ReinforcementProvenanceIsMarked()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        return result.WaveSpawned && scenario.Spawned.Count == 2 &&
            scenario.Spawned.All(ship => ship.IsDistressReinforcement && ship.DistressReinforcementEncounterId == result.EncounterId);
    }

    private static bool ReinforcementDamageDoesNotRecurse()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        NpcShip reinforcement = scenario.Spawned[0];
        int encounterCount = scenario.Service.ActiveEncounterCount;
        FactionDistressResponseResult result = scenario.Service.ProcessNpcDamage(attacker, reinforcement, 5f);
        return !result.Accepted && scenario.Service.ActiveEncounterCount == encounterCount && scenario.Spawned.Count == 2;
    }

    private static bool ReinforcementUsesFactionTargeting()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        return scenario.Spawned[0].SetFactionCombatTarget(attacker) &&
            scenario.Spawned[0].FactionCombatTarget == attacker;
    }

    private static bool PoliceReinforcementTargetsHostilePlayer()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        scenario.Reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "Phase 33 smoke");
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        NpcShip reinforcement = scenario.Spawned[0];
        Ship player = new(reinforcement.Position + new Vector3(100f, 0f, 0f));
        reinforcement.Update(Frame(), null, player, scenario.Reputation);
        return reinforcement.HasValidPlayerTarget(scenario.Reputation) && reinforcement.HasPlayerTarget;
    }

    private static bool PoliceReinforcementTargetsRogueNpc()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        return scenario.Spawned[0].SetFactionCombatTarget(attacker) &&
            NpcFactionCombatTargeting.IsValidHostileTarget(scenario.Spawned[0], attacker);
    }

    private static bool RogueReinforcementTargetsPoliceNpc()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyRogues);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyRogues, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        return scenario.Spawned[0].SetFactionCombatTarget(attacker) &&
            NpcFactionCombatTargeting.IsValidHostileTarget(scenario.Spawned[0], attacker);
    }

    private static bool NeutralTargetRemainsInvalid()
    {
        NpcShip police = CreateNpc("police", FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip civilian = CreateNpc("civilian", FactionManager.NeutralCivilians, new Vector3(300f, 0f, 0f));
        return !police.SetFactionCombatTarget(civilian) &&
            !NpcFactionCombatTargeting.IsHostileFactionPair(FactionManager.NeutralCivilians, FactionManager.LibertyPolice);
    }

    private static bool DeadAttackerClearsStaleTarget()
    {
        NpcShip police = CreateNpc("police", FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc("rogue", FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        police.SetFactionCombatTarget(rogue);
        rogue.Hull.TakeDamage(rogue.Hull.MaxHull);
        police.Update(Frame(), null);
        return police.FactionCombatTarget == null && police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool ReinforcementCruisesWithoutTarget()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip victim = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip attacker = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(attacker, victim, 5f);
        NpcShip reinforcement = scenario.Spawned[0];
        reinforcement.Update(Frame(), null, new Ship(new Vector3(100_000f, 0f, 0f)));
        return reinforcement.EncounterState == TrafficEncounterState.Cruising && reinforcement.FactionCombatTarget == null;
    }

    private static bool TemporaryHostilityExpiryClearsTarget()
    {
        ReputationManager reputation = NewReputation();
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice, "Phase 33 smoke", 60f);
        NpcShip police = CreateNpc("police", FactionManager.LibertyPolice, Vector3.Zero);
        Ship player = new(new Vector3(100f, 0f, 0f));
        police.Update(Frame(), null, player, reputation);
        bool targeted = police.HasPlayerTarget;
        reputation.TemporaryHostility.Update(60f);
        police.Update(Frame(), null, player, reputation);
        return targeted && !police.HasPlayerTarget && police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool PermanentHostilityPermitsTarget()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.60f, "Phase 33 smoke");
        NpcShip police = CreateNpc("police", FactionManager.LibertyPolice, Vector3.Zero);
        police.Update(Frame(), null, new Ship(new Vector3(100f, 0f, 0f)), reputation);
        return police.HasValidPlayerTarget(reputation);
    }

    private static bool NpcOnlyResponseLeavesReputationUnchanged()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        float policeBefore = scenario.Reputation.GetStanding(FactionManager.LibertyPolice);
        float rogueBefore = scenario.Reputation.GetStanding(FactionManager.LibertyRogues);
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        police.Hull.TakeDamage(police.Hull.MaxHull);
        scenario.Reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(policeBefore, scenario.Reputation.GetStanding(FactionManager.LibertyPolice)) &&
            Nearly(rogueBefore, scenario.Reputation.GetStanding(FactionManager.LibertyRogues));
    }

    private static bool PlayerJoiningPreservesConsequences()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        police.MarkDamagedByPlayer(5f);
        scenario.Reputation.CombatConsequences.RecordPlayerDamage(police);
        police.Hull.TakeDamage(police.Hull.MaxHull);
        scenario.Reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(
            scenario.Reputation.GetStanding(FactionManager.LibertyPolice),
            FactionCombatConsequenceService.InitialAggressionReputationPenalty + FactionCombatConsequenceService.NeutralNpcDestructionReputationPenalty);
    }

    private static bool PlayerKillPreservesRipple()
    {
        ReputationManager reputation = NewReputation();
        NpcShip rogue = CreateNpc("rogue", FactionManager.LibertyRogues, Vector3.Zero);
        rogue.MarkDamagedByPlayer(5f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionCombatConsequenceService.EnemyKillReputationReward);
    }

    private static bool PoliceRogueResponsesAreIsolated()
    {
        Scenario policeScenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(policeScenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(policeScenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        policeScenario.Service.ProcessNpcDamage(rogue, police, 5f);
        bool policeOnly = policeScenario.Spawned.All(ship => ship.FactionId == FactionManager.LibertyPolice);

        Scenario rogueScenario = CreateScenario(FactionManager.LibertyRogues);
        NpcShip rogueVictim = AddNpc(rogueScenario, FactionManager.LibertyRogues, Vector3.Zero);
        NpcShip policeAttacker = AddNpc(rogueScenario, FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
        rogueScenario.Service.ProcessNpcDamage(policeAttacker, rogueVictim, 5f);
        bool rogueOnly = rogueScenario.Spawned.All(ship => ship.FactionId == FactionManager.LibertyRogues);
        return policeOnly && rogueOnly;
    }

    private static bool TrafficSpawnPlacementIsSafe()
    {
        ConfigurationManager config = new();
        RunSilenced(config.LoadAll);
        List<NpcShip> ships = new();
        List<SpaceObject> objects = new();
        TrafficManager traffic = RunSilenced(() => new TrafficManager(config, ships, objects));
        RunSilenced(() => traffic.LoadZonesForSystem(1));

        NpcShip police = AddNpc(ships, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(ships, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        FactionDistressResponseResult result = RunSilenced(() => traffic.NotifyNpcDamage(rogue, police, 5f));
        List<NpcShip> spawned = ships.Where(ship => ship.IsDistressReinforcement).ToList();
        return result.WaveSpawned && spawned.Count == 2 &&
            spawned.All(ship => Vector3.Distance(ship.Position, police.Position) >= 1800f &&
                !ships.Any(other => other != ship && !other.IsDestroyed && Vector3.Distance(other.Position, ship.Position) < 500f));
    }

    private static bool DestroyedReinforcementFreesCap()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        scenario.Spawned[0].Hull.TakeDamage(scenario.Spawned[0].Hull.MaxHull);
        return scenario.Service.CountActiveReinforcements(FactionManager.LibertyPolice, Vector3.Zero) == 1;
    }

    private static bool DespawnedReinforcementFreesCap()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        scenario.Ships.Remove(scenario.Spawned[0]);
        return scenario.Service.CountActiveReinforcements(FactionManager.LibertyPolice, Vector3.Zero) == 1;
    }

    private static bool TransientStateIsCleaned()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        scenario.Service.Update(FactionDistressResponseService.EncounterExpirySeconds + 1f);
        return scenario.Service.ActiveEncounterCount == 0 && scenario.Service.CooldownRecordCount == 0;
    }

    private static bool SaveDataContainsNoDistressState()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        string save = JsonSerializer.Serialize(new SaveGameData());
        return !save.Contains("Distress", StringComparison.OrdinalIgnoreCase) &&
            !save.Contains("Reinforcement", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResetClearsTransientState()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        scenario.Service.ProcessNpcDamage(rogue, police, 5f);
        scenario.Service.Reset();
        police.ClearEncounterState();
        return scenario.Service.ActiveEncounterCount == 0 && scenario.Service.CooldownRecordCount == 0 &&
            police.FactionCombatTarget == null && police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool OrdinaryTrafficStillCruises()
    {
        NpcShip traffic = CreateNpc("ordinary", FactionManager.NeutralCivilians, Vector3.Zero);
        traffic.ConfigureTrafficBehavior(TrafficZoneBehaviorType.StationTraffic, "ordinary", Vector3.Zero, 900f, 90f, 3000f);
        Vector3 before = traffic.Position;
        traffic.Update(Frame(0.5f), null);
        return traffic.Position != before && traffic.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool Phase32CombatRemainsIntact()
    {
        NpcShip police = CreateNpc("police", FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc("rogue", FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        return FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues) == FactionRelationshipKind.Hostile &&
            police.SetFactionCombatTarget(rogue) && police.HasValidFactionCombatTarget();
    }

    private static bool SimultaneousLocalCombatStaysBounded()
    {
        Scenario scenario = CreateScenario(FactionManager.LibertyPolice);
        for (int i = 0; i < 8; i++)
        {
            Vector3 position = new(i * 100f, 0f, 0f);
            NpcShip police = AddNpc(scenario, FactionManager.LibertyPolice, position);
            NpcShip rogue = AddNpc(scenario, FactionManager.LibertyRogues, position + new Vector3(50f, 0f, 0f));
            scenario.Service.ProcessNpcDamage(rogue, police, 5f);
            scenario.Service.ProcessNpcDamage(police, rogue, 5f);
        }

        return scenario.Spawned.Count <= 4 &&
            scenario.Service.CountActiveReinforcements(FactionManager.LibertyPolice, Vector3.Zero) <= 2 &&
            scenario.Service.CountActiveReinforcements(FactionManager.LibertyRogues, Vector3.Zero) <= 2;
    }

    private static Scenario CreateScenario(string factionId)
    {
        Scenario scenario = new();
        return scenario;
    }

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
        reputation.SetReputation(FactionManager.LibertyPolice, 0f, "Phase 33 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0f, "Phase 33 smoke setup");
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
