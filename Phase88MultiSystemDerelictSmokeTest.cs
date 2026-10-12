using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Phase 88 focused coverage for multi-system derelict exploration.
/// The suite drives only production authorities: the curated
/// <see cref="DerelictCatalog"/> (now two sites), the shared
/// <see cref="DerelictRecoveryService"/>, the existing
/// <see cref="LootManager"/>/<see cref="CargoPod"/> physical loot path,
/// <see cref="CargoHold"/> capacity semantics, production
/// <see cref="Configuration.ConfigurationManager"/> geometry, and the
/// <see cref="SaveGameData"/> JSON contract. It proves the California site
/// is discoverable by proximity in California only, releases its own finite
/// physical salvage exactly once through existing loot systems, keeps fully
/// independent durable recovery state per stable site id, survives
/// save/load, and leaves NPC salvage, piracy, Police, trade-lane, and
/// mission systems untouched. No second exploration framework, loot pool,
/// mission, or inventory authority is introduced.
/// </summary>
internal sealed class Phase88MultiSystemDerelictSmokeTest
{
    private const string Tag = "[PHASE 88 MULTI-SYSTEM DERELICT SMOKE]";

    public Phase88MultiSystemDerelictSmokeTest(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        // Catalog and identity (spec gates 1-8).
        RunCase("exactly two intended production sites exist", TwoProductionSitesExist, ref passed, ref failed);
        RunCase("New York site identity and content are unchanged", NewYorkSiteUnchanged, ref passed, ref failed);
        RunCase("second site id is unique", SecondSiteIdUnique, ref passed, ref failed);
        RunCase("both site ids are stable", StableIdentities, ref passed, ref failed);
        RunCase("site catalog lookup is deterministic", DeterministicLookup, ref passed, ref failed);
        RunCase("catalog reordering cannot swap recovery identity", ReorderingCannotSwapIdentity, ref passed, ref failed);
        RunCase("unknown site id is rejected", UnknownSiteIdRejected, ref passed, ref failed);
        RunCase("duplicate site id is safely prevented", DuplicateSiteIdPrevented, ref passed, ref failed);
        // Placement (spec gates 9-16).
        RunCase("California site belongs to a real system", CaliforniaSiteInRealSystem, ref passed, ref failed);
        RunCase("California is an accessible system", CaliforniaAccessible, ref passed, ref failed);
        RunCase("California position is deterministic", PositionDeterministic, ref passed, ref failed);
        RunCase("California site is clear of docking geometry", ClearOfDocking, ref passed, ref failed);
        RunCase("California site is clear of trade-lane corridors", ClearOfTradeLanes, ref passed, ref failed);
        RunCase("California site is clear of jump transitions", ClearOfJumpTransitions, ref passed, ref failed);
        RunCase("California site is clear of solid collision geometry", ClearOfCollisionGeometry, ref passed, ref failed);
        RunCase("California site is inside ordinary playable space", InsidePlayableSpace, ref passed, ref failed);
        // Discovery (spec gates 17-24).
        RunCase("New York site is only detectable in New York", NewYorkOnlyInNewYork, ref passed, ref failed);
        RunCase("California site is only detectable in California", CaliforniaOnlyInCalifornia, ref passed, ref failed);
        RunCase("wrong-system interaction is rejected both ways", WrongSystemInteractionRejected, ref passed, ref failed);
        RunCase("California detection threshold is correct", DetectionThreshold, ref passed, ref failed);
        RunCase("California identification threshold is correct", IdentificationThreshold, ref passed, ref failed);
        RunCase("California recovery threshold is correct", RecoveryThreshold, ref passed, ref failed);
        RunCase("valid California investigation succeeds", ValidInvestigationSucceeds, ref passed, ref failed);
        RunCase("holding F cannot duplicate California recovery", HoldingKeyCannotDuplicate, ref passed, ref failed);
        // Salvage (spec gates 25-35).
        RunCase("New York salvage remains unchanged", NewYorkSalvageUnchanged, ref passed, ref failed);
        RunCase("California site uses canonical commodities", CanonicalCommodities, ref passed, ref failed);
        RunCase("California quantities are exact", ExactQuantities, ref passed, ref failed);
        RunCase("correct site determines the salvage manifest", CorrectSiteDeterminesManifest, ref passed, ref failed);
        RunCase("existing LootManager creates the physical pods", LootManagerOwnsPods, ref passed, ref failed);
        RunCase("California cargo is not teleported into the hold", NoTeleport, ref passed, ref failed);
        RunCase("pod capacity limits are honored", CapacityLimitsHonored, ref passed, ref failed);
        RunCase("spawn failure remains atomic", SpawnFailureAtomic, ref passed, ref failed);
        RunCase("California normal pickup works", NormalPickup, ref passed, ref failed);
        RunCase("California partial pickup works", PartialPickup, ref passed, ref failed);
        RunCase("repeated interaction releases no duplicate pods", NoDuplicatesOnRepeat, ref passed, ref failed);
        // Recovery independence (spec gates 36-42).
        RunCase("neither recovered leaves both available", NeitherRecoveredBothAvailable, ref passed, ref failed);
        RunCase("New York recovered leaves California available", NyRecoveredCaAvailable, ref passed, ref failed);
        RunCase("California recovered leaves New York available", CaRecoveredNyAvailable, ref passed, ref failed);
        RunCase("both recovered releases nothing again", BothRecoveredNeitherReleases, ref passed, ref failed);
        RunCase("cross-system travel preserves recovery state", CrossSystemNyFirst, ref passed, ref failed);
        RunCase("reverse-order travel preserves recovery state", CrossSystemCaFirst, ref passed, ref failed);
        RunCase("recovery is keyed by canonical site id", KeyedByCanonicalId, ref passed, ref failed);
        RunCase("wrong system cannot mutate remote recovery state", RemoteStateImmutability, ref passed, ref failed);
        // Persistence (spec gates 43-50).
        RunCase("both unrecovered round-trip correctly", BothUnrecoveredRoundTrip, ref passed, ref failed);
        RunCase("New York-only recovered round-trips", NyOnlyRoundTrip, ref passed, ref failed);
        RunCase("California-only recovered round-trips", CaOnlyRoundTrip, ref passed, ref failed);
        RunCase("both recovered round-trip", BothRecoveredRoundTrip, ref passed, ref failed);
        RunCase("outstanding salvage follows existing pod semantics", OutstandingPodsSemantics, ref passed, ref failed);
        RunCase("collected salvage remains player-owned", CollectedRemainsOwned, ref passed, ref failed);
        RunCase("legacy saves load safely with both available", LegacyLoadsSafely, ref passed, ref failed);
        RunCase("save schema remains compatible", SchemaCompatible, ref passed, ref failed);
        // Lifecycle and regressions (spec gates 51-64).
        RunCase("player death cannot duplicate salvage", DeathCannotDuplicate, ref passed, ref failed);
        RunCase("dock and undock cannot duplicate salvage", DockUndockCannotDuplicate, ref passed, ref failed);
        RunCase("system transition clears transient UI references", TransitionClearsTransientOnly, ref passed, ref failed);
        RunCase("full cargo hold preserves normal pickup behavior", FullHoldPreserves, ref passed, ref failed);
        RunCase("NPC destruction salvage is unchanged", NpcSalvageUnchanged, ref passed, ref failed);
        RunCase("equipment salvage is unchanged", EquipmentSalvageUnchanged, ref passed, ref failed);
        RunCase("pirate salvage path is unchanged", PirateSalvageUnchanged, ref passed, ref failed);
        RunCase("Police systems are unchanged", PoliceUnchanged, ref passed, ref failed);
        RunCase("trade-lane systems are unchanged", TradeLaneUnchanged, ref passed, ref failed);
        RunCase("mission and freight systems are unchanged", MissionFreightUnchanged, ref passed, ref failed);
        RunCase("discovery work stays system-local and side-effect free", SystemLocalBounded, ref passed, ref failed);
        RunCase("no parallel inventory authority exists", NoParallelInventory, ref passed, ref failed);
        RunCase("no parallel recovery-state registry exists", NoParallelRecoveryRegistry, ref passed, ref failed);
        // Architecture-specific.
        RunCase("routing is catalog-driven per system", CatalogDrivenRouting, ref passed, ref failed);
        RunCase("both wrecks are plain non-NPC world objects", WreckWorldObjects, ref passed, ref failed);

        Console.WriteLine($"{Tag} RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(string label, Func<(bool, string)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"{Tag} PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"{Tag} FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"{Tag} FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) Check(bool condition, string reason) => (condition, condition ? string.Empty : reason);

    // ---- Harness ------------------------------------------------------------

    private sealed class MultiSiteContext
    {
        public DerelictRecoveryService Service { get; } = new();
        public LootManager Loot { get; } = new LootManager(null, new Random(8801));
        public DerelictSite NySite { get; } = DerelictCatalog.GetById(DerelictCatalog.FirstSiteId);
        public DerelictSite CaSite { get; } = DerelictCatalog.GetById(DerelictCatalog.SecondSiteId);
        public Ship PlayerAtNy => CreatePlayer(NySite.Position);
        public Ship PlayerAtCa => CreatePlayer(CaSite.Position);
    }

    private static DerelictSite NySite() => DerelictCatalog.GetById(DerelictCatalog.FirstSiteId);

    private static DerelictSite CaSite() => DerelictCatalog.GetById(DerelictCatalog.SecondSiteId);

    private static Ship CreatePlayer(Vector3 position)
    {
        return RunSilenced(() => new Ship(position));
    }

    private static GameTime Frame(float seconds)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
        return new GameTime(elapsed, elapsed);
    }

