using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for mounted countermeasures and missile spoofing.
    /// </summary>
    internal sealed class CountermeasureSmokeTest
    {
        private const float FrameDeltaSeconds = 1f / 60f;
        private const int MissileSpoofFrameBudget = 240;
        private const float MissileLaunchOffset = 22f;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly FieldInfo _missilesField;
        private readonly MethodInfo _launchCountermeasuresMethod;

        public CountermeasureSmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _missilesField = typeof(MissileSystem).GetField("_missiles", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access MissileSystem projectile state.");
            _launchCountermeasuresMethod = typeof(Ship).GetMethod("LaunchCountermeasures", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access Ship countermeasure launch request path.");
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateStarterDropperMounted, "starter dropper mounted", ref passed, ref failed);
            RunCase(ValidateNoDropperSafety, "no-dropper safety", ref passed, ref failed);
            RunCase(ValidateDeployCooldownExpiry, "deploy/cooldown/expiry", ref passed, ref failed);
            RunCase(ValidateMissileSpoofing, "missile spoofed", ref passed, ref failed);

            Console.WriteLine($"[COUNTERMEASURE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[COUNTERMEASURE SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[COUNTERMEASURE SMOKE] FAIL {label}: {result.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[COUNTERMEASURE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateStarterDropperMounted()
        {
            Ship ship = CreateTestShip();
            EquipmentDefinition dropper = ship.GetPrimaryMountedCountermeasureDropper();

            if (dropper == null)
            {
                return Fail("starter loadout did not mount a countermeasure dropper");
            }

            if (!ship.HasMountedCountermeasureDropper())
            {
                return Fail("starter loadout reported no mounted countermeasure dropper");
            }

            if (!string.Equals(dropper.Id, "basic_countermeasure_dropper", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"expected Basic Countermeasure Dropper, got {dropper.Name ?? dropper.Id}");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateNoDropperSafety()
        {
            Ship ship = CreateTestShip();
            RunSilenced(() => ship.SetLoadout(new ShipLoadout()));

            if (ship.HasMountedCountermeasureDropper())
            {
                return Fail("isolated loadout still reported a countermeasure dropper");
            }

            RunSilenced(() => _launchCountermeasuresMethod.Invoke(ship, null));
            if (ship.ConsumeCountermeasureLaunchRequest())
            {
                return Fail("countermeasure launch request was raised without a dropper");
            }

            CountermeasureSystem countermeasureSystem = CreateCountermeasureSystem();
            string deployMessage = string.Empty;
            bool deployed = RunSilenced(() => countermeasureSystem.TryDeploy(ship, ship.GetPrimaryMountedCountermeasureDropper(), out deployMessage));

            if (deployed)
            {
                return Fail("countermeasure deploy unexpectedly succeeded without a dropper");
            }

            if (!string.Equals(deployMessage, "No countermeasure dropper mounted.", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"unexpected no-dropper failure message: {deployMessage}");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDeployCooldownExpiry()
        {
            Ship ship = CreateTestShip();
            CountermeasureSystem countermeasureSystem = CreateCountermeasureSystem();
            EquipmentDefinition dropper = ship.GetPrimaryMountedCountermeasureDropper();

            if (dropper == null)
            {
                return Fail("starter loadout did not expose a countermeasure dropper for deployment");
            }

            string deployMessage = string.Empty;
            bool deployed = RunSilenced(() => countermeasureSystem.TryDeploy(ship, dropper, out deployMessage));
            if (!deployed)
            {
                return Fail(string.IsNullOrWhiteSpace(deployMessage)
                    ? "initial countermeasure deployment was rejected"
                    : deployMessage);
            }

            if (countermeasureSystem.ActiveCountermeasures.Count != 1)
            {
                return Fail("countermeasure deployment did not create an active decoy");
            }

            if (!countermeasureSystem.IsCoolingDown || countermeasureSystem.CooldownRemaining <= 0f)
            {
                return Fail("countermeasure cooldown was not activated after deployment");
            }

            string cooldownMessage = string.Empty;
            bool cooldownDeploy = RunSilenced(() => countermeasureSystem.TryDeploy(ship, dropper, out cooldownMessage));
            if (cooldownDeploy)
            {
                return Fail("countermeasure deployment unexpectedly succeeded during cooldown");
            }

            if (string.IsNullOrWhiteSpace(cooldownMessage) || !cooldownMessage.Contains("cooling down", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"unexpected cooldown failure message: {cooldownMessage}");
            }

            AdvanceCountermeasureSystem(countermeasureSystem, dropper.CountermeasureLife + 0.1f);

            if (countermeasureSystem.ActiveCountermeasures.Count != 0)
            {
                return Fail("countermeasure decoy did not expire cleanly");
            }

            if (!countermeasureSystem.IsCoolingDown)
            {
                return Fail("countermeasure cooldown ended before expiry validation completed");
            }

            AdvanceCountermeasureSystem(countermeasureSystem, countermeasureSystem.CooldownRemaining + 0.1f);

            if (countermeasureSystem.IsCoolingDown || countermeasureSystem.CooldownRemaining > 0f)
            {
                return Fail("countermeasure cooldown did not complete after simulated time");
            }

            string redeployMessage = string.Empty;
            bool redeployed = RunSilenced(() => countermeasureSystem.TryDeploy(ship, dropper, out redeployMessage));
            if (!redeployed)
            {
                return Fail(string.IsNullOrWhiteSpace(redeployMessage)
                    ? "countermeasure deployment was not available after cooldown"
                    : redeployMessage);
            }

            if (countermeasureSystem.ActiveCountermeasures.Count != 1)
            {
                return Fail("countermeasure did not redeploy after cooldown completion");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateMissileSpoofing()
        {
            Ship baselineShip = CreateTestShip();
            Ship spoofShip = CreateTestShip();
            CountermeasureSystem countermeasureSystem = CreateCountermeasureSystem();
            MissileSystem baselineMissileSystem = CreateMissileSystem();
            MissileSystem spoofMissileSystem = CreateMissileSystem();

            EquipmentDefinition baselineLauncher = baselineShip.GetPrimaryMountedMissileLauncher();
            EquipmentDefinition spoofLauncher = spoofShip.GetPrimaryMountedMissileLauncher();
            if (baselineLauncher == null || spoofLauncher == null)
            {
                return Fail("starter loadout did not mount a missile launcher");
            }

            Vector3 targetPosition = GetTargetPosition(baselineShip, 2000f);
            NpcShip baselineTarget = CreateTestNpc("Baseline Missile Target", targetPosition);
            NpcShip spoofTarget = CreateTestNpc("Spoofed Missile Target", targetPosition);

            string launchMessage = string.Empty;
            bool baselineFired = RunSilenced(() => baselineMissileSystem.TryFire(baselineShip, baselineLauncher, baselineTarget, GetLaunchOrigin(baselineShip), out launchMessage));
            if (!baselineFired)
            {
                return Fail(string.IsNullOrWhiteSpace(launchMessage)
                    ? "baseline homing missile launch was rejected"
                    : launchMessage);
            }

            launchMessage = string.Empty;
            bool spoofFired = RunSilenced(() => spoofMissileSystem.TryFire(spoofShip, spoofLauncher, spoofTarget, GetLaunchOrigin(spoofShip), out launchMessage));
            if (!spoofFired)
            {
                return Fail(string.IsNullOrWhiteSpace(launchMessage)
                    ? "spoofed homing missile launch was rejected"
                    : launchMessage);
            }

            EquipmentDefinition dropper = spoofShip.GetPrimaryMountedCountermeasureDropper();
            if (dropper == null)
            {
                return Fail("starter loadout did not expose a countermeasure dropper for spoofing");
            }

            var spoofDropper = new EquipmentDefinition
            {
                Id = $"{dropper.Id}_smoke",
                Name = dropper.Name,
                Description = dropper.Description,
                EquipmentType = dropper.EquipmentType,
                Price = dropper.Price,
                CountermeasureLife = 8f,
                CountermeasureAttractionRadius = 6000f,
                CountermeasureStrength = 20f,
                CountermeasureCooldown = dropper.CountermeasureCooldown
            };

            string deployMessage = string.Empty;
            bool deployed = RunSilenced(() => countermeasureSystem.TryDeploy(spoofShip, spoofDropper, out deployMessage));
            if (!deployed)
            {
                return Fail(string.IsNullOrWhiteSpace(deployMessage)
                    ? "countermeasure deployment failed during spoof test"
                    : deployMessage);
            }

            if (countermeasureSystem.ActiveCountermeasures.Count == 0)
            {
                return Fail("countermeasure decoy was not active for spoofing");
            }

            int decoyId = countermeasureSystem.ActiveCountermeasures[0].Id;
            float baselineDistance = float.MaxValue;
            float spoofDistance = float.MaxValue;
            bool baselineHit = false;
            bool spoofHit = false;
            bool spoofRegistered = false;
            const int spoofValidationFrames = 180;

            for (int i = 0; i < spoofValidationFrames; i++)
            {
                if (!baselineHit)
                {
                    List<HitInfo> baselineHits = RunSilenced(() => baselineMissileSystem.Update(CreateGameTime(FrameDeltaSeconds), new[] { baselineTarget }));
                    if (baselineHits.Count > 0 || !TryGetFirstMissileSnapshot(baselineMissileSystem, out var baselineMissile))
                    {
                        baselineHit = true;
                        baselineDistance = 0f;
                    }
                    else
                    {
                        baselineDistance = Vector3.Distance(baselineMissile.Position, baselineTarget.Position);
                    }
                }

                RunSilenced(() => countermeasureSystem.Update(CreateGameTime(FrameDeltaSeconds)));
                if (countermeasureSystem.ActiveCountermeasures.Count == 0)
                {
                    return Fail("countermeasure expired before missile spoofing could be validated");
                }

                List<HitInfo> spoofHits = RunSilenced(() => spoofMissileSystem.Update(CreateGameTime(FrameDeltaSeconds), new[] { spoofTarget }, countermeasureSystem));
                if (spoofHits.Count > 0)
                {
                    spoofHit = true;
                    break;
                }

                if (!TryGetFirstMissileSnapshot(spoofMissileSystem, out var spoofMissile))
                {
                    return Fail("spoofed missile expired before the comparison could complete");
                }

                if (spoofMissile.SpoofedCountermeasureId == decoyId)
                {
                    spoofRegistered = true;
                }

                spoofDistance = Vector3.Distance(spoofMissile.Position, spoofTarget.Position);
            }

            if (spoofHit)
            {
                return Fail("missile reached the original target instead of spoofing to the countermeasure");
            }

            if (!spoofRegistered)
            {
                return Fail("missile never registered the active countermeasure as its spoof target");
            }

            if (!baselineHit)
            {
                return Fail("baseline homing missile did not reach the target within the comparison window");
            }

            if (!TryGetFirstMissileSnapshot(spoofMissileSystem, out _))
            {
                return Fail("spoofed missile disappeared before final validation");
            }

            if (spoofHit)
            {
                return Fail("missile reached the original target instead of spoofing to the countermeasure");
            }

            if (spoofDistance <= 100f)
            {
                return Fail($"spoofed missile stayed too close to the original target (distance {spoofDistance:F1})");
            }

            return Pass();
        }

        private Ship CreateTestShip()
        {
            return RunSilenced(() => new Ship(Vector3.Zero));
        }

        private CountermeasureSystem CreateCountermeasureSystem()
        {
            return RunSilenced(() => new CountermeasureSystem(_graphicsDevice));
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
            return ship.Position + ship.Forward * MissileLaunchOffset;
        }

        private Vector3 GetTargetPosition(Ship ship, float distance)
        {
            return ship.Position + ship.Forward * distance;
        }

        private void AdvanceCountermeasureSystem(CountermeasureSystem countermeasureSystem, float seconds)
        {
            if (countermeasureSystem == null || seconds <= 0f)
            {
                return;
            }

            int frames = (int)Math.Ceiling(seconds / FrameDeltaSeconds);
            for (int i = 0; i < frames; i++)
            {
                RunSilenced(() => countermeasureSystem.Update(CreateGameTime(FrameDeltaSeconds)));
            }
        }

        private bool TryGetFirstMissileSnapshot(MissileSystem missileSystem, out (Vector3 Position, Vector3 Velocity, int? SpoofedCountermeasureId) snapshot)
        {
            snapshot = default;

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

                snapshot = (
                    ReadVector3Field(missile, "Position"),
                    ReadVector3Field(missile, "Velocity"),
                    ReadNullableIntField(missile, "SpoofedCountermeasureId"));
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

        private static int? ReadNullableIntField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing missile field '{fieldName}'.");
            object value = field.GetValue(instance);
            return value is int intValue ? intValue : null;
        }

        private static Vector3 GetSafeDirection(Vector3 vector, Vector3 fallback)
        {
            if (vector.LengthSquared() < 0.0001f)
            {
                return fallback;
            }

            vector.Normalize();
            return vector;
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
