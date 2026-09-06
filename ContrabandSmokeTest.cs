using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 52/53 validation for police cargo scans, bounded
    /// enforcement, physical jettison,
    /// and Rogue contraband-smuggling mission lifecycle.
    /// </summary>
    internal sealed class ContrabandSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;
            RunCase(ValidateCleanCargoScan, "clean scan has no consequence", ref passed, ref failed);
            RunCase(ValidateContrabandDetectionConsequences, "contraband detection consequence", ref passed, ref failed);
            RunCase(ValidateCivilCompliance, "civil compliance pays and confiscates", ref passed, ref failed);
            RunCase(ValidateDemandInput, "demand input resolves exactly once", ref passed, ref failed);
            RunCase(ValidateSingleDemandOwner, "one active demand owns the incident", ref passed, ref failed);
            RunCase(ValidateRefusal, "refusal retains cargo and turns hostile", ref passed, ref failed);
            RunCase(ValidateDemandTimeout, "demand timeout behaves as refusal", ref passed, ref failed);
            RunCase(ValidateFleeingDemand, "fleeing the enforcement radius behaves as refusal", ref passed, ref failed);
            RunCase(ValidateTradeLaneEscape, "trade-lane transit behaves as flight", ref passed, ref failed);
            RunCase(ValidatePreExistingHostilitySkipsDemand, "pre-existing hostility skips peaceful demand", ref passed, ref failed);
            RunCase(ValidateScannerAttackRefuses, "scanner destruction behaves as refusal", ref passed, ref failed);
            RunCase(ValidateDemandReset, "demand reset clears transient state", ref passed, ref failed);
            RunCase(ValidateMissionCargoIsScanned, "mission cargo is scanned", ref passed, ref failed);
            RunCase(ValidatePhysicalJettisonAndRecovery, "physical jettison and pod recovery", ref passed, ref failed);
            RunCase(ValidateScanInterruption, "scan interruption and evasion", ref passed, ref failed);
            RunCase(ValidateCooldownAndDeterministicTieBreak, "cooldown and deterministic scanner tie-break", ref passed, ref failed);
            RunCase(ValidateSmugglingOfferGeneration, "smuggling offers are deterministic", ref passed, ref failed);
            RunCase(ValidateSmugglingCapacityGuard, "smuggling capacity guard", ref passed, ref failed);
            RunCase(ValidateSmugglingDeliveryExactlyOnce, "smuggling delivery and reward exactly once", ref passed, ref failed);
            RunCase(ValidateSmugglingComplianceFailsContract, "smuggling compliance fails confiscated contract", ref passed, ref failed);
            RunCase(ValidateSmugglingRefusalCanDeliver, "smuggling refusal preserves delivery path", ref passed, ref failed);
            RunCase(ValidateSaveFields, "smuggling save fields", ref passed, ref failed);
            RunCase(ValidateNonLawfulIgnored, "non-lawful scanner ignored", ref passed, ref failed);

            Console.WriteLine($"[PHASE 52/53 SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                (bool success, string failureReason) = test();
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[PHASE 52/53 SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[PHASE 52/53 SMOKE] FAIL {label}: {failureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[PHASE 52/53 SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateCleanCargoScan()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            float standing = reputation.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_800f, 0f, 0f));

            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            if (scan.State != PoliceScanState.Cleared)
                return Fail("clean cargo did not clear");
            if (credits.Credits != 10_000 || Math.Abs(reputation.GetStanding(FactionManager.LibertyPolice) - standing) > 0.0001f)
                return Fail("clean scan changed credits or standing");
            if (reputation.IsTemporarilyHostile(FactionManager.LibertyPolice))
                return Fail("clean scan created hostility");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateContrabandDetectionConsequences()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            if (contraband == null || !player.CargoHold.AddCommodity(contraband, 2))
                return Fail("could not seed canonical contraband");
            float standing = reputation.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_600f, 0f, 0f));

            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            if (scan.State != PoliceScanState.ContrabandDetected || scan.CurrentOffer?.TotalContrabandQuantity != 2)
                return Fail("scan did not report the exact contraband quantity");
            if (credits.Credits != 10_000 || player.CargoHold.GetCommodityQuantity(contraband.Name) != 2)
                return Fail("detection confiscated or charged cargo");
            if (Math.Abs(reputation.GetStanding(FactionManager.LibertyPolice) - standing) > 0.0001f)
                return Fail("detection changed standing before the player chose a response");
            if (reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) || scanner.HasPlayerTarget)
                return Fail("detection immediately activated hostility instead of a demand");
            if (!scan.IsEnforcementDemandActive || scan.EnforcementDemandRemainingSeconds <= 0f ||
                !scan.StatusText.Contains("Comply", StringComparison.OrdinalIgnoreCase))
                return Fail("detection did not open a bounded enforcement demand");
            if (scan.DetectionCount != 1)
                return Fail("detection was applied more than once");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateMissionCargoIsScanned()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            Commodity contraband = CommodityCatalog.GetById("alien-organisms");
            if (contraband == null || !player.CargoHold.AddMissionCargo(7301, contraband, 1))
                return Fail("could not seed mission-attributed contraband");

            StepScan(scan, reputation, player, credits,
                new List<NpcShip> { CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f)) }, 0.5f, 8);
            if (scan.State != PoliceScanState.ContrabandDetected || scan.CurrentOffer?.Contraband.Count != 1)
                return Fail("reserved contraband was hidden from the scan");
            if (scan.CurrentOffer.Contraband[0].Quantity != 1 || player.CargoHold.GetMissionCargoQuantity(7301) != 1)
                return Fail("mission cargo scan changed authoritative reservation");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCivilCompliance()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            Commodity legal = CommodityCatalog.GetById("food-rations");
            if (contraband == null || legal == null || !player.CargoHold.AddCommodity(contraband, 1) ||
                !player.CargoHold.AddCommodity(legal, 2))
                return Fail("could not seed mixed civil cargo");

            float standing = reputation.GetStanding(FactionManager.LibertyPolice);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            int fine = scan.CurrentOffer?.FineAmount ?? 0;
            if (!scan.IsEnforcementDemandActive || !scan.TryAcceptEnforcement(player, credits, reputation))
                return Fail("civil compliance did not resolve the demand");

            return scan.State == PoliceScanState.Cleared && credits.Credits == 5_000 - fine &&
                player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
                player.CargoHold.GetCommodityQuantity(legal.Name) == 2 &&
                !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
                Math.Abs(reputation.GetStanding(FactionManager.LibertyPolice) - (standing + PoliceScanSystem.DetectionReputationPenalty)) < 0.0001f
                ? Pass()
                : Fail("civil compliance changed the wrong authoritative state");
        }

        private (bool Success, string FailureReason) ValidateRefusal()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            player.CargoHold.AddCommodity(contraband, 1);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            if (!scan.TryRefuseEnforcement(player, credits, reputation))
                return Fail("refusal did not resolve the demand");

            return scan.State == PoliceScanState.Enforcement && credits.Credits == 5_000 &&
                player.CargoHold.GetCommodityQuantity(contraband.Name) == 1 &&
                reputation.IsTemporarilyHostile(FactionManager.LibertyPolice)
                ? Pass()
                : Fail("refusal changed money or cargo incorrectly");
        }

        private (bool Success, string FailureReason) ValidateDemandInput()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            int before = credits.Credits;
            bool handled = scan.HandleInput(
                new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
                new KeyboardState(),
                player,
                credits,
                reputation);
            bool repeated = scan.HandleInput(
                new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
                new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
                player,
                credits,
                reputation);
            return handled && !repeated && scan.State == PoliceScanState.Cleared && credits.Credits < before &&
                player.CargoHold.GetCommodityQuantity("Side Arms") == 0
                ? Pass()
                : Fail("assigned comply input did not resolve the active demand");
        }

        private (bool Success, string FailureReason) ValidateSingleDemandOwner()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            NpcShip alpha = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_400f, 0f, 0f), "Police Alpha");
            NpcShip beta = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_600f, 0f, 0f), "Police Beta");
            List<NpcShip> scanners = new() { alpha, beta };
            StepScan(scan, reputation, player, credits, scanners, 0.5f, 8);
            PoliceEnforcementOffer offer = scan.CurrentOffer;
            scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1f)), player, scanners, credits, reputation);
            return scan.IsEnforcementDemandActive && scan.DetectionCount == 1 &&
                ReferenceEquals(offer, scan.CurrentOffer) && scan.ActiveScanner != null
                ? Pass()
                : Fail("a second Police scanner replaced or duplicated the active demand");
        }

        private (bool Success, string FailureReason) ValidateDemandTimeout()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(PoliceScanSystem.EnforcementDemandSeconds + 0.1f)), player,
                new List<NpcShip> { scanner }, credits, reputation);
            return scan.State == PoliceScanState.Enforcement && reputation.IsTemporarilyHostile(FactionManager.LibertyPolice)
                ? Pass()
                : Fail("demand timeout did not enter ordinary hostile enforcement");
        }

        private (bool Success, string FailureReason) ValidateFleeingDemand()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            player.Position = scanner.Position + new Vector3(PoliceScanSystem.EnforcementEscapeRange + 100f, 0f, 0f);
            scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1f)), player,
                new List<NpcShip> { scanner }, credits, reputation);
            return scan.State == PoliceScanState.Enforcement && credits.Credits == 5_000 &&
                reputation.IsTemporarilyHostile(FactionManager.LibertyPolice)
                ? Pass()
                : Fail("leaving the enforcement radius did not behave as flight");
        }

        private (bool Success, string FailureReason) ValidateTradeLaneEscape()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1f)), player,
                new List<NpcShip> { scanner }, credits, reputation, playerDocked: false, playerInTradeLaneTransit: true);
            return scan.State == PoliceScanState.Enforcement && reputation.IsTemporarilyHostile(FactionManager.LibertyPolice)
                ? Pass()
                : Fail("trade-lane entry did not resolve the demand as flight");
        }

        private (bool Success, string FailureReason) ValidatePreExistingHostilitySkipsDemand()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            StepScan(scan, reputation, player, credits,
                new List<NpcShip> { CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f)) }, 0.5f, 8);
            return scan.State == PoliceScanState.Idle && scan.CurrentOffer == null
                ? Pass()
                : Fail("pre-existing Police hostility offered peaceful surrender");
        }

        private (bool Success, string FailureReason) ValidateScannerAttackRefuses()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            scanner.MarkDamagedByPlayer();
            scanner.Hull.TakeDamage(scanner.Hull.MaxHull + 1f);
            scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1f)), player,
                new List<NpcShip> { scanner }, credits, reputation);
            return scan.State == PoliceScanState.Enforcement && credits.Credits == 5_000 &&
                player.CargoHold.GetCommodityQuantity("Side Arms") == 1 &&
                reputation.IsTemporarilyHostile(FactionManager.LibertyPolice)
                ? Pass()
                : Fail("scanner destruction did not resolve as hostile refusal");
        }

        private (bool Success, string FailureReason) ValidateDemandReset()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(5_000);
            player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms"), 1);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 8);
            scan.Reset();
            return scan.State == PoliceScanState.Idle && !scan.IsEnforcementDemandActive &&
                scan.CurrentOffer == null && scan.EnforcementDemandRemainingSeconds == 0f &&
                credits.Credits == 5_000 && player.CargoHold.GetCommodityQuantity("Side Arms") == 1
                ? Pass()
                : Fail("reset left transient demand state or mutated cargo");
        }

        private (bool Success, string FailureReason) ValidatePhysicalJettisonAndRecovery()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            Commodity contraband = CommodityCatalog.GetById("alien-organisms");
            if (contraband == null || !player.CargoHold.AddMissionCargo(7302, contraband, 1))
                return Fail("could not seed mission cargo");
            LootManager loot = new(worldObjectsProvider: () => Array.Empty<SpaceObject>());
            scan.SetMissionManager(CreateMissionManagerForPlayer(player, out _));

            StepScan(scan, reputation, player, credits,
                new List<NpcShip> { CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f)) }, 0.5f, 8);
            if (scan.State != PoliceScanState.ContrabandDetected)
                return Fail("scan did not detect mission cargo");

            if (!scan.TryJettisonContraband(player, loot) || loot.ActivePods.Count != 1)
                return Fail("jettison did not create one physical pod");
            CargoPod pod = loot.ActivePods[0];
            if (!pod.IsMissionCargo || pod.MissionId != 7302 || player.CargoHold.GetMissionCargoQuantity(7302) != 0)
                return Fail("jettison lost mission attribution or cargo accounting");
            if (!scan.IsEnforcementDemandActive || credits.Credits != 10_000)
                return Fail("post-detection jettison incorrectly cleared or charged the demand");

            StepLoot(loot, player, 0.1f);
            if (loot.ActivePods.Count != 0 || player.CargoHold.GetMissionCargoQuantity(7302) != 1 ||
                !scan.IsEnforcementDemandActive)
                return Fail("physical mission pod was not recoverable with attribution");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateScanInterruption()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            float standing = reputation.GetStanding(FactionManager.LibertyPolice);

            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 1);
            player.Position = new Vector3(10_000f, 0f, 0f);
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 1);
            if (scan.State != PoliceScanState.Idle || scan.CooldownRemaining <= 0f)
                return Fail("out-of-range scan did not cancel with cooldown");
            if (reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) ||
                Math.Abs(reputation.GetStanding(FactionManager.LibertyPolice) - standing) > 0.0001f)
                return Fail("evasion applied a detection consequence");

            scan.Reset();
            player.Position = Vector3.Zero;
            scanner.Position = new Vector3(1_500f, 0f, 0f);
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 1);
            scanner.SetTradeLaneTransit(true, "test-lane");
            StepScan(scan, reputation, player, credits, new List<NpcShip> { scanner }, 0.5f, 1);
            if (scan.State != PoliceScanState.Idle)
                return Fail("trade-lane transit did not interrupt scan");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCooldownAndDeterministicTieBreak()
        {
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            NpcShip alpha = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f), "Police Alpha");
            NpcShip beta = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f), "Police Beta");
            PoliceScanSystem first = new();
            StepScan(first, reputation, player, credits, new List<NpcShip> { alpha, beta }, 0.5f, 1);
            string selected = first.ActiveScanner?.Name;
            PoliceScanSystem second = new();
            alpha = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f), "Police Alpha");
            beta = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f), "Police Beta");
            StepScan(second, reputation, player, credits, new List<NpcShip> { beta, alpha }, 0.5f, 1);
            if (string.IsNullOrWhiteSpace(selected) || !string.Equals(selected, second.ActiveScanner?.Name, StringComparison.Ordinal))
                return Fail("equal-distance scanner selection depended on list order");

            StepScan(first, reputation, player, credits, new List<NpcShip> { first.ActiveScanner }, 0.5f, 7);
            if (first.State != PoliceScanState.Cleared || first.CooldownRemaining <= 0f)
                return Fail("completed scan did not enter bounded retry cooldown");
            StepScan(first, reputation, player, credits, new List<NpcShip> { first.ActiveScanner }, 0.5f, 2);
            if (first.State == PoliceScanState.Scanning)
                return Fail("retry cooldown allowed immediate rescanning");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateSmugglingOfferGeneration()
        {
            MissionContext context = CreateMissionContext();
            List<Mission> first = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
            List<Mission> second = context.Manager.GenerateContrabandSmugglingMissions(context.Origin);
            if (first.Count != 3 || second.Count != 3)
                return Fail("expected easy, medium, and hard offers");
            string firstSignature = string.Join("|", first.Select(m => $"{m.Difficulty}:{m.DestinationStationId}:{m.CommodityId}:{m.RequiredQuantity}:{m.Reward}"));
            string secondSignature = string.Join("|", second.Select(m => $"{m.Difficulty}:{m.DestinationStationId}:{m.CommodityId}:{m.RequiredQuantity}:{m.Reward}"));
            if (!string.Equals(firstSignature, secondSignature, StringComparison.Ordinal) ||
                first.Any(m => m.FactionId != FactionManager.LibertyRogues || !CommodityCatalog.GetByIdOrName(m.CommodityId).IsContraband || m.DestinationStationId == m.OriginStationId))
                return Fail("offers were not canonical, Rogue-authored, and deterministic");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateSmugglingCapacityGuard()
        {
            MissionContext context = CreateMissionContext();
            Commodity filler = CommodityCatalog.GetById("water");
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            if (filler == null || contraband == null || !context.Player.CargoHold.AddCommodity(filler, 48))
                return Fail("could not fill cargo hold for capacity test");
            Mission offer = Mission.CreateContrabandSmuggling(context.Origin, context.Destination, contraband, 3, 5_000, MissionDifficulty.Easy);
            int usedBefore = context.Player.CargoHold.UsedCapacity;
            if (context.Manager.CanPlayerAcceptMission(offer, out _, context.Origin))
                return Fail("insufficient capacity did not block acceptance");
            if (context.Player.CargoHold.UsedCapacity != usedBefore || context.Manager.ActiveMission != null || offer.Status != MissionStatus.Available)
                return Fail("capacity rejection mutated mission or cargo state");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateSmugglingDeliveryExactlyOnce()
        {
            MissionContext context = CreateMissionContext();
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            Mission offer = Mission.CreateContrabandSmuggling(context.Origin, context.Destination, contraband, 2, 5_000, MissionDifficulty.Easy);
            float rogueStanding = context.Reputation.GetStanding(FactionManager.LibertyRogues);
            if (!context.Manager.AcceptMission(offer, context.Origin) ||
                context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) != 2)
                return Fail("smuggling acceptance did not load exact attributed cargo");
            int beforeCredits = context.Credits.Credits;
            if (!context.World.NotifyStationDocked(context.Destination) || offer.Status != MissionStatus.Rewarded)
                return Fail("destination docking did not complete and pay smuggling mission");
            if (context.World.NotifyStationDocked(context.Destination) ||
                context.Credits.Credits != beforeCredits + offer.Reward ||
                Math.Abs(context.Reputation.GetStanding(FactionManager.LibertyRogues) - (rogueStanding + offer.ReputationReward)) > 0.0001f ||
                context.Player.CargoHold.GetCommodityQuantity(contraband.Name) != 0)
                return Fail("smuggling reward/reputation/cargo transaction was not exactly once");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateSmugglingComplianceFailsContract()
        {
            MissionContext context = CreateMissionContext();
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            Mission offer = Mission.CreateContrabandSmuggling(context.Origin, context.Destination, contraband, 1, 5_000, MissionDifficulty.Easy);
            if (!context.Manager.AcceptMission(offer, context.Origin))
                return Fail("could not accept smuggling mission for compliance test");

            PoliceScanSystem scan = new();
            scan.SetMissionManager(context.Manager);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, context.Reputation, context.Player, context.Credits, new List<NpcShip> { scanner }, 0.5f, 8);
            if (!scan.TryAcceptEnforcement(context.Player, context.Credits, context.Reputation))
                return Fail("smuggling compliance did not complete");

            return offer.Status == MissionStatus.Failed && context.Manager.ActiveMission == null &&
                context.Player.CargoHold.GetMissionCargoQuantity(offer.Id) == 0 &&
                context.Player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
                !offer.RewardPaid && context.Credits.Credits == 125
                ? Pass()
                : Fail("confiscated mission cargo left a live or payable contract");
        }

        private (bool Success, string FailureReason) ValidateSmugglingRefusalCanDeliver()
        {
            MissionContext context = CreateMissionContext();
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            Mission offer = Mission.CreateContrabandSmuggling(context.Origin, context.Destination, contraband, 1, 5_000, MissionDifficulty.Easy);
            if (!context.Manager.AcceptMission(offer, context.Origin))
                return Fail("could not accept smuggling mission for refusal test");

            PoliceScanSystem scan = new();
            scan.SetMissionManager(context.Manager);
            NpcShip scanner = CreateScanner(FactionManager.LibertyPolice, new Vector3(1_500f, 0f, 0f));
            StepScan(scan, context.Reputation, context.Player, context.Credits, new List<NpcShip> { scanner }, 0.5f, 8);
            if (!scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation))
                return Fail("smuggling refusal did not resolve the demand");

            int beforeReward = context.Credits.Credits;
            bool delivered = context.World.NotifyStationDocked(context.Destination);
            return delivered && offer.Status == MissionStatus.Rewarded && offer.RewardPaid &&
                context.Credits.Credits == beforeReward + offer.Reward &&
                context.Player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
                reputationStillHostile(context.Reputation)
                ? Pass()
                : Fail("refused smuggling mission could not deliver while Police remained hostile");
        }

        private static bool reputationStillHostile(ReputationManager reputation) =>
            reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);

        private (bool Success, string FailureReason) ValidateSaveFields()
        {
            MissionContext context = CreateMissionContext();
            Mission offer = Mission.CreateContrabandSmuggling(
                context.Origin,
                context.Destination,
                CommodityCatalog.GetById("alien-organisms"),
                1,
                5_000,
                MissionDifficulty.Medium);
            if (!context.Manager.AcceptMission(offer, context.Origin))
                return Fail("could not create active smuggling mission for save capture");
            offer.SmugglingPoliceDetected = true;
            offer.SmugglingJettisonedQuantity = 1;
            SaveMissionData data = new SaveGameManager().CaptureMissions(new[] { offer })[0];
            if (data.Type != MissionType.ContrabandSmuggling || data.FactionId != FactionManager.LibertyRogues ||
                data.CommodityId != offer.CommodityId || data.RequiredQuantity != 1 || data.IssuedCargoQuantity != 1 ||
                data.MissionCargoLoaded != true || data.OriginStationId != offer.OriginStationId ||
                data.DestinationStationId != offer.DestinationStationId || data.SmugglingPoliceDetected != true ||
                data.SmugglingJettisonedQuantity != 1 || data.RewardPaid)
                return Fail("save capture omitted smuggling lifecycle fields");
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateNonLawfulIgnored()
        {
            PoliceScanSystem scan = new();
            ReputationManager reputation = CreateReputationManager();
            Ship player = CreatePlayer();
            PlayerCredits credits = new(10_000);
            if (!player.CargoHold.AddCommodity(CommodityCatalog.GetById("alien-organisms"), 1))
                return Fail("could not seed contraband");
            StepScan(scan, reputation, player, credits,
                new List<NpcShip> { CreateScanner(FactionManager.LibertyRogues, new Vector3(1_500f, 0f, 0f)) }, 0.5f, 8);
            return scan.State == PoliceScanState.Idle && credits.Credits == 10_000
                ? Pass()
                : Fail("non-lawful NPC initiated a police scan");
        }

        private static MissionManager CreateMissionManagerForPlayer(Ship player, out MissionWorldManager world)
        {
            MissionContext context = CreateMissionContext(player);
            world = context.World;
            return context.Manager;
        }

        private static MissionContext CreateMissionContext(Ship player = null)
        {
            player ??= CreatePlayer();
            PlayerCredits credits = new(1_000);
            ReputationManager reputation = CreateReputationManager();
            MissionManager manager = new(credits, null, reputation, null, player.CargoHold);
            MissionWaypointSystem waypointSystem = new();
            Station origin = CreateStation("Rogue Haven", FactionManager.LibertyRogues, 0f);
            Station destination = CreateStation("Fort Bush", FactionManager.LibertyCorporations, 4_000f);
            List<Station> stations = new() { origin, destination };
            List<NpcShip> npcs = new();
            List<SpaceObject> objects = stations.Cast<SpaceObject>().ToList();
            MissionWorldManager world = new(manager, waypointSystem, player, npcs, objects, () => stations);
            manager.SetWaypointSystem(waypointSystem);
            manager.SetWorldManager(world);
            return new MissionContext(player, credits, reputation, manager, world, origin, destination);
        }

        private static Station CreateStation(string name, string factionId, float x)
        {
            return new Station(new StationConfig
            {
                Description = name,
                FactionId = factionId,
                SystemIndex = 1,
                StartupPositionX = x,
                StartupPositionY = 0f,
                StartupPositionZ = 0f,
                Radius = 200f,
                DockingRange = 500f
            }, null);
        }

        private static ReputationManager CreateReputationManager() => new(new FactionManager());

        private static void StepScan(
            PoliceScanSystem scan,
            ReputationManager reputation,
            Ship player,
            PlayerCredits credits,
            IReadOnlyList<NpcShip> npcs,
            float seconds,
            int frameCount)
        {
            TimeSpan total = TimeSpan.Zero;
            for (int i = 0; i < frameCount; i++)
            {
                TimeSpan previous = total;
                total += TimeSpan.FromSeconds(seconds);
                scan.Update(new GameTime(previous, TimeSpan.FromSeconds(seconds)), player, npcs, credits, reputation);
            }
        }

        private static void StepLoot(LootManager loot, Ship player, float seconds)
        {
            loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)), player, false);
        }

        private static Ship CreatePlayer() => new(Vector3.Zero);

        private static NpcShip CreateScanner(string factionId, Vector3 position, string name = "Police Scan Test")
        {
            NpcShip scanner = new(name, position, position, 1f, 0f, factionId)
            {
                Position = position,
                Velocity = Vector3.Zero
            };
            return scanner;
        }

        private sealed record MissionContext(
            Ship Player,
            PlayerCredits Credits,
            ReputationManager Reputation,
            MissionManager Manager,
            MissionWorldManager World,
            Station Origin,
            Station Destination);

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
    }
}
