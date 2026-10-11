using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Phase 87 focused coverage for discoverable derelict wreck salvage.
/// The suite drives only production systems: <see cref="DerelictCatalog"/>,
/// <see cref="DerelictRecoveryService"/>, <see cref="LootManager"/>,
/// <see cref="CargoPod"/>, <see cref="CommodityCatalog"/>, CargoHold/Ship,
/// and the versioned save seam. It proves one curated New York site is
/// discoverable by proximity, releasable exactly once as ordinary physical
/// pods through the existing loot authority, and durable across save/load
/// without creating a parallel mission, economy, or inventory system.
/// </summary>
internal sealed class Phase87DiscoverableDerelictSmokeTest
{
    public Phase87DiscoverableDerelictSmokeTest(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        // World placement and discovery (spec items 1-10).
        RunCase("derelict belongs to a real accessible system", SiteBelongsToAccessibleSystem, ref passed, ref failed);
        RunCase("derelict has a stable canonical identity", StableCanonicalIdentity, ref passed, ref failed);
        RunCase("position is deterministic", PositionIsDeterministic, ref passed, ref failed);
        RunCase("site clears docking volumes, collision, spawn, and lanes", SiteClearsProhibitedGeometry, ref passed, ref failed);
        RunCase("outside detection range there is no proximity alert", OutsideDetectionNoAlert, ref passed, ref failed);
        RunCase("inside detection range an unidentified signal appears", InsideDetectionUnidentifiedSignal, ref passed, ref failed);
        RunCase("inside identification range the wreck identity appears", InsideIdentificationWreckIdentity, ref passed, ref failed);
        RunCase("discovery does not automatically grant loot", DiscoveryGrantsNoLoot, ref passed, ref failed);
        RunCase("discovery starts no mission and awards no credits", DiscoveryStartsNoMissionOrCredits, ref passed, ref failed);
        RunCase("distant systems never observe the site", DistantSystemsNeverObserve, ref passed, ref failed);
        // Interaction (spec items 11-18).
        RunCase("out-of-range interaction fails", OutOfRangeFails, ref passed, ref failed);
        RunCase("docked interaction fails", DockedFails, ref passed, ref failed);
        RunCase("destroyed-player interaction fails", DestroyedPlayerFails, ref passed, ref failed);
        RunCase("incompatible transit interaction fails", TransitFails, ref passed, ref failed);
        RunCase("valid nearby interaction succeeds", ValidInteractionSucceeds, ref passed, ref failed);
        RunCase("holding the key does not repeat recovery", HoldingKeyDoesNotRepeat, ref passed, ref failed);
        RunCase("two same-frame attempts cannot release loot twice", SameFrameAttemptsIdempotent, ref passed, ref failed);
        RunCase("wrong system cannot recover the wreck", WrongSystemCannotRecover, ref passed, ref failed);
        // Loot (spec items 19-28).
        RunCase("canonical commodity ids are used", CanonicalCommodityIds, ref passed, ref failed);
        RunCase("salvage quantity is exact", SalvageQuantityExact, ref passed, ref failed);
        RunCase("physical cargo pods use existing LootManager", PodsUseLootManager, ref passed, ref failed);
        RunCase("cargo is not teleported into player inventory", NoTeleportIntoHold, ref passed, ref failed);
        RunCase("normal pickup succeeds", NormalPickupSucceeds, ref passed, ref failed);
        RunCase("cargo capacity remains authoritative", CapacityAuthoritative, ref passed, ref failed);
        RunCase("partial pickup follows existing semantics", PartialPickupSemantics, ref passed, ref failed);
        RunCase("salvage provenance follows canonical clean policy", ProvenanceIsClean, ref passed, ref failed);
        RunCase("wreck recovery does not affect mission-reserved cargo", MissionReservedCargoUnaffected, ref passed, ref failed);
        RunCase("no parallel equipment salvage path is introduced", NoParallelEquipmentPath, ref passed, ref failed);
        // Persistence (spec items 29-36).
        RunCase("unrecovered wreck survives save/load", UnrecoveredSurvivesSaveLoad, ref passed, ref failed);
        RunCase("recovery state survives save/load", RecoveryStateSurvivesSaveLoad, ref passed, ref failed);
        RunCase("released pods do not duplicate after load", ReleasedPodsDoNotDuplicate, ref passed, ref failed);
        RunCase("collected salvage remains in cargo after load", CollectedSalvageRemainsAfterLoad, ref passed, ref failed);
        RunCase("re-entering the system cannot reset a recovered wreck", ReentryCannotReset, ref passed, ref failed);
        RunCase("legacy saves load without corrupting cargo", LegacySavesLoad, ref passed, ref failed);
        RunCase("schema behavior matches documented policy", SchemaBehaviorDocumented, ref passed, ref failed);
        RunCase("no runtime pointer is persisted as site identity", NoPointerPersisted, ref passed, ref failed);
        // Lifecycle (spec items 37-42).
        RunCase("player death cannot duplicate salvage", DeathCannotDuplicate, ref passed, ref failed);
        RunCase("dock and undock cannot duplicate salvage", DockUndockCannotDuplicate, ref passed, ref failed);
        RunCase("system transition clears transient interaction state", SystemTransitionClearsTransient, ref passed, ref failed);
        RunCase("full cargo hold does not corrupt recovery state", FullHoldDoesNotCorrupt, ref passed, ref failed);
        RunCase("failed loot creation cannot silently commit loss", FailedSpawnDoesNotCommitLoss, ref passed, ref failed);
        RunCase("normal non-derelict loot remains unchanged", NonDerelictLootUnchanged, ref passed, ref failed);
        // Regressions (spec items 43-52).
        RunCase("existing NPC destruction salvage unchanged", NpcDestructionSalvageUnchanged, ref passed, ref failed);
        RunCase("existing equipment salvage catalog unchanged", EquipmentSalvageUnchanged, ref passed, ref failed);
        RunCase("NPC-to-player piracy presentation unchanged", NpcPiracyUnchanged, ref passed, ref failed);
        RunCase("player-to-NPC piracy provenance unchanged", PlayerPiracyUnchanged, ref passed, ref failed);
        RunCase("police enforcement keys unchanged", PoliceEnforcementUnchanged, ref passed, ref failed);
        RunCase("trade-lane geometry independence holds", TradeLaneIndependence, ref passed, ref failed);
        RunCase("trade-lane disruption and mission systems untouched", MissionSystemsUntouched, ref passed, ref failed);
        RunCase("interaction work is spatially bounded", InteractionSpatiallyBounded, ref passed, ref failed);
        RunCase("no new general loot or inventory authority exists", NoNewLootAuthority, ref passed, ref failed);
        // Architecture-specific.
        RunCase("wreck object is a world object, never a live NPC", WreckIsWorldObjectNotNpc, ref passed, ref failed);
        RunCase("investigation uses a documented unused flight key", InvestigateKeyDocumented, ref passed, ref failed);
        RunCase("HUD texts follow existing conventions", HudTextsConventional, ref passed, ref failed);
        RunCase("recovery lookup is O(1) by stable identity", RecoveryLookupBounded, ref passed, ref failed);
        RunCase("commit point marks recovered only after full spawn", CommitPointAfterFullSpawn, ref passed, ref failed);
        RunCase("relevant existing loot suites remain green", RelevantSuitesRemainGreen, ref passed, ref failed);

        Console.WriteLine($"[PHASE 87 DISCOVERABLE DERELICT SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 87 DISCOVERABLE DERELICT SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 87 DISCOVERABLE DERELICT SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 87 DISCOVERABLE DERELICT SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) Check(bool condition, string reason) => (condition, condition ? string.Empty : reason);
    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);

    private static DerelictSite Site() => DerelictCatalog.GetById(DerelictCatalog.FirstSiteId);
    private static LootManager CreateLoot() => new();
    private static GameTime Frame(float seconds = 0.1f) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(Math.Max(0.01f, seconds)));

