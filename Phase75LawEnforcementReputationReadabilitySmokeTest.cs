#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 75 focused coverage for player-facing Liberty Police reputation
/// readability. The harness drives only production systems: the durable
/// standing authority (ReputationManager), the single Phase 74 tier policy
/// (PoliceEnforcementEscalationPolicy), the pure presentation projection
/// (PoliceAttentionPresentation / ReputationPresentation), the authoritative
/// contraband enforcement path (PoliceEnforcementService), the scan/stop
/// lifecycle (PoliceScanSystem), the single pursuit owner
/// (PoliceFugitiveManager), and SaveGameManager persistence. No parallel
/// crime, notoriety, warrant, heat, or criminal-history state exists here.
/// </summary>
internal sealed class Phase75LawEnforcementReputationReadabilitySmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(ValidateNewGameStandingIsStandard, "new-game Police standing produces standard semantics", ref passed, ref failed);
        RunCase(ValidateJustAboveElevatedThresholdStaysStandard, "standing just above -0.35 remains standard", ref passed, ref failed);
        RunCase(ValidateExactElevatedThresholdReportsElevated, "standing exactly -0.35 reports elevated", ref passed, ref failed);
        RunCase(ValidateMidElevatedReportsElevated, "mid-elevated standing reports elevated", ref passed, ref failed);
        RunCase(ValidateExactSevereThresholdReportsSevere, "standing exactly -0.50 reports severe", ref passed, ref failed);
        RunCase(ValidateLawfulSevereReportsSevere, "lawful severe standing between -0.50 and -0.60 reports severe", ref passed, ref failed);
        RunCase(ValidateHostileDoesNotReportPeacefulAttention, "hostile relationship does not report peaceful attention", ref passed, ref failed);
        RunCase(ValidateSevereToStandardRecoversImmediately, "improving severe to standard updates immediately", ref passed, ref failed);
        RunCase(ValidateElevatedToStandardRecoversImmediately, "improving elevated to standard updates immediately", ref passed, ref failed);
        RunCase(ValidateStandardToElevatedWorsensImmediately, "worsening standard to elevated updates immediately", ref passed, ref failed);
        RunCase(ValidateElevatedToSevereWorsensImmediately, "worsening elevated to severe updates immediately", ref passed, ref failed);
        RunCase(ValidateSaveLoadPreservesReadability, "save/load preserves the displayed tier through reputation persistence", ref passed, ref failed);
        RunCase(ValidateNoPersistedPhase75FieldExists, "no new persisted Phase 75 field or schema member exists", ref passed, ref failed);
        RunCase(ValidateOrdinaryContrabandChangeReflected, "ordinary contraband-driven reputation changes are reflected", ref passed, ref failed);
        RunCase(ValidateSmugglingContractChangeReflected, "smuggling-contract-driven reputation changes are reflected", ref passed, ref failed);
        RunCase(ValidatePresentationDoesNotMutateReputation, "presentation does not mutate reputation", ref passed, ref failed);
        RunCase(ValidatePresentationDoesNotMutateFugitiveHeat, "presentation does not mutate fugitive heat", ref passed, ref failed);
        RunCase(ValidatePresentationDoesNotAlterLawfulStop, "presentation does not alter lawful-stop state", ref passed, ref failed);
        RunCase(ValidateRepeatedReadsAreDeterministic, "repeated reads and open/close operations are deterministic", ref passed, ref failed);
        RunCase(ValidateExistingHostilityRulesUnchanged, "existing hostility rules remain unchanged", ref passed, ref failed);

        Console.WriteLine($"[PHASE 75 SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 75 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 75 SMOKE] FAIL {label}: {failureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 75 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidateNewGameStandingIsStandard()
    {
        ReputationManager reputation = new(new FactionManager());
        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f))
            return Fail("new-game Police standing was not -0.25");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("new-game standing did not read as standard");
        if (!PoliceAttentionPresentation.GetOverviewLine(reputation).Contains("NORMAL", StringComparison.Ordinal))
            return Fail("standard overview line did not read as normal");
        if (!string.IsNullOrEmpty(PoliceAttentionPresentation.GetReadout(reputation)))
            return Fail("standard readout should not alarm the player");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateJustAboveElevatedThresholdStaysStandard()
    {
        foreach (float standing in new[] { -0.3499f, -0.34f, 0.00f, 0.25f })
        {
            ReputationManager reputation = CreateReputation(standing);
            if (PoliceEnforcementEscalationPolicy.GetTier(reputation) != PoliceEnforcementTier.Standard)
                return Fail($"standing {standing} did not use the standard tier");
            if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Standard)
                return Fail($"standing {standing} did not read as standard");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateExactElevatedThresholdReportsElevated()
    {
        ReputationManager reputation = CreateReputation(PoliceEnforcementEscalationPolicy.ElevatedStandingThreshold);
        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.35f))
            return Fail("exact -0.35 was not stored");
        if (PoliceEnforcementEscalationPolicy.GetTier(reputation) != PoliceEnforcementTier.Elevated)
            return Fail("exact -0.35 did not use the Phase 74 elevated tier");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("exact -0.35 did not read as elevated");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateMidElevatedReportsElevated()
    {
        ReputationManager reputation = CreateReputation(-0.425f);
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("mid-elevated standing did not read as elevated");
        string readout = PoliceAttentionPresentation.GetReadout(reputation);
        if (string.IsNullOrWhiteSpace(readout) || !readout.Contains("attention", StringComparison.OrdinalIgnoreCase))
            return Fail("elevated readout did not describe increased attention");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateExactSevereThresholdReportsSevere()
    {
        ReputationManager reputation = CreateReputation(PoliceEnforcementEscalationPolicy.SevereStandingThreshold);
        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.50f))
            return Fail("exact -0.50 was not stored");
        if (PoliceEnforcementEscalationPolicy.GetTier(reputation) != PoliceEnforcementTier.Severe)
            return Fail("exact -0.50 did not use the Phase 74 severe tier");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Severe)
            return Fail("exact -0.50 did not read as severe");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateLawfulSevereReportsSevere()
    {
        foreach (float standing in new[] { -0.51f, -0.55f, -0.5999f })
        {
            ReputationManager reputation = CreateReputation(standing);
            if (reputation.IsHostile(FactionManager.LibertyPolice))
                return Fail($"standing {standing} was unexpectedly hostile");
            if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Severe)
                return Fail($"standing {standing} did not read as severe");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateHostileDoesNotReportPeacefulAttention()
    {
        foreach (float standing in new[] { -0.60f, -0.75f, -1.00f })
        {
            ReputationManager reputation = CreateReputation(standing);
            if (!reputation.IsHostile(FactionManager.LibertyPolice))
                return Fail($"standing {standing} was not staged as hostile");
            // The Phase 74 tier is standing-only and still reports severe; the
            // presentation must resolve hostility first and never describe a
            // hostile relationship as mere inspection attention.
            if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Hostile)
                return Fail($"standing {standing} did not read as hostile");
            string overview = PoliceAttentionPresentation.GetOverviewLine(reputation);
            if (!overview.Contains("HOSTILE", StringComparison.Ordinal))
                return Fail("hostile overview line did not state hostility");
            if (overview.Contains("SCRUTINY", StringComparison.Ordinal) ||
                overview.Contains("RECENT ACTIVITY", StringComparison.Ordinal))
                return Fail("hostile overview line misleadingly described peaceful attention");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSevereToStandardRecoversImmediately()
    {
        ReputationManager reputation = CreateReputation(-0.55f);
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Severe)
            return Fail("severe baseline was not staged");
        reputation.SetReputation(FactionManager.LibertyPolice, 0.10f, "phase 75 recovery setup");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("recovered standing did not immediately return to standard");
        if (!string.IsNullOrEmpty(PoliceAttentionPresentation.GetReadout(reputation)))
            return Fail("recovered readout still carried stale attention text");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedToStandardRecoversImmediately()
    {
        ReputationManager reputation = CreateReputation(-0.45f);
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("elevated baseline was not staged");
        reputation.SetReputation(FactionManager.LibertyPolice, 0.10f, "phase 75 recovery setup");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("recovered elevated standing did not immediately return to standard");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateStandardToElevatedWorsensImmediately()
    {
        ReputationManager reputation = CreateReputation(-0.25f);
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("standard baseline was not staged");
        reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.20f, ReputationChangeReason.PoliceEnforcementRefused);
        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.45f))
            return Fail("standing did not worsen to -0.45");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("worsened standing did not immediately read as elevated");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateElevatedToSevereWorsensImmediately()
    {
        ReputationManager reputation = CreateReputation(-0.45f);
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("elevated baseline was not staged");
        reputation.AdjustReputationDirect(FactionManager.LibertyPolice, -0.10f, ReputationChangeReason.PoliceEnforcementRefused);
        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.55f))
            return Fail("standing did not worsen to -0.55");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Severe)
            return Fail("worsened standing did not immediately read as severe");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSaveLoadPreservesReadability()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-phase75-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "phase75.json");
        try
        {
            if (!RoundTripLevel(path, -0.55f, LibertyPoliceAttentionLevel.Severe))
                return Fail("severe readability did not survive save/load");
            if (!RoundTripLevel(path, -0.45f, LibertyPoliceAttentionLevel.Elevated))
                return Fail("elevated readability did not survive save/load");
            if (!RoundTripLevel(path, -0.25f, LibertyPoliceAttentionLevel.Standard))
                return Fail("standard readability did not survive save/load");

            // Legacy-shaped save with no Phase 75 fields still loads and
            // reconstructs the presentation from durable reputation.
            SaveGameManager saver = new(path);
            SaveGameData legacy = new()
            {
                PlayerCredits = 5_000,
                Cargo = new List<SaveCargoItemData>(),
                FactionReputation = new List<SaveFactionReputationData>
                {
                    new() { FactionId = FactionManager.LibertyPolice, Standing = -0.20f }
                }
            };
            if (!saver.TrySave(legacy, out _) || !saver.TryLoad(out SaveGameData legacyLoaded, out _))
                return Fail("a compatible legacy-shaped save did not load");
            ReputationManager legacyRestored = new(new FactionManager());
            saver.ApplyReputation(legacyRestored, legacyLoaded);
            if (PoliceAttentionPresentation.GetLevel(legacyRestored) != LibertyPoliceAttentionLevel.Standard)
                return Fail("legacy save did not reconstruct standard readability");
            return Pass();
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static bool RoundTripLevel(string path, float standing, LibertyPoliceAttentionLevel expected)
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
            return false;

        ReputationManager restored = new(new FactionManager());
        saver.ApplyReputation(restored, loaded);
        return Nearly(restored.GetStanding(FactionManager.LibertyPolice), standing) &&
            PoliceAttentionPresentation.GetLevel(restored) == expected;
    }

    private (bool Success, string FailureReason) ValidateNoPersistedPhase75FieldExists()
    {
        foreach (PropertyInfo property in typeof(SaveGameData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = property.Name.ToLowerInvariant();
            if (name.Contains("attention") || name.Contains("readout") || name.Contains("readability") ||
                name.Contains("presentation") || name.Contains("tier") || name.Contains("escalation"))
                return Fail($"SaveGameData carries a persisted Phase 75 field '{property.Name}'");
        }

        int schemaVersion = new SaveGameData().SchemaVersion;
        if (schemaVersion != 13)
            return Fail($"save schema changed unexpectedly (version {schemaVersion})");

        ReputationManager reputation = CreateReputation(-0.55f);
        if (reputation.GetStandingsSnapshot().Any(entry => entry.Key.Contains("attention", StringComparison.OrdinalIgnoreCase)))
            return Fail("standing snapshot carries a synthetic attention entry");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateOrdinaryContrabandChangeReflected()
    {
        ReputationManager reputation = CreateReputation(-0.25f);
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Standard)
            return Fail("standard baseline was not staged");
        CargoHold cargo = new(100);
        if (!cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1))
            return Fail("could not stage ordinary contraband");
        PlayerCredits credits = new(10_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits, reputation);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, reputation, out _, out _))
            return Fail("ordinary contraband refusal did not resolve");
        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.45f))
            return Fail("ordinary contraband penalty did not lower Police standing to -0.45");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("ordinary contraband consequence was not reflected in the presentation");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSmugglingContractChangeReflected()
    {
        ReputationManager reputation = CreateReputation(-0.25f);
        CargoHold cargo = new(100);
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        if (!cargo.AddMissionCargo(9001, contraband, 2))
            return Fail("could not stage mission-bound smuggling cargo");
        PlayerCredits credits = new(10_000);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits, reputation);
        if (!offer.HasContraband)
            return Fail("smuggling contract cargo was not recognized as contraband");
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, reputation, out _, out _))
            return Fail("smuggling contract refusal did not resolve");
        if (PoliceAttentionPresentation.GetLevel(reputation) != LibertyPoliceAttentionLevel.Elevated)
            return Fail("smuggling contract consequence was not reflected through the same standing authority");
        // The source of the change is irrelevant to the presentation; only the
        // authoritative standing decides the message.
        ReputationManager equivalent = CreateReputation(reputation.GetStanding(FactionManager.LibertyPolice));
        if (PoliceAttentionPresentation.GetLevel(equivalent) != PoliceAttentionPresentation.GetLevel(reputation))
            return Fail("presentation diverged for equal standing from different sources");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidatePresentationDoesNotMutateReputation()
    {
        ReputationManager reputation = CreateReputation(-0.45f);
        IReadOnlyDictionary<string, float> before = reputation.GetStandingsSnapshot();
        for (int i = 0; i < 25; i++)
        {
            _ = PoliceAttentionPresentation.GetLevel(reputation);
            _ = PoliceAttentionPresentation.GetReadout(reputation);
            _ = PoliceAttentionPresentation.GetOverviewLine(reputation);
            _ = ReputationPresentation.BuildLibertyPoliceAttentionLine(reputation);
        }

        IReadOnlyDictionary<string, float> after = reputation.GetStandingsSnapshot();
        if (before.Count != after.Count)
            return Fail("presentation changed the faction standing set");
        foreach (KeyValuePair<string, float> entry in before)
        {
            if (!after.TryGetValue(entry.Key, out float value) || !Nearly(value, entry.Value))
                return Fail($"presentation mutated standing for {entry.Key}");
        }

        if (!Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.45f))
            return Fail("presentation mutated Liberty Police standing");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidatePresentationDoesNotMutateFugitiveHeat()
    {
        ReputationManager reputation = CreateReputation(-0.55f);
        Ship player = new(Vector3.Zero);
        PoliceFugitiveManager fugitive = new(reputation);
        if (!fugitive.BeginPursuit(player, "phase 75 heat proof", null, PoliceHeatLevel.Pursuit))
            return Fail("could not stage an active pursuit");
        PoliceHeatLevel heatBefore = fugitive.Heat;
        bool activeBefore = fugitive.IsActive;
        float escapeBefore = fugitive.EscapeProgressSeconds;
        for (int i = 0; i < 25; i++)
        {
            _ = PoliceAttentionPresentation.GetLevel(reputation);
            _ = PoliceAttentionPresentation.GetOverviewLine(reputation);
        }

        if (fugitive.Heat != heatBefore || fugitive.IsActive != activeBefore ||
            !Nearly(fugitive.EscapeProgressSeconds, escapeBefore))
            return Fail("presentation mutated fugitive heat state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidatePresentationDoesNotAlterLawfulStop()
    {
        ReputationManager reputation = CreateReputation(-0.25f);
        Ship player = new(Vector3.Zero);
        CargoHold cargo = player.CargoHold;
        if (!cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 2))
            return Fail("could not stage contraband for the lawful-stop proof");
        PlayerCredits credits = new(10_000);
        PoliceScanSystem scan = new();
        NpcShip scanner = new("Phase 75 Police Scan", new Vector3(1_500f, 0f, 0f), new Vector3(1_500f, 0f, 0f),
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
        for (int i = 0; i < 25; i++)
        {
            _ = PoliceAttentionPresentation.GetLevel(reputation);
            _ = PoliceAttentionPresentation.GetReadout(reputation);
            _ = ReputationPresentation.BuildLibertyPoliceAttentionLine(reputation);
        }

        if (scan.State != stateBefore || !ReferenceEquals(scan.CurrentOffer, offerBefore) ||
            scan.IsEnforcementDemandActive != demandBefore)
            return Fail("presentation altered the lawful-stop state");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateRepeatedReadsAreDeterministic()
    {
        ReputationManager reputation = CreateReputation(-0.55f);
        LibertyPoliceAttentionLevel level = PoliceAttentionPresentation.GetLevel(reputation);
        string readout = PoliceAttentionPresentation.GetReadout(reputation);
        string overview = PoliceAttentionPresentation.GetOverviewLine(reputation);
        string presentationLine = ReputationPresentation.BuildLibertyPoliceAttentionLine(reputation);
        for (int i = 0; i < 100; i++)
        {
            if (PoliceAttentionPresentation.GetLevel(reputation) != level ||
                !string.Equals(PoliceAttentionPresentation.GetReadout(reputation), readout, StringComparison.Ordinal) ||
                !string.Equals(PoliceAttentionPresentation.GetOverviewLine(reputation), overview, StringComparison.Ordinal) ||
                !string.Equals(ReputationPresentation.BuildLibertyPoliceAttentionLine(reputation), presentationLine, StringComparison.Ordinal))
                return Fail("repeated reads were not deterministic");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateExistingHostilityRulesUnchanged()
    {
        if (ReputationManager.GetBandForStanding(ReputationManager.HostileThreshold) != ReputationBand.Hostile)
            return Fail("the hostile threshold band changed");
        if (ReputationManager.GetBandForStanding(-0.59f) == ReputationBand.Hostile)
            return Fail("a lawful severe standing was treated as hostile");
        if (ReputationManager.GetBandForStanding(ReputationManager.UnfriendlyThreshold) != ReputationBand.Neutral)
            return Fail("the unfriendly threshold band changed");

        ReputationManager hostile = CreateReputation(-0.60f);
        if (!hostile.IsHostile(FactionManager.LibertyPolice))
            return Fail("standing -0.60 was not hostile");
        ReputationManager severe = CreateReputation(-0.5999f);
        if (severe.IsHostile(FactionManager.LibertyPolice))
            return Fail("standing -0.5999 was incorrectly hostile");
        // The enforcement tier stays standing-only; the presentation layers
        // hostility on top without redefining the band.
        if (PoliceEnforcementEscalationPolicy.GetTier(hostile) != PoliceEnforcementTier.Severe)
            return Fail("the Phase 74 tier definition changed");
        return Pass();
    }

    private static ReputationManager CreateReputation(float standing)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "phase 75 smoke setup");
        return reputation;
    }

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);

    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
}
