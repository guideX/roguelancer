using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 58 proof for bounded target scanning, authoritative cargo
/// reads, cancellation, target binding, and Phase 56/57 consistency.
/// </summary>
internal sealed class PlayerTargetScanSmokeTest
{
    private sealed class Context
    {
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public FactionManager Factions { get; } = new();
        public ReputationManager Reputation { get; }
        public Ship Player { get; } = new(Vector3.Zero);
        public LootManager Loot { get; }
        public PirateCargoDemandService Demand { get; }
        public PlayerTargetScanService Scanner { get; }
        public NpcShip TraderA { get; }
        public NpcShip TraderB { get; }

        public Context()
        {
            Reputation = new ReputationManager(Factions);
            TraderA = CreateTrader("Phase58 Trader A", new Vector3(1000f, 0f, 0f));
            TraderB = CreateTrader("Phase58 Trader B", new Vector3(1500f, 0f, 0f));
            AddNpc(TraderA);
            AddNpc(TraderB);

            Loot = new LootManager(
                graphicsDevice: null,
                random: null,
                font: null,
                pixel: null,
                salvageService: new CombatSalvageService(),
                worldObjectsProvider: () => Objects);
            Demand = new PirateCargoDemandService(
                Npcs,
                Reputation,
                spawnCargo: (trader, commodityId, quantity) =>
                    Loot.SpawnExtortionCargo(trader, commodityId, quantity, out _));
            Scanner = new PlayerTargetScanService(
                Npcs,
                Factions,
                target => Demand.TryGetAuthoritativeManifestSnapshot(target, out NpcCargoManifestSnapshot snapshot)
                    ? snapshot
                    : NpcCargoManifestSnapshot.NoRegisteredCargo());
        }

        public void AddNpc(NpcShip npc)
        {
            Npcs.Add(npc);
            Objects.Add(npc);
        }

        public PlayerTargetScanResult CompleteScan(NpcShip target)
        {
            if (!Scanner.TryStartScan(Player, target, false, out string failureReason))
                throw new InvalidOperationException(failureReason);

            Scanner.Update(PlayerTargetScanService.ScanDurationSeconds, Player, target, false);
            return Scanner.GetResultFor(target);
        }

        private static NpcShip CreateTrader(string name, Vector3 position)
        {
            NpcShip trader = new(name, position, Vector3.Zero, 1000f, 0.1f, FactionManager.NeutralCivilians);
            trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                "phase58-trader-route",
                Vector3.Zero,
                1000f,
                190f,
                3500f,
                new Vector3(-5000f, 0f, 0f),
                new Vector3(5000f, 0f, 0f));
            return trader;
        }
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("no target is rejected", NoTargetIsRejected, ref passed, ref failed);
        RunCase("destroyed target is rejected", DestroyedTargetIsRejected, ref passed, ref failed);
        RunCase("range is bounded", RangeIsBounded, ref passed, ref failed);
        RunCase("valid trader scan completes", ValidTraderScanCompletes, ref passed, ref failed);
        RunCase("scan duration is continuous and bounded", ScanDurationIsContinuous, ref passed, ref failed);
        RunCase("incomplete scan reveals nothing", IncompleteScanRevealsNothing, ref passed, ref failed);
        RunCase("range loss cancels and resets", RangeLossCancelsAndResets, ref passed, ref failed);
        RunCase("target change cancels and clears", TargetChangeCancelsAndClears, ref passed, ref failed);
        RunCase("separate trader scans stay bound", SeparateTraderScansStayBound, ref passed, ref failed);
        RunCase("only one active scan exists", OnlyOneActiveScanExists, ref passed, ref failed);
        RunCase("stable target binding prevents cross-target completion", StableTargetBindingPreventsCrossTargetCompletion, ref passed, ref failed);
        RunCase("lane transit blocks player scan", PlayerLaneTransitBlocksScan, ref passed, ref failed);
        RunCase("lane transit blocks target scan", TargetLaneTransitBlocksScan, ref passed, ref failed);
        RunCase("scan reports canonical target intelligence", ScanReportsCanonicalTargetIntelligence, ref passed, ref failed);
        RunCase("scan is read only", ScanIsReadOnly, ref passed, ref failed);
        RunCase("non-trader scan has no fabricated cargo", NonTraderHasNoFabricatedCargo, ref passed, ref failed);
        RunCase("registered empty cargo reports empty", RegisteredEmptyCargoReportsEmpty, ref passed, ref failed);
        RunCase("Police scan is legal and public", PoliceScanIsLegal, ref passed, ref failed);
        RunCase("contraband uses canonical metadata", ContrabandUsesCanonicalMetadata, ref passed, ref failed);
        RunCase("cargo value uses canonical base price", CargoValueUsesCanonicalBasePrice, ref passed, ref failed);
        RunCase("rescan refreshes current snapshot", RescanRefreshesCurrentSnapshot, ref passed, ref failed);
        RunCase("demand consumes scanned manifest", DemandConsumesScannedManifest, ref passed, ref failed);
        RunCase("surrendered pod retains stolen provenance", SurrenderedPodRetainsStolenProvenance, ref passed, ref failed);
        RunCase("destroyed target cancels scan", DestroyedTargetCancelsScan, ref passed, ref failed);
        RunCase("reset clears transient intelligence", ResetClearsTransientIntelligence, ref passed, ref failed);

