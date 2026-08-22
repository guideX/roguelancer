using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Remote-friendly mission validation. This deliberately tests
    /// the authoritative state machine without requiring a rendered traversal.
    /// </summary>
    internal sealed class MissionSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateCatalog, "catalog", ref passed, ref failed);
            RunCase(ValidateBoardAndAcceptance, "board/acceptance/single active", ref passed, ref failed);
            RunCase(ValidateCourierCapacityAndFlow, "courier capacity/delivery transaction", ref passed, ref failed);
            RunCase(ValidateCourierSaveLoad, "courier save/load integrity", ref passed, ref failed);
            RunCase(ValidateReachLocation, "reach-location objective", ref passed, ref failed);
            RunCase(ValidateDestroyHostiles, "destroy-hostiles attribution/progress", ref passed, ref failed);
            RunCase(ValidateRewardTransaction, "reward transaction", ref passed, ref failed);
            RunCase(ValidateSaveLoad, "save/load mission state", ref passed, ref failed);
            RunCase(ValidateHudAndDialogueData, "HUD/dialogue data", ref passed, ref failed);

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
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[MISSION SMOKE] FAIL {label}: {result.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[MISSION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateCatalog()
        {
            if (!MissionCatalog.Validate(out string reason))
                return Fail($"catalog validation failed: {reason}");

            if (MissionCatalog.All.Count != 3)
                return Fail($"expected 3 prototype jobs, found {MissionCatalog.All.Count}");

            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (MissionDefinition definition in MissionCatalog.All)
            {
                if (!ids.Add(definition.Id))
                    return Fail($"duplicate catalog id: {definition.Id}");
                if (definition.RewardCredits <= 0 || definition.TargetCount <= 0)
                    return Fail($"invalid reward/target metadata for {definition.Id}");
                if (MissionCatalog.GetById(definition.Id) == null)
                    return Fail($"catalog lookup failed for {definition.Id}");
            }

            MissionDefinition courier = MissionCatalog.GetById(MissionCatalog.PriorityDispatchId);
            if (courier == null || courier.Type != MissionType.CourierDelivery ||
                courier.SourceStationName != "Newark Station" || courier.DestinationStationName != "Buffalo Base" ||
                courier.PackageId != "sealed-data-package" || courier.PackageQuantity != 1 || courier.PackageVolume != 1)
            {
                return Fail("courier catalog metadata was incomplete or incorrect");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCourierCapacityAndFlow()
        {
            MissionSmokeContext ctx = CreateCourierContext();
            Mission mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PriorityDispatchId));
            Commodity package = CommodityCatalog.GetById("sealed-data-package");
            if (package == null)
                return Fail("courier package definition was not available");

            int freeBefore = ctx.Player.CargoHold.AvailableCapacity;
            if (!ctx.MissionManager.AcceptMission(mission, ctx.Origin))
                return Fail("courier mission was not accepted with free capacity");
            if (ctx.Player.CargoHold.AvailableCapacity != freeBefore - mission.PackageVolume ||
                !ctx.Player.CargoHold.HasMissionCargo(mission.Id, mission.PackageId, mission.PackageQuantity) ||
                ctx.Player.CargoHold.GetMissionCargoReservations().Count != 1)
            {
                return Fail("courier package did not consume exactly one authoritative reservation");
            }

            CommodityDealer dealer = new();
            dealer.SetDockedStation(ctx.Origin);
            PlayerCredits saleCredits = new PlayerCredits(0);
            if (dealer.TrySellCommodity(package, 1, saleCredits, ctx.Player.CargoHold, out _))
                return Fail("mission package was exposed to normal commodity selling");
            if (!ctx.Player.CargoHold.HasMissionCargo(mission.Id, mission.PackageId, mission.PackageQuantity))
                return Fail("rejected mission-cargo sale mutated the reservation");

            Station wrongStation = CreateStation("Fort Bush", 1, 10000f, 0f, 0f);
            ctx.Stations.Add(wrongStation);
            if (ctx.WorldManager.NotifyStationDocked(wrongStation))
                return Fail("wrong-station docking reported courier completion");
            if (mission.Status != MissionStatus.InProgress || !ctx.Player.CargoHold.HasMissionCargo(mission.Id, mission.PackageId, 1))
                return Fail("wrong-station docking changed courier state or cargo");

            Station sameNameWrongSystem = CreateStation("Buffalo Base", 9, 12000f, 0f, 0f);
            ctx.Stations.Add(sameNameWrongSystem);
            if (ctx.WorldManager.NotifyStationDocked(sameNameWrongSystem))
                return Fail("same-name station in another system reported courier completion");

            Station destination = ctx.Stations.First(station => station.Name == "Buffalo Base");
            if (!ctx.WorldManager.NotifyStationDocked(destination))
                return Fail("correct destination did not process courier delivery");
            if (mission.Status != MissionStatus.Completed || !mission.ObjectiveComplete ||
                mission.MissionCargoLoaded || mission.DeliveredQuantity != 1 ||
                ctx.Player.CargoHold.GetMissionCargoReservations().Count != 0 ||
                ctx.Player.CargoHold.GetCommodityQuantity(package.Name) != 0)
            {
                return Fail("courier delivery did not remove the package exactly once");
            }

            int creditsBefore = ctx.Credits.Credits;
            if (ctx.WorldManager.NotifyStationDocked(destination))
                return Fail("duplicate courier docking reported a second delivery");
            if (!ctx.MissionManager.TryClaimReward(mission, ctx.Origin, out _) ||
                ctx.Credits.Credits != creditsBefore + mission.Reward)
            {
                return Fail("courier reward could not be claimed exactly once at origin");
            }
            if (ctx.MissionManager.TryClaimReward(mission, ctx.Origin, out _))
                return Fail("courier reward was claimable twice");

            MissionSmokeContext fullContext = CreateCourierContext();
            Commodity water = CommodityCatalog.GetById("water");
            if (water == null || !fullContext.Player.CargoHold.AddCommodity(water, fullContext.Player.CargoHold.MaxCapacity))
                return Fail("could not stage full cargo hold for capacity rejection");
            Mission rejected = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PriorityDispatchId));
            if (fullContext.MissionManager.AcceptMission(rejected, fullContext.Origin))
                return Fail("courier accepted despite insufficient capacity");
            if (fullContext.MissionManager.ActiveMission != null || rejected.Status != MissionStatus.Available ||
                fullContext.Player.CargoHold.GetMissionCargoReservations().Count != 0 ||
                fullContext.Player.CargoHold.UsedCapacity != fullContext.Player.CargoHold.MaxCapacity)
            {
                return Fail("insufficient courier capacity rejection was not atomic");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCourierSaveLoad()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"roguelancer-courier-smoke-{Guid.NewGuid():N}");
            string savePath = Path.Combine(directory, "courier-save.json");
            try
            {
                MissionSmokeContext source = CreateCourierContext();
                Mission courier = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PriorityDispatchId));
                if (!source.MissionManager.AcceptMission(courier, source.Origin))
                    return Fail("courier could not be accepted before save");

                SaveGameManager saveManager = new(savePath);
                SaveGameData data = new()
                {
                    PlayerCredits = source.Credits.Credits,
                    CurrentSystemIndex = source.Origin.Config.SystemIndex,
                    Cargo = saveManager.CaptureCargo(source.Player.CargoHold),
                    ActiveMissions = saveManager.CaptureMissions(source.MissionManager.ActiveMissions)
                };
                if (data.Cargo.Count != 1 || data.Cargo[0].MissionId != courier.Id || !data.Cargo[0].MissionBound)
                    return Fail("courier save did not encode one mission-bound package");
                string saveFailure = string.Empty;
                string loadFailure = string.Empty;
                SaveGameData loaded = null;
                if (!saveManager.TrySave(data, out saveFailure) || !saveManager.TryLoad(out loaded, out loadFailure))
                    return Fail($"courier save/load failed: {saveFailure} {loadFailure}");

                MissionSmokeContext resumed = CreateCourierContext();
                saveManager.ApplyCargo(resumed.Player.CargoHold, loaded, out List<string> cargoWarnings);
                saveManager.ApplyMissions(resumed.MissionManager, loaded, out List<string> missionWarnings);
                resumed.WorldManager.RebindActiveMissions(resumed.MissionManager.ActiveMissions);
                if (cargoWarnings.Count != 0 || missionWarnings.Count != 0)
                    return Fail($"courier reload warnings: {string.Join("; ", cargoWarnings.Concat(missionWarnings))}");
                Mission resumedMission = resumed.MissionManager.ActiveMission;
                if (resumedMission == null || resumedMission.Type != MissionType.CourierDelivery ||
                    !resumedMission.MissionCargoLoaded ||
                    !resumed.Player.CargoHold.HasMissionCargo(resumedMission.Id, resumedMission.PackageId, 1) ||
                    resumed.Player.CargoHold.GetMissionCargoReservations().Count != 1)
                {
                    return Fail("courier mission/package did not survive reload");
                }

                SaveGameData secondData = new()
                {
                    PlayerCredits = resumed.Credits.Credits,
                    Cargo = saveManager.CaptureCargo(resumed.Player.CargoHold),
                    ActiveMissions = saveManager.CaptureMissions(resumed.MissionManager.ActiveMissions)
                };
                resumed.Player.CargoHold.Clear();
                resumed.MissionManager.ClearState();
                saveManager.ApplyCargo(resumed.Player.CargoHold, secondData, out cargoWarnings);
                saveManager.ApplyMissions(resumed.MissionManager, secondData, out missionWarnings);
                resumed.WorldManager.RebindActiveMissions(resumed.MissionManager.ActiveMissions);
                if (cargoWarnings.Count != 0 || missionWarnings.Count != 0 ||
                    resumed.Player.CargoHold.GetMissionCargoReservations().Count != 1 ||
                    resumed.Player.CargoHold.GetMissionCargoQuantity(resumedMission.Id) != 1)
                {
                    return Fail("repeated courier save/load multiplied or lost the package");
                }

                return Pass();
            }
            finally
            {
                TryCleanupDirectory(directory);
            }
        }

        private (bool Success, string FailureReason) ValidateBoardAndAcceptance()
        {
            MissionSmokeContext ctx = CreateContext();
            JobBoard board = new(ctx.MissionManager);
            board.RefreshMissions(2, ctx.Origin.FactionId, ctx.Origin);

            if (board.AvailableMissions.Count != 2)
                return Fail("mission board did not expose the fixed two-job catalog");
            if (!board.AvailableMissions.Any(mission => mission.Type == MissionType.ReachLocation) ||
                !board.AvailableMissions.Any(mission => mission.Type == MissionType.DestroyHostiles))
                return Fail("mission board did not expose both bounded mission types");

            Mission selected = board.AvailableMissions[0];
            int creditsBefore = ctx.Credits.Credits;
            if (!board.AcceptSelectedMission())
                return Fail("selected mission was not accepted");
            if (ctx.MissionManager.ActiveMission == null ||
                ctx.MissionManager.ActiveMission.Status != MissionStatus.InProgress)
                return Fail("accepted mission was not authoritative InProgress state");
            if (ctx.MissionManager.ActiveMission.OriginStationId != Mission.BuildStationIdentity(ctx.Origin))
                return Fail("origin station identity was not recorded");
            if (ctx.Credits.Credits != creditsBefore)
                return Fail("acceptance changed credits");
            if (board.AcceptSelectedMission())
                return Fail("repeated board activation accepted a second mission");

            Mission second = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.RogueHuntId));
            if (ctx.MissionManager.AcceptMission(second, ctx.Origin))
                return Fail("second mission was accepted while one was active");
            if (!ReferenceEquals(ctx.MissionManager.ActiveMission, selected))
                return Fail("active mission reference changed after rejected acceptance");

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateReachLocation()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PatrolSweepId));
            if (!ctx.MissionManager.AcceptMission(mission, ctx.Origin))
                return Fail("patrol mission was not accepted");
            if (!mission.TargetPosition.HasValue)
                return Fail("patrol marker did not resolve to a position");

            Vector3 marker = mission.TargetPosition.Value;
            ctx.Player.Position = marker;
            ctx.WorldManager.Update(0.1f, null, ctx.Origin.Config.SystemIndex + 1);
            if (mission.Status == MissionStatus.Completed)
                return Fail("wrong system completed the patrol objective");

            ctx.Player.Position = marker + Vector3.Right * (mission.ObjectiveRadius + 100f);
            ctx.WorldManager.Update(0.1f, null, ctx.Origin.Config.SystemIndex);
            if (mission.Status == MissionStatus.Completed)
                return Fail("outside-radius position completed the patrol objective");

            ctx.Player.Position = marker;
            ctx.WorldManager.Update(0.1f, null, ctx.Origin.Config.SystemIndex);
            if (mission.Status != MissionStatus.Completed || !mission.ObjectiveComplete)
                return Fail("entering the valid patrol radius did not complete once");
            if (ctx.Credits.Credits != 10000)
                return Fail("objective completion paid credits before station claim");

            ctx.WorldManager.Update(0.1f, null, ctx.Origin.Config.SystemIndex);
            if (ctx.MissionManager.CompletedMissions.Count != 1)
                return Fail("repeated reach updates changed completion state");

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDestroyHostiles()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.RogueHuntId));
            if (!ctx.MissionManager.AcceptMission(mission, ctx.Origin))
                return Fail("rogue hunt was not accepted");

            List<NpcShip> missionTargets = ctx.NpcShips
                .Where(npc => npc.Name.Contains("[MISSION]", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (missionTargets.Count != mission.RequiredProgress)
                return Fail($"expected {mission.RequiredProgress} mission targets, found {missionTargets.Count}");

            NpcShip unrelated = new(
                "Unrelated Friendly",
                ctx.Player.Position + Vector3.Left * 500f,
                ctx.Player.Position,
                1f,
                0f,
                FactionManager.LibertyCorporations);
            unrelated.MarkDamagedByPlayer();
            unrelated.OnDestroyed += ctx.WorldManager.NotifyNpcDestroyed;
            ctx.NpcShips.Add(unrelated);
            ctx.WorldManager.NotifyNpcDestroyed(unrelated);
            if (mission.CurrentProgress != 0)
                return Fail("unrelated friendly destruction counted as mission progress");

            NpcShip uncreditedTarget = missionTargets[0];
            uncreditedTarget.Hull.TakeDamage(uncreditedTarget.Hull.CurrentHull + 10f);
            if (mission.CurrentProgress != 0)
                return Fail("unattributed hostile destruction counted as mission progress");
            if (mission.Status != MissionStatus.Failed)
                return Fail("unattributed mission target destruction did not fail safely");

            ctx = CreateContext();
            mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.RogueHuntId));
            if (!ctx.MissionManager.AcceptMission(mission, ctx.Origin))
                return Fail("qualifying rogue hunt was not accepted");
            missionTargets = ctx.NpcShips
                .Where(npc => npc.Name.Contains("[MISSION]", StringComparison.OrdinalIgnoreCase))
                .ToList();

            NpcShip first = missionTargets[0];
            first.MarkDamagedByPlayer();
            first.Hull.TakeDamage(first.Hull.CurrentHull + 10f);
            if (mission.CurrentProgress != 1)
                return Fail("qualifying hostile destruction did not increment once");
            ctx.WorldManager.NotifyNpcDestroyed(first);
            if (mission.CurrentProgress != 1)
                return Fail("duplicate destruction callback double-counted a hostile");

            foreach (NpcShip target in missionTargets.Skip(1))
            {
                target.MarkDamagedByPlayer();
                target.Hull.TakeDamage(target.Hull.CurrentHull + 10f);
            }
            if (mission.CurrentProgress != mission.RequiredProgress ||
                mission.Status != MissionStatus.Completed)
                return Fail("hostile objective did not complete at the required count");
            if (ctx.Credits.Credits != 10000)
                return Fail("hostile objective paid before station claim");

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateRewardTransaction()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PatrolSweepId));
            if (!ctx.MissionManager.AcceptMission(mission, ctx.Origin))
                return Fail("reward test mission was not accepted");

            if (ctx.MissionManager.TryClaimReward(mission, ctx.Origin, out _))
                return Fail("incomplete mission paid a reward");

            ctx.Player.Position = mission.TargetPosition.Value;
            ctx.WorldManager.Update(0.1f, null, ctx.Origin.Config.SystemIndex);
            int creditsBeforeClaim = ctx.Credits.Credits;
            if (!ctx.MissionManager.TryClaimReward(mission, ctx.Origin, out string message))
                return Fail($"completed mission could not be claimed: {message}");
            if (ctx.Credits.Credits != creditsBeforeClaim + mission.Reward)
                return Fail("claim did not add the exact mission reward");
            if (mission.Status != MissionStatus.Rewarded || !mission.RewardPaid ||
                ctx.MissionManager.ActiveMission != null ||
                ctx.MissionManager.CompletedMissions.Count != 0)
                return Fail("reward did not atomically advance/clear mission state");

            if (ctx.MissionManager.TryClaimReward(mission, ctx.Origin, out _))
                return Fail("second reward claim paid again");
            if (ctx.Credits.Credits != creditsBeforeClaim + mission.Reward)
                return Fail("duplicate reward claim changed credits");

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateSaveLoad()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"roguelancer-mission-smoke-{Guid.NewGuid():N}");
            string savePath = Path.Combine(directory, "mission-save.json");

            try
            {
                MissionSmokeContext partialSource = CreateContext();
                Mission partialMission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.RogueHuntId));
                if (!partialSource.MissionManager.AcceptMission(partialMission, partialSource.Origin))
                    return Fail("partial-progress mission was not accepted");
                NpcShip partialTarget = partialSource.NpcShips
                    .First(npc => npc.Name.Contains("[MISSION]", StringComparison.OrdinalIgnoreCase));
                partialTarget.MarkDamagedByPlayer();
                partialTarget.Hull.TakeDamage(partialTarget.Hull.CurrentHull + 10f);
                if (partialMission.CurrentProgress != 1)
                    return Fail("partial progress was not recorded before save");

                SaveGameManager saveManager = new(savePath);
                SaveGameData partialData = new()
                {
                    PlayerCredits = partialSource.Credits.Credits,
                    CurrentSystemIndex = partialSource.Origin.Config.SystemIndex,
                    ActiveMissions = saveManager.CaptureMissions(partialSource.MissionManager.ActiveMissions)
                };
                if (!saveManager.TrySave(partialData, out string saveFailure))
                    return Fail($"partial save failed: {saveFailure}");
                if (!saveManager.TryLoad(out SaveGameData loadedPartial, out string loadFailure))
                    return Fail($"partial load failed: {loadFailure}");

                MissionSmokeContext partialResume = CreateContext();
                partialResume.Credits.SetCredits(loadedPartial.PlayerCredits);
                saveManager.ApplyMissions(partialResume.MissionManager, loadedPartial, out List<string> warnings);
                if (warnings.Count != 0)
                    return Fail($"partial mission load emitted warnings: {string.Join(", ", warnings)}");
                partialResume.WorldManager.RebindActiveMissions(partialResume.MissionManager.ActiveMissions);
                Mission resumedPartial = partialResume.MissionManager.ActiveMission;
                if (resumedPartial == null ||
                    resumedPartial.CurrentProgress != 1 ||
                    resumedPartial.RequiredProgress != 3 ||
                    resumedPartial.OriginStationId != Mission.BuildStationIdentity(partialResume.Origin))
                    return Fail("partial mission state did not resume with progress/origin intact");

                MissionSmokeContext completedSource = CreateContext();
                Mission completedMission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PatrolSweepId));
                if (!completedSource.MissionManager.AcceptMission(completedMission, completedSource.Origin))
                    return Fail("completed-save mission was not accepted");
                completedSource.Player.Position = completedMission.TargetPosition.Value;
                completedSource.WorldManager.Update(0.1f, null, completedSource.Origin.Config.SystemIndex);
                if (completedMission.Status != MissionStatus.Completed)
                    return Fail("completed mission did not reach unclaimed state before save");

                SaveGameData completedData = new()
                {
                    PlayerCredits = completedSource.Credits.Credits,
                    CurrentSystemIndex = completedSource.Origin.Config.SystemIndex,
                    CompletedMissions = saveManager.CaptureMissions(completedSource.MissionManager.CompletedMissions)
                };
                if (!saveManager.TrySave(completedData, out saveFailure))
                    return Fail($"completed save failed: {saveFailure}");
                if (!saveManager.TryLoad(out SaveGameData loadedCompleted, out loadFailure))
                    return Fail($"completed load failed: {loadFailure}");

                MissionSmokeContext completedResume = CreateContext();
                completedResume.Credits.SetCredits(loadedCompleted.PlayerCredits);
                saveManager.ApplyMissions(completedResume.MissionManager, loadedCompleted, out warnings);
                if (warnings.Count != 0 || completedResume.MissionManager.UnclaimedCompletedMission == null)
                    return Fail("completed/unclaimed mission did not persist across reload");
                Mission resumedCompleted = completedResume.MissionManager.UnclaimedCompletedMission;
                int beforeReward = completedResume.Credits.Credits;
                if (!completedResume.MissionManager.TryClaimReward(resumedCompleted, completedResume.Origin, out _))
                    return Fail("reloaded completed mission could not be claimed");
                if (completedResume.Credits.Credits != beforeReward + resumedCompleted.Reward)
                    return Fail("reloaded reward amount was incorrect");
                if (completedResume.MissionManager.TryClaimReward(resumedCompleted, completedResume.Origin, out _))
                    return Fail("reloaded reward could be claimed twice");

                return Pass();
            }
            finally
            {
                TryCleanupDirectory(directory);
            }
        }

        private (bool Success, string FailureReason) ValidateHudAndDialogueData()
        {
            MissionSmokeContext ctx = CreateContext();
            Mission mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.RogueHuntId));
            if (!ctx.MissionManager.AcceptMission(mission, ctx.Origin))
                return Fail("HUD test mission was not accepted");

            if (!mission.GetHudProgressLine().Contains("0 / 3", StringComparison.Ordinal) ||
                !mission.GetDetailedDescription().Contains("DESTROY HOSTILES", StringComparison.OrdinalIgnoreCase))
                return Fail("active mission HUD/detail text did not derive from authoritative state");

            MissionGuidanceHUD hud = new(null, null);
            MissionObjectivePanelInfo info = hud.GetActiveMissionPanelInfo(
                ctx.WaypointSystem,
                ctx.Player.Position);
            if (info == null || string.IsNullOrWhiteSpace(info.TitleLine) ||
                string.IsNullOrWhiteSpace(info.ObjectiveLine))
                return Fail("mission guidance HUD did not expose active objective text");

            if (ctx.MissionManager.ActiveMission.OriginStationName != ctx.Origin.Name)
                return Fail("origin station was not available for bartender return guidance");

            return Pass();
        }

        private static MissionSmokeContext CreateContext(string originName = "Pueblo Station", int originSystemIndex = 3)
        {
            MissionSmokeContext ctx = new()
            {
                Credits = new PlayerCredits(10000),
                Player = new Ship(Vector3.Zero)
            };

            ctx.Origin = new Station(new StationConfig
            {
                Description = originName,
                SystemIndex = originSystemIndex,
                StartupPositionX = 0f,
                StartupPositionY = 0f,
                StartupPositionZ = 0f,
                Radius = 1200f,
                DockingRange = 900f,
                FactionId = FactionManager.LibertyCorporations
            }, null);
            ctx.Stations.Add(ctx.Origin);

            FactionManager factionManager = new();
            ctx.ReputationManager = new ReputationManager(factionManager);
            ctx.MissionManager = new MissionManager(ctx.Credits, null, ctx.ReputationManager);
            ctx.WaypointSystem = new MissionWaypointSystem();
            ctx.WorldManager = new MissionWorldManager(
                ctx.MissionManager,
                ctx.WaypointSystem,
                ctx.Player,
                ctx.NpcShips,
                ctx.SpaceObjects,
                () => ctx.Stations,
                npc => ctx.WorldManager.NotifyNpcDestroyed(npc));
            ctx.MissionManager.SetWaypointSystem(ctx.WaypointSystem);
            ctx.MissionManager.SetWorldManager(ctx.WorldManager);
            return ctx;
        }

        private static MissionSmokeContext CreateCourierContext()
        {
            MissionSmokeContext ctx = CreateContext("Newark Station", 1);
            // Priority Dispatch is the representative Friendly Corporation
            // offer; put the regression harness at the exact eligible side
            // of the gate so it continues testing courier transactions.
            ctx.ReputationManager.SetReputation(
                FactionManager.LibertyCorporations,
                ReputationManager.FriendlyThreshold + 0.01f,
                "courier smoke gate");
            ctx.Stations.Add(CreateStation("Buffalo Base", 1, -30000f, -1200f, 36000f));
            return ctx;
        }

        private static Station CreateStation(string name, int systemIndex, float x, float y, float z)
        {
            return new Station(new StationConfig
            {
                Description = name,
                SystemIndex = systemIndex,
                StartupPositionX = x,
                StartupPositionY = y,
                StartupPositionZ = z,
                Radius = 900f,
                DockingRange = 700f,
                FactionId = FactionManager.LibertyCorporations
            }, null);
        }

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

        private static T RunSilenced<T>(Func<T> action)
        {
            TextWriter originalOut = Console.Out;
            using StringWriter sink = new();
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

        private static void TryCleanupDirectory(string directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            {
            }
        }

        private sealed class MissionSmokeContext
        {
            public PlayerCredits Credits { get; set; }
            public ReputationManager ReputationManager { get; set; }
            public Ship Player { get; set; }
            public Station Origin { get; set; }
            public List<NpcShip> NpcShips { get; } = new();
            public List<SpaceObject> SpaceObjects { get; } = new();
            public List<Station> Stations { get; } = new();
            public MissionWaypointSystem WaypointSystem { get; set; }
            public MissionManager MissionManager { get; set; }
            public MissionWorldManager WorldManager { get; set; }
        }
    }
}
