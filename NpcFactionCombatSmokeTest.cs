#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 32 coverage for local NPC faction hostility, target
/// lifetime, existing pursuit/weapons, and player-consequence isolation.
/// </summary>
internal sealed class NpcFactionCombatSmokeTest
{
    private readonly GraphicsDevice _graphicsDevice;
    private int _passed;
    private int _failed;

    public NpcFactionCombatSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        Check("Police/Rogue relationship is hostile", () =>
            FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues) == FactionRelationshipKind.Hostile);
        Check("hostility is symmetric", () =>
            FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyRogues, FactionManager.LibertyPolice) == FactionRelationshipKind.Hostile);
        Check("Police recognizes Rogue as hostile", PoliceRecognizesRogue);
        Check("Rogue recognizes Police as hostile", RogueRecognizesPolice);
        Check("Police does not faction-target Police", () => !NpcFactionCombatTargeting.IsHostileFactionPair(FactionManager.LibertyPolice, FactionManager.LibertyPolice));
        Check("Rogue does not faction-target Rogue", () => !NpcFactionCombatTargeting.IsHostileFactionPair(FactionManager.LibertyRogues, FactionManager.LibertyRogues));
        Check("neutral relationship does not authorize combat", () => !NpcFactionCombatTargeting.IsHostileFactionPair(FactionManager.LibertyPolice, FactionManager.NeutralCivilians));
        Check("unknown relationship defaults non-hostile", () => !NpcFactionCombatTargeting.IsHostileFactionPair("unknown_source", "unknown_target"));
        Check("invalid faction identity is non-hostile", () =>
            !NpcFactionCombatTargeting.IsHostileFactionPair(string.Empty, FactionManager.LibertyRogues) &&
            !NpcFactionCombatTargeting.IsHostileFactionPair(null, FactionManager.LibertyPolice));
        Check("near hostile NPC is acquired", AcquireNearbyHostileNpc);
        Check("distant hostile NPC is not acquired", DistantHostileNpcIsNotAcquired);
        Check("valid faction target is retained", ValidTargetIsRetained);
        Check("nearest target selection is deterministic", DeterministicNearestSelection);
        Check("equal-distance target tie breaks by name", DeterministicNameTieBreak);
        Check("valid faction target has priority over player", FactionTargetHasPriorityOverPlayer);
        Check("legitimate hostile player target remains supported", PlayerTargetRemainsSupported);
        Check("player-initiated retaliation remains supported", PlayerRetaliationRemainsSupported);
        Check("faction target uses existing pursuit movement", FactionTargetUsesPursuit);
        Check("NPC weapon system fires at faction target", NpcWeaponFiresAtFactionTarget);
        Check("NPC damage uses shield/hull path", NpcDamageUsesExistingDamagePath);
        Check("NPC damage does not mark player attribution", NpcDamageDoesNotMarkPlayer);
        Check("NPC destruction causes no player penalty", NpcDestructionHasNoPlayerPenalty);
        Check("NPC destruction causes no player relationship reward", NpcDestructionHasNoPlayerReward);
        Check("player final blow keeps direct consequence", PlayerFinalBlowKeepsDirectConsequence);
        Check("player final blow keeps relationship ripple", PlayerFinalBlowKeepsRelationshipRipple);
        Check("NPC combat does not mutate static relationship", StaticRelationshipRemainsUnchanged);
        Check("temporary player hostility does not mutate faction hostility", TemporaryHostilityIsIndependent);
        Check("dead target is cleared", DeadTargetIsCleared);
        Check("out-of-range target is cleared", OutOfRangeTargetIsCleared);
        Check("another hostile contact can be acquired after loss", AnotherTargetCanBeAcquired);
        Check("newly spawned NPC starts without transient battle state", NewlySpawnedNpcStartsClean);
        Check("save data requires no NPC target persistence", SaveDataHasNoNpcTargetState);
        Check("reset clears transient NPC target state", ResetClearsTransientTarget);
        Check("HUD disposition remains player-relative", HudDispositionRemainsPlayerRelative);
        Check("duplicate local contacts resolve to one deterministic target", DuplicateContactProcessingIsBounded);

        Console.WriteLine($"[NPC FACTION COMBAT SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[NPC FACTION COMBAT SMOKE] PASS {label}");
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
        Console.WriteLine($"[NPC FACTION COMBAT SMOKE] FAIL {label}: {reason}");
    }

    private static bool PoliceRecognizesRogue()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        return police.SetFactionCombatTarget(rogue) &&
            police.HasValidFactionCombatTarget() &&
            police.FactionCombatTarget == rogue;
    }

    private static bool RogueRecognizesPolice()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        return rogue.SetFactionCombatTarget(police) &&
            rogue.HasValidFactionCombatTarget() &&
            rogue.FactionCombatTarget == police;
    }

    private static bool AcquireNearbyHostileNpc()
    {
        (TrafficManager traffic, List<NpcShip> ships, NpcShip police, NpcShip rogue) = CreateTrafficContactScene(400f);
        traffic.Update(Frame(), new Ship(new Vector3(1_000_000f, 0f, 0f)), NewReputation());
        return police.FactionCombatTarget == rogue && rogue.FactionCombatTarget == police && ships.Contains(police) && ships.Contains(rogue);
    }

    private static bool DistantHostileNpcIsNotAcquired()
    {
        (TrafficManager traffic, _, NpcShip police, NpcShip rogue) = CreateTrafficContactScene(2_000f);
        traffic.Update(Frame(), new Ship(new Vector3(1_000_000f, 0f, 0f)), NewReputation());
        return police.FactionCombatTarget == null && rogue.FactionCombatTarget == null;
    }

    private static bool ValidTargetIsRetained()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip firstRogue = CreateNpc("Rogue A", new Vector3(500f, 0f, 0f), FactionManager.LibertyRogues);
        NpcShip secondRogue = CreateNpc("Rogue B", new Vector3(100f, 0f, 0f), FactionManager.LibertyRogues);
        police.SetFactionCombatTarget(firstRogue);
        NpcShip? selected = NpcFactionCombatTargeting.SelectNearestHostileTarget(police, new[] { firstRogue, secondRogue }, police.TrafficActivationRange);
        return selected == secondRogue && police.FactionCombatTarget == firstRogue;
    }

    private static bool DeterministicNearestSelection()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip near = CreateNpc("Near Rogue", new Vector3(200f, 0f, 0f), FactionManager.LibertyRogues);
        NpcShip far = CreateNpc("Far Rogue", new Vector3(500f, 0f, 0f), FactionManager.LibertyRogues);
        return NpcFactionCombatTargeting.SelectNearestHostileTarget(police, new[] { far, near }, 1000f) == near;
    }

    private static bool DeterministicNameTieBreak()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip bravo = CreateNamedNpc("Bravo", FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        NpcShip alpha = CreateNamedNpc("Alpha", FactionManager.LibertyRogues, new Vector3(-300f, 0f, 0f));
        return NpcFactionCombatTargeting.SelectNearestHostileTarget(police, new[] { bravo, alpha }, 1000f) == alpha;
    }

    private static bool FactionTargetHasPriorityOverPlayer()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "Phase 32 smoke");
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        Ship player = new(new Vector3(100f, 0f, 0f));
        police.SetFactionCombatTarget(rogue);
        police.Update(Frame(), null, player, reputation);
        return police.FactionCombatTarget == rogue && !police.HasPlayerTarget && police.EncounterState == TrafficEncounterState.AttackingFactionNpc;
    }

    private static bool PlayerTargetRemainsSupported()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "Phase 32 smoke");
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        police.Update(Frame(), null, new Ship(new Vector3(100f, 0f, 0f)), reputation);
        return police.HasValidPlayerTarget(reputation) && police.HasPlayerTarget;
    }

    private static bool PlayerRetaliationRemainsSupported()
    {
        ReputationManager reputation = NewReputation();
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        police.MarkDamagedByPlayer(5f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        police.Update(Frame(), null, new Ship(new Vector3(100f, 0f, 0f)), reputation);
        return police.HasPlayerInitiatedRetaliationTarget;
    }

    private static bool FactionTargetUsesPursuit()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
        police.SetFactionCombatTarget(rogue);
        Vector3 before = police.Position;
        police.Update(Frame(0.5f), null);
        return police.Position != before && police.EncounterState == TrafficEncounterState.AttackingFactionNpc;
    }

    private bool NpcWeaponFiresAtFactionTarget()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
        rogue.Radius = 1000f;
        rogue.Shields.RegenDelay = 10000f;
        police.SetFactionCombatTarget(rogue);
        NpcWeaponSystem weaponSystem = new(_graphicsDevice, NewReputation());
        Ship player = new(new Vector3(100_000f, 0f, 0f));
        weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, player);
        weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, player);
        return rogue.Shields.CurrentShields < rogue.Shields.MaxShields;
    }

    private bool NpcDamageUsesExistingDamagePath()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
        rogue.Radius = 1000f;
        rogue.Shields.RegenDelay = 10000f;
        police.SetFactionCombatTarget(rogue);
        NpcWeaponSystem weaponSystem = new(_graphicsDevice, NewReputation());
        weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));
        weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));
        return rogue.Shields.CurrentShields < rogue.Shields.MaxShields && rogue.Hull.CurrentHull == rogue.Hull.MaxHull;
    }

    private bool NpcDamageDoesNotMarkPlayer()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, Vector3.Zero);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
        rogue.Radius = 1000f;
        police.SetFactionCombatTarget(rogue);
        NpcWeaponSystem weaponSystem = new(_graphicsDevice, NewReputation());
        weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));
        weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));
        return !rogue.WasDamagedByPlayer && rogue.PlayerDamageSequence == 0;
    }

    private static bool NpcDestructionHasNoPlayerPenalty()
    {
        ReputationManager reputation = NewReputation();
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.Hull.TakeDamage(rogue.Hull.MaxHull);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), 0f);
    }

    private static bool NpcDestructionHasNoPlayerReward()
    {
        ReputationManager reputation = NewReputation();
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        float policeBefore = reputation.GetStanding(FactionManager.LibertyPolice);
        rogue.Hull.TakeDamage(rogue.Hull.MaxHull);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), policeBefore);
    }

    private static bool PlayerFinalBlowKeepsDirectConsequence()
    {
        ReputationManager reputation = NewReputation();
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), FactionCombatConsequenceService.InitialAggressionReputationPenalty + FactionCombatConsequenceService.NeutralNpcDestructionReputationPenalty);
    }

    private static bool PlayerFinalBlowKeepsRelationshipRipple()
    {
        ReputationManager reputation = NewReputation();
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionCombatConsequenceService.EnemyKillReputationReward);
    }

    private static bool StaticRelationshipRemainsUnchanged()
    {
        FactionRelationshipKind before = FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        police.SetFactionCombatTarget(rogue);
        return before == FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues);
    }

    private static bool TemporaryHostilityIsIndependent()
    {
        ReputationManager reputation = NewReputation();
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        return FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues) == FactionRelationshipKind.Hostile &&
            FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyRogues, FactionManager.LibertyPolice) == FactionRelationshipKind.Hostile;
    }

    private static bool DeadTargetIsCleared()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        police.SetFactionCombatTarget(rogue);
        rogue.Hull.TakeDamage(rogue.Hull.MaxHull);
        police.Update(Frame(), null);
        return police.FactionCombatTarget == null && police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool OutOfRangeTargetIsCleared()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "smoke", Vector3.Zero, 100f, 100f, 100f);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(250f, 0f, 0f));
        police.SetFactionCombatTarget(rogue);
        police.Update(Frame(), null);
        return police.FactionCombatTarget == null && police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool AnotherTargetCanBeAcquired()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip deadRogue = CreateNpc("Dead Rogue", new Vector3(200f, 0f, 0f), FactionManager.LibertyRogues);
        NpcShip liveRogue = CreateNpc("Live Rogue", new Vector3(400f, 0f, 0f), FactionManager.LibertyRogues);
        police.SetFactionCombatTarget(deadRogue);
        deadRogue.Hull.TakeDamage(deadRogue.Hull.MaxHull);
        police.ClearFactionCombatTarget();
        NpcShip? replacement = NpcFactionCombatTargeting.SelectNearestHostileTarget(police, new[] { deadRogue, liveRogue }, police.TrafficActivationRange);
        return replacement == liveRogue && police.SetFactionCombatTarget(replacement!);
    }

    private static bool NewlySpawnedNpcStartsClean()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        return police.FactionCombatTarget == null && police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool SaveDataHasNoNpcTargetState()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        police.SetFactionCombatTarget(rogue);
        SaveGameData data = new();
        string serialized = JsonSerializer.Serialize(data);
        NpcShip restoredSpawn = CreateNpc(FactionManager.LibertyPolice);
        return restoredSpawn.FactionCombatTarget == null &&
            !serialized.Contains("FactionCombatTarget", StringComparison.OrdinalIgnoreCase) &&
            !serialized.Contains("CombatTarget", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResetClearsTransientTarget()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        police.SetFactionCombatTarget(rogue);
        police.ClearEncounterState();
        return police.FactionCombatTarget == null && police.EncounterState == TrafficEncounterState.Cruising;
    }

    private static bool HudDispositionRemainsPlayerRelative()
    {
        ReputationManager reputation = NewReputation();
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        return NpcFactionCombatTargeting.IsHostileFactionPair(FactionManager.LibertyPolice, FactionManager.LibertyRogues) &&
            rogue.GetPlayerDisposition(reputation) == FactionDisposition.Neutral;
    }

    private static bool DuplicateContactProcessingIsBounded()
    {
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
        IReadOnlyList<NpcShip> candidates = new[] { rogue, rogue, rogue };
        NpcShip? selected = NpcFactionCombatTargeting.SelectNearestHostileTarget(police, candidates, 1000f);
        return selected == rogue && police.SetFactionCombatTarget(selected!);
    }

    private static (TrafficManager Traffic, List<NpcShip> Ships, NpcShip Police, NpcShip Rogue) CreateTrafficContactScene(float separation)
    {
        ConfigurationManager config = new();
        RunSilenced(config.LoadAll);
        List<NpcShip> ships = new();
        List<SpaceObject> objects = new();
        TrafficManager traffic = RunSilenced(() =>
        {
            TrafficManager manager = new(config, ships, objects);
            manager.LoadZonesForSystem(1);
            return manager;
        });

        Vector3 origin = new(1_000_000f, 0f, 0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice, origin);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues, origin + new Vector3(separation, 0f, 0f));
        police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase32-police", origin, 100f, 100f, 1000f);
        rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase32-rogue", origin, 100f, 100f, 1000f);
        ships.Add(police);
        ships.Add(rogue);
        return (traffic, ships, police, rogue);
    }

    private static ReputationManager NewReputation()
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, 0f, "Phase 32 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0f, "Phase 32 smoke setup");
        return reputation;
    }

    private static NpcShip CreateNpc(string factionId, Vector3? position = null, string? displayNameOrFaction = null)
    {
        string actualFaction = displayNameOrFaction != null && IsKnownFaction(displayNameOrFaction)
            ? displayNameOrFaction
            : factionId;
        string name = displayNameOrFaction != null && !IsKnownFaction(displayNameOrFaction)
            ? factionId
            : "Smoke NPC";
        NpcShip npc = new(name, position ?? Vector3.Zero, position ?? Vector3.Zero, 1f, 0f, actualFaction);
        npc.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
            "Smoke Fighter",
            actualFaction,
            "SMOKE/fighter",
            TrafficZoneBehaviorType.LawfulPatrol,
            NpcLoadoutTier.Low));
        return npc;
    }

    private static NpcShip CreateNamedNpc(string name, string factionId, Vector3? position = null) =>
        new(name, position ?? Vector3.Zero, position ?? Vector3.Zero, 1f, 0f, factionId);

    private static bool IsKnownFaction(string value) =>
        value.Equals(FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase);

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
