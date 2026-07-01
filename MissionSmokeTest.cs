using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for mission/world integration.
    /// </summary>
    internal sealed class MissionSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateBountyTargetSpawned, "bounty target spawned", ref passed, ref failed);
            RunCase(ValidateBountyCompletionReward, "bounty completion/reward", ref passed, ref failed);
            RunCase(ValidateDeliveryCargoAndDestination, "delivery cargo/destination", ref passed, ref failed);
            RunCase(ValidateDeliveryCompletionReward, "delivery completion/reward", ref passed, ref failed);
            RunCase(ValidateEscortTargetSpawned, "escort target spawned", ref passed, ref failed);
            RunCase(ValidateEscortHudAndTargeting, "escort HUD/targeting", ref passed, ref failed);
            RunCase(ValidateEscortCompletionReward, "escort completion/reward", ref passed, ref failed);
            RunCase(ValidateEscortFailureOnDestroy, "escort failure on destroy", ref passed, ref failed);
            RunCase(ValidateInvalidMissionSafety, "invalid mission safety", ref passed, ref failed);
            RunCase(ValidateMissionUiStrings, "mission UI strings", ref passed, ref failed);
            RunCase(ValidateHudFallbackText, "HUD fallback text", ref passed, ref failed);
            RunCase(ValidateLegacyDestinationAlias, "legacy destination alias", ref passed, ref failed);
            RunCase(ValidateRewardSanity, "reward sanity", ref passed, ref failed);
            RunCase(ValidateSaveLoadMissionState, "save/load mission state", ref passed, ref failed);

            Console.WriteLine($"[MISSION SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[MISSION SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[MISSION SMOKE] FAIL {label}: {result.FailureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[MISSION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateBountyTargetSpawned()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateBountyMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("bounty mission was not accepted");
            }

            if (mission.TargetSpaceObject is not NpcShip target)
            {
                return Fail("bounty mission did not bind a real NPC target");
            }

            if (!ctx.NpcShips.Contains(target))
            {
                return Fail("spawned bounty target was not added to the active NPC list");
            }

            if (!target.Name.Contains(mission.Target, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("spawned bounty target did not carry the mission name");
            }

            if (!ctx.ReputationManager.IsHostile(target.FactionId))
            {
                return Fail("spawned bounty target was not hostile");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateBountyCompletionReward()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateBountyMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("bounty mission was not accepted");
            }

            if (mission.TargetSpaceObject is not NpcShip target)
            {
                return Fail("bounty mission did not bind a real NPC target");
            }

            int creditsBefore = ctx.Credits.Credits;
            RunSilenced(() => target.Hull.TakeDamage(target.Hull.CurrentHull + 10f));

            if (mission.Status != MissionStatus.Completed)
            {
                return Fail("bounty mission did not complete after target destruction");
            }

            if (ctx.Credits.Credits != creditsBefore + mission.Reward)
            {
                return Fail("bounty reward was not paid");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDeliveryCargoAndDestination()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateDeliveryMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("delivery mission was not accepted");
            }

            string cargoName = ResolveMissionCargoName(mission.Target);
            if (ctx.Player.CargoHold.GetCommodityQuantity(cargoName) <= 0)
            {
                return Fail("delivery cargo was not assigned");
            }

            if (mission.TargetSpaceObject is not Station destination)
            {
                return Fail("delivery mission did not bind a destination station");
            }

            if (!ctx.Stations.Contains(destination))
            {
                return Fail("delivery destination was not a loaded station");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDeliveryCompletionReward()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateDeliveryMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("delivery mission was not accepted");
            }

            if (mission.TargetSpaceObject is not Station destination)
            {
                return Fail("delivery mission did not bind a destination station");
            }

            int creditsBefore = ctx.Credits.Credits;
            bool docked = ctx.DockUi.DockAtStation(destination);
            if (!docked)
            {
                return Fail("delivery dock call failed");
            }

            if (mission.Status != MissionStatus.Completed)
            {
                return Fail("delivery mission did not complete after docking");
            }

            if (ctx.Credits.Credits != creditsBefore + mission.Reward)
            {
                return Fail("delivery reward was not paid");
            }

            string cargoName = ResolveMissionCargoName(mission.Target);
            if (ctx.Player.CargoHold.GetCommodityQuantity(cargoName) != 0)
            {
                return Fail("delivery cargo was not removed on completion");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateMissionUiStrings()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission bounty = GenerateBountyMission(ctx);
            Mission delivery = GenerateDeliveryMission(ctx);
            Mission escort = GenerateEscortMission(ctx);

            string bountySummary = bounty.GetSummary();
            string bountyDetail = bounty.GetDetailedDescription();
            string deliverySummary = delivery.GetSummary();
            string deliveryDetail = delivery.GetDetailedDescription();
            string escortSummary = escort.GetSummary();
            string escortDetail = escort.GetDetailedDescription();
            string escortJobBoardLine = string.Join(" | ", new[]
            {
                $"Type: {escort.GetTypeLabel()}",
                $"Objective: Protect {escort.GetTargetLabel()}",
                $"Destination: {escort.GetDestinationLabel()}",
                $"Reward: {escort.Reward:N0} CR",
                $"Risk: {escort.GetRiskLabel()}",
                $"Client: {escort.GetClientLabel()}",
                $"Faction: {FactionManager.GetFactionDisplayName(escort.FactionId)}"
            });

            if (string.IsNullOrWhiteSpace(bountySummary) || string.IsNullOrWhiteSpace(bountyDetail))
            {
                return Fail("bounty mission UI strings were empty");
            }

            if (string.IsNullOrWhiteSpace(deliverySummary) || string.IsNullOrWhiteSpace(deliveryDetail))
            {
                return Fail("delivery mission UI strings were empty");
            }

            if (string.IsNullOrWhiteSpace(escortSummary) || string.IsNullOrWhiteSpace(escortDetail))
            {
                return Fail("escort mission UI strings were empty");
            }

            if (string.IsNullOrWhiteSpace(escortJobBoardLine))
            {
                return Fail("escort job board display string was empty");
            }

            if (!bountySummary.Contains("Reward:", StringComparison.OrdinalIgnoreCase) ||
                !bountySummary.Contains("Destroy", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("bounty summary did not include objective/reward data");
            }

            if (!deliveryDetail.Contains("Objective:", StringComparison.OrdinalIgnoreCase) ||
                !deliveryDetail.Contains("Reward:", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("delivery detail did not include objective/reward data");
            }

            if (!escortSummary.Contains("Escort", StringComparison.OrdinalIgnoreCase) ||
                !escortDetail.Contains("Protect", StringComparison.OrdinalIgnoreCase) ||
                !escortDetail.Contains("Destination:", StringComparison.OrdinalIgnoreCase) ||
                !escortDetail.Contains("Reward:", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("escort UI text did not include escort objective data");
            }

            if (!escortJobBoardLine.Contains("Type: ESCORT", StringComparison.OrdinalIgnoreCase) ||
                !escortJobBoardLine.Contains("Objective: Protect", StringComparison.OrdinalIgnoreCase) ||
                !escortJobBoardLine.Contains("Destination:", StringComparison.OrdinalIgnoreCase) ||
                !escortJobBoardLine.Contains("Reward:", StringComparison.OrdinalIgnoreCase) ||
                !escortJobBoardLine.Contains("Risk:", StringComparison.OrdinalIgnoreCase) ||
                !escortJobBoardLine.Contains("Client:", StringComparison.OrdinalIgnoreCase) ||
                !escortJobBoardLine.Contains("Faction:", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("escort job board display string was not shaped correctly");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateEscortTargetSpawned()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateEscortMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("escort mission was not accepted");
            }

            if (mission.TargetSpaceObject is not NpcShip escort)
            {
                return Fail("escort mission did not bind a real NPC escort");
            }

            if (!ctx.NpcShips.Contains(escort))
            {
                return Fail("spawned escort was not added to the active NPC list");
            }

            Station destination = ctx.WorldManager.ResolveDeliveryDestination(mission.Destination);
            if (destination == null)
            {
                return Fail("escort destination did not resolve to a real station");
            }

            if (!ReferenceEquals(mission.TargetSpaceObject, escort))
            {
                return Fail("escort mission target was not bound to the resolved escort ship");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateEscortHudAndTargeting()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateEscortMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("escort mission was not accepted");
            }

            MissionObjectivePanelInfo info = ctx.MissionHUD.GetActiveMissionPanelInfo(ctx.WaypointSystem, ctx.Player.Position);
            if (info == null)
            {
                return Fail("escort objective panel info was null");
            }

            string combined = string.Join(" | ",
                info.TitleLine,
                info.TypeLine,
                info.ObjectiveLine,
                info.TargetLine,
                info.StatusLine,
                info.DistanceLine,
                info.RewardLine,
                info.ClientLine);

            if (string.IsNullOrWhiteSpace(combined))
            {
                return Fail("escort objective panel text was empty");
            }

            if (combined.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("NaN", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("Infinity", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("escort objective panel text was not safe");
            }

            if (!NavTargeting.TryResolveMissionObjective(mission, ctx.SpaceObjects, ctx.NpcShips, out SpaceObject resolvedTarget, out string reason) || resolvedTarget == null)
            {
                return Fail(string.IsNullOrWhiteSpace(reason)
                    ? "escort target could not be resolved"
                    : reason);
            }

            if (resolvedTarget is not NpcShip && resolvedTarget is not Station)
            {
                return Fail("escort target resolved to an unsupported object");
            }

            if (!NavTargeting.TryStartGotoToMissionObjective(ctx.Player, mission, ctx.SpaceObjects, ctx.NpcShips, out string gotoReason))
            {
                return Fail(string.IsNullOrWhiteSpace(gotoReason)
                    ? "escort goto request was rejected without a reason"
                    : gotoReason);
            }

            if (!ctx.Player.IsGotoActive || ctx.Player.CurrentGotoTarget == null)
            {
                return Fail("escort goto request did not activate the ship");
            }

            if (!ReferenceEquals(ctx.Player.CurrentGotoTarget, resolvedTarget))
            {
                return Fail("escort goto request did not select the resolved mission objective");
            }

            ctx.Player.CancelGoto();
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateEscortCompletionReward()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateEscortMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("escort mission was not accepted");
            }

            if (mission.TargetSpaceObject is not NpcShip escort)
            {
                return Fail("escort mission did not bind a real NPC escort");
            }

            Station destination = ctx.WorldManager.ResolveDeliveryDestination(mission.Destination);
            if (destination == null)
            {
                return Fail("escort destination could not be resolved");
            }

            int creditsBefore = ctx.Credits.Credits;
            escort.Position = destination.Position;

            RunSilenced(() => ctx.WorldManager.Update(0.1f, null));

            if (mission.Status != MissionStatus.Completed)
            {
                return Fail("escort mission did not complete after the escort reached destination");
            }

            if (ctx.Credits.Credits != creditsBefore + mission.Reward)
            {
                return Fail("escort reward was not paid");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateEscortFailureOnDestroy()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = GenerateEscortMission(ctx);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("escort mission was not accepted");
            }

            if (mission.TargetSpaceObject is not NpcShip escort)
            {
                return Fail("escort mission did not bind a real NPC escort");
            }

            RunSilenced(() => escort.Hull.TakeDamage(escort.Hull.CurrentHull + 10f));

            if (mission.Status != MissionStatus.Failed)
            {
                return Fail("escort mission did not fail after the escort was destroyed");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateHudFallbackText()
        {
            Mission bounty = new Mission(
                MissionType.Bounty,
                MissionDifficulty.Medium,
                string.Empty,
                string.Empty,
                1_000,
                0f,
                string.Empty,
                FactionManager.LibertyRogues);

            Mission delivery = new Mission(
                MissionType.Delivery,
                MissionDifficulty.Easy,
                string.Empty,
                string.Empty,
                1_000,
                0f,
                string.Empty,
                FactionManager.LibertyCorporations);
            Mission escort = new Mission(
                MissionType.Escort,
                MissionDifficulty.Medium,
                string.Empty,
                string.Empty,
                1_000,
                0f,
                string.Empty,
                FactionManager.LibertyCorporations);

            if (bounty.GetTargetLabel() != "Target signal unresolved")
            {
                return Fail("bounty unresolved target text was not safe");
            }

            if (delivery.GetDestinationLabel() != "Destination unavailable")
            {
                return Fail("delivery unresolved destination text was not safe");
            }

            if (!bounty.GetHudFallbackLine().Contains("Target signal unresolved", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("bounty HUD fallback text did not mention unresolved target");
            }

            if (!delivery.GetHudFallbackLine().Contains("Destination unavailable", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("delivery HUD fallback text did not mention unavailable destination");
            }

            if (escort.GetTargetLabel() != "Escort signal unresolved")
            {
                return Fail("escort unresolved target text was not safe");
            }

            if (escort.GetDestinationLabel() != "Destination unavailable")
            {
                return Fail("escort unresolved destination text was not safe");
            }

            if (!escort.GetHudFallbackLine().Contains("Escort signal unresolved", StringComparison.OrdinalIgnoreCase) ||
                !escort.GetHudFallbackLine().Contains("Destination unavailable", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("escort HUD fallback text did not mention unresolved target and destination");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateLegacyDestinationAlias()
        {
            MissionSmokeContext ctx = CreateContext();
            Station destination = ctx.WorldManager.ResolveDeliveryDestination("Manhattan");
            if (destination == null)
            {
                return Fail("legacy alias did not resolve to a station");
            }

            if (!destination.Name.Contains("Fort Bush", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("legacy alias did not resolve to the expected station");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateRewardSanity()
        {
            MissionSmokeContext ctx = CreateContext();

            Mission delivery = null;
            Mission bounty = null;
            Mission escort = null;

            for (int i = 0; i < 24 && (delivery == null || bounty == null || escort == null); i++)
            {
                Mission mission = ctx.MissionManager.GenerateRandomMission(FactionManager.LibertyCorporations, ctx.Stations.FirstOrDefault());
                if (mission == null)
                {
                    continue;
                }

                if (mission.Type == MissionType.Delivery && delivery == null)
                {
                    delivery = mission;
                }
                else if (mission.Type == MissionType.Bounty && bounty == null)
                {
                    bounty = mission;
                }
                else if (mission.Type == MissionType.Escort && escort == null)
                {
                    escort = mission;
                }
            }

            if (delivery == null || bounty == null || escort == null)
            {
                return Fail("could not generate one of each mission type for reward sanity checks");
            }

            if (delivery.Reward < 500 || delivery.Reward > 10_000)
            {
                return Fail($"delivery reward {delivery.Reward} was outside the early-game sanity range");
            }

            if (bounty.Reward < 1_000 || bounty.Reward > 15_000)
            {
                return Fail($"bounty reward {bounty.Reward} was outside the early-game sanity range");
            }

            if (escort.Reward < 1_000 || escort.Reward > 15_000)
            {
                return Fail($"escort reward {escort.Reward} was outside the early-game sanity range");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateInvalidMissionSafety()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = CreateDeliveryMission("Medical Supplies", "Missing Station", 1_100);

            bool accepted = ctx.MissionManager.AcceptMission(mission);
            if (accepted)
            {
                return Fail("invalid delivery mission should have been rejected");
            }

            if (mission.Status != MissionStatus.Available)
            {
                return Fail("invalid delivery mission did not revert to available");
            }

            if (ctx.Player.CargoHold.GetCommodityQuantity("Medical Supplies") != 0)
            {
                return Fail("invalid delivery mission incorrectly assigned cargo");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateSaveLoadMissionState()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "Roguelancer_MissionSmoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            SaveGameManager saveGameManager = new SaveGameManager(Path.Combine(tempDirectory, "player_save.json"));

            try
            {
                MissionSmokeContext source = CreateContext();
                Mission bounty = CreateBountyMission("Saved Pirate Target", "Last seen near Manhattan", 2_250);
                Mission delivery = CreateDeliveryMission("Construction Materials", "Newark Station", 1_400);
                Mission escort = CreateEscortMission("Smoke Escort Freighter", "Trenton Outpost", 1_650);

                if (!source.MissionManager.AcceptMission(bounty))
                {
                    return Fail("source bounty mission was not accepted");
                }

                if (!source.MissionManager.AcceptMission(delivery))
                {
                    return Fail("source delivery mission was not accepted");
                }

                if (!source.MissionManager.AcceptMission(escort))
                {
                    return Fail("source escort mission was not accepted");
                }

                SaveGameData saveData = new SaveGameData
                {
                    PlayerCredits = source.Credits.Credits,
                    CurrentSystemIndex = 1,
                    CurrentShipName = "Scimitar",
                    PlayerPosition = SaveVector3Data.From(source.Player.Position),
                    PlayerVelocity = SaveVector3Data.From(source.Player.Velocity),
                    PlayerForward = SaveVector3Data.From(source.Player.Forward),
                    Cargo = saveGameManager.CaptureCargo(source.Player.CargoHold),
                    FactionReputation = saveGameManager.CaptureReputation(source.ReputationManager),
                    ActiveMissions = saveGameManager.CaptureMissions(source.MissionManager.ActiveMissions)
                };

                string saveFailure = string.Empty;
                bool saved = RunSilenced(() => saveGameManager.TrySave(saveData, out saveFailure));
                if (!saved)
                {
                    return Fail($"save failed unexpectedly: {saveFailure}");
                }

                string loadFailure = string.Empty;
                SaveGameData loadedData = null;
                bool loaded = RunSilenced(() => saveGameManager.TryLoad(out loadedData, out loadFailure));
                if (!loaded || loadedData == null)
                {
                    return Fail($"load failed unexpectedly: {loadFailure}");
                }

                MissionSmokeContext loadedContext = CreateContext();
                loadedContext.Credits.SetCredits(loadedData.PlayerCredits);
                saveGameManager.ApplyCargo(loadedContext.Player.CargoHold, loadedData, out _);
                saveGameManager.ApplyReputation(loadedContext.ReputationManager, loadedData);
                saveGameManager.ApplyMissions(loadedContext.MissionManager, loadedData, out _);
                loadedContext.WorldManager.RebindActiveMissions(loadedContext.MissionManager.ActiveMissions);

                if (loadedContext.MissionManager.ActiveMissions.Count != 3)
                {
                    return Fail("loaded mission state did not restore the active mission count");
                }

                if (loadedContext.MissionManager.ActiveMissions.Any(m => m.Status != MissionStatus.Active))
                {
                    return Fail("loaded active missions did not remain active");
                }

                if (loadedContext.Player.CargoHold.GetCommodityQuantity("Construction Materials") <= 0)
                {
                    return Fail("loaded delivery cargo did not round-trip");
                }

                Mission loadedEscort = loadedContext.MissionManager.ActiveMissions.FirstOrDefault(m => m.Type == MissionType.Escort);
                if (loadedEscort == null)
                {
                    return Fail("loaded escort mission was missing");
                }

                if (!NavTargeting.TryResolveMissionObjective(loadedEscort, loadedContext.SpaceObjects, loadedContext.NpcShips, out SpaceObject loadedEscortTarget, out string escortReason) || loadedEscortTarget == null)
                {
                    return Fail(string.IsNullOrWhiteSpace(escortReason)
                        ? "loaded escort mission did not resolve to an objective"
                        : escortReason);
                }

                if (loadedEscortTarget is not NpcShip && loadedEscortTarget is not Station)
                {
                    return Fail("loaded escort mission resolved to an unsupported object");
                }

                return Pass();
            }
            finally
            {
                TryCleanupDirectory(tempDirectory);
            }
        }

        private MissionSmokeContext CreateContext()
        {
            return RunSilenced(() =>
            {
                var ctx = new MissionSmokeContext
                {
                    Credits = new PlayerCredits(0),
                    ReputationManager = new ReputationManager(new FactionManager()),
                    Player = new Ship(Vector3.Zero)
                };

                ctx.Stations.AddRange(CreateStations());
                ctx.SpaceObjects.AddRange(ctx.Stations);
                ctx.WaypointSystem = new MissionWaypointSystem();
                ctx.MissionManager = new MissionManager(ctx.Credits, null, ctx.ReputationManager);
                ctx.WorldManager = new MissionWorldManager(
                    ctx.MissionManager,
                    ctx.WaypointSystem,
                    ctx.Player,
                    ctx.NpcShips,
                    ctx.SpaceObjects,
                    () => ctx.Stations,
                    npc => ctx.WorldManager?.NotifyNpcDestroyed(npc));
                ctx.MissionManager.SetWaypointSystem(ctx.WaypointSystem);
                ctx.MissionManager.SetWorldManager(ctx.WorldManager);
                ctx.MissionHUD = new MissionGuidanceHUD(null, null);
                ctx.DockUi = new StationDockUI(null, null, new ShipDealer(), new CommodityDealer(), ctx.MissionManager, ctx.ReputationManager);
                ctx.DockUi.SetMissionWorldManager(ctx.WorldManager);
                return ctx;
            });
        }

        private static List<Station> CreateStations()
        {
            return new List<Station>
            {
                new Station(new StationConfig
                {
                    Description = "Fort Bush",
                    StartupPositionX = 0f,
                    StartupPositionY = 0f,
                    StartupPositionZ = 0f,
                    Radius = 1200f,
                    DockingRange = 900f,
                    FactionId = FactionManager.LibertyPolice
                }, null),
                new Station(new StationConfig
                {
                    Description = "Newark Station",
                    StartupPositionX = 6000f,
                    StartupPositionY = 0f,
                    StartupPositionZ = 1200f,
                    Radius = 1200f,
                    DockingRange = 900f,
                    FactionId = FactionManager.LibertyPolice
                }, null),
                new Station(new StationConfig
                {
                    Description = "Trenton Outpost",
                    StartupPositionX = -4200f,
                    StartupPositionY = 0f,
                    StartupPositionZ = 1800f,
                    Radius = 1200f,
                    DockingRange = 900f,
                    FactionId = FactionManager.LibertyPolice
                }, null)
            };
        }

        private static Mission CreateBountyMission(string targetName, string destination, int reward)
        {
            return new Mission(
                MissionType.Bounty,
                MissionDifficulty.Medium,
                targetName,
                destination,
                reward,
                0f,
                $"Destroy {targetName}",
                FactionManager.BountyHunters);
        }

        private static Mission CreateDeliveryMission(string cargoName, string destination, int reward)
        {
            return new Mission(
                MissionType.Delivery,
                MissionDifficulty.Easy,
                cargoName,
                destination,
                reward,
                0f,
                $"Deliver {cargoName} to {destination}",
                FactionManager.LibertyCorporations);
        }

        private static Mission CreateEscortMission(string escortName, string destination, int reward)
        {
            return new Mission(
                MissionType.Escort,
                MissionDifficulty.Medium,
                escortName,
                destination,
                reward,
                0f,
                $"Escort {escortName} to {destination}",
                FactionManager.LibertyCorporations);
        }

        private static Mission GenerateBountyMission(MissionSmokeContext ctx)
        {
            for (int i = 0; i < 24; i++)
            {
                Mission mission = ctx.MissionManager.GenerateRandomMission(FactionManager.LibertyCorporations, ctx.Stations.FirstOrDefault());
                if (mission.Type == MissionType.Bounty)
                {
                    return mission;
                }
            }

            return CreateBountyMission("Smoke Bounty Target", "Last seen near Manhattan", 1_500);
        }

        private static Mission GenerateDeliveryMission(MissionSmokeContext ctx)
        {
            for (int i = 0; i < 24; i++)
            {
                Mission mission = ctx.MissionManager.GenerateRandomMission(FactionManager.LibertyCorporations, ctx.Stations.FirstOrDefault());
                if (mission.Type == MissionType.Delivery)
                {
                    return mission;
                }
            }

            return CreateDeliveryMission("H-Fuel Cells", "Newark Station", 1_250);
        }

        private static Mission GenerateEscortMission(MissionSmokeContext ctx)
        {
            for (int i = 0; i < 24; i++)
            {
                Mission mission = ctx.MissionManager.GenerateRandomMission(FactionManager.LibertyCorporations, ctx.Stations.FirstOrDefault());
                if (mission.Type == MissionType.Escort)
                {
                    return mission;
                }
            }

            return CreateEscortMission("Smoke Escort Freighter", "Newark Station", 1_450);
        }

        private static string ResolveMissionCargoName(string missionTarget)
        {
            Commodity commodity = CommodityCatalog.GetByIdOrName(missionTarget);
            if (commodity != null)
            {
                return commodity.Name;
            }

            string normalized = (missionTarget ?? string.Empty).Trim().ToLowerInvariant();
            normalized = normalized.Replace(" cells", string.Empty);

            return normalized switch
            {
                "medical supplies" => "Medical Supplies",
                "h-fuel" => "H-Fuel",
                "luxury goods" => "Luxury Goods",
                "construction materials" => "Construction Materials",
                "military hardware" => "Side Arms",
                "food rations" => "Food Rations",
                "side arms" => "Side Arms",
                "engine components" => "Engine Components",
                "boron" => "Boron",
                "diamonds" => "Diamonds",
                _ => missionTarget
            };
        }

        private static (bool Success, string FailureReason) Pass()
        {
            return (true, string.Empty);
        }

        private static (bool Success, string FailureReason) Fail(string reason)
        {
            return (false, reason);
        }

        private static void TryCleanupDirectory(string directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
            }
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
            _ = RunSilenced(() =>
            {
                action();
                return 0;
            });
        }

        private sealed class MissionSmokeContext
        {
            public PlayerCredits Credits { get; set; }
            public ReputationManager ReputationManager { get; set; }
            public Ship Player { get; set; }
            public List<NpcShip> NpcShips { get; } = new();
            public List<SpaceObject> SpaceObjects { get; } = new();
            public List<Station> Stations { get; } = new();
            public MissionWaypointSystem WaypointSystem { get; set; }
            public MissionManager MissionManager { get; set; }
            public MissionWorldManager WorldManager { get; set; }
            public MissionGuidanceHUD MissionHUD { get; set; }
            public StationDockUI DockUi { get; set; }
        }
    }
}
