using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Phase 40 coverage for deterministic faction/role/ship-class policy,
    /// canonical combat consumption, and the Phase 39 salvage seam.
    /// </summary>
    internal sealed class NpcLoadoutSmokeTest
    {
        private readonly GraphicsDevice _graphicsDevice;
        private int _passed;
        private int _failed;

        public NpcLoadoutSmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        }

        public (int Passed, int Failed) Run()
        {
            Check("same metadata produces the same loadout", DeterministicLoadout);
            Check("loadout does not depend on object identity", ObjectIdentityDoesNotMatter);
            Check("police uses only police profile weapons", PoliceProfileIsHonored);
            Check("rogues use only rogue profile weapons", RogueProfileIsHonored);
            Check("corporate security uses a valid professional profile", CorporateProfileIsHonored);
            Check("unknown faction uses the safe fallback", UnknownFactionUsesFallback);
            Check("faction profiles create distinct choices", FactionsCreateDistinctChoices);
            Check("standard fighter stays within gun hardpoint bounds", StandardFighterBounds);
            Check("heavy fighter fills only supported gun hardpoints", HeavyFighterBounds);
            Check("high tier improves selection without invalid mounts", TierScaling);
            Check("role keeps civilian traffic unarmed", CivilianRoleRemainsUnarmed);
            Check("pirate mission role arms a combat target", MissionRoleIsCombatCapable);
            Check("all mounted IDs resolve canonically", MountedWeaponsAreCanonical);
            Check("invalid catalog definitions are rejected safely", InvalidDefinitionsFailSafe);
            Check("NPC firing emits the assigned canonical guns", FiringUsesAssignedLoadout);
            Check("different guns retain different projectile behavior", DifferentGunsKeepStats);
            Check("mounted weapon range controls engagement", RangeUsesMountedWeapon);
            Check("weapon count controls volley count", WeaponCountControlsVolley);
            Check("hostile police and rogue NPCs acquire each other", HostileNpcAcquisitionRemainsSupported);
            Check("NPC damage uses the authoritative hull path", NpcDamageUsesAuthoritativePath);
            Check("police equipment salvage comes from police loadout", PoliceSalvageUsesCarriedWeapons);
            Check("rogue equipment salvage comes from rogue loadout", RogueSalvageUsesCarriedWeapons);
            Check("equipment salvage cannot invent a weapon", SalvageCannotInventWeapons);
            Check("commodity salvage remains independent", CommodityPolicyIsIndependent);
            Check("ambient and mission role inputs receive intended policies", SpawnRoleCoverage);
            Check("transient NPC loadouts do not change save schema", SaveSchemaRemainsVersionTen);

            Console.WriteLine($"[NPC LOADOUT SMOKE] RESULT: {_passed} passed, {_failed} failed");
            return (_passed, _failed);
        }

        private void Check(string label, Func<bool> assertion)
        {
            try
            {
                if (RunSilenced(assertion))
                {
                    _passed++;
                    Console.WriteLine($"[NPC LOADOUT SMOKE] PASS {label}");
                }
                else
                {
                    _failed++;
                    Console.WriteLine($"[NPC LOADOUT SMOKE] FAIL {label}: assertion returned false");
                }
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"[NPC LOADOUT SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static bool DeterministicLoadout()
        {
            ShipLoadout first = BuildLoadout("Phase40 Rogue 17", FactionManager.LibertyRogues);
            ShipLoadout second = BuildLoadout("Phase40 Rogue 17", FactionManager.LibertyRogues);
            return WeaponIds(first).SequenceEqual(WeaponIds(second), StringComparer.OrdinalIgnoreCase);
        }

        private static bool ObjectIdentityDoesNotMatter()
        {
            ShipLoadout left = BuildLoadout("Phase40 Police 2", FactionManager.LibertyPolice);
            ShipLoadout right = BuildLoadout("Phase40 Police 2", FactionManager.LibertyPolice);
            return !ReferenceEquals(left, right) &&
                   left.GetMountedSummary() == right.GetMountedSummary();
        }

        private static bool PoliceProfileIsHonored()
        {
            NpcLoadoutPolicyProfile profile = NpcEquipmentLoadoutFactory.GetProfileForFaction(FactionManager.LibertyPolice);
            List<string> ids = WeaponIds(BuildLoadout("Police Patrol Fighter", FactionManager.LibertyPolice));
            return ids.Count == 2 && ids.All(id => profile.PreferredWeaponIds.Contains(id, StringComparer.OrdinalIgnoreCase)) &&
                   ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        }

        private static bool RogueProfileIsHonored()
        {
            NpcLoadoutPolicyProfile profile = NpcEquipmentLoadoutFactory.GetProfileForFaction(FactionManager.LibertyRogues);
            List<string> ids = WeaponIds(BuildLoadout("Rogue Attack Fighter", FactionManager.LibertyRogues));
            return ids.Count == 2 && ids.All(id => profile.PreferredWeaponIds.Contains(id, StringComparer.OrdinalIgnoreCase)) &&
                   ids.Contains("rogue_blaster", StringComparer.OrdinalIgnoreCase);
        }

        private static bool CorporateProfileIsHonored()
        {
            NpcLoadoutPolicyProfile profile = NpcEquipmentLoadoutFactory.GetProfileForFaction(FactionManager.LibertyCorporations);
            List<string> ids = WeaponIds(BuildLoadout("Corporate Security Fighter", FactionManager.LibertyCorporations, tier: NpcLoadoutTier.High));
            return ids.Count == 2 && ids.All(id => profile.PreferredWeaponIds.Contains(id, StringComparer.OrdinalIgnoreCase)) &&
                   ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        }

        private static bool UnknownFactionUsesFallback()
        {
            NpcLoadoutPolicyProfile profile = NpcEquipmentLoadoutFactory.GetProfileForFaction("unregistered_faction");
            ShipLoadout loadout = BuildLoadout("Unknown Fighter", "unregistered_faction");
            return profile.PreferredWeaponIds.Count > 0 &&
                   WeaponIds(loadout).Count == 2 &&
                   WeaponIds(loadout).All(id => profile.PreferredWeaponIds.Contains(id, StringComparer.OrdinalIgnoreCase)) &&
                   loadout.GetMountedGuns().All(NpcEquipmentLoadoutFactory.IsValidNpcWeapon);
        }

        private static bool FactionsCreateDistinctChoices()
        {
            List<string> police = WeaponIds(BuildLoadout("Identity Fighter", FactionManager.LibertyPolice));
            List<string> rogue = WeaponIds(BuildLoadout("Identity Fighter", FactionManager.LibertyRogues));
            return !police.SequenceEqual(rogue, StringComparer.OrdinalIgnoreCase) &&
                   police.Contains("liberty_pulse_cannon", StringComparer.OrdinalIgnoreCase) &&
                   rogue.Contains("rogue_blaster", StringComparer.OrdinalIgnoreCase);
        }

        private static bool StandardFighterBounds()
        {
            ShipLoadout loadout = BuildLoadout("Standard Fighter", FactionManager.LibertyPolice);
            return WeaponIds(loadout).Count >= 1 && WeaponIds(loadout).Count <= 2 &&
                   WeaponIds(loadout).Count <= GunHardpointCapacity(loadout);
        }

        private static bool HeavyFighterBounds()
        {
            ShipLoadout loadout = BuildLoadout("Warthog Heavy Fighter", FactionManager.LibertyRogues, heavy: true);
            return WeaponIds(loadout).Count == 2 &&
                   WeaponIds(loadout).Count <= GunHardpointCapacity(loadout) &&
                   loadout.GetMountedGuns().All(NpcEquipmentLoadoutFactory.IsValidNpcWeapon);
        }

        private static bool TierScaling()
        {
            ShipLoadout low = BuildLoadout("Tiered Police Fighter", FactionManager.LibertyPolice, tier: NpcLoadoutTier.Low);
            ShipLoadout high = BuildLoadout("Tiered Police Fighter", FactionManager.LibertyPolice, tier: NpcLoadoutTier.High);
            float lowDamage = low.GetMountedGuns().Sum(weapon => weapon.Damage);
            float highDamage = high.GetMountedGuns().Sum(weapon => weapon.Damage);
            return WeaponIds(low).Count == 1 && WeaponIds(high).Count == 2 && highDamage >= lowDamage;
        }

        private static bool CivilianRoleRemainsUnarmed()
        {
            ShipLoadout loadout = BuildLoadout(
                "Transport Ship Alpha",
                FactionManager.NeutralCivilians,
                "SHIPS/PI_TRANSPORT/PI_TRANSPORT",
                TrafficZoneBehaviorType.TraderRoute,
                NpcLoadoutTier.High);
            return !loadout.HasMountedGun();
        }

        private static bool MissionRoleIsCombatCapable()
        {
            ShipLoadout loadout = BuildLoadout(
                "Rogue Hunt target",
                FactionManager.LibertyRogues,
                string.Empty,
                TrafficZoneBehaviorType.PirateAmbush,
                NpcLoadoutTier.Low);
            return WeaponIds(loadout).Count == 1;
        }

        private static bool MountedWeaponsAreCanonical()
        {
            ShipLoadout loadout = BuildLoadout("Canonical Fighter", FactionManager.LibertyRogues);
            return loadout.GetMountedGuns().All(weapon =>
                EquipmentCatalog.GetById(weapon.Id) == weapon &&
                NpcEquipmentLoadoutFactory.IsValidNpcWeapon(weapon));
        }

        private static bool InvalidDefinitionsFailSafe()
        {
            EquipmentDefinition invalid = new()
            {
                Id = "phase40-invalid-gun",
                EquipmentType = EquipmentType.Gun
            };
            ShipLoadout fallback = BuildLoadout("Fallback Fighter", "unregistered_faction");
            return EquipmentCatalog.GetById("missing-phase40-gun") == null &&
                   !NpcEquipmentLoadoutFactory.IsValidNpcWeapon(invalid) &&
                   fallback.GetMountedGuns().All(NpcEquipmentLoadoutFactory.IsValidNpcWeapon);
        }

        private bool FiringUsesAssignedLoadout()
        {
            NpcShip police = CreateCombatNpc("Phase40 Police Fighter", FactionManager.LibertyPolice);
            NpcShip rogue = CreateCombatNpc("Phase40 Rogue Fighter", FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
            police.SetFactionCombatTarget(rogue);
            rogue.SetFactionCombatTarget(police);

            NpcWeaponSystem weaponSystem = new(_graphicsDevice);
            List<string> fired = new();
            weaponSystem.NpcWeaponFired += (_, _, weaponId, _) => fired.Add(weaponId);
            weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));

            int expected = police.Loadout.GetMountedGuns().Count() + rogue.Loadout.GetMountedGuns().Count();
            return fired.Count == expected && fired.All(id => WeaponIds(police.Loadout).Contains(id, StringComparer.OrdinalIgnoreCase) ||
                WeaponIds(rogue.Loadout).Contains(id, StringComparer.OrdinalIgnoreCase));
        }

        private bool DifferentGunsKeepStats()
        {
            NpcShip police = CreateCombatNpc("Phase40 Police Stats Fighter", FactionManager.LibertyPolice);
            NpcShip rogue = CreateCombatNpc("Phase40 Rogue Stats Fighter", FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
            police.SetFactionCombatTarget(rogue);
            rogue.SetFactionCombatTarget(police);

            Dictionary<string, WeaponEquipmentDefinition> fired = new(StringComparer.OrdinalIgnoreCase);
            NpcWeaponSystem weaponSystem = new(_graphicsDevice);
            weaponSystem.NpcWeaponFired += (_, _, weaponId, weapon) => fired[weaponId] = weapon;
            weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));

            return fired.TryGetValue("liberty_pulse_cannon", out WeaponEquipmentDefinition pulse) &&
                   fired.TryGetValue("rogue_blaster", out WeaponEquipmentDefinition blaster) &&
                   pulse.Damage != blaster.Damage && pulse.Range != blaster.Range;
        }

        private bool RangeUsesMountedWeapon()
        {
            NpcShip police = CreateCombatNpc("Phase40 Range Fighter", FactionManager.LibertyPolice, new Vector3(0f, 0f, 0f));
            NpcShip rogue = CreateCombatNpc("Phase40 Range Target", FactionManager.LibertyRogues, new Vector3(0f, 0f, -4400f));
            police.SetFactionCombatTarget(rogue);
            List<string> fired = new();
            NpcWeaponSystem weaponSystem = new(_graphicsDevice);
            weaponSystem.NpcWeaponFired += (_, _, weaponId, _) => fired.Add(weaponId);
            weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));
            return fired.Contains("liberty_pulse_cannon", StringComparer.OrdinalIgnoreCase);
        }

        private bool WeaponCountControlsVolley()
        {
            NpcShip police = CreateCombatNpc("Phase40 Volley Police", FactionManager.LibertyPolice);
            NpcShip rogue = CreateCombatNpc("Phase40 Volley Rogue", FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
            police.SetFactionCombatTarget(rogue);
            List<string> fired = new();
            NpcWeaponSystem weaponSystem = new(_graphicsDevice);
            weaponSystem.NpcWeaponFired += (attacker, _, weaponId, _) =>
            {
                if (attacker == police) fired.Add(weaponId);
            };
            weaponSystem.Update(Frame(0.1f), new List<NpcShip> { police, rogue }, new Ship(new Vector3(100_000f, 0f, 0f)));
            return fired.Count == police.Loadout.GetMountedGuns().Count();
        }

        private static bool HostileNpcAcquisitionRemainsSupported()
        {
            NpcShip police = CreateCombatNpc("Phase40 Acquisition Police", FactionManager.LibertyPolice);
            NpcShip rogue = CreateCombatNpc("Phase40 Acquisition Rogue", FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
            return NpcFactionCombatTargeting.SelectNearestHostileTarget(police, new[] { rogue }, 1000f) == rogue &&
                   police.SetFactionCombatTarget(rogue) && rogue.SetFactionCombatTarget(police);
        }

        private bool NpcDamageUsesAuthoritativePath()
        {
            NpcShip police = CreateCombatNpc("Phase40 Damage Police", FactionManager.LibertyPolice);
            NpcShip rogue = CreateCombatNpc("Phase40 Damage Rogue", FactionManager.LibertyRogues, new Vector3(0f, 0f, -300f));
            rogue.Radius = 1000f;
            rogue.Shields.RegenDelay = 10000f;
            police.SetFactionCombatTarget(rogue);
            float beforeShield = rogue.Shields.CurrentShields;
            int damageEvents = 0;
            NpcWeaponSystem weaponSystem = new(_graphicsDevice);
            weaponSystem.NpcShipDamaged += (_, target, damage) =>
            {
                if (target == rogue && damage > 0f) damageEvents++;
            };
            List<NpcShip> ships = new() { police, rogue };
            Ship player = new(new Vector3(100_000f, 0f, 0f));
            weaponSystem.Update(Frame(0.1f), ships, player);
            weaponSystem.Update(Frame(0.1f), ships, player);
            return damageEvents > 0 && rogue.Shields.CurrentShields < beforeShield && !rogue.WasDamagedByPlayer;
        }

        private static bool PoliceSalvageUsesCarriedWeapons()
        {
            List<SalvageDrop> drops = SalvageForFaction(FactionManager.LibertyPolice, "Phase40 Police Salvage", heavy: false);
            return drops.Count > 0 &&
                   drops.All(drop => drop.EquipmentId == "liberty_pulse_cannon" || drop.EquipmentId == "liberty_light_laser");
        }

        private static bool RogueSalvageUsesCarriedWeapons()
        {
            List<SalvageDrop> drops = SalvageForFaction(FactionManager.LibertyRogues, "Phase40 Rogue Salvage", heavy: false);
            NpcLoadoutPolicyProfile profile = NpcEquipmentLoadoutFactory.GetProfileForFaction(FactionManager.LibertyRogues);
            return drops.Count > 0 && drops.All(drop => profile.PreferredWeaponIds.Contains(drop.EquipmentId, StringComparer.OrdinalIgnoreCase));
        }

        private static bool SalvageCannotInventWeapons()
        {
            List<SalvageDrop> drops = SalvageForFaction(FactionManager.LibertyRogues, "Phase40 Salvage Boundary", heavy: true);
            NpcShip source = CreateCombatNpc("Phase40 Salvage Boundary 0", FactionManager.LibertyRogues, modelPath: "SHIPS/WARTHOG/warthog");
            HashSet<string> carried = new(WeaponIds(source.Loadout), StringComparer.OrdinalIgnoreCase);
            return drops.Count > 0 && drops.All(drop => carried.Contains(drop.EquipmentId));
        }

        private static bool CommodityPolicyIsIndependent()
        {
            CombatSalvageService service = new();
            string selectedName = null;
            for (int i = 0; i < 10_000; i++)
            {
                string name = $"Phase40 Commodity Independence {i}";
                NpcShip armed = CreateCombatNpc(name, FactionManager.LibertyPolice);
                NpcShip empty = new(name, Vector3.Zero, Vector3.Zero, 1f, 0f, FactionManager.LibertyPolice);
                RecordCommodityCandidate(service, armed, ref selectedName);
                if (selectedName != null)
                {
                    bool armedDecision = service.ShouldDrop(armed);
                    empty.ConfigureTrafficBehavior(
                        TrafficZoneBehaviorType.PirateAmbush,
                        "phase40-smoke",
                        Vector3.Zero,
                        500f,
                        100f,
                        8000f);
                    bool emptyDecision = service.ShouldDrop(empty);
                    armed.ApplyDamage(armed.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
                    IReadOnlyList<SalvageDrop> drops = new CombatSalvageService().EvaluateDestruction(armed);
                    return armedDecision == emptyDecision && armedDecision && drops.Any(drop => drop.IsCommodity);
                }
            }

            return false;
        }

        private static bool SpawnRoleCoverage()
        {
            ShipLoadout ambient = BuildLoadout("Patrol Fighter 1", FactionManager.LibertyPolice, role: TrafficZoneBehaviorType.LawfulPatrol);
            ShipLoadout missionTarget = BuildLoadout("Rogue Hunt target", FactionManager.LibertyRogues, role: TrafficZoneBehaviorType.PirateAmbush);
            ShipLoadout escort = BuildLoadout("Escort Convoy", FactionManager.LibertyCorporations, modelPath: string.Empty, role: TrafficZoneBehaviorType.TraderRoute);
            return ambient.HasMountedGun() && missionTarget.HasMountedGun() && !escort.HasMountedGun();
        }

        private static bool SaveSchemaRemainsVersionTen() => SaveGameData.CurrentSchemaVersion == 10;

        private static void RecordCommodityCandidate(CombatSalvageService service, NpcShip ship, ref string selectedName)
        {
            if (selectedName == null && service.ShouldDrop(ship))
            {
                selectedName = ship.Name;
            }
        }

        private static List<SalvageDrop> SalvageForFaction(string factionId, string prefix, bool heavy)
        {
            CombatSalvageService service = new();
            for (int i = 0; i < 10_000; i++)
            {
                NpcShip ship = CreateCombatNpc(
                    $"{prefix} {i}",
                    factionId,
                    modelPath: heavy ? "SHIPS/WARTHOG/warthog" : string.Empty);
                if (!service.ShouldDropEquipment(ship))
                {
                    continue;
                }

                ship.ApplyDamage(ship.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
                return new CombatSalvageService().EvaluateDestruction(ship)
                    .Where(drop => drop.IsEquipment)
                    .ToList();
            }

            return new List<SalvageDrop>();
        }

        private static NpcShip CreateCombatNpc(
            string name,
            string factionId,
            Vector3? position = null,
            bool heavy = false,
            string modelPath = null)
        {
            Vector3 start = position ?? Vector3.Zero;
            string resolvedModelPath = modelPath ?? (heavy ? "SHIPS/WARTHOG/warthog" : string.Empty);
            NpcShip ship = new(name, start, start, 1f, 0f, factionId)
            {
                ModelPath = resolvedModelPath
            };
            ship.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.PirateAmbush,
                "phase40-smoke",
                start,
                500f,
                100f,
                8000f);
            ship.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                name,
                factionId,
                resolvedModelPath,
                TrafficZoneBehaviorType.PirateAmbush,
                NpcLoadoutTier.Standard));
            return ship;
        }

        private static ShipLoadout BuildLoadout(
            string name,
            string factionId,
            string modelPath = "SMOKE/fighter",
            TrafficZoneBehaviorType role = TrafficZoneBehaviorType.LawfulPatrol,
            NpcLoadoutTier tier = NpcLoadoutTier.Standard,
            bool heavy = false)
        {
            string resolvedModelPath = heavy ? "SHIPS/WARTHOG/warthog" : modelPath;
            return NpcEquipmentLoadoutFactory.CreateForNpc(name, factionId, resolvedModelPath, role, tier);
        }

        private static List<string> WeaponIds(ShipLoadout loadout) =>
            loadout?.GetMountedGuns().Select(weapon => weapon.Id).ToList() ?? new List<string>();

        private static int GunHardpointCapacity(ShipLoadout loadout) =>
            loadout?.Hardpoints.Count(hardpoint =>
                hardpoint?.AllowedEquipmentTypes?.Contains(EquipmentType.Gun) == true) ?? 0;

        private static GameTime Frame(float seconds) =>
            new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

        private static bool RunSilenced(Func<bool> assertion)
        {
            TextWriter previous = Console.Out;
            using StringWriter sink = new();
            Console.SetOut(sink);
            try
            {
                return assertion();
            }
            finally
            {
                Console.SetOut(previous);
            }
        }
    }
}
