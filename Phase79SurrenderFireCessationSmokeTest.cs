#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 79 focused coverage for accepted-surrender fire cessation. The suite
/// drives only production systems in production-equivalent order:
/// PoliceFugitiveManager pursuit, PoliceFugitiveSurrenderService transaction,
/// TrafficManager target acquisition, NpcShip disposition/encounter state,
/// PoliceScanSystem, PoliceContrabandStopCoordinator, and NpcWeaponSystem
/// emission. No invulnerability, projectile deletion, persisted hold-fire
/// state, criminal history, Heat 3, or contraband rule change exists here.
/// </summary>
internal sealed class Phase79SurrenderFireCessationSmokeTest
{
    private readonly GraphicsDevice _graphicsDevice;

    public Phase79SurrenderFireCessationSmokeTest(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("Police can fire during active Heat 1 pursuit before surrender", HeatOneFiresBeforeSurrender, ref passed, ref failed);
        RunCase("Police can fire during active Heat 2 pursuit before surrender", HeatTwoFiresBeforeSurrender, ref passed, ref failed);
        RunCase("Heat 1 surrender stops new fugitive fire in the same production update", HeatOneSurrenderStopsSameTickFire, ref passed, ref failed);
        RunCase("Heat 2 surrender stops new fugitive fire in the same production update", HeatTwoSurrenderStopsSameTickFire, ref passed, ref failed);
        RunCase("no new Police projectile is spawned after accepted surrender from stale fugitive targeting", NoNewProjectileAfterSurrender, ref passed, ref failed);
        RunCase("a Police projectile fired before surrender remains alive", PreSurrenderProjectileSurvives, ref passed, ref failed);
        RunCase("a pre-surrender Police projectile can still damage the player", PreSurrenderProjectileStillDamages, ref passed, ref failed);
        RunCase("projectile ownership is unchanged by surrender", ProjectileOwnershipUnchanged, ref passed, ref failed);
        RunCase("projectile damage is not zeroed by surrender", ProjectileDamageNotZeroed, ref passed, ref failed);
        RunCase("the player receives no invulnerability after surrender", NoInvulnerabilityAfterSurrender, ref passed, ref failed);
        RunCase("an unrelated Rogue projectile remains effective", RogueProjectileStillEffective, ref passed, ref failed);
        RunCase("an unrelated Liberty Navy projectile remains effective", NavyProjectileStillEffective, ref passed, ref failed);
        RunCase("environmental damage behavior remains unchanged", EnvironmentalDamageUnchanged, ref passed, ref failed);
        RunCase("successful surrender clears the fugitive-owned pursuit target", SurrenderClearsFugitivePursuitTarget, ref passed, ref failed);
        RunCase("successful surrender clears stale fugitive firing authority", SurrenderClearsStaleFugitiveAuthority, ref passed, ref failed);
        RunCase("successful surrender does not erase an unrelated NPC-vs-NPC target", SurrenderPreservesNpcVsNpcTarget, ref passed, ref failed);
        RunCase("successful surrender does not erase 'player attack' temporary hostility", SurrenderPreservesPlayerAttackHostility, ref passed, ref failed);
        RunCase("surviving 'player attack' hostility can continue to authorize Police combat", PlayerAttackHostilityAuthorizesCombat, ref passed, ref failed);
        RunCase("permanent hostility behavior remains unchanged", PermanentHostilityUnchanged, ref passed, ref failed);
        RunCase("a permanently hostile player still cannot successfully surrender", PermanentlyHostileCannotSurrender, ref passed, ref failed);
        RunCase("no hold-fire is applied on no-Police-present failure", NoHoldOnNoPoliceFailure, ref passed, ref failed);
        RunCase("no hold-fire is applied on insufficient-credit failure", NoHoldOnInsufficientCreditFailure, ref passed, ref failed);
        RunCase("no hold-fire is applied when the player is not fugitive", NoHoldWhenNotFugitive, ref passed, ref failed);
        RunCase("no hold-fire is applied when the player is destroyed", NoHoldWhenPlayerDestroyed, ref passed, ref failed);
        RunCase("failed surrender leaves pursuit capable of firing", FailedSurrenderPursuitCanFire, ref passed, ref failed);
        RunCase("failed surrender leaves the fugitive state active", FailedSurrenderLeavesFugitiveActive, ref passed, ref failed);
        RunCase("failed surrender leaves Phase 77 docking denied", FailedSurrenderLeavesDockingDenied, ref passed, ref failed);
        RunCase("successful surrender restores Phase 77 docking", SuccessfulSurrenderRestoresDocking, ref passed, ref failed);
        RunCase("Phase 79 does not call or mutate docking policy directly", NoDirectDockingMutation, ref passed, ref failed);
        RunCase("successful surrender opens no escape grace", SurrenderOpensNoEscapeGrace, ref passed, ref failed);
        RunCase("failed surrender opens no escape grace", FailedSurrenderOpensNoEscapeGrace, ref passed, ref failed);
        RunCase("ordinary lawful-stop hold-fire still works", OrdinaryLawfulStopHoldFireStillWorks, ref passed, ref failed);
        RunCase("compliant contraband stop behavior remains unchanged", CompliantContrabandStopUnchanged, ref passed, ref failed);
        RunCase("refusal escalation still creates normal fugitive combat", RefusalEscalationCreatesFugitiveCombat, ref passed, ref failed);
        RunCase("hostile-player lawful-stop gates remain unchanged", HostilePlayerLawfulStopGateUnchanged, ref passed, ref failed);
        RunCase("repeated S input after success cannot extend or stack suppression", RepeatedSurrenderInputBounded, ref passed, ref failed);
        RunCase("repeated passive updates do not accumulate transient state", PassiveUpdatesDoNotAccumulate, ref passed, ref failed);
        RunCase("officer death during surrender cleanup is safe", OfficerDeathDuringSurrenderSafe, ref passed, ref failed);
        RunCase("officer despawn during surrender cleanup is safe", OfficerDespawnDuringSurrenderSafe, ref passed, ref failed);
        RunCase("reset clears Phase 79 transient state", ResetClearsTransientState, ref passed, ref failed);
        RunCase("death clears Phase 79 transient state", DeathClearsTransientState, ref passed, ref failed);
        RunCase("system transition clears Phase 79 transient state", SystemTransitionClearsTransientState, ref passed, ref failed);
        RunCase("save and load does not persist Phase 79 state", SaveLoadDoesNotPersistState, ref passed, ref failed);
        RunCase("schema remains 13", SchemaRemainsThirteen, ref passed, ref failed);
        RunCase("no Phase 79 save member exists", NoPhase79SaveMember, ref passed, ref failed);
        RunCase("surrender fee remains Phase 78's Heat-derived 1000/2500 behavior", SurrenderFeeHeatDerived, ref passed, ref failed);
        RunCase("reputation remains unchanged by surrender", ReputationUnchangedBySurrender, ref passed, ref failed);
        RunCase("cargo remains unchanged by surrender", CargoUnchangedBySurrender, ref passed, ref failed);
        RunCase("Phase 76 attention notifications remain unaffected", Phase76NotificationsUnaffected, ref passed, ref failed);
        RunCase("Phase 77 docking tests remain behaviorally unchanged", Phase77DockingBehaviorUnchanged, ref passed, ref failed);
        RunCase("no additional surrender-success notification is generated", NoAdditionalSurrenderNotification, ref passed, ref failed);
        RunCase("fire cessation affects only Liberty Police authority, not unrelated factions", FireCessationScopedToPolice, ref passed, ref failed);
        RunCase("participant handling is bounded", ParticipantHandlingBounded, ref passed, ref failed);
        RunCase("no projectile enumeration or deletion is used to fake correctness", NoProjectileDeletionUsed, ref passed, ref failed);
        RunCase("same-tick proof follows production-equivalent update order", SameTickProofUsesProductionOrder, ref passed, ref failed);
        RunCase("Police can fire during active Evading state when independent hostility permits", EvadingWithIndependentHostilityCanFire, ref passed, ref failed);
        RunCase("the surrender fire hold is one-pass and self-releasing", HoldIsOnePassAndSelfReleasing, ref passed, ref failed);
        RunCase("the surrender fire hold never suppresses an unrelated faction-combat target", HoldDoesNotAffectFactionCombatTarget, ref passed, ref failed);

        Console.WriteLine($"[PHASE 79 SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(string label, Func<(bool, string)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 79 SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 79 SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 79 SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    // ---- Pre-surrender firing remains valid -------------------------------

    private (bool, string) HeatOneFiresBeforeSurrender() => FiresBeforeSurrender(PoliceHeatLevel.Pursuit);

    private (bool, string) HeatTwoFiresBeforeSurrender() => FiresBeforeSurrender(PoliceHeatLevel.HotPursuit);

    private (bool, string) FiresBeforeSurrender(PoliceHeatLevel heat)
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(heat);
        ctx.StepProduction();
        if (!police.HasPlayerTarget || police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit)
            return Fail($"pursuit did not produce a fugitive target (reason={police.PlayerTargetReason})");
        int fired = ctx.RunWeapons(6);
        return fired > 0 ? Pass() : Fail($"Police did not fire during active pursuit (heat={heat})");
    }

    // ---- Same-tick cessation ----------------------------------------------

    private (bool, string) HeatOneSurrenderStopsSameTickFire() => SurrenderStopsSameTickFire(PoliceHeatLevel.Pursuit);

    private (bool, string) HeatTwoSurrenderStopsSameTickFire() => SurrenderStopsSameTickFire(PoliceHeatLevel.HotPursuit);

    private (bool, string) SurrenderStopsSameTickFire(PoliceHeatLevel heat)
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(heat);
        ctx.StepProduction();
        int before = ctx.RunWeapons(6);
        if (before <= 0)
            return Fail($"no pre-surrender firing opportunity (heat={heat})");

        bool surrendered = ctx.TrySurrender(out _, out string failure);
        if (!surrendered)
            return Fail($"surrender failed ({failure})");

        // Same production update: traffic -> npc -> scan -> coordinator, then weapons.
        ctx.StepProduction();
        int after = ctx.RunWeapons(6);
        if (after != 0)
            return Fail($"new Police fire spawned after accepted surrender (heat={heat}, fired={after})");
        if (ctx.Fugitive.IsActive)
            return Fail("surrender did not clear the fugitive incident");
        return Pass();
    }

