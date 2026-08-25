using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 44 coverage for canonical powerplants, bounded weapon
    /// energy, player/NPC firing, dealer ownership, salvage, HUD text, and
    /// schema-10 compatibility.
    /// </summary>
    internal sealed class WeaponEnergySmokeTest
    {
        private readonly GraphicsDevice _graphicsDevice;

        public WeaponEnergySmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            Check("canonical powerplant catalog", CanonicalPowerplantsExist, ref passed, ref failed);
            Check("powerplant metadata is typed and bounded", PowerplantMetadataIsValid, ref passed, ref failed);
            Check("mount compatibility and one-plant rule", MountRulesAreEnforced, ref passed, ref failed);
            Check("new player receives starter plant at full charge", StarterPowerplantIsMounted, ref passed, ref failed);
            Check("gun energy costs are positive and differentiated", GunCostsAreValid, ref passed, ref failed);
            Check("spending is exact and bounded", SpendingIsExactAndBounded, ref passed, ref failed);
            Check("energy regenerates and clamps", RegenerationIsBounded, ref passed, ref failed);
            Check("destroyed player does not regenerate", DestroyedShipDoesNotRegenerate, ref passed, ref failed);
            Check("invalid energy input is safe", InvalidEnergyInputIsSafe, ref passed, ref failed);
            Check("mounting and unmounting rebind runtime", MountChangesRebindRuntime, ref passed, ref failed);
            Check("player firing spends energy and creates projectiles", PlayerFireSpendsEnergy, ref passed, ref failed);
            Check("insufficient player energy blocks projectiles", PlayerFireIsBlockedWhenDepleted, ref passed, ref failed);
            Check("missing plant blocks player weapon energy", MissingPlantBlocksPlayerFire, ref passed, ref failed);
            Check("NPC powerplant assignment is deterministic", NpcPowerplantAssignmentIsDeterministic, ref passed, ref failed);
            Check("standard NPC has valid full energy state", StandardNpcEnergyStateIsValid, ref passed, ref failed);
            Check("NPC fire spends energy", NpcFireSpendsEnergy, ref passed, ref failed);
            Check("depleted NPC cannot fire", DepletedNpcCannotFire, ref passed, ref failed);
            Check("NPC energy regenerates and restores fire", NpcRegeneratesAndFiresAgain, ref passed, ref failed);
            Check("NPC versus NPC fire uses energy", NpcToNpcUsesEnergy, ref passed, ref failed);
            Check("dealer buys, mounts, unmounts, and sells plants", DealerPowerplantTransactions, ref passed, ref failed);
            Check("powerplant salvage preserves actual ID", PowerplantSalvagePreservesActualId, ref passed, ref failed);
            Check("salvage cap and lifetime remain shared", SalvageBoundsRemainShared, ref passed, ref failed);
            Check("powerplant pickup creates a spare", PowerplantPickupCreatesSpare, ref passed, ref failed);
            Check("save/load preserves powerplant ownership", SaveLoadPreservesPowerplants, ref passed, ref failed);
            Check("old save receives deterministic starter fallback", OldSaveGetsStarterFallback, ref passed, ref failed);
            Check("HUD exposes current/max energy", HudTextExposesEnergy, ref passed, ref failed);
            Check("missile and mine definitions remain independent", MissileMinePolicyIsUnchanged, ref passed, ref failed);

            Console.WriteLine($"[WEAPON ENERGY SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private static void Check(
            string label,
            Func<(bool Success, string FailureReason)> test,
            ref int passed,
            ref int failed)
        {
            try
            {
                (bool success, string failureReason) = RunSilenced(test);
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[WEAPON ENERGY SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[WEAPON ENERGY SMOKE] FAIL {label}: {failureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[WEAPON ENERGY SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static (bool Success, string FailureReason) CanonicalPowerplantsExist()
        {
            string[] ids =
            {
                "civilian_powerplant", "liberty_patrol_powerplant", "liberty_military_powerplant",
                "liberty_heavy_powerplant", "rogue_scrap_powerplant", "rogue_combat_powerplant",
                "professional_powerplant"
            };
            return ids.All(id => EquipmentCatalog.GetById(id) is PowerplantEquipmentDefinition)
                ? Pass()
                : Fail("one or more canonical powerplants were missing");
        }

        private static (bool Success, string FailureReason) PowerplantMetadataIsValid()
        {
            IReadOnlyList<EquipmentDefinition> plants = EquipmentCatalog.GetAll()
                .Where(definition => definition?.EquipmentType == EquipmentType.Powerplant)
                .ToList();
            return plants.Count == 7 && plants.All(definition =>
            {
                PowerplantEquipmentDefinition plant = definition as PowerplantEquipmentDefinition;
                return plant?.IsValid == true && plant.Price > 0 && plant.EnergyCapacity > 0f && plant.EnergyRegenerationRate > 0f;
            }) ? Pass() : Fail("powerplant metadata was invalid or incomplete");
        }

        private static (bool Success, string FailureReason) MountRulesAreEnforced()
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_light_laser") as WeaponEquipmentDefinition;
            ShieldEquipmentDefinition shield = EquipmentCatalog.GetById("civilian_shield_generator") as ShieldEquipmentDefinition;
            PowerplantEquipmentDefinition first = EquipmentCatalog.GetById("civilian_powerplant") as PowerplantEquipmentDefinition;
            PowerplantEquipmentDefinition second = EquipmentCatalog.GetById("liberty_patrol_powerplant") as PowerplantEquipmentDefinition;
            loadout.AddOwnedEquipment(gun);
            loadout.AddOwnedEquipment(shield);
            loadout.AddOwnedEquipment(first);
            loadout.AddOwnedEquipment(second);

            bool mounted = loadout.TryMountEquipment("Powerplant", first, out _);
            bool duplicateRejected = !loadout.TryMountEquipment(second, out _);
            bool gunRejected = !loadout.TryMountEquipment("Powerplant", gun, out _);
            bool shieldRejected = !loadout.TryMountEquipment("Powerplant", shield, out _);
            bool reverseRejected = !loadout.TryMountEquipment("PrimaryGunLeft", first, out _);
            return mounted && duplicateRejected && gunRejected && shieldRejected && reverseRejected &&
                   loadout.GetMountedPowerplant()?.Id == first.Id
                ? Pass()
                : Fail("incompatible or duplicate powerplant mount was accepted");
        }

        private static (bool Success, string FailureReason) StarterPowerplantIsMounted()
        {
            Ship ship = new(Vector3.Zero);
            PowerplantEquipmentDefinition plant = ship.GetMountedPowerplant();
            return plant?.Id == "civilian_powerplant" &&
                   ship.Loadout.GetOwnedCount(plant.Id) == 1 &&
                   Nearly(ship.WeaponEnergy.CurrentEnergy, plant.EnergyCapacity) &&
                   Nearly(ship.WeaponEnergy.MaxEnergy, plant.EnergyCapacity)
                ? Pass()
                : Fail("starter ship did not receive a full civilian plant");
        }

        private static (bool Success, string FailureReason) GunCostsAreValid()
        {
            List<WeaponEquipmentDefinition> guns = EquipmentCatalog.GetAll()
                .OfType<WeaponEquipmentDefinition>()
                .Where(gun => gun.EquipmentType == EquipmentType.Gun)
                .ToList();
            return guns.Count >= 9 && guns.All(gun => IsFinitePositive(gun.EnergyCost)) &&
                   guns.Select(gun => gun.EnergyCost).Distinct().Count() >= 4
                ? Pass()
                : Fail("gun energy-cost metadata was not positive and differentiated");
        }

        private static (bool Success, string FailureReason) SpendingIsExactAndBounded()
        {
            PowerplantEquipmentDefinition plant = EquipmentCatalog.GetById("civilian_powerplant") as PowerplantEquipmentDefinition;
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_light_laser") as WeaponEquipmentDefinition;
            WeaponEnergy energy = new(plant);
            float before = energy.CurrentEnergy;
            bool spent = energy.TrySpend(gun.EnergyCost);
            bool exact = Nearly(energy.CurrentEnergy, before - gun.EnergyCost);
            energy.SetCurrentEnergy(float.NaN);
            bool malformed = Nearly(energy.CurrentEnergy, 0f);
            bool rejected = !energy.TrySpend(gun.EnergyCost) && Nearly(energy.CurrentEnergy, 0f);
            energy.SetCurrentEnergy(float.MaxValue);
            return spent && exact && malformed && rejected && energy.CurrentEnergy <= energy.MaxEnergy && energy.CurrentEnergy >= 0f
                ? Pass()
                : Fail("energy spending or malformed charge handling was not bounded");
        }

        private static (bool Success, string FailureReason) RegenerationIsBounded()
        {
            PowerplantEquipmentDefinition plant = EquipmentCatalog.GetById("civilian_powerplant") as PowerplantEquipmentDefinition;
            WeaponEnergy energy = new(plant);
            energy.SetCurrentEnergy(0f);
            energy.Update(Frame(2f));
            bool recovered = Nearly(energy.CurrentEnergy, plant.EnergyRegenerationRate * 2f);
            energy.Update(Frame(1000000f));
            bool clamped = Nearly(energy.CurrentEnergy, plant.EnergyCapacity);
            energy.Update(Frame(-2f));
            return recovered && clamped && energy.CurrentEnergy <= plant.EnergyCapacity ? Pass() : Fail("regeneration did not recover and clamp deterministically");
        }

        private static (bool Success, string FailureReason) InvalidEnergyInputIsSafe()
        {
            WeaponEnergy energy = new();
            PowerplantEquipmentDefinition invalid = new()
            {
                Id = "invalid-powerplant",
                EquipmentType = EquipmentType.Powerplant,
                EnergyCapacity = float.NaN,
                EnergyRegenerationRate = float.PositiveInfinity,
                Price = -1
            };
            energy.Configure(invalid);
            energy.Update(new GameTime(TimeSpan.FromDays(100000), TimeSpan.FromDays(100000)));
            return !energy.HasMountedPowerplant && energy.MaxEnergy == 0f && energy.CurrentEnergy == 0f && !energy.TrySpend(-1f)
                ? Pass()
                : Fail("invalid powerplant or spend input contaminated runtime state");
        }

        private static (bool Success, string FailureReason) DestroyedShipDoesNotRegenerate()
        {
            Ship ship = new(Vector3.Zero);
            ship.WeaponEnergy.SetCurrentEnergy(0f);
            ship.Hull.TakeDamage(ship.Hull.MaxHull);
            ship.UpdateWeaponEnergy(Frame(60f));
            return ship.Hull.IsDestroyed && ship.WeaponEnergy.CurrentEnergy == 0f
                ? Pass()
                : Fail("destroyed player regenerated weapon energy");
        }

        private static (bool Success, string FailureReason) MountChangesRebindRuntime()
        {
            Ship ship = new(Vector3.Zero);
            PowerplantEquipmentDefinition patrol = EquipmentCatalog.GetById("liberty_patrol_powerplant") as PowerplantEquipmentDefinition;
            PowerplantEquipmentDefinition civilian = ship.GetMountedPowerplant();
            ship.Loadout.AddOwnedEquipment(patrol);
            if (!ship.Loadout.TryUnmountEquipment(civilian.Id, out _) ||
                !ship.Loadout.TryMountEquipment(patrol, out _))
            {
                return Fail("could not switch mounted powerplant");
            }

            ship.RefreshWeaponEnergyFromLoadout();
            bool switched = ship.GetMountedPowerplant()?.Id == patrol.Id &&
                            Nearly(ship.WeaponEnergy.MaxEnergy, patrol.EnergyCapacity) &&
                            Nearly(ship.WeaponEnergy.CurrentEnergy, patrol.EnergyCapacity);
            if (!ship.Loadout.TryUnmountEquipment(patrol.Id, out _))
            {
                return Fail("could not unmount active powerplant");
            }

            ship.RefreshWeaponEnergyFromLoadout();
            return switched && ship.GetMountedPowerplant() == null && ship.WeaponEnergy.CurrentEnergy == 0f
                ? Pass()
                : Fail("unmount did not clear usable energy");
        }

        private (bool Success, string FailureReason) PlayerFireSpendsEnergy()
        {
            Ship player = new(Vector3.Zero);
            WeaponEquipmentDefinition gun = player.GetPrimaryMountedGun();
            WeaponSystem system = CreateWeaponSystem(gun, player.WeaponEnergy);
            float before = player.WeaponEnergy.CurrentEnergy;
            system.Fire(Vector3.Zero, Vector3.Right, Vector3.Zero);
            system.Update(Frame(0.05f));
            List<HitInfo> hits = system.CheckCollisions(new Vector3(gun.ProjectileSpeed * 0.05f, 0f, 0f), 100f, new HullIntegrity(100f));
            return hits.Count == 2 && Nearly(player.WeaponEnergy.CurrentEnergy, before - gun.EnergyCost)
                ? Pass()
                : Fail("successful player fire did not spend one exact shot cost");
        }

        private (bool Success, string FailureReason) PlayerFireIsBlockedWhenDepleted()
        {
            Ship player = new(Vector3.Zero);
            WeaponEquipmentDefinition gun = player.GetPrimaryMountedGun();
            player.WeaponEnergy.SetCurrentEnergy(gun.EnergyCost - 0.01f);
            WeaponSystem system = CreateWeaponSystem(gun, player.WeaponEnergy);
            float before = player.WeaponEnergy.CurrentEnergy;
            system.Fire(Vector3.Zero, Vector3.Right, Vector3.Zero);
            system.Update(Frame(0.05f));
            List<HitInfo> hits = system.CheckCollisions(new Vector3(gun.ProjectileSpeed * 0.05f, 0f, 0f), 100f, new HullIntegrity(100f));
            return hits.Count == 0 && Nearly(player.WeaponEnergy.CurrentEnergy, before)
                ? Pass()
                : Fail("insufficient player energy created a projectile or consumed charge");
        }

        private (bool Success, string FailureReason) MissingPlantBlocksPlayerFire()
        {
            Ship player = new(Vector3.Zero);
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_light_laser") as WeaponEquipmentDefinition;
            loadout.AddOwnedEquipment(gun);
            loadout.TryMountEquipment("PrimaryGunLeft", gun, out _);
            player.SetLoadout(loadout);
            WeaponSystem system = CreateWeaponSystem(gun, player.WeaponEnergy);
            system.Fire(Vector3.Zero, Vector3.Right, Vector3.Zero);
            system.Update(Frame(0.05f));
            return player.WeaponEnergy.MaxEnergy == 0f &&
                   system.CheckCollisions(new Vector3(gun.ProjectileSpeed * 0.05f, 0f, 0f), 100f, new HullIntegrity(100f)).Count == 0
                ? Pass()
                : Fail("a ship without a powerplant fired a gun");
        }

        private static (bool Success, string FailureReason) NpcPowerplantAssignmentIsDeterministic()
        {
            ShipLoadout first = NpcEquipmentLoadoutFactory.CreateForNpc("Police Fighter", FactionManager.LibertyPolice, "SMOKE/fighter", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Standard);
            ShipLoadout second = NpcEquipmentLoadoutFactory.CreateForNpc("Police Fighter", FactionManager.LibertyPolice, "SMOKE/fighter", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Standard);
            ShipLoadout rogueLow = NpcEquipmentLoadoutFactory.CreateForNpc("Rogue Fighter", FactionManager.LibertyRogues, "SMOKE/fighter", TrafficZoneBehaviorType.PirateAmbush, NpcLoadoutTier.Low);
            return first.GetMountedPowerplant()?.Id == "liberty_patrol_powerplant" &&
                   first.GetMountedPowerplant()?.Id == second.GetMountedPowerplant()?.Id &&
                   rogueLow.GetMountedPowerplant()?.Id == "rogue_scrap_powerplant"
                ? Pass()
                : Fail("NPC powerplant selection was not deterministic or faction-appropriate");
        }

        private static (bool Success, string FailureReason) StandardNpcEnergyStateIsValid()
        {
            NpcShip npc = CreateCombatNpc("Standard Energy Fighter", FactionManager.LibertyPolice, Vector3.Zero);
            PowerplantEquipmentDefinition plant = NpcEquipmentLoadoutFactory.GetNpcPowerplant(npc.Loadout);
            return plant != null && npc.WeaponEnergy.HasMountedPowerplant &&
                   Nearly(npc.WeaponEnergy.MaxEnergy, plant.EnergyCapacity) &&
                   Nearly(npc.WeaponEnergy.CurrentEnergy, plant.EnergyCapacity)
                ? Pass()
                : Fail("standard NPC did not receive a valid full weapon-energy state");
        }

        private (bool Success, string FailureReason) NpcFireSpendsEnergy()
        {
            NpcShip attacker = CreateCombatNpc("Energy Rogue Fighter", FactionManager.LibertyRogues, Vector3.Zero);
            NpcShip target = CreateCombatNpc("Energy Police Fighter", FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
            attacker.SetFactionCombatTarget(target);
            int fired = 0;
            NpcWeaponSystem system = new(_graphicsDevice);
            system.NpcWeaponFired += (source, _, _, _) => { if (source == attacker) fired++; };
            float before = attacker.WeaponEnergy.CurrentEnergy;
            system.Update(Frame(0.1f), new List<NpcShip> { attacker, target }, new Ship(new Vector3(100000f, 0f, 0f)));
            float expectedSpend = attacker.Loadout.GetMountedGuns().Take(fired).Sum(gun => gun.EnergyCost);
            return fired > 0 && Nearly(attacker.WeaponEnergy.CurrentEnergy, before - expectedSpend)
                ? Pass()
                : Fail("NPC projectile fire did not spend mounted-gun energy");
        }

        private (bool Success, string FailureReason) DepletedNpcCannotFire()
        {
            NpcShip attacker = CreateCombatNpc("Energy Depleted Rogue Fighter", FactionManager.LibertyRogues, Vector3.Zero);
            NpcShip target = CreateCombatNpc("Energy Depleted Police Fighter", FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
            attacker.SetFactionCombatTarget(target);
            attacker.WeaponEnergy.SetCurrentEnergy(0f);
            int fired = 0;
            NpcWeaponSystem system = new(_graphicsDevice);
            system.NpcWeaponFired += (source, _, _, _) => { if (source == attacker) fired++; };
            system.Update(Frame(0.1f), new List<NpcShip> { attacker, target }, new Ship(new Vector3(100000f, 0f, 0f)));
            return fired == 0 && system.ActiveProjectileCount == 0 && attacker.WeaponEnergy.CurrentEnergy == 0f
                ? Pass()
                : Fail("depleted NPC fired or created a projectile");
        }

        private (bool Success, string FailureReason) NpcRegeneratesAndFiresAgain()
        {
            NpcShip attacker = CreateCombatNpc("Energy Recovery Rogue Fighter", FactionManager.LibertyRogues, Vector3.Zero);
            NpcShip target = CreateCombatNpc("Energy Recovery Police Fighter", FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
            attacker.SetFactionCombatTarget(target);
            attacker.WeaponEnergy.SetCurrentEnergy(0f);
            attacker.Update(Frame(2f), null, new Ship(new Vector3(100000f, 0f, 0f)));
            int fired = 0;
            NpcWeaponSystem system = new(_graphicsDevice);
            system.NpcWeaponFired += (source, _, _, _) => { if (source == attacker) fired++; };
            system.Update(Frame(10f), new List<NpcShip> { attacker, target }, new Ship(new Vector3(100000f, 0f, 0f)));
            return attacker.WeaponEnergy.CurrentEnergy > 0f && fired > 0
                ? Pass()
                : Fail("NPC energy did not regenerate into a later valid firing decision");
        }

        private (bool Success, string FailureReason) NpcToNpcUsesEnergy()
        {
            NpcShip attacker = CreateCombatNpc("Energy Npc Fighter A", FactionManager.LibertyPolice, Vector3.Zero);
            NpcShip target = CreateCombatNpc("Energy Npc Fighter B", FactionManager.LibertyRogues, new Vector3(300f, 0f, 0f));
            attacker.SetFactionCombatTarget(target);
            float before = attacker.WeaponEnergy.CurrentEnergy;
            NpcWeaponSystem system = new(_graphicsDevice);
            int fired = 0;
            system.NpcWeaponFired += (source, victim, _, _) => { if (source == attacker && victim == target) fired++; };
            system.Update(Frame(0.1f), new List<NpcShip> { attacker, target }, new Ship(new Vector3(100000f, 0f, 0f)));
            return fired > 0 && attacker.WeaponEnergy.CurrentEnergy < before ? Pass() : Fail("NPC-to-NPC firing bypassed weapon energy");
        }

        private static (bool Success, string FailureReason) DealerPowerplantTransactions()
        {
            EquipmentDealer dealer = new();
            Ship ship = new(Vector3.Zero);
            PowerplantEquipmentDefinition plant = EquipmentCatalog.GetById("liberty_patrol_powerplant") as PowerplantEquipmentDefinition;
            PlayerCredits credits = new(plant.Price * 2);
            if (!dealer.TryBuyEquipment(plant, credits, ship, out _) || ship.Loadout.GetOwnedCount(plant.Id) != 1)
                return Fail("dealer did not sell a spare powerplant");
            if (!dealer.TryUnmountEquipment(ship.GetMountedPowerplant(), ship, out _))
                return Fail("dealer did not unmount the starter plant");
            if (!dealer.TryMountEquipment(plant, ship, out _) || ship.GetMountedPowerplant()?.Id != plant.Id)
                return Fail("dealer did not mount the purchased plant");
            bool activeSaleRejected = !dealer.TrySellUnequippedEquipment(plant, credits, ship, out _);
            if (!dealer.TryUnmountEquipment(plant, ship, out _) || !dealer.TrySellUnequippedEquipment(plant, credits, ship, out _))
                return Fail("dealer did not unmount and sell the spare plant");
            return activeSaleRejected && ship.GetMountedPowerplant() == null && credits.Credits > 0
                ? Pass()
                : Fail("powerplant dealer transaction state was not atomic");
        }

        private static (bool Success, string FailureReason) PowerplantSalvagePreservesActualId()
        {
            CombatSalvageService service = new();
            NpcShip selected = null;
            IReadOnlyList<SalvageDrop> drops = Array.Empty<SalvageDrop>();
            for (int i = 0; i < 10000; i++)
            {
                NpcShip candidate = CreateTrader($"Powerplant Salvage Trader {i}");
                if (!service.ShouldDropPowerplant(candidate))
                    continue;
                candidate.ApplyDamage(candidate.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
                drops = service.EvaluateDestruction(candidate);
                if (drops.Any(drop => drop.IsEquipment && drop.EquipmentId == candidate.Loadout.GetMountedPowerplant()?.Id))
                {
                    selected = candidate;
                    break;
                }
            }

            return selected != null && drops.Count(drop => drop.IsEquipment && drop.EquipmentId == selected.Loadout.GetMountedPowerplant()?.Id) == 1
                ? Pass()
                : Fail("no deterministic actual-ID powerplant salvage case was found");
        }

        private static (bool Success, string FailureReason) SalvageBoundsRemainShared()
        {
            return CombatSalvageService.MaxLiveSalvageObjects == 32 &&
                   Nearly((float)CombatSalvageService.SalvageLifetimeSeconds, 120f) &&
                   CombatSalvageService.StandardMaximumEquipmentObjectsPerDestruction == 1 &&
                   CombatSalvageService.HeavyMaximumEquipmentObjectsPerDestruction == 2
                ? Pass()
                : Fail("powerplant salvage did not use the existing bounded salvage policy");
        }

        private static (bool Success, string FailureReason) PowerplantPickupCreatesSpare()
        {
            CombatSalvageService service = new();
            NpcShip source = null;
            for (int i = 0; i < 10000; i++)
            {
                NpcShip candidate = CreateTrader($"Powerplant Pickup Trader {i}");
                if (!service.ShouldDropPowerplant(candidate))
                    continue;
                candidate.ApplyDamage(candidate.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
                IReadOnlyList<SalvageDrop> drops = service.EvaluateDestruction(candidate);
                if (drops.Any(drop => drop.IsEquipment && drop.EquipmentId == candidate.Loadout.GetMountedPowerplant()?.Id))
                {
                    source = candidate;
                    break;
                }
            }

            if (source == null)
                return Fail("no deterministic powerplant pickup case was found");
            // Use a fresh policy instance for the physical spawn; the probe
            // above intentionally already remembered its candidate death.
            LootManager manager = new(null, null, null, null, new CombatSalvageService());
            manager.SpawnLootForDestroyedNpc(source);
            CargoPod pod = manager.ActivePods.FirstOrDefault(active => active.IsEquipment && active.EquipmentId == source.Loadout.GetMountedPowerplant()?.Id);
            if (pod == null)
                return Fail("powerplant salvage did not create a physical equipment pod");
            Ship player = new(Vector3.Zero);
            string mountedBefore = player.GetMountedPowerplant()?.Id;
            player.Position = pod.Position;
            manager.Update(Frame(0.1f), player, false);
            return player.Loadout.GetOwnedCount(pod.EquipmentId) > 1 &&
                   player.GetMountedPowerplant()?.Id == mountedBefore
                ? Pass()
                : Fail("powerplant pickup did not create a spare without auto-mounting");
        }

        private static (bool Success, string FailureReason) SaveLoadPreservesPowerplants()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Roguelancer_WeaponEnergy_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "energy.json");
            try
            {
                ShipLoadout source = ShipLoadout.CreateStarterLoadout(false);
                PowerplantEquipmentDefinition civilian = EquipmentCatalog.GetById("civilian_powerplant") as PowerplantEquipmentDefinition;
                PowerplantEquipmentDefinition patrol = EquipmentCatalog.GetById("liberty_patrol_powerplant") as PowerplantEquipmentDefinition;
                source.AddOwnedEquipment(civilian);
                source.AddOwnedEquipment(patrol);
                source.TryMountEquipment(patrol, out _);
                SaveGameManager manager = new(path);
                SaveGameData data = new()
                {
                    OwnedEquipment = manager.CaptureOwnedEquipment(source),
                    MountedEquipment = manager.CaptureMountedEquipment(source)
                };
                if (!manager.TrySave(data, out _) || !manager.TryLoad(out SaveGameData loaded, out _))
                    return Fail("save/load transaction failed");
                ShipLoadout restored = manager.BuildLoadout(loaded, out List<string> warnings);
                Ship ship = new(Vector3.Zero);
                ship.SetLoadout(restored);
                return loaded.SchemaVersion == SaveGameData.CurrentSchemaVersion && warnings.Count == 0 &&
                       restored.GetMountedPowerplant()?.Id == patrol.Id && restored.GetOwnedCount(civilian.Id) == 1 &&
                       restored.GetOwnedCount(patrol.Id) == 1 && Nearly(ship.WeaponEnergy.CurrentEnergy, patrol.EnergyCapacity)
                    ? Pass()
                    : Fail($"powerplant save/load state was not preserved: {string.Join("; ", warnings)}");
            }
            finally
            {
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
            }
        }

        private static (bool Success, string FailureReason) OldSaveGetsStarterFallback()
        {
            ShipLoadout restored = new SaveGameManager().BuildLoadout(
                new SaveGameData { SchemaVersion = SaveGameData.CurrentSchemaVersion - 1 }, out List<string> warnings);
            return restored.GetMountedPowerplant()?.Id == "civilian_powerplant" &&
                   restored.GetOwnedCount("civilian_powerplant") == 1 && warnings.Count == 0
                ? Pass()
                : Fail("old save did not receive the deterministic starter plant");
        }

        private static (bool Success, string FailureReason) HudTextExposesEnergy()
        {
            Ship ship = new(Vector3.Zero);
            string text = ship.WeaponEnergy.GetHudText();
            return text == $"ENERGY {ship.WeaponEnergy.CurrentEnergy:F0}/{ship.WeaponEnergy.MaxEnergy:F0}"
                ? Pass()
                : Fail("weapon energy HUD text did not expose current/max values");
        }

        private static (bool Success, string FailureReason) MissileMinePolicyIsUnchanged()
        {
            EquipmentDefinition missile = EquipmentCatalog.GetById("basic_missile_launcher");
            EquipmentDefinition mine = EquipmentCatalog.GetById("basic_mine_dropper");
            return missile?.EquipmentType == EquipmentType.MissileLauncher && missile.MissileAmmoCost == 1f &&
                   mine?.EquipmentType == EquipmentType.MineDropper && mine.MineCooldown > 0f
                ? Pass()
                : Fail("missile or mine catalog policy was altered by weapon energy");
        }

        private WeaponSystem CreateWeaponSystem(WeaponEquipmentDefinition weapon, WeaponEnergy energy)
        {
            WeaponSystem system = new(_graphicsDevice) { CurrentWeapon = weapon.WeaponType };
            WeaponSystem.WeaponStats defaults = system.GetWeaponStats(weapon.WeaponType);
            system.SetWeaponProfileOverride(weapon.WeaponType, new WeaponSystem.WeaponStats
            {
                WeaponDamage = weapon.Damage,
                Speed = weapon.ProjectileSpeed,
                Range = weapon.Range,
                Life = weapon.Range / weapon.ProjectileSpeed,
                Size = defaults.Size,
                Color = defaults.Color,
                MuzzleFlashSize = defaults.MuzzleFlashSize,
                RefireRate = weapon.RefireRate,
                EnergyCost = weapon.EnergyCost
            });
            system.SetWeaponEnergySystem(energy);
            return system;
        }

        private static NpcShip CreateCombatNpc(string name, string factionId, Vector3 position)
        {
            NpcShip npc = new(name, position, position, 1000f, 0f, factionId);
            npc.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "weapon-energy-smoke", position, 1000f, 120f, 20000f);
            return npc;
        }

        private static NpcShip CreateTrader(string name)
        {
            NpcShip npc = new(name, Vector3.Zero, Vector3.Zero, 1000f, 0f, FactionManager.NeutralCivilians);
            npc.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "weapon-energy-salvage", Vector3.Zero, 1000f, 120f, 20000f);
            return npc;
        }

        private static void Destroy(NpcShip ship)
        {
            ship.ApplyDamage(ship.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
        }

        private static GameTime Frame(float seconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
            return new GameTime(elapsed, elapsed);
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.001f;
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
    }
}
