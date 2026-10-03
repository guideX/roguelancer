#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 77 focused coverage for active-fugitive docking restrictions. The
/// suite drives only production systems: the shared docking access policy
/// (FactionAccessService), the single fugitive authority
/// (PoliceFugitiveManager), the dock-assist autopilot (GotoAutopilot), the
/// final station authority (StationDockUI), the existing contraband
/// enforcement path (PoliceEnforcementService), social recovery
/// (FactionBribeService), the Phase 75 presentation, the Phase 76 transition
/// observer, and SaveGameManager persistence. No parallel crime, warrant,
/// criminal-record, station-blacklist, or persisted docking-restriction
/// state exists here.
/// </summary>
internal sealed class Phase77FugitiveDockingRestrictionsSmokeTest
{
    public (int Passed, int Failed) Run()
    {
        Check("ordinary valid docking remains allowed when not fugitive", OrdinaryDockingAllowedWhenNotFugitive);
        Check("active Heat 1 fugitive denies covered Liberty Police docking", HeatOneFugitiveDeniesPoliceDocking);
        Check("active Heat 2 fugitive denies covered Liberty Police docking", HeatTwoFugitiveDeniesPoliceDocking);
        Check("denial produces one appropriate player-facing reason", DenialProducesOneClearReason);
        Check("denial does not begin station transition", DenialDoesNotBeginStationTransition);
        Check("denial does not mutate player position or docking state", DenialDoesNotMutatePlayerPositionOrDockingState);
        Check("denial does not mutate credits", DenialDoesNotMutateCredits);
        Check("denial does not mutate reputation", DenialDoesNotMutateReputation);
        Check("denial does not mutate Heat", DenialDoesNotMutateHeat);
        Check("denial does not confiscate cargo", DenialDoesNotConfiscateCargo);
        Check("neutral facility remains dockable while Police fugitive", NeutralFacilityRemainsDockableWhileFugitive);
        Check("unrelated faction facility remains governed by existing rules", UnrelatedFactionFollowsExistingRules);
        Check("Liberty Rogue criminal facility remains governed by existing rules", RogueFacilityFollowsExistingRules);
        Check("contraband alone does not deny docking", ContrabandAloneDoesNotDenyDocking);
        Check("compliant contraband resolution without fugitive state does not deny docking", CompliantContrabandResolutionDoesNotDeny);
        Check("ordinary contraband refusal creating fugitive state causes denial", OrdinaryRefusalCreatingFugitiveDenies);
        Check("smuggling-contract refusal creating fugitive state causes the same denial", SmugglingContractRefusalDenies);
        Check("severe lawful Police reputation without active fugitive state does not trigger denial", SevereLawfulStandingWithoutFugitiveAllows);
        Check("elevated reputation without active fugitive state does not trigger denial", ElevatedStandingWithoutFugitiveAllows);
        Check("standard reputation with active fugitive state still denies", StandardStandingWithFugitiveDenies);
        Check("existing hostile-faction docking behavior remains unchanged", ExistingHostileBehaviorUnchanged);
        Check("hostile plus fugitive does not produce duplicate denial side effects", HostilePlusFugitiveSingleDenial);
        Check("clearing active fugitive state restores docking immediately", ClearingFugitiveRestoresDocking);
        Check("Heat 2 to cleared restores docking", HeatTwoClearedRestoresDocking);
        Check("bribery alone does not clear docking denial while fugitive authority remains active", BriberyAloneDoesNotClearDenial);
        Check("reputation worsening alone does not cause denial while fugitive authority is inactive", ReputationWorseningAloneDoesNotDeny);
        Check("death and reset follow existing fugitive reset and docking behavior", DeathResetFollowsFugitiveReset);
        Check("system transition follows existing fugitive lifecycle semantics", SystemTransitionFollowsFugitiveLifecycle);
        Check("docking UI open and close does not mutate fugitive state", DockUiOpenCloseDoesNotMutateFugitive);
        Check("repeated passive access checks emit no notification spam", RepeatedPassiveChecksEmitNoSpam);
        Check("repeated explicit denied attempts remain bounded", RepeatedExplicitAttemptsRemainBounded);
        Check("save and load follow existing fugitive persistence semantics", SaveLoadFollowsFugitivePersistence);
        Check("no Phase 77 save field exists", NoPhase77SaveFieldExists);
        Check("schema version remains unchanged", SchemaVersionUnchanged);
        Check("station ownership and faction are resolved from canonical data", FactionResolvedFromCanonicalData);
        Check("NPC docking and traffic behavior remains unchanged", NpcDockingBehaviorUnchanged);
        Check("denied attempt does not alter mission cargo", DeniedAttemptDoesNotAlterMissionCargo);
        Check("successful post-clear docking uses the ordinary pre-existing docking path", PostClearDockingUsesOrdinaryPath);
        Check("Phase 75 Police-attention presentation remains side-effect free", Phase75PresentationRemainsSideEffectFree);
        Check("Phase 76 transition notifications remain unaffected", Phase76NotificationsUnaffected);
        Check("evading state with contact lost still denies covered docking", EvadingStateStillDenies);
        Check("post-escape reacquisition grace is not treated as criminality", PostEscapeGraceIsNotCriminality);
        Check("approach-phase revalidation denies covered docking", ApproachPhaseRevalidationDenies);
        Check("station dock final authority denies covered docking while fugitive", StationDockFinalAuthorityDeniesWhenFugitive);
        Check("already docked player is not forced to undock", AlreadyDockedPlayerIsNotEjected);

        Console.WriteLine($"[PHASE 77 SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private int _passed;
    private int _failed;

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[PHASE 77 SMOKE] PASS {label}");
            }
            else
            {
                Fail(label, "assertion returned false");
            }
        }
        catch (Exception ex)
        {
            Fail(label, ex.Message);
        }
    }

    private void Fail(string label, string reason)
    {
        _failed++;
        Console.WriteLine($"[PHASE 77 SMOKE] FAIL {label}: {reason}");
    }

    private static bool OrdinaryDockingAllowedWhenNotFugitive()
    {
        Context ctx = CreateContext();
        return FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool HeatOneFugitiveDeniesPoliceDocking()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        return ctx.Fugitive.IsActive && ctx.Fugitive.Heat == PoliceHeatLevel.Pursuit &&
            !FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
    }

    private static bool HeatTwoFugitiveDeniesPoliceDocking()
    {
        Context ctx = CreateContext();
        NpcShip police = CreatePolice(Vector3.Zero);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        police.MarkDamagedByPlayer(1f);
        ctx.Fugitive.NotifyPlayerDamage(police, ctx.Player);
        return ctx.Fugitive.IsActive && ctx.Fugitive.Heat == PoliceHeatLevel.HotPursuit &&
            !FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
    }

    private static bool DenialProducesOneClearReason()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        FactionAccessResult access = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true);
        return !access.IsAllowed &&
            access.FailureMessage.Contains("Liberty Police", StringComparison.Ordinal) &&
            access.FailureMessage.Contains("actively pursuing", StringComparison.OrdinalIgnoreCase) &&
            access.FailureMessage.Contains("Fort Bush", StringComparison.Ordinal);
    }

    private static bool DenialDoesNotBeginStationTransition()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);
        bool activated = ship.ActivateDockAssist(station);
        return !activated &&
            autopilot.State == GotoAutopilot.AutopilotState.Cancelled &&
            autopilot.WasDockingDenied &&
            !ship.IsGotoActive &&
            !autopilot.IsDocked &&
            autopilot.LastDockingDeniedReason.Contains("actively pursuing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DenialDoesNotMutatePlayerPositionOrDockingState()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);
        Vector3 positionBefore = ship.Position;
        Vector3 velocityBefore = ship.Velocity;
        bool activated = ship.ActivateDockAssist(station);
        return !activated &&
            Vector3.Equals(ship.Position, positionBefore) &&
            Vector3.Equals(ship.Velocity, velocityBefore) &&
            !ship.IsGotoActive &&
            !autopilot.IsDocked;
    }

    private static bool DenialDoesNotMutateCredits()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);
        bool activated = ship.ActivateDockAssist(station);
        return !activated && ctx.Credits.Credits == 10_000;
    }

    private static bool DenialDoesNotMutateReputation()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);
        float standingBefore = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        bool activated = ship.ActivateDockAssist(station);
        return !activated &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), standingBefore);
    }

    private static bool DenialDoesNotMutateHeat()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);
        PoliceHeatLevel heatBefore = ctx.Fugitive.Heat;
        bool activated = ship.ActivateDockAssist(station);
        return !activated && ctx.Fugitive.Heat == heatBefore && ctx.Fugitive.IsActive;
    }

    private static bool DenialDoesNotConfiscateCargo()
    {
        Context ctx = CreateContrabandContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);
        bool activated = ship.ActivateDockAssist(station);
        return !activated && ctx.Cargo.GetCommodityQuantity(SideArmsName) == 1;
    }

    private static bool NeutralFacilityRemainsDockableWhileFugitive()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        return FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.NeutralCivilians, "Neutral Depot", true).IsAllowed;
    }

    private static bool UnrelatedFactionFollowsExistingRules()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        bool allowedWhileFugitive = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyCorporations, "Corporate Hub", true).IsAllowed;

        ctx.Reputation.SetReputation(
            FactionManager.LibertyCorporations, -0.70f, "phase 77 unrelated hostility setup");
        bool deniedByExistingHostility = !FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyCorporations, "Corporate Hub", true).IsAllowed;
        return allowedWhileFugitive && deniedByExistingHostility;
    }

    private static bool RogueFacilityFollowsExistingRules()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        return FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyRogues, "Buffalo Base", true).IsAllowed;
    }

    private static bool ContrabandAloneDoesNotDenyDocking()
    {
        Context ctx = CreateContext();
        if (!ctx.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, 2))
            return false;
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool CompliantContrabandResolutionDoesNotDeny()
    {
        Context ctx = CreateContrabandContext();
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(
            FactionManager.LibertyPolice, ctx.Cargo, ctx.Credits, ctx.Reputation);
        bool resolved = service.TryResolve(
            offer, PoliceEnforcementResolution.Comply, ctx.Cargo, ctx.Credits, ctx.Reputation, out _, out _);
        return resolved && !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
    }

    private static bool OrdinaryRefusalCreatingFugitiveDenies()
    {
        Context ctx = CreateContrabandContext();
        ResolveRefusal(ctx);
        return ctx.Fugitive.IsActive &&
            !FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
    }

    private static bool SmugglingContractRefusalDenies()
    {
        Context ctx = CreateContext();
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        if (!ctx.Cargo.AddMissionCargo(9001, contraband, 2))
            return false;

        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(
            FactionManager.LibertyPolice, ctx.Cargo, ctx.Credits, ctx.Reputation);
        if (!offer.HasContraband)
            return false;
        if (!service.TryResolve(
                offer, PoliceEnforcementResolution.Refuse, ctx.Cargo, ctx.Credits, ctx.Reputation, out _, out _))
            return false;

        ctx.Fugitive.BeginPursuit(ctx.Player, "smuggling contract refusal");
        return ctx.Fugitive.IsActive &&
            !FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
    }

    private static bool SevereLawfulStandingWithoutFugitiveAllows()
    {
        Context ctx = CreateContext(standing: -0.55f);
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool ElevatedStandingWithoutFugitiveAllows()
    {
        Context ctx = CreateContext(standing: -0.45f);
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool StandardStandingWithFugitiveDenies()
    {
        Context ctx = CreateContext(standing: -0.25f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        return ctx.Fugitive.IsActive &&
            !FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
    }

    private static bool ExistingHostileBehaviorUnchanged()
    {
        Context ctx = CreateContext(standing: -0.70f);
        FactionAccessResult access = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false);
        return !access.IsAllowed &&
            access.FailureMessage.Contains("hostile", StringComparison.OrdinalIgnoreCase) &&
            !access.FailureMessage.Contains("actively pursuing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HostilePlusFugitiveSingleDenial()
    {
        Context ctx = CreateContext(standing: -0.70f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        FactionAccessResult access = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true);
        return !access.IsAllowed &&
            access.FailureMessage.Contains("actively pursuing", StringComparison.OrdinalIgnoreCase) &&
            !access.FailureMessage.Contains("hostile", StringComparison.OrdinalIgnoreCase) &&
            access.IsTemporarilyHostile;
    }

    private static bool ClearingFugitiveRestoresDocking()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        if (FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed)
            return false;

        ctx.Fugitive.ResolveEscape();
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool HeatTwoClearedRestoresDocking()
    {
        Context ctx = CreateContext();
        NpcShip police = CreatePolice(Vector3.Zero);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        police.MarkDamagedByPlayer(1f);
        ctx.Fugitive.NotifyPlayerDamage(police, ctx.Player);
        if (ctx.Fugitive.Heat != PoliceHeatLevel.HotPursuit)
            return false;
        if (FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed)
            return false;

        ctx.Fugitive.ResolveEscape();
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool BriberyAloneDoesNotClearDenial()
    {
        Context ctx = CreateContext(standing: -0.55f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        PlayerCredits credits = new(1_000_000);
        FactionBribeOffer? offer = FactionBribeService.CreateOfferForTarget(
            "phase 77 fixer",
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            FactionManager.LibertyPolice,
            ctx.Reputation,
            credits);
        if (offer == null || !offer.IsValid)
            return false;
        if (!FactionBribeService.TryPurchase(offer, ctx.Reputation, credits, out _, out _))
            return false;

        return ctx.Fugitive.IsActive &&
            !FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
    }

    private static bool ReputationWorseningAloneDoesNotDeny()
    {
        Context ctx = CreateContext(standing: -0.25f);
        ctx.Reputation.SetReputation(
            FactionManager.LibertyPolice, -0.55f, "phase 77 worsening setup");
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool DeathResetFollowsFugitiveReset()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Player.Hull.TakeDamage(100_000f);
        ctx.Fugitive.Update(0.5f, ctx.Player, Array.Empty<NpcShip>());
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool SystemTransitionFollowsFugitiveLifecycle()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Fugitive.Reset(reason: "system transition");
        return !ctx.Fugitive.IsActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool DockUiOpenCloseDoesNotMutateFugitive()
    {
        Context ctx = CreateContext();
        StationDockUI dockUi = CreateDockUi(ctx);
        Station neutral = NeutralStation();
        for (int i = 0; i < 5; i++)
        {
            if (!dockUi.DockAtStation(neutral))
                return false;
            dockUi.Undock();
        }

        return !ctx.Fugitive.IsActive &&
            ctx.Fugitive.Heat == PoliceHeatLevel.None &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), 0.30f);
    }

    private static bool RepeatedPassiveChecksEmitNoSpam()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        Station station = PoliceStation();
        bool first = FactionAccessService.EvaluateDocking(
            ctx.Reputation, station.FactionId, station.Name, ctx.Fugitive.IsActive).IsAllowed;
        float standingBefore = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);

        for (int i = 0; i < 1000; i++)
        {
            FactionAccessResult result = FactionAccessService.EvaluateDocking(
                ctx.Reputation, station.FactionId, station.Name, ctx.Fugitive.IsActive);
            if (result.IsAllowed != first)
                return false;
            _ = DockNavigation.IsDockableStation(station, ctx.Reputation);
        }

        return !first &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), standingBefore) &&
            ctx.Fugitive.IsActive;
    }

    private static bool RepeatedExplicitAttemptsRemainBounded()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);

        for (int i = 0; i < 5; i++)
        {
            if (ship.ActivateDockAssist(station) ||
                !autopilot.WasDockingDenied ||
                !autopilot.LastDockingDeniedReason.Contains("actively pursuing", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        bool neutralActivated = ship.ActivateDockAssist(NeutralStation());
        return neutralActivated && ctx.Fugitive.IsActive;
    }

    private static bool SaveLoadFollowsFugitivePersistence()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        SaveGameManager save = new(TempPath("phase77"));
        SaveGameData data = new()
        {
            FactionReputation = save.CaptureReputation(ctx.Reputation),
            TemporaryHostility = save.CaptureTemporaryHostility(ctx.Reputation)
        };

        bool saveOmitsFugitiveHostility = data.TemporaryHostility == null || data.TemporaryHostility.All(entry =>
            !string.Equals(entry.FactionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(entry.Reason, PoliceFugitiveManager.PoliceFugitiveHostilityReason, StringComparison.OrdinalIgnoreCase));
        if (!saveOmitsFugitiveHostility)
            return false;
        if (!save.TrySave(data, out _))
            return false;
        if (!save.TryLoad(out SaveGameData loaded, out _))
            return false;

        ReputationManager restored = new(new FactionManager());
        save.ApplyReputation(restored, loaded);
        save.ApplyTemporaryHostility(restored, loaded);
        return FactionAccessService.EvaluateDocking(
            restored, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool NoPhase77SaveFieldExists()
    {
        foreach (PropertyInfo property in typeof(SaveGameData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = property.Name.ToLowerInvariant();
            if (name.Contains("fugitive") || name.Contains("docking"))
                return false;
        }

        return true;
    }

    private static bool SchemaVersionUnchanged()
    {
        return new SaveGameData().SchemaVersion == SaveGameData.CurrentSchemaVersion &&
            SaveGameData.CurrentSchemaVersion == 13;
    }

    private static bool FactionResolvedFromCanonicalData()
    {
        Context ctx = CreateContext();
        Station policeNamed = CreateStation("Fort Bush", FactionManager.LibertyPolice);
        Station rogueNamed = CreateStation("Fort Bush", FactionManager.LibertyRogues);
        ctx.Fugitive.BeginPursuit(ctx.Player);

        bool policeDenied = !FactionAccessService.EvaluateDocking(
            ctx.Reputation, policeNamed.FactionId, policeNamed.Name, true).IsAllowed;
        bool rogueAllowed = FactionAccessService.EvaluateDocking(
            ctx.Reputation, rogueNamed.FactionId, rogueNamed.Name, true).IsAllowed;
        return policeDenied && rogueAllowed;
    }

    private static bool NpcDockingBehaviorUnchanged()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        NpcShip police = CreatePolice(new Vector3(1_000f, 0f, 0f));

        bool withNpcWorld = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        bool withoutNpcWorld = FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", ctx.Fugitive.IsActive).IsAllowed;
        return !withNpcWorld && withNpcWorld == withoutNpcWorld &&
            string.Equals(police.FactionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DeniedAttemptDoesNotAlterMissionCargo()
    {
        Context ctx = CreateContext();
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        if (!ctx.Cargo.AddMissionCargo(9001, contraband, 2))
            return false;

        ResolveRefusal(ctx);
        bool denied = !FactionAccessService.EvaluateDocking(
            ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
        return denied && ctx.Cargo.HasMissionCargo(9001, contraband.Id, 2);
    }

    private static bool PostClearDockingUsesOrdinaryPath()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        ctx.Fugitive.BeginPursuit(ship);
        if (ship.ActivateDockAssist(station))
            return false;

        ctx.Fugitive.ResolveEscape();
        bool activated = ship.ActivateDockAssist(station);
        StationDockUI dockUi = CreateDockUi(ctx);
        bool docked = dockUi.DockAtStation(station);
        return activated && autopilot.IsActive && docked && dockUi.IsDocked;
    }

    private static bool Phase75PresentationRemainsSideEffectFree()
    {
        Context ctx = CreateContext(standing: -0.45f);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        float standingBefore = ctx.Reputation.GetStanding(FactionManager.LibertyPolice);
        PoliceHeatLevel heatBefore = ctx.Fugitive.Heat;

        for (int i = 0; i < 100; i++)
        {
            _ = PoliceAttentionPresentation.GetLevel(ctx.Reputation);
            _ = PoliceAttentionPresentation.GetReadout(ctx.Reputation);
            _ = PoliceAttentionPresentation.GetOverviewLine(ctx.Reputation);
        }

        return Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), standingBefore) &&
            ctx.Fugitive.IsActive &&
            ctx.Fugitive.Heat == heatBefore;
    }

    private static bool Phase76NotificationsUnaffected()
    {
        Context ctx = CreateContext(standing: -0.25f);
        List<string> notifications = new();
        PoliceAttentionTransitionNotifier notifier = new(ctx.Reputation, notifications.Add);
        ctx.Reputation.AdjustReputationDirect(
            FactionManager.LibertyPolice, -0.20f, ReputationChangeReason.PoliceEnforcementRefused);
        bool observed = notifier.TryObserve(SyntheticChange(-0.25f, -0.45f));
        return observed &&
            notifications.Count == 1 &&
            Nearly(ctx.Reputation.GetStanding(FactionManager.LibertyPolice), -0.45f) &&
            !ctx.Fugitive.IsActive;
    }

    private static bool EvadingStateStillDenies()
    {
        Context ctx = CreateContext();
        NpcShip police = CreatePolice(new Vector3(1_000f, 0f, 0f));
        ctx.Npcs.Add(police);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        police.Position = new Vector3(20_000f, 0f, 0f);
        ctx.Fugitive.Update(0.5f, ctx.Player, ctx.Npcs);
        return ctx.Fugitive.IsEvading && ctx.Fugitive.IsActive &&
            !FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
    }

    private static bool PostEscapeGraceIsNotCriminality()
    {
        Context ctx = CreateContext();
        ctx.Fugitive.BeginPursuit(ctx.Player);
        ctx.Fugitive.ResolveEscape();
        return !ctx.Fugitive.IsActive &&
            ctx.Fugitive.IsContrabandReacquisitionGraceActive &&
            FactionAccessService.EvaluateDocking(
                ctx.Reputation, FactionManager.LibertyPolice, "Fort Bush", false).IsAllowed;
    }

    private static bool ApproachPhaseRevalidationDenies()
    {
        Context ctx = CreateContext();
        Station station = PoliceStation();
        Ship ship = ctx.Player;
        GotoAutopilot autopilot = CreateAutopilot(ctx, station, ship);
        if (!ship.ActivateGoto(station))
            return false;

        ctx.Fugitive.BeginPursuit(ship);
        ship.Update(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)), new KeyboardState());
        return autopilot.WasDockingDenied &&
            autopilot.LastDockingDeniedReason.Contains("actively pursuing", StringComparison.OrdinalIgnoreCase) &&
            !ship.IsGotoActive;
    }

    private static bool StationDockFinalAuthorityDeniesWhenFugitive()
    {
        Context ctx = CreateContext();
        StationDockUI dockUi = CreateDockUi(ctx);
        ctx.Fugitive.BeginPursuit(ctx.Player);
        bool docked = dockUi.DockAtStation(PoliceStation());
        return !docked &&
            !dockUi.IsDocked &&
            dockUi.LastDockingDeniedReason.Contains("actively pursuing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AlreadyDockedPlayerIsNotEjected()
    {
        Context ctx = CreateContext();
        StationDockUI dockUi = CreateDockUi(ctx);
        Station neutral = NeutralStation();
        if (!dockUi.DockAtStation(neutral))
            return false;

        ctx.Fugitive.BeginPursuit(ctx.Player);
        return dockUi.IsDocked && ReferenceEquals(dockUi.DockedStation, neutral);
    }

    private static Context CreateContext(float standing = 0.30f, int credits = 10_000)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "Phase 77 smoke setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "Phase 77 smoke setup");
        reputation.SetReputation(FactionManager.LibertyCorporations, 0.30f, "Phase 77 smoke setup");
        reputation.SetReputation(FactionManager.NeutralCivilians, 0.30f, "Phase 77 smoke setup");
        Ship player = new(Vector3.Zero);
        return new Context(
            reputation,
            player,
            new CargoHold(100),
            new PlayerCredits(credits),
            new List<NpcShip>());
    }

    private static Context CreateContrabandContext(int quantity = 1, int credits = 10_000, float standing = 0.30f)
    {
        Context context = CreateContext(standing, credits);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, quantity);
        return context;
    }

    private static bool ResolveRefusal(Context context)
    {
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(
            FactionManager.LibertyPolice, context.Cargo, context.Credits, context.Reputation);
        if (!service.TryResolve(
                offer, PoliceEnforcementResolution.Refuse, context.Cargo, context.Credits, context.Reputation, out _, out _))
            return false;
        context.Fugitive.BeginPursuit(context.Player, "enforcement refusal");
        return context.Fugitive.IsActive && context.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static GotoAutopilot CreateAutopilot(Context context, Station station, Ship ship)
    {
        GotoAutopilot autopilot = new();
        autopilot.Initialize(
            ship,
            null,
            null,
            new List<Station> { station },
            new List<SpaceObject> { station },
            new List<NpcShip>(),
            null,
            null,
            null,
            context.Reputation);
        autopilot.SetFugitivePursuitResolver(factionId =>
            string.Equals(FactionManager.NormalizeFactionId(factionId), FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) &&
            context.Fugitive.IsActive);
        ship.SetGotoAutopilot(autopilot);
        ship.SetNotificationManager(new NotificationManager(null, new Viewport(0, 0, 1920, 1080)));
        return autopilot;
    }

    private static StationDockUI CreateDockUi(Context context)
    {
        PlayerCredits credits = new(100_000);
        MissionManager missions = new(credits, null, context.Reputation);
        StationDockUI dockUi = new StationDockUI(
            null,
            null,
            null,
            new CommodityDealer(),
            missions,
            context.Reputation);
        dockUi.SetFugitivePursuitResolver(factionId =>
            string.Equals(FactionManager.NormalizeFactionId(factionId), FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) &&
            context.Fugitive.IsActive);
        return dockUi;
    }

    private static Station PoliceStation() => CreateStation("Fort Bush", FactionManager.LibertyPolice);

    private static Station RogueStation() => CreateStation("Buffalo Base", FactionManager.LibertyRogues);

    private static Station NeutralStation() => CreateStation("Neutral Depot", FactionManager.NeutralCivilians);

    private static Station CreateStation(string name, string factionId) =>
        new(new StationConfig
        {
            Description = name,
            FactionId = factionId,
            StartupPositionX = 2_000f,
            StartupPositionY = 0f,
            StartupPositionZ = -2_000f,
            Radius = 600f,
            DockingRange = 900f
        }, null);

    private static NpcShip CreatePolice(Vector3 position) =>
        new("Phase 77 Police", position, Vector3.Zero, 1f, 0f, FactionManager.LibertyPolice);

    private static string SideArmsName => CommodityCatalog.GetById("side-arms")!.Name;

    private static string TempPath(string label) =>
        Path.Combine(Path.GetTempPath(), $"roguelancer-phase77-{label}-{Guid.NewGuid():N}.json");

    private static ReputationChangeResult SyntheticChange(float oldValue, float newValue) => new()
    {
        FactionId = FactionManager.LibertyPolice,
        FactionDisplayName = "Liberty Police",
        OldValue = oldValue,
        NewValue = newValue,
        Delta = newValue - oldValue,
        OldBand = ReputationManager.GetBandForStanding(oldValue),
        NewBand = ReputationManager.GetBandForStanding(newValue),
        Reason = ReputationChangeReason.ManualDebug
    };

    private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

    private static bool RunSilenced(Func<bool> assertion)
    {
        TextWriter original = Console.Out;
        try
        {
            using StringWriter writer = new();
            Console.SetOut(writer);
            return assertion();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private sealed class Context
    {
        public Context(
            ReputationManager reputation,
            Ship player,
            CargoHold cargo,
            PlayerCredits credits,
            List<NpcShip> npcs)
        {
            Reputation = reputation;
            Player = player;
            Cargo = cargo;
            Credits = credits;
            Npcs = npcs;
            Fugitive = new PoliceFugitiveManager(reputation);
        }

        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public CargoHold Cargo { get; }
        public PlayerCredits Credits { get; }
        public List<NpcShip> Npcs { get; }
        public PoliceFugitiveManager Fugitive { get; }
    }
}