        Console.WriteLine($"[PLAYER TARGET SCAN SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[PLAYER TARGET SCAN SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PLAYER TARGET SCAN SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PLAYER TARGET SCAN SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) NoTargetIsRejected()
    {
        Context c = new();
        bool started = c.Scanner.TryStartScan(c.Player, null, false, out string reason);
        return Check(!started && reason.Contains("valid target", StringComparison.OrdinalIgnoreCase), "null target started a scan");
    }

    private static (bool, string) DestroyedTargetIsRejected()
    {
        Context c = new();
        c.TraderA.ApplyDamage(1000f, NpcDestructionSource.Unknown);
        bool started = c.Scanner.TryStartScan(c.Player, c.TraderA, false, out string reason);
        return Check(!started && reason.Contains("destroyed", StringComparison.OrdinalIgnoreCase), "destroyed target started a scan");
    }

    private static (bool, string) RangeIsBounded()
    {
        Context c = new();
        c.Player.Position = new Vector3(c.TraderA.Position.X + PlayerTargetScanService.ScannerRange + 1f, 0f, 0f);
        bool started = c.Scanner.TryStartScan(c.Player, c.TraderA, false, out string reason);
        return Check(!started && reason.Contains("outside scanner range", StringComparison.OrdinalIgnoreCase), "out-of-range target was silently accepted");
    }

    private static (bool, string) ValidTraderScanCompletes()
    {
        Context c = new();
        PlayerTargetScanResult result = c.CompleteScan(c.TraderA);
        return Check(result != null && result.TargetName == c.TraderA.Name &&
            result.HasRegisteredCargo && result.Cargo.Count > 0 &&
            result.TargetIdentity == c.TraderA.StableIdentity, "valid trader did not complete an authoritative scan");
    }

    private static (bool, string) ScanDurationIsContinuous()
    {
        Context c = new();
        c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        c.Scanner.Update(PlayerTargetScanService.ScanDurationSeconds - 0.01f, c.Player, c.TraderA, false);
        bool partial = c.Scanner.HasActiveScan && c.Scanner.LastResult == null &&
            c.Scanner.ProgressSeconds > 0f && c.Scanner.ProgressSeconds < PlayerTargetScanService.ScanDurationSeconds;
        c.Scanner.Update(0.01f, c.Player, c.TraderA, false);
        return Check(partial && c.Scanner.LastResult != null, "scan did not require the full bounded duration");
    }

    private static (bool, string) IncompleteScanRevealsNothing()
    {
        Context c = new();
        c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        c.Scanner.Update(1f, c.Player, c.TraderA, false);
        return Check(c.Scanner.LastResult == null, "partial scan exposed cargo");
    }

    private static (bool, string) RangeLossCancelsAndResets()
    {
        Context c = new();
        c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        c.Scanner.Update(1f, c.Player, c.TraderA, false);
        c.Player.Position = new Vector3(c.TraderA.Position.X + PlayerTargetScanService.ScannerRange + 10f, 0f, 0f);
        c.Scanner.Update(0.1f, c.Player, c.TraderA, false);
        c.Player.Position = Vector3.Zero;
        bool restarted = c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        return Check(restarted && c.Scanner.HasActiveScan && c.Scanner.ProgressSeconds == 0f, "range cancellation did not fully reset progress");
    }

    private static (bool, string) TargetChangeCancelsAndClears()
    {
        Context c = new();
        c.CompleteScan(c.TraderA);
        c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        c.Scanner.NotifyTargetChanged(c.TraderB);
        return Check(!c.Scanner.HasActiveScan && c.Scanner.GetResultFor(c.TraderB) == null && c.Scanner.LastResult == null, "old scan survived target change");
    }

    private static (bool, string) SeparateTraderScansStayBound()
    {
        Context c = new();
        PlayerTargetScanResult resultA = c.CompleteScan(c.TraderA);
        c.Demand.TryGetAuthoritativeManifestSnapshot(c.TraderA, out NpcCargoManifestSnapshot manifestA);
        c.Scanner.NotifyTargetChanged(c.TraderB);
        PlayerTargetScanResult resultB = c.CompleteScan(c.TraderB);
        c.Demand.TryGetAuthoritativeManifestSnapshot(c.TraderB, out NpcCargoManifestSnapshot manifestB);
        return Check(resultA != null && resultB != null &&
            resultA.TargetIdentity != resultB.TargetIdentity &&
            ManifestKey(manifestA) == ScanKey(resultA) &&
            ManifestKey(manifestB) == ScanKey(resultB) &&
            c.Scanner.GetResultFor(c.TraderA) == null &&
            ReferenceEquals(c.Scanner.GetResultFor(c.TraderB), resultB),
            "two trader scans leaked identity or cargo across targets");
    }

    private static (bool, string) OnlyOneActiveScanExists()
    {
        Context c = new();
        bool first = c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        bool second = c.Scanner.TryStartScan(c.Player, c.TraderB, false, out string reason);
        return Check(first && !second && reason.Contains("already active", StringComparison.OrdinalIgnoreCase), "multiple active scans were accepted");
    }

    private static (bool, string) StableTargetBindingPreventsCrossTargetCompletion()
    {
        Context c = new();
        c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        c.Scanner.Update(1f, c.Player, c.TraderB, false);
        return Check(!c.Scanner.HasActiveScan && c.Scanner.LastResult == null, "scan completed against a newly selected target");
    }

    private static (bool, string) PlayerLaneTransitBlocksScan()
    {
        Context c = new();
        c.Player.SetTradeLaneTransit(true);
        bool started = c.Scanner.TryStartScan(c.Player, c.TraderA, false, out string reason);
        return Check(!started && reason.Contains("transit", StringComparison.OrdinalIgnoreCase), "player lane transit did not block scanning");
    }

    private static (bool, string) TargetLaneTransitBlocksScan()
    {
        Context c = new();
        c.TraderA.SetTradeLaneTransit(true, "phase58-lane", TradeLaneDirection.Forward, 1);
        bool started = c.Scanner.TryStartScan(c.Player, c.TraderA, false, out string reason);
        return Check(!started && reason.Contains("transit", StringComparison.OrdinalIgnoreCase), "target lane transit did not block scanning");
    }

    private static (bool, string) ScanReportsCanonicalTargetIntelligence()
    {
        Context c = new();
        c.TraderA.ModelPath = "models/transport_ship_alpha.fbx";
        PlayerTargetScanResult result = c.CompleteScan(c.TraderA);
        return Check(result.FactionId == FactionManager.NeutralCivilians &&
            result.FactionLabel == "Neutral Civilians" && result.ShipTypeLabel == "Transport Ship Alpha" &&
            result.HullPercentage > 0f && result.ShieldPercentage >= 0f, "basic target intelligence was incomplete");
    }

    private static (bool, string) ScanIsReadOnly()
    {
        Context c = new();
        c.Demand.TryGetAuthoritativeManifestSnapshot(c.TraderA, out NpcCargoManifestSnapshot before);
        string keyBefore = ManifestKey(before);
        float reputationBefore = c.Reputation.GetStanding(c.TraderA.FactionId);
        int resolvedBefore = c.Demand.ResolvedTargetCount;
        PlayerTargetScanResult result = c.CompleteScan(c.TraderA);
        c.Demand.TryGetAuthoritativeManifestSnapshot(c.TraderA, out NpcCargoManifestSnapshot after);
        return Check(result != null && keyBefore == ManifestKey(after) &&
            reputationBefore == c.Reputation.GetStanding(c.TraderA.FactionId) &&
            resolvedBefore == c.Demand.ResolvedTargetCount, "scan mutated cargo or piracy state");
    }

    private static (bool, string) NonTraderHasNoFabricatedCargo()
    {
        Context c = new();
        NpcShip rogue = new("Phase58 Rogue", new Vector3(1200f, 0f, 0f), Vector3.Zero, 600f, 0.1f, FactionManager.LibertyRogues);
        rogue.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase58-rogue", Vector3.Zero, 600f, 160f, 5000f);
        c.AddNpc(rogue);
        PlayerTargetScanResult result = c.CompleteScan(rogue);
        return Check(result != null && !result.HasRegisteredCargo && result.Cargo.Count == 0, "non-trader received fabricated cargo");
    }

    private static (bool, string) RegisteredEmptyCargoReportsEmpty()
    {
        Context c = new();
        PlayerTargetScanService scanner = new(
            c.Npcs,
            c.Factions,
            _ => new NpcCargoManifestSnapshot(hasRegisteredCargo: true));
        scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        scanner.Update(PlayerTargetScanService.ScanDurationSeconds, c.Player, c.TraderA, false);
        PlayerTargetScanResult result = scanner.LastResult;
        return Check(result != null && result.HasRegisteredCargo && result.IsCargoHoldEmpty &&
            result.CargoStatusLabel == "Cargo hold empty", "registered empty cargo was treated as missing or failed scanning");
    }

    private static (bool, string) PoliceScanIsLegal()
    {
        Context c = new();
        NpcShip police = new("Phase58 Police", new Vector3(1200f, 0f, 0f), Vector3.Zero, 600f, 0.1f, FactionManager.LibertyPolice);
        police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase58-police", Vector3.Zero, 600f, 160f, 5000f);
        c.AddNpc(police);
        float before = c.Reputation.GetStanding(police.FactionId);
        PlayerTargetScanResult result = c.CompleteScan(police);
        return Check(result != null && c.Reputation.GetStanding(police.FactionId) == before, "Police scan changed reputation");
    }

    private static (bool, string) ContrabandUsesCanonicalMetadata()
    {
        Context c = new();
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        PlayerTargetScanService scanner = new(
            c.Npcs,
            c.Factions,
            _ => new NpcCargoManifestSnapshot(true, new[] { new NpcCargoManifestStackSnapshot(contraband, 2) }));
        scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        scanner.Update(PlayerTargetScanService.ScanDurationSeconds, c.Player, c.TraderA, false);
        PlayerTargetScanCargoEntry entry = scanner.LastResult?.Cargo.SingleOrDefault();
        return Check(entry?.IsContraband == contraband.IsContraband && entry?.CommodityId == contraband.Id, "contraband label was not derived from canonical metadata");
    }

    private static (bool, string) CargoValueUsesCanonicalBasePrice()
    {
        Context c = new();
        PlayerTargetScanResult result = c.CompleteScan(c.TraderA);
        int expected = result.Cargo.Sum(entry => entry.Commodity.BasePrice * entry.Quantity);
        return Check(result.EstimatedCargoValue == expected, "cargo value did not use canonical base price");
    }

    private static (bool, string) RescanRefreshesCurrentSnapshot()
    {
        Context c = new();
        PlayerTargetScanResult first = c.CompleteScan(c.TraderA);
        c.Demand.TryGetAuthoritativeManifestSnapshot(c.TraderA, out NpcCargoManifestSnapshot current);
        c.Scanner.NotifyTargetChanged(c.TraderA);
        PlayerTargetScanResult second = c.CompleteScan(c.TraderA);
        return Check(first != null && second != null &&
            ManifestKey(current) == ScanKey(second), "rescan did not refresh current authoritative cargo");
    }

    private static (bool, string) DemandConsumesScannedManifest()
    {
        Context c = new();
        PlayerTargetScanResult beforeScan = c.CompleteScan(c.TraderA);
        int before = beforeScan.Cargo.Sum(entry => entry.Quantity);
        c.TraderA.ApplyDamage(55f, NpcDestructionSource.Unknown);
        bool demanded = c.Demand.TryIssueDemand(c.Player, c.TraderA, out string demandFailure);
        c.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds + 0.1f, c.Player);
        int surrendered = c.Demand.LastResult?.SurrenderedQuantity ?? 0;
        c.Scanner.NotifyTargetChanged(c.TraderA);
        PlayerTargetScanResult afterScan = c.CompleteScan(c.TraderA);
        int after = afterScan?.Cargo.Sum(entry => entry.Quantity) ?? -1;
        return Check(demanded && string.IsNullOrEmpty(demandFailure) && surrendered > 0 &&
            surrendered <= before && after == before - surrendered, "demand did not consume the scanned underlying manifest");
    }

