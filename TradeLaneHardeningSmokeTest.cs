using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Phase 47 coverage for shared bidirectional transit, deterministic NPC
    /// traffic, transient disruption/recovery, and Phase 46 flight guards.
    /// </summary>
    internal sealed class TradeLaneHardeningSmokeTest
    {
        private readonly GraphicsDevice _graphicsDevice;

        public TradeLaneHardeningSmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            Check("existing lane definitions still load", ExistingLaneDefinitionsStillLoad, ref passed, ref failed);
            Check("route supports forward direction", ForwardRouteSupports, ref passed, ref failed);
            Check("route supports reverse direction", ReverseRouteSupports, ref passed, ref failed);
            Check("player enters from endpoint A", PlayerEntersFromEndpointA, ref passed, ref failed);
            Check("player enters from endpoint B", PlayerEntersFromEndpointB, ref passed, ref failed);
            Check("forward traversal follows ring order", ForwardTraversalFollowsRingOrder, ref passed, ref failed);
            Check("reverse traversal follows ring order", ReverseTraversalFollowsRingOrder, ref passed, ref failed);
            Check("forward exits at endpoint B", ForwardExitsAtEndpointB, ref passed, ref failed);
            Check("reverse exits at endpoint A", ReverseExitsAtEndpointA, ref passed, ref failed);
            Check("direction is explicit and deterministic", DirectionIsExplicit, ref passed, ref failed);
            Check("player cannot enter the same lane twice", DuplicatePlayerEntryIsRejected, ref passed, ref failed);
            Check("lane entry cancels cruise", LaneEntryCancelsCruise, ref passed, ref failed);
            Check("lane entry cancels afterburner", LaneEntryCancelsAfterburner, ref passed, ref failed);
            Check("cruise cannot activate during transit", CruiseCannotActivateDuringTransit, ref passed, ref failed);
            Check("afterburner cannot stack during transit", AfterburnerCannotStackDuringTransit, ref passed, ref failed);
            Check("lane speed remains bounded", LaneSpeedRemainsBounded, ref passed, ref failed);
            Check("player transit completes", PlayerTransitCompletes, ref passed, ref failed);
            Check("NPC forward transit completes", NpcForwardTransitCompletes, ref passed, ref failed);
            Check("NPC reverse transit completes", NpcReverseTransitCompletes, ref passed, ref failed);
            Check("NPC direction assignment is deterministic", NpcDirectionAssignmentIsDeterministic, ref passed, ref failed);
            Check("both traffic directions coexist", BothTrafficDirectionsCoexist, ref passed, ref failed);
            Check("opposing traffic uses compatible offsets", OpposingTrafficUsesCompatibleOffsets, ref passed, ref failed);
            Check("opposing traffic passes without deadlock", OpposingTrafficPassesWithoutDeadlock, ref passed, ref failed);
            Check("same-direction spacing is bounded", SameDirectionSpacingIsBounded, ref passed, ref failed);
            Check("ring progression does not skip", RingProgressionDoesNotSkip, ref passed, ref failed);
            Check("ring progression does not oscillate", RingProgressionDoesNotOscillate, ref passed, ref failed);
            Check("invalid indices recover safely", InvalidIndicesRecoverSafely, ref passed, ref failed);
            Check("destruction clears lane membership", DestructionClearsLaneMembership, ref passed, ref failed);
            Check("despawn clears lane membership", DespawnClearsLaneMembership, ref passed, ref failed);
            Check("hostile player damage ejects transit", HostilePlayerDamageEjectsTransit, ref passed, ref failed);
            Check("hostile NPC damage ejects transit", HostileNpcDamageEjectsTransit, ref passed, ref failed);
            Check("shield-only damage can eject transit", ShieldOnlyDamageEjectsTransit, ref passed, ref failed);
            Check("lane segment enters disrupted state", LaneSegmentEntersDisruptedState, ref passed, ref failed);
            Check("disruption retains source attribution", DisruptionRetainsSourceAttribution, ref passed, ref failed);
            Check("disrupted segment rejects transit", DisruptedSegmentRejectsTransit, ref passed, ref failed);
            Check("NPC aborts before a disrupted segment", NpcAbortsBeforeDisruptedSegment, ref passed, ref failed);
            Check("disrupted segment recovers on a bounded timer", DisruptedSegmentRecovers, ref passed, ref failed);
            Check("recovered lane accepts traffic", RecoveredLaneAcceptsTraffic, ref passed, ref failed);
            Check("traffic resumes after recovery", TrafficResumesAfterRecovery, ref passed, ref failed);
            Check("unrelated lane remains operational", UnrelatedLaneRemainsOperational, ref passed, ref failed);
            Check("combat near lane remains functional", CombatNearLaneRemainsFunctional, ref passed, ref failed);
            Check("Police/Rogue hostility remains intact", PoliceRogueHostilityRemainsIntact, ref passed, ref failed);
            Check("weapon energy remains independent", WeaponEnergyRemainsIndependent, ref passed, ref failed);
            Check("thruster energy remains independent", ThrusterEnergyRemainsIndependent, ref passed, ref failed);
            Check("cruise remains independent outside lanes", CruiseRemainsIndependentOutsideLanes, ref passed, ref failed);
            Check("transient lane state resets on load", TransientLaneStateResetsOnLoad, ref passed, ref failed);
            Check("reset clears lane state", ResetClearsLaneState, ref passed, ref failed);
            Check("save schema remains current", SaveSchemaRemainsVersionTen, ref passed, ref failed);
            Check("malformed route metadata is safe", MalformedRouteMetadataIsSafe, ref passed, ref failed);
            Check("malformed disruption timer is safe", MalformedDisruptionTimerIsSafe, ref passed, ref failed);
            Check("graphics-backed lane initialization succeeds", GraphicsBackedLaneInitializationSucceeds, ref passed, ref failed);
            Check("bidirectional presentation data is consistent", BidirectionalPresentationDataIsConsistent, ref passed, ref failed);

            Console.WriteLine($"[TRADE LANE HARDENING SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private static void Check(
            string label,
            Func<(bool Success, string FailureReason)> test,
            ref int passed,
            ref int failed)
        {
            try
            {
                (bool success, string failureReason) = test();
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[TRADE LANE HARDENING SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[TRADE LANE HARDENING SMOKE] FAIL {label}: {failureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[TRADE LANE HARDENING SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static (bool Success, string FailureReason) Result(bool success, string reason) =>
            (success, success ? string.Empty : reason);

        private TradeLane CreateLane(string id = "phase47_test_lane", float startZ = 0f, float endZ = 1000f)
        {
            return new TradeLane(_graphicsDevice, new TradelaneConfig
            {
                Id = id,
                Name = id,
                SystemIndex = 1,
                StartPositionZ = startZ,
                EndPositionZ = endZ,
                RingSpacing = 250f,
                RingScale = 1f,
                ActivationRange = 200f,
                DockingRange = 600f,
                TravelSpeed = 1000f,
                RingVerticalOffset = 30f,
                TrafficLateralOffset = 24f,
                DisruptionDamageThreshold = 10f,
                DisruptionRecoverySeconds = 4f
            });
        }

        private static GameTime Time(float seconds) =>
            new GameTime(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(Math.Max(0f, seconds)));

        private static TradelaneManager CreateManager(TradeLane lane, List<NpcShip> npcs = null)
        {
            var manager = new TradelaneManager(null, null);
            manager.SetLanesForTesting(new[] { lane });
            manager.SetNpcShips(npcs ?? new List<NpcShip>());
            return manager;
        }

        private static Ship NewPlayer(TradeLane lane, TradeLaneDirection direction)
        {
            return new Ship(lane.GetEntryRing(direction).Position);
        }

        private static NpcShip NewNpc(TradeLane lane, TradeLaneDirection direction, string name)
        {
            Vector3 position = lane.GetEntryRing(direction).Position;
            return new NpcShip(name, position, position, 100f, 100f, FactionManager.NeutralCivilians);
        }

        private static void ConfigureNpc(NpcShip npc, TradeLane lane)
        {
            npc.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                "phase47-test-route",
                npc.Position,
                100f,
                120f,
                5000f,
                lane.Config.StartPosition,
                lane.Config.EndPosition);
        }

        private bool TryEnterPlayer(
            TradeLaneDirection direction,
            out TradelaneManager manager,
            out TradeLane lane,
            out Ship player)
        {
            lane = CreateLane();
            manager = CreateManager(lane);
            player = NewPlayer(lane, direction);
            manager.Update(Time(0f), player, new KeyboardState());
            return manager.TryEnterTradelaneAt(lane.GetEntryRing(direction), player);
        }

        private bool TryEnterNpc(
            TradeLaneDirection direction,
            out TradelaneManager manager,
            out TradeLane lane,
            out NpcShip npc)
        {
            lane = CreateLane();
            npc = NewNpc(lane, direction, $"phase47-{direction}");
            manager = CreateManager(lane, new List<NpcShip> { npc });
            ConfigureNpc(npc, lane);
            return manager.TryEnterNpcTraffic(npc);
        }

        private (bool Success, string FailureReason) ExistingLaneDefinitionsStillLoad()
        {
            var manager = new TradelaneManager(_graphicsDevice, null);
            manager.LoadAllConfigs();
            manager.LoadTradelanesForSystem(1);
            return Result(manager.GetTradeLanes().Count > 0 && manager.GetTradeLanes().All(lane => lane.Rings.Count >= 2), "no existing lane definition produced a usable ring route");
        }

        private (bool Success, string FailureReason) ForwardRouteSupports() =>
            Result(CreateLane().CanUseRoute(TradeLaneDirection.Forward), "forward route was not usable");

        private (bool Success, string FailureReason) ReverseRouteSupports() =>
            Result(CreateLane().CanUseRoute(TradeLaneDirection.Reverse), "reverse route was not usable");

        private (bool Success, string FailureReason) PlayerEntersFromEndpointA()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out _, out TradeLane lane, out Ship player);
            return Result(entered && player.IsTradeLaneTransit && lane.ActiveDirection == TradeLaneDirection.Forward, "player did not enter the forward endpoint");
        }

        private (bool Success, string FailureReason) PlayerEntersFromEndpointB()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Reverse, out _, out TradeLane lane, out Ship player);
            return Result(entered && player.IsTradeLaneTransit && lane.ActiveDirection == TradeLaneDirection.Reverse, "player did not enter the reverse endpoint");
        }

        private (bool Success, string FailureReason) ForwardTraversalFollowsRingOrder()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out TradelaneManager manager, out TradeLane lane, out Ship player);
            manager.Update(Time(0.30f), player, new KeyboardState());
            bool snapshot = lane.TryGetTransitSnapshot(player, out TradeLaneTransitSnapshot state);
            return Result(entered && snapshot && state.CurrentRingIndex == 1 && state.NextRingIndex == 2, "forward traversal did not advance to the next ring");
        }

        private (bool Success, string FailureReason) ReverseTraversalFollowsRingOrder()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Reverse, out TradelaneManager manager, out TradeLane lane, out Ship player);
            manager.Update(Time(0.30f), player, new KeyboardState());
            bool snapshot = lane.TryGetTransitSnapshot(player, out TradeLaneTransitSnapshot state);
            int last = lane.ReverseRings.Count - 1;
            return Result(entered && snapshot && state.CurrentRingIndex == last - 1 && state.NextRingIndex == last - 2, "reverse traversal did not decrement ring indices");
        }

        private (bool Success, string FailureReason) ForwardExitsAtEndpointB()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out TradelaneManager manager, out TradeLane lane, out Ship player);
            manager.Update(Time(2f), player, new KeyboardState());
            return Result(entered && !player.IsTradeLaneTransit && Vector3.Distance(player.Position, lane.Config.EndPosition) < 60f, "forward exit was not near endpoint B");
        }

        private (bool Success, string FailureReason) ReverseExitsAtEndpointA()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Reverse, out TradelaneManager manager, out TradeLane lane, out Ship player);
            manager.Update(Time(2f), player, new KeyboardState());
            return Result(entered && !player.IsTradeLaneTransit && Vector3.Distance(player.Position, lane.Config.StartPosition) < 60f, "reverse exit was not near endpoint A");
        }

        private (bool Success, string FailureReason) DirectionIsExplicit()
        {
            bool forward = TryEnterPlayer(TradeLaneDirection.Forward, out _, out TradeLane forwardLane, out _);
            bool reverse = TryEnterPlayer(TradeLaneDirection.Reverse, out _, out TradeLane reverseLane, out _);
            return Result(forward && reverse && forwardLane.ActiveDirection == TradeLaneDirection.Forward && reverseLane.ActiveDirection == TradeLaneDirection.Reverse, "active direction was not explicit");
        }

        private (bool Success, string FailureReason) DuplicatePlayerEntryIsRejected()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out TradelaneManager manager, out TradeLane lane, out Ship player);
            bool duplicate = manager.TryEnterTradelaneAt(lane.GetEntryRing(TradeLaneDirection.Forward), player);
            return Result(entered && !duplicate && lane.ActiveTravelerCount == 1, "duplicate player entry changed lane occupancy");
        }

        private (bool Success, string FailureReason) LaneEntryCancelsCruise()
        {
            TradeLane lane = CreateLane();
            TradelaneManager manager = CreateManager(lane);
            Ship player = NewPlayer(lane, TradeLaneDirection.Forward);
            bool charging = player.TryActivateCruise();
            manager.Update(Time(0f), player, new KeyboardState());
            bool entered = manager.TryEnterTradelaneAt(lane.GetEntryRing(TradeLaneDirection.Forward), player);
            return Result(charging && entered && !player.CruiseDrive.IsChargingOrActive, "cruise remained active after lane entry");
        }

        private (bool Success, string FailureReason) LaneEntryCancelsAfterburner()
        {
            TradeLane lane = CreateLane();
            TradelaneManager manager = CreateManager(lane);
            Ship player = NewPlayer(lane, TradeLaneDirection.Forward);
            bool afterburner = player.TryActivateAfterburner();
            manager.Update(Time(0f), player, new KeyboardState());
            bool entered = manager.TryEnterTradelaneAt(lane.GetEntryRing(TradeLaneDirection.Forward), player);
            return Result(afterburner && entered && !player.IsAfterburnerActive, "afterburner remained active after lane entry");
        }

        private (bool Success, string FailureReason) CruiseCannotActivateDuringTransit()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out _, out _, out Ship player);
            return Result(entered && !player.TryActivateCruise(), "cruise activation was accepted during lane transit");
        }

        private (bool Success, string FailureReason) AfterburnerCannotStackDuringTransit()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out _, out _, out Ship player);
            return Result(entered && !player.TryActivateAfterburner() && !player.IsAfterburnerActive, "afterburner stacked with lane transit");
        }

        private (bool Success, string FailureReason) LaneSpeedRemainsBounded()
        {
            TradeLane lane = CreateLane();
            float speed = lane.GetTransitSpeed();
            return Result(speed >= 50f && speed <= 10000f && !float.IsNaN(speed) && !float.IsInfinity(speed), $"lane speed was {speed}");
        }

        private (bool Success, string FailureReason) PlayerTransitCompletes()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out TradelaneManager manager, out TradeLane lane, out Ship player);
            manager.Update(Time(2f), player, new KeyboardState());
            return Result(entered && !player.IsTradeLaneTransit && lane.ActiveTravelerCount == 0, "player transit did not complete exactly once");
        }

        private (bool Success, string FailureReason) NpcForwardTransitCompletes()
        {
            bool entered = TryEnterNpc(TradeLaneDirection.Forward, out TradelaneManager manager, out TradeLane lane, out NpcShip npc);
            manager.Update(Time(2f), null, new KeyboardState());
            return Result(entered && !npc.IsTradeLaneTransit && lane.ActiveTravelerCount == 0 && Vector3.Distance(npc.Position, lane.Config.EndPosition) < 60f, "NPC forward transit did not complete");
        }

        private (bool Success, string FailureReason) NpcReverseTransitCompletes()
        {
            bool entered = TryEnterNpc(TradeLaneDirection.Reverse, out TradelaneManager manager, out TradeLane lane, out NpcShip npc);
            manager.Update(Time(2f), null, new KeyboardState());
            return Result(entered && !npc.IsTradeLaneTransit && lane.ActiveTravelerCount == 0 && Vector3.Distance(npc.Position, lane.Config.StartPosition) < 60f, "NPC reverse transit did not complete");
        }

        private (bool Success, string FailureReason) NpcDirectionAssignmentIsDeterministic()
        {
            bool first = TryEnterNpc(TradeLaneDirection.Reverse, out _, out _, out NpcShip npc1);
            bool second = TryEnterNpc(TradeLaneDirection.Reverse, out _, out _, out NpcShip npc2);
            return Result(first && second && npc1.TradeLaneDirection == TradeLaneDirection.Reverse && npc2.TradeLaneDirection == npc1.TradeLaneDirection, "identical NPC route inputs produced different directions");
        }

        private (bool Success, string FailureReason) BothTrafficDirectionsCoexist()
        {
            TradeLane lane = CreateLane();
            NpcShip reverseNpc = NewNpc(lane, TradeLaneDirection.Reverse, "phase47-reverse");
            TradelaneManager manager = CreateManager(lane, new List<NpcShip> { reverseNpc });
            Ship player = NewPlayer(lane, TradeLaneDirection.Forward);
            manager.Update(Time(0f), player, new KeyboardState());
            bool playerEntered = manager.TryEnterTradelaneAt(lane.GetEntryRing(TradeLaneDirection.Forward), player);
            ConfigureNpc(reverseNpc, lane);
            bool npcEntered = manager.TryEnterNpcTraffic(reverseNpc);
            return Result(playerEntered && npcEntered && lane.ActiveTravelerCount == 2, "opposing player/NPC traffic could not coexist");
        }

        private (bool Success, string FailureReason) OpposingTrafficUsesCompatibleOffsets()
        {
            TradeLane lane = CreateLane();
            NpcShip forward = NewNpc(lane, TradeLaneDirection.Forward, "phase47-forward");
            NpcShip reverse = NewNpc(lane, TradeLaneDirection.Reverse, "phase47-reverse");
            bool started = lane.StartTravelForNpc(forward, lane.GetEntryRing(TradeLaneDirection.Forward)) &&
                lane.StartTravelForNpc(reverse, lane.GetEntryRing(TradeLaneDirection.Reverse));
            lane.TryGetTransitSnapshot(forward, out TradeLaneTransitSnapshot fwd);
            lane.TryGetTransitSnapshot(reverse, out TradeLaneTransitSnapshot rev);
            float separation = Vector3.Dot(fwd.Position - rev.Position, lane.TrafficOffsetAxis);
            return Result(started && separation > 30f && separation < 100f, $"opposing traffic separation was {separation}");
        }

        private (bool Success, string FailureReason) OpposingTrafficPassesWithoutDeadlock()
        {
            TradeLane lane = CreateLane();
            NpcShip forward = NewNpc(lane, TradeLaneDirection.Forward, "phase47-forward");
            NpcShip reverse = NewNpc(lane, TradeLaneDirection.Reverse, "phase47-reverse");
            bool started = lane.StartTravelForNpc(forward, lane.GetEntryRing(TradeLaneDirection.Forward)) &&
                lane.StartTravelForNpc(reverse, lane.GetEntryRing(TradeLaneDirection.Reverse));
            lane.Update(Time(0.5f));
            bool bothActiveDuringPass = lane.ActiveTravelerCount == 2;
            lane.Update(Time(0.7f));
            bool completed = lane.ActiveTravelerCount == 0 && lane.DrainTransitEvents().Count == 2;
            return Result(started && bothActiveDuringPass && completed, "opposing traffic deadlocked or failed to complete");
        }

        private (bool Success, string FailureReason) SameDirectionSpacingIsBounded()
        {
            TradeLane lane = CreateLane();
            NpcShip first = NewNpc(lane, TradeLaneDirection.Forward, "phase47-first");
            NpcShip second = NewNpc(lane, TradeLaneDirection.Forward, "phase47-second");
            ConfigureNpc(first, lane);
            ConfigureNpc(second, lane);
            TradelaneManager manager = CreateManager(lane, new List<NpcShip> { first, second });
            bool firstEntered = manager.TryEnterNpcTraffic(first);
            bool secondRejected = !manager.TryEnterNpcTraffic(second);
            return Result(firstEntered && secondRejected, "same-direction traffic entered without following-distance protection");
        }

        private (bool Success, string FailureReason) RingProgressionDoesNotSkip()
        {
            TradeLane lane = CreateLane();
            NpcShip npc = NewNpc(lane, TradeLaneDirection.Forward, "phase47-progress");
            bool started = lane.StartTravelForNpc(npc, lane.GetEntryRing(TradeLaneDirection.Forward));
            lane.Update(Time(0.30f));
            bool snap = lane.TryGetTransitSnapshot(npc, out TradeLaneTransitSnapshot state);
            return Result(started && snap && state.CurrentRingIndex == 1 && state.NextRingIndex == 2 && state.NextRingIndex - state.CurrentRingIndex == 1, "ring progression skipped or corrupted an index");
        }

        private (bool Success, string FailureReason) RingProgressionDoesNotOscillate()
        {
            TradeLane lane = CreateLane();
            NpcShip npc = NewNpc(lane, TradeLaneDirection.Reverse, "phase47-oscillation");
            bool started = lane.StartTravelForNpc(npc, lane.GetEntryRing(TradeLaneDirection.Reverse));
            lane.Update(Time(0.30f));
            bool first = lane.TryGetTransitSnapshot(npc, out TradeLaneTransitSnapshot a);
            lane.Update(Time(0.10f));
            bool second = lane.TryGetTransitSnapshot(npc, out TradeLaneTransitSnapshot b);
            return Result(started && first && second && b.CurrentRingIndex <= a.CurrentRingIndex && b.NextRingIndex == b.CurrentRingIndex - 1, "reverse progression oscillated or advanced in the wrong direction");
        }

        private (bool Success, string FailureReason) InvalidIndicesRecoverSafely()
        {
            TradeLane lane = CreateLane();
            bool safe = !lane.TryDisruptSegment(-1) && !lane.TryDisruptSegment(999) &&
                !lane.ApplyRingDamage(999, 10f, true) && lane.GetDisruptionState(-1) == TradeLaneDisruptionState.Operational;
            return Result(safe, "invalid ring indices were not rejected safely");
        }

        private (bool Success, string FailureReason) DestructionClearsLaneMembership()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out _, out TradeLane lane, out Ship player);
            player.ApplyCombatDamage(100000f, true);
            return Result(entered && player.Hull.IsDestroyed && !player.IsTradeLaneTransit && lane.ActiveTravelerCount == 0, "destroyed player retained lane membership");
        }

        private (bool Success, string FailureReason) DespawnClearsLaneMembership()
        {
            bool entered = TryEnterNpc(TradeLaneDirection.Forward, out _, out TradeLane lane, out NpcShip npc);
            npc.ApplyDamage(100000f, NpcDestructionSource.Environment);
            return Result(entered && npc.IsDestroyed && !npc.IsTradeLaneTransit && lane.ActiveTravelerCount == 0, "destroyed/despawned NPC retained lane membership");
        }

        private (bool Success, string FailureReason) HostilePlayerDamageEjectsTransit()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out _, out TradeLane lane, out Ship player);
            bool wasAlive = !player.Hull.IsDestroyed;
            player.ApplyCombatDamage(1f, true);
            return Result(entered && wasAlive && !player.Hull.IsDestroyed && !player.IsTradeLaneTransit && lane.ActiveTravelerCount == 0, "hostile player damage did not eject transit");
        }

        private (bool Success, string FailureReason) HostileNpcDamageEjectsTransit()
        {
            bool entered = TryEnterNpc(TradeLaneDirection.Forward, out _, out TradeLane lane, out NpcShip npc);
            npc.ApplyCombatDamage(1f, NpcDestructionSource.Player, true);
            return Result(entered && !npc.IsDestroyed && !npc.IsTradeLaneTransit && lane.ActiveTravelerCount == 0, "hostile NPC damage did not eject transit");
        }

        private (bool Success, string FailureReason) ShieldOnlyDamageEjectsTransit()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out _, out TradeLane lane, out Ship player);
            float hullBefore = player.Hull.CurrentHull;
            player.ApplyCombatDamage(1f, true);
            return Result(entered && player.Hull.CurrentHull >= hullBefore && !player.IsTradeLaneTransit && lane.ActiveTravelerCount == 0, "shield-only hostile damage did not interrupt transit");
        }

        private (bool Success, string FailureReason) LaneSegmentEntersDisruptedState()
        {
            TradeLane lane = CreateLane();
            bool disrupted = lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Player, "smoke-player", 4f);
            return Result(disrupted && lane.GetDisruptionState(1) == TradeLaneDisruptionState.Disrupted && lane.GetActiveDisruptions().Count == 1, "segment did not enter disrupted state");
        }

        private (bool Success, string FailureReason) DisruptionRetainsSourceAttribution()
        {
            TradeLane lane = CreateLane();
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Player, "smoke-player", 4f);
            bool found = lane.TryGetDisruptionInfo(1, out TradeLaneDisruptionInfo info);
            return Result(found && info.Source == TradeLaneDisruptionSource.Player && info.SourceName == "smoke-player" && info.LaneId == lane.LaneId && info.OccurredAtSeconds >= 0d, "disruption metadata did not retain authoritative attribution");
        }

        private (bool Success, string FailureReason) DisruptedSegmentRejectsTransit()
        {
            TradeLane lane = CreateLane();
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Environment, "smoke", 4f);
            NpcShip npc = NewNpc(lane, TradeLaneDirection.Forward, "phase47-disrupted");
            return Result(!lane.CanUseRoute(TradeLaneDirection.Forward) && !lane.StartTravelForNpc(npc, lane.GetEntryRing(TradeLaneDirection.Forward)), "disrupted route accepted new transit");
        }

        private (bool Success, string FailureReason) NpcAbortsBeforeDisruptedSegment()
        {
            bool entered = TryEnterNpc(TradeLaneDirection.Forward, out TradelaneManager manager, out TradeLane lane, out NpcShip npc);
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Player, "smoke", 4f);
            manager.Update(Time(0.1f), null, new KeyboardState());
            return Result(entered && !npc.IsTradeLaneTransit && lane.ActiveTravelerCount == 0, "NPC remained stuck approaching a disrupted segment");
        }

        private (bool Success, string FailureReason) DisruptedSegmentRecovers()
        {
            TradeLane lane = CreateLane();
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Environment, "smoke", 4f);
            lane.Update(Time(3.5f));
            bool recovering = lane.GetDisruptionState(1) == TradeLaneDisruptionState.Recovering;
            lane.Update(Time(0.6f));
            return Result(recovering && lane.GetDisruptionState(1) == TradeLaneDisruptionState.Operational && lane.CanUseRoute(TradeLaneDirection.Forward), "disruption did not recover deterministically");
        }

        private (bool Success, string FailureReason) RecoveredLaneAcceptsTraffic()
        {
            TradeLane lane = CreateLane();
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Environment, "smoke", 4f);
            lane.Update(Time(4.1f));
            NpcShip npc = NewNpc(lane, TradeLaneDirection.Forward, "phase47-recovered");
            return Result(lane.CanUseRoute(TradeLaneDirection.Forward) && lane.StartTravelForNpc(npc, lane.GetEntryRing(TradeLaneDirection.Forward)), "recovered lane rejected new traffic");
        }

        private (bool Success, string FailureReason) TrafficResumesAfterRecovery()
        {
            TradeLane lane = CreateLane();
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Environment, "smoke", 4f);
            lane.Update(Time(4.1f));
            NpcShip npc = NewNpc(lane, TradeLaneDirection.Forward, "phase47-recovered-manager");
            ConfigureNpc(npc, lane);
            TradelaneManager manager = CreateManager(lane, new List<NpcShip> { npc });
            return Result(manager.TryEnterNpcTraffic(npc), "traffic manager did not resume after recovery");
        }

        private (bool Success, string FailureReason) UnrelatedLaneRemainsOperational()
        {
            TradeLane disrupted = CreateLane("phase47_disrupted");
            TradeLane unrelated = CreateLane("phase47_unrelated", 5000f, 6000f);
            disrupted.TryDisruptSegment(1, TradeLaneDisruptionSource.Player, "smoke", 4f);
            return Result(!disrupted.CanUseRoute(TradeLaneDirection.Forward) && unrelated.CanUseRoute(TradeLaneDirection.Forward), "disruption leaked into an unrelated lane");
        }

        private (bool Success, string FailureReason) CombatNearLaneRemainsFunctional()
        {
            TradeLane lane = CreateLane();
            NpcShip police = new NpcShip("phase47-police", lane.Config.StartPosition + Vector3.Right * 200f, lane.Config.StartPosition, 100f, 100f, FactionManager.LibertyPolice);
            NpcShip rogue = new NpcShip("phase47-rogue", lane.Config.StartPosition - Vector3.Right * 200f, lane.Config.StartPosition, 100f, 100f, FactionManager.LibertyRogues);
            bool damage = police.ApplyCombatDamage(1f, NpcDestructionSource.Npc, true) && rogue.ApplyCombatDamage(1f, NpcDestructionSource.Npc, true);
            return Result(damage && !police.IsDestroyed && !rogue.IsDestroyed, "combat damage near lane was not processed");
        }

        private (bool Success, string FailureReason) PoliceRogueHostilityRemainsIntact() =>
            Result(NpcFactionCombatTargeting.IsHostileFactionPair(FactionManager.LibertyPolice, FactionManager.LibertyRogues) &&
                NpcFactionCombatTargeting.IsHostileFactionPair(FactionManager.LibertyRogues, FactionManager.LibertyPolice), "Police/Rogue hostility authorization changed");

        private (bool Success, string FailureReason) WeaponEnergyRemainsIndependent()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out TradelaneManager manager, out _, out Ship player);
            float before = player.WeaponEnergy?.CurrentEnergy ?? -1f;
            manager.Update(Time(1f), player, new KeyboardState());
            float after = player.WeaponEnergy?.CurrentEnergy ?? -2f;
            return Result(entered && before >= 0f && NearlyEqual(before, after), "weapon energy changed during lane transit");
        }

        private (bool Success, string FailureReason) ThrusterEnergyRemainsIndependent()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out TradelaneManager manager, out _, out Ship player);
            float before = player.ThrusterEnergy?.CurrentEnergy ?? -1f;
            manager.Update(Time(0.5f), player, new KeyboardState());
            float after = player.ThrusterEnergy?.CurrentEnergy ?? -2f;
            return Result(entered && before >= 0f && NearlyEqual(before, after), "thruster energy changed during lane transit");
        }

        private (bool Success, string FailureReason) CruiseRemainsIndependentOutsideLanes()
        {
            Ship player = new Ship(Vector3.Zero);
            bool activated = player.TryActivateCruise();
            return Result(activated && player.CruiseDrive.IsCharging && !player.IsTradeLaneTransit, "cruise was not independently available outside a lane");
        }

        private (bool Success, string FailureReason) TransientLaneStateResetsOnLoad()
        {
            bool entered = TryEnterPlayer(TradeLaneDirection.Forward, out TradelaneManager manager, out TradeLane lane, out Ship player);
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Player, "smoke", 4f);
            manager.ResetTransientState();
            return Result(entered && !player.IsTradeLaneTransit && lane.ActiveTravelerCount == 0 && lane.GetDisruptionState(1) == TradeLaneDisruptionState.Operational, "transient lane state survived the load reset");
        }

        private (bool Success, string FailureReason) ResetClearsLaneState()
        {
            bool entered = TryEnterNpc(TradeLaneDirection.Reverse, out TradelaneManager manager, out TradeLane lane, out NpcShip npc);
            lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Npc, "smoke", 4f);
            manager.ResetTransientState();
            return Result(entered && !npc.IsTradeLaneTransit && lane.ActiveTravelerCount == 0 && lane.GetActiveDisruptions().Count == 0, "reset retained NPC occupancy or disruption state");
        }

        private (bool Success, string FailureReason) SaveSchemaRemainsVersionTen() =>
            Result(new SaveGameData().SchemaVersion == SaveGameData.CurrentSchemaVersion, "save schema version changed");

        private (bool Success, string FailureReason) MalformedRouteMetadataIsSafe()
        {
            var config = new TradelaneConfig
            {
                Id = "phase47_malformed",
                Name = "Malformed",
                StartPositionX = float.NaN,
                EndPositionZ = float.PositiveInfinity,
                RingSpacing = 0f,
                TravelSpeed = float.NaN
            };
            TradeLane lane = new TradeLane(_graphicsDevice, config);
            return Result(lane.Rings.Count == 4 && !lane.CanUseRoute(TradeLaneDirection.Forward), "malformed route metadata was accepted unsafely");
        }

        private (bool Success, string FailureReason) MalformedDisruptionTimerIsSafe()
        {
            TradeLane lane = CreateLane();
            bool started = lane.TryDisruptSegment(1, TradeLaneDisruptionSource.Environment, "smoke", float.NaN);
            bool info = lane.TryGetDisruptionInfo(1, out TradeLaneDisruptionInfo disruption);
            return Result(started && info && disruption.RecoveryDurationSeconds > 0f && disruption.RecoveryDurationSeconds <= 300f &&
                !float.IsNaN(disruption.RemainingRecoverySeconds) && !float.IsInfinity(disruption.RemainingRecoverySeconds), "malformed disruption timer was not sanitized");
        }

        private (bool Success, string FailureReason) GraphicsBackedLaneInitializationSucceeds()
        {
            TradeLane lane = CreateLane("phase47_graphics");
            if (_graphicsDevice == null)
                return Result(false, "graphics device was unavailable for the graphics-backed test");
            lane.Draw(Matrix.Identity, Matrix.Identity, Vector3.Down);
            lane.DrawEnergyEffects(Matrix.Identity, Matrix.Identity);
            return Result(lane.Rings.Count >= 2 && lane.ForwardRings.Count == lane.ReverseRings.Count, "graphics-backed lane initialization did not produce paired rings");
        }

        private (bool Success, string FailureReason) BidirectionalPresentationDataIsConsistent()
        {
            TradeLane lane = CreateLane();
            Vector3 forwardOffset = lane.GetDirectionalOffset(TradeLaneDirection.Forward);
            Vector3 reverseOffset = lane.GetDirectionalOffset(TradeLaneDirection.Reverse);
            bool paired = lane.ForwardRings.Count == lane.ReverseRings.Count && lane.Rings.Count == lane.ForwardRings.Count * 2;
            bool opposite = Vector3.Distance(forwardOffset, -reverseOffset) < 0.001f;
            bool stable = lane.GetRingTravelPosition(lane.ForwardRings[1], TradeLaneDirection.Forward) ==
                lane.ForwardRings[1].Position + forwardOffset;
            return Result(paired && opposite && stable, "directional presentation offsets were not stable and paired");
        }

        private static bool NearlyEqual(float left, float right) => Math.Abs(left - right) < 0.01f;
    }
}
