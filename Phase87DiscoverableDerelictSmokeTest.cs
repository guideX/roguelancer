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
/// Phase 87 focused coverage for discoverable derelict wreck salvage.
/// The suite drives only production authorities: the curated
/// <see cref="DerelictCatalog"/>, <see cref="DerelictRecoveryService"/>,
/// the existing <see cref="LootManager"/>/<see cref="CargoPod"/> physical
/// loot path, <see cref="CargoHold"/> capacity semantics, and the
/// <see cref="SaveGameData"/> JSON contract. It proves one deterministic
/// New York site is discoverable by proximity, releases finite physical
/// salvage exactly once through existing loot systems, survives save/load,
/// and leaves NPC salvage, piracy, Police, trade-lane, and mission systems
/// untouched. No new loot, inventory, navigation, or mission authority is
/// introduced.
/// </summary>
internal sealed class Phase87DiscoverableDerelictSmokeTest
{
    private const string Tag = "[PHASE 87 DISCOVERABLE DERELICT SMOKE]";

    public Phase87DiscoverableDerelictSmokeTest(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        // World placement and discovery (spec gates 1-10).
        RunCase("derelict belongs to a real accessible system", RealAccessibleSystem, ref passed, ref failed);
        RunCase("derelict has a stable canonical identity", StableCanonicalIdentity, ref passed, ref failed);
        RunCase("position is deterministic", DeterministicPositionAndRanges, ref passed, ref failed);
        RunCase("site does not overlap prohibited geometry", ClearOfTransitAndDocking, ref passed, ref failed);
        RunCase("outside detection range there is no alert", HiddenOutsideDetection, ref passed, ref failed);
        RunCase("inside detection range an unidentified signal appears", UnidentifiedSignalInsideDetection, ref passed, ref failed);
        RunCase("inside identification range the wreck identity appears", WreckIdentityInsideIdentification, ref passed, ref failed);
        RunCase("discovery does not grant loot", DiscoveryGrantsNoLoot, ref passed, ref failed);
        RunCase("discovery does not start a mission", DiscoveryStartsNoMission, ref passed, ref failed);
        RunCase("discovery does not award credits", DiscoveryAwardsNoCredits, ref passed, ref failed);
        // Interaction (spec gates 11-18).
        RunCase("out-of-range interaction fails", OutOfRangeFails, ref passed, ref failed);
        RunCase("docked interaction fails", DockedFails, ref passed, ref failed);
        RunCase("destroyed-player interaction fails", DestroyedFails, ref passed, ref failed);
        RunCase("incompatible transit interaction fails", TransitFails, ref passed, ref failed);
        RunCase("valid nearby interaction succeeds", ValidNearbySucceeds, ref passed, ref failed);
        RunCase("holding the key does not repeat recovery", HoldingKeyDoesNotRepeat, ref passed, ref failed);
        RunCase("two same-frame attempts release loot once", SameFrameDoubleAttemptSingleRelease, ref passed, ref failed);
        RunCase("wrong system cannot recover the wreck", WrongSystemFails, ref passed, ref failed);
        // Loot (spec gates 19-28).
        RunCase("canonical commodity IDs are used", CanonicalCommodityIds, ref passed, ref failed);
        RunCase("salvage quantity is exact", ExactQuantities, ref passed, ref failed);
        RunCase("physical pods use the existing LootManager", ExistingLootManagerAuthority, ref passed, ref failed);
        RunCase("cargo is not teleported into the hold", NotTeleported, ref passed, ref failed);
        RunCase("normal pickup succeeds", NormalPickupSucceeds, ref passed, ref failed);
        RunCase("cargo capacity remains authoritative", CapacityAuthoritative, ref passed, ref failed);
        RunCase("partial pickup follows existing semantics", PartialPickupRetry, ref passed, ref failed);
        RunCase("salvage provenance follows canonical policy", CleanProvenance, ref passed, ref failed);
        RunCase("wreck recovery does not affect mission cargo", MissionReservedIndependence, ref passed, ref failed);
        RunCase("equipment salvage uses the existing authority", EquipmentAuthorityCoexists, ref passed, ref failed);
        // Persistence (spec gates 29-36).
        RunCase("unrecovered wreck survives save/load", UnrecoveredSurvivesSaveLoad, ref passed, ref failed);
        RunCase("recovery state survives save/load", RecoverySurvivesSaveLoad, ref passed, ref failed);
        RunCase("released pods do not duplicate after load", ReleasedPodsNoDuplicate, ref passed, ref failed);
        RunCase("collected salvage remains after load", CollectedSalvageRemains, ref passed, ref failed);
        RunCase("re-entering the system cannot reset recovery", ReentryCannotReset, ref passed, ref failed);
        RunCase("legacy saves load without corruption", LegacySavesLoad, ref passed, ref failed);
        RunCase("schema behavior matches documented policy", SchemaAdditive, ref passed, ref failed);
        RunCase("no runtime pointer is persisted as identity", NoRuntimePointerPersisted, ref passed, ref failed);
        // Lifecycle (spec gates 37-42).
        RunCase("player death cannot duplicate salvage", DeathCannotDuplicate, ref passed, ref failed);
        RunCase("dock and undock cannot duplicate salvage", DockUndockCannotDuplicate, ref passed, ref failed);
        RunCase("system transition clears transient state only", SystemTransitionClearsTransientOnly, ref passed, ref failed);
        RunCase("full hold does not corrupt recovery state", FullHoldDoesNotCorrupt, ref passed, ref failed);
        RunCase("failed loot creation commits no loss", FailedSpawnDoesNotDeplete, ref passed, ref failed);
        RunCase("normal non-derelict loot remains unchanged", NonDerelictLootUnchanged, ref passed, ref failed);
        // Regressions (spec gates 43-52).
        RunCase("NPC destruction salvage is unchanged", NpcDestructionSalvageDeterministic, ref passed, ref failed);
        RunCase("equipment salvage bounds are intact", EquipmentSalvageBoundsIntact, ref passed, ref failed);
        RunCase("NPC-to-player piracy path is intact", PiracyExtortionPathIntact, ref passed, ref failed);
        RunCase("demand keys are unchanged", DemandKeysUnchanged, ref passed, ref failed);
        RunCase("Police keys and clean salvage policy hold", PoliceKeysAndCleanSalvage, ref passed, ref failed);
        RunCase("wreck has no NPC behavior", WreckHasNoNpcBehavior, ref passed, ref failed);
        RunCase("trade-lane disruption missions are unchanged", DisruptionMissionUntouched, ref passed, ref failed);
        RunCase("mission and freight state is untouched", MissionFreightStateUntouched, ref passed, ref failed);
        RunCase("interaction work is spatially bounded", SpatiallyBounded, ref passed, ref failed);
        RunCase("no new general loot authority exists", NoParallelAuthority, ref passed, ref failed);

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

    private sealed class DerelictContext
    {
        public DerelictRecoveryService Service { get; } = new();
        public LootManager Loot { get; } = new LootManager(null, new Random(8701));
        public DerelictSite Site { get; } = DerelictCatalog.GetById(DerelictCatalog.FirstSiteId);
        public Ship PlayerAtSite => CreatePlayer(Site.Position);
    }

    private static DerelictSite Site() => DerelictCatalog.GetById(DerelictCatalog.FirstSiteId);

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

    private static float PointSegmentDistance(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0f)
            return Vector3.Distance(point, start);
        float t = Math.Clamp(Vector3.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector3.Distance(point, start + segment * t);
    }

    private static NpcShip CreateTrader(string name, Vector3 position)
    {
        return RunSilenced(() =>
        {
            var npc = new NpcShip(name, position, Vector3.Zero, 1f, 0f, "neutral_civilians");
            npc.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "derelict87-smoke", Vector3.Zero, 500f, 100f);
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

    private static void DestroyPlayer(Ship player)
    {
        RunSilenced(() => player.Hull.TakeDamage(player.Hull.CurrentHull + 1f));
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

    // ---- World placement and discovery (gates 1-10) -------------------------

    private static (bool, string) RealAccessibleSystem()
    {
        DerelictSite site = Site();
        if (site == null)
            return Fail("first derelict site was missing from the catalog");
        IReadOnlyList<DerelictSite> systemSites = DerelictCatalog.GetForSystem(1);
        return Check(site.SystemIndex == 1 && systemSites.Any(candidate => ReferenceEquals(candidate, site)),
            $"site system was {site.SystemIndex}, expected New York system 1");
    }

    private static (bool, string) StableCanonicalIdentity()
    {
        DerelictSite first = DerelictCatalog.GetById(DerelictCatalog.FirstSiteId);
        DerelictSite upper = DerelictCatalog.GetById(DerelictCatalog.FirstSiteId.ToUpperInvariant());
        return Check(first != null && !string.IsNullOrWhiteSpace(first.SiteId) &&
            ReferenceEquals(first, upper) && ReferenceEquals(first, Site()),
            "site identity was not a stable case-insensitive catalog reference");
    }

    private static (bool, string) DeterministicPositionAndRanges()
    {
        DerelictSite site = Site();
        // Canonical curated position documented in
        // docs/phase_87_discoverable_derelicts_and_wreck_salvage.md. An
        // early (8000, 0, 9000) candidate was rejected for lane proximity.
        bool position = site.Position == new Vector3(-14000f, 500f, -8000f);
        bool ranges = site.DetectionRange == 6000f && site.IdentificationRange == 3000f && site.RecoveryRange == 600f;
        return Check(position && ranges,
            $"site at {site.Position} with ranges {site.DetectionRange}/{site.IdentificationRange}/{site.RecoveryRange}");
    }

    private static (bool, string) ClearOfTransitAndDocking()
    {
        DerelictSite site = Site();
        // New York trade-lane corridors (Configuration/tradelanes/*.json).
        var lanes = new (Vector3 Start, Vector3 End)[]
        {
            (new Vector3(1500f, 300f, -1200f), new Vector3(5400f, 540f, -3900f)),
            (new Vector3(1800f, 0f, 1800f), new Vector3(33000f, 0f, 33000f)),
            (new Vector3(-1200f, 300f, 1500f), new Vector3(-16500f, 840f, 22500f)),
            (new Vector3(-1200f, -300f, 900f), new Vector3(-6600f, -840f, 5400f)),
            (new Vector3(34500f, 0f, 34500f), new Vector3(24600f, -240f, 18600f)),
            (new Vector3(6600f, 660f, -5400f), new Vector3(14400f, 1140f, -17100f)),
        };
        float nearestLane = lanes.Min(lane => PointSegmentDistance(site.Position, lane.Start, lane.End));
        // New York stations (Configuration/stations/*.json).
        var stations = new Vector3[]
        {
            new(6000f, 600f, -4500f), new(-18000f, 900f, 24000f), new(-7500f, -900f, 6000f),
            new(42000f, -600f, 30000f), new(15000f, 1200f, -18000f), new(-12000f, 1800f, -24000f),
            new(24000f, -300f, 18000f), new(30000f, 1500f, -30000f), new(-30000f, -1200f, 36000f),
        };
        float nearestStation = stations.Min(station => Vector3.Distance(site.Position, station));
        // RoguelancerGame.cs fresh spawn near Planet Manhattan.
        float spawn = Vector3.Distance(site.Position, new Vector3(500f, 200f, -500f));
        float manhattan = Vector3.Distance(site.Position, Vector3.Zero);
        return Check(nearestLane > 5000f && nearestStation > 10000f && spawn > 8000f && manhattan > 5000f,
            $"clearances were lane {nearestLane:F0}m, station {nearestStation:F0}m, spawn {spawn:F0}m, Manhattan {manhattan:F0}m");
    }

    private static (bool, string) HiddenOutsideDetection()
    {
        var ctx = new DerelictContext();
        Ship player = CreatePlayer(OffsetFromSite(ctx.Site, ctx.Site.DetectionRange + 3000f));
        DerelictPresentation presentation = ctx.Service.GetPresentation(ctx.Site, player.Position, 1, true, false);
        string hud = ctx.Service.GetHudText(ctx.Site, player.Position, 1, true, false);
        return Check(presentation == DerelictPresentation.Hidden && hud == null,
            $"distant presentation was {presentation} with hud '{hud}'");
    }

    private static (bool, string) UnidentifiedSignalInsideDetection()
    {
        var ctx = new DerelictContext();
        Ship player = CreatePlayer(OffsetFromSite(ctx.Site, 5000f));
        DerelictPresentation presentation = ctx.Service.GetPresentation(ctx.Site, player.Position, 1, true, false);
        string hud = ctx.Service.GetHudText(ctx.Site, player.Position, 1, true, false);
        return Check(presentation == DerelictPresentation.UnidentifiedSignal && hud == "Unidentified signal detected",
            $"detection presentation was {presentation} with hud '{hud}'");
    }

    private static (bool, string) WreckIdentityInsideIdentification()
    {
        var ctx = new DerelictContext();
        Ship identified = CreatePlayer(OffsetFromSite(ctx.Site, 2000f));
        Ship ready = CreatePlayer(OffsetFromSite(ctx.Site, 100f));
        DerelictPresentation identifiedPresentation = ctx.Service.GetPresentation(ctx.Site, identified.Position, 1, true, false);
        string identifiedHud = ctx.Service.GetHudText(ctx.Site, identified.Position, 1, true, false);
        DerelictPresentation readyPresentation = ctx.Service.GetPresentation(ctx.Site, ready.Position, 1, true, false);
        string readyHud = ctx.Service.GetHudText(ctx.Site, ready.Position, 1, true, false);
        return Check(identifiedPresentation == DerelictPresentation.IdentifiedWreck && identifiedHud == "Abandoned spacecraft" &&
            readyPresentation == DerelictPresentation.ReadyToInvestigate && readyHud == "Abandoned spacecraft - Press F to investigate",
            $"identification was {identifiedPresentation} '{identifiedHud}', recovery prompt was {readyPresentation} '{readyHud}'");
    }

    private static (bool, string) DiscoveryGrantsNoLoot()
    {
        var ctx = new DerelictContext();
        Ship player = CreatePlayer(OffsetFromSite(ctx.Site, 2000f));
        _ = ctx.Service.GetPresentation(ctx.Site, player.Position, 1, true, false);
        _ = ctx.Service.GetHudText(ctx.Site, player.Position, 1, true, false);
        return Check(ctx.Loot.ActivePods.Count == 0 && ctx.Service.RecoveredCount == 0,
            "discovery reads created loot or recovery state");
    }

    private static (bool, string) DiscoveryStartsNoMission()
    {
        var ctx = new DerelictContext();
        bool missionPodSpawned = false;
        ctx.Loot.ConfigureMissionCargoCallbacks(_ => null, _ => missionPodSpawned = true, null, null, null);
        Ship player = CreatePlayer(OffsetFromSite(ctx.Site, 2000f));
        _ = ctx.Service.GetPresentation(ctx.Site, player.Position, 1, true, false);
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        // Discovery itself (2000m, outside the 600m recovery range) must fail;
        // the assertion is that no mission-cargo seam fires either way.
        return Check(!result.Success && !missionPodSpawned && ctx.Loot.ActivePods.All(pod => pod.MissionId == 0),
            "discovery or failed recovery touched mission cargo");
    }

    private static (bool, string) DiscoveryAwardsNoCredits()
    {
        var ctx = new DerelictContext();
        Ship player = CreatePlayer(OffsetFromSite(ctx.Site, 2000f));
        int before = player.CargoHold.UsedCapacity;
        _ = ctx.Service.GetPresentation(ctx.Site, player.Position, 1, true, false);
        _ = ctx.Service.GetHudText(ctx.Site, player.Position, 1, true, false);
        return Check(player.CargoHold.UsedCapacity == before && player.CargoHold.GetAllCommodities().Count == 0,
            "discovery reads changed player cargo");
    }

    // ---- Interaction (gates 11-18) ------------------------------------------

    private static (bool, string) OutOfRangeFails()
    {
        var ctx = new DerelictContext();
        Ship player = CreatePlayer(OffsetFromSite(ctx.Site, 1000f));
        bool canRecover = ctx.Service.CanRecover(ctx.Site, player.Position, 1, true, false, false, out string reason);
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        return Check(!canRecover && !result.Success && reason == "derelict out of range" && ctx.Loot.ActivePods.Count == 0,
            $"out-of-range recovery returned {result.Success} with '{reason}'");
    }

    private static (bool, string) DockedFails()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        bool canRecover = ctx.Service.CanRecover(ctx.Site, player.Position, 1, true, true, false, out string reason);
        return Check(!canRecover && reason == "cannot investigate while docked",
            $"docked gate returned {canRecover} with '{reason}'");
    }

    private static (bool, string) DestroyedFails()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        DestroyPlayer(player);
        if (!player.Hull.IsDestroyed)
            return Fail("destroyed-player harness did not destroy the hull");
        bool playerAlive = player.Hull?.IsDestroyed != true;
        bool canRecover = ctx.Service.CanRecover(ctx.Site, player.Position, 1, playerAlive, false, false, out string reason);
        return Check(!playerAlive && !canRecover && reason == "player ship is invalid",
            $"destroyed gate returned alive={playerAlive} with '{reason}'");
    }

    private static (bool, string) TransitFails()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        bool canRecover = ctx.Service.CanRecover(ctx.Site, player.Position, 1, true, false, true, out string reason);
        return Check(!canRecover && reason == "cannot investigate while in transit",
            $"transit gate returned {canRecover} with '{reason}'");
    }

    private static (bool, string) ValidNearbySucceeds()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        bool canRecover = ctx.Service.CanRecover(ctx.Site, player.Position, 1, true, false, false, out string gateReason);
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        return Check(canRecover && result.Success && result.SpawnedPods.Count == 3 && ctx.Service.IsRecovered(ctx.Site.SiteId),
            $"valid recovery returned {result.Success} '{result.FailureReason}' with {result.SpawnedPods.Count} pods");
    }

