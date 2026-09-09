using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Phase 69 focused coverage. The context uses the production Phase 68
/// manager, TrafficManager faction pass, NpcWeaponSystem, LootManager pod
/// authority, and versioned save DTOs without introducing a smoke-only cargo
/// or seizure implementation.
/// </summary>
internal sealed class Phase69LawfulContrabandInterdictionSmokeTest
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
                Id = "phase69-buffalo-rochester",
                Name = "Phase 69 criminal route",
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

        public NpcShip AddPolice(Vector3 position, string name = "Phase 69 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase69-police",
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

        public NpcShip AddNeutralCarrier(Vector3 position, string name = "Neutral Carrier")
        {
            NpcShip carrier = new(name, position, position, 1f, 0f, FactionManager.NeutralCivilians);
            carrier.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                "phase69-neutral",
                position,
                800f,
                160f,
                ContrabandEnforcementPolicy.DefaultDetectionRange,
                position,
                position + new Vector3(2_000f, 0f, 0f));
            Npcs.Add(carrier);
            Objects.Add(carrier);
            return carrier;
        }

        public void SetManifest(NpcShip carrier, NpcCargoManifestSnapshot manifest)
        {
            _manualManifests[carrier] = manifest;
        }

        public void StepTraffic(float seconds = 0.1f)
        {
            Traffic.Update(
                new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)),
                new Ship(new Vector3(1_000_000f, 0f, 0f)),
                Reputation,
                _ => { });
        }

        public int DestroyCarrier(RogueSmugglingShipment shipment)
        {
            if (shipment?.Carrier == null)
                return 0;

            NpcShip carrier = shipment.Carrier;
            int initialQuantity = shipment.RemainingQuantity;
            carrier.ApplyCombatDamage(
                carrier.Shields.CurrentShields + carrier.Hull.MaxHull + 10f,
                NpcDestructionSource.Npc);
            if (!carrier.IsDestroyed)
                return 0;

            int spawned = Loot.SpawnSalvageForDestroyedNpc(carrier);
            Smuggling.FinalizeDestroyedCarrier(carrier);
            return spawned > 0 && initialQuantity > 0 ? spawned : spawned;
        }

        public int PickUp(CargoPod pod, Ship player)
        {
            if (pod == null || player == null)
                return 0;

            player.Position = pod.Position;
            int before = player.CargoHold.GetCommodityQuantity(pod.GetCommodity()?.Name ?? string.Empty);
            Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1f)), player, false);
            return player.CargoHold.GetCommodityQuantity(pod.GetCommodity()?.Name ?? string.Empty) - before;
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

    public Phase69LawfulContrabandInterdictionSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("canonical legality drives detection", CanonicalLegalityDrivesDetection, ref passed, ref failed);
        RunCase("nearby Phase 68 carrier is detected", NearbyPhase68CarrierIsDetected, ref passed, ref failed);
        RunCase("Rogue faction alone is not contraband evidence", RogueFactionAloneIsNotEvidence, ref passed, ref failed);
        RunCase("legal cargo is a negative control", LegalCargoIsNotDetected, ref passed, ref failed);
        RunCase("non-Rogue contraband is representable", NonRogueContrabandIsRepresentable, ref passed, ref failed);
        RunCase("NPC weapon damage uses the existing combat path", NpcWeaponDamageUsesExistingPath, ref passed, ref failed);
        RunCase("destruction marks the Phase 68 shipment lost", DestructionMarksShipmentLost, ref passed, ref failed);
        RunCase("interception does not settle destination stock", InterceptionDoesNotSettleDestination, ref passed, ref failed);
        RunCase("exact carrier cargo becomes physical pods", ExactCargoBecomesPhysicalPods, ref passed, ref failed);
        RunCase("police approaches before seizure", PoliceApproachesBeforeSeizure, ref passed, ref failed);
        RunCase("physical seizure removes a pod once", PhysicalSeizureRemovesPodOnce, ref passed, ref failed);
        RunCase("player pickup wins the physical race", PlayerPickupWinsRace, ref passed, ref failed);
        RunCase("fully seized cargo is conserved", FullySeizedCargoIsConserved, ref passed, ref failed);
        RunCase("mixed recovery and seizure is conserved", MixedRecoveryAndSeizureIsConserved, ref passed, ref failed);
        RunCase("save/load cannot resurrect seized cargo", SaveLoadCannotResurrectSeizedCargo, ref passed, ref failed);
        RunCase("reset clears enforcement without market mutation", ResetClearsEnforcement, ref passed, ref failed);
        Console.WriteLine($"[PHASE 69 LAWFUL CONTRABAND INTERDICTION SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 69 LAWFUL CONTRABAND INTERDICTION SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 69 LAWFUL CONTRABAND INTERDICTION SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 69 LAWFUL CONTRABAND INTERDICTION SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) CanonicalLegalityDrivesDetection()
    {
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        Commodity legal = CommodityCatalog.GetById("water");
        NpcCargoManifestSnapshot illegal = new(true, new[] { new NpcCargoManifestStackSnapshot(contraband, 1, true) });
        NpcCargoManifestSnapshot ordinary = new(true, new[] { new NpcCargoManifestStackSnapshot(legal, 1) });
        return contraband?.IsContraband == true && legal?.IsContraband != true &&
            ContrabandEnforcementPolicy.HasActualContraband(illegal) &&
            !ContrabandEnforcementPolicy.HasActualContraband(ordinary)
            ? Pass()
            : Fail("manifest legality did not come from Commodity.IsContraband");
    }

    private static (bool, string) NearbyPhase68CarrierIsDetected()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        NpcShip police = context.AddPolice(shipment?.Carrier?.Position ?? Vector3.Zero);
        context.StepTraffic();
        return shipment != null && police.FactionCombatTarget == shipment.Carrier &&
            police.FactionCombatTargetOrigin == FactionCombatTargetOrigin.ContrabandEnforcement
            ? Pass()
            : Fail($"target={police.FactionCombatTarget?.Name}, origin={police.FactionCombatTargetOrigin}");
    }

    private static (bool, string) RogueFactionAloneIsNotEvidence()
    {
        Context context = new();
        NpcShip police = context.AddPolice(Vector3.Zero);
        NpcShip rogue = new("Rogue Empty", new Vector3(300f, 0f, 0f), Vector3.Zero, 1f, 0f, FactionManager.LibertyRogues);
        context.Npcs.Add(rogue);
        context.Objects.Add(rogue);
        context.StepTraffic();
        return police.FactionCombatTargetOrigin != FactionCombatTargetOrigin.ContrabandEnforcement
            ? Pass()
            : Fail("Rogue faction was treated as a cargo legality shortcut");
    }

    private static (bool, string) LegalCargoIsNotDetected()
    {
        Context context = new();
        NpcShip police = context.AddPolice(Vector3.Zero);
        NpcShip carrier = context.AddNeutralCarrier(new Vector3(300f, 0f, 0f));
        context.SetManifest(carrier, new NpcCargoManifestSnapshot(true, new[]
        {
            new NpcCargoManifestStackSnapshot(CommodityCatalog.GetById("water"), 8)
        }));
        context.StepTraffic();
        return police.FactionCombatTarget == null
            ? Pass()
            : Fail("legal cargo carrier became a contraband target");
    }

    private static (bool, string) NonRogueContrabandIsRepresentable()
    {
        Context context = new();
        NpcShip police = context.AddPolice(Vector3.Zero);
        NpcShip carrier = context.AddNeutralCarrier(new Vector3(300f, 0f, 0f));
        context.SetManifest(carrier, new NpcCargoManifestSnapshot(true, new[]
        {
            new NpcCargoManifestStackSnapshot(CommodityCatalog.GetById("side-arms"), 2)
        }));
        context.StepTraffic();
        return police.FactionCombatTarget == carrier &&
            police.FactionCombatTargetOrigin == FactionCombatTargetOrigin.ContrabandEnforcement
            ? Pass()
            : Fail("non-Rogue contraband was not accepted by the policy");
    }

    private (bool, string) NpcWeaponDamageUsesExistingPath()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        if (shipment == null)
            return Fail("shipment did not spawn");

        NpcShip police = context.AddPolice(shipment.Carrier.Position);
        bool fired = false;
        NpcWeaponSystem weapons = new(_graphicsDevice, context.Reputation);
        weapons.NpcWeaponFired += (source, target, weaponId, weapon) =>
        {
            if (source == police && target == shipment.Carrier)
                fired = true;
        };
        context.StepTraffic();
        shipment.Carrier.Radius = 1_000f;
        shipment.Carrier.Velocity = Vector3.Zero;
        shipment.Carrier.Position = police.Position + new Vector3(0f, 0f, -300f);
        for (int frame = 0; frame < 6; frame++)
            weapons.Update(Frame(0.1f), context.Npcs, new Ship(new Vector3(1_000_000f, 0f, 0f)));
        return fired && shipment.Carrier.Shields.CurrentShields < shipment.Carrier.Shields.MaxShields
            ? Pass()
            : Fail($"fired={fired}, shields={shipment.Carrier.Shields.CurrentShields}/{shipment.Carrier.Shields.MaxShields}");
    }

    private static (bool, string) DestructionMarksShipmentLost()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        int initial = shipment?.RemainingQuantity ?? 0;
        context.DestroyCarrier(shipment);
        bool lost = context.Smuggling.TryGetSettlementByIdentity(
            shipment?.ShipmentIdentity,
            out RogueSmugglingShipmentSettlement settlement) &&
            settlement == RogueSmugglingShipmentSettlement.Lost;
        return initial > 0 && lost ? Pass() : Fail($"initial={initial}, settlement={settlement}");
    }

    private static (bool, string) InterceptionDoesNotSettleDestination()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        Dictionary<string, int> before = Snapshot(context, context.Rochester, shipment?.Manifest?.Stacks);
        context.DestroyCarrier(shipment);
        Dictionary<string, int> after = Snapshot(context, context.Rochester, shipment?.Manifest?.Stacks);
        return before.Count > 0 && before.All(entry => after.TryGetValue(entry.Key, out int value) && value == entry.Value)
            ? Pass()
            : Fail("destination black-market stock changed after destruction");
    }

    private static (bool, string) ExactCargoBecomesPhysicalPods()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        int initial = shipment?.RemainingQuantity ?? 0;
        context.DestroyCarrier(shipment);
        int podQuantity = context.Loot.ActivePods
            .Where(pod => pod.GetCommodity()?.IsContraband == true)
            .Sum(pod => pod.Quantity);
        return initial > 0 && podQuantity == initial
            ? Pass()
            : Fail($"carrier={initial}, physical={podQuantity}");
    }

    private static (bool, string) PoliceApproachesBeforeSeizure()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        int initial = shipment?.RemainingQuantity ?? 0;
        context.DestroyCarrier(shipment);
        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.GetCommodity()?.IsContraband == true);
        NpcShip police = context.AddPolice(pod.Position + new Vector3(1_000f, 0f, 0f));
        context.StepTraffic();
        return initial > 0 && context.Traffic.ContrabandSeizureCount == 0 &&
            police.EncounterState == TrafficEncounterState.AttackingFactionNpc
            ? Pass()
            : Fail("police seized immediately or did not acquire pod pursuit");
    }

    private static (bool, string) PhysicalSeizureRemovesPodOnce()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        context.DestroyCarrier(shipment);
        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.GetCommodity()?.IsContraband == true);
        int expected = pod?.Quantity ?? 0;
        NpcShip police = context.AddPolice(pod?.Position ?? Vector3.Zero);
        context.StepTraffic();
        police.Position = pod?.Position ?? Vector3.Zero;
        context.StepTraffic();
        int firstCount = context.Traffic.ContrabandSeizureCount;
        int firstQuantity = context.Traffic.TotalSeizedContrabandQuantity;
        context.StepTraffic();
        return expected > 0 && firstCount == 1 && firstQuantity == expected &&
            context.Traffic.ContrabandSeizureCount == firstCount &&
            !context.Loot.ActivePods.Contains(pod)
            ? Pass()
            : Fail($"expected={expected}, records={context.Traffic.ContrabandSeizureCount}, quantity={context.Traffic.TotalSeizedContrabandQuantity}");
    }

    private static (bool, string) PlayerPickupWinsRace()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        context.DestroyCarrier(shipment);
        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.GetCommodity()?.IsContraband == true);
        Ship player = new(pod?.Position ?? Vector3.Zero);
        int picked = context.PickUp(pod, player);
        context.AddPolice(pod?.Position ?? Vector3.Zero);
        context.StepTraffic();
        return picked > 0 && context.Traffic.ContrabandSeizureCount == 0 &&
            !context.Loot.ActivePods.Contains(pod)
            ? Pass()
            : Fail($"picked={picked}, police seizures={context.Traffic.ContrabandSeizureCount}");
    }

    private static (bool, string) FullySeizedCargoIsConserved()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        int initial = shipment?.RemainingQuantity ?? 0;
        context.DestroyCarrier(shipment);
        NpcShip police = context.AddPolice(Vector3.Zero);
        for (int attempt = 0; attempt < 16 && context.Loot.ActivePods.Count > 0; attempt++)
        {
            CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.GetCommodity()?.IsContraband == true);
            if (pod == null)
                break;
            police.Position = pod.Position;
            context.StepTraffic();
            context.StepTraffic();
        }

        return initial > 0 && context.Loot.ActivePods.All(pod => pod.GetCommodity()?.IsContraband != true) &&
            context.Traffic.TotalSeizedContrabandQuantity == initial
            ? Pass()
            : Fail($"initial={initial}, seized={context.Traffic.TotalSeizedContrabandQuantity}, pods={context.Loot.ActivePods.Count}");
    }

    private static (bool, string) MixedRecoveryAndSeizureIsConserved()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        int initial = shipment?.RemainingQuantity ?? 0;
        context.DestroyCarrier(shipment);
        CargoPod firstPod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.GetCommodity()?.IsContraband == true);
        Ship player = new(firstPod?.Position ?? Vector3.Zero);
        if (firstPod?.GetCommodity() == null)
            return Fail("no contraband pod was available");
        firstPod.Velocity = Vector3.Zero;
        firstPod.Position = new Vector3(100_000f, 0f, 0f);
        player.CargoHold.SetMaxCapacity(firstPod.GetCommodity().VolumePerUnit);
        int playerRecovered = context.PickUp(firstPod, player);
        NpcShip police = context.AddPolice(firstPod.Position);
        for (int attempt = 0; attempt < 16 && context.Loot.ActivePods.Any(pod => pod.GetCommodity()?.IsContraband == true); attempt++)
        {
            CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.GetCommodity()?.IsContraband == true);
            police.Position = pod.Position;
            context.StepTraffic();
            context.StepTraffic();
        }

        return initial > 0 && playerRecovered > 0 &&
            playerRecovered + context.Traffic.TotalSeizedContrabandQuantity == initial
            ? Pass()
            : Fail($"initial={initial}, player={playerRecovered}, seized={context.Traffic.TotalSeizedContrabandQuantity}");
    }

    private static (bool, string) SaveLoadCannotResurrectSeizedCargo()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        context.DestroyCarrier(shipment);
        NpcShip police = context.AddPolice(Vector3.Zero);
        foreach (CargoPod pod in context.Loot.ActivePods.Where(pod => pod.GetCommodity()?.IsContraband == true).ToList())
        {
            police.Position = pod.Position;
            context.StepTraffic();
            context.StepTraffic();
        }

        SaveGameData save = new()
        {
            PhysicalCargoPods = context.Loot.CaptureCargoPods(),
            RogueSmugglingShipments = context.Smuggling.CaptureState()
        };
        LootManager restoredLoot = new();
        restoredLoot.RestoreCargoPods(save.PhysicalCargoPods);
        return save.PhysicalCargoPods.Count == 0 && save.RogueSmugglingShipments.Count == 0 &&
            restoredLoot.ActivePods.Count == 0
            ? Pass()
            : Fail($"saved pods={save.PhysicalCargoPods.Count}, saved shipments={save.RogueSmugglingShipments.Count}, restored pods={restoredLoot.ActivePods.Count}");
    }

    private static (bool, string) ResetClearsEnforcement()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        int sourceAfterDeparture = context.Market.GetListingForCommodity(
            context.Buffalo,
            shipment.Manifest.Stacks[0].Commodity,
            MarketSurface.BlackMarket)?.Stock ?? -1;
        context.DestroyCarrier(shipment);
        NpcShip police = context.AddPolice(Vector3.Zero);
        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.GetCommodity()?.IsContraband == true);
        police.Position = pod?.Position ?? Vector3.Zero;
        context.StepTraffic();
        context.Traffic.ResetContrabandEnforcement();
        context.Loot.Reset();
        int sourceAfterReset = context.Market.GetListingForCommodity(
            context.Buffalo,
            shipment.Manifest.Stacks[0].Commodity,
            MarketSurface.BlackMarket)?.Stock ?? -1;
        return sourceAfterDeparture >= 0 && sourceAfterReset == sourceAfterDeparture &&
            context.Traffic.ContrabandSeizureCount == 0 && context.Loot.ActivePods.Count == 0 &&
            police.FactionCombatTarget == null
            ? Pass()
            : Fail($"source={sourceAfterDeparture}->{sourceAfterReset}, seizures={context.Traffic.ContrabandSeizureCount}, pods={context.Loot.ActivePods.Count}");
    }

    private static Dictionary<string, int> Snapshot(
        Context context,
        Station station,
        IEnumerable<TraderCargoStack> stacks)
    {
        return (stacks ?? Array.Empty<TraderCargoStack>())
            .Where(stack => stack?.Commodity != null)
            .ToDictionary(
                stack => stack.Commodity.Id,
                stack => context.Market.GetListingForCommodity(station, stack.Commodity, MarketSurface.BlackMarket)?.Stock ?? -1,
                StringComparer.OrdinalIgnoreCase);
    }

    private static GameTime Frame(double seconds) =>
        new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);
}
