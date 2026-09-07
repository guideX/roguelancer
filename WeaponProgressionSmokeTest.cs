using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Phase 41 coverage for canonical weapon progression across catalog,
    /// faction/tier policy, real NPC combat, salvage, dealer transactions,
    /// and save/load identity persistence.
    /// </summary>
    internal sealed class WeaponProgressionSmokeTest
    {
        private static readonly string[] OriginalWeaponIds =
        {
            "liberty_light_laser",
            "liberty_pulse_cannon",
            "rogue_blaster"
        };

        private static readonly string[] NewWeaponIds =
        {
            "liberty_sentry_blaster",
            "liberty_ranger_laser",
            "liberty_heavy_pulse",
            "rogue_needle_blaster",
            "rogue_scattergun",
            "rogue_rail_cannon"
        };

        private readonly GraphicsDevice _graphicsDevice;
        private int _passed;
        private int _failed;

        public WeaponProgressionSmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        }

        public (int Passed, int Failed) Run()
        {
            Check("canonical catalog has the bounded expanded gun pool", CatalogIsExpandedAndValid);
            Check("original canonical weapon IDs remain stable", OriginalIdsRemainStable);
            Check("progression metadata is authored and ordered", ProgressionMetadataIsValid);
            Check("low and standard tiers exclude stronger guns", TierAvailabilityIsBounded);
            Check("hard tier can select high progression equipment", HighTierCanReachHighWeapons);
            Check("faction profiles create distinct identities", FactionProfilesAreDistinct);
            Check("difficulty changes real loadout quality", DifficultyChangesLoadoutQuality);
            Check("standard and heavy ships respect actual gun capacity", ShipClassCapacityIsRespected);
            Check("rogue heavy fighters can mount a deterministic mixed loadout", MixedLoadoutIsCanonical);
            Check("ordinary civilian traffic remains unarmed", CivilianTrafficRemainsUnarmed);
            Check("real NPC combat consumes a new canonical gun", NewGunFiresThroughNpcSystem);
            Check("new gun range and refire cadence reach NPC combat", NewGunRangeAndCadenceAreHonored);
            Check("player WeaponSystem consumes a mounted new gun profile", NewGunWorksThroughPlayerWeaponSystem);
            Check("mounted new gun becomes exact Phase 39 salvage", NewGunSalvagePreservesMountedId);
            Check("dealer exposes the new gun through normal lifecycle", DealerLifecycleWorksForNewGun);
            Check("new owned and mounted gun survives save/load", SaveLoadPreservesNewGun);

            Console.WriteLine($"[WEAPON PROGRESSION SMOKE] RESULT: {_passed} passed, {_failed} failed");
            return (_passed, _failed);
        }

        private void Check(string label, Func<bool> assertion)
        {
            try
            {
                if (RunSilenced(assertion))
                {
                    _passed++;
                    Console.WriteLine($"[WEAPON PROGRESSION SMOKE] PASS {label}");
                }
                else
                {
                    _failed++;
                    Console.WriteLine($"[WEAPON PROGRESSION SMOKE] FAIL {label}: assertion returned false");
                }
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"[WEAPON PROGRESSION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static bool CatalogIsExpandedAndValid()
        {
            List<WeaponEquipmentDefinition> guns = EquipmentCatalog.GetAll()
                .OfType<WeaponEquipmentDefinition>()
                .Where(weapon => weapon.EquipmentType == EquipmentType.Gun)
                .ToList();
            HashSet<string> ids = new(guns.Select(weapon => weapon.Id), StringComparer.OrdinalIgnoreCase);
            return guns.Count == 9 && ids.Count == guns.Count &&
                   NewWeaponIds.All(ids.Contains) &&
                   guns.All(HasValidCombatStats);
        }

        private static bool OriginalIdsRemainStable()
        {
            return OriginalWeaponIds.All(id => EquipmentCatalog.GetById(id) is WeaponEquipmentDefinition weapon &&
                weapon.EquipmentType == EquipmentType.Gun && HasValidCombatStats(weapon));
        }

        private static bool ProgressionMetadataIsValid()
        {
            List<WeaponEquipmentDefinition> guns = EquipmentCatalog.GetAll()
                .OfType<WeaponEquipmentDefinition>()
                .Where(weapon => weapon.EquipmentType == EquipmentType.Gun)
                .ToList();
            return guns.All(weapon => Enum.IsDefined(typeof(WeaponFamily), weapon.Family) &&
                                      Enum.IsDefined(typeof(WeaponProgressionTier), weapon.ProgressionTier)) &&
                   guns.Any(weapon => weapon.Family == WeaponFamily.Liberty && weapon.ProgressionTier == WeaponProgressionTier.High) &&
                   guns.Any(weapon => weapon.Family == WeaponFamily.Rogue && weapon.ProgressionTier == WeaponProgressionTier.High);
        }

        private static bool TierAvailabilityIsBounded()
        {
            ShipLoadout low = BuildLoadout("Tier Bound Police Fighter", FactionManager.LibertyPolice, tier: NpcLoadoutTier.Low);
            ShipLoadout standard = BuildLoadout("Tier Bound Police Fighter", FactionManager.LibertyPolice, tier: NpcLoadoutTier.Standard);
            return low.GetMountedGuns().Count() == 1 &&
                   low.GetMountedGuns().All(weapon => weapon.ProgressionTier == WeaponProgressionTier.Low) &&
                   standard.GetMountedGuns().Count() == 2 &&
                   standard.GetMountedGuns().All(weapon => weapon.ProgressionTier != WeaponProgressionTier.High);
        }

        private static bool HighTierCanReachHighWeapons()
        {
            ShipLoadout police = BuildLoadout("High Police Fighter", FactionManager.LibertyPolice, tier: NpcLoadoutTier.High);
            ShipLoadout rogue = BuildLoadout("High Rogue Fighter", FactionManager.LibertyRogues, tier: NpcLoadoutTier.High);
            return police.GetMountedGuns().Any(weapon => weapon.ProgressionTier == WeaponProgressionTier.High) &&
                   rogue.GetMountedGuns().Any(weapon => weapon.ProgressionTier == WeaponProgressionTier.High);
        }

        private static bool FactionProfilesAreDistinct()
        {
            List<string> police = WeaponIds(BuildLoadout("Identity Fighter", FactionManager.LibertyPolice));
            List<string> navy = WeaponIds(BuildLoadout("Identity Fighter", FactionManager.LibertyNavy));
            List<string> rogue = WeaponIds(BuildLoadout("Identity Fighter", FactionManager.LibertyRogues));
            List<string> bounty = WeaponIds(BuildLoadout("Identity Fighter", FactionManager.BountyHunters));
            return police.Contains("liberty_pulse_cannon", StringComparer.OrdinalIgnoreCase) &&
                   navy.Contains("liberty_ranger_laser", StringComparer.OrdinalIgnoreCase) &&
                   rogue.Contains("rogue_blaster", StringComparer.OrdinalIgnoreCase) &&
                   bounty.Count > 0 &&
                   (!police.SequenceEqual(rogue, StringComparer.OrdinalIgnoreCase) ||
                    !navy.SequenceEqual(bounty, StringComparer.OrdinalIgnoreCase));
        }

        private static bool DifficultyChangesLoadoutQuality()
        {
            ShipLoadout easy = BuildLoadout("Difficulty Police Fighter", FactionManager.LibertyPolice, tier: NpcLoadoutTier.Low);
            ShipLoadout hard = BuildLoadout("Difficulty Police Fighter", FactionManager.LibertyPolice, tier: NpcLoadoutTier.High);
            float easyDamage = easy.GetMountedGuns().Sum(weapon => weapon.Damage);
            float hardDamage = hard.GetMountedGuns().Sum(weapon => weapon.Damage);
            return hardDamage > easyDamage &&
                   hard.GetMountedGuns().Any(weapon => weapon.ProgressionTier == WeaponProgressionTier.High);
        }

        private static bool ShipClassCapacityIsRespected()
        {
            ShipLoadout standard = BuildLoadout("Standard Fighter", FactionManager.LibertyPolice);
            ShipLoadout heavy = BuildLoadout(
                "Warthog Heavy Fighter",
                FactionManager.LibertyNavy,
                "SHIPS/WARTHOG/warthog",
                TrafficZoneBehaviorType.LawfulPatrol,
                NpcLoadoutTier.High);
            int capacity = GunHardpointCapacity(standard);
            int heavyCapacity = GunHardpointCapacity(heavy);
            return standard.GetMountedGuns().Count() >= 1 && standard.GetMountedGuns().Count() <= Math.Min(2, capacity) &&
                   heavy.GetMountedGuns().Count() == Math.Min(2, heavyCapacity) &&
                   heavy.GetMountedGuns().All(NpcEquipmentLoadoutFactory.IsValidNpcWeapon);
        }

        private static bool MixedLoadoutIsCanonical()
        {
            ShipLoadout loadout = BuildLoadout(
                "Warthog Mixed Rogue",
                FactionManager.LibertyRogues,
                "SHIPS/WARTHOG/warthog",
                TrafficZoneBehaviorType.PirateAmbush,
                NpcLoadoutTier.Standard);
            List<WeaponEquipmentDefinition> guns = loadout.GetMountedGuns().ToList();
            return guns.Count == 2 && guns.Select(weapon => weapon.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2 &&
                   guns.All(weapon => EquipmentCatalog.GetById(weapon.Id) == weapon);
        }

        private static bool CivilianTrafficRemainsUnarmed()
        {
            ShipLoadout civilian = BuildLoadout(
                "Transport Ship Alpha",
                FactionManager.NeutralCivilians,
                "SHIPS/PI_TRANSPORT/PI_TRANSPORT",
                TrafficZoneBehaviorType.TraderRoute,
                NpcLoadoutTier.High);
            ShipLoadout escort = BuildLoadout(
                "Transport Escort Fighter",
                FactionManager.NeutralCivilians,
                string.Empty,
                TrafficZoneBehaviorType.TraderRoute,
                NpcLoadoutTier.High);
            return !civilian.HasMountedGun() && escort.GetMountedGuns().Count() == 1;
        }

        private bool NewGunFiresThroughNpcSystem()
        {
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("rogue_rail_cannon") as WeaponEquipmentDefinition;
            NpcShip attacker = CreateSingleGunNpc("Phase41 Combat Attacker", FactionManager.LibertyRogues, gun, Vector3.Zero);
            NpcShip target = CreateCombatTarget("Phase41 Combat Target", FactionManager.LibertyPolice, new Vector3(300f, 0f, 0f));
            attacker.SetFactionCombatTarget(target);

            string firedId = string.Empty;
            float firedDamage = 0f;
            int damageEvents = 0;
            NpcWeaponSystem system = new(_graphicsDevice);
            system.NpcWeaponFired += (_, _, id, weapon) =>
            {
                firedId = id;
                firedDamage = weapon.Damage;
            };
            system.NpcShipDamaged += (_, _, damage) =>
            {
                if (damage > 0f) damageEvents++;
            };

            system.Update(Frame(0.1f), new List<NpcShip> { attacker, target }, null);
            system.Update(Frame(0.1f), new List<NpcShip> { attacker, target }, null);
            return firedId == gun.Id && Nearly(firedDamage, gun.Damage) && damageEvents > 0;
        }

        private bool NewGunRangeAndCadenceAreHonored()
        {
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("rogue_rail_cannon") as WeaponEquipmentDefinition;
            NpcShip attacker = CreateSingleGunNpc("Phase41 Range Attacker", FactionManager.LibertyRogues, gun, Vector3.Zero);
            NpcShip inRangeTarget = CreateCombatTarget("Phase41 In Range", FactionManager.LibertyPolice, new Vector3(5000f, 0f, 0f));
            attacker.SetFactionCombatTarget(inRangeTarget);
            int fired = 0;
            NpcWeaponSystem system = new(_graphicsDevice);
            system.NpcWeaponFired += (_, _, _, _) => fired++;

            system.Update(Frame(0.1f), new List<NpcShip> { attacker, inRangeTarget }, null);
            system.Update(Frame(0.1f), new List<NpcShip> { attacker, inRangeTarget }, null);
            bool blockedByRefire = fired == 1;
            system.Update(Frame(gun.RefireRate), new List<NpcShip> { attacker, inRangeTarget }, null);
            bool refired = fired == 2;
            bool projectileWasCreated = system.ActiveProjectileCount > 0;
            attacker.ClearFactionCombatTarget();
            system.Update(Frame(6f), new List<NpcShip> { attacker, inRangeTarget }, null);
            return blockedByRefire && refired && projectileWasCreated && system.ActiveProjectileCount == 0;
        }

        private bool NewGunWorksThroughPlayerWeaponSystem()
        {
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_ranger_laser") as WeaponEquipmentDefinition;
            WeaponSystem system = new(_graphicsDevice)
            {
                CurrentWeapon = gun.WeaponType
            };
            WeaponSystem.WeaponStats defaults = system.GetWeaponStats(gun.WeaponType);
            system.SetWeaponProfileOverride(gun.WeaponType, new WeaponSystem.WeaponStats
            {
                WeaponDamage = gun.Damage,
                Speed = gun.ProjectileSpeed,
                Range = gun.Range,
                Life = gun.Range / gun.ProjectileSpeed,
                Size = defaults.Size,
                Color = defaults.Color,
                MuzzleFlashSize = defaults.MuzzleFlashSize,
                RefireRate = gun.RefireRate,
                EnergyCost = gun.EnergyCost
            });

            system.Fire(Vector3.Zero, Vector3.Right, Vector3.Zero);
            system.Update(Frame(0.05f));
            List<HitInfo> hits = system.CheckCollisions(
                new Vector3(gun.ProjectileSpeed * 0.05f, 0f, 0f),
                50f,
                new HullIntegrity(100f));
            return hits.Count == 2 && hits.All(hit => hit.WeaponType == gun.WeaponType && Nearly(hit.Damage, gun.Damage));
        }

        private static bool NewGunSalvagePreservesMountedId()
        {
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("rogue_rail_cannon") as WeaponEquipmentDefinition;
            CombatSalvageService salvage = new();
            NpcShip destroyed = null;
            for (int i = 0; i < 10_000; i++)
            {
                NpcShip candidate = CreateSingleGunNpc(
                    $"Phase41 Salvage Heavy {i}",
                    FactionManager.LibertyRogues,
                    gun,
                    new Vector3(1000f, 0f, 0f),
                    heavy: true);
                if (salvage.ShouldDropEquipment(candidate))
                {
                    destroyed = candidate;
                    break;
                }
            }

            if (destroyed == null)
            {
                return false;
            }

            destroyed.ApplyDamage(destroyed.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
            LootManager manager = new(null, null, null, null, salvage);
            manager.SpawnLootForDestroyedNpc(destroyed);
            CargoPod pod = manager.ActivePods.FirstOrDefault(active => active.IsEquipment);
            if (pod == null || !string.Equals(pod.EquipmentId, gun.Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Ship player = new(Vector3.Zero);
            player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            pod.Position = player.Position;
            manager.Update(Frame(0.1f), player, false);
            return manager.ActivePods.All(active => !active.IsEquipment) &&
                   player.Loadout.GetOwnedCount(gun.Id) == 1;
        }

        private static bool DealerLifecycleWorksForNewGun()
        {
            WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("rogue_needle_blaster") as WeaponEquipmentDefinition;
            EquipmentDealer dealer = new();
            Ship player = new(Vector3.Zero);
            player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            PlayerCredits credits = new(gun.Price + 100);
            bool bought = dealer.TryBuyEquipment(gun, credits, player, out _);
            bool mounted = bought && dealer.TryMountEquipment(gun, player, out _);
            bool unmounted = mounted && dealer.TryUnmountEquipment(gun, player, out _);
            int resale = dealer.GetResaleValue(gun);
            bool sold = unmounted && dealer.TrySellUnequippedEquipment(gun, credits, player, out _);
            return dealer.AvailableEquipment.Any(item => item.Id == gun.Id) && bought && mounted && unmounted && sold &&
                   player.Loadout.GetOwnedCount(gun.Id) == 0 && credits.Credits == 100 + resale;
        }

        private static bool SaveLoadPreservesNewGun()
        {
            string path = Path.Combine(Path.GetTempPath(), $"roguelancer_phase41_{Guid.NewGuid():N}.json");
            try
            {
                WeaponEquipmentDefinition gun = EquipmentCatalog.GetById("liberty_ranger_laser") as WeaponEquipmentDefinition;
                Ship source = new(Vector3.Zero);
                source.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
                source.Loadout.AddOwnedEquipment(gun, 1);
                source.Loadout.TryMountEquipment(gun, out _);
                SaveGameManager manager = new(path);
                SaveGameData data = new()
                {
                    OwnedEquipment = manager.CaptureOwnedEquipment(source.Loadout),
                    MountedEquipment = manager.CaptureMountedEquipment(source.Loadout)
                };
                bool saved = manager.TrySave(data, out _);
                bool loaded = manager.TryLoad(out SaveGameData restored, out _);
                ShipLoadout rebuilt = manager.BuildLoadout(restored, out List<string> warnings);
                return saved && loaded && SaveGameData.CurrentSchemaVersion == 11 && warnings.Count == 0 &&
                       rebuilt.GetOwnedCount(gun.Id) == 1 && rebuilt.GetMountedCount(gun.Id) == 1;
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static ShipLoadout BuildLoadout(
            string archetype,
            string factionId,
            string modelPath = "",
            TrafficZoneBehaviorType role = TrafficZoneBehaviorType.LawfulPatrol,
            NpcLoadoutTier tier = NpcLoadoutTier.Standard)
        {
            return NpcEquipmentLoadoutFactory.CreateForNpc(archetype, factionId, modelPath, role, tier);
        }

        private static NpcShip CreateSingleGunNpc(
            string name,
            string factionId,
            WeaponEquipmentDefinition gun,
            Vector3 position,
            bool heavy = false)
        {
            NpcShip npc = new(name, position, position, 1000f, 0f, factionId)
            {
                ModelPath = heavy ? "SHIPS/WARTHOG/warthog" : string.Empty
            };
            npc.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.PirateAmbush,
                "phase41-smoke",
                position,
                1000f,
                120f,
                20_000f);
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            PowerplantEquipmentDefinition powerplant = (PowerplantEquipmentDefinition)EquipmentCatalog.GetById("civilian_powerplant");
            loadout.AddOwnedEquipment(powerplant);
            loadout.TryMountEquipment(powerplant, out _);
            loadout.AddOwnedEquipment(gun, 1);
            loadout.TryMountEquipment(gun, out _);
            npc.SetLoadout(loadout);
            return npc;
        }

        private static NpcShip CreateCombatTarget(string name, string factionId, Vector3 position)
        {
            NpcShip target = new(name, position, position, 1000f, 0f, factionId);
            target.Radius = 1000f;
            target.Shields.RegenDelay = 10_000f;
            target.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.LawfulPatrol,
                "phase41-target",
                position,
                1000f,
                120f,
                20_000f);
            return target;
        }

        private static List<string> WeaponIds(ShipLoadout loadout) =>
            loadout.GetMountedGuns().Select(weapon => weapon.Id).ToList();

        private static int GunHardpointCapacity(ShipLoadout loadout) =>
            loadout.Hardpoints.Count(hardpoint =>
                hardpoint?.AllowedEquipmentTypes?.Contains(EquipmentType.Gun) == true);

        private static bool HasValidCombatStats(WeaponEquipmentDefinition weapon) =>
            weapon != null &&
            weapon.Price > 0 &&
            IsFinitePositive(weapon.Damage) &&
            IsFinitePositive(weapon.ProjectileSpeed) &&
            IsFinitePositive(weapon.RefireRate) &&
            IsFinitePositive(weapon.EnergyCost) &&
            IsFinitePositive(weapon.Range) &&
            Enum.IsDefined(typeof(WeaponType), weapon.WeaponType);

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0001f;

        private static GameTime Frame(float seconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
            return new GameTime(elapsed, elapsed);
        }

        private static bool RunSilenced(Func<bool> action)
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
    }
}