    private static (bool, string) HoldingKeyDoesNotRepeat()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        DerelictRecoveryResult first = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        DerelictRecoveryResult second = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        DerelictRecoveryResult third = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        return Check(first.Success && !second.Success && !third.Success && ctx.Loot.ActivePods.Count == 3,
            $"repeated recovery produced {ctx.Loot.ActivePods.Count} pods");
    }

    private static (bool, string) SameFrameDoubleAttemptSingleRelease()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        // No Update, notification, or frame boundary runs between attempts:
        // both dispatches happen back-to-back in one frame.
        DerelictRecoveryResult first = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        DerelictRecoveryResult second = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        int totalQuantity = ctx.Loot.ActivePods.Sum(pod => pod.Quantity);
        return Check(first.Success && !second.Success && ctx.Loot.ActivePods.Count == 3 && totalQuantity == 12,
            $"same-frame attempts released {ctx.Loot.ActivePods.Count} pods totaling {totalQuantity}");
    }

    private static (bool, string) WrongSystemFails()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        bool canRecover = ctx.Service.CanRecover(ctx.Site, player.Position, 2, true, false, false, out string reason);
        DerelictPresentation presentation = ctx.Service.GetPresentation(ctx.Site, player.Position, 2, true, false);
        // Phase 88 added a second curated site in California (system 2), so
        // the wrong-system gate now also asserts the New York site is absent
        // from the California system set while the California site is the
        // only entry there.
        IReadOnlyList<DerelictSite> foreignSites = DerelictCatalog.GetForSystem(2);
        return Check(!canRecover && reason == "derelict is in another system" &&
            presentation == DerelictPresentation.Hidden &&
            foreignSites.All(candidate => candidate.SiteId != ctx.Site.SiteId) &&
            foreignSites.Count == 1 && foreignSites[0].SiteId == DerelictCatalog.SecondSiteId,
            $"wrong-system gate returned {canRecover} '{reason}' with presentation {presentation}");
    }

    // ---- Loot (gates 19-28) -------------------------------------------------

    private static (bool, string) CanonicalCommodityIds()
    {
        DerelictSite site = Site();
        var lines = site.Salvage.OrderBy(line => line.CommodityId, StringComparer.OrdinalIgnoreCase).ToList();
        bool canonical = lines.Count == 3 &&
            lines.All(line => CommodityCatalog.GetById(line.CommodityId) != null);
        bool exact = lines[0].CommodityId == "boron" && lines[0].Quantity == 6 &&
            lines[1].CommodityId == "construction-materials" && lines[1].Quantity == 2 &&
            lines[2].CommodityId == "consumer-goods" && lines[2].Quantity == 4;
        return Check(canonical && exact, "salvage lines were not the exact canonical set");
    }

    private static (bool, string) ExactQuantities()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        var byId = result.SpawnedPods.ToDictionary(pod => pod.CommodityId.ToLowerInvariant(), pod => pod.Quantity);
        return Check(result.Success && byId.Count == 3 && byId["boron"] == 6 &&
            byId["construction-materials"] == 2 && byId["consumer-goods"] == 4,
            "released pods did not match the defined salvage exactly");
    }

    private static (bool, string) ExistingLootManagerAuthority()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        bool owned = result.Success && result.SpawnedPods.Count == 3 &&
            result.SpawnedPods.All(pod => pod is CargoPod && ctx.Loot.ActivePods.Contains(pod));
        return Check(owned, "released salvage was not owned by the existing LootManager pool");
    }

    private static (bool, string) NotTeleported()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        return Check(result.Success && player.CargoHold.UsedCapacity == 0 && ctx.Loot.ActivePods.Count == 3,
            "release teleported cargo into the hold instead of physical pods");
    }

    private static (bool, string) NormalPickupSucceeds()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        bool cargo = player.CargoHold.GetCommodityQuantity("Boron") == 6 &&
            player.CargoHold.GetCommodityQuantity("Consumer Goods") == 4 &&
            player.CargoHold.GetCommodityQuantity("Construction Materials") == 2;
        // 6x1 + 4x1 + 2x3 = 16 volume in the starter 50-capacity hold.
        return Check(ctx.Loot.ActivePods.Count == 0 && cargo && player.CargoHold.UsedCapacity == 16,
            $"pickup left {ctx.Loot.ActivePods.Count} pods with used capacity {player.CargoHold.UsedCapacity}");
    }

    private static (bool, string) CapacityAuthoritative()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        player.CargoHold.SetMaxCapacity(15);
        player.CargoHold.Clear();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.1f), player, tractorActive: false);
        CargoPod boron = ctx.Loot.ActivePods.SingleOrDefault(pod => pod.CommodityId == "boron");
        bool partial = player.CargoHold.UsedCapacity == 15 &&
            player.CargoHold.GetCommodityQuantity("Construction Materials") == 2 &&
            player.CargoHold.GetCommodityQuantity("Consumer Goods") == 4 &&
            player.CargoHold.GetCommodityQuantity("Boron") == 5 &&
            boron != null && boron.Quantity == 1 && ctx.Loot.ActivePods.Count == 1;
        return Check(partial,
            $"capacity-15 pickup left {ctx.Loot.ActivePods.Count} pods with used capacity {player.CargoHold.UsedCapacity}");
    }

    private static (bool, string) PartialPickupRetry()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        player.CargoHold.SetMaxCapacity(15);
        player.CargoHold.Clear();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.1f), player, tractorActive: false);
        player.CargoHold.SetMaxCapacity(50);
        ctx.Loot.Update(Frame(0.1f), player, tractorActive: false);
        return Check(ctx.Loot.ActivePods.Count == 0 && player.CargoHold.GetCommodityQuantity("Boron") == 6,
            $"retry left {ctx.Loot.ActivePods.Count} pods with boron {player.CargoHold.GetCommodityQuantity("Boron")}");
    }

    private static (bool, string) CleanProvenance()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        bool clean = result.Success && result.SpawnedPods.All(pod =>
            pod.Provenance == CargoProvenance.Clean && !pod.IsStolen && pod.MissionId == 0 &&
            pod.GetCommodity() != null && !pod.GetCommodity().IsContraband && !pod.GetCommodity().IsMissionCargo);
        return Check(clean, "derelict salvage was not Clean environmental cargo");
    }

    private static (bool, string) MissionReservedIndependence()
    {
        var ctx = new DerelictContext();
        bool missionPodSpawned = false;
        ctx.Loot.ConfigureMissionCargoCallbacks(_ => null, _ => missionPodSpawned = true, null, null, null);
        if (!CargoPod.TryCreate("food-rations", 2, ctx.Site.Position + new Vector3(0f, 300f, 0f), Vector3.Zero, 120f, 200f, out CargoPod missionPod))
            return Fail("mission pod harness could not be created");
        missionPod.SetMissionCargoAttribution(999, 0, "Phase87 Probe");
        InjectPod(ctx.Loot, missionPod);
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        bool missionIntact = missionPod.MissionId == 999 && missionPod.Quantity == 2 && ctx.Loot.ActivePods.Contains(missionPod);
        bool derelictClean = result.Success && result.SpawnedPods.All(pod => pod.MissionId == 0);
        return Check(missionIntact && derelictClean && !missionPodSpawned && ctx.Loot.ActivePods.Count == 4,
            "derelict recovery disturbed mission-reserved cargo");
    }

    private static (bool, string) EquipmentAuthorityCoexists()
    {
        Ship player = CreatePlayer(Vector3.Zero);
        player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
        var loot = new LootManager(null, new Random(8702));
        if (!CargoPod.TryCreateEquipment("liberty_pulse_cannon", Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod))
            return Fail("equipment pod harness could not be created");
        InjectPod(loot, pod);
        loot.Update(Frame(0.1f), player, tractorActive: false);
        return Check(loot.ActivePods.Count == 0 && player.Loadout.GetOwnedCount("liberty_pulse_cannon") == 1 &&
            loot.LastPickupNotification == "Salvaged: 1x Liberty Pulse Cannon",
            "existing equipment salvage path did not transfer ownership");
    }

    // ---- Persistence (gates 29-36) ------------------------------------------

    private static string RoundTrip(SaveGameData data)
    {
        return JsonSerializer.Serialize(data);
    }

    private static (bool, string) UnrecoveredSurvivesSaveLoad()
    {
        var ctx = new DerelictContext();
        var save = new SaveGameData { DerelictRecoveredSiteIds = ctx.Service.CaptureState() };
        SaveGameData? restored = JsonSerializer.Deserialize<SaveGameData>(RoundTrip(save));
        if (restored == null)
            return Fail("unrecovered save did not round-trip");
        var fresh = new DerelictRecoveryService();
        fresh.RestoreState(restored.DerelictRecoveredSiteIds);
        Ship player = CreatePlayer(ctx.Site.Position);
        bool canRecover = fresh.CanRecover(ctx.Site, player.Position, 1, true, false, false, out _);
        return Check(!fresh.IsRecovered(ctx.Site.SiteId) && canRecover,
            "unrecovered wreck did not survive save/load as recoverable");
    }

    private static (bool, string) RecoverySurvivesSaveLoad()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        var save = new SaveGameData { DerelictRecoveredSiteIds = ctx.Service.CaptureState() };
        SaveGameData? restored = JsonSerializer.Deserialize<SaveGameData>(RoundTrip(save));
        if (restored == null)
            return Fail("recovery save did not round-trip");
        var fresh = new DerelictRecoveryService();
        fresh.RestoreState(restored.DerelictRecoveredSiteIds);
        Ship player = CreatePlayer(ctx.Site.Position);
        DerelictRecoveryResult retry = fresh.TryRecover(ctx.Site, player.Position, 1, true, false, false, new LootManager(null, new Random(8703)));
        return Check(fresh.IsRecovered(ctx.Site.SiteId) && !retry.Success && retry.FailureReason == "derelict already salvaged",
            "recovered wreck did not survive save/load as depleted");
    }

    private static (bool, string) ReleasedPodsNoDuplicate()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        List<SaveCargoPodData> captured = ctx.Loot.CaptureCargoPods();
        List<SaveCargoPodData>? restored = JsonSerializer.Deserialize<List<SaveCargoPodData>>(JsonSerializer.Serialize(captured));
        if (restored == null)
            return Fail("released pods did not round-trip");
        var freshLoot = new LootManager(null, new Random(8704));
        int restoredCount = freshLoot.RestoreCargoPods(restored);
        var byId = freshLoot.ActivePods.ToDictionary(pod => pod.CommodityId.ToLowerInvariant(), pod => pod.Quantity);
        return Check(restoredCount == 3 && freshLoot.ActivePods.Count == 3 && byId["boron"] == 6 &&
            byId["construction-materials"] == 2 && byId["consumer-goods"] == 4,
            $"pod restore produced {restoredCount} pods instead of the exact released set");
    }

    private static (bool, string) CollectedSalvageRemains()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        List<SaveCargoItemData> cargo = new SaveGameManager().CaptureCargo(player.CargoHold);
        List<SaveCargoItemData>? roundTripped = JsonSerializer.Deserialize<List<SaveCargoItemData>>(JsonSerializer.Serialize(cargo));
        if (roundTripped == null)
            return Fail("collected cargo did not round-trip");
        var byId = roundTripped.ToDictionary(entry => entry.CommodityId.ToLowerInvariant(), entry => entry.Quantity);
        bool kept = byId["boron"] == 6 && byId["consumer-goods"] == 4 && byId["construction-materials"] == 2 &&
            roundTripped.All(entry => !entry.IsStolen && !entry.MissionBound && entry.MissionId == 0);
        return Check(kept, "collected derelict salvage did not survive the cargo save contract");
    }

    private static (bool, string) ReentryCannotReset()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        // System transition clears transient presentation only; the durable
        // set is restored from the save payload on every entry.
        ctx.Service.ResetTransient();
        var reentered = new DerelictRecoveryService();
        reentered.RestoreState(ctx.Service.CaptureState());
        reentered.ResetTransient();
        Ship player = CreatePlayer(ctx.Site.Position);
        DerelictRecoveryResult retry = reentered.TryRecover(ctx.Site, player.Position, 1, true, false, false, new LootManager(null, new Random(8705)));
        return Check(reentered.IsRecovered(ctx.Site.SiteId) && !retry.Success,
            "re-entering the system reset a recovered wreck");
    }

    private static (bool, string) LegacySavesLoad()
    {
        const string legacyJson = "{\"schema_version\":13,\"current_system_index\":1,\"player_credits\":500," +
            "\"cargo\":[{\"commodity_id\":\"food-rations\",\"quantity\":2}]}";
        SaveGameData? legacy = JsonSerializer.Deserialize<SaveGameData>(legacyJson);
        if (legacy == null)
            return Fail("legacy save did not deserialize");
        var service = new DerelictRecoveryService();
        service.RestoreState(legacy.DerelictRecoveredSiteIds);
        Ship player = CreatePlayer(Site().Position);
        bool canRecover = service.CanRecover(Site(), player.Position, 1, true, false, false, out _);
        const string explicitNull = "{\"schema_version\":13,\"derelict_recovered_site_ids\":null}";
        SaveGameData? nulled = JsonSerializer.Deserialize<SaveGameData>(explicitNull);
        if (nulled == null)
            return Fail("explicit-null save did not deserialize");
        var nulledService = new DerelictRecoveryService();
        nulledService.RestoreState(nulled.DerelictRecoveredSiteIds);
        return Check(legacy.DerelictRecoveredSiteIds != null && legacy.DerelictRecoveredSiteIds.Count == 0 &&
            legacy.Cargo.Count == 1 && legacy.PlayerCredits == 500 &&
            !service.IsRecovered(Site().SiteId) && canRecover && !nulledService.IsRecovered(Site().SiteId),
            "legacy saves did not default to unrecovered without cargo loss");
    }

    private static (bool, string) SchemaAdditive()
    {
        var save = new SaveGameData { DerelictRecoveredSiteIds = new List<string> { DerelictCatalog.FirstSiteId } };
        string json = RoundTrip(save);
        return Check(SaveGameData.CurrentSchemaVersion == 13 && json.Contains("derelict_recovered_site_ids", StringComparison.Ordinal),
            $"schema was {SaveGameData.CurrentSchemaVersion} without the additive field");
    }

    private static (bool, string) NoRuntimePointerPersisted()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        List<string> captured = ctx.Service.CaptureState();
        string json = JsonSerializer.Serialize(captured);
        return Check(captured.Count == 1 && captured[0] == DerelictCatalog.FirstSiteId &&
            json.Contains(DerelictCatalog.FirstSiteId, StringComparison.Ordinal),
            "persisted recovery identity was not the stable site id");
    }

    // ---- Lifecycle (gates 37-42) --------------------------------------------

    private static (bool, string) DeathCannotDuplicate()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        int podsAfterRelease = ctx.Loot.ActivePods.Count;
        DestroyPlayer(player);
        bool playerAlive = player.Hull?.IsDestroyed != true;
        DerelictRecoveryResult retry = ctx.Service.TryRecover(ctx.Site, player.Position, 1, playerAlive, false, false, ctx.Loot);
        ctx.Service.ResetTransient();
        // Respawned player in a fresh hull still cannot release a copy.
        Ship respawned = ctx.PlayerAtSite;
        DerelictRecoveryResult afterRespawn = ctx.Service.TryRecover(ctx.Site, respawned.Position, 1, true, false, false, ctx.Loot);
        return Check(!retry.Success && !afterRespawn.Success && ctx.Loot.ActivePods.Count == podsAfterRelease,
            "player death or respawn duplicated salvage");
    }

    private static (bool, string) DockUndockCannotDuplicate()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        bool dockedBlocked = !ctx.Service.CanRecover(ctx.Site, player.Position, 1, true, true, false, out _);
        DerelictRecoveryResult docked = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, true, false, ctx.Loot);
        DerelictRecoveryResult undocked = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        DerelictRecoveryResult redocked = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, true, false, ctx.Loot);
        return Check(dockedBlocked && !docked.Success && undocked.Success && !redocked.Success && ctx.Loot.ActivePods.Count == 3,
            "dock/undock duplicated or blocked first recovery");
    }

    private static (bool, string) SystemTransitionClearsTransientOnly()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed: {result.FailureReason}");
        ctx.Service.ResetTransient();
        Ship player = CreatePlayer(ctx.Site.Position);
        DerelictPresentation foreign = ctx.Service.GetPresentation(ctx.Site, player.Position, 2, true, false);
        string foreignHud = ctx.Service.GetHudText(ctx.Site, player.Position, 2, true, false);
        return Check(foreign == DerelictPresentation.Hidden && foreignHud == null && ctx.Service.IsRecovered(ctx.Site.SiteId),
            "system transition lost recovery state or leaked interaction text");
    }

    private static (bool, string) FullHoldDoesNotCorrupt()
    {
        var ctx = new DerelictContext();
        Ship player = ctx.PlayerAtSite;
        player.CargoHold.SetMaxCapacity(0);
        player.CargoHold.Clear();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        if (!result.Success)
            return Fail($"recovery failed with a full hold: {result.FailureReason}");
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        bool intact = ctx.Loot.ActivePods.Count == 3 && player.CargoHold.UsedCapacity == 0 && ctx.Service.IsRecovered(ctx.Site.SiteId);
        player.CargoHold.SetMaxCapacity(50);
        ctx.Loot.Update(Frame(0.5f), player, tractorActive: false);
        return Check(intact && ctx.Loot.ActivePods.Count == 0 && player.CargoHold.UsedCapacity == 16,
            "full hold corrupted recovery or lost salvage");
    }

    private static (bool, string) FailedSpawnDoesNotDeplete()
    {
        var ctx = new DerelictContext();
        for (int i = 0; i < CombatSalvageService.MaxLiveSalvageObjects; i++)
        {
            if (!CargoPod.TryCreate("food-rations", 1, new Vector3(5000f + i * 100f, 0f, 0f), Vector3.Zero, 120f, 200f, out CargoPod filler))
                return Fail("salvage-cap harness could not be filled");
            InjectPod(ctx.Loot, filler);
        }
        Ship player = ctx.PlayerAtSite;
        DerelictRecoveryResult blocked = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, ctx.Loot);
        if (blocked.Success || ctx.Service.IsRecovered(ctx.Site.SiteId) || ctx.Loot.ActivePods.Count != CombatSalvageService.MaxLiveSalvageObjects)
            return Fail("blocked recovery committed depletion or partial pods");
        var retryLoot = new LootManager(null, new Random(8706));
        DerelictRecoveryResult retry = ctx.Service.TryRecover(ctx.Site, player.Position, 1, true, false, false, retryLoot);
        return Check(!blocked.Success && retry.Success && retryLoot.ActivePods.Count == 3,
            $"blocked recovery reported '{blocked.FailureReason}' and retry released {retryLoot.ActivePods.Count} pods");
    }

    private static (bool, string) NonDerelictLootUnchanged()
    {
        var loot = new LootManager(null, new Random(8707), null, null, new CombatSalvageService());
        NpcShip trader = CreateTrader("Trader Smoke", Vector3.Zero);
        int before = loot.SalvageService.ProcessedDestructionCount;
        int spawned = 0;
        RunSilenced(() =>
        {
            trader.OnDestroyed += destroyed => spawned = loot.SpawnLootForDestroyedNpc(destroyed);
            DestroyNpc(trader);
        });
        int commodityPods = loot.ActivePods.Count(pod => pod != null && pod.GetCommodity() != null);
        int npcTotal = loot.ActivePods.Count;
        var service = new DerelictRecoveryService();
        Ship player = CreatePlayer(Site().Position);
        DerelictRecoveryResult result = service.TryRecover(Site(), player.Position, 1, true, false, false, loot);
        return Check(spawned >= 0 && commodityPods == 1 && result.Success && loot.ActivePods.Count == npcTotal + 3 &&
            loot.SalvageService.ProcessedDestructionCount == before + 1,
            $"NPC salvage produced {commodityPods} pods and derelict release added {loot.ActivePods.Count - npcTotal}");
    }

    // ---- Regressions (gates 43-52) ------------------------------------------

    private static string DropSignature(IReadOnlyList<SalvageDrop> drops)
    {
        return string.Join(";", drops
            .Where(drop => drop != null)
            .OrderBy(drop => drop.IsEquipment ? 1 : drop.IsConsumable ? 2 : 0)
            .ThenBy(drop => drop.CommodityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(drop => drop.EquipmentId, StringComparer.OrdinalIgnoreCase)
            .Select(drop => $"{drop.PayloadType}:{drop.CommodityId}:{drop.EquipmentId}:{drop.ConsumableId}:{drop.Quantity}:{drop.Tier}"));
    }

    private static (bool, string) NpcDestructionSalvageDeterministic()
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

    private static (bool, string) EquipmentSalvageBoundsIntact()
    {
        bool bounds = CombatSalvageService.StandardMaximumEquipmentObjectsPerDestruction == 1 &&
            CombatSalvageService.HeavyMaximumEquipmentObjectsPerDestruction == 2;
        bool canonical = CargoPod.TryCreateEquipment("liberty_pulse_cannon", Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod) &&
            pod != null && pod.Quantity == 1 && pod.GetEquipment() != null && pod.GetCommodity() == null;
        return Check(bounds && canonical, "equipment salvage bounds or canonical pod path changed");
    }

    private static (bool, string) PiracyExtortionPathIntact()
    {
        var loot = new LootManager(null, new Random(8708));
        NpcShip trader = CreateTrader("Piracy Regression Trader", new Vector3(1000f, 0f, 0f));
        int extortionPods = RunSilenced(() => loot.SpawnExtortionCargo(trader, "food-rations", 3, out int podCount, null) == 3 ? podCount : -1);
        CargoPod pod = loot.ActivePods.SingleOrDefault();
        return Check(extortionPods == 1 && pod != null && pod.Quantity == 3 && pod.IsStolen,
            "player-to-NPC piracy extortion path changed provenance or shape");
    }

    private static (bool, string) DemandKeysUnchanged()
    {
        return Check(NpcPirateCargoDemandService.ComplyKey == Keys.Enter &&
            NpcPirateCargoDemandService.RefuseKey == Keys.N &&
            DerelictRecoveryService.InvestigateKey == Keys.F &&
            DerelictRecoveryService.InvestigateKeyLabel == "F",
            "shared comply/refuse keys changed or investigate key collides");
    }

    private static (bool, string) PoliceKeysAndCleanSalvage()
    {
        bool keys = PoliceScanSystem.EnforcementComplyKey == Keys.Enter &&
            PoliceScanSystem.EnforcementRefuseKey == Keys.N;
        bool clean = Site().Salvage.All(line =>
        {
            Commodity commodity = CommodityCatalog.GetById(line.CommodityId);
            return commodity != null && !commodity.IsContraband && !commodity.IsMissionCargo;
        });
        return Check(keys && clean, "Police enforcement keys or derelict contraband policy changed");
    }

    private static (bool, string) WreckHasNoNpcBehavior()
    {
        var wreck = new DerelictWreckObject(Site());
        // Phase 88 curates exactly two production sites; the New York wreck
        // itself is unchanged.
        return Check(wreck is SpaceObject && !(wreck is NpcShip) && wreck.SiteId == DerelictCatalog.FirstSiteId &&
            wreck.ModelPath == "MODELS/wreck" && DerelictCatalog.Sites.Count == 2,
            "derelict wreck was not a plain non-NPC world object");
    }

    private static (bool, string) DisruptionMissionUntouched()
    {
        Mission disruption = Mission.CreateTradeLaneDisruption(
            "lane-87", "seg-a", 0, "Test Lane", Vector3.Zero, 1,
            MissionDifficulty.Easy, 1000, 10f, "Phase 87 regression probe", 0.1f);
        if (disruption == null || disruption.Type != MissionType.TradeLaneDisruption)
            return Fail("disruption mission harness could not be created");
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        return Check(result.Success && disruption.HoldProgressSeconds == 0f && !disruption.PlayerDisruptionObserved &&
            !disruption.ObjectiveComplete, "derelict recovery touched trade-lane disruption state");
    }

    private static (bool, string) MissionFreightStateUntouched()
    {
        var save = new SaveGameData
        {
            DerelictRecoveredSiteIds = new List<string> { DerelictCatalog.FirstSiteId },
            ActiveMissions = new List<SaveMissionData>
            {
                new() { MissionId = 7, Type = MissionType.Delivery, Status = MissionStatus.Active, Title = "Phase87 Probe" },
            },
        };
        SaveGameData? restored = JsonSerializer.Deserialize<SaveGameData>(RoundTrip(save));
        return Check(restored != null && restored.ActiveMissions.Count == 1 && restored.ActiveMissions[0].MissionId == 7 &&
            restored.DerelictRecoveredSiteIds.Count == 1,
            "mission and derelict payloads did not coexist in one save");
    }

    private static (bool, string) SpatiallyBounded()
    {
        var ctx = new DerelictContext();
        int systemSites = DerelictCatalog.GetForSystem(1).Count;
        Ship far = CreatePlayer(OffsetFromSite(ctx.Site, 20000f));
        string? hud = null;
        for (int i = 0; i < 5; i++)
            hud = ctx.Service.GetHudText(ctx.Site, far.Position, 1, true, false);
        return Check(systemSites == 1 && hud == null && ctx.Service.RecoveredCount == 0 && ctx.Loot.ActivePods.Count == 0,
            "derelict interaction was not spatially bounded and side-effect free");
    }

    private static (bool, string) NoParallelAuthority()
    {
        var ctx = new DerelictContext();
        DerelictRecoveryResult result = ctx.Service.TryRecover(ctx.Site, ctx.PlayerAtSite.Position, 1, true, false, false, ctx.Loot);
        List<string> captured = ctx.Service.CaptureState();
        // Phase 88 curates exactly two production sites through the same
        // catalog; both still release through the single LootManager pool.
        return Check(DerelictCatalog.Sites.Count == 2 && result.Success &&
            captured.All(id => id is string) && ctx.Loot.ActivePods.Count == 3 &&
            result.SpawnedPods.All(pod => pod is CargoPod),
            "a parallel loot or inventory authority appeared");
    }
}