    private static Vector3 OffsetFromSite(DerelictSite site, float meters)
    {
        return site.Position + new Vector3(meters, 0f, 0f);
    }

    private static void DestroyPlayer(Ship player)
    {
        RunSilenced(() => player.Hull.TakeDamage(player.Hull.CurrentHull + 1f));
    }

    private static NpcShip CreateTrader(string name, Vector3 position)
    {
        return RunSilenced(() =>
        {
            var npc = new NpcShip(name, position, Vector3.Zero, 1f, 0f, "neutral_civilians");
            npc.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "derelict88-smoke", Vector3.Zero, 500f, 100f);
            return npc;
        });
    }

    private static void DestroyNpc(NpcShip npc)
    {
        RunSilenced(() =>
        {
            if (npc.Shields.CurrentShields > 0f)
                npc.Shields.AbsorbDamage(npc.Shields.CurrentShields);
            npc.Hull.TakeDamage(npc.Hull.CurrentHull + 1f);
        });
    }

    private static void InjectPod(LootManager loot, CargoPod pod)
    {
        if (loot?.ActivePods is List<CargoPod> pods && pod != null)
        {
            pods.Add(pod);
            return;
        }

        throw new InvalidOperationException("could not access bounded salvage pool");
    }

    private static Configuration.ConfigurationManager LoadProductionConfig()
    {
        return RunSilenced(() =>
        {
            var config = new Configuration.ConfigurationManager();
            config.LoadAll();
            return config;
        });
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);

    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter originalOut = Console.Out;
        using var sink = new StringWriter();
        Console.SetOut(sink);
        try
        {
            return action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static void RunSilenced(Action action)
    {
        TextWriter originalOut = Console.Out;
        using var sink = new StringWriter();
        Console.SetOut(sink);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // ---- Catalog and identity (gates 1-8) -----------------------------------

    private static (bool, string) TwoProductionSitesExist()
    {
        IReadOnlyCollection<DerelictSite> sites = DerelictCatalog.Sites;
        bool both = sites.Count == 2 &&
            sites.Any(site => site.SiteId == DerelictCatalog.FirstSiteId) &&
            sites.Any(site => site.SiteId == DerelictCatalog.SecondSiteId);
        return Check(both, $"catalog held {sites.Count} sites instead of the two curated wrecks");
    }

    private static (bool, string) NewYorkSiteUnchanged()
    {
        DerelictSite site = NySite();
        if (site == null)
            return Fail("New York derelict site was missing from the catalog");
        var lines = site.Salvage.OrderBy(line => line.CommodityId, StringComparer.OrdinalIgnoreCase).ToList();
        bool identity = site.SiteId == "derelict_ny_manhattan_drift" && site.SystemIndex == 1;
        bool position = site.Position == new Vector3(-14000f, 500f, -8000f);
        bool ranges = site.DetectionRange == 6000f && site.IdentificationRange == 3000f && site.RecoveryRange == 600f;
        bool manifest = lines.Count == 3 &&
            lines[0].CommodityId == "boron" && lines[0].Quantity == 6 &&
            lines[1].CommodityId == "construction-materials" && lines[1].Quantity == 2 &&
            lines[2].CommodityId == "consumer-goods" && lines[2].Quantity == 4;
        return Check(identity && position && ranges && manifest,
            $"New York site changed: {site.SiteId} at {site.Position} with {lines.Count} salvage lines");
    }

    private static (bool, string) SecondSiteIdUnique()
    {
        DerelictSite ny = NySite();
        DerelictSite ca = CaSite();
        return Check(ny != null && ca != null && !ReferenceEquals(ny, ca) &&
            !string.Equals(ny.SiteId, ca.SiteId, StringComparison.OrdinalIgnoreCase) &&
            ca.SiteId == "derelict_ca_hesperia_verge",
            "second site id was missing, duplicated, or unstable");
    }

    private static (bool, string) StableIdentities()
    {
        DerelictSite ny = DerelictCatalog.GetById(DerelictCatalog.FirstSiteId);
        DerelictSite nyUpper = DerelictCatalog.GetById(DerelictCatalog.FirstSiteId.ToUpperInvariant());
        DerelictSite ca = DerelictCatalog.GetById(DerelictCatalog.SecondSiteId);
        DerelictSite caUpper = DerelictCatalog.GetById(DerelictCatalog.SecondSiteId.ToUpperInvariant());
        return Check(ReferenceEquals(ny, nyUpper) && ReferenceEquals(ca, caUpper) &&
            ReferenceEquals(ny, NySite()) && ReferenceEquals(ca, CaSite()),
            "site identity was not a stable case-insensitive catalog reference");
    }

    private static (bool, string) DeterministicLookup()
    {
        bool byId = ReferenceEquals(DerelictCatalog.GetById(DerelictCatalog.FirstSiteId), NySite()) &&
            ReferenceEquals(DerelictCatalog.GetById(DerelictCatalog.SecondSiteId), CaSite());
        IReadOnlyList<DerelictSite> nyAgain = DerelictCatalog.GetForSystem(1);
        IReadOnlyList<DerelictSite> caAgain = DerelictCatalog.GetForSystem(2);
        bool bySystem = nyAgain.Count == 1 && ReferenceEquals(nyAgain[0], NySite()) &&
            caAgain.Count == 1 && ReferenceEquals(caAgain[0], CaSite());
        return Check(byId && bySystem, "catalog lookup returned unstable or misrouted references");
    }

    private static (bool, string) ReorderingCannotSwapIdentity()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        if (!nyResult.Success)
            return Fail($"New York recovery failed: {nyResult.FailureReason}");
        // Simulate a catalog reorder by restoring the durable ids in reverse
        // order: recovery state must still attach to the same canonical ids.
        List<string> captured = ctx.Service.CaptureState();
        captured.Reverse();
        var reordered = new DerelictRecoveryService();
        reordered.RestoreState(captured);
        return Check(reordered.IsRecovered(DerelictCatalog.FirstSiteId) &&
            !reordered.IsRecovered(DerelictCatalog.SecondSiteId) &&
            ReferenceEquals(DerelictCatalog.GetById(DerelictCatalog.FirstSiteId), ctx.NySite),
            "recovery identity followed list position instead of the stable site id");
    }

    private static (bool, string) UnknownSiteIdRejected()
    {
        var ctx = new MultiSiteContext();
        DerelictSite unknown = DerelictCatalog.GetById("derelict_zz_nowhere_drift");
        DerelictSite blank = DerelictCatalog.GetById("   ");
        Ship player = ctx.PlayerAtCa;
        bool canRecover = ctx.Service.CanRecover(unknown, player.Position, 2, true, false, false, out string reason);
        DerelictRecoveryResult result = ctx.Service.TryRecover(unknown, player.Position, 2, true, false, false, ctx.Loot);
        return Check(unknown == null && blank == null && !canRecover && !result.Success &&
            reason == "no derelict in range" && ctx.Loot.ActivePods.Count == 0,
            $"unknown site id was not rejected cleanly ('{reason}')");
    }

    private static (bool, string) DuplicateSiteIdPrevented()
    {
        List<string> ids = DerelictCatalog.Sites
            .Select(site => site.SiteId.ToLowerInvariant())
            .ToList();
        bool distinct = ids.Count == 2 && ids.Distinct().Count() == 2;
        bool selfConsistent = DerelictCatalog.Sites.All(site =>
            ReferenceEquals(DerelictCatalog.GetById(site.SiteId), site) &&
            !string.IsNullOrWhiteSpace(site.SiteId));
        return Check(distinct && selfConsistent, "catalog allowed duplicate or unresolvable site ids");
    }

    // ---- Placement (gates 9-16) ---------------------------------------------

    private static (bool, string) CaliforniaSiteInRealSystem()
    {
        DerelictSite site = CaSite();
        if (site == null)
            return Fail("California derelict site was missing from the catalog");
        Configuration.ConfigurationManager config = LoadProductionConfig();
        Configuration.SystemConfig california = config.Systems
            .FirstOrDefault(system => system != null && system.SystemIndex == 2);
        bool real = california != null && california.CanonicalId == "california";
        bool routed = site.SystemIndex == 2 &&
            DerelictCatalog.GetForSystem(2).Any(candidate => ReferenceEquals(candidate, site));
        return Check(real && routed,
            $"California system was '{california?.CanonicalId}' and site system was {site.SystemIndex}");
    }

    private static (bool, string) CaliforniaAccessible()
    {
        Configuration.ConfigurationManager config = LoadProductionConfig();
        bool outbound = config.JumpHoles.Any(hole => hole != null && hole.SystemIndex == 1 && hole.TargetSystemIndex == 2);
        bool inbound = config.JumpHoles.Any(hole => hole != null && hole.SystemIndex == 2 && hole.TargetSystemIndex == 1);
        return Check(outbound && inbound, "production jump topology lacks a two-way New York/California connection");
    }

    private static (bool, string) PositionDeterministic()
    {
        DerelictSite site = CaSite();
        bool position = site != null && site.Position == new Vector3(15000f, 500f, -18000f);
        bool ranges = site != null && site.DetectionRange == 6000f &&
            site.IdentificationRange == 3000f && site.RecoveryRange == 600f;
        return Check(position && ranges,
            $"California site at {site?.Position} with ranges {site?.DetectionRange}/{site?.IdentificationRange}/{site?.RecoveryRange}");
    }

    private static (bool, string) ClearOfDocking()
    {
        DerelictSite site = CaSite();
        Configuration.ConfigurationManager config = LoadProductionConfig();
        List<Vector3> stations = config.Stations
            .Where(station => station != null && station.SystemIndex == 2)
            .Select(station => station.StartupPosition)
            .ToList();
        if (stations.Count == 0)
            return Fail("production config exposed no California stations to clear");
        float nearest = stations.Min(station => Vector3.Distance(site.Position, station));
        // Docking ranges are at most 600m and station radii at most 900m;
        // the curated verge keeps several kilometers of margin.
        return Check(nearest > 5000f,
            $"nearest California station was only {nearest:F0}m away");
    }

    private static (bool, string) ClearOfTradeLanes()
    {
        string[] files = Directory.GetFiles("Configuration/tradelanes", "*.json");
        if (files.Length < 6)
            return Fail($"expected at least 6 tradelane files, found {files.Length}");
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            if (text.Contains("\"system_index\": 2", StringComparison.Ordinal))
                return Fail($"tradelane file {Path.GetFileName(file)} declares California system 2");
        }
        return Pass();
    }

    private static (bool, string) ClearOfJumpTransitions()
    {
        DerelictSite site = CaSite();
        Configuration.ConfigurationManager config = LoadProductionConfig();
        List<Vector3> holes = config.JumpHoles
            .Where(hole => hole != null && hole.SystemIndex == 2)
            .Select(hole => hole.Position)
            .ToList();
        if (holes.Count == 0)
            return Fail("production config exposed no California jump holes to clear");
        float nearest = holes.Min(hole => Vector3.Distance(site.Position, hole));
        // Jump-hole activation ranges are at most 180m with 300m rings; the
        // verge keeps the New York arrival corridor ~12km away.
        return Check(nearest > 5000f,
            $"nearest California jump hole was only {nearest:F0}m away");
    }

    private static (bool, string) ClearOfCollisionGeometry()
    {
        DerelictSite site = CaSite();
        // California planets (Configuration/systems/system_02_california.json).
        var planets = new (Vector3 Center, float Radius)[]
        {
            (new Vector3(-6500f, 0f, 0f), 170f),
            (new Vector3(6000f, 0f, -6000f), 130f),
            (new Vector3(1500f, 0f, 8000f), 90f),
        };
        float nearestPlanet = planets.Min(planet => Vector3.Distance(site.Position, planet.Center) - planet.Radius);
        Configuration.ConfigurationManager config = LoadProductionConfig();
        float nearestStationBody = config.Stations
            .Where(station => station != null && station.SystemIndex == 2)
            .Min(station => Vector3.Distance(site.Position, station.StartupPosition) - station.Radius);
        return Check(nearestPlanet > 5000f && nearestStationBody > 5000f,
            $"clearances were planet {nearestPlanet:F0}m, station body {nearestStationBody:F0}m");
    }

    private static (bool, string) InsidePlayableSpace()
    {
        DerelictSite site = CaSite();
        bool finite = site != null &&
            float.IsFinite(site.Position.X) && float.IsFinite(site.Position.Y) && float.IsFinite(site.Position.Z);
        // The live world builds grid markers across +/-20000 on X/Z; the
        // verge sits inside that ordinary flight volume, off the Y=0 plane
        // like the Manhattan Drift site.
        bool inside = finite &&
            Math.Abs(site.Position.X) <= 20000f && Math.Abs(site.Position.Z) <= 20000f &&
            site.Position.Y == 500f;
        return Check(inside, $"California site at {site?.Position} was outside ordinary flight volume");
    }

    // ---- Discovery (gates 17-24) --------------------------------------------

    private static (bool, string) NewYorkOnlyInNewYork()
    {
        var ctx = new MultiSiteContext();
        Ship atNy = CreatePlayer(ctx.NySite.Position);
        DerelictPresentation home = ctx.Service.GetPresentation(ctx.NySite, atNy.Position, 1, true, false);
        DerelictPresentation away = ctx.Service.GetPresentation(ctx.NySite, atNy.Position, 2, true, false);
        string awayHud = ctx.Service.GetHudText(ctx.NySite, atNy.Position, 2, true, false);
        bool partitioned = DerelictCatalog.GetForSystem(1).All(site => site.SystemIndex == 1) &&
            !DerelictCatalog.GetForSystem(2).Any(site => site.SiteId == ctx.NySite.SiteId);
        return Check(home == DerelictPresentation.ReadyToInvestigate &&
            away == DerelictPresentation.Hidden && awayHud == null && partitioned,
            $"New York presentation was home={home}, away={away} with hud '{awayHud}'");
    }

    private static (bool, string) CaliforniaOnlyInCalifornia()
    {
        var ctx = new MultiSiteContext();
        Ship atCa = CreatePlayer(ctx.CaSite.Position);
        DerelictPresentation home = ctx.Service.GetPresentation(ctx.CaSite, atCa.Position, 2, true, false);
        DerelictPresentation away = ctx.Service.GetPresentation(ctx.CaSite, atCa.Position, 1, true, false);
        string awayHud = ctx.Service.GetHudText(ctx.CaSite, atCa.Position, 1, true, false);
        bool partitioned = DerelictCatalog.GetForSystem(2).All(site => site.SystemIndex == 2) &&
            !DerelictCatalog.GetForSystem(1).Any(site => site.SiteId == ctx.CaSite.SiteId);
        return Check(home == DerelictPresentation.ReadyToInvestigate &&
            away == DerelictPresentation.Hidden && awayHud == null && partitioned,
            $"California presentation was home={home}, away={away} with hud '{awayHud}'");
    }

    private static (bool, string) WrongSystemInteractionRejected()
    {
        var ctx = new MultiSiteContext();
        Ship atCa = ctx.PlayerAtCa;
        Ship atNy = ctx.PlayerAtNy;
        bool caInNy = ctx.Service.CanRecover(ctx.CaSite, atCa.Position, 1, true, false, false, out string caReason);
        bool nyInCa = ctx.Service.CanRecover(ctx.NySite, atNy.Position, 2, true, false, false, out string nyReason);
        DerelictRecoveryResult caAttempt = ctx.Service.TryRecover(ctx.CaSite, atCa.Position, 1, true, false, false, ctx.Loot);
        return Check(!caInNy && caReason == "derelict is in another system" &&
            !nyInCa && nyReason == "derelict is in another system" &&
            !caAttempt.Success && ctx.Loot.ActivePods.Count == 0 &&
            !ctx.Service.IsRecovered(ctx.CaSite.SiteId) && !ctx.Service.IsRecovered(ctx.NySite.SiteId),
            $"wrong-system gates returned '{caReason}' / '{nyReason}'");
    }

    private static (bool, string) DetectionThreshold()
    {
        var ctx = new MultiSiteContext();
        Ship inside = CreatePlayer(OffsetFromSite(ctx.CaSite, 5000f));
        Ship outside = CreatePlayer(OffsetFromSite(ctx.CaSite, 9000f));
        DerelictPresentation near = ctx.Service.GetPresentation(ctx.CaSite, inside.Position, 2, true, false);
        string nearHud = ctx.Service.GetHudText(ctx.CaSite, inside.Position, 2, true, false);
        DerelictPresentation far = ctx.Service.GetPresentation(ctx.CaSite, outside.Position, 2, true, false);
        string farHud = ctx.Service.GetHudText(ctx.CaSite, outside.Position, 2, true, false);
        return Check(near == DerelictPresentation.UnidentifiedSignal && nearHud == "Unidentified signal detected" &&
            far == DerelictPresentation.Hidden && farHud == null,
            $"detection was near={near} '{nearHud}', far={far} '{farHud}'");
    }

    private static (bool, string) IdentificationThreshold()
    {
        var ctx = new MultiSiteContext();
        Ship identified = CreatePlayer(OffsetFromSite(ctx.CaSite, 2000f));
        Ship ready = CreatePlayer(OffsetFromSite(ctx.CaSite, 100f));
        DerelictPresentation identifiedPresentation = ctx.Service.GetPresentation(ctx.CaSite, identified.Position, 2, true, false);
        string identifiedHud = ctx.Service.GetHudText(ctx.CaSite, identified.Position, 2, true, false);
        DerelictPresentation readyPresentation = ctx.Service.GetPresentation(ctx.CaSite, ready.Position, 2, true, false);
        string readyHud = ctx.Service.GetHudText(ctx.CaSite, ready.Position, 2, true, false);
        return Check(identifiedPresentation == DerelictPresentation.IdentifiedWreck && identifiedHud == "Abandoned spacecraft" &&
            readyPresentation == DerelictPresentation.ReadyToInvestigate && readyHud == "Abandoned spacecraft - Press F to investigate",
            $"identification was {identifiedPresentation} '{identifiedHud}', recovery prompt was {readyPresentation} '{readyHud}'");
    }

    private static (bool, string) RecoveryThreshold()
    {
        var ctx = new MultiSiteContext();
        Ship beyond = CreatePlayer(OffsetFromSite(ctx.CaSite, 1000f));
        bool canRecover = ctx.Service.CanRecover(ctx.CaSite, beyond.Position, 2, true, false, false, out string reason);
        DerelictRecoveryResult failed = ctx.Service.TryRecover(ctx.CaSite, beyond.Position, 2, true, false, false, ctx.Loot);
        return Check(!canRecover && !failed.Success && reason == "derelict out of range" && ctx.Loot.ActivePods.Count == 0,
            $"recovery gate at 1000m returned {failed.Success} with '{reason}'");
    }

    private static (bool, string) ValidInvestigationSucceeds()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        bool canRecover = ctx.Service.CanRecover(ctx.CaSite, player.Position, 2, true, false, false, out string gateReason);
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        return Check(canRecover && result.Success && result.SpawnedPods.Count == 3 && ctx.Service.IsRecovered(ctx.CaSite.SiteId),
            $"valid California recovery returned {result.Success} '{result.FailureReason}' with {result.SpawnedPods.Count} pods (gate '{gateReason}')");
    }

    private static (bool, string) HoldingKeyCannotDuplicate()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult first = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        DerelictRecoveryResult second = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        DerelictRecoveryResult third = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        int total = ctx.Loot.ActivePods.Sum(pod => pod.Quantity);
        return Check(first.Success && !second.Success && !third.Success &&
            ctx.Loot.ActivePods.Count == 3 && total == 12,
            $"repeated California recovery produced {ctx.Loot.ActivePods.Count} pods totaling {total}");
    }

    // ---- Salvage (gates 25-35) ------------------------------------------------

    private static (bool, string) NewYorkSalvageUnchanged()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        var byId = result.SpawnedPods.ToDictionary(pod => pod.CommodityId.ToLowerInvariant(), pod => pod.Quantity);
        return Check(result.Success && byId.Count == 3 && byId["boron"] == 6 &&
            byId["construction-materials"] == 2 && byId["consumer-goods"] == 4,
            "New York salvage manifest changed");
    }

    private static (bool, string) CanonicalCommodities()
    {
        DerelictSite site = CaSite();
        var lines = site.Salvage.OrderBy(line => line.CommodityId, StringComparer.OrdinalIgnoreCase).ToList();
        bool canonical = lines.Count == 3 && lines.All(line => CommodityCatalog.GetById(line.CommodityId) != null);
        bool policy = lines.All(line =>
        {
            Commodity commodity = CommodityCatalog.GetById(line.CommodityId);
            return commodity != null && !commodity.IsContraband && !commodity.IsMissionCargo && line.Quantity > 0;
        });
        return Check(canonical && policy, "California salvage lines were not canonical Clean cargo");
    }

    private static (bool, string) ExactQuantities()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        var byId = result.SpawnedPods.ToDictionary(pod => pod.CommodityId.ToLowerInvariant(), pod => pod.Quantity);
        bool exact = result.Success && byId.Count == 3 && byId["food-rations"] == 5 &&
            byId["water"] == 4 && byId["medical-supplies"] == 3;
        bool provenance = result.Success && result.SpawnedPods.All(pod =>
            pod.Provenance == CargoProvenance.Clean && !pod.IsStolen && pod.MissionId == 0);
        return Check(exact && provenance, "California released pods did not match the defined salvage exactly");
    }

    private static (bool, string) CorrectSiteDeterminesManifest()
    {
        var nyCtx = new MultiSiteContext();
        var caCtx = new MultiSiteContext();
        DerelictRecoveryResult nyResult = nyCtx.Service.TryRecover(nyCtx.NySite, nyCtx.PlayerAtNy.Position, 1, true, false, false, nyCtx.Loot);
        DerelictRecoveryResult caResult = caCtx.Service.TryRecover(caCtx.CaSite, caCtx.PlayerAtCa.Position, 2, true, false, false, caCtx.Loot);
        if (!nyResult.Success || !caResult.Success)
            return Fail($"site recoveries failed: '{nyResult.FailureReason}' / '{caResult.FailureReason}'");
        var nyIds = nyResult.SpawnedPods.Select(pod => pod.CommodityId.ToLowerInvariant()).OrderBy(id => id).ToList();
        var caIds = caResult.SpawnedPods.Select(pod => pod.CommodityId.ToLowerInvariant()).OrderBy(id => id).ToList();
        bool disjoint = !nyIds.Intersect(caIds).Any();
        return Check(disjoint && nyIds.Count == 3 && caIds.Count == 3,
            $"site manifests overlapped: NY [{string.Join(",", nyIds)}] vs CA [{string.Join(",", caIds)}]");
    }

    private static (bool, string) LootManagerOwnsPods()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        bool owned = result.Success && result.SpawnedPods.Count == 3 &&
            result.SpawnedPods.All(pod => pod is CargoPod && ctx.Loot.ActivePods.Contains(pod));
        return Check(owned, "California salvage was not owned by the existing LootManager pool");
    }

    private static (bool, string) NoTeleport()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        return Check(result.Success && player.CargoHold.UsedCapacity == 0 && ctx.Loot.ActivePods.Count == 3,
            "California release teleported cargo into the hold instead of physical pods");
    }

    private static (bool, string) CapacityLimitsHonored()
    {
        bool bounds = CombatSalvageService.MaxLiveSalvageObjects == 32 &&
            CombatSalvageService.SalvageLifetimeSeconds == 120d &&
            CombatSalvageService.PickupRadius == 200f;
        var ctx = new MultiSiteContext();
        for (int i = 0; i < CombatSalvageService.MaxLiveSalvageObjects; i++)
        {
            if (!CargoPod.TryCreate("food-rations", 1, new Vector3(5000f + i * 100f, 0f, 0f), Vector3.Zero, 120f, 200f, out CargoPod filler))
                return Fail("salvage-cap harness could not be filled");
            InjectPod(ctx.Loot, filler);
        }
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult blocked = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        return Check(bounds && !blocked.Success && blocked.FailureReason == "salvage capacity reached" &&
            ctx.Loot.ActivePods.Count == CombatSalvageService.MaxLiveSalvageObjects,
            $"cap gate reported '{blocked.FailureReason}' without honoring shared loot limits");
    }

    private static (bool, string) SpawnFailureAtomic()
    {
        var ctx = new MultiSiteContext();
        for (int i = 0; i < CombatSalvageService.MaxLiveSalvageObjects; i++)
        {
            if (!CargoPod.TryCreate("food-rations", 1, new Vector3(5000f + i * 100f, 0f, 0f), Vector3.Zero, 120f, 200f, out CargoPod filler))
                return Fail("salvage-cap harness could not be filled");
            InjectPod(ctx.Loot, filler);
        }
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult blocked = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        if (blocked.Success || ctx.Service.IsRecovered(ctx.CaSite.SiteId) || ctx.Loot.ActivePods.Count != CombatSalvageService.MaxLiveSalvageObjects)
            return Fail("blocked California recovery committed depletion or partial pods");
        var retryLoot = new LootManager(null, new Random(8802));
        DerelictRecoveryResult retry = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, retryLoot);
        return Check(!blocked.Success && retry.Success && retryLoot.ActivePods.Count == 3,
            $"blocked recovery reported '{blocked.FailureReason}' and retry released {retryLoot.ActivePods.Count} pods");
    }

    private static (bool, string) NormalPickup()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        bool cargo = player.CargoHold.GetCommodityQuantity("Food Rations") == 5 &&
            player.CargoHold.GetCommodityQuantity("Water") == 4 &&
            player.CargoHold.GetCommodityQuantity("Medical Supplies") == 3;
        // 5x1 + 4x1 + 3x1 = 12 volume in the starter 50-capacity hold.
        return Check(ctx.Loot.ActivePods.Count == 0 && cargo && player.CargoHold.UsedCapacity == 12,
            $"pickup left {ctx.Loot.ActivePods.Count} pods with used capacity {player.CargoHold.UsedCapacity}");
    }

    private static (bool, string) PartialPickup()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        player.CargoHold.SetMaxCapacity(11);
        player.CargoHold.Clear();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.1f), player, tractorActive: false);
        int remaining = ctx.Loot.ActivePods.Sum(pod => pod.Quantity);
        bool partial = player.CargoHold.UsedCapacity == 11 &&
            ctx.Loot.ActivePods.Count == 1 && remaining == 1;
        if (!partial)
            return Fail($"capacity-11 pickup left {ctx.Loot.ActivePods.Count} pods totaling {remaining}");
        player.CargoHold.SetMaxCapacity(50);
        ctx.Loot.Update(Frame(0.1f), player, tractorActive: false);
        bool completed = ctx.Loot.ActivePods.Count == 0 &&
            player.CargoHold.GetCommodityQuantity("Food Rations") == 5 &&
            player.CargoHold.GetCommodityQuantity("Water") == 4 &&
            player.CargoHold.GetCommodityQuantity("Medical Supplies") == 3;
        return Check(completed, "partial pickup could not complete after freeing cargo space");
    }

    private static (bool, string) NoDuplicatesOnRepeat()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult first = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        DerelictRecoveryResult second = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        int total = ctx.Loot.ActivePods.Sum(pod => pod.Quantity);
        return Check(first.Success && !second.Success && second.FailureReason == "derelict already salvaged" &&
            ctx.Loot.ActivePods.Count == 3 && total == 12,
            $"repeat interaction released {ctx.Loot.ActivePods.Count} pods totaling {total}");
    }

    // ---- Recovery independence (gates 36-42) ----------------------------------

    private static (bool, string) NeitherRecoveredBothAvailable()
    {
        var ctx = new MultiSiteContext();
        bool ny = ctx.Service.CanRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, out _);
        bool ca = ctx.Service.CanRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, out _);
        return Check(ny && ca && ctx.Service.RecoveredCount == 0,
            "fresh recovery state did not leave both sites available");
    }

    private static (bool, string) NyRecoveredCaAvailable()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        if (!nyResult.Success)
            return Fail($"New York recovery failed: {nyResult.FailureReason}");
        bool ca = ctx.Service.CanRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, out _);
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, new LootManager(null, new Random(8803)));
        return Check(!ctx.Service.CanRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, out _) &&
            ca && caResult.Success && ctx.Service.IsRecovered(ctx.NySite.SiteId) && ctx.Service.IsRecovered(ctx.CaSite.SiteId),
            "New York recovery mutated California availability");
    }

    private static (bool, string) CaRecoveredNyAvailable()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        if (!caResult.Success)
            return Fail($"California recovery failed: {caResult.FailureReason}");
        bool ny = ctx.Service.CanRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, out _);
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, new LootManager(null, new Random(8804)));
        return Check(!ctx.Service.CanRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, out _) &&
            ny && nyResult.Success && ctx.Service.IsRecovered(ctx.NySite.SiteId) && ctx.Service.IsRecovered(ctx.CaSite.SiteId),
            "California recovery mutated New York availability");
    }

    private static (bool, string) BothRecoveredNeitherReleases()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, new LootManager(null, new Random(8805)));
        if (!nyResult.Success || !caResult.Success)
            return Fail("initial recoveries failed");
        var retryLoot = new LootManager(null, new Random(8806));
        DerelictRecoveryResult nyRetry = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, retryLoot);
        DerelictRecoveryResult caRetry = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, retryLoot);
        return Check(!nyRetry.Success && !caRetry.Success && retryLoot.ActivePods.Count == 0 &&
            ctx.Service.RecoveredCount == 2,
            "depleted sites released salvage again");
    }

    private static (bool, string) CrossSystemNyFirst()
    {
        var ctx = new MultiSiteContext();
        // 1-2. Enter New York, discover and recover the New York site.
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        if (!nyResult.Success)
            return Fail($"New York recovery failed: {nyResult.FailureReason}");
        // 3-5. Leave for California, discover and recover the California site.
        ctx.Service.ResetTransient();
        DerelictPresentation caPresentation = ctx.Service.GetPresentation(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false);
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, new LootManager(null, new Random(8807)));
        if (caPresentation != DerelictPresentation.ReadyToInvestigate || !caResult.Success)
            return Fail($"California leg failed: presentation={caPresentation}, '{caResult.FailureReason}'");
        // 6-7. Return to New York: recovery state unchanged, still depleted.
        ctx.Service.ResetTransient();
        Ship nyPlayer = CreatePlayer(ctx.NySite.Position);
        DerelictRecoveryResult nyRetry = ctx.Service.TryRecover(ctx.NySite, nyPlayer.Position, 1, true, false, false, new LootManager(null, new Random(8808)));
        if (nyRetry.Success || !ctx.Service.IsRecovered(ctx.NySite.SiteId))
            return Fail("returning to New York reset or duplicated its recovery state");
        // 8-9. Return to California: cannot release salvage again.
        ctx.Service.ResetTransient();
        Ship caPlayer = CreatePlayer(ctx.CaSite.Position);
        DerelictRecoveryResult caRetry = ctx.Service.TryRecover(ctx.CaSite, caPlayer.Position, 2, true, false, false, new LootManager(null, new Random(8809)));
        return Check(!caRetry.Success && caRetry.FailureReason == "derelict already salvaged" &&
            ctx.Service.IsRecovered(ctx.CaSite.SiteId),
            "returning to California released salvage again");
    }

    private static (bool, string) CrossSystemCaFirst()
    {
        var ctx = new MultiSiteContext();
        // Reverse order: California first, then New York, then back.
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        if (!caResult.Success)
            return Fail($"California recovery failed: {caResult.FailureReason}");
        ctx.Service.ResetTransient();
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, new LootManager(null, new Random(8810)));
        if (!nyResult.Success)
            return Fail($"New York leg failed: '{nyResult.FailureReason}'");
        ctx.Service.ResetTransient();
        Ship caPlayer = CreatePlayer(ctx.CaSite.Position);
        DerelictRecoveryResult caRetry = ctx.Service.TryRecover(ctx.CaSite, caPlayer.Position, 2, true, false, false, new LootManager(null, new Random(8811)));
        ctx.Service.ResetTransient();
        Ship nyPlayer = CreatePlayer(ctx.NySite.Position);
        DerelictRecoveryResult nyRetry = ctx.Service.TryRecover(ctx.NySite, nyPlayer.Position, 1, true, false, false, new LootManager(null, new Random(8812)));
        return Check(!caRetry.Success && !nyRetry.Success &&
            ctx.Service.IsRecovered(ctx.CaSite.SiteId) && ctx.Service.IsRecovered(ctx.NySite.SiteId),
            "reverse-order travel reset or duplicated recovery state");
    }

    private static (bool, string) KeyedByCanonicalId()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        if (!caResult.Success)
            return Fail($"California recovery failed: {caResult.FailureReason}");
        List<string> captured = ctx.Service.CaptureState();
        bool exact = captured.Count == 1 && captured[0] == DerelictCatalog.SecondSiteId;
        string json = JsonSerializer.Serialize(captured);
        var restored = new DerelictRecoveryService();
        restored.RestoreState(captured);
        bool mapped = restored.IsRecovered(DerelictCatalog.SecondSiteId) &&
            !restored.IsRecovered(DerelictCatalog.FirstSiteId) &&
            json.Contains(DerelictCatalog.SecondSiteId, StringComparison.Ordinal);
        return Check(exact && mapped, "recovery state was not keyed by the canonical site id");
    }

    private static (bool, string) RemoteStateImmutability()
    {
        var ctx = new MultiSiteContext();
        Ship atNy = ctx.PlayerAtNy;
        // Attempts against the wrong system (or out of range) must not flip
        // the remote site's recovered bit.
        DerelictRecoveryResult wrongSystem = ctx.Service.TryRecover(ctx.CaSite, atNy.Position, 1, true, false, false, ctx.Loot);
        Ship farFromCa = CreatePlayer(OffsetFromSite(ctx.CaSite, 1000f));
        DerelictRecoveryResult outOfRange = ctx.Service.TryRecover(ctx.CaSite, farFromCa.Position, 2, true, false, false, ctx.Loot);
        bool caStillAvailable = ctx.Service.CanRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, out _);
        bool nyStillAvailable = ctx.Service.CanRecover(ctx.NySite, atNy.Position, 1, true, false, false, out _);
        return Check(!wrongSystem.Success && !outOfRange.Success && caStillAvailable && nyStillAvailable &&
            ctx.Service.RecoveredCount == 0 && ctx.Loot.ActivePods.Count == 0,
            "failed remote attempts mutated recovery state or loot");
    }

    // ---- Persistence (gates 43-50) --------------------------------------------

    private static string RoundTrip(SaveGameData data)
    {
        return JsonSerializer.Serialize(data);
    }

    private static DerelictRecoveryService RestoreFrom(SaveGameData save)
    {
        SaveGameData restored = JsonSerializer.Deserialize<SaveGameData>(RoundTrip(save));
        if (restored == null)
            throw new InvalidOperationException("save did not round-trip");
        var service = new DerelictRecoveryService();
        service.RestoreState(restored.DerelictRecoveredSiteIds);
        return service;
    }

    private static (bool, string) BothUnrecoveredRoundTrip()
    {
        var ctx = new MultiSiteContext();
        var save = new SaveGameData { DerelictRecoveredSiteIds = ctx.Service.CaptureState() };
        DerelictRecoveryService fresh = RestoreFrom(save);
        bool ny = fresh.CanRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, out _);
        bool ca = fresh.CanRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, out _);
        return Check(ny && ca && fresh.RecoveredCount == 0, "unrecovered pair did not survive save/load as available");
    }

    private static (bool, string) NyOnlyRoundTrip()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        if (!nyResult.Success)
            return Fail($"New York recovery failed: {nyResult.FailureReason}");
        DerelictRecoveryService fresh = RestoreFrom(new SaveGameData { DerelictRecoveredSiteIds = ctx.Service.CaptureState() });
        Ship nyPlayer = CreatePlayer(ctx.NySite.Position);
        Ship caPlayer = CreatePlayer(ctx.CaSite.Position);
        DerelictRecoveryResult nyRetry = fresh.TryRecover(ctx.NySite, nyPlayer.Position, 1, true, false, false, new LootManager(null, new Random(8813)));
        bool ca = fresh.CanRecover(ctx.CaSite, caPlayer.Position, 2, true, false, false, out _);
        return Check(fresh.IsRecovered(ctx.NySite.SiteId) && !fresh.IsRecovered(ctx.CaSite.SiteId) &&
            !nyRetry.Success && ca,
            "New York-only recovery did not round-trip with California available");
    }

    private static (bool, string) CaOnlyRoundTrip()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        if (!caResult.Success)
            return Fail($"California recovery failed: {caResult.FailureReason}");
        DerelictRecoveryService fresh = RestoreFrom(new SaveGameData { DerelictRecoveredSiteIds = ctx.Service.CaptureState() });
        Ship nyPlayer = CreatePlayer(ctx.NySite.Position);
        Ship caPlayer = CreatePlayer(ctx.CaSite.Position);
        DerelictRecoveryResult caRetry = fresh.TryRecover(ctx.CaSite, caPlayer.Position, 2, true, false, false, new LootManager(null, new Random(8814)));
        bool ny = fresh.CanRecover(ctx.NySite, nyPlayer.Position, 1, true, false, false, out _);
        return Check(fresh.IsRecovered(ctx.CaSite.SiteId) && !fresh.IsRecovered(ctx.NySite.SiteId) &&
            !caRetry.Success && ny,
            "California-only recovery did not round-trip with New York available");
    }

    private static (bool, string) BothRecoveredRoundTrip()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, new LootManager(null, new Random(8815)));
        if (!nyResult.Success || !caResult.Success)
            return Fail("initial recoveries failed");
        DerelictRecoveryService fresh = RestoreFrom(new SaveGameData { DerelictRecoveredSiteIds = ctx.Service.CaptureState() });
        var retryLoot = new LootManager(null, new Random(8816));
        DerelictRecoveryResult nyRetry = fresh.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, retryLoot);
        DerelictRecoveryResult caRetry = fresh.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, retryLoot);
        return Check(!nyRetry.Success && !caRetry.Success && retryLoot.ActivePods.Count == 0 && fresh.RecoveredCount == 2,
            "both-recovered state did not round-trip as depleted");
    }

    private static (bool, string) OutstandingPodsSemantics()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        List<SaveCargoPodData> captured = ctx.Loot.CaptureCargoPods();
        List<SaveCargoPodData> restored = JsonSerializer.Deserialize<List<SaveCargoPodData>>(JsonSerializer.Serialize(captured));
        if (restored == null)
            return Fail("released pods did not round-trip");
        var freshLoot = new LootManager(null, new Random(8817));
        int restoredCount = freshLoot.RestoreCargoPods(restored);
        var byId = freshLoot.ActivePods.ToDictionary(pod => pod.CommodityId.ToLowerInvariant(), pod => pod.Quantity);
        return Check(restoredCount == 3 && freshLoot.ActivePods.Count == 3 && byId["food-rations"] == 5 &&
            byId["water"] == 4 && byId["medical-supplies"] == 3,
            $"pod restore produced {restoredCount} pods instead of the exact released set");
    }

    private static (bool, string) CollectedRemainsOwned()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        List<SaveCargoItemData> cargo = new SaveGameManager().CaptureCargo(player.CargoHold);
        List<SaveCargoItemData> roundTripped = JsonSerializer.Deserialize<List<SaveCargoItemData>>(JsonSerializer.Serialize(cargo));
        if (roundTripped == null)
            return Fail("collected cargo did not round-trip");
        var byId = roundTripped.ToDictionary(entry => entry.CommodityId.ToLowerInvariant(), entry => entry.Quantity);
        bool kept = byId["food-rations"] == 5 && byId["water"] == 4 && byId["medical-supplies"] == 3 &&
            roundTripped.All(entry => !entry.IsStolen && !entry.MissionBound && entry.MissionId == 0);
        return Check(kept, "collected California salvage did not survive the cargo save contract");
    }

    private static (bool, string) LegacyLoadsSafely()
    {
        const string legacyJson = "{\"schema_version\":13,\"current_system_index\":1,\"player_credits\":500," +
            "\"cargo\":[{\"commodity_id\":\"food-rations\",\"quantity\":2}]}";
        SaveGameData legacy = JsonSerializer.Deserialize<SaveGameData>(legacyJson);
        if (legacy == null)
            return Fail("legacy save did not deserialize");
        var service = new DerelictRecoveryService();
        service.RestoreState(legacy.DerelictRecoveredSiteIds);
        Ship nyPlayer = CreatePlayer(NySite().Position);
        Ship caPlayer = CreatePlayer(CaSite().Position);
        bool ny = service.CanRecover(NySite(), nyPlayer.Position, 1, true, false, false, out _);
        bool ca = service.CanRecover(CaSite(), caPlayer.Position, 2, true, false, false, out _);
        const string explicitNull = "{\"schema_version\":13,\"derelict_recovered_site_ids\":null}";
        SaveGameData nulled = JsonSerializer.Deserialize<SaveGameData>(explicitNull);
        if (nulled == null)
            return Fail("explicit-null save did not deserialize");
        var nulledService = new DerelictRecoveryService();
        nulledService.RestoreState(nulled.DerelictRecoveredSiteIds);
        return Check(legacy.DerelictRecoveredSiteIds != null && legacy.DerelictRecoveredSiteIds.Count == 0 &&
            legacy.Cargo.Count == 1 && legacy.PlayerCredits == 500 &&
            !service.IsRecovered(NySite().SiteId) && !service.IsRecovered(CaSite().SiteId) &&
            ny && ca && !nulledService.IsRecovered(CaSite().SiteId),
            "legacy saves did not default to both sites available without cargo loss");
    }

    private static (bool, string) SchemaCompatible()
    {
        var save = new SaveGameData
        {
            DerelictRecoveredSiteIds = new List<string> { DerelictCatalog.FirstSiteId, DerelictCatalog.SecondSiteId },
        };
        string json = RoundTrip(save);
        SaveGameData restored = JsonSerializer.Deserialize<SaveGameData>(json);
        bool pair = restored != null && restored.DerelictRecoveredSiteIds.Count == 2 &&
            restored.DerelictRecoveredSiteIds.Contains(DerelictCatalog.FirstSiteId) &&
            restored.DerelictRecoveredSiteIds.Contains(DerelictCatalog.SecondSiteId);
        return Check(SaveGameData.CurrentSchemaVersion == 13 &&
            json.Contains("derelict_recovered_site_ids", StringComparison.Ordinal) && pair,
            $"schema was {SaveGameData.CurrentSchemaVersion} or the pair did not survive one additive field");
    }

    // ---- Lifecycle and regressions (gates 51-64) ------------------------------

    private static (bool, string) DeathCannotDuplicate()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        int podsAfterRelease = ctx.Loot.ActivePods.Count;
        DestroyPlayer(player);
        bool playerAlive = player.Hull?.IsDestroyed != true;
        DerelictRecoveryResult retry = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, playerAlive, false, false, ctx.Loot);
        ctx.Service.ResetTransient();
        // Respawned player in a fresh hull still cannot release a copy, and
        // the durable bit survives death.
        Ship respawned = ctx.PlayerAtCa;
        DerelictRecoveryResult afterRespawn = ctx.Service.TryRecover(ctx.CaSite, respawned.Position, 2, true, false, false, ctx.Loot);
        return Check(!retry.Success && !afterRespawn.Success &&
            ctx.Loot.ActivePods.Count == podsAfterRelease && ctx.Service.IsRecovered(ctx.CaSite.SiteId),
            "player death or respawn duplicated salvage or lost recovery state");
    }

    private static (bool, string) DockUndockCannotDuplicate()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        bool dockedBlocked = !ctx.Service.CanRecover(ctx.CaSite, player.Position, 2, true, true, false, out _);
        DerelictRecoveryResult docked = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, true, false, ctx.Loot);
        DerelictRecoveryResult undocked = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        DerelictRecoveryResult redocked = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, true, false, ctx.Loot);
        return Check(dockedBlocked && !docked.Success && undocked.Success && !redocked.Success && ctx.Loot.ActivePods.Count == 3,
            "dock/undock duplicated or blocked first California recovery");
    }

    private static (bool, string) TransitionClearsTransientOnly()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Service.ResetTransient();
        Ship player = CreatePlayer(ctx.CaSite.Position);
        DerelictPresentation foreign = ctx.Service.GetPresentation(ctx.CaSite, player.Position, 1, true, false);
        string foreignHud = ctx.Service.GetHudText(ctx.CaSite, player.Position, 1, true, false);
        DerelictPresentation home = ctx.Service.GetPresentation(ctx.CaSite, player.Position, 2, true, false);
        return Check(foreign == DerelictPresentation.Hidden && foreignHud == null &&
            home == DerelictPresentation.RecoveredWreck && ctx.Service.IsRecovered(ctx.CaSite.SiteId),
            "system transition lost recovery state or leaked interaction text");
    }

    private static (bool, string) FullHoldPreserves()
    {
        var ctx = new MultiSiteContext();
        Ship player = ctx.PlayerAtCa;
        player.CargoHold.SetMaxCapacity(0);
        player.CargoHold.Clear();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, player.Position, 2, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed with a full hold: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        bool intact = ctx.Loot.ActivePods.Count == 3 && player.CargoHold.UsedCapacity == 0 && ctx.Service.IsRecovered(ctx.CaSite.SiteId);
        player.CargoHold.SetMaxCapacity(50);
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        return Check(intact && ctx.Loot.ActivePods.Count == 0 && player.CargoHold.UsedCapacity == 12,
            "full hold corrupted recovery or lost California salvage");
    }

    private static string DropSignature(IReadOnlyList<SalvageDrop> drops)
    {
        return string.Join(";", drops
            .Where(drop => drop != null)
            .OrderBy(drop => drop.IsEquipment ? 1 : drop.IsConsumable ? 2 : 0)
            .ThenBy(drop => drop.CommodityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(drop => drop.EquipmentId, StringComparer.OrdinalIgnoreCase)
            .Select(drop => $"{drop.PayloadType}:{drop.CommodityId}:{drop.EquipmentId}:{drop.ConsumableId}:{drop.Quantity}:{drop.Tier}"));
    }

    private static (bool, string) NpcSalvageUnchanged()
    {
        var left = new CombatSalvageService();
        var right = new CombatSalvageService();
        NpcShip first = CreateTrader("Regression Trader", new Vector3(100f, 0f, 0f));
        NpcShip second = CreateTrader("Regression Trader", new Vector3(100f, 0f, 0f));
        IReadOnlyList<SalvageDrop> firstDrops = Array.Empty<SalvageDrop>();
        IReadOnlyList<SalvageDrop> secondDrops = Array.Empty<SalvageDrop>();
        RunSilenced(() =>
        {
            DestroyNpc(first);
            DestroyNpc(second);
            firstDrops = left.EvaluateDestruction(first);
            secondDrops = right.EvaluateDestruction(second);
        });
        bool bounds = CombatSalvageService.MaxLiveSalvageObjects == 32 &&
            CombatSalvageService.SalvageLifetimeSeconds == 120d &&
            CombatSalvageService.PickupRadius == 200f;
        return Check(bounds && DropSignature(firstDrops) == DropSignature(secondDrops),
            "NPC destruction salvage changed identity or shared bounds");
    }

    private static (bool, string) EquipmentSalvageUnchanged()
    {
        bool bounds = CombatSalvageService.StandardMaximumEquipmentObjectsPerDestruction == 1 &&
            CombatSalvageService.HeavyMaximumEquipmentObjectsPerDestruction == 2;
        Ship player = CreatePlayer(Vector3.Zero);
        player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
        var loot = new LootManager(null, new Random(8818));
        if (!CargoPod.TryCreateEquipment("liberty_pulse_cannon", Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod))
            return Fail("equipment pod harness could not be created");
        InjectPod(loot, pod);
        loot.Update(Frame(0.1f), player, tractorActive: false);
        return Check(bounds && loot.ActivePods.Count == 0 && player.Loadout.GetOwnedCount("liberty_pulse_cannon") == 1 &&
            loot.LastPickupNotification == "Salvaged: 1x Liberty Pulse Cannon",
            "existing equipment salvage path did not transfer ownership");
    }

    private static (bool, string) PirateSalvageUnchanged()
    {
        var loot = new LootManager(null, new Random(8819));
        NpcShip trader = CreateTrader("Piracy Regression Trader", new Vector3(1000f, 0f, 0f));
        int extortionPods = RunSilenced(() => loot.SpawnExtortionCargo(trader, "food-rations", 3, out int podCount, null) == 3 ? podCount : -1);
        CargoPod pod = loot.ActivePods.SingleOrDefault();
        return Check(extortionPods == 1 && pod != null && pod.Quantity == 3 && pod.IsStolen,
            "player-to-NPC piracy extortion path changed provenance or shape");
    }

    private static (bool, string) PoliceUnchanged()
    {
        bool keys = PoliceScanSystem.EnforcementComplyKey == Keys.Enter &&
            PoliceScanSystem.EnforcementRefuseKey == Keys.N &&
            NpcPirateCargoDemandService.ComplyKey == Keys.Enter &&
            NpcPirateCargoDemandService.RefuseKey == Keys.N &&
            DerelictRecoveryService.InvestigateKey == Keys.F &&
            DerelictRecoveryService.InvestigateKeyLabel == "F";
        bool clean = DerelictCatalog.Sites.All(site => site.Salvage.All(line =>
        {
            Commodity commodity = CommodityCatalog.GetById(line.CommodityId);
            return commodity != null && !commodity.IsContraband && !commodity.IsMissionCargo;
        }));
        return Check(keys && clean, "Police/demand keys or multi-site contraband policy changed");
    }

    private static (bool, string) TradeLaneUnchanged()
    {
        Mission disruption = Mission.CreateTradeLaneDisruption(
            "lane-88", "seg-a", 0, "Test Lane", Vector3.Zero, 1,
            MissionDifficulty.Easy, 1000, 10f, "Phase 88 regression probe", 0.1f);
        if (disruption == null || disruption.Type != MissionType.TradeLaneDisruption)
            return Fail("disruption mission harness could not be created");
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        return Check(result.Success && disruption.HoldProgressSeconds == 0f && !disruption.PlayerDisruptionObserved &&
            !disruption.ObjectiveComplete, "California recovery touched trade-lane disruption state");
    }

    private static (bool, string) MissionFreightUnchanged()
    {
        var save = new SaveGameData
        {
            DerelictRecoveredSiteIds = new List<string> { DerelictCatalog.FirstSiteId, DerelictCatalog.SecondSiteId },
            ActiveMissions = new List<SaveMissionData>
            {
                new() { MissionId = 7, Type = MissionType.Delivery, Status = MissionStatus.Active, Title = "Phase88 Probe" },
            },
        };
        SaveGameData restored = JsonSerializer.Deserialize<SaveGameData>(RoundTrip(save));
        return Check(restored != null && restored.ActiveMissions.Count == 1 && restored.ActiveMissions[0].MissionId == 7 &&
            restored.DerelictRecoveredSiteIds.Count == 2,
            "mission and multi-site derelict payloads did not coexist in one save");
    }

    private static (bool, string) SystemLocalBounded()
    {
        var ctx = new MultiSiteContext();
        int nySites = DerelictCatalog.GetForSystem(1).Count;
        int caSites = DerelictCatalog.GetForSystem(2).Count;
        Ship far = CreatePlayer(OffsetFromSite(ctx.CaSite, 20000f));
        string hud = null;
        for (int i = 0; i < 5; i++)
            hud = ctx.Service.GetHudText(ctx.CaSite, far.Position, 2, true, false);
        return Check(nySites == 1 && caSites == 1 && DerelictCatalog.Sites.Count == 2 &&
            hud == null && ctx.Service.RecoveredCount == 0 && ctx.Loot.ActivePods.Count == 0,
            "derelict interaction was not system-local, bounded, and side-effect free");
    }

    private static (bool, string) NoParallelInventory()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, ctx.Loot);
        List<string> captured = ctx.Service.CaptureState();
        return Check(result.Success && captured.Count == 1 && captured[0] == DerelictCatalog.SecondSiteId &&
            ctx.Loot.ActivePods.Count == 3 && result.SpawnedPods.All(pod => pod is CargoPod),
            "a parallel loot or inventory authority appeared");
    }

    private static (bool, string) NoParallelRecoveryRegistry()
    {
        var ctx = new MultiSiteContext();
        DerelictRecoveryResult nyResult = ctx.Service.TryRecover(ctx.NySite, ctx.PlayerAtNy.Position, 1, true, false, false, ctx.Loot);
        DerelictRecoveryResult caResult = ctx.Service.TryRecover(ctx.CaSite, ctx.PlayerAtCa.Position, 2, true, false, false, new LootManager(null, new Random(8820)));
        List<string> captured = ctx.Service.CaptureState();
        string json = JsonSerializer.Serialize(captured);
        // One shared service owns both bits; the ledger is plain stable
        // strings in deterministic case-insensitive sort order
        // ("..._ca_..." sorts before "..._ny_..."), with no second registry.
        bool singleLedger = nyResult.Success && caResult.Success && captured.Count == 2 &&
            captured[0] == DerelictCatalog.SecondSiteId && captured[1] == DerelictCatalog.FirstSiteId &&
            json.Contains(DerelictCatalog.FirstSiteId, StringComparison.Ordinal) &&
            json.Contains(DerelictCatalog.SecondSiteId, StringComparison.Ordinal);
        return Check(singleLedger, "recovery state split across registries or lost determinism");
    }

    // ---- Architecture-specific ------------------------------------------------

    private static (bool, string) CatalogDrivenRouting()
    {
        // Every curated site routes to exactly one system via the catalog;
        // no site appears in both systems and no system set leaks the other.
        var all = new HashSet<string>(DerelictCatalog.Sites.Select(site => site.SiteId), StringComparer.OrdinalIgnoreCase);
        var ny = DerelictCatalog.GetForSystem(1).Select(site => site.SiteId).ToList();
        var ca = DerelictCatalog.GetForSystem(2).Select(site => site.SiteId).ToList();
        bool partition = all.Count == 2 && ny.Count == 1 && ca.Count == 1 &&
            ny[0] == DerelictCatalog.FirstSiteId && ca[0] == DerelictCatalog.SecondSiteId &&
            !ny.Intersect(ca, StringComparer.OrdinalIgnoreCase).Any() &&
            new HashSet<string>(ny.Concat(ca), StringComparer.OrdinalIgnoreCase).SetEquals(all);
        return Check(partition, "catalog routing did not partition the two sites by system");
    }

    private static (bool, string) WreckWorldObjects()
    {
        var nyWreck = new DerelictWreckObject(NySite());
        var caWreck = new DerelictWreckObject(CaSite());
        // Runtime-type checks instead of pattern matching: the compiler
        // proves sibling types can never overlap, so `is NpcShip` on a
        // statically known wreck would warn as unreachable.
        bool nyPlain = nyWreck is SpaceObject && !typeof(NpcShip).IsAssignableFrom(nyWreck.GetType()) &&
            nyWreck.SiteId == DerelictCatalog.FirstSiteId && nyWreck.ModelPath == "MODELS/wreck";
        bool caPlain = caWreck is SpaceObject && !typeof(NpcShip).IsAssignableFrom(caWreck.GetType()) &&
            caWreck.SiteId == DerelictCatalog.SecondSiteId && caWreck.ModelPath == "MODELS/wreck";
        return Check(nyPlain && caPlain, "a derelict wreck was not a plain non-NPC world object");
    }
}