    private static (bool, string) SurrenderedPodRetainsStolenProvenance()
    {
        Context c = new();
        c.TraderA.ApplyDamage(55f, NpcDestructionSource.Unknown);
        if (!c.Demand.TryIssueDemand(c.Player, c.TraderA, out _))
            return Fail("demand could not be issued");
        c.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds + 0.1f, c.Player);
        return Check(c.Loot.ActivePods.Any(pod => pod.IsStolen && pod.SourceNpcName == c.TraderA.Name), "surrendered pod lost Phase 57 stolen provenance");
    }

    private static (bool, string) DestroyedTargetCancelsScan()
    {
        Context c = new();
        c.Scanner.TryStartScan(c.Player, c.TraderA, false, out _);
        c.TraderA.ApplyDamage(1000f, NpcDestructionSource.Player);
        c.Scanner.NotifyTargetDestroyed(c.TraderA);
        return Check(!c.Scanner.HasActiveScan && c.Scanner.LastResult == null, "destroyed target completed or retained a scan");
    }

    private static (bool, string) ResetClearsTransientIntelligence()
    {
        Context c = new();
        c.CompleteScan(c.TraderA);
        c.Scanner.Reset();
        return Check(!c.Scanner.HasActiveScan && c.Scanner.LastResult == null &&
            string.IsNullOrEmpty(c.Scanner.FeedbackText), "reset retained transient scan state");
    }

    private static string ManifestKey(NpcCargoManifestSnapshot snapshot) => snapshot == null
        ? string.Empty
        : string.Join(",", snapshot.Stacks.Select(stack => $"{stack.Commodity.Id}:{stack.Quantity}"));

    private static string ScanKey(PlayerTargetScanResult result) => result == null
        ? string.Empty
        : string.Join(",", result.Cargo.Select(entry => $"{entry.CommodityId}:{entry.Quantity}"));

    private static (bool Success, string FailureReason) Check(bool condition, string reason) =>
        condition ? (true, string.Empty) : (false, reason);

    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
}
