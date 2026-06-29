using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for contraband scans and police consequences.
    /// </summary>
    internal sealed class ContrabandSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateCleanCargoScan, "clean cargo scan", ref passed, ref failed);
            RunCase(ValidateContrabandDetected, "contraband detected", ref passed, ref failed);
            RunCase(ValidateFineReputationPenalty, "fine/reputation penalty", ref passed, ref failed);
            RunCase(ValidateScanCancelOutOfRange, "scan cancel out of range", ref passed, ref failed);
            RunCase(ValidateNonLawfulIgnored, "non-lawful ignored", ref passed, ref failed);

            Console.WriteLine($"[CONTRABAND SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                var result = test();
                if (result.Success)
                {
                    passed++;
                    Console.WriteLine($"[CONTRABAND SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[CONTRABAND SMOKE] FAIL {label}: {result.FailureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[CONTRABAND SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateCleanCargoScan()
        {
            PoliceScanSystem scanSystem = new PoliceScanSystem();
            ReputationManager reputationManager = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new PlayerCredits(10_000);
            float reputationBefore = reputationManager.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, player.Position + new Vector3(1800f, 0f, 0f));

            StepScan(scanSystem, reputationManager, player, credits, new List<NpcShip> { scanner }, seconds: 0.5f, frameCount: 1);
            if (scanSystem.State != PoliceScanState.Scanning)
            {
                return Fail("scan did not initiate");
            }

            StepScan(scanSystem, reputationManager, player, credits, new List<NpcShip> { scanner }, seconds: 0.5f, frameCount: 6);
            if (scanSystem.State != PoliceScanState.Cleared)
            {
                return Fail("clean scan did not complete");
            }

            if (credits.Credits != 10_000)
            {
                return Fail("clean scan changed credits");
            }

            if (Math.Abs(reputationManager.GetStanding(FactionManager.LibertyPolice) - reputationBefore) > 0.0001f)
            {
                return Fail("clean scan changed reputation");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateContrabandDetected()
        {
            PoliceScanSystem scanSystem = new PoliceScanSystem();
            ReputationManager reputationManager = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new PlayerCredits(1_000);
            float reputationBefore = reputationManager.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, player.Position + new Vector3(1600f, 0f, 0f));

            Commodity contraband = CommodityCatalog.GetById("side-arms");
            if (contraband == null)
            {
                return Fail("contraband commodity was not found");
            }

            if (!player.CargoHold.AddCommodity(contraband, 2))
            {
                return Fail("failed to seed contraband cargo");
            }

            InjectMissingCommodity(player.CargoHold, "missing-commodity", 3);

            StepScan(scanSystem, reputationManager, player, credits, new List<NpcShip> { scanner }, seconds: 0.5f, frameCount: 7);
            if (scanSystem.State != PoliceScanState.Enforcement)
            {
                return Fail("contraband scan did not trigger enforcement");
            }

            if (credits.Credits != 1_000)
            {
                return Fail("contraband scan changed credits without enough funds");
            }

            if (reputationManager.GetStanding(FactionManager.LibertyPolice) >= reputationBefore)
            {
                return Fail("contraband scan did not reduce reputation");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateFineReputationPenalty()
        {
            PoliceScanSystem scanSystem = new PoliceScanSystem();
            ReputationManager reputationManager = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new PlayerCredits(10_000);
            float reputationBefore = reputationManager.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, player.Position + new Vector3(1600f, 0f, 0f));

            Commodity contraband = CommodityCatalog.GetById("alien-organisms");
            if (contraband == null)
            {
                return Fail("contraband commodity was not found");
            }

            if (!player.CargoHold.AddCommodity(contraband, 1))
            {
                return Fail("failed to seed contraband cargo");
            }

            StepScan(scanSystem, reputationManager, player, credits, new List<NpcShip> { scanner }, seconds: 0.5f, frameCount: 7);
            if (scanSystem.State != PoliceScanState.Cleared)
            {
                return Fail("fine scan did not complete cleanly");
            }

            int expectedCredits = 10_000 - PoliceScanSystem.FineAmount;
            if (credits.Credits != expectedCredits)
            {
                return Fail($"fine was not deducted (expected {expectedCredits}, got {credits.Credits})");
            }

            if (reputationManager.GetStanding(FactionManager.LibertyPolice) >= reputationBefore)
            {
                return Fail("fine scan did not reduce reputation");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateScanCancelOutOfRange()
        {
            PoliceScanSystem scanSystem = new PoliceScanSystem();
            ReputationManager reputationManager = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new PlayerCredits(10_000);
            float reputationBefore = reputationManager.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, player.Position + new Vector3(1700f, 0f, 0f));

            StepScan(scanSystem, reputationManager, player, credits, new List<NpcShip> { scanner }, seconds: 0.5f, frameCount: 1);
            if (scanSystem.State != PoliceScanState.Scanning)
            {
                return Fail("scan did not start");
            }

            player.Position = new Vector3(10000f, 0f, 0f);
            StepScan(scanSystem, reputationManager, player, credits, new List<NpcShip> { scanner }, seconds: 0.5f, frameCount: 5);

            if (scanSystem.State != PoliceScanState.Idle)
            {
                return Fail("scan did not cancel after moving out of range");
            }

            if (credits.Credits != 10_000)
            {
                return Fail("out-of-range cancel changed credits");
            }

            if (Math.Abs(reputationManager.GetStanding(FactionManager.LibertyPolice) - reputationBefore) > 0.0001f)
            {
                return Fail("out-of-range cancel changed reputation");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateNonLawfulIgnored()
        {
            PoliceScanSystem scanSystem = new PoliceScanSystem();
            ReputationManager reputationManager = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new PlayerCredits(10_000);
            float reputationBefore = reputationManager.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyRogues, player.Position + new Vector3(1600f, 0f, 0f));

            Commodity contraband = CommodityCatalog.GetById("alien-organisms");
            if (contraband == null)
            {
                return Fail("contraband commodity was not found");
            }

            if (!player.CargoHold.AddCommodity(contraband, 1))
            {
                return Fail("failed to seed contraband cargo");
            }

            StepScan(scanSystem, reputationManager, player, credits, new List<NpcShip> { scanner }, seconds: 0.5f, frameCount: 8);
            if (scanSystem.State != PoliceScanState.Idle)
            {
                return Fail("non-lawful faction initiated a scan");
            }

            if (credits.Credits != 10_000)
            {
                return Fail("non-lawful scanner changed credits");
            }

            if (Math.Abs(reputationManager.GetStanding(FactionManager.LibertyPolice) - reputationBefore) > 0.0001f)
            {
                return Fail("non-lawful scanner changed reputation");
            }

            return Pass();
        }

        private static ReputationManager CreateReputationManager()
        {
            return new ReputationManager(new FactionManager());
        }

        private static void StepScan(PoliceScanSystem scanSystem, ReputationManager reputationManager, Ship player, PlayerCredits credits, List<NpcShip> npcs, float seconds, int frameCount)
        {
            TimeSpan elapsed = TimeSpan.Zero;
            for (int i = 0; i < frameCount; i++)
            {
                TimeSpan previous = elapsed;
                elapsed = elapsed.Add(TimeSpan.FromSeconds(seconds));
                GameTime gameTime = new GameTime(previous, TimeSpan.FromSeconds(seconds));
                scanSystem.Update(gameTime, player, npcs, credits, reputationManager);
            }
        }

        private static Ship CreatePlayer()
        {
            return new Ship(Vector3.Zero);
        }

        private static NpcShip CreateScanner(string factionId, Vector3 position)
        {
            NpcShip scanner = new NpcShip("Police Scan Test", position, position, 1f, 0f, factionId);
            scanner.Position = position;
            scanner.Velocity = Vector3.Zero;
            return scanner;
        }

        private static void InjectMissingCommodity(CargoHold cargoHold, string commodityId, int quantity)
        {
            FieldInfo field = typeof(CargoHold).GetField("_commodities", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access cargo manifest.");

            var cargo = (Dictionary<string, int>)field.GetValue(cargoHold);
            cargo[commodityId] = quantity;
        }

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
    }
}
