using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 42 coverage for the canonical shield equipment lifecycle.
    /// </summary>
    internal sealed class ShieldEquipmentSmokeTest
    {
        private readonly GraphicsDevice _graphicsDevice;
        private int _passed;
        private int _failed;

        private static readonly string[] ShieldIds =
        {
            "civilian_shield_generator",
            "liberty_patrol_shield",
            "liberty_military_shield",
            "liberty_heavy_shield",
            "rogue_scrap_shield",
            "rogue_combat_shield",
            "professional_deflector"
        };

        public ShieldEquipmentSmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        }

        public (int Passed, int Failed) Run()
        {
            Check("catalog definitions and Phase 41 guns remain valid", CatalogIsValid);
            Check("shield and gun mounts remain type-safe", MountCompatibilityIsSafe);
            Check("shield absorbs damage before hull", BasicAbsorption);
            Check("shield overflow reaches hull exactly", OverflowIsExact);
            Check("shield regeneration honors delay, rate, and clamp", RegenerationIsBounded);
            Check("NPC projectile damages player shield first", NpcToPlayerUsesShield);
            Check("player projectile damages NPC shield first", PlayerToNpcUsesShield);
            Check("NPC versus NPC combat uses canonical shield state", NpcToNpcUsesShield);
            Check("faction shield policies are canonical", FactionPoliciesAreCanonical);
            Check("difficulty and class progression are deterministic", DifficultyAndClassProgression);
            Check("civilian traffic can be shielded without guns", CivilianTrafficCanBeDefensive);
            Check("shield salvage preserves mounted canonical ID", ShieldSalvagePreservesMountedId);
            Check("unmounted shields never appear as salvage", UnmountedShieldDoesNotDrop);
            Check("pickup transfers shield through normal ownership", ShieldPickupTransfersOwnership);
            Check("dealer supports shield buy mount unmount resale", DealerLifecycleWorks);
            Check("save/load preserves shield ownership and mount", SaveLoadPreservesShield);
            Check("shield hits preserve attribution and destruction source", AttributionSurvivesShield);
            Check("invalid shield definitions fail safely", InvalidShieldFailsSafe);

            Console.WriteLine($"[SHIELD EQUIPMENT SMOKE] RESULT: {_passed} passed, {_failed} failed");
            return (_passed, _failed);
        }

        private void Check(string label, Func<bool> assertion)
        {
            try
            {
                if (RunSilenced(assertion))
                {
                    _passed++;
                    Console.WriteLine($"[SHIELD EQUIPMENT SMOKE] PASS {label}");
                }
                else
                {
                    _failed++;
                    Console.WriteLine($"[SHIELD EQUIPMENT SMOKE] FAIL {label}: assertion returned false");
                }
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"[SHIELD EQUIPMENT SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static bool CatalogIsValid()
        {
            List<ShieldEquipmentDefinition> shields = EquipmentCatalog.GetAll()
                .OfType<ShieldEquipmentDefinition>()
                .Where(shield => shield.EquipmentType == EquipmentType.ShieldGenerator)
                .ToList();
            HashSet<string> ids = new(shields.Select(shield => shield.Id), StringComparer.OrdinalIgnoreCase);
            int phase41GunCount = EquipmentCatalog.GetAll()
                .OfType<WeaponEquipmentDefinition>()
                .Count(weapon => weapon.EquipmentType == EquipmentType.Gun);
            return shields.Count == ShieldIds.Length && ids.Count == shields.Count &&
                   ShieldIds.All(ids.Contains) && shields.All(shield => shield.IsValid) &&
                   phase41GunCount == 9;
        }

        private static bool MountCompatibilityIsSafe()
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            ShieldEquipmentDefinition first = Shield("civilian_shield_generator");
            ShieldEquipmentDefinition second = Shield("liberty_patrol_shield");
            WeaponEquipmentDefinition gun = (WeaponEquipmentDefinition)EquipmentCatalog.GetById("liberty_light_laser");
            loadout.AddOwnedEquipment(first);
            loadout.AddOwnedEquipment(second);
            loadout.AddOwnedEquipment(gun);
            bool shieldMounted = loadout.TryMountEquipment("ShieldGenerator", first, out _);
            bool secondShieldRejected = !loadout.TryMountEquipment(second, out _);
            bool shieldInGunRejected = !loadout.TryMountEquipment("PrimaryGunLeft", second, out _);
            bool gunInShieldRejected = !loadout.TryMountEquipment("ShieldGenerator", gun, out _);
            return shieldMounted && secondShieldRejected && shieldInGunRejected && gunInShieldRejected &&
                   loadout.GetMountedShield()?.Id == first.Id && loadout.GetMountedShield() != null;
        }

        private static bool BasicAbsorption()
        {
            ShieldSystem shields = new(Shield("liberty_patrol_shield"));
            HullIntegrity hull = new(100f);
            float overflow = shields.AbsorbDamage(25f);
            return Nearly(overflow, 0f) && Nearly(shields.CurrentShields, 40f) &&
                   Nearly(hull.CurrentHull, 100f);
        }

        private static bool OverflowIsExact()
        {
            ShieldSystem shields = new(Shield("rogue_scrap_shield"));
            HullIntegrity hull = new(100f);
            float overflow = shields.AbsorbDamage(60f);
            hull.TakeDamage(overflow);
            return Nearly(shields.CurrentShields, 0f) && Nearly(overflow, 15f) &&
                   Nearly(hull.CurrentHull, 85f) && overflow >= 0f;
        }

        private static bool RegenerationIsBounded()
        {
            ShieldEquipmentDefinition definition = Shield("civilian_shield_generator");
            ShieldSystem shields = new(definition);
            shields.AbsorbDamage(30f);
            float afterHit = shields.CurrentShields;
            shields.Update(Frame(2.9f));
            bool delayed = Nearly(shields.CurrentShields, afterHit);
            shields.Update(Frame(0.1f));
            bool beganAtDelayBoundary = shields.CurrentShields > afterHit;
            float afterDelay = shields.CurrentShields;
            shields.Update(Frame(1f));
            bool regenerated = Nearly(shields.CurrentShields, afterDelay + definition.RegenerationRate);
            shields.Update(Frame(100f));
            bool clamped = Nearly(shields.CurrentShields, definition.Capacity);
            shields.AbsorbDamage(5f);
            float resetValue = shields.CurrentShields;
            shields.Update(Frame(1f));
            bool reset = Nearly(shields.CurrentShields, resetValue);
            return delayed && beganAtDelayBoundary && regenerated && clamped && reset;
        }

        private bool NpcToPlayerUsesShield()
        {
            Ship player = CreatePlayerWithShield("civilian_shield_generator");
            NpcShip attacker = CreateNpcWithGun("Shield Smoke NPC Attacker", FactionManager.LibertyRogues, "rogue_rail_cannon", Vector3.Zero);
            player.Position = new Vector3(100f, 0f, 0f);
            player.CollisionRadius = 1000f;
            float shieldBefore = player.Shields.CurrentShields;
            float hullBefore = player.Hull.CurrentHull;
            NpcWeaponSystem system = new(_graphicsDevice);
            system.Update(Frame(0.1f), new List<NpcShip> { attacker }, player);
            system.Update(Frame(0.07f), new List<NpcShip> { attacker }, player);
            return player.Shields.CurrentShields < shieldBefore && Nearly(player.Hull.CurrentHull, hullBefore);
        }

        private bool PlayerToNpcUsesShield()
        {
            NpcShip target = CreateNpcWithShield("Shield Smoke NPC Target", FactionManager.LibertyPolice, "liberty_patrol_shield");
            target.Radius = 1000f;
            WeaponEquipmentDefinition weapon = (WeaponEquipmentDefinition)EquipmentCatalog.GetById("liberty_light_laser");
            WeaponSystem system = CreateWeaponSystem(weapon);
            float shieldBefore = target.Shields.CurrentShields;
            float hullBefore = target.Hull.CurrentHull;
            system.Fire(Vector3.Zero, Vector3.Right, Vector3.Zero);
            system.Update(Frame(0.05f));
            List<HitInfo> hits = system.CheckCollisions(target.Position, target.Radius, target.Hull, target.Shields, target);
            return hits.Count > 0 && target.Shields.CurrentShields < shieldBefore &&
                Nearly(target.Hull.CurrentHull, hullBefore) && target.WasDamagedByPlayer;
        }

        private bool NpcToNpcUsesShield()
        {
            NpcShip attacker = CreateNpcWithGun("Shield Smoke Police", FactionManager.LibertyPolice, "liberty_pulse_cannon", Vector3.Zero);
            NpcShip target = CreateNpcWithShield("Shield Smoke Rogue", FactionManager.LibertyRogues, "rogue_combat_shield");
            target.Position = new Vector3(300f, 0f, 0f);
            target.Radius = 1000f;
            attacker.SetFactionCombatTarget(target);
            float shieldBefore = target.Shields.CurrentShields;
            float hullBefore = target.Hull.CurrentHull;
            NpcWeaponSystem system = new(_graphicsDevice);
            system.Update(Frame(0.1f), new List<NpcShip> { attacker, target }, new Ship(new Vector3(100_000f, 0f, 0f)));
            system.Update(Frame(0.1f), new List<NpcShip> { attacker, target }, new Ship(new Vector3(100_000f, 0f, 0f)));
            return target.Shields.CurrentShields < shieldBefore && Nearly(target.Hull.CurrentHull, hullBefore) &&
                   !target.WasDamagedByPlayer;
        }

        private static bool FactionPoliciesAreCanonical()
        {
            (string Name, string Faction, string Expected)[] cases =
            {
                ("Police Patrol Fighter", FactionManager.LibertyPolice, "liberty_patrol_shield"),
                ("Navy Patrol Fighter", FactionManager.LibertyNavy, "professional_deflector"),
                ("Rogue Attack Fighter", FactionManager.LibertyRogues, "rogue_combat_shield"),
                ("Corporate Security Fighter", FactionManager.LibertyCorporations, "professional_deflector"),
                ("Bounty Hunter Fighter", FactionManager.BountyHunters, "professional_deflector"),
                ("Junker Fighter", FactionManager.Junkers, "civilian_shield_generator")
            };
            return cases.All(test => NpcEquipmentLoadoutFactory.CreateForNpc(
                test.Name,
                test.Faction,
                "SMOKE/fighter",
                TrafficZoneBehaviorType.PirateAmbush,
                NpcLoadoutTier.Standard).GetMountedShield()?.Id == test.Expected);
        }

        private static bool DifficultyAndClassProgression()
        {
            ShipLoadout low = NpcEquipmentLoadoutFactory.CreateForNpc(
                "Police Fighter", FactionManager.LibertyPolice, "SMOKE/fighter",
                TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Low);
            ShipLoadout highHeavy = NpcEquipmentLoadoutFactory.CreateForNpc(
                "Police Warthog Heavy Fighter", FactionManager.LibertyPolice, "SHIPS/WARTHOG/warthog",
                TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.High);
            ShipLoadout repeated = NpcEquipmentLoadoutFactory.CreateForNpc(
                "Police Warthog Heavy Fighter", FactionManager.LibertyPolice, "SHIPS/WARTHOG/warthog",
                TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.High);
            ShieldEquipmentDefinition lowShield = low.GetMountedShield();
            ShieldEquipmentDefinition highShield = highHeavy.GetMountedShield();
            return lowShield != null && highShield != null &&
                   lowShield.ProgressionTier == ShieldProgressionTier.Low &&
                   highShield.ProgressionTier == ShieldProgressionTier.High &&
                   highShield.Capacity > lowShield.Capacity &&
                   highHeavy.GetMountedSummary() == repeated.GetMountedSummary();
        }

        private static bool CivilianTrafficCanBeDefensive()
        {
            ShipLoadout trader = NpcEquipmentLoadoutFactory.CreateForNpc(
                "Transport Ship Alpha",
                FactionManager.NeutralCivilians,
                "SHIPS/PI_TRANSPORT/PI_TRANSPORT",
                TrafficZoneBehaviorType.TraderRoute,
                NpcLoadoutTier.High);
            return trader.GetMountedShield()?.Id == "civilian_shield_generator" && !trader.HasMountedGun();
        }

        private static bool ShieldSalvagePreservesMountedId()
        {
            CombatSalvageService service = new();
            string name = null;
            NpcShip destroyed = null;
            for (int i = 0; i < 10_000; i++)
            {
                NpcShip candidate = CreateNpcWithShield(
                    $"Shield Salvage Heavy {i}",
                    FactionManager.LibertyRogues,
                    "rogue_combat_shield",
                    heavy: true);
                if (service.ShouldDropShield(candidate))
                {
                    name = candidate.Name;
                    destroyed = candidate;
                    break;
                }
            }

            if (destroyed == null || name == null)
                return false;

            string mountedId = destroyed.Loadout.GetMountedShield().Id;
            destroyed.ApplyDamage(destroyed.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
            IReadOnlyList<SalvageDrop> drops = service.EvaluateDestruction(destroyed);
            SalvageDrop shieldDrop = drops.FirstOrDefault(drop => drop.IsEquipment && drop.EquipmentId == mountedId);
            if (shieldDrop == null || drops.Count(drop => drop.IsEquipment && drop.EquipmentId == mountedId) != 1)
                return false;

            LootManager manager = new(null, null, null, null, new CombatSalvageService());
            manager.SpawnLootForDestroyedNpc(destroyed);
            return manager.ActivePods.Any(pod => pod.IsEquipment && pod.EquipmentId == mountedId);
        }

        private static bool UnmountedShieldDoesNotDrop()
        {
            CombatSalvageService service = new();
            NpcShip ship = new("Shieldless Salvage Fighter", Vector3.Zero, Vector3.Zero, 1f, 0f, FactionManager.LibertyRogues);
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            WeaponEquipmentDefinition gun = (WeaponEquipmentDefinition)EquipmentCatalog.GetById("rogue_blaster");
            loadout.AddOwnedEquipment(gun);
            loadout.TryMountEquipment(gun, out _);
            ship.SetLoadout(loadout);
            ship.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "shield-smoke", Vector3.Zero, 500f, 100f);
            ship.ApplyDamage(ship.Hull.MaxHull + 1f, NpcDestructionSource.Npc);
            return !service.EvaluateDestruction(ship).Any(drop => drop.IsEquipment &&
                EquipmentCatalog.GetById(drop.EquipmentId) is ShieldEquipmentDefinition);
        }

        private static bool ShieldPickupTransfersOwnership()
        {
            Ship player = new(Vector3.Zero);
            player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            bool created = CargoPod.TryCreateEquipment("rogue_combat_shield", Vector3.Zero, Vector3.Zero, 120f, 200f, out CargoPod pod);
            LootManager manager = new();
            if (!created || pod == null || manager.ActivePods is not List<CargoPod> pods)
                return false;
            pods.Add(pod);
            manager.Update(Frame(0.1f), player, false);
            return manager.ActivePods.Count == 0 && player.Loadout.GetOwnedCount("rogue_combat_shield") == 1 &&
                   player.Loadout.GetMountedShield() == null;
        }

        private static bool DealerLifecycleWorks()
        {
            ShieldEquipmentDefinition shield = Shield("professional_deflector");
            Ship player = new(Vector3.Zero);
            player.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            EquipmentDealer dealer = new();
            PlayerCredits credits = new(shield.Price + 100);
            bool bought = dealer.TryBuyEquipment(shield, credits, player, out _);
            bool mounted = bought && dealer.TryMountEquipment(shield, player, out _);
            bool active = mounted && player.Shields.MountedShieldId == shield.Id &&
                          Nearly(player.Shields.CurrentShields, shield.Capacity);
            bool unmounted = mounted && dealer.TryUnmountEquipment(shield, player, out _);
            int resale = dealer.GetResaleValue(shield);
            bool sold = unmounted && dealer.TrySellUnequippedEquipment(shield, credits, player, out _);
            return active && sold && player.Shields.MountedShieldId == string.Empty &&
                   player.Loadout.GetOwnedCount(shield.Id) == 0 && credits.Credits == 100 + resale;
        }

        private static bool SaveLoadPreservesShield()
        {
            string path = Path.Combine(Path.GetTempPath(), $"roguelancer_phase42_{Guid.NewGuid():N}.json");
            try
            {
                ShieldEquipmentDefinition shield = Shield("rogue_combat_shield");
                Ship source = new(Vector3.Zero);
                source.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
                source.Loadout.AddOwnedEquipment(shield);
                source.Loadout.TryMountEquipment(shield, out _);
                SaveGameManager manager = new(path);
                SaveGameData data = new()
                {
                    OwnedEquipment = manager.CaptureOwnedEquipment(source.Loadout),
                    MountedEquipment = manager.CaptureMountedEquipment(source.Loadout)
                };
                bool saved = manager.TrySave(data, out _);
                bool loaded = manager.TryLoad(out SaveGameData restored, out _);
                ShipLoadout rebuilt = manager.BuildLoadout(restored, out List<string> warnings);
                SaveGameData old = new();
                ShipLoadout oldLoadout = manager.BuildLoadout(old, out List<string> oldWarnings);
                return saved && loaded && SaveGameData.CurrentSchemaVersion == 10 && warnings.Count == 0 &&
                       oldWarnings.Count == 0 && oldLoadout.GetMountedShield() == null &&
                       rebuilt.GetOwnedCount(shield.Id) == 1 && rebuilt.GetMountedShield()?.Id == shield.Id;
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static bool AttributionSurvivesShield()
        {
            NpcShip target = CreateNpcWithShield("Attribution Shield Target", FactionManager.LibertyRogues, "rogue_scrap_shield");
            bool hitRecorded = target.MarkDamagedByPlayer(10f);
            float overflow = target.Shields.AbsorbDamage(target.Shields.CurrentShields);
            target.ApplyDamage(overflow + target.Hull.MaxHull + 1f, NpcDestructionSource.Player);
            return hitRecorded && target.WasDamagedByPlayer && target.IsDestroyed &&
                   target.DestructionSource == NpcDestructionSource.Player;
        }

        private static bool InvalidShieldFailsSafe()
        {
            ShieldEquipmentDefinition invalid = new()
            {
                Id = "phase42-invalid-shield",
                Name = "Invalid Shield",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 1,
                Capacity = float.NaN,
                RegenerationRate = -1f,
                RegenerationDelay = float.PositiveInfinity
            };
            ShieldSystem runtime = new(invalid);
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            bool added = loadout.AddOwnedEquipment(invalid);
            bool mounted = loadout.TryMountEquipment(invalid, out _);
            return !runtime.HasMountedShield && runtime.CurrentShields == 0f && added && !mounted &&
                   !CargoPod.TryCreateEquipment(invalid.Id, Vector3.Zero, Vector3.Zero, 120f, 200f, out _);
        }

        private static ShieldEquipmentDefinition Shield(string id) =>
            EquipmentCatalog.GetById(id) as ShieldEquipmentDefinition;

        private static Ship CreatePlayerWithShield(string shieldId)
        {
            Ship player = new(Vector3.Zero);
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            PowerplantEquipmentDefinition powerplant = (PowerplantEquipmentDefinition)EquipmentCatalog.GetById("civilian_powerplant");
            loadout.AddOwnedEquipment(powerplant);
            loadout.TryMountEquipment(powerplant, out _);
            ShieldEquipmentDefinition shield = Shield(shieldId);
            loadout.AddOwnedEquipment(shield);
            loadout.TryMountEquipment(shield, out _);
            player.SetLoadout(loadout);
            return player;
        }

        private static NpcShip CreateNpcWithShield(string name, string factionId, string shieldId, bool heavy = false)
        {
            Vector3 start = Vector3.Zero;
            NpcShip ship = new(name, start, new Vector3(100f, 0f, 0f), 1f, 0f, factionId)
            {
                ModelPath = heavy ? "SHIPS/WARTHOG/warthog" : "SMOKE/fighter"
            };
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            PowerplantEquipmentDefinition powerplant = (PowerplantEquipmentDefinition)EquipmentCatalog.GetById("civilian_powerplant");
            loadout.AddOwnedEquipment(powerplant);
            loadout.TryMountEquipment(powerplant, out _);
            ShieldEquipmentDefinition shield = Shield(shieldId);
            loadout.AddOwnedEquipment(shield);
            loadout.TryMountEquipment(shield, out _);
            ship.SetLoadout(loadout);
            ship.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "shield-smoke", start, 500f, 100f, 8000f);
            return ship;
        }

        private static NpcShip CreateNpcWithGun(string name, string factionId, string gunId, Vector3 position)
        {
            NpcShip ship = new(name, position, position + Vector3.Right * 100f, 1f, 0f, factionId)
            {
                ModelPath = "SMOKE/fighter"
            };
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            PowerplantEquipmentDefinition powerplant = (PowerplantEquipmentDefinition)EquipmentCatalog.GetById("civilian_powerplant");
            loadout.AddOwnedEquipment(powerplant);
            loadout.TryMountEquipment(powerplant, out _);
            WeaponEquipmentDefinition gun = (WeaponEquipmentDefinition)EquipmentCatalog.GetById(gunId);
            loadout.AddOwnedEquipment(gun);
            loadout.TryMountEquipment(gun, out _);
            ship.SetLoadout(loadout);
            ship.ConfigureTrafficBehavior(TrafficZoneBehaviorType.PirateAmbush, "shield-smoke", position, 500f, 100f, 8000f);
            return ship;
        }

        private WeaponSystem CreateWeaponSystem(WeaponEquipmentDefinition weapon)
        {
            WeaponSystem system = new(_graphicsDevice)
            {
                CurrentWeapon = weapon.WeaponType
            };
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
            return system;
        }

        private static GameTime Frame(float seconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
            return new GameTime(elapsed, elapsed);
        }

        private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.001f;

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
