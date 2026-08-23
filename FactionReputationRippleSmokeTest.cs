#nullable enable

using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 31 proof. It drives the authoritative combat
/// consequence service directly, then queries the existing access, mission,
/// disposition, bribe, and persistence authorities.
/// </summary>
internal sealed class FactionReputationRippleSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("Police/Rogue relationship is hostile", PoliceRogueRelationshipIsHostile);
        Check("Rogue/Police relationship is hostile", RoguePoliceRelationshipIsHostile);
        Check("configured relationship is symmetric", RelationshipIsSymmetric);
        Check("unknown relationship defaults neutral", UnknownRelationshipDefaultsNeutral);
        Check("Rogue destruction rewards Police exactly", RogueDestructionRewardsPolice);
        Check("Police destruction rewards Rogues exactly", PoliceDestructionRewardsRogues);
        Check("victim receives the normal direct Phase 30 consequence", VictimReceivesDirectConsequence);
        Check("direct and secondary mutations remain independent", DirectAndSecondaryAreIndependent);
        Check("damage alone cannot award an enemy-kill reward", DamageAloneDoesNotReward);
        Check("destruction reward applies exactly once", DestructionRewardAppliesOnce);
        Check("duplicate destruction callback is ignored", DuplicateDestructionCallbackIsIgnored);
        Check("post-death updates cannot duplicate the reward", PostDeathUpdatesCannotDuplicate);
        Check("NPC-caused kill gives the player no reward", NpcCausedKillGivesNoReward);
        Check("environmental kill gives the player no reward", EnvironmentalKillGivesNoReward);
        Check("invalid attribution gives the player no reward", InvalidAttributionGivesNoReward);
        Check("self-defense against hostile Rogues rewards Police", SelfDefenseRewardsPolice);
        Check("self-defense does not reintroduce a Rogue murder penalty", SelfDefenseAvoidsMurderPenalty);
        Check("Rogue direct consequence remains separate from Police reward", RogueConsequenceRemainsCorrect);
        Check("Police temporary hostility survives the enemy reward", TemporaryHostilitySurvivesReward);
        Check("Police permanent hostility recovers above -0.60", PermanentHostilityRecovers);
        Check("NPC disposition updates after recovery", DispositionUpdatesAfterRecovery);
        Check("docking updates after recovery", DockingUpdatesAfterRecovery);
        Check("service authorization updates after recovery", ServiceUpdatesAfterRecovery);
        Check("Friendly equipment gate responds to accumulated rewards", FriendlyEquipmentRespondsToRewards);
        Check("mission eligibility responds to resulting reputation", MissionEligibilityRespondsToRewards);
        Check("bribe and enemy-kill recovery compose", BribeAndEnemyKillRecoveryCompose);
        Check("bribe ceiling remains unchanged", BribeCeilingRemainsUnchanged);
        Check("Police/Rogue ripples leave unrelated factions unchanged", UnrelatedFactionsRemainUnchanged);
        Check("save/load preserves resulting reputation", SaveLoadPreservesResultingReputation);
        Check("reset restores baseline and keeps static relationships", ResetRestoresBaseline);
        Check("relationship definitions require no save-schema state", RelationshipDefinitionsAreNotPersisted);
        Check("relationship matrix does not create NPC diplomacy", MatrixDoesNotCreateNpcDiplomacy);

        Console.WriteLine($"[FACTION REPUTATION RIPPLE SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION REPUTATION RIPPLE SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION REPUTATION RIPPLE SMOKE] FAIL {label}: {reason}");
    }

    private static bool PoliceRogueRelationshipIsHostile() =>
        FactionRelationshipMatrix.GetRelationship(
            FactionManager.LibertyPolice,
            FactionManager.LibertyRogues) == FactionRelationshipKind.Hostile;

    private static bool RoguePoliceRelationshipIsHostile() =>
        FactionRelationshipMatrix.GetRelationship(
            FactionManager.LibertyRogues,
            FactionManager.LibertyPolice) == FactionRelationshipKind.Hostile;

    private static bool RelationshipIsSymmetric() =>
        FactionRelationshipMatrix.GetRelationship(
            FactionManager.LibertyPolice,
            FactionManager.LibertyRogues) ==
        FactionRelationshipMatrix.GetRelationship(
            FactionManager.LibertyRogues,
            FactionManager.LibertyPolice);

    private static bool UnknownRelationshipDefaultsNeutral() =>
        FactionRelationshipMatrix.GetRelationship("unknown_a", "unknown_b") == FactionRelationshipKind.Neutral &&
        FactionRelationshipMatrix.GetRelationship("unknown_a", FactionManager.LibertyPolice) == FactionRelationshipKind.Neutral;

    private static bool RogueDestructionRewardsPolice()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionCombatConsequenceService.EnemyKillReputationReward);
    }

    private static bool PoliceDestructionRewardsRogues()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyPolice);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), FactionCombatConsequenceService.EnemyKillReputationReward);
    }

    private static bool VictimReceivesDirectConsequence()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        float expectedRogue = FactionCombatConsequenceService.InitialAggressionReputationPenalty +
            FactionCombatConsequenceService.NeutralNpcDestructionReputationPenalty;
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), expectedRogue) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionCombatConsequenceService.EnemyKillReputationReward);
    }

    private static bool DirectAndSecondaryAreIndependent()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        List<ReputationChangeResult> changes = new();
        reputation.OnReputationChanged += changes.Add;
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);

        ReputationChangeResult? direct = changes.FirstOrDefault(change =>
            change.Reason == ReputationChangeReason.FactionShipDestroyed &&
            !change.IsSecondaryEffect);
        ReputationChangeResult? secondary = changes.FirstOrDefault(change =>
            change.Reason == ReputationChangeReason.FactionShipDestroyed &&
            change.IsSecondaryEffect);
        return direct != null && secondary != null &&
            direct.FactionId == FactionManager.LibertyRogues && Nearly(direct.Delta, -0.12f) &&
            secondary.FactionId == FactionManager.LibertyPolice &&
            Nearly(secondary.Delta, FactionCombatConsequenceService.EnemyKillReputationReward) &&
            secondary.SourceFactionId == FactionManager.LibertyRogues;
    }

    private static bool DamageAloneDoesNotReward()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        float policeBefore = reputation.GetStanding(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), policeBefore) &&
            !reputation.IsFactionCurrentlyHostile(FactionManager.LibertyPolice);
    }

    private static bool DestructionRewardAppliesOnce()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(75f);
        reputation.ApplyPlayerShipDestroyed(rogue);
        float policeAfter = reputation.GetStanding(FactionManager.LibertyPolice);
        float rogueAfter = reputation.GetStanding(FactionManager.LibertyRogues);
        reputation.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), policeAfter) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyRogues), rogueAfter);
    }

    private static bool DuplicateDestructionCallbackIsIgnored()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.RecordPlayerDamage(rogue);
        reputation.ApplyPlayerShipDestroyed(rogue);
        float policeAfter = reputation.GetStanding(FactionManager.LibertyPolice);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), policeAfter);
    }

    private static bool PostDeathUpdatesCannotDuplicate()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(75f);
        rogue.Hull.TakeDamage(rogue.Hull.MaxHull);
        reputation.ApplyPlayerShipDestroyed(rogue);
        float policeAfter = reputation.GetStanding(FactionManager.LibertyPolice);
        reputation.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), policeAfter);
    }

    private static bool NpcCausedKillGivesNoReward()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.Hull.TakeDamage(rogue.Hull.MaxHull);
        return reputation.ApplyPlayerShipDestroyed(rogue) == null &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0f) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyRogues), 0f);
    }

    private static bool EnvironmentalKillGivesNoReward()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.Hull.TakeDamage(10f);
        rogue.Hull.TakeDamage(rogue.Hull.CurrentHull);
        return reputation.ApplyPlayerShipDestroyed(rogue) == null &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0f);
    }

    private static bool InvalidAttributionGivesNoReward()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        return !rogue.MarkDamagedByPlayer(0f) &&
            reputation.ApplyPlayerShipDestroyed(rogue) == null &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0f);
    }

    private static bool SelfDefenseRewardsPolice()
    {
        ReputationManager reputation = NewReputation(0f, ReputationManager.HostileThreshold);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionCombatConsequenceService.EnemyKillReputationReward);
    }

    private static bool SelfDefenseAvoidsMurderPenalty()
    {
        ReputationManager reputation = NewReputation(0f, ReputationManager.HostileThreshold);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), ReputationManager.HostileThreshold);
    }

    private static bool RogueConsequenceRemainsCorrect()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), -0.15f) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.04f);
    }

    private static bool TemporaryHostilitySurvivesReward()
    {
        ReputationManager reputation = NewReputation(0f, ReputationManager.HostileThreshold);
        RecordHit(reputation, FactionManager.LibertyPolice);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice),
            FactionCombatConsequenceService.InitialAggressionReputationPenalty +
            FactionCombatConsequenceService.EnemyKillReputationReward) &&
            reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool PermanentHostilityRecovers()
    {
        ReputationManager reputation = NewReputation(ReputationManager.HostileThreshold - 0.03f, ReputationManager.HostileThreshold);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.59f) &&
            !reputation.IsHostile(FactionManager.LibertyPolice);
    }

    private static bool DispositionUpdatesAfterRecovery()
    {
        ReputationManager reputation = NewReputation(ReputationManager.HostileThreshold - 0.03f, ReputationManager.HostileThreshold);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        bool hostileBefore = police.HasPlayerTarget;
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        police.ClearEncounterState();
        police.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return hostileBefore && !police.HasPlayerTarget &&
            FactionDispositionEvaluator.Evaluate(FactionManager.LibertyPolice, reputation) != FactionDisposition.Hostile;
    }

    private static bool DockingUpdatesAfterRecovery()
    {
        ReputationManager reputation = NewReputation(ReputationManager.HostileThreshold - 0.03f, ReputationManager.HostileThreshold);
        bool deniedBefore = !FactionAccessService.EvaluateDocking(reputation, FactionManager.LibertyPolice).IsAllowed;
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return deniedBefore && FactionAccessService.EvaluateDocking(reputation, FactionManager.LibertyPolice).IsAllowed;
    }

    private static bool ServiceUpdatesAfterRecovery()
    {
        ReputationManager reputation = NewReputation(ReputationManager.HostileThreshold - 0.03f, ReputationManager.HostileThreshold);
        bool deniedBefore = !FactionAccessService.EvaluateService(reputation, FactionManager.LibertyPolice, "equipment").IsAllowed;
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return deniedBefore && FactionAccessService.EvaluateService(reputation, FactionManager.LibertyPolice, "equipment").IsAllowed;
    }

    private static bool FriendlyEquipmentRespondsToRewards()
    {
        ReputationManager reputation = NewReputation(0.17f, ReputationManager.HostileThreshold);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        bool lockedBefore = !dealer.GetEquipmentAccess(pulse).IsAllowed;
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return lockedBefore && Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.21f) &&
            dealer.GetEquipmentAccess(pulse).IsAllowed;
    }

    private static bool MissionEligibilityRespondsToRewards()
    {
        ReputationManager reputation = NewReputation(0.17f, ReputationManager.HostileThreshold);
        MissionManager missions = new(new PlayerCredits(0), null, reputation);
        Mission mission = CreateMission(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        bool lockedBefore = !missions.GetMissionEligibility(mission).IsEligible;
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return lockedBefore && missions.GetMissionEligibility(mission).IsEligible;
    }

    private static bool BribeAndEnemyKillRecoveryCompose()
    {
        ReputationManager reputation = NewReputation(-0.01f, ReputationManager.HostileThreshold);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Police contact",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            reputation,
            credits);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.03f) &&
            offer?.IsValid == true &&
            FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionBribeService.BriberyCeiling);
    }

    private static bool BribeCeilingRemainsUnchanged()
    {
        ReputationManager reputation = NewReputation(0.03f, ReputationManager.HostileThreshold);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Police contact",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            reputation,
            credits);
        return offer?.ResultingReputation == FactionBribeService.BriberyCeiling &&
            FactionBribeService.BriberyCeiling == 0.10f;
    }

    private static bool UnrelatedFactionsRemainUnchanged()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        reputation.SetReputation(FactionManager.LibertyNavy, 0.12f, "Phase 31 smoke setup");
        reputation.SetReputation(FactionManager.NeutralCivilians, 0.13f, "Phase 31 smoke setup");
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        return Nearly(reputation.GetStanding(FactionManager.LibertyNavy), 0.12f) &&
            Nearly(reputation.GetStanding(FactionManager.NeutralCivilians), 0.13f);
    }

    private static bool SaveLoadPreservesResultingReputation()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguelancer-phase31-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameManager saveManager = new(path);
            ReputationManager source = NewReputation(0f, 0f);
            ApplyPlayerDestruction(source, FactionManager.LibertyRogues);
            SaveGameData data = new()
            {
                FactionReputation = saveManager.CaptureReputation(source),
                TemporaryHostility = saveManager.CaptureTemporaryHostility(source)
            };
            if (!saveManager.TrySave(data, out _) || !saveManager.TryLoad(out SaveGameData loaded, out _))
                return false;

            ReputationManager restored = NewReputation(0f, 0f);
            saveManager.ApplyReputation(restored, loaded);
            saveManager.ApplyTemporaryHostility(restored, loaded);
            return Nearly(restored.GetStanding(FactionManager.LibertyPolice), 0.04f) &&
                Nearly(restored.GetStanding(FactionManager.LibertyRogues), -0.15f) &&
                FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues) == FactionRelationshipKind.Hostile;
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static bool ResetRestoresBaseline()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        ApplyPlayerDestruction(reputation, FactionManager.LibertyRogues);
        reputation.ResetToNewGame();
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyRogues), 0.35f) &&
            FactionRelationshipMatrix.GetRelationship(FactionManager.LibertyPolice, FactionManager.LibertyRogues) == FactionRelationshipKind.Hostile;
    }

    private static bool RelationshipDefinitionsAreNotPersisted()
    {
        return SaveGameData.CurrentSchemaVersion == 10 &&
            !typeof(SaveGameData).GetProperties()
                .Any(property => property.Name.Contains("Relationship", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatrixDoesNotCreateNpcDiplomacy()
    {
        ReputationManager reputation = NewReputation(0f, 0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        Ship player = new(Vector3.Zero);
        police.Update(Frame(), null, player, reputation);
        rogue.Update(Frame(), null, player, reputation);
        return !police.HasPlayerTarget && !rogue.HasPlayerTarget;
    }

    private static ReputationManager NewReputation(float policeStanding, float rogueStanding)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, policeStanding, "Phase 31 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, rogueStanding, "Phase 31 smoke setup");
        return reputation;
    }

    private static NpcShip CreateNpc(string factionId) =>
        new("Phase 31 Smoke NPC", Vector3.Zero, Vector3.Zero, 1f, 0f, factionId);

    private static void ApplyPlayerDestruction(ReputationManager reputation, string factionId)
    {
        NpcShip target = CreateNpc(factionId);
        target.MarkDamagedByPlayer(target.Hull.MaxHull);
        reputation.ApplyPlayerShipDestroyed(target);
    }

    private static void RecordHit(ReputationManager reputation, string factionId)
    {
        NpcShip target = CreateNpc(factionId);
        target.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(target);
    }

    private static EquipmentDealer CreateEquipmentDealer(ReputationManager reputation, string factionId)
    {
        EquipmentDealer dealer = new(reputation);
        dealer.SetDockedStation(new Station(new StationConfig
        {
            Description = "Phase 31 station",
            FactionId = factionId,
            Radius = 100f,
            DockingRange = 100f,
            DockingApproachDistance = 50f
        }, null));
        return dealer;
    }

    private static Mission CreateMission(string factionId, float minimumStanding) =>
        new(
            MissionType.Delivery,
            MissionDifficulty.Easy,
            "Phase 31 mission",
            "Phase 31 destination",
            1_000,
            0f,
            "Phase 31 mission gate",
            factionId)
        {
            MinimumEmployerReputation = minimumStanding
        };

    private static GameTime Frame(float seconds = 0.1f) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static bool RunSilenced(Func<bool> action)
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
}
