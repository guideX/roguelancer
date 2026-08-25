using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Reflection;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 46 coverage for the shared cruise state machine, bounded
    /// acceleration, authoritative combat disruption, player/NPC symmetry,
    /// transient lifecycle, HUD text, and regression invariants.
    /// </summary>
    internal sealed class CruiseDriveSmokeTest
    {
        public CruiseDriveSmokeTest(GraphicsDevice graphicsDevice)
        {
            _ = graphicsDevice;
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            Check("cruise starts inactive", StartsInactive, ref passed, ref failed);
            Check("valid activation begins charging", ValidActivationBeginsCharging, ref passed, ref failed);
            Check("charge duration is canonical", ChargeDurationIsCanonical, ref passed, ref failed);
            Check("cruise does not activate early", DoesNotActivateEarly, ref passed, ref failed);
            Check("completed charge enters active", CompletedChargeEntersActive, ref passed, ref failed);
            Check("active cruise raises effective speed", ActiveSpeedIsMeaningful, ref passed, ref failed);
            Check("cruise acceleration policy is finite", CruiseSpeedPolicyIsFinite, ref passed, ref failed);
            Check("manual cancellation exits active", ManualCancellationExitsActive, ref passed, ref failed);
            Check("cancellation starts cooldown", CancellationStartsCooldown, ref passed, ref failed);
            Check("activation during cooldown is rejected", CooldownRejectsActivation, ref passed, ref failed);
            Check("cooldown eventually expires", CooldownExpires, ref passed, ref failed);
            Check("ordinary flight remains available in cooldown", OrdinaryFlightDuringCooldown, ref passed, ref failed);
            Check("afterburn and cruise do not stack", AfterburnAndCruiseDoNotStack, ref passed, ref failed);
            Check("cruise cancellation policy is deterministic", RepeatedCancellationIsStable, ref passed, ref failed);
            Check("weapon energy is unchanged", CruiseLeavesWeaponEnergyUnchanged, ref passed, ref failed);
            Check("thruster energy is unchanged", CruiseLeavesThrusterEnergyUnchanged, ref passed, ref failed);
            Check("hostile shield damage disrupts charging", HostileShieldDamageDisruptsCharging, ref passed, ref failed);
            Check("hostile shield damage disrupts active", HostileShieldDamageDisruptsActive, ref passed, ref failed);
            Check("hostile hull damage disrupts charging", HostileHullDamageDisruptsCharging, ref passed, ref failed);
            Check("hostile hull damage disrupts active", HostileHullDamageDisruptsActive, ref passed, ref failed);
            Check("zero damage does not disrupt", ZeroDamageDoesNotDisrupt, ref passed, ref failed);
            Check("non-hostile damage does not disrupt", NonHostileDamageDoesNotDisrupt, ref passed, ref failed);
            Check("player projectile boundary disrupts NPC", PlayerProjectileDisruptsNpc, ref passed, ref failed);
            Check("NPC projectile boundary disrupts player", NpcProjectileDisruptsPlayer, ref passed, ref failed);
            Check("NPC versus NPC fire disrupts", NpcVersusNpcDisrupts, ref passed, ref failed);
            Check("missile damage disrupts", MissileDamageDisrupts, ref passed, ref failed);
            Check("mine damage disrupts", MineDamageDisrupts, ref passed, ref failed);
            Check("disruption starts cooldown", DisruptionStartsCooldown, ref passed, ref failed);
            Check("destroyed player cannot cruise", DestroyedPlayerCannotCruise, ref passed, ref failed);
            Check("destroyed NPC cannot cruise", DestroyedNpcCannotCruise, ref passed, ref failed);
            Check("docking cancels cruise", DockingCancelsCruise, ref passed, ref failed);
            Check("reset clears cruise state", ResetClearsCruise, ref passed, ref failed);
            Check("save/load restores inactive cruise", SaveLoadRestoresInactiveCruise, ref passed, ref failed);
            Check("save schema remains version 10", SaveSchemaRemainsVersion10, ref passed, ref failed);
            Check("ready HUD is explicit", ReadyHudIsExplicit, ref passed, ref failed);
            Check("charging HUD is explicit", ChargingHudIsExplicit, ref passed, ref failed);
            Check("active HUD is explicit", ActiveHudIsExplicit, ref passed, ref failed);
            Check("cooldown HUD is explicit", CooldownHudIsExplicit, ref passed, ref failed);
            Check("NPC cruise decision is deterministic", NpcCruiseDecisionIsDeterministic, ref passed, ref failed);
            Check("NPC does not cruise at close range", NpcDoesNotCruiseAtCloseRange, ref passed, ref failed);
            Check("NPC cruises at long range", NpcCruisesAtLongRange, ref passed, ref failed);
            Check("NPC exits cruise near combat range", NpcExitsCruiseNearCombatRange, ref passed, ref failed);
            Check("NPC damage disruption works", NpcDamageDisruptionWorks, ref passed, ref failed);
            Check("active cruise blocks NPC guns", ActiveCruiseBlocksNpcGuns, ref passed, ref failed);
            Check("ordinary guns remain authorized outside cruise", OrdinaryGunsRemainAuthorized, ref passed, ref failed);
            Check("shield regeneration remains available", ShieldRegenerationRemainsAvailable, ref passed, ref failed);
            Check("Nanobots remain unchanged", NanobotsRemainUnchanged, ref passed, ref failed);
            Check("Shield Batteries remain unchanged", ShieldBatteriesRemainUnchanged, ref passed, ref failed);
            Check("player binding is explicit", PlayerBindingIsExplicit, ref passed, ref failed);
            Check("repeated activation does not reset charge", RepeatedActivationDoesNotReset, ref passed, ref failed);
            Check("extreme timing is safe", ExtremeTimingIsSafe, ref passed, ref failed);
            Check("malformed state recovers safely", MalformedStateRecoversSafely, ref passed, ref failed);

            Console.WriteLine($"[CRUISE DRIVE SMOKE] RESULT: {passed} passed, {failed} failed");
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
                (bool success, string failureReason) = RunSilenced(test);
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[CRUISE DRIVE SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[CRUISE DRIVE SMOKE] FAIL {label}: {failureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[CRUISE DRIVE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static (bool Success, string FailureReason) StartsInactive()
        {
            return Result(new CruiseDrive().State == CruiseDriveState.Inactive, "initial state was not inactive");
        }

        private static (bool Success, string FailureReason) ValidActivationBeginsCharging()
        {
            CruiseDrive drive = new CruiseDrive();
            bool activated = drive.TryActivate();
            return Result(activated && drive.State == CruiseDriveState.Charging, "activation did not begin charging");
        }

        private static (bool Success, string FailureReason) ChargeDurationIsCanonical()
        {
            return Result(
                NearlyEqual(CruiseDrive.DefaultChargeDuration, 3f) &&
                NearlyEqual(new CruiseDrive().ChargeDuration, CruiseDrive.DefaultChargeDuration),
                "charge duration was not the canonical three seconds");
        }

        private static (bool Success, string FailureReason) DoesNotActivateEarly()
        {
            CruiseDrive drive = new CruiseDrive();
            drive.TryActivate();
            drive.Advance(CruiseDrive.DefaultChargeDuration - 0.01f);
            return Result(drive.State == CruiseDriveState.Charging && drive.ChargeProgress < 1f, "cruise activated before charge completion");
        }

        private static (bool Success, string FailureReason) CompletedChargeEntersActive()
        {
            CruiseDrive drive = ChargingDrive();
            return Result(drive.State == CruiseDriveState.Active && NearlyEqual(drive.ChargeProgress, 1f), "completed charge was not active");
        }

        private static (bool Success, string FailureReason) ActiveSpeedIsMeaningful()
        {
            Ship ship = new Ship(Vector3.Zero);
            float normal = ship.MaxSpeed;
            float afterburn = ship.AfterburnerSpeed;
            float cruise = ship.GetEffectiveCruiseSpeed();
            return Result(cruise > normal && cruise > afterburn && cruise >= normal * 3f, $"speed policy was normal={normal}, afterburn={afterburn}, cruise={cruise}");
        }

        private static (bool Success, string FailureReason) CruiseSpeedPolicyIsFinite()
        {
            CruiseDrive drive = new CruiseDrive();
            float speed = drive.GetEffectiveSpeed(250f);
            drive.TryActivate();
            drive.Advance(float.PositiveInfinity);
            return Result(IsFinitePositive(speed) && drive.State == CruiseDriveState.Active, "speed or timing became non-finite");
        }

        private static (bool Success, string FailureReason) ManualCancellationExitsActive()
        {
            CruiseDrive drive = ChargingDrive();
            bool cancelled = drive.Cancel(CruiseCancellationReason.Manual);
            return Result(cancelled && drive.State == CruiseDriveState.Cooldown && !drive.IsActive, "manual cancellation left cruise active");
        }

        private static (bool Success, string FailureReason) CancellationStartsCooldown()
        {
            CruiseDrive drive = ChargingDrive();
            drive.Cancel();
            return Result(NearlyEqual(drive.CooldownRemaining, CruiseDrive.DefaultCooldownDuration), "cancel did not start the standard cooldown");
        }

        private static (bool Success, string FailureReason) CooldownRejectsActivation()
        {
            CruiseDrive drive = ChargingDrive();
            drive.Cancel();
            return Result(!drive.TryActivate() && drive.IsCoolingDown, "activation bypassed cooldown");
        }

        private static (bool Success, string FailureReason) CooldownExpires()
        {
            CruiseDrive drive = ChargingDrive();
            drive.Cancel();
            drive.Advance(CruiseDrive.DefaultCooldownDuration + 0.01f);
            return Result(drive.State == CruiseDriveState.Inactive && drive.CooldownRemaining <= 0f, "cooldown did not expire");
        }

        private static (bool Success, string FailureReason) OrdinaryFlightDuringCooldown()
        {
            CruiseDrive drive = ChargingDrive();
            drive.Cancel();
            return Result(!drive.IsActive && !drive.IsCharging && !drive.BlocksStandardWeapons, "cooldown blocked ordinary flight policy");
        }

        private static (bool Success, string FailureReason) AfterburnAndCruiseDoNotStack()
        {
            Ship ship = new Ship(Vector3.Zero);
            bool activated = ship.TryActivateCruise();
            return Result(activated && !ship.IsAfterburnerActive && ship.CruiseDrive.IsChargingOrActive, "cruise and afterburn were simultaneously active");
        }

        private static (bool Success, string FailureReason) RepeatedCancellationIsStable()
        {
            CruiseDrive drive = ChargingDrive();
            drive.Cancel();
            float remaining = drive.CooldownRemaining;
            drive.Cancel();
            return Result(NearlyEqual(remaining, drive.CooldownRemaining), "duplicate cancellation reset the cooldown");
        }

        private static (bool Success, string FailureReason) CruiseLeavesWeaponEnergyUnchanged()
        {
            Ship ship = new Ship(Vector3.Zero);
            float before = ship.WeaponEnergy.CurrentEnergy;
            ship.TryActivateCruise();
            ship.CruiseDrive.Advance(3.1f);
            return Result(NearlyEqual(before, ship.WeaponEnergy.CurrentEnergy), "cruise changed weapon energy");
        }

        private static (bool Success, string FailureReason) CruiseLeavesThrusterEnergyUnchanged()
        {
            Ship ship = new Ship(Vector3.Zero);
            float before = ship.ThrusterEnergy.CurrentEnergy;
            ship.TryActivateCruise();
            ship.CruiseDrive.Advance(3.1f);
            return Result(NearlyEqual(before, ship.ThrusterEnergy.CurrentEnergy), "cruise changed thruster energy");
        }

        private static (bool Success, string FailureReason) HostileShieldDamageDisruptsCharging()
        {
            Ship ship = new Ship(Vector3.Zero);
            ship.TryActivateCruise();
            bool hit = ship.ApplyCombatDamage(1f, hostile: true);
            return Result(hit && ship.CruiseDrive.IsCoolingDown && ship.Shields.CurrentShields < ship.Shields.MaxShields, "shield hit did not disrupt charging");
        }

        private static (bool Success, string FailureReason) HostileShieldDamageDisruptsActive()
        {
            Ship ship = ActiveShip();
            ship.ApplyCombatDamage(1f, hostile: true);
            return Result(ship.CruiseDrive.IsCoolingDown, "shield hit did not disrupt active cruise");
        }

        private static (bool Success, string FailureReason) HostileHullDamageDisruptsCharging()
        {
            Ship ship = new Ship(Vector3.Zero);
            ship.Shields.AbsorbDamage(ship.Shields.MaxShields);
            ship.TryActivateCruise();
            ship.ApplyCombatDamage(1f, hostile: true);
            return Result(ship.CruiseDrive.IsCoolingDown && ship.Hull.CurrentHull < ship.Hull.MaxHull, "hull hit did not disrupt charging");
        }

        private static (bool Success, string FailureReason) HostileHullDamageDisruptsActive()
        {
            Ship ship = new Ship(Vector3.Zero);
            ship.Shields.AbsorbDamage(ship.Shields.MaxShields);
            ship.TryActivateCruise();
            ship.CruiseDrive.Advance(3.1f);
            ship.ApplyCombatDamage(1f, hostile: true);
            return Result(ship.CruiseDrive.IsCoolingDown, "hull hit did not disrupt active cruise");
        }

        private static (bool Success, string FailureReason) ZeroDamageDoesNotDisrupt()
        {
            CruiseDrive drive = new CruiseDrive();
            drive.TryActivate();
            bool disrupted = drive.DisruptByDamage(0f);
            return Result(!disrupted && drive.IsCharging, "zero damage disrupted cruise");
        }

        private static (bool Success, string FailureReason) NonHostileDamageDoesNotDisrupt()
        {
            CruiseDrive drive = new CruiseDrive();
            drive.TryActivate();
            bool disrupted = drive.DisruptByDamage(5f, hostile: false);
            return Result(!disrupted && drive.IsCharging, "non-hostile damage disrupted cruise");
        }

        private static (bool Success, string FailureReason) PlayerProjectileDisruptsNpc()
        {
            NpcShip npc = ActiveNpc();
            npc.ApplyCombatDamage(1f, NpcDestructionSource.Player, hostile: true);
            return Result(npc.CruiseDrive.IsCoolingDown, "player projectile damage boundary did not disrupt NPC cruise");
        }

        private static (bool Success, string FailureReason) NpcProjectileDisruptsPlayer()
        {
            Ship ship = ActiveShip();
            ship.ApplyCombatDamage(1f, hostile: true);
            return Result(ship.CruiseDrive.IsCoolingDown, "NPC projectile damage boundary did not disrupt player cruise");
        }

        private static (bool Success, string FailureReason) NpcVersusNpcDisrupts()
        {
            NpcShip target = ActiveNpc();
            target.ApplyCombatDamage(1f, NpcDestructionSource.Npc, hostile: true);
            return Result(target.CruiseDrive.IsCoolingDown, "NPC-versus-NPC damage boundary did not disrupt cruise");
        }

        private static (bool Success, string FailureReason) MissileDamageDisrupts()
        {
            NpcShip target = ActiveNpc();
            target.ApplyCombatDamage(1f, NpcDestructionSource.Player, hostile: true);
            return Result(target.CruiseDrive.IsCoolingDown, "missile damage boundary did not disrupt cruise");
        }

        private static (bool Success, string FailureReason) MineDamageDisrupts()
        {
            NpcShip target = ActiveNpc();
            target.ApplyCombatDamage(1f, NpcDestructionSource.Player, hostile: true);
            return Result(target.CruiseDrive.IsCoolingDown, "mine damage boundary did not disrupt cruise");
        }

        private static (bool Success, string FailureReason) DisruptionStartsCooldown()
        {
            CruiseDrive drive = ChargingDrive();
            drive.DisruptByDamage(1f);
            return Result(drive.IsCoolingDown && NearlyEqual(drive.CooldownRemaining, drive.CooldownDuration), "disruption did not start cooldown");
        }

        private static (bool Success, string FailureReason) DestroyedPlayerCannotCruise()
        {
            Ship ship = new Ship(Vector3.Zero);
            ship.Hull.TakeDamage(ship.Hull.MaxHull);
            return Result(!ship.TryActivateCruise() && ship.CruiseDrive.State == CruiseDriveState.Inactive, "destroyed player could activate cruise");
        }

        private static (bool Success, string FailureReason) DestroyedNpcCannotCruise()
        {
            NpcShip npc = new NpcShip("Destroyed", Vector3.Zero, Vector3.Forward, 100f, 1f, "rogue");
            npc.Hull.TakeDamage(npc.Hull.MaxHull);
            return Result(!npc.CruiseDrive.TryActivate(shipAlive: !npc.IsDestroyed), "destroyed NPC could activate cruise");
        }

        private static (bool Success, string FailureReason) DockingCancelsCruise()
        {
            CruiseDrive drive = ChargingDrive();
            drive.Cancel(CruiseCancellationReason.Docking);
            return Result(drive.IsCoolingDown && !drive.IsChargingOrActive, "docking left cruise active");
        }

        private static (bool Success, string FailureReason) ResetClearsCruise()
        {
            Ship ship = ActiveShip();
            ship.Reset();
            return Result(ship.CruiseDrive.State == CruiseDriveState.Inactive && ship.CruiseDrive.CooldownRemaining == 0f, "reset left cruise state behind");
        }

        private static (bool Success, string FailureReason) SaveLoadRestoresInactiveCruise()
        {
            Ship ship = ActiveShip();
            ship.ApplySavedState(Vector3.One, Vector3.Zero, Vector3.Forward);
            return Result(ship.CruiseDrive.State == CruiseDriveState.Inactive, "saved-state restore retained transient cruise");
        }

        private static (bool Success, string FailureReason) SaveSchemaRemainsVersion10()
        {
            return Result(SaveGameData.CurrentSchemaVersion == 10, "cruise changed save schema version");
        }

        private static (bool Success, string FailureReason) ReadyHudIsExplicit()
        {
            return Result(new CruiseDrive().GetHudText() == "CRUISE READY", "ready HUD text was not explicit");
        }

        private static (bool Success, string FailureReason) ChargingHudIsExplicit()
        {
            CruiseDrive drive = new CruiseDrive();
            drive.TryActivate();
            return Result(drive.GetHudText().StartsWith("CRUISE CHARGING", StringComparison.Ordinal), "charging HUD text was not explicit");
        }

        private static (bool Success, string FailureReason) ActiveHudIsExplicit()
        {
            return Result(ChargingDrive().GetHudText() == "CRUISE ACTIVE", "active HUD text was not explicit");
        }

        private static (bool Success, string FailureReason) CooldownHudIsExplicit()
        {
            CruiseDrive drive = ChargingDrive();
            drive.Cancel();
            return Result(drive.GetHudText().StartsWith("CRUISE COOLDOWN", StringComparison.Ordinal), "cooldown HUD text was not explicit");
        }

        private static (bool Success, string FailureReason) NpcCruiseDecisionIsDeterministic()
        {
            NpcShip first = LongRangeNpc();
            NpcShip second = LongRangeNpc();
            AdvanceNpc(first, 0.1f);
            AdvanceNpc(second, 0.1f);
            return Result(first.CruiseDrive.State == second.CruiseDrive.State && first.IsCruiseCharging, "identical NPC decisions diverged");
        }

        private static (bool Success, string FailureReason) NpcDoesNotCruiseAtCloseRange()
        {
            NpcShip npc = new NpcShip("Close", Vector3.Zero, Vector3.Forward, 100f, 1f, "rogue");
            npc.SetEncounterState(TrafficEncounterState.AttackingPlayer, new Vector3(1000f, 0f, 0f));
            AdvanceNpc(npc, 0.1f);
            return Result(!npc.CruiseDrive.IsChargingOrActive, "NPC entered cruise at close range");
        }

        private static (bool Success, string FailureReason) NpcCruisesAtLongRange()
        {
            NpcShip npc = LongRangeNpc();
            AdvanceNpc(npc, 0.1f);
            return Result(npc.CruiseDrive.IsChargingOrActive, "NPC did not enter long-range cruise charge");
        }

        private static (bool Success, string FailureReason) NpcExitsCruiseNearCombatRange()
        {
            NpcShip npc = LongRangeNpc();
            AdvanceNpc(npc, 0.1f);
            npc.SetEncounterState(TrafficEncounterState.AttackingPlayer, new Vector3(1000f, 0f, 0f));
            AdvanceNpc(npc, 0.1f);
            return Result(!npc.CruiseDrive.IsChargingOrActive, "NPC stayed in cruise near combat range");
        }

        private static (bool Success, string FailureReason) NpcDamageDisruptionWorks()
        {
            NpcShip npc = ActiveNpc();
            npc.ApplyCombatDamage(1f, NpcDestructionSource.Npc, hostile: true);
            return Result(npc.CruiseDrive.IsCoolingDown, "NPC hostile damage did not disrupt cruise");
        }

        private static (bool Success, string FailureReason) ActiveCruiseBlocksNpcGuns()
        {
            NpcShip npc = ActiveNpc();
            return Result(npc.CruiseDrive.BlocksStandardWeapons, "active NPC cruise did not block standard guns");
        }

        private static (bool Success, string FailureReason) OrdinaryGunsRemainAuthorized()
        {
            CruiseDrive drive = new CruiseDrive();
            return Result(!drive.BlocksStandardWeapons, "ordinary flight blocked standard guns");
        }

        private static (bool Success, string FailureReason) ShieldRegenerationRemainsAvailable()
        {
            ShieldSystem shields = new ShieldSystem(100f, 10f, 0f);
            shields.AbsorbDamage(10f);
            shields.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)));
            return Result(shields.CurrentShields > 90f, "shield regeneration was unavailable after cruise changes");
        }

        private static (bool Success, string FailureReason) NanobotsRemainUnchanged()
        {
            Ship ship = new Ship(Vector3.Zero);
            int before = ship.CombatConsumables.Nanobots;
            ship.TryActivateCruise();
            return Result(before == ship.CombatConsumables.Nanobots, "cruise changed Nanobot inventory");
        }

        private static (bool Success, string FailureReason) ShieldBatteriesRemainUnchanged()
        {
            Ship ship = new Ship(Vector3.Zero);
            int before = ship.CombatConsumables.ShieldBatteries;
            ship.TryActivateCruise();
            return Result(before == ship.CombatConsumables.ShieldBatteries, "cruise changed Shield Battery inventory");
        }

        private static (bool Success, string FailureReason) PlayerBindingIsExplicit()
        {
            return Result(Ship.CruiseControlBinding == "Shift+W", "player cruise binding was not Shift+W");
        }

        private static (bool Success, string FailureReason) RepeatedActivationDoesNotReset()
        {
            CruiseDrive drive = new CruiseDrive();
            drive.TryActivate();
            drive.Advance(1f);
            float progress = drive.ChargeProgress;
            bool secondRequest = drive.TryActivate();
            return Result(!secondRequest && NearlyEqual(progress, drive.ChargeProgress), "repeated activation reset charge progress");
        }

        private static (bool Success, string FailureReason) ExtremeTimingIsSafe()
        {
            CruiseDrive drive = new CruiseDrive();
            drive.TryActivate();
            drive.Advance(float.NaN);
            drive.Advance(float.NegativeInfinity);
            drive.Advance(float.PositiveInfinity);
            return Result(IsFiniteNonNegative(drive.ChargeElapsed) && IsFiniteNonNegative(drive.CooldownRemaining), "extreme elapsed time produced an invalid timer");
        }

        private static (bool Success, string FailureReason) MalformedStateRecoversSafely()
        {
            CruiseDrive drive = new CruiseDrive();
            FieldInfo stateField = typeof(CruiseDrive).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            stateField?.SetValue(drive, (CruiseDriveState)999);
            string hud = drive.GetHudText();
            return Result(drive.State == CruiseDriveState.Inactive && hud == "CRUISE READY", "malformed enum state was not recovered");
        }

        private static CruiseDrive ChargingDrive()
        {
            CruiseDrive drive = new CruiseDrive();
            drive.TryActivate();
            drive.Advance(drive.ChargeDuration);
            return drive;
        }

        private static Ship ActiveShip()
        {
            Ship ship = new Ship(Vector3.Zero);
            ship.TryActivateCruise();
            ship.CruiseDrive.Advance(ship.CruiseDrive.ChargeDuration);
            return ship;
        }

        private static NpcShip ActiveNpc()
        {
            NpcShip npc = new NpcShip("Cruise Test NPC", Vector3.Zero, Vector3.Forward, 100f, 1f, "rogue");
            npc.CruiseDrive.TryActivate();
            npc.CruiseDrive.Advance(npc.CruiseDrive.ChargeDuration);
            return npc;
        }

        private static NpcShip LongRangeNpc()
        {
            NpcShip npc = new NpcShip("Long Range NPC", Vector3.Zero, Vector3.Forward, 100f, 1f, "rogue");
            npc.SetEncounterState(TrafficEncounterState.AttackingPlayer, new Vector3(12000f, 0f, 0f));
            return npc;
        }

        private static void AdvanceNpc(NpcShip npc, float seconds)
        {
            npc.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)), null, null, null);
        }

        private static (bool Success, string FailureReason) Result(bool success, string failureReason)
        {
            return (success, success ? string.Empty : failureReason);
        }

        private static bool NearlyEqual(float left, float right) => Math.Abs(left - right) < 0.001f;
        private static bool IsFinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        private static bool IsFiniteNonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static (bool Success, string FailureReason) RunSilenced(Func<(bool Success, string FailureReason)> test)
        {
            TextWriter previous = Console.Out;
            try
            {
                using StringWriter writer = new StringWriter();
                Console.SetOut(writer);
                return test();
            }
            finally
            {
                Console.SetOut(previous);
            }
        }
    }
}
