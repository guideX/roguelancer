#nullable enable

using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 29 coverage for live faction disposition and the
/// existing NPC encounter/weapon target seam.
/// </summary>
internal sealed class FactionDispositionSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("neutral police reputation is neutral", NeutralPoliceIsNonHostile);
        Check("friendly police reputation is friendly", FriendlyPoliceIsNonHostile);
        Check("permanent hostile threshold is hostile", PermanentHostilityUsesThreshold);
        Check("standing just above hostile threshold is not hostile", JustAboveHostileThresholdIsNotHostile);
        Check("temporary hostility overrides positive standing", TemporaryHostilityOverridesStanding);
        Check("police hostility does not affect Rogue ships", PoliceAndRogueAreIndependent);
        Check("Rogue hostility does not affect Police ships", RogueAndPoliceAreIndependent);
        Check("hostile NPC acquires a valid faction target", HostileNpcAcquiresFactionTarget);
        Check("neutral NPC does not acquire player", NeutralNpcDoesNotAcquirePlayer);
        Check("friendly NPC does not acquire player", FriendlyNpcDoesNotAcquirePlayer);
        Check("temporary expiry removes faction target", TemporaryExpiryDisengagesNpc);
        Check("permanent hostility survives temporary expiry", PermanentHostilitySurvivesExpiry);
        Check("reputation recovery removes faction target", ReputationRecoveryDisengagesNpc);
        Check("bribe crossing threshold removes faction hostility", BribeRecoversDisposition);
        Check("bribe does not clear active temporary hostility", BribePreservesTemporaryHostility);
        Check("police refusal immediately targets Police NPCs", PoliceRefusalChangesNpcDisposition);
        Check("police refusal leaves Rogue NPCs unaffected", PoliceRefusalLeavesRoguesUnaffected);
        Check("docking denial follows the same Police hostility", PoliceRefusalDeniesDocking);
        Check("newly spawned NPC uses live disposition", NewlySpawnedNpcUsesLiveDisposition);
        Check("save/load derives NPC disposition", SaveLoadDerivesDisposition);
        Check("reset restores baseline NPC disposition", ResetRestoresBaselineDisposition);
        Check("player attack retaliation remains available", PlayerInitiatedRetaliationRemainsAvailable);
        Check("unrelated civilian NPC remains non-hostile", UnrelatedNpcRemainsNonHostile);
        Check("target HUD exposes live disposition", TargetHudExposesDisposition);

        Console.WriteLine($"[FACTION DISPOSITION SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION DISPOSITION SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION DISPOSITION SMOKE] FAIL {label}: {reason}");
    }

    private static bool NeutralPoliceIsNonHostile()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(0f, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.GetPlayerDisposition(reputation) == FactionDisposition.Neutral && !npc.HasPlayerTarget;
    }

    private static bool FriendlyPoliceIsNonHostile()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(0.35f, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.GetPlayerDisposition(reputation) == FactionDisposition.Friendly && !npc.HasPlayerTarget;
    }

    private static bool PermanentHostilityUsesThreshold()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(ReputationManager.HostileThreshold, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.GetPlayerDisposition(reputation) == FactionDisposition.Hostile && npc.HasFactionDerivedPlayerTarget;
    }

    private static bool JustAboveHostileThresholdIsNotHostile()
    {
        float standing = ReputationManager.HostileThreshold + ReputationManager.Precision;
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(standing, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.GetPlayerDisposition(reputation) != FactionDisposition.Hostile && !npc.HasPlayerTarget;
    }

    private static bool TemporaryHostilityOverridesStanding()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(0.55f, FactionManager.LibertyPolice);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.GetPlayerDisposition(reputation) == FactionDisposition.Hostile && npc.HasFactionDerivedPlayerTarget;
    }

    private static bool PoliceAndRogueAreIndependent()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "phase 29 isolation setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "phase 29 isolation setup");
        Ship player = new(Vector3.Zero);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        police.Update(Frame(0.1f), null, player, reputation);
        rogue.Update(Frame(0.1f), null, player, reputation);
        return police.HasFactionDerivedPlayerTarget && !rogue.HasPlayerTarget &&
            rogue.GetPlayerDisposition(reputation) != FactionDisposition.Hostile;
    }

    private static bool RogueAndPoliceAreIndependent()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, 0.35f, "phase 29 isolation setup");
        reputation.SetReputation(FactionManager.LibertyRogues, -0.70f, "phase 29 isolation setup");
        Ship player = new(Vector3.Zero);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        police.Update(Frame(0.1f), null, player, reputation);
        rogue.Update(Frame(0.1f), null, player, reputation);
        return rogue.HasFactionDerivedPlayerTarget && !police.HasPlayerTarget &&
            police.GetPlayerDisposition(reputation) != FactionDisposition.Hostile;
    }

    private static bool HostileNpcAcquiresFactionTarget()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(-0.70f, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.EncounterState == TrafficEncounterState.AttackingPlayer &&
            npc.HasValidPlayerTarget(reputation) &&
            npc.PlayerTargetReason == NpcPlayerTargetReason.FactionDisposition;
    }

    private static bool NeutralNpcDoesNotAcquirePlayer()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(0f, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.EncounterState == TrafficEncounterState.Cruising && !npc.HasValidPlayerTarget(reputation);
    }

    private static bool FriendlyNpcDoesNotAcquirePlayer()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(0.35f, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.EncounterState == TrafficEncounterState.Cruising && !npc.HasValidPlayerTarget(reputation);
    }

    private static bool TemporaryExpiryDisengagesNpc()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(0.35f, FactionManager.LibertyPolice);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        npc.Update(Frame(0.1f), null, player, reputation);
        return !reputation.IsFactionCurrentlyHostile(FactionManager.LibertyPolice) &&
            npc.EncounterState == TrafficEncounterState.Cruising && !npc.HasPlayerTarget;
    }

    private static bool PermanentHostilitySurvivesExpiry()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(-0.70f, FactionManager.LibertyPolice);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        npc.Update(Frame(0.1f), null, player, reputation);
        return reputation.IsHostile(FactionManager.LibertyPolice) &&
            npc.GetPlayerDisposition(reputation) == FactionDisposition.Hostile && npc.HasPlayerTarget;
    }

    private static bool ReputationRecoveryDisengagesNpc()
    {
        (ReputationManager reputation, NpcShip npc, Ship player) = CreateScenario(-0.70f, FactionManager.LibertyPolice);
        npc.Update(Frame(0.1f), null, player, reputation);
        reputation.SetReputation(FactionManager.LibertyPolice, -0.55f, "phase 29 recovery setup");
        npc.Update(Frame(0.1f), null, player, reputation);
        return npc.GetPlayerDisposition(reputation) != FactionDisposition.Hostile &&
            npc.EncounterState == TrafficEncounterState.Cruising && !npc.HasPlayerTarget &&
            FactionAccessService.EvaluateDocking(reputation, FactionManager.LibertyPolice).IsAllowed;
    }

    private static bool BribeRecoversDisposition()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.65f, "phase 29 bribe setup");
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Elena Vasquez",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            reputation,
            credits);
        if (offer?.IsValid != true || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;

        (NpcShip npc, Ship player) = (CreateNpc(FactionManager.LibertyPolice), new Ship(Vector3.Zero));
        npc.Update(Frame(0.1f), null, player, reputation);
        return reputation.GetStanding(FactionManager.LibertyPolice) > ReputationManager.HostileThreshold &&
            npc.GetPlayerDisposition(reputation) != FactionDisposition.Hostile && !npc.HasPlayerTarget;
    }

    private static bool BribePreservesTemporaryHostility()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.65f, "phase 29 bribe hostility setup");
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Elena Vasquez", FactionManager.LibertyPolice, FactionManager.LibertyPolice,
            FactionManager.LibertyPolice, reputation, credits);
        if (offer?.IsValid != true || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;

        return reputation.GetStanding(FactionManager.LibertyPolice) > ReputationManager.HostileThreshold &&
            reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            FactionDispositionEvaluator.Evaluate(FactionManager.LibertyPolice, reputation) == FactionDisposition.Hostile;
    }

    private static bool PoliceRefusalChangesNpcDisposition()
    {
        ReputationManager reputation = NewReputation();
        CargoHold cargo = new(100);
        cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PlayerCredits credits = new(10_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, reputation, out _, out _))
            return false;

        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.Update(Frame(0.1f), null, new Ship(Vector3.Zero), reputation);
        return reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) && police.HasFactionDerivedPlayerTarget;
    }

    private static bool PoliceRefusalLeavesRoguesUnaffected()
    {
        ReputationManager reputation = NewReputation();
        CargoHold cargo = new(100);
        cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PlayerCredits credits = new(10_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, reputation, out _, out _))
            return false;

        NpcShip rogue = CreateNpc(FactionManager.LibertyRogues);
        rogue.Update(Frame(0.1f), null, new Ship(Vector3.Zero), reputation);
        return !rogue.HasPlayerTarget && rogue.GetPlayerDisposition(reputation) != FactionDisposition.Hostile;
    }

    private static bool PoliceRefusalDeniesDocking()
    {
        ReputationManager reputation = NewReputation();
        CargoHold cargo = new(100);
        cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1);
        PlayerCredits credits = new(10_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, reputation, out _, out _))
            return false;

        return !FactionAccessService.EvaluateDocking(reputation, FactionManager.LibertyPolice).IsAllowed &&
            FactionDispositionEvaluator.IsHostile(FactionManager.LibertyPolice, reputation);
    }

    private static bool NewlySpawnedNpcUsesLiveDisposition()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "phase 29 spawn setup");
        List<NpcShip> npcs = new();
        List<SpaceObject> objects = new();
        ConfigurationManager configuration = new();
        configuration.LoadAll();
        TrafficManager traffic = new(configuration, npcs, objects);
        traffic.LoadZonesForSystem(1);
        NpcShip? police = npcs.FirstOrDefault(npc => npc.FactionId == FactionManager.LibertyPolice);
        if (police == null)
            return false;

        police.Position = new Vector3(400f, 0f, 0f);
        Ship player = new(Vector3.Zero);
        police.Update(Frame(0.1f), null, player, reputation);
        return police.GetPlayerDisposition(reputation) == FactionDisposition.Hostile && police.HasPlayerTarget;
    }

    private static bool SaveLoadDerivesDisposition()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguelancer-phase29-{Guid.NewGuid():N}.json");
        SaveGameManager saveManager = new(path);
        try
        {
            ReputationManager source = NewReputation();
            source.SetReputation(FactionManager.LibertyPolice, -0.70f, "phase 29 save setup");
            source.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            SaveGameData data = new()
            {
                FactionReputation = saveManager.CaptureReputation(source),
                TemporaryHostility = saveManager.CaptureTemporaryHostility(source)
            };
            if (!saveManager.TrySave(data, out _) || !saveManager.TryLoad(out SaveGameData? loaded, out _) || loaded == null)
                return false;

            ReputationManager restored = NewReputation();
            saveManager.ApplyReputation(restored, loaded);
            saveManager.ApplyTemporaryHostility(restored, loaded);
            NpcShip police = CreateNpc(FactionManager.LibertyPolice);
            police.Update(Frame(0.1f), null, new Ship(Vector3.Zero), restored);
            return restored.IsHostile(FactionManager.LibertyPolice) && police.HasFactionDerivedPlayerTarget;
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static bool ResetRestoresBaselineDisposition()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "phase 29 reset setup");
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        Ship player = new(Vector3.Zero);
        police.Update(Frame(0.1f), null, player, reputation);
        reputation.ResetToNewGame();
        police.Update(Frame(0.1f), null, player, reputation);
        return reputation.GetStanding(FactionManager.LibertyPolice) == -0.25f &&
            FactionDispositionEvaluator.Evaluate(FactionManager.LibertyPolice, reputation) != FactionDisposition.Hostile &&
            !police.HasPlayerTarget;
    }

    private static bool PlayerInitiatedRetaliationRemainsAvailable()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, 0.35f, "phase 29 retaliation setup");
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer();
        if (!reputation.RecordPlayerDamage(police))
            return false;

        police.Update(Frame(0.1f), null, new Ship(Vector3.Zero), reputation);
        return reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            police.HasPlayerInitiatedRetaliationTarget && police.HasValidPlayerTarget(reputation);
    }

    private static bool UnrelatedNpcRemainsNonHostile()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "phase 29 unrelated setup");
        NpcShip civilian = new("Civilian", new Vector3(400f, 0f, 0f), Vector3.Zero, 1f, 0f);
        civilian.Update(Frame(0.1f), null, new Ship(Vector3.Zero), reputation);
        return civilian.FactionId == FactionManager.NeutralCivilians &&
            civilian.GetPlayerDisposition(reputation) != FactionDisposition.Hostile && !civilian.HasPlayerTarget;
    }

    private static bool TargetHudExposesDisposition()
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "phase 29 HUD setup");
        NpcShip police = CreateNpc(FactionManager.LibertyPolice);
        return NavTargeting.TryBuildHudData(
                police,
                Vector3.Zero,
                reputation,
                reputation.FactionManager,
                null,
                out NavTargetHudData? hud,
                out _) &&
            hud != null && hud.DispositionLabel == "HOSTILE" && hud.FactionLabel == "Liberty Police";
    }

    private static ReputationManager NewReputation() => new(new FactionManager());

    private static (ReputationManager Reputation, NpcShip Npc, Ship Player) CreateScenario(float policeStanding, string factionId)
    {
        ReputationManager reputation = NewReputation();
        reputation.SetReputation(factionId, policeStanding, "phase 29 scenario setup");
        return (reputation, CreateNpc(factionId), new Ship(Vector3.Zero));
    }

    private static NpcShip CreateNpc(string factionId) =>
        new("Disposition Test NPC", new Vector3(400f, 0f, 0f), Vector3.Zero, 1f, 0f, factionId);

    private static GameTime Frame(float seconds)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
        return new GameTime(elapsed, elapsed);
    }

    private static bool RunSilenced(Func<bool> action)
    {
        TextWriter original = Console.Out;
        using StringWriter sink = new();
        Console.SetOut(sink);
        try
        {
            return action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