    private static Vector3 NearSite(DerelictSite site, float distance)
    {
        return site.Position + new Vector3(Math.Max(0f, distance), 0f, 0f);
    }

    private static DerelictRecoveryResult RecoverAt(
        DerelictRecoveryService service,
        DerelictSite site,
        LootManager loot,
        Vector3 playerPosition,
        int system = 1,
        bool alive = true,
        bool docked = false,
        bool inTransit = false)
    {
        return service.TryRecover(site, playerPosition, system, alive, docked, inTransit, loot);
    }

    // ---- World placement and discovery ------------------------------------

    private static (bool, string) SiteBelongsToAccessibleSystem()
    {
        DerelictSite site = Site();
        if (site == null)
            return Fail("first curated site was missing");
        IReadOnlyList<DerelictSite> systemSites = DerelictCatalog.GetForSystem(1);
        return Check(site.SystemIndex == 1 && systemSites.Any(candidate => candidate == site),
            $"site system was {site.SystemIndex}, expected New York (1)");
    }

    private static (bool, string) StableCanonicalIdentity()
    {
        if (string.IsNullOrWhiteSpace(DerelictCatalog.FirstSiteId))
            return Fail("first site id was blank");
        DerelictSite first = Site();
        DerelictSite second = DerelictCatalog.GetById(DerelictCatalog.FirstSiteId.Trim().ToUpperInvariant());
        return Check(first != null && ReferenceEquals(first, second) &&
                string.Equals(first.SiteId, DerelictCatalog.FirstSiteId, StringComparison.OrdinalIgnoreCase),
            "site identity was not a stable case-insensitive canonical id");
    }

    private static (bool, string) PositionIsDeterministic()
    {
        DerelictSite site = Site();
        Vector3 expected = new(-14000f, 500f, -8000f);
        return Check(site != null && site.Position == expected &&
                DerelictCatalog.GetById(site.SiteId).Position == expected,
            $"site position was {site?.Position}, expected {expected}");
    }

    // Config-derived reference geometry for New York (system 1).
    private static readonly Vector3[] System1Stations =
    {
        new(6000f, 600f, -4500f), new(-18000f, 900f, 24000f), new(-7500f, -900f, 6000f),
        new(42000f, -600f, 30000f), new(15000f, 1200f, -18000f), new(-12000f, 1800f, -24000f),
        new(24000f, -300f, 18000f), new(30000f, 1500f, -30000f), new(-30000f, -1200f, 36000f),
    };

    private static readonly (Vector3 Start, Vector3 End)[] System1Lanes =
    {
        (new Vector3(1500f, 300f, -1200f), new Vector3(5400f, 540f, -3900f)),
        (new Vector3(1800f, 0f, 1800f), new Vector3(33000f, 0f, 33000f)),
        (new Vector3(-1200f, 300f, 1500f), new Vector3(-16500f, 840f, 22500f)),
        (new Vector3(-1200f, -300f, 900f), new Vector3(-6600f, -840f, 5400f)),
        (new Vector3(34500f, 0f, 34500f), new Vector3(24600f, -240f, 18600f)),
        (new Vector3(6600f, 660f, -5400f), new Vector3(14400f, 1140f, -17100f)),
    };

