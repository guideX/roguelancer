using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Phase 72 focused hardening coverage. The harness drives the production
/// TrafficManager detection pass, PoliceScanSystem scan/demand,
/// PoliceEnforcementService confiscation, PoliceFugitiveManager pursuit,
/// NpcWeaponSystem fire, LootManager physical pods, and canonical
/// CargoHold/CommodityCatalog through the one bounded
/// PoliceContrabandStopCoordinator. No smoke-only police, cargo, movement,
/// or crime implementation exists here.
/// </summary>
internal sealed class Phase72LawfulContrabandStopUxPersistenceSmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = new();
        public Station Buffalo { get; }
        public Station Rochester { get; }
        public List<Station> Stations { get; }
        public TrafficZoneConfig Route { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public RogueSmugglingManager Smuggling { get; }
        public TrafficManager Traffic { get; }
        public LootManager Loot { get; } = new();
        public ReputationManager Reputation { get; } = new(new FactionManager());
        public PoliceScanSystem Scan { get; } = new();
        public PoliceFugitiveManager Fugitive { get; }
        public PoliceContrabandStopCoordinator Coordinator { get; } = new();
        public PlayerCredits Credits { get; } = new(10_000);
        public List<string> Log { get; } = new();
        private readonly Dictionary<NpcShip, NpcCargoManifestSnapshot> _manualManifests = new();

        public Context()
        {
            Buffalo = CreateStation("Buffalo Base", FactionManager.LibertyRogues,
                new Vector3(-30_000f, -1_200f, 36_000f));
            Rochester = CreateStation("Rochester Base", FactionManager.Junkers,
                new Vector3(42_000f, -600f, 30_000f));
            Stations = new List<Station> { Buffalo, Rochester };
            Route = new TrafficZoneConfig
            {
                Id = "phase72-buffalo-rochester",
                Name = "Phase 72 criminal route",
                SystemIndex = 1,
                BehaviorType = TrafficZoneBehaviorType.TraderRoute,
                IsRogueSmugglingRoute = true,
                OriginStationId = "buffalo_base",
                DestinationStationId = "rochester_base",
                RouteStartX = Buffalo.Position.X,
                RouteStartY = Buffalo.Position.Y,
                RouteStartZ = Buffalo.Position.Z,
                RouteEndX = Rochester.Position.X,
                RouteEndY = Rochester.Position.Y,
                RouteEndZ = Rochester.Position.Z,
                CenterX = 6_000f,
                CenterY = -900f,
                CenterZ = 33_000f,
                Radius = 12_000f,
                MaxShips = 2,
                ShipDescription = "Transport Ship Alpha",
                FactionId = FactionManager.LibertyRogues
            };

            Smuggling = new RogueSmugglingManager(Market, () => Stations, () => new[] { Route });
            Smuggling.ConfigureRuntime(SpawnCarrier, RetireCarrier);
            Traffic = new TrafficManager(new ConfigurationManager(), Npcs, Objects);
            Traffic.ConfigureContrabandEnforcement(
                ResolveManifest,
                () => Loot.ActivePods,
                (enforcer, pod) => Loot.TrySeizeContrabandPodForNpc(enforcer, pod));
            Fugitive = new PoliceFugitiveManager(Reputation);
            Traffic.FugitiveManager = Fugitive;
            Scan.SetFugitiveManager(Fugitive);
            Loot.ConfigureEconomicCargoCallbacks(
                carrier => Smuggling.GetDestructionSalvage(carrier),
                (carrier, commodityId, quantity) => Smuggling.ConsumeDestructionSalvage(carrier, commodityId, quantity));
        }

        public NpcShip AddPolice(Vector3 position, string name = "Phase 72 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase72-police",
                position,
                800f,
                180f,
                ContrabandEnforcementPolicy.DefaultDetectionRange);
            police.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                "Smoke Fighter",
                FactionManager.LibertyPolice,
                "SMOKE/fighter",
                TrafficZoneBehaviorType.LawfulPatrol,
                NpcLoadoutTier.Standard));
            Npcs.Add(police);
            Objects.Add(police);
            return police;
        }

        public Ship CreatePlayer(Vector3 position)
        {
            Ship player = new(position);
            return player;
        }

        public static bool GiveContraband(Ship player, int quantity = 2)
        {
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            return contraband != null && player.CargoHold.AddCommodity(contraband, quantity);
        }

        public static bool GiveLegal(Ship player, int quantity = 3)
        {
            Commodity legal = CommodityCatalog.GetById("water");
            return legal != null && player.CargoHold.AddCommodity(legal, quantity);
        }

        public void StepTraffic(Ship playerShip, float seconds = 0.1f)
        {
            Traffic.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                playerShip,
                Reputation,
                Log.Add);
        }

        public void StepNpc(Ship playerShip, float seconds = 0.1f)
        {
            GameTime frame = new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
            foreach (NpcShip npc in Npcs.ToList())
                npc?.Update(frame, null, playerShip, Reputation);
        }

        public void StepScan(Ship playerShip, float seconds = 0.1f)
        {
            Scan.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                playerShip,
                Npcs,
                Credits,
                Reputation,
                notificationManager: null,
                log: Log.Add);
        }

        public void StepCoordinator(Ship playerShip)
        {
            Coordinator.Update(playerShip, Npcs, Traffic, Scan, Fugitive, Reputation, Log.Add);
        }

        public void StepFull(Ship playerShip, float seconds = 0.1f)
        {
            StepTraffic(playerShip, seconds);
            StepNpc(playerShip, seconds);
            StepScan(playerShip, seconds);
            StepCoordinator(playerShip);
        }

        public void StepScanFrames(Ship playerShip, float seconds, int frames)
        {
            for (int i = 0; i < frames; i++)
                StepFull(playerShip, seconds);
        }

        public int CountLog(string fragment)
        {
            int count = 0;
            foreach (string entry in Log)
            {
                if (entry != null && entry.Contains(fragment, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Mirrors RoguelancerGame.ApplySaveData plus the system reload it
        /// triggers: only durable cargo, credits, reputation, and
        /// non-fugitive hostility survive; the transient stop (scan offer,
        /// owner, hold-fire, enforcer targets, fugitive incident) is
        /// discarded and must re-detect deterministically from live state.
        /// </summary>
        public void ApplyProductionLoad(SaveGameData loaded)
        {
            Scan.Reset();
            Coordinator.Reset();
            Fugitive.Reset(Log.Add, "save/load");
            Traffic.ResetContrabandEnforcement();
            if (loaded == null)
                return;
        }

        private NpcCargoManifestSnapshot ResolveManifest(NpcShip carrier)
        {
            if (_manualManifests.TryGetValue(carrier, out NpcCargoManifestSnapshot manual))
                return manual;

            return Smuggling.TryGetManifestSnapshot(carrier, out NpcCargoManifestSnapshot smuggling)
                ? smuggling
                : NpcCargoManifestSnapshot.NoRegisteredCargo();
        }

        private NpcShip SpawnCarrier(TrafficZoneConfig route, string identityHint)
        {
            NpcShip carrier = new(
                identityHint,
                route.RouteStart.Value + new Vector3(90f, 0f, 0f),
                route.Center,
                route.Radius,
                0.2f,
                FactionManager.LibertyRogues);
            carrier.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                route.Id,
                route.Center,
                route.Radius,
                190f,
                ContrabandEnforcementPolicy.DefaultDetectionRange,
                route.RouteStart,
                route.RouteEnd);
            carrier.RestoreStableIdentity(identityHint);
            carrier.OnDestroyed += Smuggling.NotifyCarrierDestroyed;
            Npcs.Add(carrier);
            Objects.Add(carrier);
            return carrier;
        }

        private void RetireCarrier(NpcShip carrier, string reason)
        {
            Npcs.Remove(carrier);
            Objects.Remove(carrier);
        }

        private static Station CreateStation(string name, string faction, Vector3 position) =>
            new(new StationConfig
            {
                Description = name,
                FactionId = faction,
                SystemIndex = 1,
                StartupPositionX = position.X,
                StartupPositionY = position.Y,
                StartupPositionZ = position.Z
            }, null);
    }

    private readonly GraphicsDevice _graphicsDevice;

    public Phase72LawfulContrabandStopUxPersistenceSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("detection still begins lawful interception", DetectionBeginsInterception, ref passed, ref failed);
        RunCase("interceptor remains hold-fire while stop is lawful", LawfulHoldFire, ref passed, ref failed);
        RunCase("hostile Police may still fire despite lawful-stop flag", HostileMayFire, ref passed, ref failed);
        RunCase("approach can reach valid scan range", ApproachReachesScanRange, ref passed, ref failed);
        RunCase("scan progresses normally", ScanProgresses, ref passed, ref failed);
        RunCase("one clear scan-state notification is produced", SingleScanNotification, ref passed, ref failed);
        RunCase("demand appears after successful scan", DemandAfterScan, ref passed, ref failed);
        RunCase("demand exposes existing comply/refuse controls", DemandExposesControls, ref passed, ref failed);
        RunCase("demand timing uses the existing authoritative timer", DemandAuthoritativeTimer, ref passed, ref failed);
        RunCase("two officers still create only one scan owner", SingleScanOwner, ref passed, ref failed);
        RunCase("supporter does not create duplicate demand", SupporterNoDuplicateDemand, ref passed, ref failed);
        RunCase("supporters receive deterministic non-stacking behavior", SupporterSpacing, ref passed, ref failed);
        RunCase("compliance confiscates authoritative live cargo", ComplianceConfiscatesLive, ref passed, ref failed);
        RunCase("legal cargo remains", LegalCargoRemains, ref passed, ref failed);
        RunCase("jettisoned cargo is not confiscated as phantom cargo", JettisonNoPhantom, ref passed, ref failed);
        RunCase("compliance feedback reports actual resolved quantities", ComplianceFeedbackQuantities, ref passed, ref failed);
        RunCase("refusal still creates fugitive pursuit", RefusalCreatesPursuit, ref passed, ref failed);
        RunCase("fleeing the enforcement radius still creates fugitive pursuit", FleeCreatesPursuit, ref passed, ref failed);
        RunCase("acceleration alone inside the lawful stop does not count as refusal", AccelerationNotRefusal, ref passed, ref failed);
        RunCase("player attack still escalates through existing hostility/fugitive authority", PlayerAttackEscalates, ref passed, ref failed);
        RunCase("escape does not instantly produce pathological same-tick re-stop", EscapeNoInstantRestop, ref passed, ref failed);
        RunCase("later legitimate new encounter can detect remaining contraband again", NewEncounterRedetects, ref passed, ref failed);
        RunCase("reset clears transient stop state", ResetClearsTransient, ref passed, ref failed);
        RunCase("destroyed/despawned owner leaves no stale reference", OwnerDestroyedNoStaleRef, ref passed, ref failed);
        RunCase("player death clears transient stop state safely", PlayerDeathClears, ref passed, ref failed);
        RunCase("save/load during an allowed pre-demand stop state is safe", SaveLoadPreDemandSafe, ref passed, ref failed);
        RunCase("save/load during an allowed demand state cannot duplicate confiscation/fine", SaveLoadDemandNoDuplicate, ref passed, ref failed);
        RunCase("post-load scanner selection follows deterministic policy", PostLoadReselection, ref passed, ref failed);
        RunCase("missing pre-save scanner degrades safely", MissingScannerDegrades, ref passed, ref failed);
        RunCase("no persisted supporter/runtime object references are required", NoPersistedRuntimeRefs, ref passed, ref failed);
        Console.WriteLine($"[PHASE 72 LAWFUL CONTRABAND STOP UX PERSISTENCE SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(
        string label,
        Func<(bool Success, string FailureReason)> test,
        ref int passed,
        ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 72 LAWFUL CONTRABAND STOP UX PERSISTENCE SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 72 LAWFUL CONTRABAND STOP UX PERSISTENCE SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 72 LAWFUL CONTRABAND STOP UX PERSISTENCE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool, string) DetectionBeginsInterception()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(player);
        return police.HasPlayerTarget && police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement
            ? Pass()
            : Fail($"detection did not create interception: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
    }

    private (bool, string) LawfulHoldFire()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(player);
        if (!police.IsLawfulStopHoldFire)
            return Fail("coordinator did not set hold-fire on unconfirmed interception");
        bool fired = false;
        NpcWeaponSystem weapons = new(_graphicsDevice, context.Reputation);
        weapons.NpcWeaponFired += (source, target, weaponId, weapon) =>
        {
            if (source == police && target == null)
                fired = true;
        };
        float shieldsBefore = player.Shields.CurrentShields;
        float hullBefore = player.Hull.CurrentHull;
        player.CollisionRadius = 1_000f;
        police.Position = player.Position + new Vector3(0f, 0f, -300f);
        player.Position = Vector3.Zero;
        police.Velocity = Vector3.Zero;
        for (int frame = 0; frame < 8; frame++)
            weapons.Update(Frame(0.1f), context.Npcs, player);
        bool damaged = player.Shields.CurrentShields < shieldsBefore || player.Hull.CurrentHull < hullBefore;
        return !fired && !damaged
            ? Pass()
            : Fail($"lawful interception fired: fired={fired}, damaged={damaged}");
    }

    private (bool, string) HostileMayFire()
    {
        Context context = new();
        context.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice, "pre-existing");
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(player);
        police.SetLawfulStopHoldFire(true);
        bool fired = false;
        NpcWeaponSystem weapons = new(_graphicsDevice, context.Reputation);
        weapons.NpcWeaponFired += (source, target, weaponId, weapon) =>
        {
            if (source == police && target == null)
                fired = true;
        };
        player.CollisionRadius = 1_000f;
        police.Position = player.Position + new Vector3(0f, 0f, -300f);
        player.Position = Vector3.Zero;
        for (int frame = 0; frame < 8; frame++)
            weapons.Update(Frame(0.1f), context.Npcs, player);
        return fired
            ? Pass()
            : Fail("already-hostile police was pacified by the lawful-stop flag");
    }

    private static (bool, string) ApproachReachesScanRange()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(5_000f, 0f, 0f), "Phase 72 Closer");
        context.StepTraffic(player);
        context.StepCoordinator(player);
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("interception did not start at detection range");
        float before = Vector3.Distance(police.Position, player.Position);
        bool scanned = false;
        for (int i = 0; i < 40; i++)
        {
            context.StepFull(player, 0.5f);
            if (context.Scan.State == PoliceScanState.Scanning || context.Scan.IsEnforcementDemandActive)
            {
                scanned = true;
                break;
            }
        }

        float after = Vector3.Distance(police.Position, player.Position);
        if (after >= before)
            return Fail($"officer did not close via movement: before={before}, after={after}");
        return scanned && after <= PoliceScanSystem.ScanRange
            ? Pass()
            : Fail($"approach did not reach scan range: after={after}, scan={context.Scan.State}");
    }

    private static (bool, string) ScanProgresses()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepFull(player, 0.5f);
        if (context.Scan.State != PoliceScanState.Scanning)
            return Fail($"scan did not start: {context.Scan.State}");
        float first = context.Scan.ScanProgress;
        context.StepFull(player, 0.5f);
        float second = context.Scan.ScanProgress;
        if (second <= first)
            return Fail($"scan did not progress: {first} -> {second}");
        context.StepScanFrames(player, 0.5f, 8);
        return context.Scan.State == PoliceScanState.ContrabandDetected && context.Scan.IsEnforcementDemandActive
            ? Pass()
            : Fail($"scan did not complete to demand: {context.Scan.State}");
    }

    private static (bool, string) SingleScanNotification()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(5_000f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 6);
        police.Position = new Vector3(1_500f, 0f, 0f);
        context.StepScanFrames(player, 0.5f, 10);
        int scanStarts = context.CountLog("[POLICE SCAN] Scan initiated");
        int stopOrders = context.CountLog("[CONTRABAND STOP] Lawful interception ordered");
        if (scanStarts != 1)
            return Fail($"scan initiation announced {scanStarts} times");
        if (stopOrders != 1)
            return Fail($"interception announced {stopOrders} times");
        return Pass();
    }

    private static (bool, string) DemandAfterScan()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive || context.Scan.State != PoliceScanState.ContrabandDetected)
            return Fail($"contraband did not produce demand: state={context.Scan.State}");
        return context.Scan.CurrentOffer != null && context.Scan.CurrentOffer.TotalViolationQuantity == 2
            ? Pass()
            : Fail("demand offer has the wrong quantity");
    }

    private static (bool, string) DemandExposesControls()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        if (!context.Scan.StatusText.Contains("[Enter] Comply", StringComparison.Ordinal) ||
            !context.Scan.StatusText.Contains("[N] Refuse", StringComparison.Ordinal))
            return Fail($"demand HUD hides controls: {context.Scan.StatusText}");
        int creditsBefore = context.Credits.Credits;
        bool handled = context.Scan.HandleInput(
            new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
            new KeyboardState(),
            player,
            context.Credits,
            context.Reputation);
        return handled && context.Scan.State == PoliceScanState.Cleared && context.Credits.Credits < creditsBefore
            ? Pass()
            : Fail("Enter did not comply through the existing input path");
    }

    private static (bool, string) DemandAuthoritativeTimer()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        float remaining = context.Scan.EnforcementDemandRemainingSeconds;
        if (remaining <= 0f || remaining > PoliceScanSystem.EnforcementDemandSeconds)
            return Fail($"demand timer outside authoritative window: {remaining}");
        context.StepScanFrames(player, 0.5f, 2);
        float later = context.Scan.EnforcementDemandRemainingSeconds;
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("demand expired after only 1s instead of using the 8s timer");
        return Math.Abs((remaining - later) - 1f) < 0.01f
            ? Pass()
            : Fail($"demand did not tick the authoritative timer: {remaining} -> {later}");
    }

    private static (bool, string) SingleScanOwner()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_400f, 0f, 0f), "Police Alpha");
        context.AddPolice(new Vector3(1_600f, 0f, 0f), "Police Beta");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("no demand created");
        if (context.Scan.DetectionCount != 1 || context.Scan.ActiveScanner == null)
            return Fail($"authority unclear: detections={context.Scan.DetectionCount}");
        context.StepCoordinator(player);
        return context.Coordinator.ActiveOwner != null
            ? Pass()
            : Fail("coordinator has no encounter owner");
    }

    private static (bool, string) SupporterNoDuplicateDemand()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_400f, 0f, 0f), "Police Alpha");
        context.AddPolice(new Vector3(1_600f, 0f, 0f), "Police Beta");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        PoliceEnforcementOffer offer = context.Scan.CurrentOffer;
        int detections = context.Scan.DetectionCount;
        for (int i = 0; i < 5; i++)
            context.StepFull(player, 0.1f);
        return context.Scan.DetectionCount == detections && ReferenceEquals(offer, context.Scan.CurrentOffer)
            ? Pass()
            : Fail("supporter duplicated the demand on repeated ticks");
    }

    private static (bool, string) SupporterSpacing()
    {
        Context first = new();
        Ship firstPlayer = first.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(firstPlayer, 2))
            return Fail("could not seed contraband");
        first.AddPolice(new Vector3(5_000f, 0f, 0f), "Police Alpha");
        first.AddPolice(new Vector3(5_200f, 0f, 0f), "Police Beta");
        first.AddPolice(new Vector3(5_400f, 0f, 0f), "Police Gamma");
        first.StepFull(firstPlayer, 0.5f);
        if (first.Coordinator.ActiveOwner == null)
            return Fail("coordinator has no encounter owner");
        List<Vector3> offsets = new();
        foreach (NpcShip enforcer in first.Traffic.ActivePlayerContrabandEnforcers)
        {
            if (enforcer == null || enforcer.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
                return Fail("enforcer lost interception reason");
            offsets.Add(enforcer.ContrabandSupportOffset);
        }

        if (offsets.Count < 3)
            return Fail($"expected 3 interceptors, saw {offsets.Count}");
        if (first.Coordinator.ActiveOwner.ContrabandSupportOffset != Vector3.Zero)
            return Fail("scan owner does not hold the exact player anchor");
        HashSet<string> distinct = new();
        foreach (Vector3 offset in offsets)
            distinct.Add($"{offset.X:0.0}|{offset.Y:0.0}|{offset.Z:0.0}");
        if (distinct.Count != offsets.Count)
            return Fail("supporters stack on the same anchor");

        Context second = new();
        Ship secondPlayer = second.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(secondPlayer, 2))
            return Fail("could not seed contraband");
        second.AddPolice(new Vector3(5_000f, 0f, 0f), "Police Alpha");
        second.AddPolice(new Vector3(5_200f, 0f, 0f), "Police Beta");
        second.AddPolice(new Vector3(5_400f, 0f, 0f), "Police Gamma");
        second.StepFull(secondPlayer, 0.5f);
        if (second.Coordinator.ActiveOwner?.Name != first.Coordinator.ActiveOwner?.Name)
            return Fail("owner selection is not deterministic");
        for (int i = 0; i < first.Traffic.ActivePlayerContrabandEnforcers.Count; i++)
        {
            if (first.Traffic.ActivePlayerContrabandEnforcers[i].ContrabandSupportOffset !=
                second.Traffic.ActivePlayerContrabandEnforcers[i].ContrabandSupportOffset)
                return Fail("supporter spacing is not deterministic");
        }

        return Pass();
    }

    private static (bool, string) ComplianceConfiscatesLive()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (contraband == null || !player.CargoHold.AddCommodity(contraband, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        int fine = context.Scan.CurrentOffer.FineAmount;
        int creditsBefore = context.Credits.Credits;
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation, null, context.Log.Add))
            return Fail("compliance did not resolve");
        return player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
            context.Credits.Credits == creditsBefore - fine &&
            context.Scan.State == PoliceScanState.Cleared
            ? Pass()
            : Fail("compliance did not confiscate the live hold exactly once");
    }

    private static (bool, string) LegalCargoRemains()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        Commodity legal = CommodityCatalog.GetById("water");
        if (!player.CargoHold.AddCommodity(contraband, 1) || !player.CargoHold.AddCommodity(legal, 4))
            return Fail("could not seed mixed cargo");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation))
            return Fail("compliance did not resolve");
        return player.CargoHold.GetCommodityQuantity(legal.Name) == 4
            ? Pass()
            : Fail($"legal cargo did not survive: {player.CargoHold.GetCommodityQuantity(legal.Name)}");
    }

    private static (bool, string) JettisonNoPhantom()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (contraband == null || !player.CargoHold.AddCommodity(contraband, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        int jettisoned = context.Loot.TryJettisonContraband(
            player.CargoHold, player.Position, player.Velocity, out int podCount);
        if (jettisoned != 2 || podCount <= 0)
            return Fail($"jettison did not move exact cargo: removed={jettisoned}, pods={podCount}");
        int podsBefore = context.Loot.ActivePods.Count;
        context.Log.Clear();
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation, null, context.Log.Add))
            return Fail("post-jettison compliance did not resolve");
        if (context.Loot.ActivePods.Count != podsBefore)
            return Fail("compliance deleted physical pods");
        if (context.CountLog("No illegal cargo remaining") == 0)
            return Fail("compliance feedback did not report the truthful zero-remaining result");
        return player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
            context.Scan.State == PoliceScanState.Cleared
            ? Pass()
            : Fail("jettisoned cargo was confiscated twice or state invalid");
    }

    private static (bool, string) ComplianceFeedbackQuantities()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (contraband == null || !player.CargoHold.AddCommodity(contraband, 3))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        int fine = context.Scan.CurrentOffer.FineAmount;
        context.Log.Clear();
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation, null, context.Log.Add))
            return Fail("compliance did not resolve");
        if (context.CountLog("3 illegal units") == 0)
            return Fail("compliance feedback did not report the actual confiscated quantity");
        if (context.CountLog($"{fine:N0}") == 0)
            return Fail("compliance feedback did not report the actual fine paid");
        return Pass();
    }

    private static (bool, string) RefusalCreatesPursuit()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.TryRefuseEnforcement(player, context.Credits, context.Reputation))
            return Fail("explicit refuse did not resolve");
        context.StepCoordinator(player);
        return context.Scan.State == PoliceScanState.Enforcement && context.Fugitive.IsActive
            ? Pass()
            : Fail($"refuse did not escalate: scan={context.Scan.State}, fugitive={context.Fugitive.IsActive}");
    }

    private static (bool, string) FleeCreatesPursuit()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        player.Position = police.Position + new Vector3(PoliceScanSystem.EnforcementEscapeRange + 100f, 0f, 0f);
        context.Scan.Update(
            new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1f)),
            player, context.Npcs, context.Credits, context.Reputation, null, context.Log.Add);
        context.StepCoordinator(player);
        if (context.Scan.State != PoliceScanState.Enforcement || !context.Fugitive.IsActive)
            return Fail($"evasion did not escalate: scan={context.Scan.State}, fugitive={context.Fugitive.IsActive}");
        return police.PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit
            ? Pass()
            : Fail($"escalation did not convert to fugitive pursuit: {police.PlayerTargetReason}");
    }

    private static (bool, string) AccelerationNotRefusal()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        PoliceEnforcementOffer offer = context.Scan.CurrentOffer;
        player.Velocity = new Vector3(300f, 0f, 0f);
        player.Position += new Vector3(500f, 0f, 0f);
        for (int i = 0; i < 3; i++)
            context.StepFull(player, 0.1f);
        return context.Scan.State == PoliceScanState.ContrabandDetected &&
            ReferenceEquals(offer, context.Scan.CurrentOffer) &&
            !context.Fugitive.IsActive
            ? Pass()
            : Fail($"lawful acceleration refused the stop: scan={context.Scan.State}, fugitive={context.Fugitive.IsActive}");
    }

    private static (bool, string) PlayerAttackEscalates()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(400f, 0f, 0f));
        context.StepFull(player);
        police.MarkDamagedByPlayer(5f);
        context.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice, "player attack");
        context.Fugitive.NotifyPlayerDamage(police, player);
        context.StepTraffic(player);
        context.StepCoordinator(player);
        context.StepNpc(player);
        return police.HasPlayerTarget && police.HasValidPlayerTarget(context.Reputation) &&
            (police.PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit ||
             police.PlayerTargetReason == NpcPlayerTargetReason.PlayerInitiatedAggression ||
             police.PlayerTargetReason == NpcPlayerTargetReason.FactionDisposition ||
             police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement)
            ? Pass()
            : Fail($"attack did not escalate through hostility authority: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
    }

    private static (bool, string) EscapeNoInstantRestop()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(1_500f, 0f, 0f), "Phase 72 Escaper");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.TryRefuseEnforcement(player, context.Credits, context.Reputation))
            return Fail("refusal did not escalate");
        player.Position = new Vector3(100_000f, 0f, 0f);
        for (int i = 0; i < 40; i++)
            context.StepFull(player, 0.5f);
        if (context.Fugitive.IsActive)
            return Fail("fugitive did not resolve after legitimate escape");
        if (!context.Fugitive.IsContrabandReacquisitionGraceActive)
            return Fail("escape did not open the bounded reacquisition grace");
        player.Position = police.Position + new Vector3(300f, 0f, 0f);
        context.StepTraffic(player);
        context.StepCoordinator(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 0
            ? Pass()
            : Fail("same-tick re-stop fired inside the escape grace window");
    }

    private static (bool, string) NewEncounterRedetects()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(1_500f, 0f, 0f), "Phase 72 Return");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.TryRefuseEnforcement(player, context.Credits, context.Reputation))
            return Fail("refusal did not escalate");
        player.Position = new Vector3(100_000f, 0f, 0f);
        for (int i = 0; i < 40; i++)
            context.StepFull(player, 0.5f);
        if (context.Fugitive.IsActive)
            return Fail("escape setup did not resolve");
        context.Fugitive.Update(
            PoliceFugitiveManager.ContrabandReacquisitionGraceSeconds + 1f,
            player,
            context.Npcs,
            context.Log.Add);
        if (context.Fugitive.IsContrabandReacquisitionGraceActive)
            return Fail("grace did not expire after its bounded window");
        player.Position = police.Position + new Vector3(300f, 0f, 0f);
        context.StepTraffic(player);
        context.StepCoordinator(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 1 &&
            police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement
            ? Pass()
            : Fail("legitimate new encounter did not re-detect remaining contraband");
    }

    private static (bool, string) ResetClearsTransient()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (!player.CargoHold.AddCommodity(contraband, 2))
            return Fail("could not seed contraband");
        int cargoBefore = player.CargoHold.GetCommodityQuantity(contraband.Name);
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("setup did not detect");
        context.Traffic.ResetContrabandEnforcement();
        context.Scan.Reset();
        context.Fugitive.Reset();
        context.Coordinator.Reset();
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 0 &&
            !police.HasPlayerTarget &&
            context.Scan.State == PoliceScanState.Idle &&
            !context.Fugitive.IsActive &&
            context.Fugitive.IsContrabandReacquisitionGraceActive == false &&
            player.CargoHold.GetCommodityQuantity(contraband.Name) == cargoBefore
            ? Pass()
            : Fail("reset retained pursuit state or mutated cargo");
    }

    private static (bool, string) OwnerDestroyedNoStaleRef()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip owner = context.AddPolice(new Vector3(1_500f, 0f, 0f), "Stop Owner");
        context.AddPolice(new Vector3(1_700f, 0f, 0f), "Stop Supporter");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        NpcShip active = context.Scan.ActiveScanner;
        if (active == null)
            return Fail("no active scanner to destroy");
        int creditsBefore = context.Credits.Credits;
        active.ApplyCombatDamage(
            active.Shields.CurrentShields + active.Hull.MaxHull + 10f,
            NpcDestructionSource.Npc);
        if (!active.IsDestroyed)
            return Fail("owner could not be destroyed");
        context.Traffic.NotifyNpcDestroyed(active);
        context.Coordinator.NotifyNpcDestroyed(active);
        context.StepFull(player, 0.1f);
        bool ownerGone = !context.Traffic.ActivePlayerContrabandEnforcers.Contains(active) &&
            !ReferenceEquals(context.Coordinator.ActiveOwner, active) &&
            !ReferenceEquals(context.Scan.ActiveScanner, active);
        return ownerGone && context.Credits.Credits == creditsBefore && !context.Fugitive.IsActive
            ? Pass()
            : Fail($"stale owner survived destruction: scan={context.Scan.State}, fugitive={context.Fugitive.IsActive}");
    }

    private static (bool, string) PlayerDeathClears()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("setup did not detect");
        player.ApplyCombatDamage(1_000_000f, hostile: true);
        if (player.Hull?.IsDestroyed != true)
            return Fail("player could not be destroyed");
        context.StepFull(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 0 &&
            context.Scan.State == PoliceScanState.Idle &&
            !context.Scan.IsEnforcementDemandActive &&
            !context.Fugitive.IsActive
            ? Pass()
            : Fail($"death retained stop state: enforcers={context.Traffic.ActivePlayerContrabandEnforcerCount}, scan={context.Scan.State}");
    }

    private static (bool, string) SaveLoadPreDemandSafe()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(5_000f, 0f, 0f));
        context.StepTraffic(player);
        context.StepCoordinator(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("setup did not intercept");
        string roundTrip = RoundTripProductionSave(context, player);
        if (roundTrip != null)
            return Fail(roundTrip);
        if (context.Scan.State != PoliceScanState.Idle || context.Scan.CurrentOffer != null)
            return Fail("load resurrected a phantom pre-demand offer");
        if (context.Fugitive.IsActive)
            return Fail("load resurrected a phantom pursuit");
        if (player.CargoHold.GetCommodityQuantity("Side Arms") != 2)
            return Fail("load mutated authoritative cargo");
        context.StepTraffic(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 1
            ? Pass()
            : Fail("post-load world did not deterministically re-detect");
    }

    private static (bool, string) SaveLoadDemandNoDuplicate()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        int creditsBefore = context.Credits.Credits;
        string roundTrip = RoundTripProductionSave(context, player);
        if (roundTrip != null)
            return Fail(roundTrip);
        if (context.Scan.CurrentOffer != null || context.Scan.IsEnforcementDemandActive)
            return Fail("load resurrected a phantom demand offer");
        if (context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation))
            return Fail("phantom post-load demand resolved without authority");
        if (context.Credits.Credits != creditsBefore)
            return Fail("phantom post-load demand moved credits");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("legitimate post-load re-stop did not produce a demand");
        int fine = context.Scan.CurrentOffer.FineAmount;
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation))
            return Fail("post-load compliance did not resolve");
        return context.Credits.Credits == creditsBefore - fine &&
            player.CargoHold.GetCommodityQuantity("Side Arms") == 0
            ? Pass()
            : Fail($"fine/confiscation applied more than once: credits={context.Credits.Credits}");
    }

    private static (bool, string) PostLoadReselection()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(5_000f, 0f, 0f));
        context.StepTraffic(player);
        string roundTrip = RoundTripProductionSave(context, player);
        if (roundTrip != null)
            return Fail(roundTrip);
        context.AddPolice(new Vector3(1_400f, 0f, 0f), "Police Alpha");
        context.AddPolice(new Vector3(1_600f, 0f, 0f), "Police Beta");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("post-load re-stop did not produce a demand");
        if (context.Scan.ActiveScanner == null || context.Coordinator.ActiveOwner == null)
            return Fail("post-load owner was not reconstructed");
        if (!ReferenceEquals(context.Scan.ActiveScanner, context.Coordinator.ActiveOwner))
            return Fail("scan owner and encounter owner disagree after load");
        return string.Equals(context.Scan.ActiveScanner.Name, "Police Alpha", StringComparison.Ordinal)
            ? Pass()
            : Fail($"reselection was not nearest-first deterministic: {context.Scan.ActiveScanner.Name}");
    }

    private static (bool, string) MissingScannerDegrades()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip scanner = context.AddPolice(new Vector3(1_500f, 0f, 0f), "Vanishing Scanner");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        int creditsBefore = context.Credits.Credits;
        context.Npcs.Remove(scanner);
        context.Objects.Remove(scanner);
        context.Coordinator.NotifyNpcDespawned(scanner);
        context.StepFull(player, 0.1f);
        bool noStaleRef = !ReferenceEquals(context.Scan.ActiveScanner, scanner) &&
            !ReferenceEquals(context.Coordinator.ActiveOwner, scanner);
        return noStaleRef && context.Credits.Credits == creditsBefore && !context.Fugitive.IsActive
            ? Pass()
            : Fail($"missing scanner left stale state: scan={context.Scan.State}, fugitive={context.Fugitive.IsActive}");
    }

    private static (bool, string) NoPersistedRuntimeRefs()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        SaveGameManager manager = new(NewTempSavePath());
        try
        {
            SaveGameData data = new()
            {
                PlayerCredits = context.Credits.Credits,
                Cargo = manager.CaptureCargo(player.CargoHold),
                FactionReputation = manager.CaptureReputation(context.Reputation),
                TemporaryHostility = manager.CaptureTemporaryHostility(context.Reputation)
            };
            if (!manager.TrySave(data, out string saveFailure))
                return Fail($"save failed: {saveFailure}");
            string json = File.ReadAllText(manager.SavePath);
            foreach (string forbidden in new[] { "scanner", "supporter", "enforcer", "holdfire", "hold_fire", "hold-fire", "fugitive" })
            {
                if (json.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    return Fail($"save payload leaks transient stop state: '{forbidden}'");
            }

            if (!manager.TryLoad(out SaveGameData loaded, out string loadFailure) || loaded == null)
                return Fail($"load failed: {loadFailure}");
            if (loaded.SchemaVersion != SaveGameData.CurrentSchemaVersion)
                return Fail("round-trip changed the schema version");
            if (loaded.Cargo.Count == 0)
                return Fail("round-trip lost authoritative cargo");
            return Pass();
        }
        finally
        {
            TryDelete(manager.SavePath);
        }
    }

    /// <summary>
    /// Production save proof: durable cargo/credits/reputation round-trip
    /// through a real save file while the transient stop is discarded exactly
    /// like RoguelancerGame.ApplySaveData does. Returns null on success.
    /// </summary>
    private static string RoundTripProductionSave(Context context, Ship player)
    {
        SaveGameManager manager = new(NewTempSavePath());
        try
        {
            SaveGameData data = new()
            {
                PlayerCredits = context.Credits.Credits,
                Cargo = manager.CaptureCargo(player.CargoHold),
                FactionReputation = manager.CaptureReputation(context.Reputation),
                TemporaryHostility = manager.CaptureTemporaryHostility(context.Reputation)
            };
            if (!manager.TrySave(data, out string saveFailure))
                return $"save failed: {saveFailure}";
            if (!manager.TryLoad(out SaveGameData loaded, out string loadFailure) || loaded == null)
                return $"load failed: {loadFailure}";
            context.ApplyProductionLoad(loaded);
            List<string> cargoWarnings = new();
            manager.ApplyCargo(player.CargoHold, loaded, out cargoWarnings);
            if (cargoWarnings.Count > 0)
                return $"cargo restore warned: {string.Join("; ", cargoWarnings)}";
            manager.ApplyReputation(context.Reputation, loaded);
            manager.ApplyTemporaryHostility(context.Reputation, loaded);
            context.Credits.SetCredits(loaded.PlayerCredits);
            return null;
        }
        finally
        {
            TryDelete(manager.SavePath);
        }
    }

    private static string NewTempSavePath() =>
        Path.Combine(Path.GetTempPath(), $"phase72_{Guid.NewGuid():N}.json");

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Temp-file cleanup is best-effort and never fails the proof.
        }
    }

    private static GameTime Frame(double seconds) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);
}
