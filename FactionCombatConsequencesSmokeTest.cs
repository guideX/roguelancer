#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 30 proof. Combat events are represented by the same
/// one-way NPC damage marker used by live weapon systems; simulation time is
/// advanced directly and no wall-clock waits are used.
/// </summary>
internal sealed class FactionCombatConsequencesSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("neutral Police NPC begins non-hostile", NeutralPoliceBeginsNonHostile);
        Check("player-caused damaging hit is recognized", PlayerDamageIsRecognized);
        Check("first unlawful aggression activates Police temporary hostility", FirstAggressionActivatesHostility);
        Check("initial aggression applies exact Police penalty", InitialAggressionPenaltyIsExact);
        Check("victim retaliates through existing behavior", VictimRetaliates);
        Check("nearby Police shares faction hostility", NearbyPoliceResponds);
        Check("Rogue NPC remains unaffected", RogueRemainsUnaffectedByPoliceAggression);
        Check("Police docking is denied through existing access", PoliceDockingUsesLiveHostility);
        Check("Police service access uses live hostility", PoliceServiceUsesLiveHostility);
        Check("neutral-victim destruction applies exact additional penalty", DestructionPenaltyIsExact);
        Check("destruction penalty applies exactly once", DestructionPenaltyAppliesOnce);
        Check("post-death updates cannot duplicate destruction penalty", PostDeathUpdatesDoNotDuplicate);
        Check("zero-damage event has no consequence", ZeroDamageIsIgnored);
        Check("non-player damage has no consequence", NonPlayerDamageIsIgnored);
        Check("permanently hostile victim can be destroyed without murder penalty", PermanentHostilityIsSelfDefense);
        Check("player-first aggression provenance survives retaliation", PlayerFirstProvenanceSurvivesRetaliation);
        Check("temporary hostility expires safely", TemporaryHostilityExpires);
        Check("standing above hostile threshold restores disposition after expiry", RecoveryAboveHostileThresholdRestoresDisposition);
        Check("repeated unlawful kills reach permanent hostility", RepeatedKillsReachPermanentHostility);
        Check("permanent hostility survives temporary expiry", PermanentHostilitySurvivesExpiry);
        Check("new Police NPC observes saved permanent hostility", NewlySpawnedPoliceObservesHostility);
        Check("Police combat leaves Rogue standing unchanged", PoliceCombatDoesNotRippleToRogues);
        Check("Rogue combat affects only Rogue standing", RogueCombatIsIndependent);
        Check("Phase 28 refusal preserves defensive-fire provenance", EnforcementRefusalIsSelfDefense);
        Check("bribe recovers standing across hostile threshold", BribeRecoversCombatStanding);
        Check("bribe does not erase active temporary hostility", BribePreservesActiveHostility);
        Check("save/load preserves combat reputation and hostility", SaveLoadPreservesCombatConsequences);
        Check("reset restores baseline reputation and disposition", ResetRestoresBaseline);
        Check("existing player retaliation remains functional", ExistingRetaliationRemainsFunctional);
        Check("existing faction disposition remains intact", ExistingDispositionRemainsIntact);

        Console.WriteLine($"[FACTION COMBAT CONSEQUENCES SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION COMBAT CONSEQUENCES SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION COMBAT CONSEQUENCES SMOKE] FAIL {label}: {reason}");
    }

    private static bool NeutralPoliceBeginsNonHostile()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        Ship player = new(Vector3.Zero);
        police.Update(Frame(), null, player, reputation);
        return reputation.GetBand(FactionManager.LibertyPolice) == ReputationBand.Neutral &&
            FactionDispositionEvaluator.Evaluate(FactionManager.LibertyPolice, reputation) != FactionDisposition.Hostile &&
            !police.HasPlayerTarget;
    }

    private static bool PlayerDamageIsRecognized()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(12f);
        return reputation.CombatConsequences.RecordPlayerDamage(police) &&
            police.WasDamagedByPlayer && police.PlayerDamageSequence == 1;
    }

    private static bool FirstAggressionActivatesHostility()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        return reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            reputation.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice) == TemporaryHostilityManager.DefaultDurationSeconds;
    }

    private static bool InitialAggressionPenaltyIsExact()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionCombatConsequenceService.InitialAggressionReputationPenalty);
    }

    private static bool VictimRetaliates()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        police.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return police.HasPlayerInitiatedRetaliationTarget && police.HasValidPlayerTarget(reputation);
    }

    private static bool NearbyPoliceResponds()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip attacker = CreateNpc(FactionManager.LibertyPolice, new Vector3(100f, 0f, 0f));
        NpcShip nearby = CreateNpc(FactionManager.LibertyPolice, new Vector3(120f, 0f, 0f));
        attacker.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(attacker);
        nearby.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return nearby.HasFactionDerivedPlayerTarget && nearby.HasValidPlayerTarget(reputation);
    }

    private static bool RogueRemainsUnaffectedByPoliceAggression()
    {
        ReputationManager reputation = NewReputation(0f);
        float rogueBefore = reputation.GetStanding(FactionManager.LibertyRogues);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), rogueBefore) &&
            !reputation.IsTemporarilyHostile(FactionManager.LibertyRogues) && !rogue.HasPlayerTarget;
    }

    private static bool PoliceDockingUsesLiveHostility()
    {
        ReputationManager reputation = NewReputation(0f);
        RecordHit(reputation, FactionManager.LibertyPolice);
        return !FactionAccessService.EvaluateDocking(reputation, FactionManager.LibertyPolice).IsAllowed;
    }

    private static bool PoliceServiceUsesLiveHostility()
    {
        ReputationManager reputation = NewReputation(0f);
        RecordHit(reputation, FactionManager.LibertyPolice);
        return !FactionAccessService.EvaluateService(reputation, FactionManager.LibertyPolice, "equipment").IsAllowed;
    }

    private static bool DestructionPenaltyIsExact()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        float expected = FactionCombatConsequenceService.InitialAggressionReputationPenalty +
            FactionCombatConsequenceService.NeutralNpcDestructionReputationPenalty;
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), expected);
    }

    private static bool DestructionPenaltyAppliesOnce()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        float after = reputation.GetStanding(FactionManager.LibertyPolice);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), after);
    }

    private static bool PostDeathUpdatesDoNotDuplicate()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(75f);
        police.Hull.TakeDamage(police.Hull.MaxHull);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        float after = reputation.GetStanding(FactionManager.LibertyPolice);
        for (int i = 0; i < 10; i++)
            reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), after);
    }

    private static bool ZeroDamageIsIgnored()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        bool marked = police.MarkDamagedByPlayer(0f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        return !marked && !police.WasDamagedByPlayer &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0f) &&
            !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool NonPlayerDamageIsIgnored()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.Hull.TakeDamage(12f);
        reputation.CombatConsequences.RecordPlayerDamage(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0f) &&
            !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool PermanentHostilityIsSelfDefense()
    {
        ReputationManager reputation = NewReputation(ReputationManager.HostileThreshold);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), ReputationManager.HostileThreshold) &&
            police.WasFactionHostileBeforePlayerAggression;
    }

    private static bool PlayerFirstProvenanceSurvivesRetaliation()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        RecordHit(reputation, police);
        police.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return police.HasPlayerInitiatedRetaliationTarget && !police.WasFactionHostileBeforePlayerAggression;
    }

    private static bool TemporaryHostilityExpires()
    {
        ReputationManager reputation = NewReputation(0f);
        RecordHit(reputation, FactionManager.LibertyPolice);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        return !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            reputation.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice) == 0f;
    }

    private static bool RecoveryAboveHostileThresholdRestoresDisposition()
    {
        ReputationManager reputation = NewReputation(-0.59f);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return !reputation.IsHostile(FactionManager.LibertyPolice) && !police.HasPlayerTarget;
    }

    private static bool RepeatedKillsReachPermanentHostility()
    {
        ReputationManager reputation = NewReputation(0f);
        for (int i = 0; i < 4; i++)
        {
            NpcShip police = CreateNpc(FactionManager.LibertyPolice);
            police.MarkDamagedByPlayer(75f);
            reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        }

        return reputation.GetStanding(FactionManager.LibertyPolice) <= ReputationManager.HostileThreshold &&
            reputation.IsHostile(FactionManager.LibertyPolice);
    }

    private static bool PermanentHostilitySurvivesExpiry()
    {
        ReputationManager reputation = NewReputation(ReputationManager.HostileThreshold);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        return reputation.IsHostile(FactionManager.LibertyPolice) &&
            !FactionAccessService.EvaluateDocking(reputation, FactionManager.LibertyPolice).IsAllowed;
    }

    private static bool NewlySpawnedPoliceObservesHostility()
    {
        ReputationManager reputation = NewReputation(ReputationManager.HostileThreshold);
        NpcShip spawnedPolice = CreateNpc(FactionManager.LibertyPolice);
        spawnedPolice.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return spawnedPolice.HasFactionDerivedPlayerTarget &&
            spawnedPolice.GetPlayerDisposition(reputation) == FactionDisposition.Hostile;
    }

    private static bool PoliceCombatDoesNotRippleToRogues()
    {
        ReputationManager reputation = NewReputation(0f);
        float rogueBefore = reputation.GetStanding(FactionManager.LibertyRogues);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), rogueBefore);
    }

    private static bool RogueCombatIsIndependent()
    {
        ReputationManager reputation = NewReputation(0f);
        float policeBefore = reputation.GetStanding(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(rogue);
        return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), 0.20f) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), policeBefore);
    }

    private static bool EnforcementRefusalIsSelfDefense()
    {
        ReputationManager reputation = NewReputation(0.30f);
        CargoHold cargo = new(20);
        cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PoliceEnforcementService enforcement = new();
        PoliceEnforcementOffer offer = enforcement.Evaluate(FactionManager.LibertyPolice, cargo, new PlayerCredits(10_000));
        if (!enforcement.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, new PlayerCredits(10_000), reputation, out _, out _))
            return false;

        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(75f);
        reputation.CombatConsequences.ApplyPlayerShipDestroyed(police);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.10f) &&
            police.WasFactionHostileBeforePlayerAggression;
    }

    private static bool BribeRecoversCombatStanding()
    {
        ReputationManager reputation = NewReputation(-0.65f);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Police contact", FactionManager.LibertyPolice, FactionManager.LibertyPolice,
            FactionManager.LibertyPolice, reputation, credits);
        return offer?.IsValid == true &&
            FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionBribeService.BriberyCeiling) &&
            !reputation.IsHostile(FactionManager.LibertyPolice);
    }

    private static bool BribePreservesActiveHostility()
    {
        ReputationManager reputation = NewReputation(0f);
        RecordHit(reputation, FactionManager.LibertyPolice);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Police contact", FactionManager.LibertyPolice, FactionManager.LibertyPolice,
            FactionManager.LibertyPolice, reputation, credits);
        return offer?.IsValid == true &&
            FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _) &&
            reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool SaveLoadPreservesCombatConsequences()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguelancer-phase30-{Guid.NewGuid():N}.json");
        SaveGameManager saveManager = new(path);
        try
        {
            ReputationManager source = NewReputation(0f);
            NpcShip police = CreateNpc(FactionManager.LibertyPolice);
            police.MarkDamagedByPlayer(75f);
            source.CombatConsequences.ApplyPlayerShipDestroyed(police);
            SaveGameData data = new()
            {
                FactionReputation = saveManager.CaptureReputation(source),
                TemporaryHostility = saveManager.CaptureTemporaryHostility(source)
            };
            if (!saveManager.TrySave(data, out _) || !saveManager.TryLoad(out SaveGameData loaded, out _))
                return false;

            ReputationManager restored = NewReputation(0f);
            saveManager.ApplyReputation(restored, loaded);
            saveManager.ApplyTemporaryHostility(restored, loaded);
            return Nearly(restored.GetStanding(FactionManager.LibertyPolice), -0.15f) &&
                restored.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static bool ResetRestoresBaseline()
    {
        ReputationManager reputation = NewReputation(0f);
        RecordHit(reputation, FactionManager.LibertyPolice);
        reputation.ResetToNewGame();
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f) &&
            Nearly(reputation.GetStanding(FactionManager.LibertyRogues), 0.35f) &&
            !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            !police.HasPlayerTarget;
    }

    private static bool ExistingRetaliationRemainsFunctional()
    {
        ReputationManager reputation = NewReputation(0f);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        RecordHit(reputation, police);
        police.Update(Frame(), null, new Ship(Vector3.Zero), reputation);
        return police.HasPlayerInitiatedRetaliationTarget && police.PlayerTargetReason == NpcPlayerTargetReason.PlayerInitiatedAggression;
    }

    private static bool ExistingDispositionRemainsIntact()
    {
        ReputationManager reputation = NewReputation(0f);
        reputation.SetReputation(FactionManager.LibertyPolice, 0.35f, "Phase 30 disposition proof");
        bool friendly = FactionDispositionEvaluator.Evaluate(FactionManager.LibertyPolice, reputation) == FactionDisposition.Friendly;
        reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "Phase 30 disposition proof");
        bool hostile = FactionDispositionEvaluator.Evaluate(FactionManager.LibertyPolice, reputation) == FactionDisposition.Hostile;
        return friendly && hostile;
    }

    private static ReputationManager NewReputation(float policeStanding)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, policeStanding, "Phase 30 smoke setup");
        return reputation;
    }

    private static NpcShip CreateNpc(string factionId, Vector3? position = null) =>
        new("Smoke NPC", position ?? Vector3.Zero, position ?? Vector3.Zero, 1f, 0f, factionId);

    private static void RecordHit(ReputationManager reputation, string factionId)
    {
        NpcShip target = CreateNpc(factionId);
        target.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(target);
    }

    private static void RecordHit(ReputationManager reputation, NpcShip target)
    {
        target.MarkDamagedByPlayer(12f);
        reputation.CombatConsequences.RecordPlayerDamage(target);
    }

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
