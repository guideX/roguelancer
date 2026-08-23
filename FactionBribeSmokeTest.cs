#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Headless Phase 25 acceptance coverage. Offers are deliberately regenerated
/// from station/contact context so this suite exercises the same seams used by
/// the bar UI without requiring a graphics window.
/// </summary>
internal sealed class FactionBribeSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("lawful and criminal contact coverage", ContactCoverage);
        Check("3D bar roles resolve reusable bribe contacts", StationRoleProfiles);
        Check("damaged lawful standing creates a valid offer", ValidLawfulOffer);
        Check("damaged criminal standing creates a valid offer", ValidCriminalOffer);
        Check("offer exposes target, current, result, price, and affordability", OfferPayloadIsComplete);
        Check("irrelevant faction is not offered at an inappropriate station", IrrelevantFactionIsUnavailable);
        Check("standing at the ceiling has no valid offer", CeilingRejectsOffer);
        Check("deep hostility is refused", DeepHostilityRejectsOffer);
        Check("more severe damage costs more", SevereDamageCostsMore);
        Check("same quote state has deterministic pricing", CostIsDeterministic);
        Check("successful purchase charges once and repairs once", SuccessfulPurchaseIsAtomic);
        Check("purchase cannot directly buy Friendly or Allied", PurchaseStopsAtNeutralCeiling);
        Check("insufficient credits cause no mutation", InsufficientCreditsAreSafe);
        Check("stale or repeated purchase cannot double-charge", RepeatedPurchaseIsRejected);
        Check("mission unlocks immediately when bribe crosses threshold", MissionUnlocksAfterBribe);
        Check("mission remains locked below its threshold", MissionRemainsLockedBelowThreshold);
        Check("temporary hostility survives a bribe", BribeDoesNotClearTemporaryHostility);
        Check("new-game reset clears bribe effects and hostility", ResetRestoresNewGameState);
        Check("saved post-bribe standing prevents quote duplication", SavedStandingInvalidatesQuote);
        Check("older schema saves remain loadable without bribe state", OlderSaveStillLoads);

        Console.WriteLine($"[FACTION BRIBE SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (assertion())
            {
                _passed++;
                Console.WriteLine($"[FACTION BRIBE SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION BRIBE SMOKE] FAIL {label}: {reason}");
    }

    private static bool ContactCoverage()
    {
        BarNpc[] contacts = BarNpc.GenerateBarNpcs().ToArray();
        BarNpc lawful = contacts.Single(contact => contact.Name == "Elena Vasquez");
        BarNpc criminal = contacts.Single(contact => contact.Name.StartsWith("Zara", StringComparison.Ordinal));
        return lawful.ReputationBribeTargetFactionIds.Contains(FactionManager.LibertyPolice) &&
            lawful.ReputationBribeTargetFactionIds.Contains(FactionManager.LibertyCorporations) &&
            criminal.ReputationBribeTargetFactionIds.Contains(FactionManager.LibertyRogues);
    }

    private static bool StationRoleProfiles()
    {
        BarNpc? bartender = StationBarSocial.GetReputationContactProfile("bartender");
        BarNpc? smuggler = StationBarSocial.GetReputationContactProfile("smuggler");
        return bartender?.Name == "Elena Vasquez" &&
            bartender.ReputationBribeTargetFactionIds.Contains(FactionManager.LibertyPolice) &&
            smuggler?.Name.StartsWith("Zara", StringComparison.Ordinal) == true &&
            smuggler.ReputationBribeTargetFactionIds.Contains(FactionManager.LibertyRogues);
    }

    private static bool ValidLawfulOffer()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(
            Contact("Elena Vasquez"),
            FactionManager.LibertyPolice,
            reputation,
            credits);
        return offer?.IsValid == true && offer.TargetFactionId == FactionManager.LibertyPolice;
    }

    private static bool ValidCriminalOffer()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyRogues, -0.40f);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(
            Contact("Zara"),
            FactionManager.LibertyRogues,
            reputation,
            credits);
        return offer?.IsValid == true && offer.TargetFactionId == FactionManager.LibertyRogues;
    }

    private static bool OfferPayloadIsComplete()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.32f);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(
            Contact("Elena Vasquez"),
            FactionManager.LibertyPolice,
            reputation,
            new PlayerCredits(100_000));
        return offer != null &&
            offer.TargetFactionId == FactionManager.LibertyPolice &&
            Nearly(offer.CurrentReputation, -0.32f) &&
            Nearly(offer.ResultingReputation, FactionBribeService.BriberyCeiling) &&
            offer.CreditCost > 0 && offer.CanAfford && offer.IsValid &&
            offer.BuildDialogueLine().Contains("Liberty Police", StringComparison.Ordinal) &&
            offer.BuildDialogueLine().Contains("-0.32", StringComparison.Ordinal) &&
            offer.BuildDialogueLine().Contains("+0.10", StringComparison.Ordinal);
    }

    private static bool IrrelevantFactionIsUnavailable()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyCorporations, -0.30f);
        return !FactionBribeService.IsTargetContextuallyAvailable(
                FactionManager.LibertyCorporations,
                FactionManager.LibertyCorporations,
                FactionManager.LibertyRogues,
                reputation.FactionManager) &&
            FactionBribeService.GetOfferForContact(
                Contact("Elena Vasquez"),
                FactionManager.LibertyRogues,
                reputation,
                new PlayerCredits(100_000)) == null;
    }

    private static bool CeilingRejectsOffer()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, FactionBribeService.BriberyCeiling);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(
            Contact("Elena Vasquez"),
            FactionManager.LibertyPolice,
            reputation,
            new PlayerCredits(100_000));
        return offer != null && !offer.IsValid && offer.UnavailableReason.Contains("ceiling", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DeepHostilityRejectsOffer()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.90f);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(
            Contact("Elena Vasquez"),
            FactionManager.LibertyPolice,
            reputation,
            new PlayerCredits(100_000));
        return offer != null && !offer.IsValid && offer.UnavailableReason.Contains("beyond", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SevereDamageCostsMore()
    {
        int mild = FactionBribeService.CalculateCost(FactionManager.LibertyPolice, FactionManager.LibertyCorporations, -0.20f, new FactionManager());
        int severe = FactionBribeService.CalculateCost(FactionManager.LibertyPolice, FactionManager.LibertyCorporations, -0.70f, new FactionManager());
        return severe > mild && mild >= FactionBribeService.BaseCost;
    }

    private static bool CostIsDeterministic()
    {
        FactionManager factions = new();
        int first = FactionBribeService.CalculateCost(FactionManager.LibertyPolice, FactionManager.LibertyCorporations, -0.32f, factions);
        int second = FactionBribeService.CalculateCost(FactionManager.LibertyPolice, FactionManager.LibertyCorporations, -0.32f, factions);
        return first == second;
    }

    private static bool SuccessfulPurchaseIsAtomic()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyPolice, reputation, credits);
        if (offer == null || !offer.IsValid)
            return false;

        int beforeCredits = credits.Credits;
        float beforeStanding = reputation.GetStanding(FactionManager.LibertyPolice);
        if (!FactionBribeService.TryPurchase(offer, reputation, credits, out ReputationChangeResult? change, out _))
            return false;

        return change != null &&
            credits.Credits == beforeCredits - offer.CreditCost &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionBribeService.BriberyCeiling) &&
            Nearly(change.NewValue - beforeStanding, FactionBribeService.BriberyCeiling - beforeStanding);
    }

    private static bool PurchaseStopsAtNeutralCeiling()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.80f);
        PlayerCredits credits = new(200_000);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyPolice, reputation, credits);
        if (offer == null || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionBribeService.BriberyCeiling) &&
            !reputation.IsFriendly(FactionManager.LibertyPolice) &&
            !reputation.IsAllied(FactionManager.LibertyPolice);
    }

    private static bool InsufficientCreditsAreSafe()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        PlayerCredits credits = new(1);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyPolice, reputation, credits);
        float beforeStanding = reputation.GetStanding(FactionManager.LibertyPolice);
        return offer != null && !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out string failure) &&
            credits.Credits == 1 && Nearly(reputation.GetStanding(FactionManager.LibertyPolice), beforeStanding) &&
            failure.Contains("insufficient", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RepeatedPurchaseIsRejected()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyPolice, reputation, credits);
        if (offer == null || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;

        int afterFirstPurchase = credits.Credits;
        float afterFirstStanding = reputation.GetStanding(FactionManager.LibertyPolice);
        bool secondPurchase = FactionBribeService.TryPurchase(offer, reputation, credits, out _, out string failure);
        return !secondPurchase && credits.Credits == afterFirstPurchase &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), afterFirstStanding) &&
            failure.Contains("ceiling", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MissionUnlocksAfterBribe()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyCorporations, -0.15f);
        PlayerCredits credits = new(100_000);
        MissionManager missions = new(credits, null!, reputation);
        Mission mission = CreateMission(0.10f, FactionManager.LibertyCorporations);
        if (missions.GetMissionEligibility(mission).IsEligible)
            return false;

        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyCorporations, reputation, credits);
        return offer != null && FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _) &&
            missions.GetMissionEligibility(mission).IsEligible &&
            Nearly(missions.GetMissionEligibility(mission).CurrentStanding, FactionBribeService.BriberyCeiling);
    }

    private static bool MissionRemainsLockedBelowThreshold()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyCorporations, -0.15f);
        PlayerCredits credits = new(100_000);
        MissionManager missions = new(credits, null!, reputation);
        Mission mission = CreateMission(0.20f, FactionManager.LibertyCorporations);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyCorporations, reputation, credits);
        return offer != null && FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _) &&
            !missions.GetMissionEligibility(mission).IsEligible &&
            missions.GetMissionEligibility(mission).Reason.Contains("REPUTATION TOO LOW", StringComparison.Ordinal);
    }

    private static bool BribeDoesNotClearTemporaryHostility()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyPolice, reputation, credits);
        if (offer == null || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;

        bool remainedActive = reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        return remainedActive && !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool ResetRestoresNewGameState()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyPolice, reputation, credits);
        if (offer == null || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;
        reputation.ResetToNewGame();
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f) &&
            !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool OlderSaveStillLoads()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguelancer-phase25-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameManager manager = new(path);
            SaveGameData oldSave = new()
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion - 1,
                PlayerCredits = 4_200,
                FactionReputation = new()
                {
                    new SaveFactionReputationData { FactionId = FactionManager.LibertyPolice, Standing = -0.31f }
                }
            };
            // TrySave intentionally upgrades writes to the current schema, so
            // write this fixture directly to exercise the older-save loader.
            File.WriteAllText(path, JsonSerializer.Serialize(oldSave));
            return manager.TryLoad(out SaveGameData? loaded, out _) &&
                loaded?.SchemaVersion == SaveGameData.CurrentSchemaVersion - 1 &&
                loaded.PlayerCredits == 4_200;
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static bool SavedStandingInvalidatesQuote()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguelancer-phase25-quote-{Guid.NewGuid():N}.json");
        try
        {
            ReputationManager sourceReputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
            PlayerCredits sourceCredits = new(100_000);
            FactionBribeOffer? offer = FactionBribeService.GetOfferForContact(Contact("Elena Vasquez"), FactionManager.LibertyPolice, sourceReputation, sourceCredits);
            if (offer == null || !FactionBribeService.TryPurchase(offer, sourceReputation, sourceCredits, out _, out _))
                return false;

            SaveGameManager manager = new(path);
            SaveGameData data = new()
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                PlayerCredits = sourceCredits.Credits,
                FactionReputation = sourceReputation.GetStandingsSnapshot()
                    .Select(entry => new SaveFactionReputationData { FactionId = entry.Key, Standing = entry.Value })
                    .ToList()
            };
            if (!manager.TrySave(data, out _) || !manager.TryLoad(out SaveGameData? loaded, out _) || loaded == null)
                return false;

            ReputationManager restoredReputation = new(new FactionManager());
            manager.ApplyReputation(restoredReputation, loaded);
            FactionBribeOffer? reopened = FactionBribeService.GetOfferForContact(
                Contact("Elena Vasquez"),
                FactionManager.LibertyPolice,
                restoredReputation,
                new PlayerCredits(loaded.PlayerCredits));
            return reopened != null && !reopened.IsValid;
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static BarNpc Contact(string name)
    {
        return BarNpc.GenerateBarNpcs().First(contact => contact.Name.StartsWith(name, StringComparison.Ordinal));
    }

    private static ReputationManager NewReputation(string factionId, float standing)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(factionId, standing, "phase 25 smoke setup");
        return reputation;
    }

    private static Mission CreateMission(float minimum, string factionId)
    {
        return new Mission(
            MissionType.Delivery,
            MissionDifficulty.Easy,
            "Medical Supplies",
            "Destination",
            1_000,
            0f,
            "Deliver medical supplies.",
            factionId)
        {
            MinimumEmployerReputation = minimum
        };
    }

    private static bool Nearly(float left, float right) => Math.Abs(left - right) < ReputationManager.Precision;
}
