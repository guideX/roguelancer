using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for first-dock usability and station routing safety.
    /// </summary>
    internal sealed class DockSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateNearestStationResolution, "nearest station resolved", ref passed, ref failed);
            RunCase(ValidateNearestStationSelectionSafety, "nearest station target selected", ref passed, ref failed);
            RunCase(ValidateFirstDockHintText, "fresh-start dock hint text", ref passed, ref failed);
            RunCase(ValidateStationSelectedDockPrompt, "station-selected dock prompt", ref passed, ref failed);
            RunCase(ValidateDockAssistActivePrompt, "dock-assist-active prompt", ref passed, ref failed);
            RunCase(ValidateDockAssistApproach, "dock assist approach", ref passed, ref failed);
            RunCase(ValidateDockRangePrompt, "dock range prompt", ref passed, ref failed);
            RunCase(ValidateNoTargetDockFallback, "no-target dock fallback", ref passed, ref failed);
            RunCase(ValidateStationCyclingSafety, "station cycling safety", ref passed, ref failed);
            RunCase(ValidateRealStationSessionPreservesShip, "real station session preserves ship", ref passed, ref failed);
            RunCase(ValidateStationLaunchClearsDockRange, "station launch clears dock range", ref passed, ref failed);

            Console.WriteLine($"[DOCK SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[DOCK SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[DOCK SMOKE] FAIL {label}: {result.FailureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[DOCK SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateNearestStationResolution()
        {
            DockSmokeContext ctx = CreateContext();
            if (!DockNavigation.TryResolveNearestDockableStation(ctx.Stations, ctx.Player.Position, ctx.ReputationManager, out Station nearestStation, out float distance, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "nearest station could not be resolved"
                    : failureReason);
            }

            if (!ReferenceEquals(nearestStation, ctx.Stations[0]))
            {
                return Fail("nearest station did not resolve to the expected starting station");
            }

            if (distance <= 0f)
            {
                return Fail("nearest station distance was not positive");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateNearestStationSelectionSafety()
        {
            DockSmokeContext ctx = CreateContext();
            if (!DockNavigation.TryResolveNearestDockableStation(ctx.Stations, ctx.Player.Position, ctx.ReputationManager, out Station nearestStation, out _, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "nearest station could not be resolved"
                    : failureReason);
            }

            object selectedTarget = nearestStation;
            if (!NavTargeting.TryBuildHudData(selectedTarget, ctx.Player.Position, ctx.ReputationManager, ctx.FactionManager, null, out NavTargetHudData hud, out string hudFailure))
            {
                return Fail(string.IsNullOrWhiteSpace(hudFailure)
                    ? "selected station HUD data could not be built"
                    : hudFailure);
            }

            if (hud == null ||
                string.IsNullOrWhiteSpace(hud.Name) ||
                string.IsNullOrWhiteSpace(hud.TypeLabel) ||
                string.IsNullOrWhiteSpace(hud.DistanceLabel))
            {
                return Fail("selected station HUD data was incomplete");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateFirstDockHintText()
        {
            if (!DockNavigation.TryBuildFirstDockHintData(out DockOnboardingHudData dockHint, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "fresh-start dock hint could not be built"
                    : failureReason);
            }

            if (dockHint == null ||
                string.IsNullOrWhiteSpace(dockHint.HeaderLabel) ||
                string.IsNullOrWhiteSpace(dockHint.PrimaryLine) ||
                string.IsNullOrWhiteSpace(dockHint.SecondaryLine))
            {
                return Fail("fresh-start dock hint text was incomplete");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateStationSelectedDockPrompt()
        {
            DockSmokeContext ctx = CreateContext();
            Station station = ctx.Stations[0];
            Vector3 outsidePosition = station.Position + new Vector3(station.DockingRange + 400f, 0f, 0f);

            if (!DockNavigation.TryBuildDockAssistHudData(station, outsidePosition, dockAssistActive: false, out DockAssistHudData dockHud, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "station-selected dock HUD could not be built"
                    : failureReason);
            }

            if (dockHud == null ||
                string.IsNullOrWhiteSpace(dockHud.StationLabel) ||
                string.IsNullOrWhiteSpace(dockHud.RangeDeltaLabel) ||
                string.IsNullOrWhiteSpace(dockHud.GuidanceLabel))
            {
                return Fail("station-selected dock prompt text was incomplete");
            }

            if (!dockHud.StationLabel.StartsWith("Press F3: Approach/Dock", StringComparison.OrdinalIgnoreCase) ||
                !dockHud.RangeDeltaLabel.StartsWith("Distance to dock range:", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("station-selected dock prompt did not use the expected wording");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDockAssistActivePrompt()
        {
            DockSmokeContext ctx = CreateContext();
            Station station = ctx.Stations[0];
            Vector3 outsidePosition = station.Position + new Vector3(station.DockingRange + 400f, 0f, 0f);

            if (!DockNavigation.TryBuildDockAssistHudData(station, outsidePosition, dockAssistActive: true, out DockAssistHudData dockHud, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "dock-assist-active HUD could not be built"
                    : failureReason);
            }

            if (dockHud == null ||
                string.IsNullOrWhiteSpace(dockHud.StationLabel) ||
                string.IsNullOrWhiteSpace(dockHud.RangeDeltaLabel) ||
                string.IsNullOrWhiteSpace(dockHud.GuidanceLabel))
            {
                return Fail("dock-assist-active prompt text was incomplete");
            }

            if (!dockHud.StationLabel.StartsWith("Dock Assist: Approaching", StringComparison.OrdinalIgnoreCase) ||
                !dockHud.RangeDeltaLabel.StartsWith("Dock range in", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("dock-assist-active prompt did not use the expected wording");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDockAssistApproach()
        {
            DockSmokeContext ctx = CreateContext();
            if (!DockNavigation.TryResolveNearestDockableStation(ctx.Stations, ctx.Player.Position, ctx.ReputationManager, out Station nearestStation, out _, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "nearest station could not be resolved"
                    : failureReason);
            }

            ctx.Player.ActivateDockAssist(nearestStation);

            if (!ctx.Player.IsDockAssistActive || !ReferenceEquals(ctx.Player.CurrentDockAssistTarget, nearestStation))
            {
                return Fail("dock assist did not keep the selected station active");
            }

            if (!ctx.Player.IsGotoActive || ctx.Player.GotoAutopilot == null || !ctx.Player.GotoAutopilot.IsDockAssistMode)
            {
                return Fail("dock assist did not activate the direct station route");
            }

            if (!string.Equals(ctx.Player.GotoAutopilot.ModeLabel, "DOCK ASSIST", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("dock assist route did not report the expected mode label");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDockRangePrompt()
        {
            DockSmokeContext ctx = CreateContext();
            Station station = ctx.Stations[0];
            Vector3 insidePosition = station.Position + new Vector3(Math.Min(100f, station.DockingRange * 0.25f), 0f, 0f);

            if (!DockNavigation.TryBuildDockAssistHudData(station, insidePosition, out DockAssistHudData dockHud, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "dock assist HUD data could not be built"
                    : failureReason);
            }

            if (!dockHud.InRange ||
                !string.Equals(dockHud.GuidanceLabel, "Press F3 to dock", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(dockHud.RangeDeltaLabel, "Within dock range", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(dockHud.DistanceLabel) ||
                string.IsNullOrWhiteSpace(dockHud.DockRangeLabel))
            {
                return Fail("dock assist HUD did not report the in-range prompt");
            }

            if (!NavTargeting.TryBuildHudData(station, insidePosition, ctx.ReputationManager, ctx.FactionManager, null, out NavTargetHudData navHud, out string navFailure))
            {
                return Fail(string.IsNullOrWhiteSpace(navFailure)
                    ? "station HUD data could not be built"
                    : navFailure);
            }

            if (navHud == null || string.IsNullOrWhiteSpace(navHud.Name) || string.IsNullOrWhiteSpace(navHud.DistanceLabel))
            {
                return Fail("station HUD data was incomplete");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateNoTargetDockFallback()
        {
            DockSmokeContext ctx = CreateContext();
            if (!DockNavigation.TryResolveNearestDockableStation(ctx.Stations, ctx.Player.Position, ctx.ReputationManager, out Station nearestStation, out _, out string failureReason))
            {
                return Fail(string.IsNullOrWhiteSpace(failureReason)
                    ? "nearest station could not be resolved"
                    : failureReason);
            }

            ctx.Player.ActivateDockAssist(nearestStation);

            if (!ctx.Player.IsDockAssistActive ||
                !ctx.Player.IsGotoActive ||
                !ReferenceEquals(ctx.Player.CurrentDockAssistTarget, nearestStation))
            {
                return Fail("dock fallback did not activate the resolved nearest station");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateStationCyclingSafety()
        {
            DockSmokeContext ctx = CreateContext();
            List<Station> orderedStations = DockNavigation.GetStationsSortedByDistance(ctx.Stations, ctx.Player.Position);
            if (orderedStations.Count < 2)
            {
                return Fail("expected at least two stations for cycling");
            }

            float lastDistance = -1f;
            for (int i = 0; i < orderedStations.Count; i++)
            {
                float currentDistance = Vector3.Distance(ctx.Player.Position, orderedStations[i].Position);
                if (currentDistance < lastDistance)
                {
                    return Fail("station cycle order was not distance-sorted");
                }

                lastDistance = currentDistance;
            }

            Station current = orderedStations[0];
            Station next = orderedStations[(orderedStations.IndexOf(current) + 1) % orderedStations.Count];
            if (next == null)
            {
                return Fail("station cycling selected a null station");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateRealStationSessionPreservesShip()
        {
            DockSmokeContext ctx = CreateContext();
            Station station = ctx.Stations[0];
            Ship player = ctx.Player;
            player.DisplayName = "Smoke Test Ship";
            player.ModelPath = "SHIPS/test/ship";
            float hullBefore = player.Hull.CurrentHull;
            ShipLoadout loadoutBefore = player.Loadout;
            Vector3 positionBefore = player.Position;

            StationSession session = StationSession.CreateRealDocked(station, player, 1);
            if (!ReferenceEquals(session.PlayerShip, player) ||
                !ReferenceEquals(player.Loadout, loadoutBefore) ||
                !string.Equals(player.DisplayName, "Smoke Test Ship", StringComparison.Ordinal) ||
                !string.Equals(player.ModelPath, "SHIPS/test/ship", StringComparison.Ordinal) ||
                player.Hull.CurrentHull != hullBefore)
            {
                return Fail("station session changed authoritative ship identity or state");
            }

            if (player.Position != positionBefore || session.DockingSpacePosition != positionBefore)
            {
                return Fail("station session mutated the authoritative space position on entry");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateStationLaunchClearsDockRange()
        {
            DockSmokeContext ctx = CreateContext();
            Station station = ctx.Stations[0];
            StationSession session = StationSession.CreateRealDocked(station, ctx.Player, 1);
            ctx.Player.RestoreFlightState(session.LaunchPosition, session.LaunchForward);

            if (Vector3.Distance(ctx.Player.Position, station.Position) <= station.DockingRange)
            {
                return Fail("launch position remained inside station docking range");
            }

            if (ctx.Player.EnginesKilled || ctx.Player.Velocity.LengthSquared() > 0.001f)
            {
                return Fail("flight controls were not restored cleanly after launch");
            }

            return Pass();
        }

        private DockSmokeContext CreateContext()
        {
            var ctx = new DockSmokeContext
            {
                Credits = new PlayerCredits(0),
                FactionManager = new FactionManager(),
                Player = new Ship(new Vector3(500f, 200f, -500f))
            };

            ctx.ReputationManager = new ReputationManager(ctx.FactionManager);
            ctx.ReputationManager.SetReputation(FactionManager.LibertyPolice, 0.55f, "dock smoke setup");
            ctx.ReputationManager.SetReputation(FactionManager.LibertyCorporations, 0.45f, "dock smoke setup");
            ctx.ReputationManager.SetReputation(FactionManager.LibertyRogues, -0.75f, "dock smoke setup");

            ctx.Stations.AddRange(CreateStations());
            ctx.Player.SetGotoAutopilot(new GotoAutopilot());

            return ctx;
        }

        private static List<Station> CreateStations()
        {
            return new List<Station>
            {
                new Station(new Roguelancer.Configuration.StationConfig
                {
                    Description = "Fort Bush",
                    StartupPositionX = 6000f,
                    StartupPositionY = 600f,
                    StartupPositionZ = -4500f,
                    Radius = 1000f,
                    DockingRange = 900f,
                    FactionId = FactionManager.LibertyPolice
                }, null),
                new Station(new Roguelancer.Configuration.StationConfig
                {
                    Description = "Newark Station",
                    StartupPositionX = 20000f,
                    StartupPositionY = 0f,
                    StartupPositionZ = -12000f,
                    Radius = 1200f,
                    DockingRange = 900f,
                    FactionId = FactionManager.LibertyPolice
                }, null),
                new Station(new Roguelancer.Configuration.StationConfig
                {
                    Description = "Rochester Base",
                    StartupPositionX = 28000f,
                    StartupPositionY = 500f,
                    StartupPositionZ = -6000f,
                    Radius = 1200f,
                    DockingRange = 900f,
                    FactionId = FactionManager.LibertyPolice
                }, null)
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

        private sealed class DockSmokeContext
        {
            public PlayerCredits Credits { get; set; }
            public FactionManager FactionManager { get; set; }
            public ReputationManager ReputationManager { get; set; }
            public Ship Player { get; set; }
            public List<Station> Stations { get; } = new();
        }
    }
}
