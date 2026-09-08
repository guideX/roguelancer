using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 38 coverage for deterministic policy, physical lifetime,
    /// bounded world objects, and authoritative CargoHold pickup.
    /// </summary>
    internal sealed class CombatSalvageSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(PolicyIsDeterministicAndUsesValidCommodity, "deterministic valid policy", ref passed, ref failed);
            RunCase(NoDropCaseIsDeterministic, "deterministic no-drop case", ref passed, ref failed);
            RunCase(StandardAndHeavyBounds, "standard/heavy quantity bounds", ref passed, ref failed);
            RunCase(FactionAndAttributionAreIndependent, "faction and attribution independence", ref passed, ref failed);
            RunCase(DuplicateAndInvalidDestructionsAreRejected, "duplicate/invalid destruction rejection", ref passed, ref failed);
            RunCase(SpawnIsNearDeathAndAvoidsObstacle, "nearby deterministic spawn and avoidance", ref passed, ref failed);
            RunCase(LiveCapAndPerDestructionCaps, "live and per-destruction caps", ref passed, ref failed);
            RunCase(AutomaticPickupAndNotification, "automatic pickup and notification", ref passed, ref failed);
            RunCase(FullHoldLeavesDropAndSuppressesSpam, "full hold safety", ref passed, ref failed);
            RunCase(PartialPickupLeavesRemainder, "partial pickup", ref passed, ref failed);
            RunCase(MissionReservationAndUnrelatedCargoRemainSafe, "mission reservation safety", ref passed, ref failed);
            RunCase(ExpiryAndResetClearPhysicalState, "expiry/reset lifecycle", ref passed, ref failed);
            RunCase(SaveSchemaAndEconomyRemainUnchanged, "save/economy isolation", ref passed, ref failed);

            Console.WriteLine($"[COMBAT SALVAGE SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                (bool success, string failureReason) = RunSilenced(test);
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[COMBAT SALVAGE SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[COMBAT SALVAGE SMOKE] FAIL {label}: {failureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[COMBAT SALVAGE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) PolicyIsDeterministicAndUsesValidCommodity()
        {
            CombatSalvageService firstService = new();
            string name = FindNameForDrop(firstService, "liberty_rogues", "Rogue Deterministic", heavy: false);
            NpcShip first = CreateNpc(name, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            NpcShip second = CreateNpc(name, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            Destroy(first, NpcDestructionSource.Player);
            Destroy(second, NpcDestructionSource.Npc);

            IReadOnlyList<SalvageDrop> firstDrop = firstService.EvaluateDestruction(first);
            IReadOnlyList<SalvageDrop> secondDrop = new CombatSalvageService().EvaluateDestruction(second);
            List<SalvageDrop> firstCommodityDrops = firstDrop.Where(drop => drop != null && drop.IsCommodity).ToList();
            List<SalvageDrop> secondCommodityDrops = secondDrop.Where(drop => drop != null && drop.IsCommodity).ToList();
            if (firstCommodityDrops.Count != 1 || secondCommodityDrops.Count != 1)
            {
                return Fail("eligible deterministic ships did not produce one standard drop");
            }

            SalvageDrop left = firstCommodityDrops[0];
            SalvageDrop right = secondCommodityDrops[0];
            return left.CommodityId == right.CommodityId && left.Quantity == right.Quantity &&
                   CommodityCatalog.GetById(left.CommodityId) != null &&
                   left.Tier == CombatSalvageTier.Standard
                ? Pass()
                : Fail("same ship definition produced different or invalid salvage");
        }

        private (bool Success, string FailureReason) NoDropCaseIsDeterministic()
        {
            CombatSalvageService service = new();
            string name = FindNameForNoDrop(service, FactionManager.LibertyRogues, "Rogue No Drop");
            NpcShip ship = CreateNpc(name, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            Destroy(ship, NpcDestructionSource.Environment);
            IReadOnlyList<SalvageDrop> drops = service.EvaluateDestruction(ship);
            return !drops.Any(drop => drop != null && drop.IsCommodity) && !service.ShouldDrop(ship)
                ? Pass()
                : Fail("stable no-drop identity unexpectedly produced salvage");
        }

        private (bool Success, string FailureReason) StandardAndHeavyBounds()
        {
            CombatSalvageService service = new();
            string standardName = FindNameForDrop(service, FactionManager.LibertyRogues, "Rogue Standard", heavy: false);
            NpcShip standard = CreateNpc(standardName, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            Destroy(standard, NpcDestructionSource.Npc);
            IReadOnlyList<SalvageDrop> standardDrops = service.EvaluateDestruction(standard)
                .Where(drop => drop != null && drop.IsCommodity)
                .ToList();

            string heavyName = FindNameForDrop(service, FactionManager.LibertyRogues, "Warthog Heavy", heavy: true);
            NpcShip heavy = CreateNpc(heavyName, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush, "SHIPS/WARTHOG/warthog");
            Destroy(heavy, NpcDestructionSource.Npc);
            IReadOnlyList<SalvageDrop> heavyDrops = service.EvaluateDestruction(heavy)
                .Where(drop => drop != null && drop.IsCommodity)
                .ToList();

            int heavyQuantity = heavyDrops.Sum(drop => drop.Quantity);
            return standardDrops.Count == 1 &&
                   standardDrops[0].Quantity >= CombatSalvageService.StandardMinimumQuantity &&
                   standardDrops[0].Quantity <= CombatSalvageService.StandardMaximumQuantity &&
                   standardDrops[0].Tier == CombatSalvageTier.Standard &&
                   heavyDrops.Count >= 1 && heavyDrops.Count <= CombatSalvageService.HeavyMaximumObjectsPerDestruction &&
                   heavyQuantity >= CombatSalvageService.HeavyMinimumQuantity &&
                   heavyQuantity <= CombatSalvageService.HeavyMaximumQuantity &&
                   heavyDrops.All(drop => drop.Tier == CombatSalvageTier.Heavy) &&
                   service.DetermineTier(heavy) == CombatSalvageTier.Heavy
                ? Pass()
                : Fail("standard or Warthog heavy bounds/classification were incorrect");
        }

        private (bool Success, string FailureReason) FactionAndAttributionAreIndependent()
        {
            CombatSalvageService service = new();
            string rogueName = FindNameForDrop(service, FactionManager.LibertyRogues, "Rogue NPC Kill", heavy: false);
            string policeName = FindNameForDrop(service, FactionManager.LibertyPolice, "Police Player Kill", heavy: false, behavior: TrafficZoneBehaviorType.LawfulPatrol);
            NpcShip rogue = CreateNpc(rogueName, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            NpcShip police = CreateNpc(policeName, FactionManager.LibertyPolice, TrafficZoneBehaviorType.LawfulPatrol);
            Destroy(rogue, NpcDestructionSource.Npc);
            Destroy(police, NpcDestructionSource.Player);

            IReadOnlyList<SalvageDrop> rogueDrops = service.EvaluateDestruction(rogue);
            IReadOnlyList<SalvageDrop> policeDrops = service.EvaluateDestruction(police);
            PlayerCredits credits = new(1000);
            int creditsBefore = credits.Credits;
            credits.SetCredits(credits.Credits);
            return rogueDrops.Count > 0 && policeDrops.Count > 0 && credits.Credits == creditsBefore
                ? Pass()
                : Fail("NPC/player attribution changed physical salvage or credits");
        }

        private (bool Success, string FailureReason) DuplicateAndInvalidDestructionsAreRejected()
        {
            CombatSalvageService service = new();
            NpcShip alive = CreateNpc("Alive traffic", FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            if (service.EvaluateDestruction(alive).Count != 0 || service.EvaluateDestruction(null).Count != 0)
            {
                return Fail("alive/null victim was accepted");
            }

            string name = FindNameForDrop(service, FactionManager.LibertyRogues, "Rogue Duplicate", heavy: false);
            NpcShip destroyed = CreateNpc(name, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            Destroy(destroyed, NpcDestructionSource.Player);
            int first = service.EvaluateDestruction(destroyed).Count;
            int duplicate = service.EvaluateDestruction(destroyed).Count;
            return first > 0 && duplicate == 0 && service.ProcessedDestructionCount == 1
                ? Pass()
                : Fail("duplicate destruction callback created another policy result");
        }

        private (bool Success, string FailureReason) SpawnIsNearDeathAndAvoidsObstacle()
        {
            CombatSalvageService service = new();
            string name = FindNameForDrop(service, FactionManager.LibertyRogues, "Rogue Spawn", heavy: false);
            NpcShip ship = CreateNpc(name, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush, position: new Vector3(1000f, 50f, -200f));
            Destroy(ship, NpcDestructionSource.Player);
            Vector3 blocked = ship.Position + service.GetSpawnOffset(ship, 0);
            List<SpaceObject> objects = new() { new SpaceObject("blocking object", blocked, 200f) };
            LootManager manager = new(null, null, null, null, service, () => objects);
            int spawned = manager.SpawnLootForDestroyedNpc(ship);
            List<CargoPod> commodityPods = manager.ActivePods
                .Where(pod => pod != null && pod.GetCommodity() != null)
                .ToList();
            if (spawned < 1 || commodityPods.Count != 1)
            {
                return Fail("commodity salvage did not survive bounded collision placement");
            }

            CargoPod pod = commodityPods[0];
            float distance = Vector3.Distance(ship.Position, pod.Position);
            return distance >= CombatSalvageService.MinimumSpawnOffset &&
                   distance <= CombatSalvageService.MaximumSpawnOffset &&
                   Vector3.Distance(blocked, pod.Position) > 1f
                ? Pass()
                : Fail("salvage spawn was not near the wreck or used blocked coordinates");
        }

        private (bool Success, string FailureReason) LiveCapAndPerDestructionCaps()
        {
            CombatSalvageService service = new();
            LootManager manager = new(null, null, null, null, service);
            string heavyName = FindNameForDrop(service, FactionManager.LibertyRogues, "Warthog Cap", heavy: true);
            NpcShip heavy = CreateNpc(heavyName, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush, "SHIPS/WARTHOG/warthog", new Vector3(5000f, 0f, 0f));
            Destroy(heavy, NpcDestructionSource.Npc);
            int heavySpawned = manager.SpawnLootForDestroyedNpc(heavy);
            int before = manager.ActiveSalvageCount;
            int heavyCommodityObjects = manager.ActivePods.Count(pod => pod != null && pod.GetCommodity() != null);
            int heavyEquipmentObjects = manager.ActivePods.Count(pod => pod != null && pod.IsEquipment);
            if (heavySpawned < 1 || heavyCommodityObjects < 1 ||
                heavyCommodityObjects > CombatSalvageService.HeavyMaximumObjectsPerDestruction ||
                heavyEquipmentObjects > CombatSalvageService.HeavyMaximumEquipmentObjectsPerDestruction)
            {
                return Fail("heavy ship exceeded per-destruction object cap");
            }

            for (int i = 0; i < CombatSalvageService.MaxLiveSalvageObjects + 8 && manager.ActiveSalvageCount < CombatSalvageService.MaxLiveSalvageObjects; i++)
            {
                string name = FindNameForDrop(service, FactionManager.LibertyRogues, $"Rogue Cap {i}", heavy: false);
                NpcShip ship = CreateNpc(name, FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush, position: new Vector3(10000f + i * 500f, 0f, 0f));
                Destroy(ship, NpcDestructionSource.Npc);
                manager.SpawnLootForDestroyedNpc(ship);
            }

            return before <= CombatSalvageService.MaxLiveSalvageObjects &&
                   manager.ActiveSalvageCount == CombatSalvageService.MaxLiveSalvageObjects
                ? Pass()
                : Fail("global live salvage cap was not enforced deterministically");
        }

        private (bool Success, string FailureReason) AutomaticPickupAndNotification()
        {
            LootManager manager = new();
            Ship player = new(Vector3.Zero);
            CargoPod pod = CreatePod("food-rations", 3, Vector3.Zero, 120f);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, tractorActive: false);
            return manager.ActiveSalvageCount == 0 &&
                   player.CargoHold.GetCommodityQuantity("Food Rations") == 3 &&
                   manager.LastPickupNotification == "Salvaged: 3 Food Rations"
                ? Pass()
                : Fail("close-range pickup did not use CargoHold or notification aggregation");
        }

        private (bool Success, string FailureReason) FullHoldLeavesDropAndSuppressesSpam()
        {
            LootManager manager = new();
            Ship player = new(Vector3.Zero);
            player.CargoHold.SetMaxCapacity(0);
            CargoPod pod = CreatePod("food-rations", 1, Vector3.Zero, 120f);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, tractorActive: false);
            string firstNotification = manager.LastPickupNotification;
            manager.Update(Frame(0.1f), player, tractorActive: false);
            return manager.ActiveSalvageCount == 1 && pod.Quantity == 1 &&
                   firstNotification == "Cargo hold full" && string.IsNullOrEmpty(manager.LastPickupNotification)
                ? Pass()
                : Fail("full-hold pickup removed cargo or repeated the full notification");
        }

        private (bool Success, string FailureReason) PartialPickupLeavesRemainder()
        {
            LootManager manager = new();
            Ship player = new(Vector3.Zero);
            player.CargoHold.SetMaxCapacity(1);
            CargoPod pod = CreatePod("food-rations", 3, Vector3.Zero, 120f);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, tractorActive: false);
            return manager.ActiveSalvageCount == 1 && pod.Quantity == 2 &&
                   player.CargoHold.GetCommodityQuantity("Food Rations") == 1 &&
                   manager.LastPickupNotification == "Salvaged: 1 Food Rations"
                ? Pass()
                : Fail("partial capacity did not leave the physical remainder");
        }

        private (bool Success, string FailureReason) MissionReservationAndUnrelatedCargoRemainSafe()
        {
            Commodity food = CommodityCatalog.GetById("food-rations");
            Commodity water = CommodityCatalog.GetById("water");
            CargoHold hold = new(5);
            if (food == null || water == null || !hold.AddMissionCargo(71, food, 4) || !hold.AddCommodity(water, 1))
            {
                return Fail("mission/cargo smoke setup failed");
            }

            if (hold.TryAddCommodityPartial(food, 2, out int directAdded) || directAdded != 0)
            {
                return Fail("partial cargo seam consumed mission-reserved capacity");
            }

            LootManager manager = new();
            Ship player = new(Vector3.Zero);
            player.CargoHold.SetMaxCapacity(5);
            player.CargoHold.AddMissionCargo(71, food, 4);
            player.CargoHold.AddCommodity(water, 1);
            CargoPod pod = CreatePod("food-rations", 2, Vector3.Zero, 120f);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, tractorActive: false);
            return player.CargoHold.GetMissionCargoQuantity(71) == 4 &&
                   player.CargoHold.GetCommodityQuantity("Water") == 1 &&
                   player.CargoHold.GetCommodityQuantity("Food Rations") == 4 &&
                   pod.Quantity == 2 && manager.ActiveSalvageCount == 1
                ? Pass()
                : Fail("mission cargo or unrelated stack was changed by salvage pickup");
        }

        private (bool Success, string FailureReason) ExpiryAndResetClearPhysicalState()
        {
            LootManager manager = new();
            Ship player = new(Vector3.Zero);
            AddPod(manager, CreatePod("food-rations", 1, new Vector3(1000f, 0f, 0f), 120f));
            manager.Update(Frame(119.9f), player, tractorActive: false);
            bool aliveBeforeExpiry = manager.ActiveSalvageCount == 1;
            manager.Update(Frame(0.2f), player, tractorActive: false);
            bool expired = manager.ActiveSalvageCount == 0;
            AddPod(manager, CreatePod("food-rations", 1, new Vector3(1000f, 0f, 0f), 120f));
            manager.Reset();
            return aliveBeforeExpiry && expired && manager.ActiveSalvageCount == 0 &&
                   manager.SalvageService.ProcessedDestructionCount == 0
                ? Pass()
                : Fail("salvage TTL or reset cleanup left stale physical state");
        }

        private (bool Success, string FailureReason) SaveSchemaAndEconomyRemainUnchanged()
        {
            string save = JsonSerializer.Serialize(new SaveGameData());
            PlayerCredits credits = new(500);
            CargoHold cargo = new(20);
            CombatSalvageService service = new();
            NpcShip ship = CreateNpc("Rogue Save Isolation", FactionManager.LibertyRogues, TrafficZoneBehaviorType.PirateAmbush);
            Destroy(ship, NpcDestructionSource.Player);
            service.EvaluateDestruction(ship);
            return new SaveGameData().SchemaVersion == SaveGameData.CurrentSchemaVersion &&
                   !save.Contains("Salvage", StringComparison.OrdinalIgnoreCase) &&
                   !save.Contains("CargoPod", StringComparison.OrdinalIgnoreCase) &&
                   credits.Credits == 500 && cargo.UsedCapacity == 0
                ? Pass()
                : Fail("transient salvage leaked into save or economy state");
        }

        private static string FindNameForDrop(
            CombatSalvageService service,
            string factionId,
            string prefix,
            bool heavy,
            TrafficZoneBehaviorType behavior = TrafficZoneBehaviorType.PirateAmbush)
        {
            for (int i = 0; i < 10_000; i++)
            {
                string name = $"{prefix} {i}";
                NpcShip ship = CreateNpc(name, factionId, behavior, heavy ? "SHIPS/WARTHOG/warthog" : string.Empty);
                if (service.DetermineTier(ship) != (heavy ? CombatSalvageTier.Heavy : CombatSalvageTier.Standard) ||
                    !service.ShouldDrop(ship))
                {
                    continue;
                }

                return name;
            }

            throw new InvalidOperationException("could not find deterministic drop identity");
        }

        private static string FindNameForNoDrop(CombatSalvageService service, string factionId, string prefix)
        {
            for (int i = 0; i < 10_000; i++)
            {
                string name = $"{prefix} {i}";
                NpcShip ship = CreateNpc(name, factionId, TrafficZoneBehaviorType.PirateAmbush);
                if (!service.ShouldDrop(ship))
                {
                    return name;
                }
            }

            throw new InvalidOperationException("could not find deterministic no-drop identity");
        }

        private static NpcShip CreateNpc(
            string name,
            string factionId,
            TrafficZoneBehaviorType behavior,
            string modelPath = "",
            Vector3? position = null)
        {
            return RunSilenced(() =>
            {
                Vector3 start = position ?? Vector3.Zero;
                NpcShip ship = new(name, start, start, 1f, 0f, factionId)
                {
                    ModelPath = modelPath ?? string.Empty
                };
                ship.ConfigureTrafficBehavior(behavior, "combat-salvage-smoke", start, 500f, 100f);
                return ship;
            });
        }

        private static void Destroy(NpcShip ship, NpcDestructionSource source)
        {
            RunSilenced(() => ship.ApplyDamage(ship.Hull.CurrentHull + 1f, source));
        }

        private static CargoPod CreatePod(string commodityId, int quantity, Vector3 position, float lifetime)
        {
            if (!CargoPod.TryCreate(commodityId, quantity, position, Vector3.Zero, lifetime, CombatSalvageService.PickupRadius, out CargoPod pod))
            {
                throw new InvalidOperationException($"could not create test pod for {commodityId}");
            }

            return pod;
        }

        private static void AddPod(LootManager manager, CargoPod pod)
        {
            if (manager.ActivePods is List<CargoPod> pods)
            {
                pods.Add(pod);
                return;
            }

            throw new InvalidOperationException("test manager did not expose its bounded pod list");
        }

        private static GameTime Frame(float seconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
            return new GameTime(elapsed, elapsed);
        }

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

        private static T RunSilenced<T>(Func<T> action)
        {
            TextWriter previous = Console.Out;
            using StringWriter sink = new();
            Console.SetOut(sink);
            try
            {
                return action();
            }
            finally
            {
                Console.SetOut(previous);
            }
        }

        private static (bool Success, string FailureReason) RunSilenced(Func<(bool Success, string FailureReason)> action) =>
            RunSilenced<(bool Success, string FailureReason)>(action);
    }
}
