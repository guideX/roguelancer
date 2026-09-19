using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Phase 71 focused coverage. The harness reuses the production TrafficManager
/// detection pass, PoliceScanSystem scan/demand, PoliceEnforcementService
/// confiscation, PoliceFugitiveManager pursuit, NpcWeaponSystem fire,
/// LootManager physical pods, and canonical CargoHold/CommodityCatalog through
/// the one bounded PoliceContrabandStopCoordinator. No smoke-only police,
/// cargo, or crime implementation exists here.
/// </summary>
internal sealed class Phase71LawfulContrabandStopAndComplianceSmokeTest
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
                Id = "phase71-buffalo-rochester",
                Name = "Phase 71 criminal route",
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

        public RogueSmugglingShipment SpawnShipment()
        {
            Smuggling.Update(RogueSmugglingManager.SpawnIntervalSeconds + 1f, _ => { });
            return Smuggling.ActiveShipments.SingleOrDefault();
        }

        public NpcShip AddPolice(Vector3 position, string name = "Phase 71 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase71-police",
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
                _ => { });
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
                Reputation);
        }

        public void StepCoordinator(Ship playerShip)
        {
            Coordinator.Update(playerShip, Npcs, Traffic, Scan, Fugitive, Reputation, _ => { });
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

        public void SetManifest(NpcShip carrier, NpcCargoManifestSnapshot manifest)
        {
            _manualManifests[carrier] = manifest;
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

    public Phase71LawfulContrabandStopAndComplianceSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("detection initiates interception without contraband-only fire", DetectionHoldsFire, ref passed, ref failed);
        RunCase("officer closes using existing NPC movement", ClosesViaMovement, ref passed, ref failed);
        RunCase("scan begins only within valid scan range", ScanOnlyWithinRange, ref passed, ref failed);
        RunCase("legal cargo does not produce contraband demand", LegalProducesNoDemand, ref passed, ref failed);
        RunCase("contraband produces existing police demand", ContrabandProducesDemand, ref passed, ref failed);
        RunCase("compliant surrender removes only required contraband", ComplianceRemovesOnlyContraband, ref passed, ref failed);
        RunCase("legal cargo survives confiscation", LegalSurvivesConfiscation, ref passed, ref failed);
        RunCase("compliance clears interception state", ComplianceClearsInterception, ref passed, ref failed);
        RunCase("compliance does not create fugitive pursuit", ComplianceNoFugitive, ref passed, ref failed);
        RunCase("refusal timeout evasion escalates to fugitive", RefusalTimeoutEvasionEscalates, ref passed, ref failed);
        RunCase("fugitive escalation enables normal pursuit combat", FugitiveEnablesCombat, ref passed, ref failed);
        RunCase("player attack causes immediate valid hostility", PlayerAttackCausesHostility, ref passed, ref failed);
        RunCase("already-hostile police not suppressed", AlreadyHostileNotSuppressed, ref passed, ref failed);
        RunCase("jettisoned contraband cannot be confiscated twice", JettisonNoDoubleConfiscation, ref passed, ref failed);
        RunCase("clean hold before scan completion resolves safely", CleanBeforeCompletionResolves, ref passed, ref failed);
        RunCase("multiple officers create one scan authority", SingleScanAuthority, ref passed, ref failed);
        RunCase("repeated ticks do not duplicate demands", NoDuplicateOnRepeatedTicks, ref passed, ref failed);
        RunCase("scan owner destruction resolves safely", OwnerDestructionResolves, ref passed, ref failed);
        RunCase("despawn reset player destruction clears state", LifetimeClearsState, ref passed, ref failed);
        RunCase("Phase 68 regression still passes", Phase68Regression, ref passed, ref failed);
        RunCase("Phase 69 regression still passes", Phase69Regression, ref passed, ref failed);
        RunCase("Phase 70 regression still passes", Phase70Regression, ref passed, ref failed);
        Console.WriteLine($"[PHASE 71 LAWFUL CONTRABAND STOP AND COMPLIANCE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 71 LAWFUL CONTRABAND STOP AND COMPLIANCE SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 71 LAWFUL CONTRABAND STOP AND COMPLIANCE SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 71 LAWFUL CONTRABAND STOP AND COMPLIANCE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool, string) DetectionHoldsFire()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(player);
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail($"detection did not create interception: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
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
            : Fail($"contraband-only interception fired: fired={fired}, damaged={damaged}");
    }

    private static (bool, string) ClosesViaMovement()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(2_500f, 0f, 0f), "Phase 71 Closer");
        context.StepTraffic(player);
        context.StepCoordinator(player);
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("interception did not start");
        float before = Vector3.Distance(police.Position, player.Position);
        for (int i = 0; i < 10; i++)
            context.StepNpc(player, 0.5f);
        float after = Vector3.Distance(police.Position, player.Position);
        return after < before
            ? Pass()
            : Fail($"officer did not close via movement: before={before}, after={after}");
    }

    private static (bool, string) ScanOnlyWithinRange()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(5_000f, 0f, 0f));
        context.StepFull(player);
        if (!police.HasPlayerTarget)
            return Fail("outside-scan-range officer did not intercept at detection range");
        context.StepScanFrames(player, 0.5f, 4);
        if (context.Scan.State == PoliceScanState.Scanning || context.Scan.IsEnforcementDemandActive)
            return Fail($"scan started outside valid range: state={context.Scan.State}");
        police.Position = new Vector3(1_500f, 0f, 0f);
        context.StepScanFrames(player, 0.5f, 8);
        return context.Scan.State == PoliceScanState.ContrabandDetected && context.Scan.IsEnforcementDemandActive
            ? Pass()
            : Fail($"scan did not start within range: state={context.Scan.State}");
    }

    private static (bool, string) LegalProducesNoDemand()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveLegal(player, 3))
            return Fail("could not seed legal cargo");
        NpcShip police = context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (context.Scan.IsEnforcementDemandActive)
            return Fail("legal cargo produced a contraband demand");
        if (police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("legal cargo triggered contraband interception");
        return context.Scan.State == PoliceScanState.Cleared || context.Scan.State == PoliceScanState.Idle
            ? Pass()
            : Fail($"unexpected scan state for legal hold: {context.Scan.State}");
    }

    private static (bool, string) ContrabandProducesDemand()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive || context.Scan.State != PoliceScanState.ContrabandDetected)
            return Fail($"contraband did not produce demand: state={context.Scan.State}");
        if (context.Scan.CurrentOffer == null || !context.Scan.CurrentOffer.HasViolation)
            return Fail("demand offer has no violation");
        return context.Scan.CurrentOffer.TotalViolationQuantity == 2
            ? Pass()
            : Fail($"demand quantity mismatch: {context.Scan.CurrentOffer.TotalViolationQuantity}");
    }

    private static (bool, string) ComplianceRemovesOnlyContraband()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        Commodity legal = CommodityCatalog.GetById("food-rations") ?? CommodityCatalog.GetById("water");
        if (contraband == null || legal == null)
            return Fail("catalog changed");
        if (!player.CargoHold.AddCommodity(contraband, 2) || !player.CargoHold.AddCommodity(legal, 3))
            return Fail("could not seed mixed cargo");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        int fine = context.Scan.CurrentOffer.FineAmount;
        int creditsBefore = context.Credits.Credits;
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation))
            return Fail("compliance did not resolve");
        return player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
            context.Credits.Credits == creditsBefore - fine &&
            context.Scan.State == PoliceScanState.Cleared
            ? Pass()
            : Fail($"compliance removed wrong quantities: contraband={player.CargoHold.GetCommodityQuantity(contraband.Name)}, credits={context.Credits.Credits}");
    }

    private static (bool, string) LegalSurvivesConfiscation()
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

    private static (bool, string) ComplianceClearsInterception()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation))
            return Fail("compliance did not resolve");
        context.StepCoordinator(player);
        context.StepTraffic(player);
        context.StepNpc(player);
        return !police.HasPlayerTarget &&
            police.PlayerTargetReason == NpcPlayerTargetReason.None &&
            context.Traffic.ActivePlayerContrabandEnforcerCount == 0
            ? Pass()
            : Fail($"compliance retained interception: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
    }

    private static (bool, string) ComplianceNoFugitive()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation))
            return Fail("compliance did not resolve");
        context.StepCoordinator(player);
        return !context.Fugitive.IsActive &&
            !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice)
            ? Pass()
            : Fail($"compliance created pursuit: fugitive={context.Fugitive.IsActive}");
    }

    private static (bool, string) RefusalTimeoutEvasionEscalates()
    {
        Context refuseContext = new();
        Ship refusePlayer = refuseContext.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(refusePlayer, 1))
            return Fail("refuse setup failed");
        refuseContext.AddPolice(new Vector3(1_500f, 0f, 0f));
        refuseContext.StepScanFrames(refusePlayer, 0.5f, 8);
        if (!refuseContext.Scan.TryRefuseEnforcement(refusePlayer, refuseContext.Credits, refuseContext.Reputation))
            return Fail("explicit refuse did not resolve");
        refuseContext.StepCoordinator(refusePlayer);
        if (refuseContext.Scan.State != PoliceScanState.Enforcement || !refuseContext.Fugitive.IsActive)
            return Fail($"refuse did not escalate: scan={refuseContext.Scan.State}, fugitive={refuseContext.Fugitive.IsActive}");

        Context timeoutContext = new();
        Ship timeoutPlayer = timeoutContext.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(timeoutPlayer, 1))
            return Fail("timeout setup failed");
        NpcShip timeoutPolice = timeoutContext.AddPolice(new Vector3(1_500f, 0f, 0f));
        timeoutContext.StepScanFrames(timeoutPlayer, 0.5f, 8);
        timeoutContext.Scan.Update(
            new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(PoliceScanSystem.EnforcementDemandSeconds + 0.1f)),
            timeoutPlayer, timeoutContext.Npcs, timeoutContext.Credits, timeoutContext.Reputation);
        timeoutContext.StepCoordinator(timeoutPlayer);
        if (timeoutContext.Scan.State != PoliceScanState.Enforcement || !timeoutContext.Fugitive.IsActive)
            return Fail($"timeout did not escalate: scan={timeoutContext.Scan.State}, fugitive={timeoutContext.Fugitive.IsActive}");

        Context evadeContext = new();
        Ship evadePlayer = evadeContext.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(evadePlayer, 1))
            return Fail("evasion setup failed");
        NpcShip evadePolice = evadeContext.AddPolice(new Vector3(1_500f, 0f, 0f));
        evadeContext.StepScanFrames(evadePlayer, 0.5f, 8);
        evadePlayer.Position = evadePolice.Position + new Vector3(PoliceScanSystem.EnforcementEscapeRange + 100f, 0f, 0f);
        evadeContext.Scan.Update(
            new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1f)),
            evadePlayer, evadeContext.Npcs, evadeContext.Credits, evadeContext.Reputation);
        evadeContext.StepCoordinator(evadePlayer);
        if (evadeContext.Scan.State != PoliceScanState.Enforcement || !evadeContext.Fugitive.IsActive)
            return Fail($"evasion did not escalate: scan={evadeContext.Scan.State}, fugitive={evadeContext.Fugitive.IsActive}");

        bool converted = timeoutPolice.PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit ||
            evadePolice.PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit;
        return converted
            ? Pass()
            : Fail("escalation did not convert interception to fugitive pursuit");
    }

    private (bool, string) FugitiveEnablesCombat()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.TryRefuseEnforcement(player, context.Credits, context.Reputation))
            return Fail("refusal did not escalate");
        context.StepCoordinator(player);
        context.StepTraffic(player);
        if (police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit)
            return Fail($"escalation did not yield fugitive reason: {police.PlayerTargetReason}");
        if (!police.HasValidPlayerTarget(context.Reputation))
            return Fail("fugitive target is not valid for combat");
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
        return fired && damaged
            ? Pass()
            : Fail($"fugitive did not enable combat: fired={fired}, damaged={damaged}");
    }

    private static (bool, string) PlayerAttackCausesHostility()
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
            : Fail($"attack did not cause valid hostility: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
    }

    private (bool, string) AlreadyHostileNotSuppressed()
    {
        Context context = new();
        context.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice, "pre-existing");
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepFull(player);
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
            : Fail("already-hostile police was pacified by hold-fire");
    }

    private static (bool, string) JettisonNoDoubleConfiscation()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (contraband == null || !player.CargoHold.AddCommodity(contraband, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        int jettisoned = context.Loot.TryJettisonContraband(
            player.CargoHold, player.Position, player.Velocity, out int podCount);
        if (jettisoned != 2 || podCount <= 0)
            return Fail($"jettison did not move exact cargo: removed={jettisoned}, pods={podCount}");
        if (player.CargoHold.GetCommodityQuantity(contraband.Name) != 0)
            return Fail("hold not empty after jettison");
        int podsBefore = context.Loot.ActivePods.Count;
        if (!context.Scan.TryAcceptEnforcement(player, context.Credits, context.Reputation))
            return Fail("post-jettison compliance did not resolve");
        if (context.Loot.ActivePods.Count != podsBefore)
            return Fail("compliance deleted physical pods");
        return player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
            context.Scan.State == PoliceScanState.Cleared
            ? Pass()
            : Fail("jettisoned cargo was confiscated twice or state invalid");
    }

    private static (bool, string) CleanBeforeCompletionResolves()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (contraband == null || !player.CargoHold.AddCommodity(contraband, 2))
            return Fail("could not seed contraband");
        NpcShip police = context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepTraffic(player);
        context.StepCoordinator(player);
        context.StepScan(player, 0.5f);
        if (context.Scan.State != PoliceScanState.Scanning)
            return Fail($"scan did not start: {context.Scan.State}");
        if (!player.CargoHold.TryConfiscateContraband(
                new Dictionary<string, int> { [contraband.Id] = 2 }, out _))
            return Fail("authoritative removal failed");
        context.StepScanFrames(player, 0.5f, 8);
        context.StepCoordinator(player);
        return context.Scan.State == PoliceScanState.Cleared &&
            !context.Scan.IsEnforcementDemandActive &&
            !context.Fugitive.IsActive
            ? Pass()
            : Fail($"clean hold did not resolve safely: scan={context.Scan.State}, fugitive={context.Fugitive.IsActive}");
    }

    private static (bool, string) SingleScanAuthority()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip alpha = context.AddPolice(new Vector3(1_400f, 0f, 0f), "Police Alpha");
        NpcShip beta = context.AddPolice(new Vector3(1_600f, 0f, 0f), "Police Beta");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("no demand created");
        if (context.Scan.DetectionCount != 1)
            return Fail($"multiple demands created: {context.Scan.DetectionCount}");
        if (context.Scan.ActiveScanner == null)
            return Fail("no scan owner selected");
        context.StepCoordinator(player);
        if (context.Coordinator.ActiveOwner == null)
            return Fail("coordinator has no encounter owner");
        bool bothIntercept = context.Traffic.ActivePlayerContrabandEnforcerCount >= 1;
        return bothIntercept && context.Scan.DetectionCount == 1
            ? Pass()
            : Fail($"authority unclear: enforcers={context.Traffic.ActivePlayerContrabandEnforcerCount}, detections={context.Scan.DetectionCount}");
    }

    private static (bool, string) NoDuplicateOnRepeatedTicks()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        context.AddPolice(new Vector3(1_500f, 0f, 0f));
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        var offer = context.Scan.CurrentOffer;
        int detections = context.Scan.DetectionCount;
        int creditsBefore = context.Credits.Credits;
        int cargoBefore = player.CargoHold.GetCommodityQuantity("Side Arms");
        for (int i = 0; i < 5; i++)
            context.StepFull(player, 0.1f);
        return context.Scan.DetectionCount == detections &&
            ReferenceEquals(offer, context.Scan.CurrentOffer) &&
            context.Credits.Credits == creditsBefore &&
            player.CargoHold.GetCommodityQuantity("Side Arms") == cargoBefore
            ? Pass()
            : Fail("repeated ticks duplicated demand or mutated cargo");
    }

    private static (bool, string) OwnerDestructionResolves()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        NpcShip owner = context.AddPolice(new Vector3(1_500f, 0f, 0f), "Stop Owner");
        NpcShip supporter = context.AddPolice(new Vector3(1_700f, 0f, 0f), "Stop Supporter");
        context.StepScanFrames(player, 0.5f, 8);
        if (!context.Scan.IsEnforcementDemandActive)
            return Fail("setup did not create demand");
        NpcShip active = context.Scan.ActiveScanner;
        if (active == null)
            return Fail("no active scanner to destroy");
        active.ApplyCombatDamage(
            active.Shields.CurrentShields + active.Hull.MaxHull + 10f,
            NpcDestructionSource.Npc);
        if (!active.IsDestroyed)
            return Fail("owner could not be destroyed");
        context.Traffic.NotifyNpcDestroyed(active);
        context.Coordinator.NotifyNpcDestroyed(active);
        context.StepFull(player, 0.1f);
        bool ownerGone = !context.Traffic.ActivePlayerContrabandEnforcers.Contains(active);
        bool safeState = context.Scan.State == PoliceScanState.Idle ||
            context.Scan.State == PoliceScanState.Cleared ||
            context.Scan.State == PoliceScanState.ContrabandDetected ||
            context.Scan.State == PoliceScanState.Enforcement;
        return ownerGone && safeState
            ? Pass()
            : Fail($"owner destruction unsafe: contained={context.Traffic.ActivePlayerContrabandEnforcers.Contains(active)}, scan={context.Scan.State}");
    }

    private static (bool, string) LifetimeClearsState()
    {
        Context despawnContext = new();
        Ship despawnPlayer = despawnContext.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(despawnPlayer, 2))
            return Fail("despawn setup failed");
        NpcShip despawnPolice = despawnContext.AddPolice(new Vector3(300f, 0f, 0f));
        despawnContext.StepFull(despawnPlayer);
        if (despawnContext.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("despawn setup did not detect");
        despawnContext.Npcs.Remove(despawnPolice);
        despawnContext.Objects.Remove(despawnPolice);
        despawnContext.Coordinator.NotifyNpcDespawned(despawnPolice);
        despawnContext.StepFull(despawnPlayer);
        if (despawnContext.Traffic.ActivePlayerContrabandEnforcerCount != 0)
            return Fail("despawn retained enforcement");

        Context resetContext = new();
        Ship resetPlayer = resetContext.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (!resetPlayer.CargoHold.AddCommodity(contraband, 2))
            return Fail("reset setup failed");
        int cargoBefore = resetPlayer.CargoHold.GetCommodityQuantity(contraband.Name);
        NpcShip resetPolice = resetContext.AddPolice(new Vector3(300f, 0f, 0f));
        resetContext.StepFull(resetPlayer);
        resetContext.Traffic.ResetContrabandEnforcement();
        resetContext.Scan.Reset();
        resetContext.Fugitive.Reset();
        resetContext.Coordinator.Reset();
        if (resetContext.Traffic.ActivePlayerContrabandEnforcerCount != 0 ||
            resetPolice.HasPlayerTarget ||
            resetPlayer.CargoHold.GetCommodityQuantity(contraband.Name) != cargoBefore)
            return Fail("reset mutated cargo or retained pursuit");

        Context deathContext = new();
        Ship deathPlayer = deathContext.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(deathPlayer, 2))
            return Fail("death setup failed");
        deathContext.AddPolice(new Vector3(300f, 0f, 0f));
        deathContext.StepFull(deathPlayer);
        deathPlayer.ApplyCombatDamage(1_000_000f, hostile: true);
        if (deathPlayer.Hull?.IsDestroyed != true)
            return Fail("player could not be destroyed");
        deathContext.StepFull(deathPlayer);
        if (deathContext.Traffic.ActivePlayerContrabandEnforcerCount != 0)
            return Fail("player destruction retained enforcement");
        return Pass();
    }

    private static (bool, string) Phase68Regression()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        if (shipment?.Carrier == null)
            return Fail("shipment did not spawn");
        if (!shipment.Carrier.IsRogueSmuggler)
            return Fail("carrier lost Rogue provenance");
        if (shipment.RemainingQuantity <= 0)
            return Fail("manifest empty");
        bool allContraband = shipment.Manifest.Stacks
            .Where(stack => stack != null && stack.Quantity > 0)
            .All(stack => stack.Commodity?.IsContraband == true);
        return allContraband && shipment.InitialQuantity == shipment.RemainingQuantity
            ? Pass()
            : Fail("manifest not exact canonical contraband");
    }

    private static (bool, string) Phase69Regression()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        if (shipment?.Carrier == null)
            return Fail("Phase 68 shipment did not spawn for Phase 69 proof");
        NpcShip police = context.AddPolice(shipment.Carrier.Position);
        Ship farPlayer = context.CreatePlayer(new Vector3(1_000_000f, 0f, 0f));
        context.StepTraffic(farPlayer);
        return police.FactionCombatTarget == shipment.Carrier &&
            police.FactionCombatTargetOrigin == FactionCombatTargetOrigin.ContrabandEnforcement
            ? Pass()
            : Fail($"target={police.FactionCombatTarget?.Name}, origin={police.FactionCombatTargetOrigin}");
    }

    private static (bool, string) Phase70Regression()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");
        if (!ContrabandEnforcementPolicy.HasPlayerContraband(player.CargoHold))
            return Fail("canonical contraband not recognized");
        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1 ||
            !police.HasPlayerTarget ||
            police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("Phase 70 detection did not trigger");
        Ship legalPlayer = context.CreatePlayer(new Vector3(500_000f, 0f, 0f));
        if (!Context.GiveLegal(legalPlayer, 3))
            return Fail("could not seed legal cargo");
        NpcShip legalPolice = context.AddPolice(new Vector3(500_300f, 0f, 0f), "Phase 70 Legal Check");
        context.StepTraffic(legalPlayer);
        if (legalPolice.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("legal cargo triggered Phase 70 enforcement");
        return Pass();
    }

    private static GameTime Frame(double seconds) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);
}
