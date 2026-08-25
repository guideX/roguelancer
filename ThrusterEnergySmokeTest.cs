using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 45 coverage for canonical thrusters, bounded afterburn,
    /// NPC enforcement, dealer/salvage ownership, schema-10 recovery, and
    /// independence from weapon energy.
    /// </summary>
    internal sealed class ThrusterEnergySmokeTest
    {
        public ThrusterEnergySmokeTest(GraphicsDevice graphicsDevice)
        {
            _ = graphicsDevice;
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            Check("canonical thrusters exist", CanonicalThrustersExist, ref passed, ref failed);
            Check("thruster metadata is valid", ThrusterMetadataIsValid, ref passed, ref failed);
            Check("thruster balance is finite", ThrusterBalanceIsFinite, ref passed, ref failed);
            Check("thruster mounts only in thruster slot", ThrusterMountRules, ref passed, ref failed);
            Check("at most one thruster mounts", DuplicateThrusterIsRejected, ref passed, ref failed);
            Check("new player receives civilian starter", StarterThrusterIsMounted, ref passed, ref failed);
            Check("starter runtime initializes full", StarterRuntimeIsFull, ref passed, ref failed);
            Check("normal flight does not consume thruster energy", NormalFlightDoesNotConsume, ref passed, ref failed);
            Check("afterburn drains exact elapsed cost", AfterburnDrainsExactCost, ref passed, ref failed);
            Check("afterburn drain scales with elapsed time", AfterburnDrainScalesWithElapsedTime, ref passed, ref failed);
            Check("afterburn energy clamps at zero", DrainClampsAtZero, ref passed, ref failed);
            Check("zero charge disables afterburn", ZeroChargeDisablesAfterburn, ref passed, ref failed);
            Check("holding at zero cannot create negative charge", HoldingAtZeroRegeneratesSafely, ref passed, ref failed);
            Check("releasing afterburn regenerates", ReleaseRegenerates, ref passed, ref failed);
            Check("regeneration clamps at maximum", RegenerationClampsAtMaximum, ref passed, ref failed);
            Check("missing thruster is unusable", MissingThrusterIsSafe, ref passed, ref failed);
            Check("destroyed ship does not regenerate", DestroyedShipDoesNotRegenerate, ref passed, ref failed);
            Check("unmount clears player runtime", UnmountClearsRuntime, ref passed, ref failed);
            Check("changing thruster rebinds runtime", ChangingThrusterRebindsRuntime, ref passed, ref failed);
            Check("dealer buys and mounts thruster", DealerBuysAndMountsThruster, ref passed, ref failed);
            Check("dealer rejects incompatible thruster mounts", DealerRejectsIncompatibleMount, ref passed, ref failed);
            Check("dealer sells spare thruster", DealerSellsSpareThruster, ref passed, ref failed);
            Check("NPC thrusters are faction appropriate", NpcThrusterAssignmentIsAppropriate, ref passed, ref failed);
            Check("NPC thruster assignment is deterministic", NpcThrusterAssignmentIsDeterministic, ref passed, ref failed);
            Check("NPC starts with bounded charge", NpcRuntimeStartsFull, ref passed, ref failed);
            Check("NPC afterburn consumes charge", NpcAfterburnConsumesCharge, ref passed, ref failed);
            Check("depleted NPC afterburn stops", DepletedNpcCannotAfterburn, ref passed, ref failed);
            Check("NPC charge regenerates", NpcChargeRegenerates, ref passed, ref failed);
            Check("afterburn leaves weapon energy unchanged", AfterburnLeavesWeaponEnergyUnchanged, ref passed, ref failed);
            Check("gun energy leaves thruster energy unchanged", GunfireLeavesThrusterEnergyUnchanged, ref passed, ref failed);
            Check("zero weapon energy does not block afterburn", ZeroWeaponEnergyStillAllowsAfterburn, ref passed, ref failed);
            Check("zero thruster energy does not block gun energy", ZeroThrusterEnergyStillAllowsGunfire, ref passed, ref failed);
            Check("thruster salvage preserves actual ID", ThrusterSalvagePreservesActualId, ref passed, ref failed);
            Check("thruster salvage uses shared bounds", ThrusterSalvageUsesSharedBounds, ref passed, ref failed);
            Check("thruster pod is canonical", ThrusterPodIsCanonical, ref passed, ref failed);
            Check("thruster pickup creates spare", ThrusterPickupCreatesSpare, ref passed, ref failed);
            Check("thruster pickup does not auto-mount", ThrusterPickupDoesNotAutoMount, ref passed, ref failed);
            Check("save/load preserves mounted thruster", SaveLoadPreservesMountedThruster, ref passed, ref failed);
            Check("save/load preserves spare thrusters", SaveLoadPreservesSpareThrusters, ref passed, ref failed);
            Check("loaded runtime restores full charge", LoadedRuntimeRestoresFull, ref passed, ref failed);
            Check("old save receives one civilian fallback", OldSaveGetsThrusterFallback, ref passed, ref failed);
            Check("malformed thruster values are safe", MalformedThrusterStateIsSafe, ref passed, ref failed);
            Check("HUD exposes thruster current/max", HudTextExposesThruster, ref passed, ref failed);
            Check("starter boost duration is finite", StarterDurationIsFinite, ref passed, ref failed);
            Check("starter recharge duration is finite", StarterRechargeDurationIsFinite, ref passed, ref failed);
            Check("thruster equipment is accepted by salvage pods", ThrusterEquipmentPodIsAccepted, ref passed, ref failed);
            Check("legacy light thruster remains loadable", LegacyLightThrusterRemainsLoadable, ref passed, ref failed);
            Check("thruster state resets full", ThrusterResetRestoresFull, ref passed, ref failed);
            Check("invalid thruster definition is rejected", InvalidDefinitionIsRejected, ref passed, ref failed);
            Check("thruster slot survives hardpoint reconfiguration", ThrusterSurvivesHardpointReconfiguration, ref passed, ref failed);

            Console.WriteLine($"[THRUSTER ENERGY SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[THRUSTER ENERGY SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[THRUSTER ENERGY SMOKE] FAIL {label}: {failureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[THRUSTER ENERGY SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static (bool Success, string FailureReason) CanonicalThrustersExist()
        {
            string[] ids =
            {
                "civilian_thruster", "liberty_patrol_thruster", "liberty_military_thruster",
                "liberty_heavy_thruster", "rogue_scrap_thruster", "rogue_combat_thruster",
                "professional_thruster"
            };
            return ids.All(id => EquipmentCatalog.GetById(id) is ThrusterEquipmentDefinition)
                ? Pass()
                : Fail("one or more canonical thrusters were missing");
        }

        private static (bool Success, string FailureReason) ThrusterMetadataIsValid()
        {
            string[] canonicalIds =
            {
                "civilian_thruster", "liberty_patrol_thruster", "liberty_military_thruster",
                "liberty_heavy_thruster", "rogue_scrap_thruster", "rogue_combat_thruster",
                "professional_thruster"
            };
            return canonicalIds.Select(EquipmentCatalog.GetById).OfType<ThrusterEquipmentDefinition>().Count() == canonicalIds.Length &&
                   canonicalIds.Select(EquipmentCatalog.GetById).OfType<ThrusterEquipmentDefinition>().All(thruster => thruster.IsValid)
                ? Pass()
                : Fail("canonical thruster metadata was invalid");
        }

        private static (bool Success, string FailureReason) ThrusterBalanceIsFinite()
        {
            return EquipmentCatalog.GetAll().OfType<ThrusterEquipmentDefinition>().All(thruster =>
                       IsFinitePositive(thruster.EnergyCapacity) &&
                       IsFinitePositive(thruster.EnergyRegenerationRate) &&
                       IsFinitePositive(thruster.AfterburnDrainRate) &&
                       thruster.EnergyRegenerationRate < thruster.AfterburnDrainRate &&
                       thruster.AfterburnSpeedMultiplier >= 1f &&
                       thruster.AfterburnSpeedMultiplier <= 3f)
                ? Pass()
                : Fail("thruster values were not finite, bounded, or eventually depleting");
        }

        private static (bool Success, string FailureReason) ThrusterMountRules()
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            ThrusterEquipmentDefinition thruster = EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition;
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_light_laser") as WeaponEquipmentDefinition;
            ShieldEquipmentDefinition shield = EquipmentCatalog.GetById("civilian_shield_generator") as ShieldEquipmentDefinition;
            PowerplantEquipmentDefinition plant = EquipmentCatalog.GetById("civilian_powerplant") as PowerplantEquipmentDefinition;
            loadout.AddOwnedEquipment(thruster);
            loadout.AddOwnedEquipment(gun);
            loadout.AddOwnedEquipment(shield);
            loadout.AddOwnedEquipment(plant);
            bool mounted = loadout.TryMountEquipment("Thruster", thruster, out _);
            bool gunRejected = !loadout.TryMountEquipment("Thruster", gun, out _);
            bool shieldRejected = !loadout.TryMountEquipment("Thruster", shield, out _);
            bool plantRejected = !loadout.TryMountEquipment("Thruster", plant, out _);
            return mounted && gunRejected && shieldRejected && plantRejected && loadout.GetMountedThruster()?.Id == thruster.Id
                ? Pass()
                : Fail("a non-thruster item mounted in the thruster slot");
        }

        private static (bool Success, string FailureReason) DuplicateThrusterIsRejected()
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            ThrusterEquipmentDefinition first = EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition;
            ThrusterEquipmentDefinition second = EquipmentCatalog.GetById("liberty_patrol_thruster") as ThrusterEquipmentDefinition;
            loadout.AddOwnedEquipment(first);
            loadout.AddOwnedEquipment(second);
            return loadout.TryMountEquipment(first, out _) &&
                   !loadout.TryMountEquipment(second, out _) &&
                   loadout.GetMountedCount(first.Id) == 1 && loadout.GetMountedCount(second.Id) == 0
                ? Pass()
                : Fail("multiple active thrusters were accepted");
        }

        private static (bool Success, string FailureReason) StarterThrusterIsMounted()
        {
            Ship ship = new(Vector3.Zero);
            ThrusterEquipmentDefinition thruster = ship.GetMountedThruster();
            return thruster?.Id == "civilian_thruster" && ship.Loadout.GetOwnedCount(thruster.Id) == 1
                ? Pass()
                : Fail("new player did not receive the civilian starter thruster");
        }

        private static (bool Success, string FailureReason) StarterRuntimeIsFull()
        {
            Ship ship = new(Vector3.Zero);
            return ship.ThrusterEnergy.HasMountedThruster &&
                   Nearly(ship.ThrusterEnergy.CurrentEnergy, ship.ThrusterEnergy.MaxEnergy) &&
                   Nearly(ship.ThrusterEnergy.CurrentEnergy, 240f)
                ? Pass()
                : Fail("starter thruster runtime did not initialize full");
        }

        private static (bool Success, string FailureReason) NormalFlightDoesNotConsume()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.SetCurrentEnergy(100f);
            energy.Advance(1f, afterburnRequested: false);
            return Nearly(energy.CurrentEnergy, 136f)
                ? Pass()
                : Fail("idle thruster state did not regenerate without afterburn");
        }

        private static (bool Success, string FailureReason) AfterburnDrainsExactCost()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            bool active = energy.Advance(1f, afterburnRequested: true);
            return active && Nearly(energy.CurrentEnergy, 200f)
                ? Pass()
                : Fail("one second of afterburn did not spend the canonical drain rate");
        }

        private static (bool Success, string FailureReason) AfterburnDrainScalesWithElapsedTime()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.Advance(2.5f, afterburnRequested: true);
            return Nearly(energy.CurrentEnergy, 140f)
                ? Pass()
                : Fail("afterburn drain did not scale with elapsed time");
        }

        private static (bool Success, string FailureReason) DrainClampsAtZero()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            bool active = energy.Advance(10f, afterburnRequested: true);
            return !active && Nearly(energy.CurrentEnergy, 0f) && energy.CurrentEnergy >= 0f
                ? Pass()
                : Fail("afterburn drain escaped the zero lower bound");
        }

        private static (bool Success, string FailureReason) ZeroChargeDisablesAfterburn()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.SetCurrentEnergy(0f);
            bool active = energy.Advance(0f, afterburnRequested: true);
            return !active && !energy.CanAfterburn()
                ? Pass()
                : Fail("zero thruster energy remained usable");
        }

        private static (bool Success, string FailureReason) HoldingAtZeroRegeneratesSafely()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.SetCurrentEnergy(0f);
            bool active = energy.Advance(0.5f, afterburnRequested: true);
            return !active && Nearly(energy.CurrentEnergy, 18f) && energy.CurrentEnergy >= 0f
                ? Pass()
                : Fail("holding afterburn at zero created invalid charge or restarted boost");
        }

        private static (bool Success, string FailureReason) ReleaseRegenerates()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.SetCurrentEnergy(100f);
            energy.Advance(1f, afterburnRequested: true);
            float before = energy.CurrentEnergy;
            energy.Advance(1f, afterburnRequested: false);
            return Nearly(before, 60f) && Nearly(energy.CurrentEnergy, 96f)
                ? Pass()
                : Fail("releasing afterburn did not immediately regenerate");
        }

        private static (bool Success, string FailureReason) RegenerationClampsAtMaximum()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.SetCurrentEnergy(239f);
            energy.Advance(10f, afterburnRequested: false);
            return Nearly(energy.CurrentEnergy, energy.MaxEnergy)
                ? Pass()
                : Fail("thruster regeneration exceeded capacity");
        }

        private static (bool Success, string FailureReason) MissingThrusterIsSafe()
        {
            ThrusterEnergy energy = new();
            return !energy.HasMountedThruster && !energy.CanAfterburn() &&
                   energy.MaxEnergy == 0f && energy.CurrentEnergy == 0f &&
                   !energy.Advance(10f, afterburnRequested: true)
                ? Pass()
                : Fail("missing thruster supplied usable energy");
        }

        private static (bool Success, string FailureReason) DestroyedShipDoesNotRegenerate()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.SetCurrentEnergy(100f);
            energy.Advance(1f, afterburnRequested: false, shipAlive: false);
            return Nearly(energy.CurrentEnergy, 100f)
                ? Pass()
                : Fail("destroyed ship regenerated thruster energy");
        }

        private static (bool Success, string FailureReason) UnmountClearsRuntime()
        {
            Ship ship = new(Vector3.Zero);
            ThrusterEquipmentDefinition mounted = ship.GetMountedThruster();
            bool unmounted = ship.Loadout.TryUnmountEquipment(mounted.Id, out _);
            ship.RefreshThrusterFromLoadout();
            return unmounted && ship.GetMountedThruster() == null &&
                   !ship.ThrusterEnergy.HasMountedThruster && ship.ThrusterEnergy.CurrentEnergy == 0f
                ? Pass()
                : Fail("unmount left stale thruster runtime state");
        }

        private static (bool Success, string FailureReason) ChangingThrusterRebindsRuntime()
        {
            Ship ship = new(Vector3.Zero);
            ThrusterEquipmentDefinition patrol = EquipmentCatalog.GetById("liberty_patrol_thruster") as ThrusterEquipmentDefinition;
            ship.Loadout.AddOwnedEquipment(patrol);
            ship.Loadout.TryUnmountEquipment(ship.GetMountedThruster().Id, out _);
            bool mounted = ship.Loadout.TryMountEquipment(patrol, out _);
            ship.RefreshThrusterFromLoadout();
            return mounted && ship.ThrusterEnergy.MountedThrusterId == patrol.Id &&
                   Nearly(ship.ThrusterEnergy.MaxEnergy, patrol.EnergyCapacity) &&
                   Nearly(ship.ThrusterEnergy.CurrentEnergy, patrol.EnergyCapacity)
                ? Pass()
                : Fail("changing thrusters left stale runtime values");
        }

        private static (bool Success, string FailureReason) DealerBuysAndMountsThruster()
        {
            Ship ship = new(Vector3.Zero);
            EquipmentDealer dealer = new();
            ThrusterEquipmentDefinition patrol = EquipmentCatalog.GetById("liberty_patrol_thruster") as ThrusterEquipmentDefinition;
            PlayerCredits credits = new(patrol.Price * 2);
            bool bought = dealer.TryBuyEquipment(patrol, credits, ship, out _);
            bool unmounted = dealer.TryUnmountEquipment(ship.GetMountedThruster(), ship, out _);
            bool mounted = dealer.TryMountEquipment(patrol, ship, out _);
            return bought && unmounted && mounted && ship.GetMountedThruster()?.Id == patrol.Id &&
                   Nearly(ship.ThrusterEnergy.CurrentEnergy, patrol.EnergyCapacity)
                ? Pass()
                : Fail("dealer did not atomically buy, unmount, and mount a thruster");
        }

        private static (bool Success, string FailureReason) DealerRejectsIncompatibleMount()
        {
            Ship ship = new(Vector3.Zero);
            EquipmentDealer dealer = new();
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_light_laser") as WeaponEquipmentDefinition;
            return !dealer.TryMountEquipment(gun, ship, out _) && ship.GetMountedThruster()?.Id == "civilian_thruster"
                ? Pass()
                : Fail("dealer accepted an incompatible thruster mount");
        }

        private static (bool Success, string FailureReason) DealerSellsSpareThruster()
        {
            Ship ship = new(Vector3.Zero);
            EquipmentDealer dealer = new();
            ThrusterEquipmentDefinition patrol = EquipmentCatalog.GetById("liberty_patrol_thruster") as ThrusterEquipmentDefinition;
            PlayerCredits credits = new(patrol.Price * 2);
            if (!dealer.TryBuyEquipment(patrol, credits, ship, out _))
                return Fail("dealer could not buy a spare thruster");
            int before = credits.Credits;
            bool sold = dealer.TrySellUnequippedEquipment(patrol, credits, ship, out _);
            return sold && ship.Loadout.GetOwnedCount(patrol.Id) == 0 && credits.Credits == before + dealer.GetResaleValue(patrol)
                ? Pass()
                : Fail("dealer did not sell an unmounted spare thruster");
        }

        private static (bool Success, string FailureReason) NpcThrusterAssignmentIsAppropriate()
        {
            string police = NpcEquipmentLoadoutFactory.CreateForNpc("Police Fighter", FactionManager.LibertyPolice, "smoke/fighter", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Standard).GetMountedThruster()?.Id;
            string navy = NpcEquipmentLoadoutFactory.CreateForNpc("Navy Heavy", FactionManager.LibertyNavy, "smoke/heavy", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.High).GetMountedThruster()?.Id;
            string rogue = NpcEquipmentLoadoutFactory.CreateForNpc("Rogue Fighter", FactionManager.LibertyRogues, "smoke/fighter", TrafficZoneBehaviorType.PirateAmbush, NpcLoadoutTier.Low).GetMountedThruster()?.Id;
            string professional = NpcEquipmentLoadoutFactory.CreateForNpc("Bounty Hunter", FactionManager.BountyHunters, "smoke/fighter", TrafficZoneBehaviorType.PirateAmbush, NpcLoadoutTier.Standard).GetMountedThruster()?.Id;
            return police == "liberty_patrol_thruster" && navy == "liberty_heavy_thruster" &&
                   rogue == "rogue_scrap_thruster" && professional == "professional_thruster"
                ? Pass()
                : Fail("NPC thruster policy did not follow faction/tier flavor");
        }

        private static (bool Success, string FailureReason) NpcThrusterAssignmentIsDeterministic()
        {
            ShipLoadout first = NpcEquipmentLoadoutFactory.CreateForNpc("Police Fighter", FactionManager.LibertyPolice, "smoke/fighter", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Standard);
            ShipLoadout second = NpcEquipmentLoadoutFactory.CreateForNpc("Police Fighter", FactionManager.LibertyPolice, "smoke/fighter", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Standard);
            return first.GetMountedThruster()?.Id == second.GetMountedThruster()?.Id &&
                   first.GetMountedThruster()?.Id == "liberty_patrol_thruster"
                ? Pass()
                : Fail("identical NPC state produced different thrusters");
        }

        private static (bool Success, string FailureReason) NpcRuntimeStartsFull()
        {
            NpcShip npc = CreateCombatNpc("Thruster NPC", FactionManager.LibertyPolice, Vector3.Zero);
            ThrusterEquipmentDefinition thruster = npc.Loadout.GetMountedThruster();
            return thruster != null && npc.ThrusterEnergy.HasMountedThruster &&
                   Nearly(npc.ThrusterEnergy.CurrentEnergy, thruster.EnergyCapacity)
                ? Pass()
                : Fail("NPC thruster runtime did not initialize from its loadout");
        }

        private static (bool Success, string FailureReason) NpcAfterburnConsumesCharge()
        {
            NpcShip attacker = CreateCombatNpc("Long Approach Fighter", FactionManager.LibertyPolice, Vector3.Zero);
            NpcShip target = CreateCombatNpc("Far Rogue Fighter", FactionManager.LibertyRogues, new Vector3(10000f, 0f, 0f));
            attacker.SetFactionCombatTarget(target);
            float before = attacker.ThrusterEnergy.CurrentEnergy;
            attacker.Update(Frame(1f), null);
            return attacker.IsAfterburnerActive && attacker.ThrusterEnergy.CurrentEnergy < before
                ? Pass()
                : Fail("NPC pursuit did not route afterburn through thruster energy");
        }

        private static (bool Success, string FailureReason) DepletedNpcCannotAfterburn()
        {
            NpcShip attacker = CreateCombatNpc("Depleted Approach Fighter", FactionManager.LibertyPolice, Vector3.Zero);
            NpcShip target = CreateCombatNpc("Far Rogue Target", FactionManager.LibertyRogues, new Vector3(10000f, 0f, 0f));
            attacker.SetFactionCombatTarget(target);
            attacker.ThrusterEnergy.SetCurrentEnergy(0f);
            attacker.Update(Frame(1f), null);
            return !attacker.IsAfterburnerActive && attacker.ThrusterEnergy.CurrentEnergy > 0f
                ? Pass()
                : Fail("depleted NPC retained afterburn or invalid energy");
        }

        private static (bool Success, string FailureReason) NpcChargeRegenerates()
        {
            NpcShip npc = CreateCombatNpc("Recovering Fighter", FactionManager.LibertyPolice, Vector3.Zero);
            npc.ClearEncounterState();
            npc.ThrusterEnergy.SetCurrentEnergy(0f);
            npc.Update(Frame(1f), null);
            return Nearly(npc.ThrusterEnergy.CurrentEnergy, npc.ThrusterEnergy.RegenRate)
                ? Pass()
                : Fail("NPC thruster energy did not regenerate while idle");
        }

        private static (bool Success, string FailureReason) AfterburnLeavesWeaponEnergyUnchanged()
        {
            Ship ship = new(Vector3.Zero);
            ship.ThrusterEnergy.SetCurrentEnergy(100f);
            float before = ship.WeaponEnergy.CurrentEnergy;
            ship.ThrusterEnergy.Advance(1f, afterburnRequested: true);
            return Nearly(ship.WeaponEnergy.CurrentEnergy, before)
                ? Pass()
                : Fail("afterburn modified the weapon-energy pool");
        }

        private static (bool Success, string FailureReason) GunfireLeavesThrusterEnergyUnchanged()
        {
            Ship ship = new(Vector3.Zero);
            ship.ThrusterEnergy.SetCurrentEnergy(100f);
            float before = ship.ThrusterEnergy.CurrentEnergy;
            WeaponEquipmentDefinition gun = ship.GetPrimaryMountedGun();
            bool fired = ship.WeaponEnergy.TrySpend(gun.EnergyCost);
            return fired && Nearly(ship.ThrusterEnergy.CurrentEnergy, before)
                ? Pass()
                : Fail("gun energy spending modified the thruster pool");
        }

        private static (bool Success, string FailureReason) ZeroWeaponEnergyStillAllowsAfterburn()
        {
            Ship ship = new(Vector3.Zero);
            ship.WeaponEnergy.SetCurrentEnergy(0f);
            ship.ThrusterEnergy.SetCurrentEnergy(100f);
            return ship.WeaponEnergy.CurrentEnergy == 0f && ship.ThrusterEnergy.Advance(1f, true) && ship.ThrusterEnergy.CurrentEnergy < 100f
                ? Pass()
                : Fail("zero weapon energy blocked afterburn");
        }

        private static (bool Success, string FailureReason) ZeroThrusterEnergyStillAllowsGunfire()
        {
            Ship ship = new(Vector3.Zero);
            ship.ThrusterEnergy.SetCurrentEnergy(0f);
            WeaponEquipmentDefinition gun = ship.GetPrimaryMountedGun();
            return ship.ThrusterEnergy.CurrentEnergy == 0f && ship.WeaponEnergy.TrySpend(gun.EnergyCost)
                ? Pass()
                : Fail("zero thruster energy blocked gun energy spending");
        }

        private static (bool Success, string FailureReason) ThrusterSalvagePreservesActualId()
        {
            (NpcShip source, string id) = FindThrusterSalvageSource();
            return source != null && !string.IsNullOrWhiteSpace(id)
                ? Pass()
                : Fail("no deterministic actual-ID thruster salvage case was found");
        }

        private static (bool Success, string FailureReason) ThrusterSalvageUsesSharedBounds()
        {
            return CombatSalvageService.MaxLiveSalvageObjects == 32 &&
                   Nearly((float)CombatSalvageService.SalvageLifetimeSeconds, 120f) &&
                   CombatSalvageService.StandardMaximumEquipmentObjectsPerDestruction == 1 &&
                   CombatSalvageService.HeavyMaximumEquipmentObjectsPerDestruction == 2
                ? Pass()
                : Fail("thruster salvage did not use the shared cap/lifetime");
        }

        private static (bool Success, string FailureReason) ThrusterPodIsCanonical()
        {
            bool created = CargoPod.TryCreateEquipment("civilian_thruster", Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod);
            return created && pod?.IsEquipment == true && pod.EquipmentId == "civilian_thruster" &&
                   pod.GetEquipment() is ThrusterEquipmentDefinition
                ? Pass()
                : Fail("thruster salvage pod was not canonical");
        }

        private static (bool Success, string FailureReason) ThrusterPickupCreatesSpare()
        {
            (NpcShip source, string id) = FindThrusterSalvageSource();
            if (source == null)
                return Fail("no thruster salvage source was found");

            LootManager manager = new(null, null, null, null, new CombatSalvageService());
            manager.SpawnLootForDestroyedNpc(source);
            CargoPod pod = manager.ActivePods.FirstOrDefault(active => active.IsEquipment && active.EquipmentId == id);
            if (pod == null)
                return Fail("thruster salvage did not spawn a physical pod");

            Ship player = new(Vector3.Zero);
            int before = player.Loadout.GetOwnedCount(id);
            player.Position = pod.Position;
            manager.Update(Frame(0.1f), player, false);
            return player.Loadout.GetOwnedCount(id) == before + 1
                ? Pass()
                : Fail("thruster pickup did not add one owned spare");
        }

        private static (bool Success, string FailureReason) ThrusterPickupDoesNotAutoMount()
        {
            (NpcShip source, string id) = FindThrusterSalvageSource();
            if (source == null)
                return Fail("no thruster salvage source was found");

            LootManager manager = new(null, null, null, null, new CombatSalvageService());
            manager.SpawnLootForDestroyedNpc(source);
            CargoPod pod = manager.ActivePods.FirstOrDefault(active => active.IsEquipment && active.EquipmentId == id);
            Ship player = new(Vector3.Zero);
            string mountedBefore = player.GetMountedThruster()?.Id;
            if (pod == null)
                return Fail("thruster salvage did not spawn a physical pod");
            player.Position = pod.Position;
            manager.Update(Frame(0.1f), player, false);
            return player.GetMountedThruster()?.Id == mountedBefore
                ? Pass()
                : Fail("thruster pickup auto-mounted the salvaged item");
        }

        private static (bool Success, string FailureReason) SaveLoadPreservesMountedThruster()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Roguelancer_ThrusterEnergy_" + Guid.NewGuid().ToString("N"));
            try
            {
                ShipLoadout source = ShipLoadout.CreateStarterLoadout(false);
                ThrusterEquipmentDefinition civilian = EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition;
                ThrusterEquipmentDefinition patrol = EquipmentCatalog.GetById("liberty_patrol_thruster") as ThrusterEquipmentDefinition;
                source.AddOwnedEquipment(civilian);
                source.AddOwnedEquipment(patrol);
                source.TryMountEquipment(patrol, out _);
                SaveGameManager manager = new(Path.Combine(directory, "thruster.json"));
                SaveGameData data = new()
                {
                    OwnedEquipment = manager.CaptureOwnedEquipment(source),
                    MountedEquipment = manager.CaptureMountedEquipment(source)
                };
                manager.TrySave(data, out _);
                manager.TryLoad(out SaveGameData loaded, out _);
                ShipLoadout restored = manager.BuildLoadout(loaded, out List<string> warnings);
                return loaded.SchemaVersion == SaveGameData.CurrentSchemaVersion && warnings.Count == 0 &&
                       restored.GetMountedThruster()?.Id == patrol.Id
                    ? Pass()
                    : Fail("save/load did not preserve mounted thruster");
            }
            finally
            {
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
            }
        }

        private static (bool Success, string FailureReason) SaveLoadPreservesSpareThrusters()
        {
            ShipLoadout source = ShipLoadout.CreateStarterLoadout(false);
            ThrusterEquipmentDefinition civilian = EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition;
            ThrusterEquipmentDefinition patrol = EquipmentCatalog.GetById("liberty_patrol_thruster") as ThrusterEquipmentDefinition;
            source.AddOwnedEquipment(civilian, 2);
            source.AddOwnedEquipment(patrol);
            source.TryMountEquipment(civilian, out _);
            SaveGameManager manager = new();
            SaveGameData data = new()
            {
                OwnedEquipment = manager.CaptureOwnedEquipment(source),
                MountedEquipment = manager.CaptureMountedEquipment(source)
            };
            ShipLoadout restored = manager.BuildLoadout(data, out _);
            return restored.GetOwnedCount(civilian.Id) == 2 && restored.GetOwnedCount(patrol.Id) == 1
                ? Pass()
                : Fail("save/load did not preserve spare thruster ownership");
        }

        private static (bool Success, string FailureReason) LoadedRuntimeRestoresFull()
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            ThrusterEquipmentDefinition thruster = EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition;
            loadout.AddOwnedEquipment(thruster);
            loadout.TryMountEquipment(thruster, out _);
            Ship ship = new(Vector3.Zero);
            ship.SetLoadout(loadout);
            ship.ThrusterEnergy.SetCurrentEnergy(1f);
            ship.SetLoadout(loadout);
            return Nearly(ship.ThrusterEnergy.CurrentEnergy, thruster.EnergyCapacity)
                ? Pass()
                : Fail("loaded runtime charge did not restore to full");
        }

        private static (bool Success, string FailureReason) OldSaveGetsThrusterFallback()
        {
            ShipLoadout restored = new SaveGameManager().BuildLoadout(
                new SaveGameData { SchemaVersion = SaveGameData.CurrentSchemaVersion }, out List<string> warnings);
            return restored.GetMountedThruster()?.Id == "civilian_thruster" &&
                   restored.GetOwnedCount("civilian_thruster") == 1 && warnings.Count == 0
                ? Pass()
                : Fail("old schema-10 save did not receive exactly one civilian thruster");
        }

        private static (bool Success, string FailureReason) MalformedThrusterStateIsSafe()
        {
            ThrusterEnergy energy = CreateEnergy("civilian_thruster");
            energy.SetCurrentEnergy(float.NaN);
            energy.Advance(-1f, true);
            energy.Advance(float.MaxValue, false);
            return IsFiniteNonNegative(energy.CurrentEnergy) &&
                   IsFinitePositive(energy.MaxEnergy) && energy.CurrentEnergy <= energy.MaxEnergy
                ? Pass()
                : Fail("malformed or extreme thruster values escaped normalization");
        }

        private static (bool Success, string FailureReason) HudTextExposesThruster()
        {
            Ship ship = new(Vector3.Zero);
            return ship.ThrusterEnergy.GetHudText() == $"THRUSTER {ship.ThrusterEnergy.CurrentEnergy:F0}/{ship.ThrusterEnergy.MaxEnergy:F0}"
                ? Pass()
                : Fail("thruster HUD text did not expose current/max values");
        }

        private static (bool Success, string FailureReason) StarterDurationIsFinite()
        {
            ThrusterEquipmentDefinition thruster = EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition;
            float seconds = thruster.EnergyCapacity / thruster.AfterburnDrainRate;
            return seconds >= 4f && seconds <= 7f && Nearly(seconds, 6f)
                ? Pass()
                : Fail($"starter continuous afterburn duration was {seconds:F2}s");
        }

        private static (bool Success, string FailureReason) StarterRechargeDurationIsFinite()
        {
            ThrusterEquipmentDefinition thruster = EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition;
            float seconds = thruster.EnergyCapacity / thruster.EnergyRegenerationRate;
            return seconds >= 5f && seconds <= 10f && Nearly(seconds, 6.666667f)
                ? Pass()
                : Fail($"starter recharge duration was {seconds:F2}s");
        }

        private static (bool Success, string FailureReason) ThrusterEquipmentPodIsAccepted()
        {
            EquipmentDefinition equipment = EquipmentCatalog.GetById("liberty_heavy_thruster");
            return CargoPod.TryCreateEquipment(equipment.Id, Vector3.Zero, Vector3.Zero, 120f, 200f, out _)
                ? Pass()
                : Fail("canonical thruster was rejected by shared equipment pod infrastructure");
        }

        private static (bool Success, string FailureReason) LegacyLightThrusterRemainsLoadable()
        {
            EquipmentDefinition legacy = EquipmentCatalog.GetById("light_thruster");
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            loadout.AddOwnedEquipment(legacy);
            bool mounted = loadout.TryMountEquipment(legacy, out _);
            return legacy is ThrusterEquipmentDefinition && mounted && loadout.GetMountedThruster()?.Id == legacy.Id
                ? Pass()
                : Fail("legacy light thruster could not be restored as typed equipment");
        }

        private static (bool Success, string FailureReason) ThrusterResetRestoresFull()
        {
            Ship ship = new(Vector3.Zero);
            ship.ThrusterEnergy.SetCurrentEnergy(1f);
            ship.Reset();
            return Nearly(ship.ThrusterEnergy.CurrentEnergy, ship.ThrusterEnergy.MaxEnergy) && !ship.IsAfterburnerActive
                ? Pass()
                : Fail("ship reset leaked transient thruster state");
        }

        private static (bool Success, string FailureReason) InvalidDefinitionIsRejected()
        {
            ThrusterEquipmentDefinition invalid = new()
            {
                Id = "invalid-thruster",
                Name = "Invalid",
                EquipmentType = EquipmentType.Thruster,
                Price = 1,
                EnergyCapacity = -1f,
                EnergyRegenerationRate = float.NaN,
                AfterburnDrainRate = 0f,
                AfterburnSpeedMultiplier = float.PositiveInfinity
            };
            ThrusterEnergy energy = new(invalid);
            return !energy.HasMountedThruster && energy.MaxEnergy == 0f && !energy.CanAfterburn()
                ? Pass()
                : Fail("invalid thruster definition created usable runtime state");
        }

        private static (bool Success, string FailureReason) ThrusterSurvivesHardpointReconfiguration()
        {
            ShipLoadout source = ShipLoadout.CreateStarterLoadout();
            ShipLoadout restored = source.ReconfigureHardpoints(new[]
            {
                new ShipHardpoint { Id = "Thruster", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Thruster } },
                new ShipHardpoint { Id = "Gun", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Gun } }
            }, out List<string> warnings);
            return restored.GetMountedThruster()?.Id == "civilian_thruster" &&
                   restored.GetMountedCount("civilian_thruster") == 1
                ? Pass()
                : Fail($"hardpoint reconfiguration lost the mounted thruster: {restored.GetMountedSummary()} | warnings={string.Join("; ", warnings)}");
        }

        private static ThrusterEnergy CreateEnergy(string id)
        {
            return new ThrusterEnergy(EquipmentCatalog.GetById(id) as ThrusterEquipmentDefinition);
        }

        private static (NpcShip Ship, string Id) FindThrusterSalvageSource()
        {
            CombatSalvageService service = new();
            for (int i = 0; i < 10000; i++)
            {
                NpcShip candidate = CreateTrader($"Thruster Salvage Trader {i}");
                string id = candidate.Loadout.GetMountedThruster()?.Id;
                if (!service.ShouldDropThruster(candidate))
                    continue;

                candidate.ApplyDamage(candidate.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
                IReadOnlyList<SalvageDrop> drops = service.EvaluateDestruction(candidate);
                if (id != null && drops.Any(drop => drop.IsEquipment && drop.EquipmentId == id))
                    return (candidate, id);
            }

            return (null, string.Empty);
        }

        private static NpcShip CreateCombatNpc(string name, string factionId, Vector3 position)
        {
            NpcShip npc = new(name, position, position, 1000f, 0f, factionId);
            npc.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "thruster-energy-smoke", position, 1000f, 120f, 20000f);
            return npc;
        }

        private static NpcShip CreateTrader(string name)
        {
            NpcShip npc = new(name, Vector3.Zero, Vector3.Zero, 1000f, 0f, FactionManager.NeutralCivilians);
            npc.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "thruster-salvage", Vector3.Zero, 1000f, 120f, 20000f);
            return npc;
        }

        private static GameTime Frame(float seconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
            return new GameTime(elapsed, elapsed);
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.01f;
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