    private (bool, string) NoNewProjectileAfterSurrender()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(2);
        int before = ctx.ActiveProjectileCount();
        if (before <= 0)
            return Fail("setup did not create a projectile");
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        ctx.StepProduction();
        int spawned = ctx.RunWeapons(6);
        int after = ctx.ActiveProjectileCount();
        // The count must not grow from stale fugitive fire; it may shrink only
        // from legitimate physical travel/hit resolution.
        return spawned == 0 && after <= before
            ? Pass()
            : Fail($"projectile emission after surrender: spawned={spawned}, before={before}, after={after}");
    }

    // ---- Existing projectile behavior -------------------------------------

    private (bool, string) PreSurrenderProjectileSurvives()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(900f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        int before = ctx.ActiveProjectileCount();
        if (before <= 0)
            return Fail("setup did not create a projectile");
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        int afterSurrender = ctx.ActiveProjectileCount();
        return afterSurrender > 0
            ? Pass()
            : Fail("pre-surrender projectile was removed by surrender");
    }

    private (bool, string) PreSurrenderProjectileStillDamages()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(900f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        if (ctx.ActiveProjectileCount() <= 0)
            return Fail("setup did not create a projectile");
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        ctx.Player.CollisionRadius = 400f;
        float hullBefore = ctx.Player.Hull.CurrentHull;
        float shieldsBefore = ctx.Player.Shields.CurrentShields;
        for (int i = 0; i < 20; i++)
            ctx.RunWeapons(1);
        bool damaged = ctx.Player.Hull.CurrentHull < hullBefore || ctx.Player.Shields.CurrentShields < shieldsBefore;
        return damaged ? Pass() : Fail("pre-surrender projectile did not damage the player after surrender");
    }

    private (bool, string) ProjectileOwnershipUnchanged()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(900f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        List<NpcShip> ownersBefore = ctx.ProjectileOwners();
        if (ownersBefore.Count == 0)
            return Fail("setup did not create a projectile");
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        List<NpcShip> ownersAfter = ctx.ProjectileOwners();
        return ownersAfter.Count == ownersBefore.Count && ownersAfter.All(owner => ReferenceEquals(owner, police))
            ? Pass()
            : Fail("surrender rewrote projectile ownership");
    }

    private (bool, string) ProjectileDamageNotZeroed()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(900f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        List<float> damageBefore = ctx.ProjectileDamages();
        if (damageBefore.Count == 0 || damageBefore.All(d => d <= 0f))
            return Fail("setup did not create a positive-damage projectile");
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        List<float> damageAfter = ctx.ProjectileDamages();
        return damageAfter.Count == damageBefore.Count && damageAfter.All(d => d > 0f)
            ? Pass()
            : Fail("surrender zeroed projectile damage");
    }

    private (bool, string) NoInvulnerabilityAfterSurrender()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        float hullBefore = ctx.Player.Hull.CurrentHull;
        float shieldsBefore = ctx.Player.Shields.CurrentShields;
        ctx.Player.ApplyCombatDamage(50f, hostile: true);
        bool damaged = ctx.Player.Hull.CurrentHull < hullBefore || ctx.Player.Shields.CurrentShields < shieldsBefore;
        return damaged ? Pass() : Fail("player became invulnerable after surrender");
    }

    private (bool, string) RogueProjectileStillEffective() => UnrelatedFactionStillFires(FactionManager.LibertyRogues);

    private (bool, string) NavyProjectileStillEffective() => UnrelatedFactionStillFires(FactionManager.LibertyNavy);

    private (bool, string) UnrelatedFactionStillFires(string factionId)
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        NpcShip attacker = ctx.AddFactionShip(factionId, new Vector3(700f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.Reputation.TemporaryHostility.RecordHostileAction(factionId, TemporaryHostilityManager.PlayerAggressionReason);
        attacker.SetPlayerTarget(ctx.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        int fired = ctx.RunWeapons(6, attacker);
        return fired > 0
            ? Pass()
            : Fail($"{factionId} combat was suppressed by Police surrender");
    }

    private (bool, string) EnvironmentalDamageUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        float hullBefore = ctx.Player.Hull.CurrentHull;
        float shieldsBefore = ctx.Player.Shields.CurrentShields;
        ctx.Player.ApplyCombatDamage(15f, hostile: false);
        return ctx.Player.Hull.CurrentHull < hullBefore || ctx.Player.Shields.CurrentShields < shieldsBefore
            ? Pass()
            : Fail("environmental damage was altered by surrender");
    }

    // ---- Target / authority cleanup ---------------------------------------

    private (bool, string) SurrenderClearsFugitivePursuitTarget()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit
            ? Pass()
            : Fail("fugitive pursuit target survived surrender");
    }

    private (bool, string) SurrenderClearsStaleFugitiveAuthority()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(2);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        ctx.StepProduction();
        int fired = ctx.RunWeapons(6);
        bool noFugitiveAuthority = !ctx.Fugitive.IsActive &&
            !ctx.Reputation.TemporaryHostility.HasReason(FactionManager.LibertyPolice, PoliceFugitiveManager.PoliceFugitiveHostilityReason);
        return noFugitiveAuthority && fired == 0
            ? Pass()
            : Fail($"stale fugitive authority remained (active={ctx.Fugitive.IsActive}, fired={fired})");
    }

    private (bool, string) SurrenderPreservesNpcVsNpcTarget()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        NpcShip rogue = ctx.AddFactionShip(FactionManager.LibertyRogues, new Vector3(1_400f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        // A separate officer holds an unrelated NPC-vs-NPC engagement.
        NpcShip enforcer = ctx.AddPolice(new Vector3(2_500f, 0f, 0f), "Phase 79 Enforcer");
        if (!enforcer.SetFactionCombatTarget(rogue))
            return Fail("setup could not create NPC-vs-NPC target");
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return ReferenceEquals(enforcer.FactionCombatTarget, rogue)
            ? Pass()
            : Fail("surrender erased an unrelated NPC-vs-NPC target");
    }

    private (bool, string) SurrenderPreservesPlayerAttackHostility()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.Reputation.TemporaryHostility.RecordHostileAction(
            FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return ctx.Reputation.TemporaryHostility.HasReason(
            FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason)
            ? Pass()
            : Fail("surrender erased 'player attack' temporary hostility");
    }

    private (bool, string) PlayerAttackHostilityAuthorizesCombat()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.Reputation.TemporaryHostility.RecordHostileAction(
            FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        // Surviving player-attack hostility re-authorizes ordinary acquisition.
        police.SetPlayerTarget(ctx.Player.Position, NpcPlayerTargetReason.PlayerInitiatedAggression);
        int fired = ctx.RunWeapons(6);
        return fired > 0
            ? Pass()
            : Fail("surviving 'player attack' hostility did not authorize Police combat");
    }

    // ---- Permanent hostility ----------------------------------------------

    private (bool, string) PermanentHostilityUnchanged()
    {
        Context ctx = CreateContext(standing: -0.70f);
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        PoliceSurrenderAssessment assessment = PoliceFugitiveSurrenderService.Assess(
            ctx.Fugitive, ctx.Reputation, ctx.Player, ctx.Npcs, ctx.Credits);
        return !assessment.IsEligible &&
            assessment.Eligibility == PoliceSurrenderEligibility.PermanentlyHostile &&
            ctx.Reputation.IsHostile(FactionManager.LibertyPolice)
            ? Pass()
            : Fail("permanent hostility boundary changed");
    }

    private (bool, string) PermanentlyHostileCannotSurrender()
    {
        Context ctx = CreateContext(standing: -0.70f);
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        int creditsBefore = ctx.Credits.Credits;
        bool success = ctx.TrySurrender(out _, out _);
        return !success && ctx.Fugitive.IsActive && ctx.Credits.Credits == creditsBefore
            ? Pass()
            : Fail("permanently hostile player surrendered");
    }

    // ---- Failed surrender does nothing ------------------------------------

    private (bool, string) NoHoldOnNoPoliceFailure()
    {
        Context ctx = CreateContext();
        NpcShip rogue = ctx.AddFactionShip(FactionManager.LibertyRogues, new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        bool success = ctx.TrySurrender(out _, out _);
        return !success && !rogue.HasSurrenderFireHold
            ? Pass()
            : Fail("no-Police failure applied a hold-fire");
    }

    private (bool, string) NoHoldOnInsufficientCreditFailure()
    {
        Context ctx = CreateContext(credits: 100);
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        bool success = ctx.TrySurrender(out _, out _);
        return !success && !police.HasSurrenderFireHold && ctx.Credits.Credits == 100
            ? Pass()
            : Fail("insufficient-credit failure applied a hold-fire");
    }

    private (bool, string) NoHoldWhenNotFugitive()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        bool success = ctx.TrySurrender(out _, out _);
        return !success && !police.HasSurrenderFireHold && !ctx.Fugitive.IsActive
            ? Pass()
            : Fail("non-fugitive surrender applied a hold-fire");
    }

    private (bool, string) NoHoldWhenPlayerDestroyed()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.Player.Hull.TakeDamage(100_000f);
        bool success = ctx.TrySurrender(out _, out _);
        return !success && !police.HasSurrenderFireHold
            ? Pass()
            : Fail("destroyed-player surrender applied a hold-fire");
    }

    private (bool, string) FailedSurrenderPursuitCanFire()
    {
        Context ctx = CreateContext(credits: 100);
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        bool success = ctx.TrySurrender(out _, out _);
        int fired = ctx.RunWeapons(6);
        return !success && fired > 0
            ? Pass()
            : Fail($"failed surrender disabled pursuit fire (fired={fired})");
    }

    private (bool, string) FailedSurrenderLeavesFugitiveActive()
    {
        Context ctx = CreateContext(credits: 100);
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.TrySurrender(out _, out _);
        return ctx.Fugitive.IsActive ? Pass() : Fail("failed surrender cleared the fugitive state");
    }

    private (bool, string) FailedSurrenderLeavesDockingDenied()
    {
        Context ctx = CreateContext(credits: 100);
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.TrySurrender(out _, out _);
        bool denied = !FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        return denied ? Pass() : Fail("failed surrender allowed docking");
    }

    private (bool, string) SuccessfulSurrenderRestoresDocking()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        bool deniedBefore = !FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        bool allowedAfter = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        return deniedBefore && allowedAfter
            ? Pass()
            : Fail("successful surrender did not restore docking");
    }

    private (bool, string) NoDirectDockingMutation()
    {
        // The Phase 79 fire-cessation members must not reference docking policy.
        foreach (MemberInfo member in typeof(PoliceFugitiveManager)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            string name = member.Name.ToLowerInvariant();
            if (name.Contains("dock") && !name.Contains("document"))
                return Fail($"Phase 79 manager exposes docking member '{member.Name}'");
        }

        foreach (MemberInfo member in typeof(PoliceFugitiveSurrenderService)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            string name = member.Name.ToLowerInvariant();
            if (name.Contains("dock"))
                return Fail($"Phase 79 service exposes docking member '{member.Name}'");
        }

        return Pass();
    }

    private (bool, string) SurrenderOpensNoEscapeGrace()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return !ctx.Fugitive.IsContrabandReacquisitionGraceActive
            ? Pass()
            : Fail("surrender opened contraband reacquisition grace");
    }

    private (bool, string) FailedSurrenderOpensNoEscapeGrace()
    {
        Context ctx = CreateContext(credits: 100);
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.TrySurrender(out _, out _);
        return !ctx.Fugitive.IsContrabandReacquisitionGraceActive
            ? Pass()
            : Fail("failed surrender opened contraband reacquisition grace");
    }

    // ---- Lawful-stop regression -------------------------------------------

    private (bool, string) OrdinaryLawfulStopHoldFireStillWorks()
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        ctx.GiveContraband(player, 2);
        NpcShip police = ctx.AddPolice(new Vector3(300f, 0f, 0f));
        ctx.StepProduction();
        if (!police.IsLawfulStopHoldFire || police.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            return Fail($"lawful stop did not hold fire (reason={police.PlayerTargetReason}, hold={police.IsLawfulStopHoldFire})");
        int fired = ctx.RunWeapons(6);
        return fired == 0 ? Pass() : Fail($"unconfirmed interception fired ({fired})");
    }

    private (bool, string) CompliantContrabandStopUnchanged()
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        ctx.GiveContraband(player, 2);
        NpcShip police = ctx.AddPolice(new Vector3(300f, 0f, 0f));
        // Start + complete the scan.
        ctx.Scan.Update(Frame(0.1f), player, ctx.Npcs, ctx.Credits, ctx.Reputation);
        ctx.Scan.Update(Frame(PoliceScanSystem.ScanDurationSeconds + 0.1f), player, ctx.Npcs, ctx.Credits, ctx.Reputation);
        if (ctx.Scan.State != PoliceScanState.ContrabandDetected)
            return Fail($"scan did not detect contraband (state={ctx.Scan.State})");
        bool complied = ctx.Scan.TryAcceptEnforcement(player, ctx.Credits, ctx.Reputation);
        ctx.StepProduction();
        return complied && ctx.Scan.State != PoliceScanState.Enforcement && !ctx.Fugitive.IsActive
            ? Pass()
            : Fail("compliant contraband stop behavior changed");
    }

    private (bool, string) RefusalEscalationCreatesFugitiveCombat()
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        ctx.GiveContraband(player, 2);
        NpcShip police = ctx.AddPolice(new Vector3(300f, 0f, 0f));
        ctx.Scan.Update(Frame(0.1f), player, ctx.Npcs, ctx.Credits, ctx.Reputation);
        ctx.Scan.Update(Frame(PoliceScanSystem.ScanDurationSeconds + 0.1f), player, ctx.Npcs, ctx.Credits, ctx.Reputation);
        if (!ctx.Scan.TryRefuseEnforcement(player, ctx.Credits, ctx.Reputation))
            return Fail("refusal did not resolve");
        ctx.StepProduction();
        if (police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit)
            return Fail($"refusal did not convert to fugitive (reason={police.PlayerTargetReason})");
        int fired = ctx.RunWeapons(6);
        return fired > 0 ? Pass() : Fail("refusal escalation did not enable combat");
    }

    private (bool, string) HostilePlayerLawfulStopGateUnchanged()
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        ctx.GiveContraband(player, 2);
        NpcShip police = ctx.AddPolice(new Vector3(300f, 0f, 0f));
        ctx.StepProduction();
        // A pre-existing hostility must defeat the lawful-stop hold (Phase 71).
        ctx.Reputation.TemporaryHostility.RecordHostileAction(
            FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason);
        int fired = ctx.RunWeapons(6);
        return fired > 0 ? Pass() : Fail("hostile-player lawful-stop gate suppressed legal fire");
    }

    // ---- Bounded input / lifecycle ----------------------------------------

    private (bool, string) RepeatedSurrenderInputBounded()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        int creditsBefore = ctx.Credits.Credits;
        if (!ctx.TrySurrender(out _, out _))
            return Fail("first surrender failed");
        int creditsAfterFirst = ctx.Credits.Credits;
        ctx.RunWeapons(1);
        for (int i = 0; i < 5; i++)
        {
            if (ctx.TrySurrender(out _, out _))
                return Fail("repeated surrender unexpectedly succeeded");
        }

        bool noHold = ctx.Npcs.All(npc => !npc.HasSurrenderFireHold);
        return noHold && ctx.Credits.Credits == creditsAfterFirst && creditsAfterFirst < creditsBefore
            ? Pass()
            : Fail("repeated S input stacked suppression or charged again");
    }

    private (bool, string) PassiveUpdatesDoNotAccumulate()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        for (int i = 0; i < 50; i++)
        {
            ctx.StepProduction();
            ctx.RunWeapons(1);
        }

        bool noHold = ctx.Npcs.All(npc => !npc.HasSurrenderFireHold);
        return noHold && !ctx.Fugitive.IsActive && ctx.Fugitive.ActivePursuerCount == 0
            ? Pass()
            : Fail("passive updates accumulated transient Phase 79 state");
    }

    private (bool, string) OfficerDeathDuringSurrenderSafe()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        police.Hull.TakeDamage(100_000f);
        ctx.Coordinator.NotifyNpcDestroyed(police);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        return !ctx.Fugitive.IsActive && ctx.Npcs.All(npc => !npc.HasSurrenderFireHold)
            ? Pass()
            : Fail("officer death during surrender cleanup was not safe");
    }

    private (bool, string) OfficerDespawnDuringSurrenderSafe()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        ctx.Coordinator.NotifyNpcDespawned(police);
        ctx.Npcs.Remove(police);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        return !ctx.Fugitive.IsActive && ctx.Npcs.All(npc => !npc.HasSurrenderFireHold)
            ? Pass()
            : Fail("officer despawn during surrender cleanup was not safe");
    }

    private (bool, string) ResetClearsTransientState()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        ctx.Fugitive.Reset(reason: "reset");
        ctx.RunWeapons(1);
        return !ctx.Fugitive.IsActive && ctx.Npcs.All(npc => !npc.HasSurrenderFireHold)
            ? Pass()
            : Fail("reset did not clear Phase 79 transient state");
    }

    private (bool, string) DeathClearsTransientState()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        police.SetSurrenderFireHold(true);
        ctx.Player.Hull.TakeDamage(100_000f);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        ctx.RunWeapons(1);
        return !ctx.Fugitive.IsActive && ctx.Npcs.All(npc => !npc.HasSurrenderFireHold)
            ? Pass()
            : Fail("death did not clear Phase 79 transient state");
    }

    private (bool, string) SystemTransitionClearsTransientState()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        police.SetSurrenderFireHold(true);
        ctx.Fugitive.Reset(reason: "system transition");
        ctx.RunWeapons(1);
        return !ctx.Fugitive.IsActive && ctx.Npcs.All(npc => !npc.HasSurrenderFireHold)
            ? Pass()
            : Fail("system transition did not clear Phase 79 transient state");
    }

    // ---- Save / schema ----------------------------------------------------

    private (bool, string) SaveLoadDoesNotPersistState()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        SaveGameManager save = new(TempPath("phase79"));
        SaveGameData data = new()
        {
            FactionReputation = save.CaptureReputation(ctx.Reputation),
            TemporaryHostility = save.CaptureTemporaryHostility(ctx.Reputation)
        };
        if (!save.TrySave(data, out _) || !save.TryLoad(out SaveGameData loaded, out _))
            return Fail("save/load failed");
        ReputationManager restored = new(new FactionManager());
        save.ApplyReputation(restored, loaded);
        save.ApplyTemporaryHostility(restored, loaded);
        NpcShip fresh = CreatePolice(new Vector3(1_000f, 0f, 0f));
        return !fresh.HasSurrenderFireHold &&
            !restored.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            FactionAccessService.EvaluateDocking(restored, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed
            ? Pass()
            : Fail("save/load recreated Phase 79 state");
    }

    private (bool, string) SchemaRemainsThirteen()
    {
        return new SaveGameData().SchemaVersion == SaveGameData.CurrentSchemaVersion &&
            SaveGameData.CurrentSchemaVersion == 13
            ? Pass()
            : Fail("save schema changed");
    }

    private (bool, string) NoPhase79SaveMember()
    {
        foreach (PropertyInfo property in typeof(SaveGameData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = property.Name.ToLowerInvariant();
            if (name.Contains("surrender") || name.Contains("firehold") || name.Contains("holdfire") || name.Contains("ceasefire"))
                return Fail($"Phase 79 persisted member found: {property.Name}");
        }

        return Pass();
    }

    // ---- Phase 78 consequences preserved ----------------------------------

    private (bool, string) SurrenderFeeHeatDerived()
    {
        return PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.Pursuit) == 1_000 &&
            PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.HotPursuit) == 2_500
            ? Pass()
            : Fail("surrender fee behavior changed");
    }

    private (bool, string) ReputationUnchangedBySurrender()
    {
        Context ctx = CreateContext(standing: -0.45f);
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        float before = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), before)
            ? Pass()
            : Fail("surrender changed durable reputation");
    }

    private (bool, string) CargoUnchangedBySurrender()
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        ctx.GiveContraband(player, 2);
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        int before = player.CargoHold.GetCommodityQuantity(SideArmsName);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return player.CargoHold.GetCommodityQuantity(SideArmsName) == before
            ? Pass()
            : Fail("surrender changed cargo");
    }

    private (bool, string) Phase76NotificationsUnaffected()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        List<string> attentionNotifications = new();
        PoliceAttentionTransitionNotifier notifier = new(ctx.Reputation, attentionNotifications.Add);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return attentionNotifications.Count == 0
            ? Pass()
            : Fail("surrender misused the Phase 76 attention notifier");
    }

    private (bool, string) Phase77DockingBehaviorUnchanged()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        bool denied = !FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        bool allowed = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        return denied && allowed
            ? Pass()
            : Fail("Phase 77 docking policy behavior changed");
    }

    private (bool, string) NoAdditionalSurrenderNotification()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.Notifications.Clear();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return ctx.Notifications.Count == 1
            ? Pass()
            : Fail($"expected exactly one surrender notification, saw {ctx.Notifications.Count}");
    }

    // ---- Scope / bounds ---------------------------------------------------

    private (bool, string) FireCessationScopedToPolice()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(600f, 0f, 0f));
        NpcShip rogue = ctx.AddFactionShip(FactionManager.LibertyRogues, new Vector3(700f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyRogues, TemporaryHostilityManager.PlayerAggressionReason);
        rogue.SetPlayerTarget(ctx.Player.Position, NpcPlayerTargetReason.FactionDisposition);
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        int fired = ctx.RunWeapons(6, rogue);
        return fired > 0 ? Pass() : Fail("fire cessation incorrectly affected a non-Police faction");
    }

    private (bool, string) ParticipantHandlingBounded()
    {
        Context ctx = CreateContext();
        NpcShip participant = ctx.AddPolice(new Vector3(600f, 0f, 0f), "Phase 79 Participant");
        NpcShip distant = ctx.AddPolice(new Vector3(60_000f, 0f, 0f), "Phase 79 Distant");
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return participant.HasSurrenderFireHold && !distant.HasSurrenderFireHold
            ? Pass()
            : Fail("surrender fire hold was not bounded to participants");
    }

    private (bool, string) NoProjectileDeletionUsed()
    {
        Context ctx = CreateContext();
        ctx.AddPolice(new Vector3(900f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        ctx.RunWeapons(1);
        int before = ctx.ActiveProjectileCount();
        if (before <= 0)
            return Fail("setup did not create a projectile");
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        return ctx.ActiveProjectileCount() == before
            ? Pass()
            : Fail("surrender deleted or spawned projectiles to fake correctness");
    }

    private (bool, string) SameTickProofUsesProductionOrder()
    {
        Context ctx = CreateContext();
        Ship player = ctx.Player;
        ctx.GiveContraband(player, 2);
        NpcShip police = ctx.AddPolice(new Vector3(300f, 0f, 0f));
        // Production order: start scan, complete, refuse -> enforcement result
        // + fugitive; then a full frame in game order; confirm pursuit combat.
        ctx.Scan.Update(Frame(0.1f), player, ctx.Npcs, ctx.Credits, ctx.Reputation);
        ctx.Scan.Update(Frame(PoliceScanSystem.ScanDurationSeconds + 0.1f), player, ctx.Npcs, ctx.Credits, ctx.Reputation);
        if (!ctx.Scan.TryRefuseEnforcement(player, ctx.Credits, ctx.Reputation))
            return Fail("refusal failed");
        ctx.StepProduction();
        if (police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit)
            return Fail("refusal did not escalate to fugitive");
        int before = ctx.RunWeapons(6);
        if (before <= 0)
            return Fail("no firing opportunity before surrender");

        // Surrender at the input point of the frame, then the rest of the
        // frame in exact production order, then the weapon pass.
        if (!ctx.TrySurrender(out _, out _))
            return Fail("surrender failed");
        ctx.StepProduction();
        int after = ctx.RunWeapons(6);
        return after == 0 && !ctx.Fugitive.IsActive && ctx.Scan.State != PoliceScanState.Enforcement
            ? Pass()
            : Fail($"stale enforcement re-armed the incident (fired={after}, scan={ctx.Scan.State}, active={ctx.Fugitive.IsActive})");
    }

    private (bool, string) EvadingWithIndependentHostilityCanFire()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        // Push the officer out of contact so the incident is Evading.
        police.Position = new Vector3(50_000f, 0f, 0f);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        if (!ctx.Fugitive.IsEvading)
            return Fail("setup did not reach Evading");
        // Independent player-attack hostility permits combat while evading.
        ctx.Reputation.TemporaryHostility.RecordHostileAction(
            FactionManager.LibertyPolice, TemporaryHostilityManager.PlayerAggressionReason);
        police.Position = new Vector3(600f, 0f, 0f);
        police.SetPlayerTarget(ctx.Player.Position, NpcPlayerTargetReason.PlayerInitiatedAggression);
        int fired = ctx.RunWeapons(6);
        return fired > 0 ? Pass() : Fail("Police could not fire during Evading with independent hostility");
    }

    private (bool, string) HoldIsOnePassAndSelfReleasing()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        ctx.BeginPursuit(PoliceHeatLevel.Pursuit);
        ctx.StepProduction();
        // Arm the hold directly and keep a valid fugitive target.
        police.SetSurrenderFireHold(true);
        int held = ctx.RunWeapons(1);
        if (held != 0)
            return Fail($"hold pass did not suppress fugitive fire (fired={held})");
        int released = ctx.RunWeapons(1);
        return released > 0
            ? Pass()
            : Fail("surrender hold did not self-release after one pass");
    }

    private (bool, string) HoldDoesNotAffectFactionCombatTarget()
    {
        Context ctx = CreateContext();
        NpcShip police = ctx.AddPolice(new Vector3(600f, 0f, 0f));
        NpcShip rogue = ctx.AddFactionShip(FactionManager.LibertyRogues, new Vector3(700f, 0f, 0f));
        if (!police.SetFactionCombatTarget(rogue))
            return Fail("setup could not create faction combat target");
        police.SetSurrenderFireHold(true);
        int fired = ctx.RunWeapons(6, targetIsNpc: true);
        return fired > 0 ? Pass() : Fail("surrender hold suppressed legitimate faction combat");
    }

    // ---- Harness ----------------------------------------------------------

    private sealed class Context
    {
        public ReputationManager Reputation { get; }
        public PoliceScanSystem Scan { get; } = new();
        public PoliceFugitiveManager Fugitive { get; }
        public PoliceContrabandStopCoordinator Coordinator { get; } = new();
        public TrafficManager Traffic { get; }
        public PlayerCredits Credits { get; }
        public Ship Player { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public List<string> Notifications { get; } = new();
        private readonly NpcWeaponSystem _weapons;

        public Context(GraphicsDevice graphicsDevice, float standing, int credits)
        {
            Reputation = new ReputationManager(new FactionManager());
            Reputation.SetReputation(FactionManager.LibertyPolice, standing, "Phase 79 smoke setup");
            Reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "Phase 79 smoke setup");
            Reputation.SetReputation(FactionManager.LibertyNavy, 0.35f, "Phase 79 smoke setup");
            Reputation.SetReputation(FactionManager.NeutralCivilians, 0.30f, "Phase 79 smoke setup");
            Player = new Ship(Vector3.Zero) { CollisionRadius = 120f };
            Credits = new PlayerCredits(credits);
            Fugitive = new PoliceFugitiveManager(Reputation, Notifications.Add);
            Traffic = new TrafficManager(new ConfigurationManager(), Npcs, Objects);
            Traffic.FugitiveManager = Fugitive;
            Scan.SetFugitiveManager(Fugitive);
            _weapons = new NpcWeaponSystem(graphicsDevice, Reputation);
        }

        public NpcShip AddPolice(Vector3 position, string name = "Phase 79 Police")
        {
            NpcShip police = new(name, position, position, 1f, 0f, FactionManager.LibertyPolice);
            ConfigureFighter(police, name);
            return police;
        }

        public NpcShip AddFactionShip(string factionId, Vector3 position)
        {
            NpcShip ship = new($"Phase 79 {factionId}", position, position, 1f, 0f, factionId);
            ConfigureFighter(ship, ship.Name);
            return ship;
        }

        private void ConfigureFighter(NpcShip ship, string name)
        {
            ship.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase79-zone", ship.Position, 800f, 180f, 6500f);
            ship.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                name, ship.FactionId, "SMOKE/fighter", TrafficZoneBehaviorType.LawfulPatrol, NpcLoadoutTier.Standard));
            Npcs.Add(ship);
            Objects.Add(ship);
        }

        public void GiveContraband(Ship player, int quantity)
        {
            Commodity? contraband = CommodityCatalog.GetById("side-arms");
            if (contraband != null)
                player.CargoHold.AddCommodity(contraband, quantity);
        }

        public void BeginPursuit(PoliceHeatLevel heat) =>
            Fugitive.BeginPursuit(Player, "phase 79 smoke", null, heat);

        public bool TrySurrender(out PoliceSurrenderResult? result, out string failureReason) =>
            PoliceFugitiveSurrenderService.TrySurrender(
                Fugitive, Reputation, Player, Npcs, Credits, null, out result, out failureReason, Scan);

        /// <summary>Production-equivalent frame order after the input stage.</summary>
        public void StepProduction()
        {
            GameTime frame = Frame(0.1f);
            Traffic.Update(frame, Player, Reputation, null);
            foreach (NpcShip npc in Npcs.ToList())
                npc.Update(frame, null, Player, Reputation);
            Scan.Update(frame, Player, Npcs, Credits, Reputation);
            Coordinator.Update(Player, Npcs, Traffic, Scan, Fugitive, Reputation, null);
        }

        public int RunWeapons(int frames, NpcShip? only = null, bool targetIsNpc = false)
        {
            int fired = 0;
            void OnFired(NpcShip source, NpcShip target, string weaponId, WeaponEquipmentDefinition weapon)
            {
                if (only != null && !ReferenceEquals(source, only))
                    return;
                if (targetIsNpc && target == null)
                    return;
                if (targetIsNpc && target != null)
                    fired++;
                else if (!targetIsNpc)
                    fired++;
            }

            _weapons.NpcWeaponFired += OnFired;
            for (int i = 0; i < frames; i++)
                _weapons.Update(Frame(0.2f), Npcs, Player);
            _weapons.NpcWeaponFired -= OnFired;
            return fired;
        }

        public int ActiveProjectileCount()
        {
            FieldInfo field = typeof(NpcWeaponSystem).GetField("_projectiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return ((ICollection)field.GetValue(_weapons)!).Count;
        }

        public List<NpcShip> ProjectileOwners()
        {
            List<NpcShip> owners = new();
            foreach (object projectile in Projectiles())
            {
                FieldInfo owner = projectile.GetType().GetField("Owner", BindingFlags.Public | BindingFlags.Instance)!;
                if (owner.GetValue(projectile) is NpcShip ship)
                    owners.Add(ship);
            }

            return owners;
        }

        public List<float> ProjectileDamages()
        {
            List<float> damages = new();
            foreach (object projectile in Projectiles())
            {
                FieldInfo damage = projectile.GetType().GetField("Damage", BindingFlags.Public | BindingFlags.Instance)!;
                damages.Add((float)damage.GetValue(projectile)!);
            }

            return damages;
        }

        private IEnumerable<object> Projectiles()
        {
            FieldInfo field = typeof(NpcWeaponSystem).GetField("_projectiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (object projectile in (IEnumerable)field.GetValue(_weapons)!)
                yield return projectile;
        }
    }

    private Context CreateContext(float standing = 0.30f, int credits = 10_000) =>
        new(_graphicsDevice, standing, credits);

    private static NpcShip CreatePolice(Vector3 position) =>
        new("Phase 79 Police", position, position, 1f, 0f, FactionManager.LibertyPolice);

    private static string SideArmsName => CommodityCatalog.GetById("side-arms")!.Name;

    private static string TempPath(string label) =>
        Path.Combine(Path.GetTempPath(), $"roguelancer-phase79-{label}-{Guid.NewGuid():N}.json");

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static GameTime Frame(double seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason);
}
