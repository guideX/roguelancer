using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Phase 70 focused coverage. The harness reuses the production Phase 68
/// shipment manager, TrafficManager faction/contraband pass, NpcWeaponSystem,
/// LootManager pod authority, and canonical CargoHold/CommodityCatalog without
/// introducing a smoke-only cargo or police implementation.
/// </summary>
internal sealed class Phase70PlayerContrabandInterdictionSmokeTest
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
                Id = "phase70-buffalo-rochester",
                Name = "Phase 70 criminal route",
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
            Loot.ConfigureEconomicCargoCallbacks(
                carrier => Smuggling.GetDestructionSalvage(carrier),
                (carrier, commodityId, quantity) => Smuggling.ConsumeDestructionSalvage(carrier, commodityId, quantity));
        }

        public RogueSmugglingShipment SpawnShipment()
        {
            Smuggling.Update(RogueSmugglingManager.SpawnIntervalSeconds + 1f, _ => { });
            return Smuggling.ActiveShipments.SingleOrDefault();
        }

        public NpcShip AddPolice(Vector3 position, string name = "Phase 70 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase70-police",
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

        public NpcShip AddNonEnforcer(Vector3 position, string factionId, string name = "Non-Enforcer")
        {
            NpcShip ship = new(name, position, position, 1f, 0f, factionId);
            ship.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase70-nonenforcer",
                position,
                800f,
                180f,
                ContrabandEnforcementPolicy.DefaultDetectionRange);
            Npcs.Add(ship);
            Objects.Add(ship);
            return ship;
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

        public void SetManifest(NpcShip carrier, NpcCargoManifestSnapshot manifest)
        {
            _manualManifests[carrier] = manifest;
        }

        public void StepTraffic(Ship playerShip, float seconds = 0.1f)
        {
            Traffic.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                playerShip,
                Reputation,
                _ => { });
        }

        public void StepNpcRetention(Ship playerShip, float seconds = 0.1f)
        {
            GameTime frame = new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
            foreach (NpcShip npc in Npcs.ToList())
                npc?.Update(frame, null, playerShip, Reputation);
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

    public Phase70PlayerContrabandInterdictionSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("legal cargo does not trigger enforcement", LegalCargoDoesNotTrigger, ref passed, ref failed);
        RunCase("player contraband is detected by lawful enforcement", PlayerContrabandIsDetected, ref passed, ref failed);
        RunCase("player outside detection boundary is not detected", OutsideBoundaryIsNotDetected, ref passed, ref failed);
        RunCase("non-enforcement factions do not interdict", NonEnforcementFactionDoesNotInterdict, ref passed, ref failed);
        RunCase("enforcement creates player target pursuit state", EnforcementCreatesPlayerTarget, ref passed, ref failed);
        RunCase("normal combat systems fire and damage", NormalCombatFiresAndDamages, ref passed, ref failed);
        RunCase("mixed legal plus contraband detects correctly", MixedCargoDetects, ref passed, ref failed);
        RunCase("removal of contraband clears enforcement", RemovalClearsEnforcement, ref passed, ref failed);
        RunCase("reset clears transient state safely", ResetClearsTransientState, ref passed, ref failed);
        RunCase("enforcer destroyed clears transient state", EnforcerDestroyedClears, ref passed, ref failed);
        RunCase("player destroyed clears transient state", PlayerDestroyedClears, ref passed, ref failed);
        RunCase("despawn clears transient state", DespawnClearsTransientState, ref passed, ref failed);
        RunCase("Phase 69 NPC interdiction still works", Phase69NpcStillWorks, ref passed, ref failed);
        RunCase("Phase 68 smuggling still works", Phase68SmugglingStillWorks, ref passed, ref failed);
        RunCase("repeated checks do not duplicate enforcement", NoDuplicateEnforcement, ref passed, ref failed);
        Console.WriteLine($"[PHASE 70 PLAYER CONTRABAND INTERDICTION SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 70 PLAYER CONTRABAND INTERDICTION SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 70 PLAYER CONTRABAND INTERDICTION SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 70 PLAYER CONTRABAND INTERDICTION SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) LegalCargoDoesNotTrigger()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveLegal(player, 3))
            return Fail("could not seed legal cargo");
        // Legal stolen goods alone must not count as contraband either.
        Commodity water = CommodityCatalog.GetById("water");
        Ship stolenCheck = context.CreatePlayer(new Vector3(0f, 0f, 0f));
        stolenCheck.CargoHold.AddStolenCommodity(water, 1);
        if (ContrabandEnforcementPolicy.HasPlayerContraband(stolenCheck.CargoHold))
            return Fail("legal stolen cargo was classified as contraband");

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        return police.HasPlayerTarget == false &&
            police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement &&
            context.Traffic.ActivePlayerContrabandEnforcerCount == 0
            ? Pass()
            : Fail($"legal hold triggered enforcement: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
    }

    private static (bool, string) PlayerContrabandIsDetected()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed canonical contraband");
        if (!ContrabandEnforcementPolicy.HasPlayerContraband(player.CargoHold))
            return Fail("canonical contraband not recognized in player hold");

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 1 &&
            context.Traffic.ActivePlayerContrabandEnforcers.Contains(police) &&
            police.HasPlayerTarget &&
            police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement
            ? Pass()
            : Fail($"count={context.Traffic.ActivePlayerContrabandEnforcerCount}, target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");
    }

    private static (bool, string) OutsideBoundaryIsNotDetected()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");

        float range = ContrabandEnforcementPolicy.DefaultDetectionRange;
        NpcShip police = context.AddPolice(new Vector3(range + 1_500f, 0f, 0f));
        context.StepTraffic(player);
        if (police.HasPlayerTarget || context.Traffic.ActivePlayerContrabandEnforcerCount != 0)
            return Fail("outside-range police acquired player contraband");

        // Moving inside the same activation range must detect deterministically.
        police.Position = new Vector3(400f, 0f, 0f);
        context.StepTraffic(player);
        return police.HasPlayerTarget &&
            police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement
            ? Pass()
            : Fail("inside-range police did not detect after outside negative");
    }

    private static (bool, string) NonEnforcementFactionDoesNotInterdict()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");

        NpcShip rogue = context.AddNonEnforcer(new Vector3(300f, 0f, 0f), FactionManager.LibertyRogues, "Rogue Bystander");
        NpcShip neutral = context.AddNonEnforcer(new Vector3(-300f, 0f, 0f), FactionManager.NeutralCivilians, "Neutral Bystander");
        context.StepTraffic(player);
        return rogue.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement &&
            neutral.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement &&
            context.Traffic.ActivePlayerContrabandEnforcerCount == 0
            ? Pass()
            : Fail($"rogue={rogue.PlayerTargetReason}, neutral={neutral.PlayerTargetReason}, count={context.Traffic.ActivePlayerContrabandEnforcerCount}");
    }

    private static (bool, string) EnforcementCreatesPlayerTarget()
    {
        Context context = new();
        Ship player = context.CreatePlayer(new Vector3(1_000f, 0f, 0f));
        if (!Context.GiveContraband(player, 1))
            return Fail("could not seed contraband");

        NpcShip police = context.AddPolice(new Vector3(1_200f, 0f, 0f), "Phase 70 Pursuer");
        context.StepTraffic(player);
        bool valid = police.HasValidPlayerTarget(context.Reputation);
        return police.HasPlayerTarget &&
            police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement &&
            police.EncounterState == TrafficEncounterState.AttackingPlayer &&
            police.PlayerTargetReason != NpcPlayerTargetReason.FactionDisposition &&
            police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit &&
            valid &&
            context.Traffic.ActivePlayerContrabandEnforcers.Contains(police)
            ? Pass()
            : Fail($"target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}, state={police.EncounterState}, valid={valid}");
    }

    private (bool, string) NormalCombatFiresAndDamages()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail("enforcement did not establish player target before weapon check");

        bool fired = false;
        NpcWeaponSystem weapons = new(_graphicsDevice, context.Reputation);
        weapons.NpcWeaponFired += (source, target, weaponId, weapon) =>
        {
            if (source == police && target == null)
                fired = true;
        };
        float shieldsBefore = player.Shields.CurrentShields;
        float hullBefore = player.Hull.CurrentHull;
        // Widen the player collision envelope exactly like the Phase 69 NPC
        // proof widens its carrier radius, so the existing projectile path
        // is exercised without depending on RNG spread at small radii.
        player.CollisionRadius = 1_000f;
        police.Position = player.Position + new Vector3(0f, 0f, -300f);
        player.Position = Vector3.Zero;
        police.Velocity = Vector3.Zero;
        for (int frame = 0; frame < 8; frame++)
            weapons.Update(Frame(0.1f), context.Npcs, player);

        bool damaged = player.Shields.CurrentShields < shieldsBefore || player.Hull.CurrentHull < hullBefore;
        return fired && damaged
            ? Pass()
            : Fail($"fired={fired}, shields={shieldsBefore}->{player.Shields.CurrentShields}, hull={hullBefore}->{player.Hull.CurrentHull}");
    }

    private static (bool, string) MixedCargoDetects()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveLegal(player, 2) || !Context.GiveContraband(player, 1))
            return Fail("could not seed mixed cargo");

        Commodity legal = CommodityCatalog.GetById("water");
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (legal?.IsContraband == true || contraband?.IsContraband != true)
            return Fail("catalog legality changed");

        NpcShip police = context.AddPolice(new Vector3(250f, 0f, 0f));
        context.StepTraffic(player);
        return police.HasPlayerTarget &&
            police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement &&
            player.CargoHold.GetCommodityQuantity(legal.Name) == 2 &&
            player.CargoHold.GetCommodityQuantity(contraband.Name) == 1
            ? Pass()
            : Fail($"mixed hold not detected without mutating legal cargo");
    }

    private static (bool, string) RemovalClearsEnforcement()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (contraband == null || !player.CargoHold.AddCommodity(contraband, 2))
            return Fail("could not seed contraband");

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        if (!police.HasPlayerTarget)
            return Fail("setup did not detect");

        // Authoritative removal through the existing confiscation seam.
        if (!player.CargoHold.TryConfiscateContraband(
                new Dictionary<string, int> { [contraband.Id] = 2 },
                out _))
            return Fail("authoritative confiscation seam rejected exact contraband removal");

        if (ContrabandEnforcementPolicy.HasPlayerContraband(player.CargoHold))
            return Fail("hold still reports contraband after exact removal");

        context.StepTraffic(player);
        context.StepNpcRetention(player);
        return !police.HasPlayerTarget &&
            police.PlayerTargetReason == NpcPlayerTargetReason.None &&
            context.Traffic.ActivePlayerContrabandEnforcerCount == 0
            ? Pass()
            : Fail($"clean hold retained pursuit: target={police.HasPlayerTarget}, reason={police.PlayerTargetReason}");

    }

    private static (bool, string) ResetClearsTransientState()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        if (!player.CargoHold.AddCommodity(contraband, 2))
            return Fail("could not seed contraband");
        int cargoBefore = player.CargoHold.GetCommodityQuantity(contraband.Name);

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("setup did not detect");

        context.Traffic.ResetContrabandEnforcement();
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 0 &&
            !police.HasPlayerTarget &&
            player.CargoHold.GetCommodityQuantity(contraband.Name) == cargoBefore
            ? Pass()
            : Fail("reset mutated cargo or retained pursuit");
    }

    private static (bool, string) EnforcerDestroyedClears()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("setup did not detect");

        police.ApplyCombatDamage(police.Shields.CurrentShields + police.Hull.MaxHull + 10f, NpcDestructionSource.Npc);
        if (!police.IsDestroyed)
            return Fail("enforcer could not be destroyed");
        context.Traffic.NotifyNpcDestroyed(police);
        context.StepTraffic(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 0 &&
            !context.Traffic.ActivePlayerContrabandEnforcers.Contains(police)
            ? Pass()
            : Fail("destroyed enforcer retained transient enforcement");
    }

    private static (bool, string) PlayerDestroyedClears()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");

        context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("setup did not detect");

        player.ApplyCombatDamage(1_000_000f, hostile: true);
        if (player.Hull?.IsDestroyed != true)
            return Fail("player could not be destroyed");
        context.StepTraffic(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 0
            ? Pass()
            : Fail("player destruction retained transient enforcement");
    }

    private static (bool, string) DespawnClearsTransientState()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        if (context.Traffic.ActivePlayerContrabandEnforcerCount != 1)
            return Fail("setup did not detect");

        context.Npcs.Remove(police);
        context.Objects.Remove(police);
        context.StepTraffic(player);
        return context.Traffic.ActivePlayerContrabandEnforcerCount == 0
            ? Pass()
            : Fail("despawned enforcer retained transient enforcement");
    }

    private static (bool, string) Phase69NpcStillWorks()
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

    private static (bool, string) Phase68SmugglingStillWorks()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        if (shipment?.Carrier == null)
            return Fail("shipment did not spawn");
        if (!shipment.Carrier.IsRogueSmuggler)
            return Fail("carrier lost Rogue smuggler provenance");
        if (shipment.RemainingQuantity <= 0)
            return Fail("manifest is empty");

        bool allContraband = shipment.Manifest.Stacks
            .Where(stack => stack != null && stack.Quantity > 0)
            .All(stack => stack.Commodity?.IsContraband == true);
        return allContraband && shipment.InitialQuantity == shipment.RemainingQuantity
            ? Pass()
            : Fail("manifest is not exact canonical contraband");
    }

    private static (bool, string) NoDuplicateEnforcement()
    {
        Context context = new();
        Ship player = context.CreatePlayer(Vector3.Zero);
        if (!Context.GiveContraband(player, 2))
            return Fail("could not seed contraband");

        NpcShip police = context.AddPolice(new Vector3(300f, 0f, 0f));
        context.StepTraffic(player);
        int first = context.Traffic.ActivePlayerContrabandEnforcerCount;
        context.StepTraffic(player);
        context.StepTraffic(player);
        context.StepNpcRetention(player);
        return first == 1 &&
            context.Traffic.ActivePlayerContrabandEnforcerCount == 1 &&
            police.HasPlayerTarget &&
            police.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement
            ? Pass()
            : Fail($"repeated checks duplicated or lost enforcement: first={first}, now={context.Traffic.ActivePlayerContrabandEnforcerCount}");
    }

    private static GameTime Frame(double seconds) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);
}
