using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for navigation, targeting, and mission objective usability.
    /// </summary>
    internal sealed class NavSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateBountyTargetSelection, "bounty target selection", ref passed, ref failed);
            RunCase(ValidateDeliveryDestinationSelection, "delivery destination selection", ref passed, ref failed);
            RunCase(ValidateEscortObjectiveSelection, "escort objective selection", ref passed, ref failed);
            RunCase(ValidateUnresolvedObjectiveSafety, "unresolved objective safety", ref passed, ref failed);
            RunCase(ValidateTargetHudData, "target HUD data", ref passed, ref failed);
            RunCase(ValidateObjectivePanelText, "objective panel text", ref passed, ref failed);
            RunCase(ValidateMissionGotoSafety, "mission GOTO safety", ref passed, ref failed);

            Console.WriteLine($"[NAV SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[NAV SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[NAV SMOKE] FAIL {label}: {result.FailureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[NAV SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateBountyTargetSelection()
        {
            NavSmokeContext ctx = CreateContext();
            Mission mission = CreateBountyMission("Smoke Pirate Ace", "Last seen near Manhattan", 2_000);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("bounty mission was not accepted");
            }

            if (!NavTargeting.TryResolveMissionObjective(mission, ctx.SpaceObjects, ctx.NpcShips, out SpaceObject resolvedTarget, out string reason) || resolvedTarget == null)
            {
                return Fail(string.IsNullOrWhiteSpace(reason)
                    ? "bounty target could not be resolved"
                    : reason);
            }

            if (resolvedTarget is not NpcShip npcTarget)
            {
                return Fail("bounty target did not resolve to an NPC ship");
            }

            if (!ReferenceEquals(mission.TargetSpaceObject, npcTarget))
            {
                return Fail("bounty mission target was not bound to the resolved NPC");
            }

            if (!ctx.NpcShips.Contains(npcTarget))
            {
                return Fail("resolved bounty target was not present in the active NPC list");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDeliveryDestinationSelection()
        {
            NavSmokeContext ctx = CreateContext();
            Mission mission = CreateDeliveryMission("Medical Supplies", "Newark Station", 1_250);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("delivery mission was not accepted");
            }

            if (!NavTargeting.TryResolveMissionObjective(mission, ctx.SpaceObjects, ctx.NpcShips, out SpaceObject resolvedTarget, out string reason) || resolvedTarget == null)
            {
                return Fail(string.IsNullOrWhiteSpace(reason)
                    ? "delivery destination could not be resolved"
                    : reason);
            }

            if (resolvedTarget is not Station destination)
            {
                return Fail("delivery destination did not resolve to a station");
            }

            if (!ReferenceEquals(mission.TargetSpaceObject, destination))
            {
                return Fail("delivery mission target was not bound to the resolved station");
            }

            if (!ctx.Stations.Contains(destination))
            {
                return Fail("resolved delivery destination was not present in the active station list");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateEscortObjectiveSelection()
        {
            NavSmokeContext ctx = CreateContext();
            Mission mission = CreateEscortMission("Smoke Escort", "Newark Station", 1_450);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("escort mission was not accepted");
            }

            if (!NavTargeting.TryResolveMissionObjective(mission, ctx.SpaceObjects, ctx.NpcShips, out SpaceObject resolvedTarget, out string reason) || resolvedTarget == null)
            {
                return Fail(string.IsNullOrWhiteSpace(reason)
                    ? "escort objective could not be resolved"
                    : reason);
            }

            if (resolvedTarget is not NpcShip && resolvedTarget is not Station)
            {
                return Fail("escort objective resolved to an unsupported object");
            }

            if (resolvedTarget is NpcShip npcTarget &&
                !ReferenceEquals(mission.TargetSpaceObject, npcTarget))
            {
                return Fail("escort mission target was not bound to the resolved NPC");
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
                return Fail("escort goto request did not select the resolved escort objective");
            }

            ctx.Player.CancelGoto();
            return Pass();
        }

        private (bool Success, string FailureReason) ValidateUnresolvedObjectiveSafety()
        {
            Mission unresolved = new Mission(
                MissionType.Bounty,
                MissionDifficulty.Medium,
                string.Empty,
                string.Empty,
                1_000,
                0f,
                string.Empty,
                FactionManager.BountyHunters);
            unresolved.Status = MissionStatus.Active;

            MissionWaypointSystem waypointSystem = new MissionWaypointSystem();
            waypointSystem.RegisterMission(unresolved);

            MissionGuidanceHUD hud = new MissionGuidanceHUD(null, null);
            MissionObjectivePanelInfo panelInfo = hud.GetActiveMissionPanelInfo(waypointSystem);
            if (panelInfo == null)
            {
                return Fail("objective panel info was null for unresolved mission");
            }

            if (panelInfo.IsResolved)
            {
                return Fail("unresolved mission incorrectly reported as resolved");
            }

            if (string.IsNullOrWhiteSpace(panelInfo.StatusLine) ||
                string.IsNullOrWhiteSpace(panelInfo.DistanceLine) ||
                string.IsNullOrWhiteSpace(panelInfo.ObjectiveLine))
            {
                return Fail("objective panel text was empty for unresolved mission");
            }

            if (panelInfo.StatusLine.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                panelInfo.DistanceLine.Contains("null", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("objective panel text was not sanitized");
            }

            if (!NavTargeting.TryResolveMissionObjective(unresolved, Array.Empty<SpaceObject>(), Array.Empty<NpcShip>(), out _, out string reason) &&
                string.IsNullOrWhiteSpace(reason))
            {
                return Fail("unresolved mission did not return a safe failure reason");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateTargetHudData()
        {
            NavSmokeContext ctx = CreateContext();

            Station station = ctx.Stations.First();
            if (!NavTargeting.TryBuildHudData(station, ctx.Player.Position, ctx.ReputationManager, ctx.FactionManager, null, out NavTargetHudData stationHud, out string stationFailure))
            {
                return Fail($"station HUD data failed: {stationFailure}");
            }

            if (string.IsNullOrWhiteSpace(stationHud.Name) ||
                string.IsNullOrWhiteSpace(stationHud.TypeLabel) ||
                string.IsNullOrWhiteSpace(stationHud.FactionLabel) ||
                string.IsNullOrWhiteSpace(stationHud.DistanceLabel))
            {
                return Fail("station HUD data was incomplete");
            }

            NpcShip hostileNpc = CreateHostileNpc("Pirate Smoke", ctx.Player.Position + ctx.Player.Forward * 2500f);
            if (!NavTargeting.TryBuildHudData(hostileNpc, ctx.Player.Position, ctx.ReputationManager, ctx.FactionManager, null, out NavTargetHudData npcHud, out string npcFailure))
            {
                return Fail($"hostile NPC HUD data failed: {npcFailure}");
            }

            if (string.IsNullOrWhiteSpace(npcHud.IntegrityLabel) ||
                string.IsNullOrWhiteSpace(npcHud.StandingLabel) ||
                string.IsNullOrWhiteSpace(npcHud.TypeLabel))
            {
                return Fail("hostile NPC HUD data was incomplete");
            }

            if (CargoPod.TryCreate("medical-supplies", 3, ctx.Player.Position + ctx.Player.Forward * 900f, Vector3.Zero, 120f, 40f, out CargoPod cargoPod))
            {
                ctx.CargoPods.Add(cargoPod);
                if (!NavTargeting.TryBuildHudData(cargoPod, ctx.Player.Position, ctx.ReputationManager, ctx.FactionManager, null, out NavTargetHudData cargoHud, out string cargoFailure))
                {
                    return Fail($"cargo pod HUD data failed: {cargoFailure}");
                }

                if (string.IsNullOrWhiteSpace(cargoHud.TypeLabel) ||
                    string.IsNullOrWhiteSpace(cargoHud.StatusLabel) ||
                    string.IsNullOrWhiteSpace(cargoHud.DistanceLabel))
                {
                    return Fail("cargo pod HUD data was incomplete");
                }
            }
            else
            {
                return Fail("failed to create cargo pod for HUD data test");
            }

            Mission mission = CreateDeliveryMission("Construction Materials", station.Name, 1_500);
            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("mission target HUD test could not accept delivery mission");
            }

            if (!NavTargeting.TryBuildHudData(mission.TargetSpaceObject, ctx.Player.Position, ctx.ReputationManager, ctx.FactionManager, mission, out NavTargetHudData missionHud, out string missionFailure))
            {
                return Fail($"mission target HUD data failed: {missionFailure}");
            }

            if (string.IsNullOrWhiteSpace(missionHud.MissionLabel) ||
                string.IsNullOrWhiteSpace(missionHud.TypeLabel) ||
                string.IsNullOrWhiteSpace(missionHud.DistanceLabel))
            {
                return Fail("mission target HUD data was incomplete");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateObjectivePanelText()
        {
            NavSmokeContext ctx = CreateContext();
            Mission mission = CreateBountyMission("Smoke Enforcer", "Last seen near Newark Station", 1_900);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("objective panel mission was not accepted");
            }

            MissionObjectivePanelInfo info = ctx.MissionHUD.GetActiveMissionPanelInfo(ctx.WaypointSystem, ctx.Player.Position);
            if (info == null)
            {
                return Fail("objective panel info was null");
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
                return Fail("objective panel text was empty");
            }

            if (combined.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("NaN", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("Infinity", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("objective panel text was not safe");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateMissionGotoSafety()
        {
            NavSmokeContext ctx = CreateContext();
            Mission mission = CreateDeliveryMission("Engine Components", "Newark Station", 1_700);

            if (!ctx.MissionManager.AcceptMission(mission))
            {
                return Fail("goto safety mission was not accepted");
            }

            if (!NavTargeting.TryStartGotoToMissionObjective(ctx.Player, mission, ctx.SpaceObjects, ctx.NpcShips, out string gotoReason))
            {
                return Fail(string.IsNullOrWhiteSpace(gotoReason)
                    ? "goto request was rejected without a reason"
                    : gotoReason);
            }

            if (!ctx.Player.IsGotoActive || ctx.Player.CurrentGotoTarget == null)
            {
                return Fail("goto request did not activate the ship");
            }

            if (!ReferenceEquals(ctx.Player.CurrentGotoTarget, mission.TargetSpaceObject))
            {
                return Fail("goto request did not select the mission destination");
            }

            ctx.Player.CancelGoto();
            return Pass();
        }

        private NavSmokeContext CreateContext()
        {
            var ctx = new NavSmokeContext
            {
                Credits = new PlayerCredits(0),
                FactionManager = new FactionManager(),
                Player = new Ship(Vector3.Zero)
            };

            ctx.ReputationManager = new ReputationManager(ctx.FactionManager);

            ctx.ReputationManager.SetReputation(FactionManager.LibertyPolice, 0.55f, "nav smoke setup");
            ctx.ReputationManager.SetReputation(FactionManager.LibertyCorporations, 0.45f, "nav smoke setup");
            ctx.ReputationManager.SetReputation(FactionManager.LibertyRogues, -0.75f, "nav smoke setup");

            ctx.Stations.AddRange(CreateStations());
            ctx.SpaceObjects.AddRange(ctx.Stations);

            ctx.NpcShips.Add(CreateHostileNpc("Pirate Smoke Wing", ctx.Player.Position + ctx.Player.Forward * 4500f));
            ctx.SpaceObjects.AddRange(ctx.NpcShips);

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

            return ctx;
        }

        private static List<Station> CreateStations()
        {
            return new List<Station>
            {
                new Station(new Roguelancer.Configuration.StationConfig
                {
                    Description = "Fort Bush",
                    StartupPositionX = 0f,
                    StartupPositionY = 0f,
                    StartupPositionZ = 0f,
                    Radius = 1200f,
                    DockingRange = 900f,
                    FactionId = FactionManager.LibertyPolice
                }, null),
                new Station(new Roguelancer.Configuration.StationConfig
                {
                    Description = "Newark Station",
                    StartupPositionX = 6000f,
                    StartupPositionY = 0f,
                    StartupPositionZ = 1200f,
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

        private NpcShip CreateHostileNpc(string name, Vector3 position)
        {
            NpcShip npc = new NpcShip(name, position, position, 1f, 0f, FactionManager.LibertyRogues);
            npc.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.PirateAmbush,
                $"nav-smoke-{name.Replace(" ", "-", StringComparison.OrdinalIgnoreCase)}",
                position,
                1200f,
                180f,
                10000f);
            return npc;
        }

        private static (bool Success, string FailureReason) Pass()
        {
            return (true, string.Empty);
        }

        private static (bool Success, string FailureReason) Fail(string reason)
        {
            return (false, reason);
        }

        private static TResult RunSilenced<TResult>(Func<TResult> func)
        {
            var originalOut = Console.Out;
            try
            {
                using var writer = new System.IO.StringWriter();
                Console.SetOut(writer);
                return func();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        private sealed class NavSmokeContext
        {
            public PlayerCredits Credits { get; set; }
            public FactionManager FactionManager { get; set; }
            public ReputationManager ReputationManager { get; set; }
            public Ship Player { get; set; }
            public MissionManager MissionManager { get; set; }
            public MissionWaypointSystem WaypointSystem { get; set; }
            public MissionWorldManager WorldManager { get; set; }
            public MissionGuidanceHUD MissionHUD { get; set; }
            public List<Station> Stations { get; } = new();
            public List<SpaceObject> SpaceObjects { get; } = new();
            public List<NpcShip> NpcShips { get; } = new();
            public List<CargoPod> CargoPods { get; } = new();
        }
    }
}
