#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Focused Phase 36 coverage. These tests exercise the presentation observer
/// directly so failures cannot be hidden by rendering or audio state.
/// </summary>
internal sealed class FactionCombatCommunicationSmokeTest
{
    private sealed class Scenario
    {
        public List<NpcShip> Ships { get; } = new();
        public Ship Player { get; } = new(Vector3.Zero);
        public NpcShip Police { get; }
        public NpcShip Rogue { get; }
        public FactionCombatCommunicationService Service { get; }

        public Scenario(float policePosition = 100f, float roguePosition = 300f)
        {
            Police = CreateNpc("Phase 36 Police", FactionManager.LibertyPolice, new Vector3(policePosition, 0f, 0f));
            Rogue = CreateNpc("Phase 36 Rogue", FactionManager.LibertyRogues, new Vector3(roguePosition, 0f, 0f));
            Ships.Add(Police);
            Ships.Add(Rogue);
            Service = new FactionCombatCommunicationService(Ships);
            Service.RegisterShip(Police);
            Service.RegisterShip(Rogue);
            Service.Update(0f, Player);
        }
    }

    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("Police engagement is communicated once", PoliceEngagementIsBounded);
        Check("Rogue engagement is communicated once", RogueEngagementIsBounded);
        Check("genuine reacquisition can communicate again", ReacquisitionCommunicatesAgain);
        Check("accepted Police distress is communicated", PoliceDistressIsCommunicated);
        Check("accepted Rogue distress is communicated", RogueDistressIsCommunicated);
        Check("rejected zero damage is silent", RejectedZeroDamageIsSilent);
        Check("environmental damage is silent", EnvironmentalDamageIsSilent);
        Check("self damage is silent", SelfDamageIsSilent);
        Check("invalid attacker is silent", InvalidAttackerIsSilent);
        Check("duplicate distress is suppressed", DuplicateDistressIsSuppressed);
        Check("actual distress responder acknowledges", ActualDistressResponderAcknowledges);
        Check("nearby assistance can acknowledge", NearbyAssistanceAcknowledges);
        Check("failed distress response is not announced", FailedDistressResponseIsNotAnnounced);
        Check("Police escalation and heavy acknowledgement are surfaced", PoliceEscalationIsSurfaced);
        Check("Rogue escalation is surfaced", RogueEscalationIsSurfaced);
        Check("escalation is emitted once", EscalationIsEmittedOnce);
        Check("ambient chatter respects speaker cooldown", AmbientSpeakerCooldownIsRespected);
        Check("ambient chatter respects encounter cooldown", AmbientEncounterCooldownIsRespected);
        Check("queue remains bounded", QueueRemainsBounded);
        Check("low-priority chatter can be dropped", LowPriorityChatterCanBeDropped);
        Check("line selection is deterministic", LineSelectionIsDeterministic);
        Check("Police and Rogue pools are distinct", FactionPoolsAreDistinct);
        Check("player-directed Police warning is appropriate", PolicePlayerWarningIsAppropriate);
        Check("player-directed Rogue warning is appropriate", RoguePlayerWarningIsAppropriate);
        Check("nearby NPC-only combat is relevant", NearbyNpcCombatIsRelevant);
        Check("distant NPC-only combat is suppressed", DistantNpcCombatIsSuppressed);
        Check("player-involved combat remains relevant", PlayerInvolvedCombatIsRelevant);
        Check("meaningful disengagement can communicate target loss", DisengagementCommunicatesTargetLost);
        Check("destruction cleanup does not chatter", DestructionCleanupDoesNotChatter);
        Check("reset clears transient communication state", ResetClearsTransientState);
        Check("communications do not mutate reputation", CommunicationsDoNotMutateReputation);
        Check("communications do not alter combat target", CommunicationsDoNotAlterCombatTarget);
        Check("save schema remains current", SaveSchemaRemainsVersionTen);
        Check("TrafficManager integration reports ordinary acquisition", TrafficManagerReportsAcquisition);

