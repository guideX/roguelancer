using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 43 coverage for canonical combat consumables, economy,
    /// bounded NPC use, physical salvage, and schema-10 persistence.
    /// </summary>
    internal sealed class CombatConsumableSmokeTest
    {
        private int _passed;
        private int _failed;

        public (int Passed, int Failed) Run()
        {
            Check("canonical consumable definitions are valid", CanonicalDefinitionsAreValid);
            Check("consumables cannot mount in gun or shield slots", ConsumablesAreNotMountable);
            Check("new player starts with bounded modest supplies", StarterSuppliesAreBounded);
            Check("Nanobots repair hull and consume one", NanobotsRepairExactlyOne);
            Check("Nanobots clamp at maximum hull", NanobotsClampAtMaximum);
            Check("full hull consumes no Nanobot", FullHullConsumesNothing);
            Check("Shield Batteries restore shield and consume one", ShieldBatteryRestoresExactlyOne);
            Check("Shield Batteries clamp at capacity and do not alter hull", ShieldBatteryClampsWithoutHullChange);
            Check("full shield consumes no battery", FullShieldConsumesNothing);
            Check("battery without mounted shield consumes nothing", BatteryWithoutShieldConsumesNothing);
            Check("held flight input is edge-triggered", HeldInputConsumesOnce);
            Check("dealer buys Nanobots atomically", DealerBuysNanobots);
            Check("dealer sells Nanobots atomically", DealerSellsNanobots);
            Check("dealer buys Shield Batteries atomically", DealerBuysShieldBatteries);
            Check("dealer sells Shield Batteries atomically", DealerSellsShieldBatteries);
            Check("carry limits and insufficient credits are safe", CarryAndCreditLimitsAreSafe);
            Check("NPC supplies are deterministic", NpcSuppliesAreDeterministic);
            Check("NPC supplies remain bounded", NpcSuppliesAreBounded);
            Check("NPC consumption reduces actual carried supply", NpcConsumptionReducesSupply);
            Check("NPC consumption cooldown prevents repeat use", NpcCooldownPreventsRepeatUse);
            Check("consumable salvage never exceeds remaining supply", ConsumableSalvageIsBoundedBySource);
            Check("consumable salvage uses physical cargo pods", ConsumableSalvageUsesPhysicalPods);
            Check("consumable pickup increases authoritative quantity", ConsumablePickupTransfersQuantity);
            Check("consumable capacity overflow leaves physical remainder", ConsumableOverflowLeavesRemainder);
            Check("weapon salvage remains mounted-weapon-only", WeaponSalvageRemainsIndependent);
            Check("shield salvage remains mounted-shield-only", ShieldSalvageRemainsIndependent);
            Check("save/load preserves consumable quantities", SaveLoadPreservesConsumables);
            Check("older saves default missing consumables safely", OlderSavesDefaultConsumables);
            Check("malformed saved quantities clamp safely", MalformedQuantitiesClamp);
            Check("NPC transient reset clears cooldown", ResetClearsNpcTransientState);
            Check("destroyed ships cannot consume", DestroyedShipCannotConsume);
            Check("unknown and invalid consumable input is safe", InvalidConsumableInputIsSafe);

            Console.WriteLine($"[COMBAT CONSUMABLE SMOKE] RESULT: {_passed} passed, {_failed} failed");
            return (_passed, _failed);
        }

        private void Check(string label, Func<bool> assertion)
        {
            try
            {
                if (RunSilenced(assertion))
                {
                    _passed++;
                    Console.WriteLine($"[COMBAT CONSUMABLE SMOKE] PASS {label}");
                }
                else
                {
                    _failed++;
                    Console.WriteLine($"[COMBAT CONSUMABLE SMOKE] FAIL {label}: assertion returned false");
                }
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"[COMBAT CONSUMABLE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static bool CanonicalDefinitionsAreValid()
        {
            ConsumableEquipmentDefinition nanobots = Nanobots;
            ConsumableEquipmentDefinition batteries = ShieldBatteries;
            return nanobots != null && batteries != null && nanobots.IsValid && batteries.IsValid &&
                   nanobots.Id == CombatConsumableIds.Nanobots &&
                   batteries.Id == CombatConsumableIds.ShieldBatteries &&
                   nanobots.ConsumableType == CombatConsumableType.Nanobots &&
                   batteries.ConsumableType == CombatConsumableType.ShieldBattery &&
                   nanobots.RestorationAmount > 0f && batteries.RestorationAmount > 0f;
        }

        private static bool ConsumablesAreNotMountable()
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            loadout.AddOwnedEquipment(Nanobots, 1);
            loadout.AddOwnedEquipment(ShieldBatteries, 1);
            bool gunRejected = !loadout.TryMountEquipment("PrimaryGunLeft", Nanobots, out _);
            bool shieldRejected = !loadout.TryMountEquipment("ShieldGenerator", ShieldBatteries, out _);
            bool dealerMountRejected = !new EquipmentDealer().TryMountEquipment(Nanobots, loadout, out _);
            return gunRejected && shieldRejected && dealerMountRejected &&
                   loadout.GetMountedCount(CombatConsumableIds.Nanobots) == 0 &&
                   loadout.GetMountedCount(CombatConsumableIds.ShieldBatteries) == 0;
        }

        private static bool StarterSuppliesAreBounded()
        {
            Ship player = new(Vector3.Zero);
            return player.CombatConsumables.Nanobots == 5 &&
                   player.CombatConsumables.ShieldBatteries == 5 &&
                   player.CombatConsumables.Nanobots <= CombatConsumableInventory.MaximumNanobots &&
                   player.CombatConsumables.ShieldBatteries <= CombatConsumableInventory.MaximumShieldBatteries;
        }

        private static bool NanobotsRepairExactlyOne()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.Nanobots = 2;
            player.Hull.TakeDamage(40f);
            float beforeHull = player.Hull.CurrentHull;
            bool used = player.TryUseNanobot(out _);
            return used && Nearly(player.Hull.CurrentHull, beforeHull + Nanobots.RestorationAmount) &&
                   player.CombatConsumables.Nanobots == 1;
        }

        private static bool NanobotsClampAtMaximum()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.Nanobots = 1;
            player.Hull.TakeDamage(5f);
            bool used = player.TryUseNanobot(out _);
            return used && Nearly(player.Hull.CurrentHull, player.Hull.MaxHull) &&
                   player.CombatConsumables.Nanobots == 0;
        }

        private static bool FullHullConsumesNothing()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.Nanobots = 1;
            bool used = player.TryUseNanobot(out _);
            return !used && player.CombatConsumables.Nanobots == 1 &&
                   Nearly(player.Hull.CurrentHull, player.Hull.MaxHull);
        }

        private static bool ShieldBatteryRestoresExactlyOne()
        {
            Ship player = CreateEmptyPlayerWithShield();
            player.CombatConsumables.ShieldBatteries = 2;
            player.Shields.AbsorbDamage(40f);
            float beforeShield = player.Shields.CurrentShields;
            float beforeHull = player.Hull.CurrentHull;
            bool used = player.TryUseShieldBattery(out _);
            return used && Nearly(player.Shields.CurrentShields, beforeShield + ShieldBatteries.RestorationAmount) &&
                   Nearly(player.Hull.CurrentHull, beforeHull) &&
                   player.CombatConsumables.ShieldBatteries == 1;
        }

        private static bool ShieldBatteryClampsWithoutHullChange()
        {
            Ship player = CreateEmptyPlayerWithShield();
            player.CombatConsumables.ShieldBatteries = 1;
            player.Shields.AbsorbDamage(25f);
            float beforeHull = player.Hull.CurrentHull;
            bool used = player.TryUseShieldBattery(out _);
            return used && Nearly(player.Shields.CurrentShields, player.Shields.MaxShields) &&
                   Nearly(player.Hull.CurrentHull, beforeHull) && player.CombatConsumables.ShieldBatteries == 0;
        }

        private static bool FullShieldConsumesNothing()
        {
            Ship player = CreateEmptyPlayerWithShield();
            player.CombatConsumables.ShieldBatteries = 1;
            bool used = player.TryUseShieldBattery(out _);
            return !used && player.CombatConsumables.ShieldBatteries == 1 &&
                   Nearly(player.Shields.CurrentShields, player.Shields.MaxShields);
        }

        private static bool BatteryWithoutShieldConsumesNothing()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.ShieldBatteries = 1;
            bool used = player.TryUseShieldBattery(out _);
            return !used && player.CombatConsumables.ShieldBatteries == 1 &&
                   !player.Shields.HasMountedShield;
        }

        private static bool HeldInputConsumesOnce()
        {
            Ship player = CreateEmptyPlayer();
            player.SetNotificationManager(new NotificationManager(null, new Viewport(0, 0, 1280, 720)));
            player.CombatConsumables.Nanobots = 2;
            player.Hull.TakeDamage(40f);
            player.Update(Frame(0.01f), new KeyboardState());
            player.Update(Frame(0.01f), new KeyboardState(Keys.Y));
            int afterPress = player.CombatConsumables.Nanobots;
            player.Update(Frame(0.01f), new KeyboardState(Keys.Y));
            return afterPress == 1 && player.CombatConsumables.Nanobots == afterPress;
        }

        private static bool DealerBuysNanobots()
        {
            Ship player = CreateEmptyPlayer();
            EquipmentDealer dealer = new();
            PlayerCredits credits = new(Nanobots.Price + 10);
            bool bought = dealer.TryBuyEquipment(Nanobots, credits, player, out _);
            return bought && player.CombatConsumables.Nanobots == 1 && credits.Credits == 10;
        }

        private static bool DealerSellsNanobots()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.Nanobots = 2;
            EquipmentDealer dealer = new();
            PlayerCredits credits = new(0);
            bool sold = dealer.TrySellUnequippedEquipment(Nanobots, credits, player, out _);
            return sold && player.CombatConsumables.Nanobots == 1 &&
                   credits.Credits == dealer.GetResaleValue(Nanobots);
        }

        private static bool DealerBuysShieldBatteries()
        {
            Ship player = CreateEmptyPlayer();
            EquipmentDealer dealer = new();
            PlayerCredits credits = new(ShieldBatteries.Price + 10);
            bool bought = dealer.TryBuyEquipment(ShieldBatteries, credits, player, out _);
            return bought && player.CombatConsumables.ShieldBatteries == 1 && credits.Credits == 10;
        }

        private static bool DealerSellsShieldBatteries()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.ShieldBatteries = 2;
            EquipmentDealer dealer = new();
            PlayerCredits credits = new(0);
            bool sold = dealer.TrySellUnequippedEquipment(ShieldBatteries, credits, player, out _);
            return sold && player.CombatConsumables.ShieldBatteries == 1 &&
                   credits.Credits == dealer.GetResaleValue(ShieldBatteries);
        }

        private static bool CarryAndCreditLimitsAreSafe()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.Nanobots = int.MaxValue;
            player.CombatConsumables.ShieldBatteries = -100;
            EquipmentDealer dealer = new();
            bool fullRejected = !dealer.TryBuyEquipment(Nanobots, new PlayerCredits(Nanobots.Price), player, out _);
            bool insufficientRejected = !dealer.TryBuyEquipment(ShieldBatteries, new PlayerCredits(0), player, out _);
            return player.CombatConsumables.Nanobots == CombatConsumableInventory.MaximumNanobots &&
                   player.CombatConsumables.ShieldBatteries == 0 && fullRejected && insufficientRejected;
        }

        private static bool NpcSuppliesAreDeterministic()
        {
            ShipLoadout first = NpcEquipmentLoadoutFactory.CreateForNpc(
                "Phase43 Navy Heavy", FactionManager.LibertyNavy, "SHIPS/WARTHOG/warthog",
                TrafficZoneBehaviorType.PirateAmbush, NpcLoadoutTier.High);
            ShipLoadout second = NpcEquipmentLoadoutFactory.CreateForNpc(
                "Phase43 Navy Heavy", FactionManager.LibertyNavy, "SHIPS/WARTHOG/warthog",
                TrafficZoneBehaviorType.PirateAmbush, NpcLoadoutTier.High);
            return first.GetOwnedCount(CombatConsumableIds.Nanobots) == second.GetOwnedCount(CombatConsumableIds.Nanobots) &&
                   first.GetOwnedCount(CombatConsumableIds.ShieldBatteries) == second.GetOwnedCount(CombatConsumableIds.ShieldBatteries);
        }

        private static bool NpcSuppliesAreBounded()
        {
            ShipLoadout[] loadouts =
            {
                NpcEquipmentLoadoutFactory.CreateForNpc("Civilian", FactionManager.NeutralCivilians),
                NpcEquipmentLoadoutFactory.CreateForNpc("Police Fighter", FactionManager.LibertyPolice),
                NpcEquipmentLoadoutFactory.CreateForNpc("Navy Warthog Heavy", FactionManager.LibertyNavy, "SHIPS/WARTHOG/warthog", TrafficZoneBehaviorType.PirateAmbush, NpcLoadoutTier.High),
                NpcEquipmentLoadoutFactory.CreateForNpc("Rogue Fighter", FactionManager.LibertyRogues, "fighter", TrafficZoneBehaviorType.PirateAmbush)
            };
            return loadouts.All(loadout => loadout.GetOwnedCount(CombatConsumableIds.Nanobots) is >= 0 and <= CombatConsumableInventory.MaximumNanobots &&
                                           loadout.GetOwnedCount(CombatConsumableIds.ShieldBatteries) is >= 0 and <= CombatConsumableInventory.MaximumShieldBatteries);
        }

        private static bool NpcConsumptionReducesSupply()
        {
            NpcShip npc = CreateNpcWithManualSupplies("Phase43 NPC use");
            npc.Hull.TakeDamage(55f);
            int before = npc.Loadout.GetOwnedCount(CombatConsumableIds.Nanobots);
            bool used = CombatConsumableService.TryUseNpcConsumable(npc, out CombatConsumableType usedType);
            return used && usedType == CombatConsumableType.Nanobots &&
                   npc.Loadout.GetOwnedCount(CombatConsumableIds.Nanobots) == before - 1;
        }

        private static bool NpcCooldownPreventsRepeatUse()
        {
            NpcShip npc = CreateNpcWithManualSupplies("Phase43 NPC cooldown");
            npc.Hull.TakeDamage(55f);
            bool first = CombatConsumableService.TryUseNpcConsumable(npc, out _);
            int afterFirst = npc.Loadout.GetOwnedCount(CombatConsumableIds.Nanobots);
            bool second = CombatConsumableService.TryUseNpcConsumable(npc, out _);
            return first && !second && npc.Loadout.GetOwnedCount(CombatConsumableIds.Nanobots) == afterFirst &&
                   npc.CombatConsumableCooldownRemaining > 0f;
        }

        private static bool ConsumableSalvageIsBoundedBySource()
        {
            CombatSalvageService service = new();
            NpcShip source = FindConsumableSalvageSource(service);
            if (source == null) return false;
            int nanobots = source.Loadout.GetOwnedCount(CombatConsumableIds.Nanobots);
            int batteries = source.Loadout.GetOwnedCount(CombatConsumableIds.ShieldBatteries);
            source.ApplyDamage(source.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
            return service.EvaluateDestruction(source)
                .Where(drop => drop.IsConsumable)
                .All(drop => (drop.ConsumableId == CombatConsumableIds.Nanobots && drop.Quantity <= nanobots) ||
                             (drop.ConsumableId == CombatConsumableIds.ShieldBatteries && drop.Quantity <= batteries));
        }

        private static bool ConsumableSalvageUsesPhysicalPods()
        {
            CombatSalvageService service = new();
            NpcShip source = FindConsumableSalvageSource(service);
            if (source == null) return false;
            source.ApplyDamage(source.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
            LootManager manager = new(null, null, null, null, service);
            int spawned = manager.SpawnLootForDestroyedNpc(source);
            CargoPod pod = manager.ActivePods.FirstOrDefault(active => active.IsConsumable);
            return spawned > 0 && pod != null && pod.GetConsumable() != null &&
                   pod.LifetimeSeconds == (float)CombatSalvageService.SalvageLifetimeSeconds &&
                   pod.PickupRadius == CombatSalvageService.PickupRadius;
        }

        private static bool ConsumablePickupTransfersQuantity()
        {
            Ship player = CreateEmptyPlayer();
            LootManager manager = new();
            if (!CargoPod.TryCreateConsumable(CombatConsumableIds.Nanobots, 3, Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod))
                return false;
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, false);
            return player.CombatConsumables.Nanobots == 3 && manager.ActivePods.Count == 0;
        }

        private static bool ConsumableOverflowLeavesRemainder()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.Nanobots = CombatConsumableInventory.MaximumNanobots - 1;
            LootManager manager = new();
            if (!CargoPod.TryCreateConsumable(CombatConsumableIds.Nanobots, 3, Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod))
                return false;
            AddPod(manager, pod);
            manager.Update(Frame(0.1f), player, false);
            return player.CombatConsumables.Nanobots == CombatConsumableInventory.MaximumNanobots &&
                   manager.ActivePods.Count == 1 && manager.ActivePods[0].Quantity == 2;
        }

        private static bool WeaponSalvageRemainsIndependent()
        {
            CombatSalvageService service = new();
            NpcShip source = CreateNpcWithManualSupplies("Phase43 Weapon salvage");
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_light_laser") as WeaponEquipmentDefinition;
            source.Loadout.AddOwnedEquipment(gun, 1);
            source.Loadout.TryMountEquipment(gun, out _);
            bool expected = service.ShouldDropEquipment(source);
            source.ApplyDamage(source.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
            List<SalvageDrop> weapons = service.EvaluateDestruction(source).Where(drop => drop.IsEquipment).ToList();
            return weapons.All(drop => drop.EquipmentId == gun.Id) && (expected ? weapons.Count > 0 : weapons.Count == 0);
        }

        private static bool ShieldSalvageRemainsIndependent()
        {
            CombatSalvageService service = new();
            NpcShip source = CreateNpcWithManualSupplies("Phase43 Shield salvage");
            ShieldEquipmentDefinition shield = EquipmentCatalog.GetById("civilian_shield_generator") as ShieldEquipmentDefinition;
            source.Loadout.AddOwnedEquipment(shield, 1);
            source.Loadout.TryMountEquipment(shield, out _);
            bool expected = service.ShouldDropShield(source);
            source.ApplyDamage(source.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
            List<SalvageDrop> shields = service.EvaluateDestruction(source)
                .Where(drop => drop.IsEquipment && drop.EquipmentId == shield.Id).ToList();
            return shields.Count <= 1 && (expected ? shields.Count == 1 : shields.Count == 0);
        }

        private static bool SaveLoadPreservesConsumables()
        {
            string path = Path.Combine(Path.GetTempPath(), $"roguelancer_phase43_{Guid.NewGuid():N}.json");
            try
            {
                ShipLoadout source = ShipLoadout.CreateStarterLoadout(false);
                source.CombatConsumables.SetQuantities(17, 23);
                SaveGameManager manager = new(path);
                SaveGameData data = new()
                {
                    Nanobots = source.CombatConsumables.Nanobots,
                    ShieldBatteries = source.CombatConsumables.ShieldBatteries
                };
                bool saved = manager.TrySave(data, out _);
                bool loaded = manager.TryLoad(out SaveGameData restored, out _);
                ShipLoadout rebuilt = manager.BuildLoadout(restored, out _);
                return saved && loaded && rebuilt.CombatConsumables.Nanobots == 17 &&
                       rebuilt.CombatConsumables.ShieldBatteries == 23 && SaveGameData.CurrentSchemaVersion == 10;
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static bool OlderSavesDefaultConsumables()
        {
            ShipLoadout rebuilt = new SaveGameManager().BuildLoadout(
                new SaveGameData { SchemaVersion = SaveGameData.CurrentSchemaVersion - 1 }, out _);
            return rebuilt.CombatConsumables.Nanobots == 0 && rebuilt.CombatConsumables.ShieldBatteries == 0;
        }

        private static bool MalformedQuantitiesClamp()
        {
            ShipLoadout rebuilt = new SaveGameManager().BuildLoadout(
                new SaveGameData { Nanobots = int.MaxValue, ShieldBatteries = int.MinValue }, out _);
            return rebuilt.CombatConsumables.Nanobots == CombatConsumableInventory.MaximumNanobots &&
                   rebuilt.CombatConsumables.ShieldBatteries == 0;
        }

        private static bool ResetClearsNpcTransientState()
        {
            NpcShip npc = CreateNpcWithManualSupplies("Phase43 reset");
            npc.Hull.TakeDamage(55f);
            bool used = CombatConsumableService.TryUseNpcConsumable(npc, out _);
            npc.ResetCombatConsumableState();
            return used && npc.CombatConsumableCooldownRemaining == 0f;
        }

        private static bool DestroyedShipCannotConsume()
        {
            Ship player = CreateEmptyPlayer();
            player.CombatConsumables.Nanobots = 1;
            player.Hull.TakeDamage(player.Hull.MaxHull + 1f);
            bool used = player.TryUseNanobot(out _);
            return !used && player.CombatConsumables.Nanobots == 1 && player.Hull.IsDestroyed;
        }

        private static bool InvalidConsumableInputIsSafe()
        {
            CombatConsumableInventory inventory = new();
            bool unknownRejected = !inventory.TryAdd("unknown-consumable", 1) &&
                                   inventory.GetQuantity("unknown-consumable") == 0;
            ConsumableEquipmentDefinition invalid = new()
            {
                Id = "invalid-consumable",
                EquipmentType = EquipmentType.Consumable,
                Price = 1,
                ConsumableType = CombatConsumableType.Nanobots,
                MaximumCarryQuantity = 0,
                RestorationAmount = 0f
            };
            bool invalidRejected = !invalid.IsValid &&
                                   !CargoPod.TryCreateConsumable(invalid.Id, 1, Vector3.Zero, Vector3.Zero, 120f, 200f, out _);
            bool equipmentPathRejected = !CargoPod.TryCreateEquipment(CombatConsumableIds.Nanobots, Vector3.Zero, Vector3.Zero, 120f, 200f, out _);
            inventory.Nanobots = -1;
            inventory.ShieldBatteries = int.MaxValue;
            return unknownRejected && invalidRejected && equipmentPathRejected &&
                   inventory.Nanobots == 0 && inventory.ShieldBatteries == CombatConsumableInventory.MaximumShieldBatteries;
        }

        private static NpcShip FindConsumableSalvageSource(CombatSalvageService service)
        {
            for (int i = 0; i < 10_000; i++)
            {
                NpcShip source = CreateNpcWithManualSupplies($"Phase43 salvage {i}");
                if (service.ShouldDropConsumable(source, CombatConsumableIds.Nanobots) ||
                    service.ShouldDropConsumable(source, CombatConsumableIds.ShieldBatteries))
                {
                    return source;
                }
            }

            return null;
        }

        private static NpcShip CreateNpcWithManualSupplies(string name)
        {
            NpcShip npc = new(name, Vector3.Zero, Vector3.Zero, 1f, 0f, FactionManager.LibertyPolice)
            {
                ModelPath = "fighter"
            };
            npc.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "phase43-smoke", Vector3.Zero, 500f, 100f);
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            loadout.CombatConsumables.SetQuantities(2, 2);
            ShieldEquipmentDefinition shield = EquipmentCatalog.GetById("civilian_shield_generator") as ShieldEquipmentDefinition;
            loadout.AddOwnedEquipment(shield, 1);
            loadout.TryMountEquipment(shield, out _);
            npc.SetLoadout(loadout);
            return npc;
        }

        private static Ship CreateEmptyPlayer()
        {
            Ship player = new(Vector3.Zero);
            player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            return player;
        }

        private static Ship CreateEmptyPlayerWithShield()
        {
            Ship player = CreateEmptyPlayer();
            ShieldEquipmentDefinition shield = EquipmentCatalog.GetById("civilian_shield_generator") as ShieldEquipmentDefinition;
            player.Loadout.AddOwnedEquipment(shield, 1);
            player.Loadout.TryMountEquipment(shield, out _);
            player.RefreshShieldFromLoadout();
            return player;
        }

        private static void AddPod(LootManager manager, CargoPod pod)
        {
            if (manager?.ActivePods is List<CargoPod> pods && pod != null)
            {
                pods.Add(pod);
            }
        }

        private static ConsumableEquipmentDefinition Nanobots =>
            EquipmentCatalog.GetById(CombatConsumableIds.Nanobots) as ConsumableEquipmentDefinition;

        private static ConsumableEquipmentDefinition ShieldBatteries =>
            EquipmentCatalog.GetById(CombatConsumableIds.ShieldBatteries) as ConsumableEquipmentDefinition;

        private static GameTime Frame(float seconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
            return new GameTime(elapsed, elapsed);
        }

        private static bool Nearly(float left, float right) => Math.Abs(left - right) < 0.001f;

        private static T RunSilenced<T>(Func<T> action)
        {
            TextWriter previous = Console.Out;
            using StringWriter sink = new();
            Console.SetOut(sink);
            try { return action(); }
            finally { Console.SetOut(previous); }
        }
    }
}
