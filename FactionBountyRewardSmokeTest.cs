#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace Roguelancer;

/// <summary>
/// Focused Phase 37 proof. The helper destroys NPCs through the same
/// NpcShip.ApplyDamage attribution seam used by live weapon systems; direct
/// Hull.TakeDamage calls represent unknown/environmental destruction.
/// </summary>
internal sealed class FactionBountyRewardSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("explicit Police-to-Rogue policy", ExplicitPolicy);
        Check("standard Rogue payout is deterministic", StandardPayout);
        Check("Warthog heavy payout is higher", HeavyPayout);
        Check("ordinary Rogue traffic is eligible", OrdinaryTrafficIsEligible);
        Check("distress Rogue reinforcement is eligible", DistressReinforcementIsEligible);
        Check("escalation heavy responder is eligible", EscalationResponderIsEligible);
        Check("reinforcement provenance does not multiply payout", ReinforcementDoesNotMultiplyPayout);
        Check("eligible destruction pays once", EligibleDestructionPaysOnce);
        Check("duplicate callback pays zero", DuplicateCallbackPaysZero);
        Check("duplicate callback has no second notification", DuplicateCallbackHasNoSecondNotification);
        Check("NPC Police kill pays zero", NpcPoliceKillPaysZero);
        Check("NPC Rogue kill of Police pays zero", NpcRogueKillPaysZero);
        Check("NPC-only combat remains credit and reputation isolated", NpcOnlyCombatIsIsolated);
        Check("player damage with NPC final blow pays zero", PlayerDamageNpcFinalBlowPaysZero);
        Check("NPC damage with player final blow pays full payout", PlayerFinalBlowPaysFullPayout);
        Check("environmental destruction pays zero", EnvironmentalDestructionPaysZero);
        Check("unknown-source destruction pays zero", UnknownSourcePaysZero);
        Check("invalid victim pays zero", InvalidVictimPaysZero);
        Check("already-dead victim cannot pay", AlreadyDeadVictimCannotPay);
        Check("Police destruction has no Police bounty", PoliceDestructionHasNoBounty);
        Check("neutral destruction has no Police bounty", NeutralDestructionHasNoBounty);
        Check("unknown faction has no bounty", UnknownFactionHasNoBounty);
        Check("same-faction target has no bounty", SameFactionHasNoBounty);
        Check("temporary sponsor hostility blocks payout", TemporaryHostilityBlocksPayout);
        Check("expired temporary hostility permits future kill", ExpiredTemporaryHostilityPermitsFutureKill);
        Check("permanent sponsor hostility blocks payout", PermanentHostilityBlocksPayout);
        Check("non-hostile player receives payout", NonHostilePlayerReceivesPayout);
        Check("Phase 30 Police-kill consequence remains", Phase30PoliceKillConsequenceRemains);
        Check("Phase 31 Rogue ripple remains exactly once", Phase31RogueRippleRemainsExactlyOnce);
        Check("bounty adds no reputation mutation", BountyAddsNoReputationMutation);
        Check("credit mutation uses PlayerCredits authority", CreditMutationUsesAuthority);
        Check("failed eligibility does not mutate credits", FailedEligibilityDoesNotMutateCredits);
        Check("overflow cannot create a wrapped reward", OverflowCannotCreateReward);
        Check("notification occurs after successful credit award", NotificationOccursAfterCreditAward);
        Check("notification contains sponsor and amount", NotificationContainsSponsorAndAmount);
        Check("distress event itself gives no money", DistressEventItselfGivesNoMoney);
        Check("escalation event itself gives no money", EscalationEventItselfGivesNoMoney);
        Check("disengagement event itself gives no money", DisengagementEventItselfGivesNoMoney);
        Check("radio communication remains presentation-only", RadioCommunicationRemainsPresentationOnly);
        Check("save schema remains version 10", SaveSchemaRemainsVersionTen);
        Check("reset clears duplicate bookkeeping", ResetClearsDuplicateBookkeeping);
        Check("new NPC after reset is independent", NewNpcAfterResetIsIndependent);
        Check("duplicate state is bounded", DuplicateStateIsBounded);
        Check("destruction result carries deterministic timestamp", ResultCarriesTimestamp);

        Console.WriteLine($"[FACTION BOUNTY REWARD SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION BOUNTY REWARD SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION BOUNTY REWARD SMOKE] FAIL {label}: {reason}");
    }

    private static bool ExplicitPolicy()
    {
        Fixture fixture = NewFixture();
        return fixture.Service.BountyPolicies.Count == 1 &&
            fixture.Service.BountyPolicies[0].SponsorFactionId == FactionManager.LibertyPolice &&
            fixture.Service.BountyPolicies[0].TargetFactionId == FactionManager.LibertyRogues &&
            fixture.Service.BountyPolicies[0].StandardPayout == 250 &&
            fixture.Service.BountyPolicies[0].HeavyPayout == 500;
    }

    private static bool StandardPayout()
    {
        Fixture fixture = NewFixture();
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Rogue Fighter");
        return result.IsEligible && result.Tier == FactionBountyTier.Standard &&
            result.AmountAwarded == FactionBountyRewardService.StandardRogueBounty &&
            fixture.Credits.Credits == 250;
    }

    private static bool HeavyPayout()
    {
        Fixture fixture = NewFixture();
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Warthog Heavy Fighter");
        return result.IsEligible && result.Tier == FactionBountyTier.Heavy &&
            result.AmountAwarded == FactionBountyRewardService.HeavyRogueBounty &&
            fixture.Credits.Credits == 500;
    }

    private static bool OrdinaryTrafficIsEligible()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Traffic Fighter");
        rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "ordinary-rogue", Vector3.Zero, 500f, 100f);
        FactionBountyRewardResult result = fixture.DestroyByPlayer(rogue);
        return result.IsEligible && result.AmountAwarded == 250;
    }

    private static bool DistressReinforcementIsEligible()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Distress Fighter");
        rogue.MarkDistressReinforcement("smoke-distress");
        FactionBountyRewardResult result = fixture.DestroyByPlayer(rogue);
        return result.IsEligible && result.AmountAwarded == 250 && fixture.Credits.Credits == 250;
    }

    private static bool EscalationResponderIsEligible()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Warthog Heavy Fighter");
        rogue.MarkEscalationReinforcement("smoke-escalation");
        FactionBountyRewardResult result = fixture.DestroyByPlayer(rogue);
        return result.IsEligible && result.AmountAwarded == 500;
    }

    private static bool ReinforcementDoesNotMultiplyPayout()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Warthog Heavy Fighter");
        rogue.MarkDistressReinforcement("smoke-distress");
        rogue.MarkEscalationReinforcement("smoke-escalation");
        fixture.DestroyByPlayer(rogue);
        return fixture.Credits.Credits == 500;
    }

    private static bool EligibleDestructionPaysOnce()
    {
        Fixture fixture = NewFixture();
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Rogue Fighter");
        return result.AmountAwarded == 250 && fixture.Credits.Credits == 250 &&
            fixture.Service.RememberedDestructionCount == 1;
    }

    private static bool DuplicateCallbackPaysZero()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        fixture.DestroyByPlayer(rogue);
        FactionBountyRewardResult duplicate = fixture.Service.ProcessDestruction(rogue, 2d);
        return duplicate.RejectionReason == FactionBountyRejectionReason.DuplicateKill &&
            duplicate.AmountAwarded == 0 && fixture.Credits.Credits == 250;
    }

    private static bool DuplicateCallbackHasNoSecondNotification()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        fixture.DestroyByPlayer(rogue);
        fixture.Service.ProcessDestruction(rogue);
        return fixture.Notifications.Count == 1;
    }

    private static bool NpcPoliceKillPaysZero()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.ApplyDamage(rogue.Hull.CurrentHull, NpcDestructionSource.Npc);
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(rogue);
        return result.RejectionReason == FactionBountyRejectionReason.NotPlayerAttributed &&
            fixture.Credits.Credits == 0;
    }

    private static bool NpcRogueKillPaysZero()
    {
        Fixture fixture = NewFixture();
        NpcShip police = fixture.CreateNpc("Police Fighter", FactionManager.LibertyPolice);
        police.ApplyDamage(police.Hull.CurrentHull, NpcDestructionSource.Npc);
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(police);
        return !result.IsEligible && result.AmountAwarded == 0 && fixture.Credits.Credits == 0;
    }

    private static bool NpcOnlyCombatIsIsolated()
    {
        Fixture fixture = NewFixture();
        float policeBefore = fixture.Reputation.GetStanding(FactionManager.LibertyPolice);
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.ApplyDamage(rogue.Hull.CurrentHull, NpcDestructionSource.Npc);
        bool consequence = fixture.Reputation.ApplyPlayerShipDestroyed(rogue) != null;
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(rogue);
        return !consequence && !result.IsEligible && fixture.Credits.Credits == 0 &&
            Nearly(fixture.Reputation.GetStanding(FactionManager.LibertyPolice), policeBefore);
    }

    private static bool PlayerDamageNpcFinalBlowPaysZero()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.MarkDamagedByPlayer(10f);
        rogue.ApplyDamage(rogue.Hull.CurrentHull, NpcDestructionSource.Npc);
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(rogue);
        return result.RejectionReason == FactionBountyRejectionReason.NotPlayerAttributed &&
            fixture.Credits.Credits == 0;
    }

    private static bool PlayerFinalBlowPaysFullPayout()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.ApplyDamage(10f, NpcDestructionSource.Npc);
        rogue.MarkDamagedByPlayer(10f);
        rogue.ApplyDamage(rogue.Hull.CurrentHull, NpcDestructionSource.Player);
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(rogue);
        return result.IsEligible && result.AmountAwarded == 250 && fixture.Credits.Credits == 250;
    }

    private static bool EnvironmentalDestructionPaysZero()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.Hull.TakeDamage(rogue.Hull.CurrentHull);
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(rogue);
        return !result.IsEligible && result.AmountAwarded == 0 && fixture.Credits.Credits == 0;
    }

    private static bool UnknownSourcePaysZero() => EnvironmentalDestructionPaysZero();

    private static bool InvalidVictimPaysZero()
    {
        Fixture fixture = NewFixture();
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(new SpaceObject("asteroid", Vector3.Zero));
        return result.RejectionReason == FactionBountyRejectionReason.InvalidVictim && fixture.Credits.Credits == 0;
    }

    private static bool AlreadyDeadVictimCannotPay()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.Hull.TakeDamage(rogue.Hull.CurrentHull);
        FactionBountyRewardResult first = fixture.Service.ProcessDestruction(rogue);
        FactionBountyRewardResult second = fixture.Service.ProcessDestruction(rogue);
        return !first.IsEligible && second.RejectionReason == FactionBountyRejectionReason.DuplicateKill &&
            fixture.Credits.Credits == 0;
    }

    private static bool PoliceDestructionHasNoBounty()
    {
        Fixture fixture = NewFixture();
        return !fixture.DestroyByPlayer("Police Fighter", FactionManager.LibertyPolice).IsEligible &&
            fixture.Credits.Credits == 0;
    }

    private static bool NeutralDestructionHasNoBounty()
    {
        Fixture fixture = NewFixture();
        return !fixture.DestroyByPlayer("Neutral Fighter", FactionManager.NeutralCivilians).IsEligible &&
            fixture.Credits.Credits == 0;
    }

    private static bool UnknownFactionHasNoBounty()
    {
        Fixture fixture = NewFixture();
        return fixture.DestroyByPlayer("Unknown Fighter", "unknown_faction").RejectionReason == FactionBountyRejectionReason.InvalidFaction &&
            fixture.Credits.Credits == 0;
    }

    private static bool SameFactionHasNoBounty() => PoliceDestructionHasNoBounty();

    private static bool TemporaryHostilityBlocksPayout()
    {
        Fixture fixture = NewFixture();
        fixture.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Rogue Fighter");
        return result.RejectionReason == FactionBountyRejectionReason.SponsorHostileToPlayer && fixture.Credits.Credits == 0;
    }

    private static bool ExpiredTemporaryHostilityPermitsFutureKill()
    {
        Fixture fixture = NewFixture();
        fixture.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        fixture.Reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Rogue Fighter");
        return !fixture.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) && result.AmountAwarded == 250;
    }

    private static bool PermanentHostilityBlocksPayout()
    {
        Fixture fixture = NewFixture(-0.60f);
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Rogue Fighter");
        return result.RejectionReason == FactionBountyRejectionReason.SponsorHostileToPlayer && fixture.Credits.Credits == 0;
    }

    private static bool NonHostilePlayerReceivesPayout()
    {
        Fixture fixture = NewFixture(0f);
        return fixture.DestroyByPlayer("Rogue Fighter").AmountAwarded == 250;
    }

    private static bool Phase30PoliceKillConsequenceRemains()
    {
        Fixture fixture = NewFixture(0f);
        NpcShip police = fixture.CreateNpc("Police Fighter", FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(10f);
        police.ApplyDamage(police.Hull.CurrentHull, NpcDestructionSource.Player);
        ReputationChangeResult? consequence = fixture.Reputation.ApplyPlayerShipDestroyed(police);
        return consequence != null && Nearly(fixture.Reputation.GetStanding(FactionManager.LibertyPolice), -0.15f) &&
            fixture.Credits.Credits == 0;
    }

    private static bool Phase31RogueRippleRemainsExactlyOnce()
    {
        Fixture fixture = NewFixture(0f);
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.MarkDamagedByPlayer(10f);
        rogue.ApplyDamage(rogue.Hull.CurrentHull, NpcDestructionSource.Player);
        fixture.Reputation.ApplyPlayerShipDestroyed(rogue);
        float policeAfterConsequences = fixture.Reputation.GetStanding(FactionManager.LibertyPolice);
        FactionBountyRewardResult result = fixture.Service.ProcessDestruction(rogue);
        return result.AmountAwarded == 250 && Nearly(policeAfterConsequences, 0.04f) &&
            Nearly(fixture.Reputation.GetStanding(FactionManager.LibertyPolice), policeAfterConsequences);
    }

    private static bool BountyAddsNoReputationMutation()
    {
        Fixture fixture = NewFixture(0f);
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.MarkDamagedByPlayer(10f);
        rogue.ApplyDamage(rogue.Hull.CurrentHull, NpcDestructionSource.Player);
        IReadOnlyDictionary<string, float> before = fixture.Reputation.GetStandingsSnapshot();
        fixture.Service.ProcessDestruction(rogue);
        IReadOnlyDictionary<string, float> after = fixture.Reputation.GetStandingsSnapshot();
        return Nearly(before[FactionManager.LibertyPolice], after[FactionManager.LibertyPolice]) &&
            Nearly(before[FactionManager.LibertyRogues], after[FactionManager.LibertyRogues]);
    }

    private static bool CreditMutationUsesAuthority()
    {
        Fixture fixture = NewFixture();
        int changes = 0;
        fixture.Credits.OnCreditsChanged += _ => changes++;
        fixture.DestroyByPlayer("Rogue Fighter");
        return changes == 1 && fixture.Credits.Credits == 250;
    }

    private static bool FailedEligibilityDoesNotMutateCredits()
    {
        Fixture fixture = NewFixture(-0.60f);
        int changes = 0;
        fixture.Credits.OnCreditsChanged += _ => changes++;
        fixture.DestroyByPlayer("Rogue Fighter");
        return changes == 0 && fixture.Credits.Credits == 0;
    }

    private static bool OverflowCannotCreateReward()
    {
        Fixture fixture = NewFixture(startingCredits: int.MaxValue);
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Rogue Fighter");
        return result.RejectionReason == FactionBountyRejectionReason.CreditMutationFailed &&
            fixture.Credits.Credits == int.MaxValue && fixture.Notifications.Count == 0;
    }

    private static bool NotificationOccursAfterCreditAward()
    {
        ReputationManager reputation = NewReputation(0f);
        PlayerCredits credits = new(0);
        bool sawPaidBalance = false;
        FactionBountyRewardService service = new(reputation, credits, _ => sawPaidBalance = credits.Credits == 250);
        NpcShip rogue = new("Rogue Fighter", Vector3.Zero, Vector3.Zero, 100f, 100f, FactionManager.LibertyRogues);
        rogue.MarkDamagedByPlayer(10f);
        rogue.ApplyDamage(rogue.Hull.CurrentHull, NpcDestructionSource.Player);
        service.ProcessDestruction(rogue);
        return sawPaidBalance;
    }

    private static bool NotificationContainsSponsorAndAmount()
    {
        Fixture fixture = NewFixture();
        fixture.DestroyByPlayer("Rogue Fighter");
        return fixture.Notifications.Count == 1 &&
            fixture.Notifications[0].Contains("Liberty Police", StringComparison.Ordinal) &&
            fixture.Notifications[0].Contains("+250 credits", StringComparison.Ordinal);
    }

    private static bool DistressEventItselfGivesNoMoney()
    {
        Fixture fixture = NewFixture();
        List<NpcShip> ships = new();
        FactionDistressResponseService distress = new(
            ships,
            fixture.Reputation,
            (_, _, _, _) => Array.Empty<NpcShip>());
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        rogue.MarkDamagedByPlayer(5f);
        distress.ProcessPlayerDamage(rogue);
        return fixture.Credits.Credits == 0;
    }

    private static bool EscalationEventItselfGivesNoMoney()
    {
        Fixture fixture = NewFixture();
        List<NpcShip> ships = new();
        FactionDistressResponseService distress = new(ships, fixture.Reputation, (_, _, _, _) => Array.Empty<NpcShip>());
        FactionCombatEscalationService escalation = new(ships, distress, fixture.Reputation, (_, _, _, _) => Array.Empty<NpcShip>());
        escalation.Update(1f);
        return fixture.Credits.Credits == 0;
    }

    private static bool DisengagementEventItselfGivesNoMoney()
    {
        Fixture fixture = NewFixture();
        List<NpcShip> ships = new();
        FactionCombatDisengagementService disengagement = new(ships, fixture.Reputation);
        disengagement.Update(1f);
        return fixture.Credits.Credits == 0;
    }

    private static bool RadioCommunicationRemainsPresentationOnly()
    {
        Fixture fixture = NewFixture();
        List<NpcShip> ships = new();
        FactionCombatCommunicationService radio = new(ships);
        radio.Update(1f, null);
        return fixture.Credits.Credits == 0 && fixture.Notifications.Count == 0;
    }

    private static bool SaveSchemaRemainsVersionTen() => SaveGameData.CurrentSchemaVersion == 10;

    private static bool ResetClearsDuplicateBookkeeping()
    {
        Fixture fixture = NewFixture();
        NpcShip rogue = fixture.CreateRogue("Rogue Fighter");
        fixture.DestroyByPlayer(rogue);
        fixture.Service.Reset();
        return fixture.Service.RememberedDestructionCount == 0 && fixture.Credits.Credits == 250;
    }

    private static bool NewNpcAfterResetIsIndependent()
    {
        Fixture fixture = NewFixture();
        fixture.DestroyByPlayer("Rogue Fighter");
        fixture.Service.Reset();
        fixture.DestroyByPlayer("Rogue Fighter 2");
        return fixture.Credits.Credits == 500 && fixture.Notifications.Count == 2;
    }

    private static bool DuplicateStateIsBounded()
    {
        Fixture fixture = NewFixture();
        for (int i = 0; i < FactionBountyRewardService.MaximumRememberedDestructions + 20; i++)
        {
            NpcShip rogue = fixture.CreateRogue($"Rogue {i}");
            rogue.Hull.TakeDamage(rogue.Hull.CurrentHull);
            fixture.Service.ProcessDestruction(rogue);
        }

        return fixture.Service.RememberedDestructionCount <= FactionBountyRewardService.MaximumRememberedDestructions;
    }

    private static bool ResultCarriesTimestamp()
    {
        Fixture fixture = NewFixture();
        FactionBountyRewardResult result = fixture.DestroyByPlayer("Rogue Fighter", timestamp: 42.5d);
        return result.IsEligible && Math.Abs(result.SimulationTimestampSeconds - 42.5d) < 0.0001d;
    }

    private static Fixture NewFixture(float policeStanding = 0f, int startingCredits = 0) =>
        new(policeStanding, startingCredits);

    private static ReputationManager NewReputation(float policeStanding)
    {
        FactionManager factions = new();
        ReputationManager reputation = new(factions);
        reputation.SetReputation(FactionManager.LibertyPolice, policeStanding);
        reputation.SetReputation(FactionManager.LibertyRogues, 0f);
        return reputation;
    }

    private static bool Nearly(float actual, float expected) => Math.Abs(actual - expected) < 0.0001f;

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter original = Console.Out;
        using StringWriter capture = new();
        Console.SetOut(capture);
        try
        {
            return action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private sealed class Fixture
    {
        public ReputationManager Reputation { get; }
        public PlayerCredits Credits { get; }
        public List<string> Notifications { get; } = new();
        public FactionBountyRewardService Service { get; }

        public Fixture(float policeStanding, int startingCredits)
        {
            Reputation = NewReputation(policeStanding);
            Credits = new PlayerCredits(startingCredits);
            Service = new FactionBountyRewardService(Reputation, Credits, Notifications.Add);
        }

        public NpcShip CreateRogue(string name) => CreateNpc(name, FactionManager.LibertyRogues);

        public NpcShip CreateNpc(string name, string faction)
        {
            return new NpcShip(name, Vector3.Zero, Vector3.Zero, 100f, 100f, faction);
        }

        public FactionBountyRewardResult DestroyByPlayer(
            string name,
            string faction = FactionManager.LibertyRogues,
            double timestamp = 1d)
        {
            return DestroyByPlayer(CreateNpc(name, faction), timestamp);
        }

        public FactionBountyRewardResult DestroyByPlayer(NpcShip ship, double timestamp = 1d)
        {
            ship.MarkDamagedByPlayer(10f);
            ship.ApplyDamage(ship.Hull.CurrentHull, NpcDestructionSource.Player);
            return Service.ProcessDestruction(ship, timestamp);
        }
    }
}