        Console.WriteLine($"[FACTION COMBAT COMMUNICATION SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION COMBAT COMMUNICATION SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION COMBAT COMMUNICATION SMOKE] FAIL {label}: {reason}");
    }

    private static bool PoliceEngagementIsBounded()
    {
        Scenario scenario = new();
        bool first = scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        bool duplicate = scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        FactionCombatCommunicationRequest request = FindRequest(scenario.Service, FactionCombatCommunicationType.HostileContact);
        return first && !duplicate && request.FactionId == FactionManager.LibertyPolice &&
            request.LineId.StartsWith("police_", StringComparison.Ordinal);
    }

    private static bool RogueEngagementIsBounded()
    {
        Scenario scenario = new();
        bool first = scenario.Service.NotifyEngagementAcquired(scenario.Rogue, scenario.Police, scenario.Player);
        bool duplicate = scenario.Service.NotifyEngagementAcquired(scenario.Rogue, scenario.Police, scenario.Player);
        FactionCombatCommunicationRequest request = FindRequest(scenario.Service, FactionCombatCommunicationType.HostileContact);
        return first && !duplicate && request.FactionId == FactionManager.LibertyRogues &&
            request.LineId.StartsWith("rogue_", StringComparison.Ordinal);
    }

    private static bool ReacquisitionCommunicatesAgain()
    {
        Scenario scenario = new();
        scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        scenario.Service.Update(FactionCombatCommunicationService.PerSpeakerCooldownSeconds, scenario.Player);
        bool lost = scenario.Service.NotifyDisengagement(new FactionCombatDisengagementEvent(
            scenario.Police,
            false,
            scenario.Rogue,
            scenario.Rogue.Position,
            FactionCombatDisengagementReason.ExcessivePursuitDistance,
            scenario.Service.SimulationTime));
        scenario.Service.Update(FactionCombatCommunicationService.PerSpeakerCooldownSeconds, scenario.Player);
        bool reacquired = scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        return lost && reacquired && CountType(scenario.Service, FactionCombatCommunicationType.HostileContact) == 2;
    }

    private static bool PoliceDistressIsCommunicated()
    {
        Scenario scenario = new();
        FactionDistressResponseResult response = DistressResult(true, "police-distress", FactionManager.LibertyPolice);
        return scenario.Service.NotifyDistressRequest(response, scenario.Police, scenario.Rogue) &&
            FindRequest(scenario.Service, FactionCombatCommunicationType.DistressRequest).LineId.StartsWith("police_", StringComparison.Ordinal);
    }

    private static bool RogueDistressIsCommunicated()
    {
        Scenario scenario = new();
        FactionDistressResponseResult response = DistressResult(true, "rogue-distress", FactionManager.LibertyRogues);
        return scenario.Service.NotifyDistressRequest(response, scenario.Rogue, scenario.Police) &&
            FindRequest(scenario.Service, FactionCombatCommunicationType.DistressRequest).LineId.StartsWith("rogue_", StringComparison.Ordinal);
    }

    private static bool RejectedZeroDamageIsSilent() => RejectedDistressIsSilent("zero damage");
    private static bool EnvironmentalDamageIsSilent() => RejectedDistressIsSilent("environmental damage");
    private static bool SelfDamageIsSilent() => RejectedDistressIsSilent("self damage");
    private static bool InvalidAttackerIsSilent() => RejectedDistressIsSilent("invalid attacker");

    private static bool RejectedDistressIsSilent(string reason)
    {
        Scenario scenario = new();
        FactionDistressResponseResult response = new(
            Accepted: false,
            WaveSpawned: false,
            CooldownBlocked: false,
            AssistedShipCount: 0,
            SpawnedShipCount: 0,
            EncounterId: string.Empty,
            FactionId: FactionManager.LibertyPolice,
            Reason: reason);
        return !scenario.Service.NotifyDistressRequest(response, scenario.Police, scenario.Rogue) &&
            scenario.Service.QueuedMessageCount == 0;
    }

    private static bool DuplicateDistressIsSuppressed()
    {
        Scenario scenario = new();
        FactionDistressResponseResult response = DistressResult(true, "duplicate-distress", FactionManager.LibertyPolice);
        bool first = scenario.Service.NotifyDistressRequest(response, scenario.Police, scenario.Rogue);
        bool duplicate = scenario.Service.NotifyDistressRequest(response, scenario.Police, scenario.Rogue);
        return first && !duplicate && CountType(scenario.Service, FactionCombatCommunicationType.DistressRequest) == 1;
    }

    private static bool ActualDistressResponderAcknowledges()
    {
        Scenario scenario = new();
        FactionDistressResponseResult response = DistressResult(true, "distress-wave", FactionManager.LibertyPolice);
        scenario.Service.NotifyDistressRequest(response, scenario.Police, scenario.Rogue);
        NpcShip responder = CreateNpc("Police Responder", FactionManager.LibertyPolice, new Vector3(400f, 0f, 0f));
        responder.MarkDistressReinforcement(response.EncounterId);
        scenario.Ships.Add(responder);
        bool acknowledged = scenario.Service.NotifyDistressResponse(response);
        return acknowledged && CountType(scenario.Service, FactionCombatCommunicationType.DistressResponseAcknowledgement) == 1;
    }

    private static bool FailedDistressResponseIsNotAnnounced()
    {
        Scenario scenario = new();
        FactionDistressResponseResult response = DistressResult(false, "failed-wave", FactionManager.LibertyPolice);
        return !scenario.Service.NotifyDistressResponse(response) &&
            CountType(scenario.Service, FactionCombatCommunicationType.DistressResponseAcknowledgement) == 0;
    }

    private static bool NearbyAssistanceAcknowledges()
    {
        Scenario scenario = new();
        NpcShip helper = CreateNpc("Police Helper", FactionManager.LibertyPolice, new Vector3(500f, 0f, 0f));
        helper.SetFactionCombatTarget(scenario.Rogue);
        scenario.Ships.Add(helper);
        FactionDistressResponseResult response = new(
            Accepted: true,
            WaveSpawned: false,
            CooldownBlocked: true,
            AssistedShipCount: 1,
            SpawnedShipCount: 0,
            EncounterId: "nearby-assistance",
            FactionId: FactionManager.LibertyPolice,
            Reason: "nearby assistance committed");
        scenario.Service.NotifyDistressRequest(response, scenario.Police, scenario.Rogue);
        return scenario.Service.NotifyDistressResponse(response) &&
            CountType(scenario.Service, FactionCombatCommunicationType.DistressResponseAcknowledgement) == 1;
    }

    private static bool PoliceEscalationIsSurfaced()
    {
        Scenario scenario = new();
        FactionDistressResponseResult distress = DistressResult(true, "police-escalation", FactionManager.LibertyPolice);
        scenario.Service.NotifyDistressRequest(distress, scenario.Police, scenario.Rogue);
        scenario.Service.Update(FactionCombatCommunicationService.PerSpeakerCooldownSeconds, scenario.Player);
        NpcShip responder = CreateNpc("Police Heavy", FactionManager.LibertyPolice, new Vector3(500f, 0f, 0f));
        responder.MarkEscalationReinforcement(distress.EncounterId);
        scenario.Ships.Add(responder);
        FactionCombatEscalationResult escalation = EscalationResult(true, distress.EncounterId, FactionManager.LibertyPolice);
        bool emitted = scenario.Service.NotifyHeavyEscalation(escalation);
        return emitted && CountType(scenario.Service, FactionCombatCommunicationType.HeavyEscalation) == 1 &&
            CountType(scenario.Service, FactionCombatCommunicationType.HeavyEscalationResponseAcknowledgement) == 1;
    }

    private static bool RogueEscalationIsSurfaced()
    {
        Scenario scenario = new();
        FactionDistressResponseResult distress = DistressResult(true, "rogue-escalation", FactionManager.LibertyRogues);
        scenario.Service.NotifyDistressRequest(distress, scenario.Rogue, scenario.Police);
        scenario.Service.Update(FactionCombatCommunicationService.PerSpeakerCooldownSeconds, scenario.Player);
        NpcShip responder = CreateNpc("Rogue Heavy", FactionManager.LibertyRogues, new Vector3(500f, 0f, 0f));
        responder.MarkEscalationReinforcement(distress.EncounterId);
        scenario.Ships.Add(responder);
        FactionCombatEscalationResult escalation = EscalationResult(true, distress.EncounterId, FactionManager.LibertyRogues);
        return scenario.Service.NotifyHeavyEscalation(escalation) &&
            FindRequest(scenario.Service, FactionCombatCommunicationType.HeavyEscalation).LineId.StartsWith("rogue_", StringComparison.Ordinal);
    }

    private static bool EscalationIsEmittedOnce()
    {
        Scenario scenario = new();
        FactionDistressResponseResult distress = DistressResult(true, "one-escalation", FactionManager.LibertyPolice);
        scenario.Service.NotifyDistressRequest(distress, scenario.Police, scenario.Rogue);
        scenario.Service.Update(8f, scenario.Player);
        NpcShip responder = CreateNpc("Heavy Responder", FactionManager.LibertyPolice, new Vector3(500f, 0f, 0f));
        responder.MarkEscalationReinforcement(distress.EncounterId);
        scenario.Ships.Add(responder);
        FactionCombatEscalationResult escalation = EscalationResult(true, distress.EncounterId, FactionManager.LibertyPolice);
        bool first = scenario.Service.NotifyHeavyEscalation(escalation);
        bool duplicate = scenario.Service.NotifyHeavyEscalation(escalation);
        return first && !duplicate && CountType(scenario.Service, FactionCombatCommunicationType.HeavyEscalation) == 1 &&
            CountType(scenario.Service, FactionCombatCommunicationType.HeavyEscalationResponseAcknowledgement) == 1;
    }

    private static bool AmbientSpeakerCooldownIsRespected()
    {
        Scenario scenario = new();
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Rogue.SetFactionCombatTarget(scenario.Police);
        bool first = scenario.Service.NotifyCombatDamage(scenario.Police, scenario.Rogue, 5f);
        scenario.Service.Update(FactionCombatCommunicationService.PerSpeakerCooldownSeconds - 0.1f, scenario.Player);
        bool tooSoon = scenario.Service.NotifyCombatDamage(scenario.Police, scenario.Rogue, 5f);
        return first && !tooSoon;
    }

    private static bool AmbientEncounterCooldownIsRespected()
    {
        Scenario scenario = new();
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Rogue.SetFactionCombatTarget(scenario.Police);
        scenario.Service.NotifyCombatDamage(scenario.Police, scenario.Rogue, 5f);
        scenario.Service.Update(FactionCombatCommunicationService.PerSpeakerCooldownSeconds, scenario.Player);
        bool tooSoonForEncounter = scenario.Service.NotifyCombatDamage(scenario.Police, scenario.Rogue, 5f);
        scenario.Service.Update(FactionCombatCommunicationService.AmbientEncounterCooldownSeconds, scenario.Player);
        bool afterCooldown = scenario.Service.NotifyCombatDamage(scenario.Police, scenario.Rogue, 5f);
        return !tooSoonForEncounter && afterCooldown;
    }

    private static bool QueueRemainsBounded()
    {
        List<NpcShip> ships = new();
        NpcShip playerTarget = CreateNpc("Queue Target", FactionManager.LibertyRogues, new Vector3(200f, 0f, 0f));
        ships.Add(playerTarget);
        FactionCombatCommunicationService service = new(ships);
        Ship player = new(Vector3.Zero);
        service.Update(0f, player);
        for (int i = 0; i < FactionCombatCommunicationService.QueueCapacity + 8; i++)
        {
            NpcShip source = CreateNpc($"Queue Police {i}", FactionManager.LibertyPolice, new Vector3(100f + i, 0f, 0f));
            ships.Add(source);
            service.NotifyEngagementAcquired(source, playerTarget, player);
        }

        return service.QueuedMessageCount <= FactionCombatCommunicationService.QueueCapacity;
    }

    private static bool LowPriorityChatterCanBeDropped()
    {
        List<NpcShip> ships = new();
        FactionCombatCommunicationService service = new(ships);
        Ship player = new(Vector3.Zero);
        service.Update(0f, player);
        for (int i = 0; i < FactionCombatCommunicationService.QueueCapacity; i++)
        {
            NpcShip source = CreateNpc($"Ambient Police {i}", FactionManager.LibertyPolice, new Vector3(100f + i, 0f, 0f));
            NpcShip target = CreateNpc($"Ambient Rogue {i}", FactionManager.LibertyRogues, new Vector3(200f + i, 0f, 0f));
            source.SetFactionCombatTarget(target);
            target.SetFactionCombatTarget(source);
            ships.Add(source);
            ships.Add(target);
            service.NotifyCombatDamage(source, target, 5f);
        }

        NpcShip prioritySource = CreateNpc("Priority Police", FactionManager.LibertyPolice, new Vector3(100f, 0f, 0f));
        NpcShip priorityTarget = CreateNpc("Priority Rogue", FactionManager.LibertyRogues, new Vector3(200f, 0f, 0f));
        ships.Add(prioritySource);
        ships.Add(priorityTarget);
        bool queued = service.NotifyDistressRequest(
            DistressResult(true, "priority-distress", FactionManager.LibertyPolice),
            prioritySource,
            priorityTarget,
            player);
        return queued && service.DroppedMessageCount > 0 &&
            CountType(service, FactionCombatCommunicationType.DistressRequest) == 1;
    }

    private static bool LineSelectionIsDeterministic()
    {
        Scenario first = new();
        Scenario second = new();
        first.Service.NotifyEngagementAcquired(first.Police, first.Rogue, first.Player);
        second.Service.NotifyEngagementAcquired(second.Police, second.Rogue, second.Player);
        return FindRequest(first.Service, FactionCombatCommunicationType.HostileContact).LineId ==
            FindRequest(second.Service, FactionCombatCommunicationType.HostileContact).LineId;
    }

    private static bool FactionPoolsAreDistinct()
    {
        Scenario scenario = new();
        scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        scenario.Service.Update(8f, scenario.Player);
        scenario.Service.NotifyEngagementAcquired(scenario.Rogue, scenario.Police, scenario.Player);
        string police = FindRequest(scenario.Service, FactionCombatCommunicationType.HostileContact).LineId;
        string rogue = scenario.Service.GetPendingSnapshot()
            .First(request => request.FactionId.Equals(FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase))
            .LineId;
        return police.StartsWith("police_", StringComparison.Ordinal) && rogue.StartsWith("rogue_", StringComparison.Ordinal);
    }

    private static bool PolicePlayerWarningIsAppropriate()
    {
        Scenario scenario = new();
        scenario.Police.SetPlayerTarget(scenario.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        FactionCombatCommunicationRequest request = FindRequest(scenario.Service, FactionCombatCommunicationType.PlayerWarning);
        return request.PlayerInvolved && request.LineId.StartsWith("police_player_", StringComparison.Ordinal) &&
            request.Text.Contains("stand down", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RoguePlayerWarningIsAppropriate()
    {
        Scenario scenario = new();
        scenario.Rogue.SetPlayerTarget(scenario.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        FactionCombatCommunicationRequest request = FindRequest(scenario.Service, FactionCombatCommunicationType.PlayerWarning);
        return request.PlayerInvolved && request.LineId.StartsWith("rogue_player_", StringComparison.Ordinal) &&
            (request.Text.Contains("fight", StringComparison.OrdinalIgnoreCase) ||
                request.Text.Contains("freelancer", StringComparison.OrdinalIgnoreCase));
    }

    private static bool NearbyNpcCombatIsRelevant()
    {
        Scenario scenario = new();
        return scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue);
    }

    private static bool DistantNpcCombatIsSuppressed()
    {
        Scenario scenario = new(7_000f, 7_300f);
        return !scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue);
    }

    private static bool PlayerInvolvedCombatIsRelevant()
    {
        Scenario scenario = new(20_000f, 20_300f);
        scenario.Police.SetPlayerTarget(scenario.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        return scenario.Service.QueuedMessageCount == 1 &&
            FindRequest(scenario.Service, FactionCombatCommunicationType.PlayerWarning).PlayerInvolved;
    }

    private static bool DisengagementCommunicatesTargetLost()
    {
        Scenario scenario = new();
        scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        scenario.Service.Update(8f, scenario.Player);
        bool communicated = scenario.Service.NotifyDisengagement(new FactionCombatDisengagementEvent(
            scenario.Police,
            false,
            scenario.Rogue,
            scenario.Rogue.Position,
            FactionCombatDisengagementReason.StalePursuit,
            scenario.Service.SimulationTime));
        return communicated && CountType(scenario.Service, FactionCombatCommunicationType.TargetLost) == 1;
    }

    private static bool DestructionCleanupDoesNotChatter()
    {
        Scenario scenario = new();
        scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        scenario.Service.Update(8f, scenario.Player);
        bool communicated = scenario.Service.NotifyDisengagement(new FactionCombatDisengagementEvent(
            scenario.Police,
            false,
            scenario.Rogue,
            scenario.Rogue.Position,
            FactionCombatDisengagementReason.TargetDestroyed,
            scenario.Service.SimulationTime));
        return !communicated && CountType(scenario.Service, FactionCombatCommunicationType.TargetLost) == 0;
    }

    private static bool ResetClearsTransientState()
    {
        Scenario scenario = new();
        scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        scenario.Service.Reset();
        bool canSpeakAgain = scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        return canSpeakAgain && scenario.Service.QueuedMessageCount == 1 &&
            scenario.Service.SimulationTime == 0f && scenario.Service.DroppedMessageCount == 0;
    }

    private static bool CommunicationsDoNotMutateReputation()
    {
        Scenario scenario = new();
        ReputationManager reputation = NewReputation();
        float policeBefore = reputation.GetStanding(FactionManager.LibertyPolice);
        float rogueBefore = reputation.GetStanding(FactionManager.LibertyRogues);
        scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        scenario.Service.NotifyDistressRequest(DistressResult(true, "reputation-isolation", FactionManager.LibertyPolice), scenario.Police, scenario.Rogue);
        return Nearly(policeBefore, reputation.GetStanding(FactionManager.LibertyPolice)) &&
            Nearly(rogueBefore, reputation.GetStanding(FactionManager.LibertyRogues));
    }

    private static bool CommunicationsDoNotAlterCombatTarget()
    {
        Scenario scenario = new();
        scenario.Police.SetFactionCombatTarget(scenario.Rogue);
        scenario.Service.NotifyEngagementAcquired(scenario.Police, scenario.Rogue, scenario.Player);
        scenario.Service.NotifyCombatDamage(scenario.Police, scenario.Rogue, 5f);
        return scenario.Police.FactionCombatTarget == scenario.Rogue &&
            scenario.Police.EncounterState == TrafficEncounterState.AttackingFactionNpc;
    }

    private static bool SaveSchemaRemainsVersionTen()
    {
        string save = JsonSerializer.Serialize(new SaveGameData());
        return new SaveGameData().SchemaVersion == SaveGameData.CurrentSchemaVersion &&
            !save.Contains("communication", StringComparison.OrdinalIgnoreCase) &&
            !save.Contains("radio", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrafficManagerReportsAcquisition()
    {
        Configuration.ConfigurationManager configuration = new();
        RunSilenced(configuration.LoadAll);
        List<NpcShip> ships = new();
        TrafficManager traffic = RunSilenced(() => new TrafficManager(configuration, ships, new List<SpaceObject>()));
        RunSilenced(() => traffic.LoadZonesForSystem(1));
        ships.Clear();
        Vector3 origin = new(1_000_000f, 0f, 0f);
        NpcShip police = CreateNpc("Integration Police", FactionManager.LibertyPolice, origin);
        NpcShip rogue = CreateNpc("Integration Rogue", FactionManager.LibertyRogues, origin + new Vector3(300f, 0f, 0f));
        police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase36", origin, 100f, 100f, 1000f);
        rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase36", origin, 100f, 100f, 1000f);
        ships.Add(police);
        ships.Add(rogue);
        Ship player = new(origin + new Vector3(500f, 0f, 0f));
        traffic.Update(Frame(0f), player, NewReputation());
        return police.FactionCombatTarget == rogue &&
            traffic.CombatCommunication.QueuedMessageCount > 0 &&
            traffic.TryDequeueCombatCommunication(out FactionCombatCommunicationRequest request) &&
            request.Type == FactionCombatCommunicationType.HostileContact;
    }

    private static FactionDistressResponseResult DistressResult(bool waveSpawned, string encounterId, string factionId) =>
        new(
            Accepted: true,
            WaveSpawned: waveSpawned,
            CooldownBlocked: !waveSpawned,
            AssistedShipCount: 0,
            SpawnedShipCount: waveSpawned ? 1 : 0,
            EncounterId: encounterId,
            FactionId: factionId,
            Reason: waveSpawned ? "reinforcements inbound" : "response unavailable");

    private static FactionCombatEscalationResult EscalationResult(bool waveSpawned, string encounterId, string factionId) =>
        new(
            Accepted: true,
            WaveSpawned: waveSpawned,
            CooldownBlocked: !waveSpawned,
            SpawnedShipCount: waveSpawned ? 1 : 0,
            PlayerInvolved: false,
            EncounterId: encounterId,
            FactionId: factionId,
            Reason: waveSpawned ? "heavy reinforcements inbound" : "escalation unavailable");

    private static FactionCombatCommunicationRequest FindRequest(
        FactionCombatCommunicationService service,
        FactionCombatCommunicationType type) =>
        service.GetPendingSnapshot().First(request => request.Type == type);

    private static int CountType(
        FactionCombatCommunicationService service,
        FactionCombatCommunicationType type) =>
        service.GetPendingSnapshot().Count(request => request.Type == type);

    private static NpcShip CreateNpc(string name, string factionId, Vector3 position) =>
        new(name, position, position, 100f, 100f, factionId);

    private static ReputationManager NewReputation()
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, 0f, "Phase 36 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0f, "Phase 36 smoke setup");
        return reputation;
    }

    private static GameTime Frame(float seconds) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static T RunSilenced<T>(Func<T> action)
    {
        System.IO.TextWriter previous = Console.Out;
        using System.IO.StringWriter sink = new();
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
        System.IO.TextWriter previous = Console.Out;
        using System.IO.StringWriter sink = new();
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

    private static bool RunSilenced(Func<bool> assertion) => RunSilenced<bool>(assertion);
}
