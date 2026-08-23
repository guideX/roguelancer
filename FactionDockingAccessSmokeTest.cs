#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 27 coverage for physical station authorization. The
/// assertions exercise the shared policy, legacy dock context, 3D entry seam,
/// autopilot route, and persisted authoritative reputation state.
/// </summary>
internal sealed class FactionDockingAccessSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("production stations expose independent faction ownership", ProductionStationFactionOwnership);
        Check("baseline station docking is allowed", BaselineDockingIsAllowed);
        Check("exact hostile threshold denies docking", HostileThresholdDeniesDocking);
        Check("denial reason identifies faction hostility", DenialReasonIdentifiesFaction);
        Check("temporary hostility denies immediately", TemporaryHostilityDeniesImmediately);
        Check("temporary hostility expiry restores access", TemporaryHostilityExpiryRestoresAccess);
        Check("unrelated faction remains accessible", UnrelatedFactionRemainsAccessible);
        Check("Liberty Police and Liberty Rogues remain independent", IndependentFactionBehavior);
        Check("authoritative reputation recovery restores access", ReputationRecoveryRestoresAccess);
        Check("Phase 25 bribe recovery restores docking naturally", BribeRecoveryRestoresDocking);
        Check("hostile reputation survives save/load", HostileReputationSaveLoadDenies);
        Check("eligible reputation survives save/load", EligibleReputationSaveLoadAllows);
        Check("temporary hostility save/load and expiry are live", TemporaryHostilitySaveLoadRestores);
        Check("new-game reset restores baseline access", ResetRestoresBaselineAccess);
        Check("already-docked player remains safe after reputation loss", AlreadyDockedStateRemainsSafe);
        Check("subsequent re-entry is denied after loss", SubsequentReentryIsDenied);
        Check("legacy and 3D route boundaries share authorization", RouteBoundariesCannotBypass);
        Check("autopilot revalidates live docking access", AutopilotRevalidatesLiveAccess);

        Console.WriteLine($"[FACTION DOCKING ACCESS SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION DOCKING ACCESS SMOKE] PASS {label}");
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
        Console.WriteLine($"[FACTION DOCKING ACCESS SMOKE] FAIL {label}: {reason}");
    }

    private static bool ProductionStationFactionOwnership()
    {
        StationConfig police = LoadStationConfig("station_01_space_station.json");
        StationConfig rogues = LoadStationConfig("station_09_buffalo_base.json");
        return police != null && rogues != null &&
            string.Equals(police.FactionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(rogues.FactionId, FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase);
    }

    private static bool BaselineDockingIsAllowed()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, 0.00f);
        return DockingAccess(PoliceStation(), reputation).IsAllowed;
    }

    private static bool HostileThresholdDeniesDocking()
    {
        ReputationManager reputation = NewReputation(
            FactionManager.LibertyPolice,
            FactionAccessService.DockingHostileThreshold);
        FactionAccessResult access = DockingAccess(PoliceStation(), reputation);
        return !access.IsAllowed && access.CurrentBand == ReputationBand.Hostile;
    }

    private static bool DenialReasonIdentifiesFaction()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        FactionAccessResult access = DockingAccess(PoliceStation(), reputation);
        return !access.IsAllowed &&
            access.FailureMessage.Contains("Liberty Police", StringComparison.Ordinal) &&
            access.FailureMessage.Contains("hostile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TemporaryHostilityDeniesImmediately()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, 0.35f);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        FactionAccessResult access = DockingAccess(PoliceStation(), reputation);
        return !access.IsAllowed && access.IsTemporarilyHostile &&
            access.FailureMessage.Contains("temporarily hostile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TemporaryHostilityExpiryRestoresAccess()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, 0.35f);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        return DockingAccess(PoliceStation(), reputation).IsAllowed &&
            Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.35f);
    }

    private static bool UnrelatedFactionRemainsAccessible()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "phase 27 isolation setup");
        return !DockingAccess(PoliceStation(), reputation).IsAllowed &&
            DockingAccess(RogueStation(), reputation).IsAllowed;
    }

    private static bool IndependentFactionBehavior()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "phase 27 isolation setup");
        bool policeDenied = !DockingAccess(PoliceStation(), reputation).IsAllowed;
        bool rogueAllowed = DockingAccess(RogueStation(), reputation).IsAllowed;

        reputation.SetReputation(FactionManager.LibertyPolice, 0.35f, "phase 27 isolation recovery");
        reputation.SetReputation(FactionManager.LibertyRogues, -0.70f, "phase 27 isolation reversal");
        return policeDenied && rogueAllowed &&
            DockingAccess(PoliceStation(), reputation).IsAllowed &&
            !DockingAccess(RogueStation(), reputation).IsAllowed;
    }

    private static bool ReputationRecoveryRestoresAccess()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        if (DockingAccess(PoliceStation(), reputation).IsAllowed)
            return false;

        reputation.AdjustReputation(FactionManager.LibertyPolice, 0.20f, ReputationChangeReason.MissionCompleted);
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.50f) &&
            DockingAccess(PoliceStation(), reputation).IsAllowed;
    }

    private static bool BribeRecoveryRestoresDocking()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.65f);
        PlayerCredits credits = new(100_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "Elena Vasquez",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            reputation,
            credits);
        if (offer?.IsValid != true || DockingAccess(PoliceStation(), reputation).IsAllowed)
            return false;

        if (!FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;

        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionBribeService.BriberyCeiling) &&
            DockingAccess(PoliceStation(), reputation).IsAllowed;
    }

    private static bool HostileReputationSaveLoadDenies()
    {
        ReputationManager source = NewReputation(FactionManager.LibertyPolice, -0.70f);
        SaveGameManager saveManager = new(TemporaryPath("hostile"));
        if (!TryRoundTrip(saveManager, source, out ReputationManager restored))
            return false;

        return Nearly(restored.GetStanding(FactionManager.LibertyPolice), -0.70f) &&
            !DockingAccess(PoliceStation(), restored).IsAllowed;
    }

    private static bool EligibleReputationSaveLoadAllows()
    {
        ReputationManager source = NewReputation(FactionManager.LibertyPolice, 0.10f);
        SaveGameManager saveManager = new(TemporaryPath("eligible"));
        if (!TryRoundTrip(saveManager, source, out ReputationManager restored))
            return false;

        return Nearly(restored.GetStanding(FactionManager.LibertyPolice), 0.10f) &&
            DockingAccess(PoliceStation(), restored).IsAllowed;
    }

    private static bool TemporaryHostilitySaveLoadRestores()
    {
        ReputationManager source = NewReputation(FactionManager.LibertyPolice, 0.30f);
        source.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        source.UpdateTemporaryHostility(12f);
        SaveGameManager saveManager = new(TemporaryPath("temporary"));
        if (!TryRoundTrip(saveManager, source, out ReputationManager restored))
            return false;

        bool deniedWhileRestored = !DockingAccess(PoliceStation(), restored).IsAllowed &&
            Nearly(restored.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice), 48f);
        restored.UpdateTemporaryHostility(48f);
        return deniedWhileRestored && DockingAccess(PoliceStation(), restored).IsAllowed;
    }

    private static bool ResetRestoresBaselineAccess()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        reputation.ResetToNewGame();
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f) &&
            !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            DockingAccess(PoliceStation(), reputation).IsAllowed;
    }

    private static bool AlreadyDockedStateRemainsSafe()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, 0.10f);
        StationDockUI dockUi = CreateDockUi(reputation);
        Station station = PoliceStation();
        if (!dockUi.DockAtStation(station))
            return false;

        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "standing loss while docked");
        return dockUi.IsDocked && ReferenceEquals(dockUi.DockedStation, station);
    }

    private static bool SubsequentReentryIsDenied()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, 0.10f);
        StationDockUI dockUi = CreateDockUi(reputation);
        Station station = PoliceStation();
        if (!dockUi.DockAtStation(station))
            return false;

        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "standing loss while docked");
        dockUi.Undock();
        return !dockUi.IsDocked && !dockUi.DockAtStation(station) &&
            dockUi.LastDockingDeniedReason.Contains("Liberty Police", StringComparison.Ordinal);
    }

    private static bool RouteBoundariesCannotBypass()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        Station station = PoliceStation();
        StationDockUI dockUi = CreateDockUi(reputation);
        return !dockUi.DockAtStation(station) &&
            !DockNavigation.IsDockableStation(station, reputation);
    }

    private static bool AutopilotRevalidatesLiveAccess()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, 0.10f);
        Station station = PoliceStation();
        Ship ship = new(Vector3.Zero);
        GotoAutopilot autopilot = new();
        autopilot.Initialize(
            ship,
            null,
            null,
            new List<Station> { station },
            new List<SpaceObject> { station },
            new List<NpcShip>(),
            null,
            null,
            null,
            reputation);
        ship.SetGotoAutopilot(autopilot);
        ship.SetNotificationManager(new NotificationManager(null, new Viewport(0, 0, 1920, 1080)));

        if (!ship.ActivateGoto(station))
            return false;

        reputation.SetReputation(FactionManager.LibertyPolice, -0.70f, "standing loss during approach");
        ship.Update(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)), new KeyboardState());
        return autopilot.WasDockingDenied &&
            autopilot.LastDockingDeniedReason.Contains("Liberty Police", StringComparison.Ordinal) &&
            !ship.IsGotoActive;
    }

    private static FactionAccessResult DockingAccess(Station station, ReputationManager reputation) =>
        FactionAccessService.EvaluateDocking(reputation, station.FactionId, station.Name);

    private static Station PoliceStation() => CreateStation("Fort Bush", FactionManager.LibertyPolice);

    private static Station RogueStation() => CreateStation("Buffalo Base", FactionManager.LibertyRogues);

    private static Station CreateStation(string name, string factionId) =>
        new(new StationConfig
        {
            Description = name,
            FactionId = factionId,
            StartupPositionX = 2_000f,
            StartupPositionY = 0f,
            StartupPositionZ = -2_000f,
            Radius = 600f,
            DockingRange = 900f
        }, null);

    private static ReputationManager NewReputation(string factionId, float standing)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(factionId, standing, "phase 27 smoke setup");
        return reputation;
    }

    private static StationDockUI CreateDockUi(ReputationManager reputation)
    {
        PlayerCredits credits = new(100_000);
        MissionManager missions = new(credits, null, reputation);
        return new StationDockUI(
            null,
            null,
            null,
            new CommodityDealer(),
            missions,
            reputation);
    }

    private static bool TryRoundTrip(
        SaveGameManager saveManager,
        ReputationManager source,
        out ReputationManager restored)
    {
        restored = new(new FactionManager());
        try
        {
            SaveGameData data = new()
            {
                FactionReputation = saveManager.CaptureReputation(source),
                TemporaryHostility = saveManager.CaptureTemporaryHostility(source)
            };
            if (!saveManager.TrySave(data, out _))
                return false;
            if (!saveManager.TryLoad(out SaveGameData loaded, out _))
                return false;

            saveManager.ApplyReputation(restored, loaded);
            saveManager.ApplyTemporaryHostility(restored, loaded);
            return true;
        }
        finally
        {
            if (File.Exists(saveManager.SavePath))
                File.Delete(saveManager.SavePath);
        }
    }

    private static string TemporaryPath(string label) =>
        Path.Combine(Path.GetTempPath(), $"roguelancer-phase27-{label}-{Guid.NewGuid():N}.json");

    private static StationConfig LoadStationConfig(string fileName)
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Configuration", "stations", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "stations", fileName)
        };
        string path = candidates.First(candidate => File.Exists(candidate));
        return JsonSerializer.Deserialize<StationConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static TResult RunSilenced<TResult>(Func<TResult> function)
    {
        TextWriter original = Console.Out;
        try
        {
            using StringWriter writer = new();
            Console.SetOut(writer);
            return function();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
