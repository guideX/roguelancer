using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for ambient traffic zones and encounter spawning.
    /// </summary>
    internal sealed class TrafficSmokeTest
    {
        private readonly ConfigurationManager _config;

        public TrafficSmokeTest()
        {
            _config = RunSilenced(() =>
            {
                var config = new ConfigurationManager();
                config.LoadAll();
                return config;
            });
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateZonesLoaded, "zones loaded", ref passed, ref failed);
            RunCase(ValidateLawfulPatrolSpawn, "lawful patrol spawn", ref passed, ref failed);
            RunCase(ValidateTraderRouteSpawn, "trader route spawn", ref passed, ref failed);
            RunCase(ValidatePirateAmbushSpawn, "pirate ambush spawn", ref passed, ref failed);
            RunCase(ValidateSpawnCapsAndDespawnSafety, "spawn caps/despawn", ref passed, ref failed);

            Console.WriteLine($"[TRAFFIC SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                var result = RunSilenced(test);
                if (result.Success)
                {
                    passed++;
                    Console.WriteLine($"[TRAFFIC SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[TRAFFIC SMOKE] FAIL {label}: {result.FailureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[TRAFFIC SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateZonesLoaded()
        {
            List<TrafficZoneConfig> zones = _config.GetTrafficZonesForSystem(1);
            if (zones.Count < 4)
            {
                return Fail($"expected at least 4 traffic zones, found {zones.Count}");
            }

            if (!zones.Any(zone => zone.BehaviorType == TrafficZoneBehaviorType.LawfulPatrol))
            {
                return Fail("missing lawful patrol zone");
            }

            if (!zones.Any(zone => zone.BehaviorType == TrafficZoneBehaviorType.TraderRoute))
            {
                return Fail("missing trader route zone");
            }

            if (!zones.Any(zone => zone.BehaviorType == TrafficZoneBehaviorType.PirateAmbush))
            {
                return Fail("missing pirate ambush zone");
            }

            if (!zones.Any(zone => zone.BehaviorType == TrafficZoneBehaviorType.StationTraffic))
            {
                return Fail("missing station traffic zone");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateLawfulPatrolSpawn()
        {
            var traffic = CreateTrafficManager(out Ship player, out ReputationManager reputationManager, out PoliceScanSystem scanSystem);
            TrafficZoneConfig zone = traffic.LoadedZones.FirstOrDefault(z => z.BehaviorType == TrafficZoneBehaviorType.LawfulPatrol);
            if (zone == null)
            {
                return Fail("lawful patrol zone was not found");
            }

            IReadOnlyList<NpcShip> ships = traffic.GetActiveShipsForZone(zone.Id);
            if (ships.Count == 0)
            {
                return Fail("lawful patrol zone did not spawn any ships");
            }

            NpcShip ship = ships[0];
            if (!scanSystem.IsLawfulScannerFaction(ship.FactionId))
            {
                return Fail($"lawful patrol ship '{ship.Name}' was not scanner-capable");
            }

            if (reputationManager.IsHostile(ship.FactionId))
            {
                return Fail("lawful patrol spawned a hostile faction");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateTraderRouteSpawn()
        {
            var traffic = CreateTrafficManager(out _, out ReputationManager reputationManager, out _);
            TrafficZoneConfig zone = traffic.LoadedZones.FirstOrDefault(z => z.BehaviorType == TrafficZoneBehaviorType.TraderRoute);
            if (zone == null)
            {
                return Fail("trader route zone was not found");
            }

            IReadOnlyList<NpcShip> ships = traffic.GetActiveShipsForZone(zone.Id);
            if (ships.Count == 0)
            {
                return Fail("trader route zone did not spawn any ships");
            }

            NpcShip ship = ships[0];
            if (reputationManager.IsHostile(ship.FactionId))
            {
                return Fail($"trader '{ship.Name}' spawned hostile to the player");
            }

            if (ship.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute)
            {
                return Fail("trader route ship behavior was not configured correctly");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidatePirateAmbushSpawn()
        {
            var traffic = CreateTrafficManager(out _, out ReputationManager reputationManager, out _);
            TrafficZoneConfig zone = traffic.LoadedZones.FirstOrDefault(z => z.BehaviorType == TrafficZoneBehaviorType.PirateAmbush);
            if (zone == null)
            {
                return Fail("pirate ambush zone was not found");
            }

            IReadOnlyList<NpcShip> ships = traffic.GetActiveShipsForZone(zone.Id);
            if (ships.Count == 0)
            {
                return Fail("pirate ambush zone did not spawn any ships");
            }

            NpcShip ship = ships[0];
            if (!reputationManager.IsHostile(ship.FactionId))
            {
                return Fail($"pirate '{ship.Name}' was not hostile");
            }

            if (ship.TrafficBehavior != TrafficZoneBehaviorType.PirateAmbush)
            {
                return Fail("pirate ambush ship behavior was not configured correctly");
            }

            PoliceScanSystem scanSystem = new PoliceScanSystem();
            Ship player = new Ship(Vector3.Zero);
            PlayerCredits credits = new PlayerCredits(10_000);

            RunSilenced(() =>
            {
                for (int i = 0; i < 8; i++)
                {
                    scanSystem.Update(CreateGameTime(0.5f), player, new List<NpcShip> { ship }, credits, reputationManager);
                }
            });

            if (scanSystem.State != PoliceScanState.Idle)
            {
                return Fail("non-lawful pirate traffic triggered a police scan");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateSpawnCapsAndDespawnSafety()
        {
            var traffic = CreateTrafficManager(out Ship player, out ReputationManager reputationManager, out _);
            TrafficZoneConfig zone = traffic.LoadedZones.FirstOrDefault(z => z.BehaviorType == TrafficZoneBehaviorType.StationTraffic);
            if (zone == null)
            {
                return Fail("station traffic zone was not found");
            }

            if (traffic.GetActiveShipCount(zone.Id) > zone.MaxShips)
            {
                return Fail($"initial spawn exceeded cap for zone '{zone.Name}'");
            }

            RunSilenced(() =>
            {
                for (int i = 0; i < 70; i++)
                {
                    traffic.Update(CreateGameTime(0.5f), player, reputationManager);
                }
            });

            if (traffic.GetActiveShipCount(zone.Id) > zone.MaxShips)
            {
                return Fail($"spawn cap was exceeded for zone '{zone.Name}'");
            }

            IReadOnlyList<NpcShip> ships = traffic.GetActiveShipsForZone(zone.Id);
            if (ships.Count == 0)
            {
                return Fail("station traffic zone did not retain any active ships");
            }

            NpcShip farShip = ships[0];
            farShip.Position += new Vector3(100_000f, 0f, 0f);

            RunSilenced(() => traffic.Update(CreateGameTime(0.5f), player, reputationManager));
            if (traffic.GetActiveShipsForZone(zone.Id).Contains(farShip))
            {
                return Fail("far traffic ship was not safely despawned");
            }

            return Pass();
        }

        private TrafficManager CreateTrafficManager(out Ship player, out ReputationManager reputationManager, out PoliceScanSystem scanSystem)
        {
            player = new Ship(Vector3.Zero);
            reputationManager = new ReputationManager(new FactionManager());
            scanSystem = new PoliceScanSystem();

            var npcShips = new List<NpcShip>();
            var spaceObjects = new List<SpaceObject>();
            var traffic = RunSilenced(() =>
            {
                var manager = new TrafficManager(_config, npcShips, spaceObjects);
                manager.LoadZonesForSystem(1);
                return manager;
            });

            return traffic;
        }

        private static GameTime CreateGameTime(float deltaSeconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(deltaSeconds);
            return new GameTime(elapsed, elapsed);
        }

        private static (bool Success, string FailureReason) Pass()
        {
            return (true, string.Empty);
        }

        private static (bool Success, string FailureReason) Fail(string reason)
        {
            return (false, reason);
        }

        private static T RunSilenced<T>(Func<T> action)
        {
            TextWriter originalOut = Console.Out;
            using var sink = new StringWriter();
            Console.SetOut(sink);
            try
            {
                return action();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        private static void RunSilenced(Action action)
        {
            TextWriter originalOut = Console.Out;
            using var sink = new StringWriter();
            Console.SetOut(sink);
            try
            {
                action();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
