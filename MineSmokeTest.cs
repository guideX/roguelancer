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
    /// Developer-only validation harness for mounted mine droppers and proximity mine combat flow.
    /// </summary>
    internal sealed class MineSmokeTest
    {
        private const float FrameDeltaSeconds = 1f / 60f;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly FieldInfo _minesField;
        private readonly MethodInfo _launchMineMethod;

        public MineSmokeTest(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _minesField = typeof(MineSystem).GetField("_mines", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access MineSystem active mine state.");
            _launchMineMethod = typeof(Ship).GetMethod("LaunchMine", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access Ship mine launch request path.");
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateNoDropperSafety, "no-dropper safety", ref passed, ref failed);
            RunCase(ValidateMountedDropperResolved, "mounted dropper resolved", ref passed, ref failed);
            RunCase(ValidateDeployArmCooldown, "deploy/arm/cooldown", ref passed, ref failed);
            RunCase(ValidateTriggerDamageNpc, "trigger/damage NPC", ref passed, ref failed);
            RunCase(ValidateExpiry, "expiry", ref passed, ref failed);

            Console.WriteLine($"[MINE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[MINE SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[MINE SMOKE] FAIL {label}: {result.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[MINE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateNoDropperSafety()
        {
            Ship ship = CreateTestShip(mountMineDropper: false);
            if (ship.HasMountedMineDropper())
            {
                return Fail("isolated loadout unexpectedly reported a mine dropper");
            }

            RunSilenced(() => RequestMineLaunch(ship));
            if (!ship.ConsumeMineLaunchRequest())
            {
                return Fail("mine launch request was not raised safely");
            }

            MineSystem mineSystem = CreateMineSystem();
            string deployMessage = string.Empty;
            bool deployed = RunSilenced(() => mineSystem.TryDeploy(ship, ship.GetPrimaryMountedMineDropper(), out deployMessage));

            if (deployed)
            {
                return Fail("mine deployment unexpectedly succeeded without a mine dropper");
            }

            if (!string.Equals(deployMessage, "No mine dropper mounted.", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"unexpected no-dropper failure message: {deployMessage}");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateMountedDropperResolved()
        {
            Ship ship = CreateTestShip(mountMineDropper: true);
            EquipmentDefinition dropper = ship.GetPrimaryMountedMineDropper();

            if (dropper == null)
            {
                return Fail("isolated loadout did not mount a mine dropper");
            }

            if (!ship.HasMountedMineDropper())
            {
                return Fail("isolated loadout reported no mounted mine dropper");
            }

            if (!string.Equals(dropper.Id, "basic_mine_dropper", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"expected Basic Mine Dropper, got {dropper.Name ?? dropper.Id}");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateDeployArmCooldown()
        {
            Ship ship = CreateTestShip(mountMineDropper: true);
            MineSystem mineSystem = CreateMineSystem();
            EquipmentDefinition dropper = ship.GetPrimaryMountedMineDropper();
            if (dropper == null)
            {
                return Fail("isolated loadout did not expose a mine dropper for deployment");
            }

            string deployMessage = string.Empty;
            bool deployed = RunSilenced(() => mineSystem.TryDeploy(ship, dropper, out deployMessage));
            if (!deployed)
            {
                return Fail(string.IsNullOrWhiteSpace(deployMessage)
                    ? "initial mine deployment was rejected"
                    : deployMessage);
            }

            if (GetMineCount(mineSystem) != 1)
            {
                return Fail("mine deployment did not create an active mine");
            }

            if (!mineSystem.IsCoolingDown || mineSystem.CooldownRemaining <= 0f)
            {
                return Fail("mine cooldown was not activated after deployment");
            }

            if (!TryGetFirstMineSnapshot(mineSystem, out var deployedMine))
            {
                return Fail("deployed mine could not be inspected");
            }

            Vector3 behindOffset = deployedMine.Position - ship.Position;
            if (Vector3.Dot(behindOffset, ship.Forward) >= 0f)
            {
                return Fail("mine did not deploy behind the player");
            }

            string cooldownMessage = string.Empty;
            bool cooldownDeploy = RunSilenced(() => mineSystem.TryDeploy(ship, dropper, out cooldownMessage));
            if (cooldownDeploy)
            {
                return Fail("mine deployment unexpectedly succeeded during cooldown");
            }

            if (string.IsNullOrWhiteSpace(cooldownMessage) || !cooldownMessage.Contains("cooling down", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"unexpected cooldown failure message: {cooldownMessage}");
            }

            AdvanceMineSystem(mineSystem, deployedMine.ArmDelay + 0.1f);

            if (!TryGetFirstMineSnapshot(mineSystem, out var armedMine) || !armedMine.IsArmed)
            {
                return Fail("mine did not arm after the configured arm delay");
            }

            AdvanceMineSystem(mineSystem, mineSystem.CooldownRemaining + 0.1f);

            if (mineSystem.IsCoolingDown || mineSystem.CooldownRemaining > 0f)
            {
                return Fail("mine cooldown did not complete after simulated time");
            }

            string redeployMessage = string.Empty;
            bool redeployed = RunSilenced(() => mineSystem.TryDeploy(ship, dropper, out redeployMessage));
            if (!redeployed)
            {
                return Fail(string.IsNullOrWhiteSpace(redeployMessage)
                    ? "mine deployment was not available after cooldown"
                    : redeployMessage);
            }

            if (!mineSystem.IsCoolingDown || mineSystem.CooldownRemaining <= 0f)
            {
                return Fail("mine cooldown did not restart after redeployment");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateTriggerDamageNpc()
        {
            Ship ship = CreateTestShip(mountMineDropper: true);
            MineSystem mineSystem = CreateMineSystem();
            EquipmentDefinition dropper = ship.GetPrimaryMountedMineDropper();
            if (dropper == null)
            {
                return Fail("isolated loadout did not expose a mine dropper for trigger validation");
            }

            NpcShip target = CreateTestNpc("Mine Smoke Target", ship.Position + ship.Forward * 600f);
            float shieldsBefore = target.Shields.CurrentShields;
            float hullBefore = target.Hull.CurrentHull;

            string deployMessage = string.Empty;
            bool deployed = RunSilenced(() => mineSystem.TryDeploy(ship, dropper, out deployMessage));
            if (!deployed)
            {
                return Fail(string.IsNullOrWhiteSpace(deployMessage)
                    ? "mine deployment was rejected before trigger validation"
                    : deployMessage);
            }

            AdvanceMineSystem(mineSystem, dropper.MineArmDelay + 0.1f, new[] { target }, npc => true);

            if (!TryGetFirstMineSnapshot(mineSystem, out var armedMine) || !armedMine.IsArmed)
            {
                return Fail("mine did not arm before trigger validation");
            }

            target.Position = armedMine.Position;

            int detonationCount = AdvanceMineSystem(mineSystem, 0.5f, new[] { target }, npc => true);
            if (detonationCount <= 0)
            {
                return Fail("armed mine did not detonate on the hostile NPC");
            }

            if (GetMineCount(mineSystem) != 0)
            {
                return Fail("detonated mine remained active after trigger resolution");
            }

            if (target.Shields.CurrentShields >= shieldsBefore || target.Hull.CurrentHull >= hullBefore)
            {
                return Fail("mine detonation did not reduce NPC shields and hull");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateExpiry()
        {
            Ship ship = CreateTestShip(mountMineDropper: true);
            MineSystem mineSystem = CreateMineSystem();
            EquipmentDefinition dropper = ship.GetPrimaryMountedMineDropper();
            if (dropper == null)
            {
                return Fail("isolated loadout did not expose a mine dropper for expiry validation");
            }

            NpcShip distantNpc = CreateTestNpc("Mine Smoke Bystander", ship.Position + ship.Forward * 5000f);

            string deployMessage = string.Empty;
            bool deployed = RunSilenced(() => mineSystem.TryDeploy(ship, dropper, out deployMessage));
            if (!deployed)
            {
                return Fail(string.IsNullOrWhiteSpace(deployMessage)
                    ? "mine deployment was rejected before expiry validation"
                    : deployMessage);
            }

            if (!TryGetFirstMineSnapshot(mineSystem, out var mine))
            {
                return Fail("deployed mine could not be inspected for expiry validation");
            }

            int detonationCount = AdvanceMineSystem(mineSystem, mine.MaxLife + 0.1f, new[] { distantNpc }, npc => true);
            if (detonationCount != 0)
            {
                return Fail("mine detonated instead of expiring cleanly");
            }

            if (GetMineCount(mineSystem) != 0)
            {
                return Fail("expired mine remained active");
            }

            return Pass();
        }

        private Ship CreateTestShip(bool mountMineDropper)
        {
            Ship ship = new Ship(Vector3.Zero);
            ShipLoadout loadout = CreateIsolatedMineLoadout(mountMineDropper);
            RunSilenced(() => ship.SetLoadout(loadout));
            return ship;
        }

        private ShipLoadout CreateIsolatedMineLoadout(bool mountMineDropper)
        {
            return RunSilenced(() =>
            {
                var loadout = new ShipLoadout(new[]
                {
                    new ShipHardpoint
                    {
                        Id = "MineRack",
                        AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.MineDropper }
                    }
                });

                if (mountMineDropper)
                {
                    EquipmentDefinition mineDropper = EquipmentCatalog.GetById("basic_mine_dropper")
                        ?? throw new InvalidOperationException("Basic Mine Dropper definition is missing.");

                    if (!loadout.AddOwnedEquipment(mineDropper, 1))
                    {
                        throw new InvalidOperationException("Failed to add the Basic Mine Dropper to the isolated loadout.");
                    }

                    if (!loadout.TryMountEquipment("MineRack", mineDropper, out string mountMessage))
                    {
                        throw new InvalidOperationException(mountMessage);
                    }
                }

                return loadout;
            });
        }

        private MineSystem CreateMineSystem()
        {
            return RunSilenced(() => new MineSystem(_graphicsDevice));
        }

        private NpcShip CreateTestNpc(string name, Vector3 position)
        {
            return RunSilenced(() => new NpcShip(name, position, position, 1f, 0f, "pirate"));
        }

        private int AdvanceMineSystem(MineSystem mineSystem, float seconds, IReadOnlyList<NpcShip> npcShips = null, Func<NpcShip, bool> hostilePredicate = null)
        {
            if (mineSystem == null || seconds <= 0f)
            {
                return 0;
            }

            int frames = (int)Math.Ceiling(seconds / FrameDeltaSeconds);
            int detonationCount = 0;
            IReadOnlyList<NpcShip> activeNpcShips = npcShips ?? Array.Empty<NpcShip>();

            RunSilenced(() =>
            {
                for (int i = 0; i < frames; i++)
                {
                    detonationCount += mineSystem.Update(CreateGameTime(FrameDeltaSeconds), activeNpcShips, hostilePredicate).Count;
                }
            });

            return detonationCount;
        }

        private int GetMineCount(MineSystem mineSystem)
        {
            if (mineSystem == null)
            {
                return 0;
            }

            object minesValue = _minesField.GetValue(mineSystem);
            if (minesValue is ICollection collection)
            {
                return collection.Count;
            }

            if (minesValue is IEnumerable mines)
            {
                int count = 0;
                foreach (object mine in mines)
                {
                    if (mine != null)
                    {
                        count++;
                    }
                }

                return count;
            }

            return 0;
        }

        private bool TryGetFirstMineSnapshot(MineSystem mineSystem, out (Vector3 Position, float Life, float MaxLife, float TriggerRadius, float BlastRadius, float ArmDelay, bool IsArmed) snapshot)
        {
            snapshot = default;

            if (mineSystem == null)
            {
                return false;
            }

            object minesValue = _minesField.GetValue(mineSystem);
            if (minesValue is not IEnumerable mines)
            {
                return false;
            }

            foreach (object mine in mines)
            {
                if (mine == null)
                {
                    continue;
                }

                snapshot = (
                    ReadField<Vector3>(mine, "Position"),
                    ReadField<float>(mine, "Life"),
                    ReadField<float>(mine, "MaxLife"),
                    ReadField<float>(mine, "TriggerRadius"),
                    ReadField<float>(mine, "BlastRadius"),
                    ReadField<float>(mine, "ArmDelay"),
                    ReadField<bool>(mine, "IsArmed"));
                return true;
            }

            return false;
        }

        private void RequestMineLaunch(Ship ship)
        {
            _launchMineMethod.Invoke(ship, null);
        }

        private static T ReadField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing mine field '{fieldName}'.");
            object value = field.GetValue(instance);
            if (value is T typedValue)
            {
                return typedValue;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not of expected type {typeof(T).Name}.");
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
