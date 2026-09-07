using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 39 coverage for deterministic weapon drops, one bounded
    /// physical salvage pool, ownership transfer, dealer economy, and save
    /// boundaries.
    /// </summary>
    internal sealed class EquipmentSalvageSmokeTest
    {
        private const string StandardFaction = FactionManager.LibertyNavy;
        private const string HeavyFaction = FactionManager.LibertyRogues;

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(DeterministicStandardPolicy, "standard deterministic policy", ref passed, ref failed);
            RunCase(DeterministicHeavyPolicy, "heavy deterministic policy and bounds", ref passed, ref failed);
            RunCase(NoLoadoutProducesNoEquipment, "no eligible loadout produces no equipment", ref passed, ref failed);
            RunCase(SelectionComesFromDestroyedLoadout, "selection comes from destroyed loadout", ref passed, ref failed);
            RunCase(CommoditySalvageRemainsIndependent, "commodity salvage remains independent", ref passed, ref failed);
            RunCase(EquipmentPodCreationIsCanonical, "equipment pod uses canonical identity", ref passed, ref failed);
            RunCase(EquipmentSpawnsNearDeathAndSharesCap, "physical spawn and global cap", ref passed, ref failed);
            RunCase(ExpiryAndResetClearEquipmentPods, "expiry and reset lifecycle", ref passed, ref failed);
            RunCase(DuplicateDestructionCannotDuplicateEquipment, "duplicate destruction protection", ref passed, ref failed);
            RunCase(SuccessfulPickupTransfersOwnership, "successful pickup transfers ownership", ref passed, ref failed);
            RunCase(FullEquipmentStorageLeavesPod, "full storage leaves pod", ref passed, ref failed);
            RunCase(RetryAfterFreeingCapacitySucceeds, "retry after freeing capacity", ref passed, ref failed);
            RunCase(PickupCannotRepeat, "pickup cannot repeat", ref passed, ref failed);
            RunCase(NpcOnlyDestructionCanDropEquipment, "NPC-only destruction can drop equipment", ref passed, ref failed);
            RunCase(DealerListsOwnedSalvage, "dealer lists owned salvage", ref passed, ref failed);
            RunCase(OwnedSalvageCanBeEquipped, "owned salvage can be equipped", ref passed, ref failed);
            RunCase(SaleRemovesExactlyOneAndPaysOnce, "sale removes one and pays once", ref passed, ref failed);
            RunCase(SaveLoadPreservesCollectedEquipment, "save/load preserves collected equipment", ref passed, ref failed);
            RunCase(TemporaryPodsAreNotSaved, "temporary pods are not saved", ref passed, ref failed);
            RunCase(EquipmentCapacityIsBounded, "equipment capacity is bounded", ref passed, ref failed);

            Console.WriteLine($"[EQUIPMENT SALVAGE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[EQUIPMENT SALVAGE SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[EQUIPMENT SALVAGE SMOKE] FAIL {label}: {failureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[EQUIPMENT SALVAGE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) DeterministicStandardPolicy()
        {
            CombatSalvageService service = new();
            string name = FindEquipmentDropName(service, "Phase39 Standard Fighter", heavy: false);
            NpcShip first = CreateFighter(name, StandardFaction, heavy: false);
            NpcShip second = CreateFighter(name, StandardFaction, heavy: false);
            Destroy(first, NpcDestructionSource.Player);
            Destroy(second, NpcDestructionSource.Npc);

            SalvageDrop left = EquipmentDrops(service.EvaluateDestruction(first)).SingleOrDefault();
            SalvageDrop right = EquipmentDrops(new CombatSalvageService().EvaluateDestruction(second)).SingleOrDefault();
            return left != null && right != null && left.EquipmentId == right.EquipmentId &&
                   EquipmentCatalog.GetById(left.EquipmentId) is WeaponEquipmentDefinition
                ? Pass()
                : Fail("standard equipment decision was not stable and canonical");
        }

        private (bool Success, string FailureReason) DeterministicHeavyPolicy()
        {
            CombatSalvageService service = new();
            string name = FindEquipmentDropName(service, "Phase39 Warthog Heavy", heavy: true);
            NpcShip ship = CreateFighter(name, HeavyFaction, heavy: true);
            Destroy(ship, NpcDestructionSource.Npc);
            List<SalvageDrop> drops = EquipmentDrops(service.EvaluateDestruction(ship));
            return drops.Count >= 1 && drops.Count <= CombatSalvageService.HeavyMaximumEquipmentObjectsPerDestruction &&
                   drops.Count <= ship.Loadout.GetMountedGuns().Count() &&
                   drops.All(drop => drop.Quantity == 1 && drop.Tier == CombatSalvageTier.Heavy)
                ? Pass()
                : Fail("heavy equipment policy exceeded its bounded object count");
        }

        private (bool Success, string FailureReason) NoLoadoutProducesNoEquipment()
        {
            CombatSalvageService service = new();
            string name = FindEquipmentDropName(service, "Phase39 Empty Fighter", heavy: false);
            NpcShip ship = CreateNpc(name, StandardFaction, heavy: false);
            ship.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            Destroy(ship, NpcDestructionSource.Npc);
            return !service.EvaluateDestruction(ship).Any(drop => drop.IsEquipment)
                ? Pass()
                : Fail("empty NPC loadout generated an equipment payload");
        }

        private (bool Success, string FailureReason) SelectionComesFromDestroyedLoadout()
        {
            CombatSalvageService service = new();
            string name = FindEquipmentDropName(service, "Phase39 Custom Fighter", heavy: true);
            NpcShip ship = CreateNpc(name, HeavyFaction, heavy: true);
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            EquipmentDefinition light = EquipmentCatalog.GetById("liberty_pulse_cannon");
            EquipmentDefinition rogue = EquipmentCatalog.GetById("rogue_blaster");
            loadout.AddOwnedEquipment(light, 1);
            loadout.AddOwnedEquipment(rogue, 1);
            loadout.TryMountEquipment(light, out _);
            loadout.TryMountEquipment(rogue, out _);
            ship.SetLoadout(loadout);
            Destroy(ship, NpcDestructionSource.Environment);

            HashSet<string> eligible = new(new[] { light.Id, rogue.Id }, StringComparer.OrdinalIgnoreCase);
            List<SalvageDrop> drops = EquipmentDrops(service.EvaluateDestruction(ship));
            return drops.Count > 0 && drops.All(drop => eligible.Contains(drop.EquipmentId))
                ? Pass()
                : Fail("equipment salvage was not selected from the destroyed loadout");
        }

        private (bool Success, string FailureReason) CommoditySalvageRemainsIndependent()
        {
            CombatSalvageService service = new();
            string commodityName = FindCommodityDropName(service, "Phase39 Commodity Fighter", heavy: false);
            NpcShip commodityShip = CreateNpc(commodityName, StandardFaction, heavy: false);
            Destroy(commodityShip, NpcDestructionSource.Npc);
            IReadOnlyList<SalvageDrop> commodityDrops = service.EvaluateDestruction(commodityShip);

            string equipmentName = FindEquipmentDropName(service, "Phase39 Independent Fighter", heavy: false);
            NpcShip equipmentShip = CreateFighter(equipmentName, StandardFaction, heavy: false);
            Destroy(equipmentShip, NpcDestructionSource.Npc);
            IReadOnlyList<SalvageDrop> equipmentDrops = service.EvaluateDestruction(equipmentShip);
            return commodityDrops.Any(drop => drop.IsCommodity && CommodityCatalog.GetById(drop.CommodityId) != null) &&
                   equipmentDrops.Any(drop => drop.IsEquipment)
                ? Pass()
                : Fail("equipment addition changed the commodity salvage path");
        }

        private (bool Success, string FailureReason) EquipmentPodCreationIsCanonical()
        {
            EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
            bool created = CargoPod.TryCreateEquipment(weapon.Id, Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod);
            bool invalidRejected = !CargoPod.TryCreateEquipment("missing-weapon", Vector3.Zero, Vector3.Zero, 120f, 200f, out _);
            return created && pod != null && pod.IsEquipment && pod.EquipmentId == weapon.Id &&
                   pod.GetEquipment() == weapon && pod.GetCommodity() == null && invalidRejected
                ? Pass()
                : Fail("equipment pod did not preserve a valid canonical payload");
        }

        private (bool Success, string FailureReason) EquipmentSpawnsNearDeathAndSharesCap()
        {
            CombatSalvageService service = new();
            string name = FindEquipmentDropName(service, "Phase39 Spawn Warthog", heavy: true);
            NpcShip ship = CreateFighter(name, HeavyFaction, heavy: true, position: new Vector3(1000f, 50f, -200f));
            Destroy(ship, NpcDestructionSource.Npc);
            LootManager manager = new(null, null, null, null, service);
            int spawned = manager.SpawnLootForDestroyedNpc(ship);
            CargoPod equipmentPod = manager.ActivePods.FirstOrDefault(pod => pod.IsEquipment);
            float distance = equipmentPod == null ? 0f : Vector3.Distance(ship.Position, equipmentPod.Position);
            bool nearby = equipmentPod != null && distance >= CombatSalvageService.MinimumSpawnOffset &&
                          distance <= CombatSalvageService.MaximumSpawnOffset;

            while (manager.ActivePods.Count < CombatSalvageService.MaxLiveSalvageObjects)
            {
                CargoPod.TryCreateEquipment("liberty_pulse_cannon", new Vector3(20_000f + manager.ActivePods.Count, 0f, 0f), Vector3.Zero, 120f, 200f, out CargoPod filler);
                AddPod(manager, filler);
            }

            NpcShip second = CreateFighter(FindEquipmentDropName(service, "Phase39 Cap Fighter", false), StandardFaction, false, new Vector3(5000f, 0f, 0f));
            Destroy(second, NpcDestructionSource.Npc);
            int blockedSpawn = manager.SpawnLootForDestroyedNpc(second);
            return spawned >= 1 && nearby && manager.ActivePods.Count == CombatSalvageService.MaxLiveSalvageObjects && blockedSpawn == 0
                ? Pass()
                : Fail("equipment salvage did not use the shared bounded physical pool");
        }

        private (bool Success, string FailureReason) ExpiryAndResetClearEquipmentPods()
        {
            LootManager manager = new();
            CargoPod.TryCreateEquipment("liberty_pulse_cannon", Vector3.Zero, Vector3.Zero, 1f, 200f, out CargoPod pod);
            AddPod(manager, pod);
            manager.Update(Frame(1.1f), new Ship(new Vector3(1000f, 0f, 0f)), false);
            bool expired = manager.ActivePods.Count == 0;
            CargoPod.TryCreateEquipment("liberty_pulse_cannon", Vector3.Zero, Vector3.Zero, 120f, 200f, out pod);
            AddPod(manager, pod);
            manager.Reset();
            return expired && manager.ActivePods.Count == 0 && manager.SalvageService.ProcessedDestructionCount == 0
                ? Pass()
                : Fail("temporary equipment salvage survived expiry or reset");
        }

        private (bool Success, string FailureReason) DuplicateDestructionCannotDuplicateEquipment()
        {
            CombatSalvageService service = new();
            string name = FindEquipmentDropName(service, "Phase39 Duplicate Fighter", false);
            NpcShip ship = CreateFighter(name, StandardFaction, false);
            Destroy(ship, NpcDestructionSource.Player);
            LootManager manager = new(null, null, null, null, service);
            int first = manager.SpawnLootForDestroyedNpc(ship);
            int second = manager.SpawnLootForDestroyedNpc(ship);
            return first >= 1 && second == 0 && manager.ActivePods.Count(drop => drop.IsEquipment) <= 1
                ? Pass()
                : Fail("same destruction generated equipment salvage twice");
        }

        private (bool Success, string FailureReason) SuccessfulPickupTransfersOwnership()
        {
            Ship player = new(Vector3.Zero);
            player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            LootManager manager = new();
            CargoPod.TryCreateEquipment("liberty_pulse_cannon", Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, false);
            return manager.ActivePods.Count == 0 && player.Loadout.GetOwnedCount("liberty_pulse_cannon") == 1 &&
                   manager.LastPickupNotification == "Salvaged: 1x Liberty Pulse Cannon"
                ? Pass()
                : Fail("successful pickup did not atomically transfer durable ownership");
        }

        private (bool Success, string FailureReason) FullEquipmentStorageLeavesPod()
        {
            Ship player = CreateEmptyPlayer();
            EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
            player.Loadout.AddOwnedEquipment(weapon, ShipLoadout.MaximumOwnedEquipmentCount);
            LootManager manager = new();
            CargoPod.TryCreateEquipment(weapon.Id, Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, false);
            return manager.ActivePods.Count == 1 && pod.Quantity == 1 &&
                   manager.LastPickupNotification == "Equipment storage full"
                ? Pass()
                : Fail("full equipment storage deleted or repeatedly transferred the pod");
        }

        private (bool Success, string FailureReason) RetryAfterFreeingCapacitySucceeds()
        {
            Ship player = CreateEmptyPlayer();
            EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
            player.Loadout.AddOwnedEquipment(weapon, ShipLoadout.MaximumOwnedEquipmentCount);
            LootManager manager = new();
            CargoPod.TryCreateEquipment(weapon.Id, Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, false);
            player.Loadout.RemoveOwnedEquipment(weapon.Id, 1);
            manager.Update(Frame(0.1f), player, false);
            return manager.ActivePods.Count == 0 && player.Loadout.GetOwnedCount(weapon.Id) == ShipLoadout.MaximumOwnedEquipmentCount
                ? Pass()
                : Fail("equipment pod could not be retried after capacity was freed");
        }

        private (bool Success, string FailureReason) PickupCannotRepeat()
        {
            Ship player = CreateEmptyPlayer();
            LootManager manager = new();
            CargoPod.TryCreateEquipment("liberty_pulse_cannon", Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod);
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, false);
            int owned = player.Loadout.GetOwnedCount("liberty_pulse_cannon");
            manager.Update(Frame(0.1f), player, false);
            return owned == 1 && player.Loadout.GetOwnedCount("liberty_pulse_cannon") == 1
                ? Pass()
                : Fail("collected equipment was transferred more than once");
        }

        private (bool Success, string FailureReason) NpcOnlyDestructionCanDropEquipment()
        {
            CombatSalvageService service = new();
            string name = FindEquipmentDropName(service, "Phase39 Npc Versus Npc Fighter", false);
            NpcShip ship = CreateFighter(name, StandardFaction, false);
            Destroy(ship, NpcDestructionSource.Npc);
            List<SalvageDrop> equipment = EquipmentDrops(service.EvaluateDestruction(ship));
            PlayerCredits credits = new(0);
            return equipment.Count > 0 && credits.Credits == 0
                ? Pass()
                : Fail("physical equipment salvage required player attribution or paid a bounty");
        }

        private (bool Success, string FailureReason) DealerListsOwnedSalvage()
        {
            Ship player = CreateEmptyPlayer();
            EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
            player.Loadout.AddOwnedEquipment(weapon, 1);
            EquipmentDealer dealer = new();
            return dealer.GetOwnedEquipment(player.Loadout).Any(item => item.Id == weapon.Id)
                ? Pass()
                : Fail("owned salvaged weapon was not exposed to the dealer inventory");
        }

        private (bool Success, string FailureReason) OwnedSalvageCanBeEquipped()
        {
            Ship player = CreateEmptyPlayer();
            EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
            player.Loadout.AddOwnedEquipment(weapon, 1);
            EquipmentDealer dealer = new();
            bool mounted = dealer.TryMountEquipment(weapon, player, out _);
            return mounted && player.Loadout.GetMountedCount(weapon.Id) == 1 &&
                   player.Loadout.GetOwnedCount(weapon.Id) == 1
                ? Pass()
                : Fail("owned salvaged weapon could not be installed through the dealer seam");
        }

        private (bool Success, string FailureReason) SaleRemovesExactlyOneAndPaysOnce()
        {
            Ship player = CreateEmptyPlayer();
            EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
            player.Loadout.AddOwnedEquipment(weapon, 2);
            EquipmentDealer dealer = new();
            PlayerCredits credits = new(0);
            int resale = dealer.GetResaleValue(weapon);
            bool first = dealer.TrySellUnequippedEquipment(weapon, credits, player, out _);
            bool second = dealer.TrySellUnequippedEquipment(weapon, credits, player, out _);
            bool third = dealer.TrySellUnequippedEquipment(weapon, credits, player, out _);
            return first && second && !third && player.Loadout.GetOwnedCount(weapon.Id) == 0 &&
                   credits.Credits == resale * 2
                ? Pass()
                : Fail("sale did not remove exactly one spare item per successful transaction");
        }

        private (bool Success, string FailureReason) SaveLoadPreservesCollectedEquipment()
        {
            string path = Path.Combine(Path.GetTempPath(), $"roguelancer_phase39_{Guid.NewGuid():N}.json");
            try
            {
                Ship source = CreateEmptyPlayer();
                EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
                source.Loadout.AddOwnedEquipment(weapon, 2);
                SaveGameManager manager = new(path);
                SaveGameData data = new() { OwnedEquipment = manager.CaptureOwnedEquipment(source.Loadout) };
                bool saved = manager.TrySave(data, out _);
                bool loaded = manager.TryLoad(out SaveGameData restored, out _);
                ShipLoadout rebuilt = manager.BuildLoadout(restored, out _);
                return saved && loaded && SaveGameData.CurrentSchemaVersion == 11 &&
                       rebuilt.GetOwnedCount(weapon.Id) == 2
                    ? Pass()
                    : Fail("collected equipment did not survive the existing save schema");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private (bool Success, string FailureReason) TemporaryPodsAreNotSaved()
        {
            string json = System.Text.Json.JsonSerializer.Serialize(new SaveGameData());
            return !json.Contains("CargoPod", StringComparison.OrdinalIgnoreCase) &&
                   !json.Contains("Salvage", StringComparison.OrdinalIgnoreCase)
                ? Pass()
                : Fail("temporary physical salvage leaked into save data");
        }

        private (bool Success, string FailureReason) EquipmentCapacityIsBounded()
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            EquipmentDefinition weapon = EquipmentCatalog.GetById("liberty_pulse_cannon");
            int added = ShipLoadout.MaximumOwnedEquipmentCount - loadout.OwnedEquipmentCount;
            bool filled = loadout.AddOwnedEquipment(weapon, added);
            bool rejected = !loadout.AddOwnedEquipment(weapon, 1);
            return filled && rejected && loadout.OwnedEquipmentCount == ShipLoadout.MaximumOwnedEquipmentCount
                ? Pass()
                : Fail("owned equipment inventory was not bounded");
        }

        private static NpcShip CreateFighter(string name, string factionId, bool heavy, Vector3? position = null)
        {
            NpcShip ship = CreateNpc(name, factionId, heavy, position);
            ship.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(name, factionId, ship.ModelPath));
            return ship;
        }

        private static NpcShip CreateNpc(string name, string factionId, bool heavy, Vector3? position = null)
        {
            Vector3 start = position ?? Vector3.Zero;
            NpcShip ship = new(name, start, start, 1f, 0f, factionId)
            {
                ModelPath = heavy ? "SHIPS/WARTHOG/warthog" : string.Empty
            };
            ship.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase39-smoke", start, 500f, 100f);
            return ship;
        }

        private static string FindEquipmentDropName(CombatSalvageService service, string prefix, bool heavy)
        {
            for (int i = 0; i < 10_000; i++)
            {
                string name = $"{prefix} {i}";
                NpcShip ship = CreateFighter(name, heavy ? HeavyFaction : StandardFaction, heavy);
                if (service.ShouldDropEquipment(ship)) return name;
            }

            throw new InvalidOperationException("could not find a deterministic equipment-drop identity");
        }

        private static string FindCommodityDropName(CombatSalvageService service, string prefix, bool heavy)
        {
            for (int i = 0; i < 10_000; i++)
            {
                string name = $"{prefix} {i}";
                NpcShip ship = CreateFighter(name, heavy ? HeavyFaction : StandardFaction, heavy);
                if (service.ShouldDrop(ship)) return name;
            }

            throw new InvalidOperationException("could not find a deterministic commodity-drop identity");
        }

        private static List<SalvageDrop> EquipmentDrops(IReadOnlyList<SalvageDrop> drops)
        {
            return drops.Where(drop => drop != null && drop.IsEquipment).ToList();
        }

        private static void Destroy(NpcShip ship, NpcDestructionSource source)
        {
            ship.ApplyDamage(ship.Hull.CurrentHull + 1f, source);
        }

        private static Ship CreateEmptyPlayer()
        {
            Ship player = new(Vector3.Zero);
            player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            return player;
        }

        private static void AddPod(LootManager manager, CargoPod pod)
        {
            if (manager == null || pod == null || manager.ActivePods is not List<CargoPod> pods)
                throw new InvalidOperationException("could not access bounded salvage pool");
            pods.Add(pod);
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
            try { return action(); }
            finally { Console.SetOut(previous); }
        }

        private static (bool Success, string FailureReason) RunSilenced(Func<(bool Success, string FailureReason)> action) =>
            RunSilenced<(bool Success, string FailureReason)>(action);
    }
}
