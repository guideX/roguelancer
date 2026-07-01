using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for the first cargo pod loot loop.
    /// </summary>
    internal sealed class LootSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidatePodCreation, "pod creation", ref passed, ref failed);
            RunCase(ValidateUnknownCommoditySafety, "unknown commodity safety", ref passed, ref failed);
            RunCase(ValidateNpcDropSpawn, "NPC drop spawn", ref passed, ref failed);
            RunCase(ValidateTractorMovement, "tractor movement", ref passed, ref failed);
            RunCase(ValidateCargoPickup, "cargo pickup", ref passed, ref failed);
            RunCase(ValidateFullCargoHoldSafety, "full cargo hold safety", ref passed, ref failed);
            RunCase(ValidateCargoHudHint, "cargo HUD hint", ref passed, ref failed);
            RunCase(ValidateExpiry, "expiry", ref passed, ref failed);

            Console.WriteLine($"[LOOT SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[LOOT SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[LOOT SMOKE] FAIL {label}: {result.FailureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[LOOT SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidatePodCreation()
        {
            bool created = CargoPod.TryCreate(
                "food-rations",
                3,
                Vector3.Zero,
                Vector3.Zero,
                60f,
                24f,
                out CargoPod pod);

            if (!created || pod == null)
            {
                return Fail("valid cargo pod could not be created");
            }

            if (!string.Equals(pod.CommodityId, "food-rations", StringComparison.OrdinalIgnoreCase) || pod.Quantity != 3)
            {
                return Fail("cargo pod fields were not populated correctly");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateUnknownCommoditySafety()
        {
            bool created = CargoPod.TryCreate(
                "missing-commodity",
                1,
                Vector3.Zero,
                Vector3.Zero,
                60f,
                24f,
                out CargoPod pod);

            if (created || pod != null)
            {
                return Fail("unknown commodity id should have failed safely");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateNpcDropSpawn()
        {
            LootManager traderLoot = CreateLootManager(1001);
            LootManager pirateLoot = CreateLootManager(1002);

            NpcShip trader = CreateTestNpc("Trader Smoke", TrafficZoneBehaviorType.TraderRoute, "neutral_civilians");
            NpcShip pirate = CreateTestNpc("Pirate Smoke", TrafficZoneBehaviorType.PirateAmbush, "liberty_rogues");

            int traderCount = TriggerNpcDestruction(trader, traderLoot);
            int pirateCount = TriggerNpcDestruction(pirate, pirateLoot);

            if (traderCount != 1)
            {
                return Fail($"trader ship spawned {traderCount} pods instead of 1");
            }

            if (pirateCount != 1)
            {
                return Fail($"pirate ship spawned {pirateCount} pods instead of 1");
            }

            CargoPod traderPod = traderLoot.ActivePods.FirstOrDefault();
            CargoPod piratePod = pirateLoot.ActivePods.FirstOrDefault();
            if (traderPod == null || piratePod == null)
            {
                return Fail("expected cargo pods were not created");
            }

            Commodity traderCommodity = traderPod.GetCommodity();
            Commodity pirateCommodity = piratePod.GetCommodity();

            if (traderCommodity == null || traderCommodity.IsContraband)
            {
                return Fail("trader pod did not contain a legal commodity");
            }

            if (pirateCommodity == null)
            {
                return Fail("pirate pod commodity was invalid");
            }

            if (traderCommodity.BasePrice <= 0 || pirateCommodity.BasePrice <= 0)
            {
                return Fail("spawned loot commodity had a non-positive base value");
            }

            int traderValue = traderCommodity.BasePrice * traderPod.Quantity;
            int pirateValue = pirateCommodity.BasePrice * piratePod.Quantity;
            if (traderValue <= 0 || pirateValue <= 0)
            {
                return Fail("spawned loot pod value should always be positive");
            }

            if (traderPod.Quantity < 1 || traderPod.Quantity > 2 || piratePod.Quantity < 1 || piratePod.Quantity > 2)
            {
                return Fail("loot pod quantity was outside the early-game range");
            }

            bool pirateLootLooksCriminal = pirateCommodity.IsContraband ||
                                           string.Equals(pirateCommodity.Id, "diamonds", StringComparison.OrdinalIgnoreCase);
            if (!pirateLootLooksCriminal)
            {
                return Fail("pirate pod did not contain contraband or valuables");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateTractorMovement()
        {
            LootManager loot = CreateLootManager(2001);
            Ship player = CreatePlayer();

            bool created = CargoPod.TryCreate("food-rations", 1, new Vector3(500f, 0f, 0f), Vector3.Zero, 60f, 24f, out CargoPod pod);
            if (!created || pod == null)
            {
                return Fail("tractor test cargo pod could not be created");
            }
            InjectPod(loot, pod);

            float before = Vector3.Distance(player.Position, pod.Position);
            loot.Update(CreateGameTime(1f), player, tractorActive: true);
            float after = Vector3.Distance(player.Position, pod.Position);

            if (after >= before)
            {
                return Fail("tractor beam did not pull the pod closer");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCargoPickup()
        {
            LootManager loot = CreateLootManager(3001);
            Ship player = CreatePlayer();

            bool created = CargoPod.TryCreate("food-rations", 3, new Vector3(10f, 0f, 0f), Vector3.Zero, 60f, 24f, out CargoPod pod);
            if (!created || pod == null)
            {
                return Fail("pickup test cargo pod could not be created");
            }
            InjectPod(loot, pod);

            loot.Update(CreateGameTime(0.5f), player, tractorActive: true);

            if (loot.ActivePods.Count != 0)
            {
                return Fail("cargo pod should have been collected");
            }

            if (player.CargoHold.GetCommodityQuantity("Food Rations") != 3)
            {
                return Fail("cargo hold did not receive tractored cargo");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateFullCargoHoldSafety()
        {
            LootManager loot = CreateLootManager(4001);
            Ship player = CreatePlayer();
            player.CargoHold.SetMaxCapacity(0);
            player.CargoHold.Clear();

            bool created = CargoPod.TryCreate("food-rations", 1, new Vector3(10f, 0f, 0f), Vector3.Zero, 60f, 24f, out CargoPod pod);
            if (!created || pod == null)
            {
                return Fail("full-hold test cargo pod could not be created");
            }
            InjectPod(loot, pod);

            loot.Update(CreateGameTime(0.5f), player, tractorActive: true);

            if (loot.ActivePods.Count != 1)
            {
                return Fail("cargo pod was incorrectly removed while the hold was full");
            }

            if (player.CargoHold.GetCommodityQuantity("Food Rations") != 0)
            {
                return Fail("cargo hold unexpectedly accepted cargo while full");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCargoHudHint()
        {
            LootManager loot = CreateLootManager(4501);
            Ship player = CreatePlayer();

            bool created = CargoPod.TryCreate("food-rations", 3, new Vector3(500f, 0f, 0f), Vector3.Zero, 60f, 24f, out CargoPod pod);
            if (!created || pod == null)
            {
                return Fail("HUD hint cargo pod could not be created");
            }

            InjectPod(loot, pod);

            if (!loot.HasNearbyCargoPod(player.Position))
            {
                return Fail("nearby cargo pod was not detected for HUD hinting");
            }

            string hint = loot.GetNearestCargoPodHint(player.Position, player.CargoHold);
            if (string.IsNullOrWhiteSpace(hint) || !hint.Contains("Hold P: Tractor Food Rations x3", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("cargo HUD hint did not include the nearest pod commodity and quantity");
            }

            loot = CreateLootManager(4502);
            player = CreatePlayer();
            player.CargoHold.SetMaxCapacity(0);
            player.CargoHold.Clear();

            InjectPod(loot, pod);

            hint = loot.GetNearestCargoPodHint(player.Position, player.CargoHold);
            if (!string.Equals(hint, "Cargo hold full", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("cargo HUD hint did not switch to the full-hold message");
            }

            bool farPodCreated = CargoPod.TryCreate("food-rations", 1, new Vector3(2000f, 0f, 0f), Vector3.Zero, 60f, 24f, out CargoPod farPod);
            if (!farPodCreated || farPod == null)
            {
                return Fail("far cargo pod could not be created");
            }

            loot = CreateLootManager(4503);
            InjectPod(loot, farPod);
            if (loot.HasNearbyCargoPod(player.Position))
            {
                return Fail("distant cargo pod should not have been considered nearby");
            }

            if (loot.GetNearestCargoPodHint(player.Position, player.CargoHold) != null)
            {
                return Fail("distant cargo pod should not produce a HUD hint");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateExpiry()
        {
            LootManager loot = CreateLootManager(5001);
            Ship player = CreatePlayer();

            bool created = CargoPod.TryCreate("food-rations", 1, new Vector3(100f, 0f, 0f), Vector3.Zero, 0.25f, 24f, out CargoPod pod);
            if (!created || pod == null)
            {
                return Fail("expiry test cargo pod could not be created");
            }
            InjectPod(loot, pod);

            loot.Update(CreateGameTime(0.5f), player, tractorActive: false);
            if (loot.ActivePods.Count != 0)
            {
                return Fail("expired cargo pod was not removed");
            }

            return Pass();
        }

        private static LootManager CreateLootManager(int seed)
        {
            return RunSilenced(() => new LootManager(null, new Random(seed)));
        }

        private static Ship CreatePlayer()
        {
            return RunSilenced(() => new Ship(Vector3.Zero));
        }

        private static NpcShip CreateTestNpc(string name, TrafficZoneBehaviorType behavior, string factionId)
        {
            return RunSilenced(() =>
            {
                var npc = new NpcShip(name, Vector3.Zero, Vector3.Zero, 1f, 0f, factionId);
                npc.ConfigureTrafficBehavior(behavior, "loot-smoke-zone", Vector3.Zero, 500f, 100f);
                return npc;
            });
        }

        private static int TriggerNpcDestruction(NpcShip npc, LootManager loot)
        {
            int spawned = 0;
            RunSilenced(() =>
            {
                npc.OnDestroyed += destroyedShip =>
                {
                    spawned = loot.SpawnLootForDestroyedNpc(destroyedShip);
                };

                if (npc.Shields.CurrentShields > 0f)
                {
                    npc.Shields.AbsorbDamage(npc.Shields.CurrentShields);
                }

                npc.Hull.TakeDamage(npc.Hull.CurrentHull + 1f);
            });

            return spawned;
        }

        private static void InjectPod(LootManager loot, CargoPod pod)
        {
            if (loot == null || pod == null)
            {
                throw new InvalidOperationException("Could not inject cargo pod.");
            }

            if (loot.ActivePods is List<CargoPod> pods)
            {
                pods.Add(pod);
                return;
            }

            throw new InvalidOperationException("Could not access active cargo pods.");
        }

        private static GameTime CreateGameTime(float seconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
            return new GameTime(elapsed, elapsed);
        }

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

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
