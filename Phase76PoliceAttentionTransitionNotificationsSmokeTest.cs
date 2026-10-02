#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 76 focused coverage for bounded Liberty Police attention transition
/// feedback. The harness drives only production systems: the durable standing
/// authority (ReputationManager), the single Phase 74 tier policy
/// (PoliceEnforcementEscalationPolicy), the pure Phase 75 projection
/// (PoliceAttentionPresentation / ReputationPresentation), the authoritative
/// contraband enforcement path (PoliceEnforcementService), social recovery
/// (FactionBribeService), the scan/stop lifecycle (PoliceScanSystem), the
/// single pursuit owner (PoliceFugitiveManager), and SaveGameManager
/// persistence. No parallel crime, notoriety, warrant, heat, criminal
/// history, or persisted transition state exists here.
/// </summary>
internal sealed class Phase76PoliceAttentionTransitionNotificationsSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(ValidateNewGameInitializesStandardSilently, "new game initializes standard silently", ref passed, ref failed);
        RunCase(ValidateLoadingStandardInitializesSilently, "loading standard initializes silently", ref passed, ref failed);
        RunCase(ValidateLoadingElevatedInitializesSilently, "loading elevated initializes silently", ref passed, ref failed);
        RunCase(ValidateLoadingSevereInitializesSilently, "loading severe initializes silently", ref passed, ref failed);
        RunCase(ValidateStandardToElevatedNotifiesOnce, "standard to elevated emits exactly one notification", ref passed, ref failed);
        RunCase(ValidateElevatedToSevereNotifiesOnce, "elevated to severe emits exactly one notification", ref passed, ref failed);
        RunCase(ValidateSevereToElevatedDeescalatesOnce, "severe to elevated emits exactly one de-escalation", ref passed, ref failed);
        RunCase(ValidateElevatedToStandardRecoversOnce, "elevated to standard emits exactly one recovery", ref passed, ref failed);
        RunCase(ValidateSevereToStandardJumpNotifiesOnce, "severe to standard direct jump emits one not two", ref passed, ref failed);
        RunCase(ValidateStandardToSevereJumpNotifiesFinalState, "standard to severe direct jump emits one final-state notification", ref passed, ref failed);
        RunCase(ValidateSameBandStandardChangeSilent, "same-band standard change emits none", ref passed, ref failed);
        RunCase(ValidateSameBandElevatedChangeSilent, "same-band elevated change emits none", ref passed, ref failed);
        RunCase(ValidateSameBandSevereChangeSilent, "same-band severe change emits none", ref passed, ref failed);
        RunCase(ValidateRepeatedPresentationReadsSilent, "repeated presentation reads emit nothing", ref passed, ref failed);
        RunCase(ValidateDockOverviewSilent, "opening dock reputation overview emits nothing", ref passed, ref failed);
        RunCase(ValidateMissionBoardOverviewSilent, "opening mission-board reputation overview emits nothing", ref passed, ref failed);
        RunCase(ValidateStationNpcDialogueSilent, "station NPC dialogue emits nothing by itself", ref passed, ref failed);
        RunCase(ValidateContrabandComplianceNotifiesOnce, "contraband compliance crossing a boundary notifies once", ref passed, ref failed);
        RunCase(ValidateContrabandRefusalNotifiesOnce, "contraband refusal crossing a boundary notifies once", ref passed, ref failed);
        RunCase(ValidateSmugglingContractNotifiesOnce, "smuggling-contract consequence crossing a boundary notifies once", ref passed, ref failed);
        RunCase(ValidateBriberyRecoveryNotifiesOnce, "bribery recovery severe to standard notifies once", ref passed, ref failed);
        RunCase(ValidateMissionRecoveryNotifiesOnce, "mission reputation recovery crossing a boundary notifies once", ref passed, ref failed);
        RunCase(ValidateSaveLoadDoesNotReplayTransition, "save/load does not replay the previous transition", ref passed, ref failed);
        RunCase(ValidatePostLoadGenuineTransitionNotifies, "post-load genuine transition still notifies", ref passed, ref failed);
        RunCase(ValidateNoPersistedPhase76FieldExists, "no persisted Phase 76 field or schema member exists", ref passed, ref failed);
        RunCase(ValidateNotificationDoesNotMutateReputation, "notification does not mutate reputation", ref passed, ref failed);
        RunCase(ValidateNotificationDoesNotMutateFugitiveHeat, "notification does not mutate fugitive heat", ref passed, ref failed);
        RunCase(ValidateNotificationDoesNotAlterLawfulStop, "notification does not alter scan or lawful-stop state", ref passed, ref failed);
        RunCase(ValidateNotificationDoesNotAlterHostility, "notification does not alter hostility", ref passed, ref failed);
        RunCase(ValidateHostileTransitionNoDuplicate, "hostile transition produces no duplicate feedback", ref passed, ref failed);
        RunCase(ValidateHostileRecoveryAtMostOneFeedback, "hostile to lawful recovery produces at most one feedback event", ref passed, ref failed);
        RunCase(ValidateResetDoesNotInventTransition, "reset/death lifecycle does not invent a transition", ref passed, ref failed);
        RunCase(ValidateDockUndockDoesNotInventTransition, "dock/undock does not invent a transition", ref passed, ref failed);
        RunCase(ValidateSystemTransitionDoesNotInventTransition, "system transition does not invent a transition", ref passed, ref failed);
        RunCase(ValidateRepeatedSameLevelUpdatesSilent, "repeated same-level updates remain silent and bounded", ref passed, ref failed);
        RunCase(ValidateExactElevatedBoundary, "exact -0.35 boundary crossing notifies once", ref passed, ref failed);
        RunCase(ValidateExactSevereBoundary, "exact -0.50 boundary crossing notifies once", ref passed, ref failed);
        RunCase(ValidateHostileBoundarySuppressed, "exact -0.60 boundary is deferred to existing hostility feedback", ref passed, ref failed);
        RunCase(ValidateHostileRecoveryBoundarySuppressed, "hostile to lawful boundary is deferred to existing feedback", ref passed, ref failed);
        RunCase(ValidateNonPoliceFactionSilent, "non-Liberty-Police reputation changes stay silent", ref passed, ref failed);

        Console.WriteLine($"[PHASE 76 SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string failureReason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 76 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 76 SMOKE] FAIL {label}: {failureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 76 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidateNewGameInitializesStandardSilently()
    {
        AttentionObserver observer = new(new ReputationManager(new FactionManager()));
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("new game did not read as standard");
        if (observer.Notifications.Count != 0)
            return Fail($"new game emitted {observer.Notifications.Count} notification(s)");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateLoadingStandardInitializesSilently()
        => ValidateLoadingInitializesSilently(-0.25f, LibertyPoliceAttentionLevel.Standard);

    private (bool Success, string FailureReason) ValidateLoadingElevatedInitializesSilently()
        => ValidateLoadingInitializesSilently(-0.45f, LibertyPoliceAttentionLevel.Elevated);

    private (bool Success, string FailureReason) ValidateLoadingSevereInitializesSilently()
        => ValidateLoadingInitializesSilently(-0.55f, LibertyPoliceAttentionLevel.Severe);

    private (bool Success, string FailureReason) ValidateLoadingInitializesSilently(float standing, LibertyPoliceAttentionLevel expected)
    {
        ReputationManager restored = RoundTripReputation(standing);
        AttentionObserver observer = new(restored);
        if (PoliceAttentionPresentation.GetLevel(restored) != expected)
            return Fail($"loaded standing {standing} did not read as {expected}");
        if (observer.Notifications.Count != 0)
            return Fail($"loading emitted {observer.Notifications.Count} notification(s)");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateStandardToElevatedNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.25f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.20f, ReputationChangeReason.PoliceEnforcementRefused);
        if (!Nearly(observer.Reputation.GetStanding(FactionManager.LibertyPolice), -0.45f))
            return Fail("standing did not move to -0.45");
        if (observer.Notifications.Count != 1)
            return Fail($"expected exactly one notification, got {observer.Notifications.Count}");
        if (!observer.Notifications[0].Contains("increased", StringComparison.OrdinalIgnoreCase))
            return Fail("notification did not describe increased attention");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedToSevereNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.45f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.10f, ReputationChangeReason.PoliceEnforcementRefused);
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Severe)
            return Fail("standing did not reach severe");
        if (observer.Notifications.Count != 1)
            return Fail($"expected exactly one notification, got {observer.Notifications.Count}");
        if (!observer.Notifications[0].Contains("scrutiny", StringComparison.OrdinalIgnoreCase))
            return Fail("severe notification did not describe elevated scrutiny");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSevereToElevatedDeescalatesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, 0.10f, ReputationChangeReason.PoliceEnforcementPaid);
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("standing did not return to elevated");
        if (observer.Notifications.Count != 1)
            return Fail($"expected exactly one notification, got {observer.Notifications.Count}");
        if (!observer.Notifications[0].Contains("decreased", StringComparison.OrdinalIgnoreCase))
            return Fail("de-escalation notification did not describe decreased attention");
        if (observer.Notifications[0].Contains("normal", StringComparison.OrdinalIgnoreCase))
            return Fail("de-escalation notification incorrectly claimed full normalization");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedToStandardRecoversOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.45f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, 0.30f, ReputationChangeReason.PoliceEnforcementPaid);
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("standing did not return to standard");
        if (observer.Notifications.Count != 1)
            return Fail($"expected exactly one notification, got {observer.Notifications.Count}");
        if (!observer.Notifications[0].Contains("normal", StringComparison.OrdinalIgnoreCase))
            return Fail("recovery notification did not describe normalization");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSevereToStandardJumpNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, 0.65f, ReputationChangeReason.PoliceEnforcementPaid);
        if (!Nearly(observer.Reputation.GetStanding(FactionManager.LibertyPolice), 0.10f))
            return Fail("standing did not jump to +0.10");
        if (observer.Notifications.Count != 1)
            return Fail($"multi-band jump emitted {observer.Notifications.Count} notifications instead of one");
        if (!observer.Notifications[0].Contains("normal", StringComparison.OrdinalIgnoreCase))
            return Fail("final-state notification did not describe the recovered standard state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateStandardToSevereJumpNotifiesFinalState()
    {
        AttentionObserver observer = new(CreateReputation(-0.25f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.30f, ReputationChangeReason.PoliceEnforcementRefused);
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Severe)
            return Fail("standing did not jump to severe");
        if (observer.Notifications.Count != 1)
            return Fail($"multi-band jump emitted {observer.Notifications.Count} notifications instead of one");
        if (!observer.Notifications[0].Contains("scrutiny", StringComparison.OrdinalIgnoreCase))
            return Fail("final-state notification did not describe severe scrutiny");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSameBandStandardChangeSilent()
        => ValidateSameBandSilent(-0.25f, -0.05f);

    private (bool Success, string FailureReason) ValidateSameBandElevatedChangeSilent()
        => ValidateSameBandSilent(-0.45f, -0.02f);

    private (bool Success, string FailureReason) ValidateSameBandSevereChangeSilent()
        => ValidateSameBandSilent(-0.55f, -0.02f);

    private (bool Success, string FailureReason) ValidateSameBandSilent(float start, float delta)
    {
        AttentionObserver observer = new(CreateReputation(start));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, delta, ReputationChangeReason.PoliceEnforcementPaid);
        if (observer.Notifications.Count != 0)
            return Fail($"same-band change emitted {observer.Notifications.Count} notification(s)");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRepeatedPresentationReadsSilent()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        for (int i = 0; i < 100; i++)
        {
            _ = PoliceAttentionPresentation.GetLevel(observer.Reputation);
            _ = PoliceAttentionPresentation.GetReadout(observer.Reputation);
            _ = PoliceAttentionPresentation.GetOverviewLine(observer.Reputation);
            _ = ReputationPresentation.BuildLibertyPoliceAttentionLine(observer.Reputation);
        }

        if (observer.Notifications.Count != 0)
            return Fail("repeated presentation reads produced notifications");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateDockOverviewSilent()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        for (int i = 0; i < 25; i++)
        {
            _ = ReputationPresentation.BuildLibertyPoliceAttentionLine(observer.Reputation);
            _ = ReputationPresentation.BuildOverview(observer.Reputation);
        }

        if (observer.Notifications.Count != 0)
            return Fail("dock reputation overview produced notifications");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateMissionBoardOverviewSilent()
    {
        AttentionObserver observer = new(CreateReputation(-0.45f));
        for (int i = 0; i < 25; i++)
        {
            _ = PoliceAttentionPresentation.GetOverviewLine(observer.Reputation);
            _ = ReputationPresentation.BuildLibertyPoliceAttentionLine(observer.Reputation);
        }

        if (observer.Notifications.Count != 0)
            return Fail("mission-board reputation overview produced notifications");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateStationNpcDialogueSilent()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        string ambient = PoliceAttentionPresentation.GetStationAmbientLine(FactionManager.LibertyPolice, observer.Reputation);
        if (string.IsNullOrWhiteSpace(ambient))
            return Fail("ambient dialogue was unexpectedly empty for severe standing");
        if (observer.Notifications.Count != 0)
            return Fail("station NPC dialogue produced notifications by itself");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateContrabandComplianceNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.33f));
        CargoHold cargo = new(100);
        if (!cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1))
            return Fail("could not stage ordinary contraband");
        PlayerCredits credits = new(1_000_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits, observer.Reputation);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Comply, cargo, credits, observer.Reputation, out _, out _))
            return Fail("contraband compliance did not resolve");
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("compliance did not cross into elevated attention");
        if (observer.Notifications.Count != 1)
            return Fail($"compliance emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateContrabandRefusalNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.33f));
        CargoHold cargo = new(100);
        if (!cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1))
            return Fail("could not stage ordinary contraband");
        PlayerCredits credits = new(10_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits, observer.Reputation);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, observer.Reputation, out _, out _))
            return Fail("contraband refusal did not resolve");
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Severe)
            return Fail("refusal did not cross into severe attention");
        if (observer.Notifications.Count != 1)
            return Fail($"refusal emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSmugglingContractNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.33f));
        CargoHold cargo = new(100);
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        if (!cargo.AddMissionCargo(9001, contraband, 2))
            return Fail("could not stage mission-bound smuggling cargo");
        PlayerCredits credits = new(10_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits, observer.Reputation);
        if (!offer.HasContraband)
            return Fail("smuggling contract cargo was not recognized as contraband");
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, observer.Reputation, out _, out _))
            return Fail("smuggling contract refusal did not resolve");
        if (observer.Notifications.Count != 1)
            return Fail($"smuggling consequence emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateBriberyRecoveryNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        PlayerCredits credits = new(1_000_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "phase 76 fixer",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            observer.Reputation,
            credits);
        if (offer == null || !offer.IsValid)
            return Fail("bribe offer was not available");
        if (!FactionBribeService.TryPurchase(offer, observer.Reputation, credits, out _, out string failure))
            return Fail($"bribe purchase failed: {failure}");
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("bribe did not recover standing to standard");
        if (observer.Notifications.Count != 1)
            return Fail($"bribe recovery emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateMissionRecoveryNotifiesOnce()
    {
        AttentionObserver observer = new(CreateReputation(-0.45f));
        observer.Reputation.AdjustReputation(FactionManager.LibertyPolice, 0.30f, ReputationChangeReason.MissionCompleted);
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("mission reward did not recover standing to standard");
        if (observer.Notifications.Count != 1)
            return Fail($"mission recovery emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSaveLoadDoesNotReplayTransition()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-phase76-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "phase76.json");
        try
        {
            AttentionObserver observer = new(CreateReputation(-0.45f));
            observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.10f, ReputationChangeReason.PoliceEnforcementRefused);
            if (observer.Notifications.Count != 1)
                return Fail("pre-save transition was not observed exactly once");

            SaveGameManager saver = new(path);
            SaveGameData data = new()
            {
                PlayerCredits = 10_000,
                Cargo = new List<SaveCargoItemData>(),
                FactionReputation = saver.CaptureReputation(observer.Reputation)
            };
            if (!saver.TrySave(data, out _) || !saver.TryLoad(out SaveGameData loaded, out _))
                return Fail("save/load failed");

            ReputationManager restored = new(new FactionManager());
            saver.ApplyReputation(restored, loaded);
            AttentionObserver postLoad = new(restored);
            if (postLoad.Notifications.Count != 0)
                return Fail("loading replayed a previous transition");
            if (PoliceAttentionPresentation.GetLevel(restored) != LibertyPoliceAttentionLevel.Severe)
                return Fail("loaded standing did not read as severe");
            return Pass();
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private (bool Success, string FailureReason) ValidatePostLoadGenuineTransitionNotifies()
    {
        ReputationManager restored = RoundTripReputation(-0.55f);
        AttentionObserver observer = new(restored);
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, 0.65f, ReputationChangeReason.PoliceEnforcementPaid);
        if (observer.Notifications.Count != 1)
            return Fail($"post-load genuine transition emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNoPersistedPhase76FieldExists()
    {
        foreach (PropertyInfo property in typeof(SaveGameData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = property.Name.ToLowerInvariant();
            if (name.Contains("attention") || name.Contains("transition") || name.Contains("notifier") ||
                name.Contains("notification"))
                return Fail($"SaveGameData carries a persisted Phase 76 field '{property.Name}'");
        }

        int schemaVersion = new SaveGameData().SchemaVersion;
        if (schemaVersion != 13)
            return Fail($"save schema changed unexpectedly (version {schemaVersion})");

        ReputationManager reputation = CreateReputation(-0.55f);
        if (reputation.GetStandingsSnapshot().Any(entry =>
            entry.Key.Contains("attention", StringComparison.OrdinalIgnoreCase) ||
            entry.Key.Contains("transition", StringComparison.OrdinalIgnoreCase)))
            return Fail("standing snapshot carries a synthetic transition entry");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNotificationDoesNotMutateReputation()
    {
        ReputationManager reputation = CreateReputation(-0.45f);
        List<string> notifications = new();
        PoliceAttentionTransitionNotifier notifier = new(reputation, notifications.Add);
        IReadOnlyDictionary<string, float> before = reputation.GetStandingsSnapshot();
        if (!notifier.TryObserve(SyntheticChange(-0.25f, -0.45f)))
            return Fail("synthetic transition was not observed");
        IReadOnlyDictionary<string, float> after = reputation.GetStandingsSnapshot();
        if (before.Count != after.Count)
            return Fail("notification changed the faction standing set");
        foreach (KeyValuePair<string, float> entry in before)
        {
            if (!after.TryGetValue(entry.Key, out float value) || !Nearly(value, entry.Value))
                return Fail($"notification mutated standing for {entry.Key}");
        }

        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.45f))
            return Fail("notification mutated Liberty Police standing");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNotificationDoesNotMutateFugitiveHeat()
    {
        ReputationManager reputation = CreateReputation(-0.45f);
        Ship player = new(Vector3.Zero);
        PoliceFugitiveManager fugitive = new(reputation);
        if (!fugitive.BeginPursuit(player, "phase 76 heat proof", null, PoliceHeatLevel.Pursuit))
            return Fail("could not stage an active pursuit");
        PoliceHeatLevel heatBefore = fugitive.Heat;
        bool activeBefore = fugitive.IsActive;
        float escapeBefore = fugitive.EscapeProgressSeconds;

        List<string> notifications = new();
        PoliceAttentionTransitionNotifier notifier = new(reputation, notifications.Add);
        if (!notifier.TryObserve(SyntheticChange(-0.25f, -0.45f)))
            return Fail("synthetic transition was not observed");

        if (fugitive.Heat != heatBefore || fugitive.IsActive != activeBefore ||
            !Nearly(fugitive.EscapeProgressSeconds, escapeBefore))
            return Fail("notification mutated fugitive heat state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNotificationDoesNotAlterLawfulStop()
    {
        ReputationManager reputation = CreateReputation(-0.45f);
        Ship player = new(Vector3.Zero);
        CargoHold cargo = player.CargoHold;
        if (!cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 2))
            return Fail("could not stage contraband for the lawful-stop proof");
        PlayerCredits credits = new(10_000);
        PoliceScanSystem scan = new();
        NpcShip scanner = new("Phase 76 Police Scan", new Vector3(1_500f, 0f, 0f), new Vector3(1_500f, 0f, 0f),
            1f, 0f, FactionManager.LibertyPolice)
        {
            Position = new Vector3(1_500f, 0f, 0f),
            Velocity = Vector3.Zero
        };
        List<NpcShip> scanners = new() { scanner };
        for (int i = 0; i < 8 && scan.State != PoliceScanState.ContrabandDetected; i++)
        {
            scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5)),
                player, scanners, credits, reputation, notificationManager: null);
        }

        if (scan.State != PoliceScanState.ContrabandDetected || scan.CurrentOffer == null)
            return Fail("lawful stop demand was not staged");
        PoliceScanState stateBefore = scan.State;
        PoliceEnforcementOffer? offerBefore = scan.CurrentOffer;
        bool demandBefore = scan.IsEnforcementDemandActive;

        List<string> notifications = new();
        PoliceAttentionTransitionNotifier notifier = new(reputation, notifications.Add);
        if (!notifier.TryObserve(SyntheticChange(-0.25f, -0.45f)))
            return Fail("synthetic transition was not observed");

        if (scan.State != stateBefore || !ReferenceEquals(scan.CurrentOffer, offerBefore) ||
            scan.IsEnforcementDemandActive != demandBefore)
            return Fail("notification altered the lawful-stop state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNotificationDoesNotAlterHostility()
    {
        ReputationManager reputation = CreateReputation(-0.45f);
        bool hostileBefore = reputation.IsHostile(FactionManager.LibertyPolice);
        bool tempBefore = reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        List<string> notifications = new();
        PoliceAttentionTransitionNotifier notifier = new(reputation, notifications.Add);
        if (!notifier.TryObserve(SyntheticChange(-0.25f, -0.45f)))
            return Fail("synthetic transition was not observed");
        if (reputation.IsHostile(FactionManager.LibertyPolice) != hostileBefore ||
            reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) != tempBefore)
            return Fail("notification altered hostility state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostileTransitionNoDuplicate()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.10f, ReputationChangeReason.PoliceEnforcementRefused);
        if (!observer.Reputation.IsHostile(FactionManager.LibertyPolice))
            return Fail("standing did not reach hostility");
        if (observer.Notifications.Count != 0)
            return Fail("hostility transition produced a duplicate attention notification");
        // The existing band feedback path is available for this change.
        if (!observer.Reputation.GetBand(FactionManager.LibertyPolice).Equals(ReputationBand.Hostile))
            return Fail("existing hostile band feedback was not available");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostileRecoveryAtMostOneFeedback()
    {
        AttentionObserver observer = new(CreateReputation(-0.65f));
        ReputationBand bandBefore = observer.Reputation.GetBand(FactionManager.LibertyPolice);
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, 0.15f, ReputationChangeReason.PoliceEnforcementPaid);
        if (observer.Notifications.Count > 1)
            return Fail("hostile recovery produced more than one notifier feedback event");
        if (observer.Reputation.IsHostile(FactionManager.LibertyPolice))
            return Fail("standing did not leave hostility");
        if (observer.Reputation.GetBand(FactionManager.LibertyPolice) == bandBefore)
            return Fail("existing band feedback was not available for the recovery");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateResetDoesNotInventTransition()
    {
        AttentionObserver observer = new(CreateReputation(-0.45f));
        observer.Reputation.ResetToNewGame();
        if (observer.Notifications.Count != 0)
            return Fail("reset produced a transition notification");
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("reset did not return to the standard baseline");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateDockUndockDoesNotInventTransition()
    {
        AttentionObserver observer = new(CreateReputation(-0.45f));
        for (int cycle = 0; cycle < 10; cycle++)
        {
            _ = ReputationPresentation.BuildLibertyPoliceAttentionLine(observer.Reputation);
            _ = PoliceAttentionPresentation.GetLevel(observer.Reputation);
        }

        if (observer.Notifications.Count != 0)
            return Fail("dock/undock cycle produced a transition notification");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSystemTransitionDoesNotInventTransition()
    {
        AttentionObserver observer = new(CreateReputation(-0.55f));
        for (int i = 0; i < 10; i++)
        {
            observer.Reputation.UpdateTemporaryHostility(1f);
            _ = PoliceAttentionPresentation.GetLevel(observer.Reputation);
        }

        if (observer.Notifications.Count != 0)
            return Fail("system transition produced a transition notification");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRepeatedSameLevelUpdatesSilent()
    {
        AttentionObserver observer = new(CreateReputation(-0.20f));
        for (int i = 0; i < 10; i++)
            observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.01f, ReputationChangeReason.PoliceEnforcementPaid);
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("staged changes unexpectedly left the standard band");
        if (observer.Notifications.Count != 0)
            return Fail($"repeated same-level updates emitted {observer.Notifications.Count} notifications");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateExactElevatedBoundary()
    {
        AttentionObserver observer = new(CreateReputation(-0.34f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.01f, ReputationChangeReason.PoliceEnforcementRefused);
        if (!Nearly(observer.Reputation.GetStanding(FactionManager.LibertyPolice), -0.35f))
            return Fail("standing did not land exactly on -0.35");
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("exact -0.35 did not read as elevated");
        if (observer.Notifications.Count != 1)
            return Fail($"exact -0.35 crossing emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateExactSevereBoundary()
    {
        AttentionObserver observer = new(CreateReputation(-0.49f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.01f, ReputationChangeReason.PoliceEnforcementRefused);
        if (!Nearly(observer.Reputation.GetStanding(FactionManager.LibertyPolice), -0.50f))
            return Fail("standing did not land exactly on -0.50");
        if (PoliceAttentionPresentation.GetLevel(observer.Reputation) != LibertyPoliceAttentionLevel.Severe)
            return Fail("exact -0.50 did not read as severe");
        if (observer.Notifications.Count != 1)
            return Fail($"exact -0.50 crossing emitted {observer.Notifications.Count} notifications instead of one");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostileBoundarySuppressed()
    {
        AttentionObserver observer = new(CreateReputation(-0.5999f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.0001f, ReputationChangeReason.PoliceEnforcementRefused);
        if (!observer.Reputation.IsHostile(FactionManager.LibertyPolice))
            return Fail("standing did not reach hostility at -0.60");
        if (observer.Notifications.Count != 0)
            return Fail("exact -0.60 hostility crossing produced a duplicate attention notification");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostileRecoveryBoundarySuppressed()
    {
        AttentionObserver observer = new(CreateReputation(-0.60f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyPolice, 0.0001f, ReputationChangeReason.PoliceEnforcementPaid);
        if (observer.Reputation.IsHostile(FactionManager.LibertyPolice))
            return Fail("standing did not leave hostility at -0.5999");
        if (observer.Notifications.Count != 0)
            return Fail("hostile recovery boundary produced a duplicate attention notification");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateNonPoliceFactionSilent()
    {
        AttentionObserver observer = new(CreateReputation(-0.25f));
        observer.Reputation.AdjustReputationDirect(FactionManager.LibertyRogues, 0.20f, ReputationChangeReason.MissionCompleted);
        if (observer.Notifications.Count != 0)
            return Fail("non-Police reputation change produced a Police attention notification");
        return Pass();
    }

    private static ReputationManager RoundTripReputation(float standing)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-phase76-rt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "phase76-rt.json");
        try
        {
            SaveGameManager saver = new(path);
            ReputationManager source = CreateReputation(standing);
            SaveGameData data = new()
            {
                PlayerCredits = 10_000,
                Cargo = new List<SaveCargoItemData>(),
                FactionReputation = saver.CaptureReputation(source)
            };
            if (!saver.TrySave(data, out _) || !saver.TryLoad(out SaveGameData loaded, out _))
                throw new InvalidOperationException("save/load round trip failed");

            ReputationManager restored = new(new FactionManager());
            saver.ApplyReputation(restored, loaded);
            return restored;
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static ReputationChangeResult SyntheticChange(float oldValue, float newValue) => new()
    {
        FactionId = FactionManager.LibertyPolice,
        FactionDisplayName = "Liberty Police",
        OldValue = oldValue,
        NewValue = newValue,
        Delta = newValue - oldValue,
        OldBand = ReputationManager.GetBandForStanding(oldValue),
        NewBand = ReputationManager.GetBandForStanding(newValue),
        Reason = ReputationChangeReason.ManualDebug
    };

    private static ReputationManager CreateReputation(float standing)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "phase 76 smoke setup");
        return reputation;
    }

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);

    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private sealed class AttentionObserver
    {
        public AttentionObserver(ReputationManager reputation)
        {
            Reputation = reputation;
            Notifier = new PoliceAttentionTransitionNotifier(reputation, Notifications.Add);
            reputation.OnReputationChanged += change => Notifier.TryObserve(change);
        }

        public ReputationManager Reputation { get; }

        public PoliceAttentionTransitionNotifier Notifier { get; }

        public List<string> Notifications { get; } = new();
    }
}
