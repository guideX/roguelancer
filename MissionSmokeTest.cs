using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Remote-friendly Phase 11 mission validation. This deliberately tests
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

            if (MissionCatalog.All.Count != 2)
                return Fail($"expected 2 prototype jobs, found {MissionCatalog.All.Count}");

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

            return Pass();
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

        private static MissionSmokeContext CreateContext()
        {
            MissionSmokeContext ctx = new()
            {
                Credits = new PlayerCredits(10000),
                Player = new Ship(Vector3.Zero)
            };

            ctx.Origin = new Station(new StationConfig
            {
                Description = "Pueblo Station",
                SystemIndex = 3,
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