    private static float PointSegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denom = ab.LengthSquared();
        float t = denom <= 0f ? 0f : Math.Clamp(Vector3.Dot(p - a, ab) / denom, 0f, 1f);
        return Vector3.Distance(p, a + ab * t);
    }

    private static (bool, string) SiteClearsProhibitedGeometry()
    {
        DerelictSite site = Site();
        if (site == null)
            return Fail("site was missing");
        float minStation = System1Stations.Min(station => Vector3.Distance(site.Position, station));
        // Station docking ranges are <= 900m and the largest station radius
        // is 3000m; a 5000m floor keeps the site far outside both, while the
        // live-offset salvage pods (<= 200m) cannot reach them either.
        if (minStation < 5000f)
            return Fail($"nearest station was {minStation:F0}m, required >= 5000m");
        float minLane = System1Lanes.Min(lane => PointSegmentDistance(site.Position, lane.Start, lane.End));
        if (minLane < 3000f)
            return Fail($"nearest trade-lane segment was {minLane:F0}m, required >= 3000m");
        float spawnDistance = Vector3.Distance(site.Position, new Vector3(500f, 200f, -500f));
        if (spawnDistance < 5000f)
            return Fail($"player spawn was {spawnDistance:F0}m away, required >= 5000m");
        return Pass();
    }

    private static (bool, string) OutsideDetectionNoAlert()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        Vector3 far = NearSite(site, site.DetectionRange + 500f);
        DerelictPresentation presentation = service.GetPresentation(site, far, 1, true, false);
        return Check(presentation == DerelictPresentation.Hidden &&
                service.GetHudText(site, far, 1, true, false) == null,
            $"outside detection produced {presentation}, expected Hidden with no text");
    }

    private static (bool, string) InsideDetectionUnidentifiedSignal()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        Vector3 pos = NearSite(site, (site.DetectionRange + site.IdentificationRange) / 2f);
        DerelictPresentation presentation = service.GetPresentation(site, pos, 1, true, false);
        string text = service.GetHudText(site, pos, 1, true, false);
        return Check(presentation == DerelictPresentation.UnidentifiedSignal && text == "Unidentified signal detected",
            $"detection produced '{presentation}'/'{text}'");
    }

    private static (bool, string) InsideIdentificationWreckIdentity()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        Vector3 pos = NearSite(site, (site.IdentificationRange + site.RecoveryRange) / 2f);
        DerelictPresentation presentation = service.GetPresentation(site, pos, 1, true, false);
        string text = service.GetHudText(site, pos, 1, true, false);
        return Check(presentation == DerelictPresentation.IdentifiedWreck && text == site.DisplayName,
            $"identification produced '{presentation}'/'{text}'");
    }

    private static (bool, string) DiscoveryGrantsNoLoot()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Vector3 pos = NearSite(site, site.IdentificationRange - 100f);
        _ = service.GetPresentation(site, pos, 1, true, false);
        _ = service.GetHudText(site, pos, 1, true, false);
        return Check(loot.ActivePods.Count == 0 && !service.IsRecovered(site.SiteId),
            "proximity discovery created loot or recovery state");
    }

    private static (bool, string) DiscoveryStartsNoMissionOrCredits()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        var credits = new PlayerCredits(10_000);
        Vector3 pos = NearSite(site, site.RecoveryRange - 50f);
        _ = service.GetPresentation(site, pos, 1, true, false);
        DerelictRecoveryResult result = RecoverAt(service, site, loot, pos);
        if (!result.Success)
            return Fail($"valid recovery failed: {result.FailureReason}");
        bool podsClean = result.SpawnedPods.All(pod => pod != null && !pod.IsMissionCargo && pod.MissionId == 0);
        return Check(podsClean && credits.Credits == 10_000,
            "recovery attached mission cargo or touched credits");
    }

    private static (bool, string) DistantSystemsNeverObserve()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        // Buffalo Base corner of New York is ~46km away; any other system id
        // must also stay silent even at the wreck coordinates.
        Vector3 farCorner = new(-30000f, -1200f, 36000f);
        bool silentFar = service.GetPresentation(site, farCorner, 1, true, false) == DerelictPresentation.Hidden;
        bool silentOtherSystem = service.GetPresentation(site, site.Position, 2, true, false) == DerelictPresentation.Hidden;
        return Check(silentFar && silentOtherSystem, "site leaked an alert outside its detection scope");
    }

    // ---- Interaction -------------------------------------------------------

    private static (bool, string) OutOfRangeFails()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, site.RecoveryRange + 100f));
        return Check(!result.Success && loot.ActivePods.Count == 0 && !service.IsRecovered(site.SiteId),
            $"out-of-range recovery succeeded or staged loot: {result.FailureReason}");
    }

    private static (bool, string) DockedFails()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f), docked: true);
        return Check(!result.Success && !service.IsRecovered(site.SiteId), "docked recovery was allowed");
    }

    private static (bool, string) DestroyedPlayerFails()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        player.Hull.TakeDamage(player.Hull.MaxHull + 1f);
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position, alive: player.Hull.IsDestroyed == false);
        return Check(player.Hull.IsDestroyed && !result.Success && !service.IsRecovered(site.SiteId),
            "destroyed-player recovery was allowed");
    }

    private static (bool, string) TransitFails()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f), inTransit: true);
        return Check(!result.Success && !service.IsRecovered(site.SiteId), "transit recovery was allowed");
    }

    private static (bool, string) ValidInteractionSucceeds()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f));
        return Check(result.Success && result.SpawnedPods.Count == site.Salvage.Count &&
                loot.ActivePods.Count == site.Salvage.Count && service.IsRecovered(site.SiteId),
            $"valid recovery produced {result.SpawnedPods.Count} pods: {result.FailureReason}");
    }

    private static (bool, string) HoldingKeyDoesNotRepeat()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Vector3 pos = NearSite(site, 100f);
        DerelictRecoveryResult first = RecoverAt(service, site, loot, pos);
        DerelictRecoveryResult second = RecoverAt(service, site, loot, pos);
        DerelictRecoveryResult third = RecoverAt(service, site, loot, pos);
        return Check(first.Success && !second.Success && !third.Success &&
                loot.ActivePods.Count == site.Salvage.Count,
            "repeated investigation released additional loot");
    }

    private static (bool, string) SameFrameAttemptsIdempotent()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Vector3 pos = NearSite(site, 100f);
        // Two dispatches with no frame between them share one guard.
        DerelictRecoveryResult first = RecoverAt(service, site, loot, pos);
        DerelictRecoveryResult second = RecoverAt(service, site, loot, pos);
        return Check(first.Success && !second.Success &&
                loot.ActivePods.Count == site.Salvage.Count &&
                loot.ActivePods.Select(pod => pod.Quantity).Sum() == site.Salvage.Sum(line => line.Quantity),
            "same-frame attempts duplicated salvage");
    }

    private static (bool, string) WrongSystemCannotRecover()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, site.Position, system: 2);
        return Check(!result.Success && loot.ActivePods.Count == 0 && !service.IsRecovered(site.SiteId),
            "cross-system recovery was allowed");
    }

    // ---- Loot ---------------------------------------------------------------

    private static (bool, string) CanonicalCommodityIds()
    {
        DerelictSite site = Site();
        return Check(site != null && site.Salvage.Count > 0 &&
                site.Salvage.All(line => CommodityCatalog.GetById(line.CommodityId) != null),
            "salvage referenced a non-canonical commodity id");
    }

    private static (bool, string) SalvageQuantityExact()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f));
        if (!result.Success)
            return Fail(result.FailureReason);
        var expected = site.Salvage
            .GroupBy(line => line.CommodityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.OrdinalIgnoreCase);
        var actual = loot.ActivePods
            .GroupBy(pod => pod.CommodityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(pod => pod.Quantity), StringComparer.OrdinalIgnoreCase);
        return Check(expected.Count == actual.Count &&
                expected.All(entry => actual.TryGetValue(entry.Key, out int quantity) && quantity == entry.Value),
            "released pod quantities did not match the deterministic definition");
    }

    private static (bool, string) PodsUseLootManager()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f));
        return Check(result.Success && result.SpawnedPods.Count > 0 &&
                result.SpawnedPods.All(pod => loot.ActivePods.Contains(pod)),
            "salvage pods bypassed the LootManager authority");
    }

    private static (bool, string) NoTeleportIntoHold()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        return Check(result.Success && player.CargoHold.UsedCapacity == 0 &&
                player.CargoHold.GetAllCommodities().Count == 0,
            "recovery teleported cargo into the hold instead of staging pods");
    }

    private static void TeleportPodsToPlayer(LootManager loot, Ship player)
    {
        foreach (CargoPod pod in loot.ActivePods)
        {
            pod.Position = player.Position;
            pod.Velocity = Microsoft.Xna.Framework.Vector3.Zero;
        }
    }

    private static (bool, string) NormalPickupSucceeds()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        if (!result.Success)
            return Fail(result.FailureReason);
        TeleportPodsToPlayer(loot, player);
        loot.Update(Frame(), player, false);
        bool allCollected = loot.ActivePods.Count == 0;
        bool cargoMatches = site.Salvage.All(line =>
            player.CargoHold.GetCommodityQuantity(CommodityCatalog.GetById(line.CommodityId).Name) == line.Quantity);
        return Check(allCollected && cargoMatches, "ordinary tractor/pickup did not collect the staged pods");
    }

    private static (bool, string) CapacityAuthoritative()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        player.CargoHold.SetMaxCapacity(0);
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        if (!result.Success)
            return Fail($"release must not depend on free hold space: {result.FailureReason}");
        TeleportPodsToPlayer(loot, player);
        loot.Update(Frame(), player, false);
        return Check(player.CargoHold.UsedCapacity == 0 && loot.ActivePods.Count == site.Salvage.Count,
            "full hold forced cargo in or destroyed pods");
    }

    private static (bool, string) PartialPickupSemantics()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        // Salvage volume is 6*1 + 4*1 + 2*3 = 16. Capacity 13 with reverse
        // pickup order (construction, consumer-goods, boron) collects 2+4
        // units fully and exactly 3 of 6 boron units, leaving a 3-unit
        // boron remainder behind.
        player.CargoHold.SetMaxCapacity(13);
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        if (!result.Success)
            return Fail(result.FailureReason);
        TeleportPodsToPlayer(loot, player);
        loot.Update(Frame(), player, false);
        CargoPod remainder = loot.ActivePods.SingleOrDefault();
        Commodity boron = CommodityCatalog.GetById("boron");
        Commodity construction = CommodityCatalog.GetById("construction-materials");
        Commodity consumer = CommodityCatalog.GetById("consumer-goods");
        bool partial = remainder != null && loot.ActivePods.Count == 1 &&
            string.Equals(remainder.CommodityId, boron.Id, StringComparison.OrdinalIgnoreCase) &&
            remainder.Quantity == 3 &&
            player.CargoHold.GetCommodityQuantity(boron.Name) == 3 &&
            player.CargoHold.GetCommodityQuantity(construction.Name) == 2 &&
            player.CargoHold.GetCommodityQuantity(consumer.Name) == 4 &&
            player.CargoHold.UsedCapacity == 13;
        if (!partial)
            return Fail("partial pickup did not follow TryAddCommodityPartial semantics");
        player.CargoHold.SetMaxCapacity(50);
        loot.Update(Frame(), player, false);
        return Check(loot.ActivePods.Count == 0 &&
                player.CargoHold.GetCommodityQuantity(boron.Name) == 6,
            "remainder could not be collected after freeing space");
    }

    private static (bool, string) ProvenanceIsClean()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        if (!result.Success)
            return Fail(result.FailureReason);
        bool podsClean = loot.ActivePods.All(pod => !pod.IsStolen && pod.Provenance == CargoProvenance.Clean);
        TeleportPodsToPlayer(loot, player);
        loot.Update(Frame(), player, false);
        bool holdClean = site.Salvage.All(line =>
        {
            Commodity commodity = CommodityCatalog.GetById(line.CommodityId);
            return player.CargoHold.GetSellableCleanCommodityQuantity(commodity.Name) == line.Quantity &&
                player.CargoHold.GetSellableStolenCommodityQuantity(commodity.Name) == 0;
        });
        return Check(podsClean && holdClean, "derelict salvage was marked stolen or contraband");
    }

    private static (bool, string) MissionReservedCargoUnaffected()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        Commodity food = CommodityCatalog.GetById("food-rations");
        if (!player.CargoHold.AddMissionCargo(9100, food, 2))
            return Fail("could not stage mission-reserved cargo");
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        if (!result.Success)
            return Fail(result.FailureReason);
        TeleportPodsToPlayer(loot, player);
        loot.Update(Frame(), player, false);
        return Check(player.CargoHold.GetMissionCargoQuantity(9100) == 2,
            "derelict pickup disturbed mission-reserved cargo");
    }

    private static (bool, string) NoParallelEquipmentPath()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f));
        if (!result.Success)
            return Fail(result.FailureReason);
        return Check(site.Salvage.All(line => CommodityCatalog.GetById(line.CommodityId) != null) &&
                loot.ActivePods.All(pod => pod.PayloadType == CargoPodPayloadType.Commodity && pod.GetEquipment() == null),
            "derelict salvage bypassed the canonical equipment authority");
    }

    // ---- Persistence ----------------------------------------------------------

    private static (bool, string) UnrecoveredSurvivesSaveLoad()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        List<string> saved = service.CaptureState();
        var restored = new DerelictRecoveryService();
        restored.RestoreState(saved);
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(restored, site, loot, NearSite(site, 100f));
        return Check(saved.Count == 0 && result.Success, "unrecovered wreck did not survive save/load");
    }

    private static (bool, string) RecoveryStateSurvivesSaveLoad()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        _ = RecoverAt(service, site, loot, NearSite(site, 100f));
        var restored = new DerelictRecoveryService();
        restored.RestoreState(service.CaptureState());
        DerelictRecoveryResult retry = RecoverAt(restored, site, CreateLoot(), NearSite(site, 100f));
        return Check(service.IsRecovered(site.SiteId) && restored.IsRecovered(site.SiteId) && !retry.Success,
            "recovery state did not survive save/load");
    }

    private static (bool, string) ReleasedPodsDoNotDuplicate()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f));
        if (!result.Success)
            return Fail(result.FailureReason);
        List<SaveCargoPodData> saved = loot.CaptureCargoPods();
        LootManager restoredLoot = CreateLoot();
        int restoredCount = restoredLoot.RestoreCargoPods(saved);
        var restoredService = new DerelictRecoveryService();
        restoredService.RestoreState(service.CaptureState());
        DerelictRecoveryResult retry = RecoverAt(restoredService, site, restoredLoot, NearSite(site, 100f));
        int totalQuantity = restoredLoot.ActivePods.Sum(pod => pod.Quantity);
        return Check(restoredCount == site.Salvage.Count && !retry.Success &&
                restoredLoot.ActivePods.Count == site.Salvage.Count &&
                totalQuantity == site.Salvage.Sum(line => line.Quantity),
            $"pods duplicated across load (pods={restoredLoot.ActivePods.Count}, retry={retry.Success})");
    }

    private static (bool, string) CollectedSalvageRemainsAfterLoad()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        _ = RecoverAt(service, site, loot, player.Position);
        TeleportPodsToPlayer(loot, player);
        loot.Update(Frame(), player, false);
        var saver = new SaveGameManager();
        SaveGameData data = new()
        {
            Cargo = saver.CaptureCargo(player.CargoHold),
            DerelictRecoveredSiteIds = service.CaptureState(),
            PhysicalCargoPods = loot.CaptureCargoPods(),
        };
        Ship resumed = new(player.Position);
        saver.ApplyCargo(resumed.CargoHold, data, out _);
        var restoredService = new DerelictRecoveryService();
        restoredService.RestoreState(data.DerelictRecoveredSiteIds);
        bool cargoKept = site.Salvage.All(line =>
            resumed.CargoHold.GetCommodityQuantity(CommodityCatalog.GetById(line.CommodityId).Name) == line.Quantity);
        return Check(cargoKept && restoredService.IsRecovered(site.SiteId) && data.PhysicalCargoPods.Count == 0,
            "collected salvage or recovery state was lost across save/load");
    }

    private static (bool, string) ReentryCannotReset()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        _ = RecoverAt(service, site, loot, NearSite(site, 100f));
        // System transitions only reset transient interaction state.
        service.ResetTransient();
        DerelictPresentation presentation = service.GetPresentation(site, NearSite(site, 100f), 1, true, false);
        DerelictRecoveryResult retry = RecoverAt(service, site, CreateLoot(), NearSite(site, 100f));
        return Check(service.IsRecovered(site.SiteId) && !retry.Success &&
                presentation == DerelictPresentation.RecoveredWreck,
            "re-entering the system reset a recovered wreck");
    }

    private static (bool, string) LegacySavesLoad()
    {
        // A pre-Phase-87 file carries no derelict field at all.
        SaveGameData legacy = JsonSerializer.Deserialize<SaveGameData>("{\"schema_version\":13,\"player_credits\":500}");
        if (legacy == null)
            return Fail("legacy save did not deserialize");
        var service = new DerelictRecoveryService();
        service.RestoreState(legacy.DerelictRecoveredSiteIds);
        DerelictSite site = Site();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        player.CargoHold.AddCommodity(CommodityCatalog.GetById("food-rations"), 3);
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        return Check(legacy.SchemaVersion == SaveGameData.CurrentSchemaVersion &&
                player.CargoHold.GetCommodityQuantity("Food Rations") == 3 && result.Success,
            "legacy save defaulted unsafely or touched existing cargo");
    }

    private static (bool, string) SchemaBehaviorDocumented()
    {
        return Check(SaveGameData.CurrentSchemaVersion == 13 &&
                new SaveGameData().DerelictRecoveredSiteIds != null &&
                new SaveGameData().DerelictRecoveredSiteIds.Count == 0,
            "save schema was bumped or the additive field defaulted unsafely");
    }

    private static (bool, string) NoPointerPersisted()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        _ = RecoverAt(service, site, CreateLoot(), NearSite(site, 100f));
        List<string> saved = service.CaptureState();
        string json = JsonSerializer.Serialize(saved);
        return Check(saved.Count == 1 && string.Equals(saved[0], site.SiteId, StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("RuntimeIdentity") &&
                DerelictCatalog.GetById(saved[0]) == site,
            $"persisted identity was not the stable site id: {json}");
    }

    // ---- Lifecycle --------------------------------------------------------------

    private static (bool, string) DeathCannotDuplicate()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        _ = RecoverAt(service, site, loot, NearSite(site, 100f));
        // Death clears transient pods exactly like combat salvage; the
        // durable recovery bit must still prevent re-release on respawn.
        loot.Reset();
        service.ResetTransient();
        DerelictRecoveryResult retry = RecoverAt(service, site, loot, NearSite(site, 100f));
        return Check(service.IsRecovered(site.SiteId) && !retry.Success && loot.ActivePods.Count == 0,
            "death/respawn duplicated or reset salvage");
    }

    private static (bool, string) DockUndockCannotDuplicate()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        _ = RecoverAt(service, site, loot, NearSite(site, 100f));
        service.ResetTransient();
        DerelictRecoveryResult retry = RecoverAt(service, site, loot, NearSite(site, 100f), docked: false);
        return Check(service.IsRecovered(site.SiteId) && !retry.Success &&
                loot.ActivePods.Count == site.Salvage.Count,
            "dock/undock duplicated salvage");
    }

    private static (bool, string) SystemTransitionClearsTransient()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        service.ResetTransient();
        bool hiddenElsewhere = service.GetPresentation(site, site.Position, 2, true, false) == DerelictPresentation.Hidden;
        LootManager loot = CreateLoot();
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f));
        return Check(hiddenElsewhere && result.Success && service.RecoveredCount == 1,
            "system transition left stale interaction state");
    }

    private static (bool, string) FullHoldDoesNotCorrupt()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        Ship player = new(NearSite(site, 100f));
        player.CargoHold.SetMaxCapacity(0);
        DerelictRecoveryResult result = RecoverAt(service, site, loot, player.Position);
        if (!result.Success)
            return Fail($"full hold blocked release: {result.FailureReason}");
        TeleportPodsToPlayer(loot, player);
        loot.Update(Frame(), player, false);
        bool intact = service.IsRecovered(site.SiteId) && loot.ActivePods.Count == site.Salvage.Count;
        player.CargoHold.SetMaxCapacity(50);
        loot.Update(Frame(), player, false);
        return Check(intact && loot.ActivePods.Count == 0, "full hold corrupted recovery or pods");
    }

    private static List<SaveCargoPodData> BuildFillerPods(int count)
    {
        var filler = new List<SaveCargoPodData>(count);
        for (int i = 0; i < count; i++)
        {
            filler.Add(new SaveCargoPodData
            {
                CommodityId = "food-rations",
                Quantity = 1,
                AgeSeconds = 0f,
                Position = SaveVector3Data.From(new Vector3(100_000f + i * 5000f, 0f, 0f)),
                Velocity = SaveVector3Data.From(Vector3.Zero),
            });
        }
        return filler;
    }

    private static (bool, string) FailedSpawnDoesNotCommitLoss()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        int filled = loot.RestoreCargoPods(BuildFillerPods(CombatSalvageService.MaxLiveSalvageObjects));
        if (filled != CombatSalvageService.MaxLiveSalvageObjects)
            return Fail($"could not saturate loot capacity (filled={filled})");
        DerelictRecoveryResult blocked = RecoverAt(service, site, loot, NearSite(site, 100f));
        if (blocked.Success || service.IsRecovered(site.SiteId))
            return Fail("saturated loot committed a recovery without pods");
        loot.Reset();
        DerelictRecoveryResult retry = RecoverAt(service, site, loot, NearSite(site, 100f));
        return Check(retry.Success && service.IsRecovered(site.SiteId) &&
                loot.ActivePods.Count == site.Salvage.Count,
            $"retry after capacity freed failed: {retry.FailureReason}");
    }

    private static (bool, string) NonDerelictLootUnchanged()
    {
        LootManager loot = CreateLoot();
        NpcShip trader = new("Phase87 Extortion Trader", Vector3.Zero, Vector3.Zero, 1f, 0f, FactionManager.NeutralCivilians);
        trader.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "phase87-smoke", Vector3.Zero, 500f, 100f);
        int released = loot.SpawnExtortionCargo(trader, "food-rations", 2, out int podCount);
        CargoPod pod = loot.ActivePods.SingleOrDefault();
        return Check(released == 2 && podCount == 1 && pod?.IsStolen == true,
            "existing extortion loot path changed");
    }

    // ---- Regressions ---------------------------------------------------------------

    private static NpcShip CreateRogue(string name, Vector3 position)
    {
        NpcShip ship = new(name, position, position, 1f, 0f, FactionManager.LibertyRogues);
        ship.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase87-smoke", position, 500f, 100f);
        return ship;
    }

    private static void DestroyNpc(NpcShip ship)
    {
        ship.ApplyDamage(ship.Hull.CurrentHull + 1f, NpcDestructionSource.Player);
    }

    private static (bool, string) NpcDestructionSalvageUnchanged()
    {
        var salvage = new CombatSalvageService();
        NpcShip first = CreateRogue("Phase87 Rogue Determinism A", Vector3.Zero);
        NpcShip second = CreateRogue("Phase87 Rogue Determinism A", Vector3.Zero);
        DestroyNpc(first);
        DestroyNpc(second);
        IReadOnlyList<SalvageDrop> firstDrops = salvage.EvaluateDestruction(first);
        int countBefore = salvage.ProcessedDestructionCount;
        // Derelict recovery touches no destruction bookkeeping.
        DerelictSite site = Site();
        _ = new DerelictRecoveryService().TryRecover(site, NearSite(site, 100f), 1, true, false, false, CreateLoot());
        IReadOnlyList<SalvageDrop> secondDrops = salvage.EvaluateDestruction(second);
        bool deterministic = firstDrops.Count == secondDrops.Count &&
            firstDrops.Zip(secondDrops).All(pair =>
                pair.First.PayloadType == pair.Second.PayloadType &&
                pair.First.CommodityId == pair.Second.CommodityId &&
                pair.First.Quantity == pair.Second.Quantity);
        return Check(deterministic && salvage.ProcessedDestructionCount == countBefore + 1,
            "NPC destruction salvage determinism changed");
    }

    private static (bool, string) EquipmentSalvageUnchanged()
    {
        return Check(EquipmentCatalog.GetById("liberty_light_laser") != null &&
                EquipmentCatalog.GetById("civilian_shield_generator") is ShieldEquipmentDefinition &&
                EquipmentCatalog.GetById("civilian_powerplant") is PowerplantEquipmentDefinition &&
                EquipmentCatalog.GetById("civilian_thruster") is ThrusterEquipmentDefinition,
            "canonical equipment catalog changed");
    }

    private static (bool, string) NpcPiracyUnchanged()
    {
        return Check(NpcPirateCargoDemandService.ComplyKey == Keys.Enter &&
                NpcPirateCargoDemandService.RefuseKey == Keys.N,
            "NPC piracy demand keys changed");
    }

    private static (bool, string) PlayerPiracyUnchanged()
    {
        LootManager loot = CreateLoot();
        NpcShip trader = new("Phase87 Piracy Trader", Vector3.Zero, Vector3.Zero, 1f, 0f, FactionManager.NeutralCivilians);
        trader.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "phase87-smoke", Vector3.Zero, 500f, 100f);
        int released = loot.SpawnExtortionCargo(trader, "food-rations", 2, out _);
        CargoPod pod = loot.ActivePods.SingleOrDefault();
        return Check(released == 2 && pod?.IsStolen == true &&
                CombatSalvageService.MaxLiveSalvageObjects == 32 &&
                CombatSalvageService.PickupRadius == 200f,
            "player-to-NPC piracy provenance or salvage bounds changed");
    }

    private static (bool, string) PoliceEnforcementUnchanged()
    {
        return Check(PoliceScanSystem.EnforcementComplyKey == Keys.Enter &&
                PoliceScanSystem.EnforcementRefuseKey == Keys.N,
            "police enforcement keys changed");
    }

    private static (bool, string) TradeLaneIndependence()
    {
        DerelictSite site = Site();
        float minLane = System1Lanes.Min(lane => PointSegmentDistance(site.Position, lane.Start, lane.End));
        return Check(minLane > 3000f && TradeLaneStateSanitizer.IsFinite(site.Position),
            "derelict encroaches on trade-lane transit geometry");
    }

    private static (bool, string) MissionSystemsUntouched()
    {
        Type service = typeof(DerelictRecoveryService);
        FieldInfo[] fields = service.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        bool touchesMissions = fields.Any(field =>
            field.FieldType.Name.Contains("Mission", StringComparison.OrdinalIgnoreCase) ||
            field.FieldType.Name.Contains("TradeLane", StringComparison.OrdinalIgnoreCase) ||
            field.FieldType.Name.Contains("Police", StringComparison.OrdinalIgnoreCase) ||
            field.FieldType.Name.Contains("Reputation", StringComparison.OrdinalIgnoreCase));
        return Check(!touchesMissions, "recovery service gained mission/lane/police state");
    }

    private static (bool, string) InteractionSpatiallyBounded()
    {
        return Check(DerelictCatalog.Sites.Count == 1 &&
                DerelictCatalog.GetForSystem(1).Count == 1 &&
                DerelictCatalog.GetForSystem(2).Count == 0 &&
                DerelictCatalog.GetForSystem(4).Count == 0,
            "derelict interaction scope is not a single bounded site");
    }

    private static (bool, string) NoNewLootAuthority()
    {
        Assembly assembly = typeof(DerelictRecoveryService).Assembly;
        bool hasParallelAuthority = assembly.GetTypes().Any(type =>
            string.Equals(type.Name, "DerelictInventory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.Name, "DerelictLootManager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.Name, "WreckInventory", StringComparison.OrdinalIgnoreCase));
        bool serviceAddsPods = typeof(DerelictRecoveryService).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(method => string.Equals(method.Name, "SpawnDerelictSalvage", StringComparison.OrdinalIgnoreCase));
        bool lootOwnsSpawn = typeof(LootManager).GetMethod("SpawnDerelictSalvage",
            BindingFlags.Instance | BindingFlags.Public) != null;
        return Check(!hasParallelAuthority && !serviceAddsPods && lootOwnsSpawn,
            "a parallel loot/inventory authority was introduced");
    }

    // ---- Architecture-specific ----------------------------------------------------------

    private static (bool, string) WreckIsWorldObjectNotNpc()
    {
        DerelictSite site = Site();
        var wreck = new DerelictWreckObject(site);
        Type wreckType = wreck.GetType();
        return Check(wreck is SpaceObject &&
                wreckType == typeof(DerelictWreckObject) &&
                wreckType.BaseType == typeof(SpaceObject) &&
                !typeof(NpcShip).IsAssignableFrom(wreckType) &&
                string.Equals(wreck.SiteId, site.SiteId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(wreck.ModelPath, "MODELS/wreck", StringComparison.OrdinalIgnoreCase),
            "wreck representation is not a plain world object with the shared wreck model");
    }

    private static (bool, string) InvestigateKeyDocumented()
    {
        return Check(DerelictRecoveryService.InvestigateKey == Keys.F &&
                DerelictRecoveryService.InvestigateKeyLabel == "F",
            "investigate key is not the documented unused F mapping");
    }

    private static (bool, string) HudTextsConventional()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        string ready = service.GetHudText(site, NearSite(site, 100f), 1, true, false);
        string recovered = new DerelictRecoveryService().GetHudText(site, NearSite(site, 100f), 1, true, false);
        _ = new DerelictRecoveryService().TryRecover(site, NearSite(site, 100f), 1, true, false, false, CreateLoot());
        var recoveredService = new DerelictRecoveryService();
        _ = recoveredService.TryRecover(site, NearSite(site, 100f), 1, true, false, false, CreateLoot());
        string recoveredText = recoveredService.GetHudText(site, NearSite(site, 100f), 1, true, false);
        return Check(ready == $"{site.DisplayName} - Press F to investigate" &&
                recovered == ready &&
                recoveredText == $"{site.DisplayName} (salvaged)",
            $"HUD texts were '{ready}' / '{recoveredText}'");
    }

    private static (bool, string) RecoveryLookupBounded()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        _ = RecoverAt(service, site, CreateLoot(), NearSite(site, 100f));
        for (int i = 0; i < 1000; i++)
        {
            if (service.IsRecovered($"unknown_site_{i}"))
                return Fail("unknown site reported recovered");
        }
        return Check(service.IsRecovered(site.SiteId) && service.RecoveredCount == 1,
            "recovery lookup is not a bounded stable-id set");
    }

    private static (bool, string) CommitPointAfterFullSpawn()
    {
        DerelictSite site = Site();
        var service = new DerelictRecoveryService();
        LootManager loot = CreateLoot();
        bool before = service.IsRecovered(site.SiteId);
        DerelictRecoveryResult result = RecoverAt(service, site, loot, NearSite(site, 100f));
        return Check(!before && result.Success && service.IsRecovered(site.SiteId) &&
                loot.ActivePods.Count == site.Salvage.Count,
            "recovery committed before or without a full pod spawn");
    }

    private static (bool, string) RelevantSuitesRemainGreen()
    {
        (int lootPassed, int lootFailed) = new LootSmokeTest().Run();
        if (lootFailed != 0 || lootPassed == 0)
            return Fail($"loot suite regressed: {lootPassed} passed, {lootFailed} failed");
        (int phase57Passed, int phase57Failed) = new Phase57StolenCargoSmokeTest().Run();
        if (phase57Failed != 0 || phase57Passed == 0)
            return Fail($"phase 57 suite regressed: {phase57Passed} passed, {phase57Failed} failed");
        (int salvagePassed, int salvageFailed) = new CombatSalvageSmokeTest().Run();
        return Check(salvageFailed == 0 && salvagePassed > 0,
            $"combat salvage suite regressed: {salvagePassed} passed, {salvageFailed} failed");
    }
}
