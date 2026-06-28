using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for mounted missile launchers and missile combat flow.
    /// </summary>
    internal sealed class MissileSmokeTest
    {
        private const float FrameDeltaSeconds = 1f / 60f;
        private const int DumbfireFrameBudget = 360;
        private const int LockHitFrameBudget = 360;
        private const float LaunchOffset = 22f;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly FieldInfo _missilesField;

        public MissileSmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _missilesField = typeof(MissileSystem).GetField("_missiles", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access MissileSystem projectile state.");
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateStarterLauncherMounted, "starter launcher mounted", ref passed, ref failed);
            RunCase(ValidateNoLauncherSafety, "no-launcher safety", ref passed, ref failed);
            RunCase(ValidateDumbfireTimeout, "dumbfire launch/timeout", ref passed, ref failed);
            RunCase(ValidateLockHitAndDamage, "lock/hit NPC", ref passed, ref failed);

            Console.WriteLine($"[MISSILE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[MISSILE SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[MISSILE SMOKE] FAIL {label}: {result.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[MISSILE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateStarterLauncherMounted()
        {
            Ship ship = CreateTestShip();
            EquipmentDefinition launcher = ship.GetPrimaryMountedMissileLauncher();

            if (launcher == null)
            {
                return Fail("starter loadout did not mount a missile launcher");
            }

            if (!string.Equals(launcher.Id, "basic_missile_launcher", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"expected Basic Missile Launcher, got {launcher.Name ?? launcher.Id}");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateNoLauncherSafety()
        {
            Ship ship = CreateTestShip();
            RunSilenced(() => ship.SetLoadout(new ShipLoadout()));

            EquipmentDefinition launcher = ship.GetPrimaryMountedMissileLauncher();
            if (launcher != null)
            {
                return Fail("isolated loadout still reported a missile launcher");
            }

            MissileSystem missileSystem = CreateMissileSystem();
            string launchMessage = string.Empty;
            bool fired = RunSilenced(() => missileSystem.TryFire(ship, launcher, null, GetLaunchOrigin(ship), out launchMessage));

            if (fired)
            {
                return Fail("missile launch unexpectedly succeeded without a launcher");
            }

            if (!string.Equals(launchMessage, "No missile launcher mounted.", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"unexpected no-launcher failure message: {launchMessage}");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDumbfireTimeout()
        {
            Ship ship = CreateTestShip();
            MissileSystem missileSystem = CreateMissileSystem();
            EquipmentDefinition launcher = ship.GetPrimaryMountedMissileLauncher();
            Vector3 launchOrigin = GetLaunchOrigin(ship);

            string launchMessage = string.Empty;
            bool fired = RunSilenced(() => missileSystem.TryFire(ship, launcher, null, launchOrigin, out launchMessage));
            if (!fired)
            {
                return Fail(string.IsNullOrWhiteSpace(launchMessage)
                    ? "dumbfire launch was rejected"
                    : launchMessage);
            }

            if (!launchMessage.Contains("Dumbfire", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"dumbfire launch message was not tagged correctly: {launchMessage}");
            }

            if (!TryGetFirstMissileState(missileSystem, out Vector3 initialPosition, out float initialLife, out float initialMaxLife))
            {
                return Fail("missile did not spawn for dumbfire validation");
            }

            if (initialLife != 0f || initialMaxLife <= 0f)
            {
                return Fail("spawned missile had invalid initial lifetime state");
            }

            for (int i = 0; i < 30; i++)
            {
                RunSilenced(() => missileSystem.Update(CreateGameTime(FrameDeltaSeconds), Array.Empty<NpcShip>()));
            }

            if (!TryGetFirstMissileState(missileSystem, out Vector3 midPosition, out _, out _))
            {
                return Fail("dumbfire missile disappeared before its lifetime elapsed");
            }

            Vector3 forwardDelta = midPosition - initialPosition;
            if (Vector3.Dot(forwardDelta, ship.Forward) <= 0f)
            {
                return Fail("dumbfire missile did not move forward");
            }

            if (forwardDelta.LengthSquared() < 1f)
            {
                return Fail("dumbfire missile moved an unexpectedly small distance");
            }

            for (int i = 30; i < DumbfireFrameBudget; i++)
            {
                RunSilenced(() => missileSystem.Update(CreateGameTime(FrameDeltaSeconds), Array.Empty<NpcShip>()));
            }

            if (TryGetFirstMissileState(missileSystem, out _, out _, out _))
            {
                return Fail("dumbfire missile was still active after its lifetime budget");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateLockHitAndDamage()
        {
            Ship ship = CreateTestShip();
            MissileSystem missileSystem = CreateMissileSystem();
            EquipmentDefinition launcher = ship.GetPrimaryMountedMissileLauncher();

            NpcShip target = CreateTestNpc("Missile Smoke Target", GetTargetPosition(ship, 280f));
            float shieldsBefore = target.Shields.CurrentShields;
            float hullBefore = target.Hull.CurrentHull;

            string launchMessage = string.Empty;
            bool fired = RunSilenced(() => missileSystem.TryFire(ship, launcher, target, GetLaunchOrigin(ship), out launchMessage));
            if (!fired)
            {
                return Fail(string.IsNullOrWhiteSpace(launchMessage)
                    ? "lock-on launch was rejected"
                    : launchMessage);
            }

            if (!launchMessage.Contains("Locked", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"lock-on launch message was not tagged correctly: {launchMessage}");
            }

            bool hitOccurred = false;
            for (int i = 0; i < LockHitFrameBudget; i++)
            {
                List<HitInfo> hits = RunSilenced(() => missileSystem.Update(CreateGameTime(FrameDeltaSeconds), new[] { target }));
                if (hits.Count > 0)
                {
                    hitOccurred = true;
                    break;
                }

                if (!TryGetFirstMissileState(missileSystem, out _, out _, out _))
                {
                    break;
                }
            }

            if (!hitOccurred)
            {
                return Fail("lock-on missile did not hit the NPC before timing out");
            }

            if (target.Shields.CurrentShields >= shieldsBefore && target.Hull.CurrentHull >= hullBefore)
            {
                return Fail("missile hit did not reduce NPC shields or hull");
            }

            if (target.Hull.CurrentHull >= hullBefore)
            {
                return Fail("missile hit did not reduce NPC hull after shields absorbed damage");
            }

            if (!ValidateDestructionNotificationPath(ship, launcher))
            {
                return Fail("destroyed-target mission notification path did not fire safely");
            }

            return Pass();
        }

        private bool ValidateDestructionNotificationPath(Ship ship, EquipmentDefinition launcher)
        {
            NpcShip destroyTarget = CreateTestNpc("Missile Smoke Bounty", GetTargetPosition(ship, 320f));
            RunSilenced(() =>
            {
                destroyTarget.Shields.AbsorbDamage(destroyTarget.Shields.CurrentShields);
                destroyTarget.Hull.TakeDamage(Math.Max(0f, destroyTarget.Hull.CurrentHull - 1f));
            });

            var missionManager = new MissionManager(new PlayerCredits(0), null, null);
            var mission = new Mission(
                MissionType.Bounty,
                MissionDifficulty.Easy,
                destroyTarget.Name,
                "Smoke Target",
                1000,
                0f,
                "Smoke bounty");

            RunSilenced(() => missionManager.AcceptMission(mission));

            bool destroyedEventRaised = false;
            RunSilenced(() =>
            {
                destroyTarget.OnDestroyed += destroyedShip =>
                {
                    destroyedEventRaised = true;
                    missionManager.NotifyTargetDestroyed(destroyedShip.Name);
                };
            });

            MissileSystem missileSystem = CreateMissileSystem();
            string launchMessage = string.Empty;
            bool fired = RunSilenced(() => missileSystem.TryFire(ship, launcher, destroyTarget, GetLaunchOrigin(ship), out launchMessage));
            if (!fired)
            {
                return false;
            }

            for (int i = 0; i < LockHitFrameBudget; i++)
            {
                List<HitInfo> hits = RunSilenced(() => missileSystem.Update(CreateGameTime(FrameDeltaSeconds), new[] { destroyTarget }));
                if (hits.Count > 0)
                {
                    break;
                }

                if (!TryGetFirstMissileState(missileSystem, out _, out _, out _))
                {
                    break;
                }
            }

            return destroyedEventRaised && destroyTarget.IsDestroyed && mission.ObjectiveComplete;
        }

        private Ship CreateTestShip()
        {
            return RunSilenced(() => new Ship(Vector3.Zero));
        }

        private MissileSystem CreateMissileSystem()
        {
            return RunSilenced(() => new MissileSystem(_graphicsDevice));
        }

        private NpcShip CreateTestNpc(string name, Vector3 position)
        {
            return RunSilenced(() => new NpcShip(name, position, position, 1f, 0f, "pirate"));
        }

        private Vector3 GetLaunchOrigin(Ship ship)
        {
            return ship.Position + ship.Forward * LaunchOffset;
        }

        private Vector3 GetTargetPosition(Ship ship, float distance)
        {
            return ship.Position + ship.Forward * distance;
        }

        private bool TryGetFirstMissileState(MissileSystem missileSystem, out Vector3 position, out float life, out float maxLife)
        {
            position = Vector3.Zero;
            life = 0f;
            maxLife = 0f;

            if (missileSystem == null)
            {
                return false;
            }

            object missilesValue = _missilesField.GetValue(missileSystem);
            if (missilesValue is not IEnumerable missiles)
            {
                return false;
            }

            foreach (object missile in missiles)
            {
                if (missile == null)
                {
                    continue;
                }

                position = ReadVector3Field(missile, "Position");
                life = ReadFloatField(missile, "Life");
                maxLife = ReadFloatField(missile, "MaxLife");
                return true;
            }

            return false;
        }

        private static Vector3 ReadVector3Field(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing missile field '{fieldName}'.");
            return (Vector3)field.GetValue(instance);
        }

        private static float ReadFloatField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing missile field '{fieldName}'.");
            object value = field.GetValue(instance);
            return value is float floatValue ? floatValue : 0f;
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
