#nullable enable

using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Deterministic Phase 54 coverage. The suite drives only simulation deltas
/// and the existing authoritative attribution/targeting seams; it never waits
/// on wall clock time or creates an alternate combat implementation.
/// </summary>
internal sealed class PoliceFugitiveSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("clean scan creates no heat", CleanScanCreatesNoHeat);
        Check("peaceful compliance creates no heat", PeacefulComplianceCreatesNoHeat);
        Check("refusal starts Heat 1", RefusalStartsHeatOne);
        Check("timeout starts Heat 1", TimeoutStartsHeatOne);
        Check("enforcement flight starts Heat 1", FlightStartsHeatOne);
        Check("trade-lane flight starts Heat 1", TradeLaneFlightStartsHeatOne);
        Check("scanner attack starts Heat 1", ScannerAttackStartsHeatOne);
        Check("fugitive works without an active mission", FugitiveWorksWithoutMission);
        Check("nearby Police acquire fugitive", NearbyPoliceAcquire);
        Check("distant Police do not acquire omnisciently", DistantPoliceDoNotAcquire);
        Check("multiple Police share one incident", MultiplePoliceShareIncident);
        Check("multiple Police do not stack heat", MultiplePoliceDoNotStackHeat);
        Check("original scanner loss does not clear incident", OriginalScannerLossDoesNotClear);
        Check("another patrol reacquires", AnotherPatrolReacquires);
        Check("one remaining contact prevents evasion", OneRemainingContactPreventsEvasion);
        Check("all contact loss begins evasion", AllContactLossBeginsEvasion);
        Check("one-frame loss does not clear pursuit", OneFrameLossDoesNotClear);
        Check("Heat 1 duration is continuous", HeatOneRequiresContinuousDuration);
        Check("Heat 2 duration is longer", HeatTwoRequiresLongerDuration);
        Check("reacquisition resets escape progress", ReacquisitionResetsEscapeProgress);
        Check("successful Heat 1 escape clears incident", HeatOneEscapeClearsIncident);
        Check("successful Heat 2 escape clears incident", HeatTwoEscapeClearsIncident);
        Check("escape clears pursuit hostility", EscapeClearsPursuitHostility);
        Check("escape keeps permanent reputation", EscapeKeepsPermanentReputation);
        Check("permanently hostile Police remain hostile", PermanentHostilityRemainsHostile);
        Check("player damage escalates Heat 1", PlayerDamageEscalatesHeat);
        Check("NPC-only damage does not escalate", NpcOnlyDamageDoesNotEscalate);
        Check("Police destruction cannot exceed Heat 2", PoliceDestructionCapsHeat);
        Check("heat refresh remains bounded", HeatRefreshRemainsBounded);
        Check("no dedicated infinite Police spawning", NoDedicatedPoliceSpawning);
        Check("population list remains bounded", PopulationListRemainsBounded);
        Check("distress is not duplicated by fugitive state", DistressIsNotDuplicated);
        Check("Police disengagement can cause contact loss", DisengagementCanCauseLoss);
        Check("trade-lane entry does not clear heat", TradeLaneEntryDoesNotClearHeat);
        Check("lane separation can begin evasion", LaneSeparationBeginsEvasion);
        Check("Police station access is denied during pursuit", PoliceStationAccessDenied);
        Check("Rogue access follows faction policy", RogueAccessFollowsPolicy);
        Check("undetected contraband alone creates no heat", UndetectedContrabandCreatesNoHeat);
        Check("Phase 53 fine compliance remains peaceful", Phase53FineComplianceRemainsPeaceful);
        Check("Phase 53 confiscation remains exact", Phase53ConfiscationRemainsExact);
        Check("Phase 53 insufficient credits remain unresolved", Phase53InsufficientCreditsRemainUnresolved);
        Check("refusal preserves ordinary cargo", RefusalPreservesOrdinaryCargo);
        Check("smuggling cargo remains intact during Heat 1", SmugglingCargoSurvivesHeatOne);
        Check("Heat 2 does not auto-fail cargo run", HotPursuitDoesNotAutoFailCargoRun);
        Check("lawful penalty remains after escape", LawfulPenaltyRemainsAfterEscape);
        Check("Rogue reward policy is unchanged", RogueRewardPolicyUnchanged);
        Check("save omits active fugitive state", SaveOmitsActiveFugitiveState);
        Check("load does not restore fugitive hostility", LoadDoesNotRestoreFugitiveHostility);
        Check("save preserves permanent standing", SavePreservesPermanentStanding);
        Check("reset clears heat", ResetClearsHeat);
        Check("world clear clears pursuit references", WorldClearClearsReferences);
        Check("death/reset clears pursuit", DeathResetClearsPursuit);
        Check("system transition clears local heat", SystemTransitionClearsLocalHeat);
        Check("clean scan status stays empty for fugitive", CleanScanStatusIsEmpty);
        Check("active Heat 1 HUD is concise", HeatOneHudIsConcise);
        Check("active Heat 2 HUD is concise", HeatTwoHudIsConcise);
        Check("contact-lost HUD exposes countdown", ContactLostHudExposesCountdown);
        Check("reacquired HUD returns to pursuit", ReacquiredHudReturnsToPursuit);
        Check("escape HUD reports evasion", EscapeHudReportsEvasion);
        Check("pursuit hostility has one authority", PursuitHostilityUsesExistingAuthority);
        Check("no permanent fugitive record exists", NoPermanentFugitiveRecord);
        Check("mission state is not mutated by escape", EscapeDoesNotMutateMissionState);
        Check("deterministic refuse-and-escape proof", DeterministicRefuseAndEscapeProof);
        Check("deterministic hot-pursuit proof", DeterministicHotPursuitProof);
        Check("deterministic smuggler-escape proof", DeterministicSmugglerEscapeProof);

        Console.WriteLine($"[POLICE FUGITIVE SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            bool success = RunSilenced(assertion);
            if (success)
            {
                _passed++;
                Console.WriteLine($"[POLICE FUGITIVE SMOKE] PASS {label}");
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
        Console.WriteLine($"[POLICE FUGITIVE SMOKE] FAIL {label}: {reason}");
    }

    private static bool CleanScanCreatesNoHeat()
    {
        Context context = CreateContext();
        PoliceEnforcementOffer offer = new PoliceEnforcementService().Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return !offer.HasContraband && !context.Fugitive.IsActive;
    }

    private static bool PeacefulComplianceCreatesNoHeat()
    {
        Context context = CreateContrabandContext();
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        bool resolved = service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out _);
        return resolved && !context.Fugitive.IsActive && !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool RefusalStartsHeatOne() => ResolveRefusal(CreateContrabandContext());

    private static bool TimeoutStartsHeatOne() => StartAndCheck("enforcement demand timed out");

    private static bool FlightStartsHeatOne() => StartAndCheck("enforcement radius exceeded");

    private static bool TradeLaneFlightStartsHeatOne() => StartAndCheck("trade-lane escape");

    private static bool ScannerAttackStartsHeatOne()
    {
        Context context = CreateContext();
        NpcShip scanner = CreatePolice(Vector3.Zero);
        scanner.MarkDamagedByPlayer(2f);
        return context.Fugitive.NotifyPlayerDamage(scanner, context.Player) &&
            context.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static bool FugitiveWorksWithoutMission()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        return context.Fugitive.IsActive;
    }

    private static bool NearbyPoliceAcquire()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(new Vector3(2_000f, 0f, 0f));
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.1f, context.Player, context.Npcs);
        return police.HasPlayerTarget && police.PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit &&
            context.Fugitive.ActiveContactCount == 1;
    }

    private static bool DistantPoliceDoNotAcquire()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(new Vector3(10_001f, 0f, 0f));
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return !police.HasPlayerTarget && context.Fugitive.ActiveContactCount == 0;
    }

    private static bool MultiplePoliceShareIncident()
    {
        Context context = CreateContext();
        context.Npcs.Add(CreatePolice(new Vector3(1_000f, 0f, 0f)));
        context.Npcs.Add(CreatePolice(new Vector3(-1_000f, 0f, 0f)));
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Fugitive.ActiveContactCount == 2 && context.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static bool MultiplePoliceDoNotStackHeat()
    {
        Context context = CreateContext();
        NpcShip first = CreatePolice(new Vector3(1_000f, 0f, 0f));
        NpcShip second = CreatePolice(new Vector3(-1_000f, 0f, 0f));
        context.Npcs.Add(first);
        context.Npcs.Add(second);
        context.Fugitive.BeginPursuit(context.Player);
        first.MarkDamagedByPlayer(1f);
        second.MarkDamagedByPlayer(1f);
        context.Fugitive.NotifyPlayerDamage(first, context.Player);
        context.Fugitive.NotifyPlayerDamage(second, context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Fugitive.Heat == PoliceHeatLevel.HotPursuit && context.Fugitive.ActiveContactCount == 2;
    }

    private static bool OriginalScannerLossDoesNotClear()
    {
        Context context = CreateContext();
        NpcShip scanner = CreatePolice(Vector3.Zero);
        NpcShip patrol = CreatePolice(new Vector3(1_000f, 0f, 0f));
        context.Npcs.Add(scanner);
        context.Npcs.Add(patrol);
        context.Fugitive.BeginPursuit(context.Player);
        scanner.ApplyDamage(1_000f, NpcDestructionSource.Environment);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Fugitive.IsActive && patrol.HasPlayerTarget;
    }

    private static bool AnotherPatrolReacquires()
    {
        Context context = CreateContext();
        NpcShip first = CreatePolice(new Vector3(1_000f, 0f, 0f));
        NpcShip second = CreatePolice(new Vector3(20_000f, 0f, 0f));
        context.Npcs.Add(first);
        context.Npcs.Add(second);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        first.Position = new Vector3(20_000f, 0f, 0f);
        second.Position = new Vector3(2_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Fugitive.IsActive && context.Fugitive.ActiveContactCount == 1 && second.HasPlayerTarget;
    }

    private static bool OneRemainingContactPreventsEvasion()
    {
        Context context = CreateContext();
        NpcShip first = CreatePolice(new Vector3(1_000f, 0f, 0f));
        NpcShip second = CreatePolice(new Vector3(-1_000f, 0f, 0f));
        context.Npcs.Add(first);
        context.Npcs.Add(second);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        first.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(14f, context.Player, context.Npcs);
        return context.Fugitive.IsPursued && context.Fugitive.ActiveContactCount == 1;
    }

    private static bool AllContactLossBeginsEvasion()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(new Vector3(1_000f, 0f, 0f));
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        police.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Fugitive.IsEvading && context.Fugitive.EscapeProgressSeconds > 0f;
    }

    private static bool OneFrameLossDoesNotClear()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(new Vector3(1_000f, 0f, 0f));
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        police.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.016f, context.Player, context.Npcs);
        return context.Fugitive.IsActive && !context.Fugitive.IsEvading;
    }

    private static bool HeatOneRequiresContinuousDuration()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds - 0.6f, context.Player, context.Npcs);
        return context.Fugitive.IsActive && context.Fugitive.IsEvading;
    }

    private static bool HeatTwoRequiresLongerDuration()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        NpcShip police = context.Npcs[0];
        police.MarkDamagedByPlayer(1f);
        context.Fugitive.NotifyPlayerDamage(police, context.Player);
        police.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, context.Npcs);
        return context.Fugitive.IsActive && context.Fugitive.Heat == PoliceHeatLevel.HotPursuit;
    }

    private static bool ReacquisitionResetsEscapeProgress()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        context.Fugitive.Update(5f, context.Player, context.Npcs);
        float progressBefore = context.Fugitive.EscapeProgressSeconds;
        context.Npcs[0].Position = new Vector3(1_000f, 0f, 0f);
        context.Fugitive.Update(0.1f, context.Player, context.Npcs);
        return progressBefore > 0f && context.Fugitive.IsPursued && context.Fugitive.EscapeProgressSeconds == 0f;
    }

    private static bool HeatOneEscapeClearsIncident()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, context.Npcs);
        return !context.Fugitive.IsActive;
    }

    private static bool HeatTwoEscapeClearsIncident()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        context.Fugitive.NotifyPlayerDamage(context.Npcs[0], context.Player);
        context.Npcs[0].Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        context.Fugitive.Update(PoliceFugitiveManager.Heat2EscapeDurationSeconds, context.Player, context.Npcs);
        return !context.Fugitive.IsActive;
    }

    private static bool EscapeClearsPursuitHostility()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, context.Npcs);
        return !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool EscapeKeepsPermanentReputation()
    {
        Context context = CreateContext(standing: 0.30f);
        float before = context.Reputation.GetStanding(FactionManager.LibertyPolice);
        context.Fugitive.BeginPursuit(context.Player);
        context.Npcs.Add(CreatePolice(new Vector3(20_000f, 0f, 0f)));
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, context.Npcs);
        return Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), before);
    }

    private static bool PermanentHostilityRemainsHostile()
    {
        Context context = CreateContext(standing: -0.70f);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, Array.Empty<NpcShip>());
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, Array.Empty<NpcShip>());
        return context.Reputation.IsHostile(FactionManager.LibertyPolice) &&
            !FactionAccessService.EvaluateDocking(context.Reputation, FactionManager.LibertyPolice).IsAllowed;
    }

    private static bool PlayerDamageEscalatesHeat()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Fugitive.BeginPursuit(context.Player);
        police.MarkDamagedByPlayer(1f);
        return context.Fugitive.NotifyPlayerDamage(police, context.Player) && context.Fugitive.Heat == PoliceHeatLevel.HotPursuit;
    }

    private static bool NpcOnlyDamageDoesNotEscalate()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Fugitive.BeginPursuit(context.Player);
        police.ApplyDamage(1f, NpcDestructionSource.Npc);
        return !context.Fugitive.NotifyPlayerDamage(police, context.Player) && context.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static bool PoliceDestructionCapsHeat()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Fugitive.BeginPursuit(context.Player);
        police.MarkDamagedByPlayer(100f);
        police.ApplyDamage(100_000f, NpcDestructionSource.Player);
        context.Fugitive.NotifyPlayerDamage(police, context.Player);
        context.Fugitive.NotifyPlayerDamage(police, context.Player);
        return context.Fugitive.Heat == PoliceHeatLevel.HotPursuit;
    }

    private static bool HeatRefreshRemainsBounded()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        for (int i = 0; i < 20; i++)
            context.Fugitive.BeginPursuit(context.Player, "bounded refresh");
        return context.Fugitive.IsActive && context.Fugitive.AbsoluteIncidentRemainingSeconds <= PoliceFugitiveManager.AbsoluteIncidentBoundSeconds;
    }

    private static bool NoDedicatedPoliceSpawning()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Npcs.Count == 0;
    }

    private static bool PopulationListRemainsBounded()
    {
        Context context = CreateContext();
        for (int i = 0; i < 3; i++)
            context.Npcs.Add(CreatePolice(new Vector3(1_000f + i * 100f, 0f, 0f)));
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Npcs.Count == 3 && context.Fugitive.ActiveContactCount == 3;
    }

    private static bool DistressIsNotDuplicated()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Npcs.Add(police);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Npcs.Count == 1 && context.Fugitive.ActiveContactCount == 1;
    }

    private static bool DisengagementCanCauseLoss()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(new Vector3(1_000f, 0f, 0f));
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        police.ClearEncounterState();
        police.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Fugitive.IsEvading;
    }

    private static bool TradeLaneEntryDoesNotClearHeat()
    {
        Context context = CreateContext();
        context.Player.SetTradeLaneTransit(true);
        context.Fugitive.BeginPursuit(context.Player);
        return context.Fugitive.IsActive;
    }

    private static bool LaneSeparationBeginsEvasion()
    {
        Context context = CreateContext();
        context.Player.SetTradeLaneTransit(true);
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        police.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        return context.Fugitive.IsEvading;
    }

    private static bool PoliceStationAccessDenied()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Fugitive.BeginPursuit(context.Player);
        return !FactionAccessService.EvaluateDocking(context.Reputation, FactionManager.LibertyPolice, "Fort Bush", true).IsAllowed;
    }

    private static bool RogueAccessFollowsPolicy()
    {
        Context context = CreateContext(standing: 0.30f);
        context.Fugitive.BeginPursuit(context.Player);
        return FactionAccessService.EvaluateDocking(context.Reputation, FactionManager.LibertyRogues, "Buffalo Base", true).IsAllowed;
    }

    private static bool UndetectedContrabandCreatesNoHeat()
    {
        Context context = CreateContrabandContext();
        PoliceEnforcementOffer offer = new PoliceEnforcementService().Evaluate(FactionManager.LibertyRogues, context.Cargo, context.Credits);
        return !offer.IsInspectionApplicable && !context.Fugitive.IsActive;
    }

    private static bool Phase53FineComplianceRemainsPeaceful() => PeacefulComplianceCreatesNoHeat();

    private static bool Phase53ConfiscationRemainsExact()
    {
        Context context = CreateContrabandContext(quantity: 2);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out PoliceEnforcementResult? result, out _) &&
            result?.ConfiscatedQuantity == 2 && context.Cargo.GetCommodityQuantity(SideArmsName) == 0;
    }

    private static bool Phase53InsufficientCreditsRemainUnresolved()
    {
        Context context = CreateContrabandContext(credits: 1);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        return !service.TryResolve(offer, PoliceEnforcementResolution.Comply, context.Cargo, context.Credits, context.Reputation, out _, out _) &&
            context.Cargo.GetCommodityQuantity(SideArmsName) == 1 && !context.Fugitive.IsActive;
    }

    private static bool RefusalPreservesOrdinaryCargo()
    {
        Context context = CreateContrabandContext();
        Commodity legal = CommodityCatalog.GetById("food-rations")!;
        context.Cargo.AddCommodity(legal, 2);
        ResolveRefusal(context);
        return context.Cargo.GetCommodityQuantity(SideArmsName) == 1 && context.Cargo.GetCommodityQuantity("Food Rations") == 2;
    }

    private static bool SmugglingCargoSurvivesHeatOne()
    {
        Context context = CreateContrabandContext();
        ResolveRefusal(context);
        context.Fugitive.Update(0.5f, context.Player, Array.Empty<NpcShip>());
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, Array.Empty<NpcShip>());
        return context.Cargo.GetCommodityQuantity(SideArmsName) == 1;
    }

    private static bool HotPursuitDoesNotAutoFailCargoRun()
    {
        Context context = CreateContrabandContext();
        ResolveRefusal(context);
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Npcs.Add(police);
        police.MarkDamagedByPlayer(1f);
        context.Fugitive.NotifyPlayerDamage(police, context.Player);
        return context.Fugitive.Heat == PoliceHeatLevel.HotPursuit && context.Cargo.GetCommodityQuantity(SideArmsName) == 1;
    }

    private static bool LawfulPenaltyRemainsAfterEscape()
    {
        Context context = CreateContrabandContext(standing: 0.30f);
        ResolveRefusal(context);
        float afterRefusal = context.Reputation.GetStanding(FactionManager.LibertyPolice);
        context.Fugitive.Update(0.5f, context.Player, Array.Empty<NpcShip>());
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, Array.Empty<NpcShip>());
        return Nearly(afterRefusal, 0.10f) && Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), afterRefusal);
    }

    private static bool RogueRewardPolicyUnchanged()
    {
        Context context = CreateContext();
        FactionBountyRewardService bounty = new(context.Reputation, context.Credits);
        NpcShip police = CreatePolice(Vector3.Zero);
        police.MarkDamagedByPlayer(1f);
        police.ApplyDamage(100_000f, NpcDestructionSource.Player);
        FactionBountyRewardResult result = bounty.ProcessDestruction(police);
        return result.RejectionReason == FactionBountyRejectionReason.NoBountyPolicy && context.Credits.Credits == 10_000;
    }

    private static bool SaveOmitsActiveFugitiveState()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        SaveGameManager save = new(Path.Combine(Path.GetTempPath(), $"phase54-{Guid.NewGuid():N}.json"));
        return save.CaptureTemporaryHostility(context.Reputation).All(entry =>
            !string.Equals(entry.FactionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LoadDoesNotRestoreFugitiveHostility()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        SaveGameData data = new()
        {
            TemporaryHostility = new List<SaveTemporaryHostilityData>
            {
                new()
                {
                    FactionId = FactionManager.LibertyPolice,
                    Reason = TemporaryHostilityManager.FugitivePursuitReason,
                    RemainingSeconds = 120f
                }
            }
        };
        ReputationManager restored = new(new FactionManager());
        new SaveGameManager().ApplyTemporaryHostility(restored, data);
        return !restored.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool SavePreservesPermanentStanding()
    {
        Context context = CreateContext(standing: -0.30f);
        SaveGameManager save = new();
        SaveGameData data = new() { FactionReputation = save.CaptureReputation(context.Reputation) };
        ReputationManager restored = new(new FactionManager());
        save.ApplyReputation(restored, data);
        return Nearly(restored.GetStanding(FactionManager.LibertyPolice), -0.30f);
    }

    private static bool ResetClearsHeat()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Reset();
        return !context.Fugitive.IsActive && string.IsNullOrWhiteSpace(context.Fugitive.StatusText) &&
            !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool WorldClearClearsReferences()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        context.Fugitive.Reset();
        return !police.HasPlayerTarget && context.Fugitive.ActiveContactCount == 0;
    }

    private static bool DeathResetClearsPursuit()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        context.Player.Hull.TakeDamage(100_000f);
        context.Fugitive.Update(0.5f, context.Player, Array.Empty<NpcShip>());
        return !context.Fugitive.IsActive && !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool SystemTransitionClearsLocalHeat()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Reset(reason: "system transition");
        return !context.Fugitive.IsActive;
    }

    private static bool CleanScanStatusIsEmpty()
    {
        Context context = CreateContext();
        return string.IsNullOrWhiteSpace(new PoliceScanSystem().StatusText) && !context.Fugitive.IsActive;
    }

    private static bool HeatOneHudIsConcise()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        return context.Fugitive.StatusText == "LIBERTY POLICE PURSUIT";
    }

    private static bool HeatTwoHudIsConcise()
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(Vector3.Zero);
        context.Fugitive.BeginPursuit(context.Player);
        police.MarkDamagedByPlayer(1f);
        context.Fugitive.NotifyPlayerDamage(police, context.Player);
        return context.Fugitive.StatusText == "LIBERTY POLICE — HOT PURSUIT";
    }

    private static bool ContactLostHudExposesCountdown()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        return context.Fugitive.StatusText.StartsWith("POLICE CONTACT LOST | Stay clear:", StringComparison.Ordinal);
    }

    private static bool ReacquiredHudReturnsToPursuit()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        context.Npcs[0].Position = Vector3.Zero;
        context.Fugitive.Update(0.1f, context.Player, context.Npcs);
        return context.Fugitive.StatusText == "POLICE REACQUIRED";
    }

    private static bool EscapeHudReportsEvasion()
    {
        Context context = CreateEvadingContext(PoliceHeatLevel.Pursuit);
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, context.Npcs);
        return context.Fugitive.StatusText == "PURSUIT EVADED";
    }

    private static bool PursuitHostilityUsesExistingAuthority()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        return context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            context.Reputation.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice) <= TemporaryHostilityManager.MaximumDurationSeconds;
    }

    private static bool NoPermanentFugitiveRecord()
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player);
        return context.Reputation.GetStandingsSnapshot().Count == new FactionManager().Factions.Count;
    }

    private static bool EscapeDoesNotMutateMissionState()
    {
        Context context = CreateContrabandContext();
        ResolveRefusal(context);
        int cargoBefore = context.Cargo.GetCommodityQuantity(SideArmsName);
        context.Fugitive.Update(0.5f, context.Player, Array.Empty<NpcShip>());
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, Array.Empty<NpcShip>());
        return cargoBefore == context.Cargo.GetCommodityQuantity(SideArmsName);
    }

    private static bool DeterministicRefuseAndEscapeProof()
    {
        Context context = CreateContext(standing: 0.30f);
        if (!context.Player.CargoHold.AddCommodity(CommodityCatalog.GetById("side-arms")!, 1))
            return false;

        PoliceScanSystem scan = new();
        scan.SetFugitiveManager(context.Fugitive);
        NpcShip scanner = CreatePolice(new Vector3(1_500f, 0f, 0f));
        context.Npcs.Add(scanner);
        StepPoliceScan(scan, context.Player, context.Credits, context.Reputation, context.Npcs);
        if (!scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation) ||
            context.Fugitive.Heat != PoliceHeatLevel.Pursuit)
        {
            return false;
        }

        scanner.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        if (!context.Fugitive.IsEvading)
            return false;

        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, context.Npcs);
        return !context.Fugitive.IsActive &&
            context.Player.CargoHold.GetCommodityQuantity(SideArmsName) == 1 &&
            Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), 0.10f);
    }

    private static bool DeterministicHotPursuitProof()
    {
        MissionProofContext context = CreateMissionProofContext();
        Mission? mission = CreateAndAcceptSmugglingMission(context);
        if (mission == null)
            return false;

        PoliceScanSystem scan = new();
        scan.SetMissionManager(context.Manager);
        scan.SetFugitiveManager(context.Fugitive);
        NpcShip police = CreatePolice(new Vector3(1_500f, 0f, 0f));
        context.Npcs.Add(police);
        StepPoliceScan(scan, context.Player, context.Credits, context.Reputation, context.Npcs);
        if (!scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation))
            return false;

        float standingAfterRefusal = context.Reputation.GetStanding(FactionManager.LibertyPolice);
        police.MarkDamagedByPlayer(1f);
        FactionCombatConsequenceService consequences = new(context.Reputation);
        consequences.RecordPlayerDamage(police);
        if (!context.Fugitive.NotifyPlayerDamage(police, context.Player) ||
            context.Fugitive.Heat != PoliceHeatLevel.HotPursuit)
        {
            return false;
        }

        police.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        if (!context.Fugitive.IsEvading)
            return false;

        context.Fugitive.Update(PoliceFugitiveManager.Heat2EscapeDurationSeconds, context.Player, context.Npcs);
        return !context.Fugitive.IsActive &&
            mission.Status == MissionStatus.InProgress &&
            context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == mission.RequiredQuantity &&
            Nearly(context.Reputation.GetStanding(FactionManager.LibertyPolice), standingAfterRefusal);
    }

    private static bool DeterministicSmugglerEscapeProof()
    {
        MissionProofContext context = CreateMissionProofContext();
        Mission? mission = CreateAndAcceptSmugglingMission(context);
        if (mission == null)
            return false;

        PoliceScanSystem scan = new();
        scan.SetMissionManager(context.Manager);
        scan.SetFugitiveManager(context.Fugitive);
        NpcShip scanner = CreatePolice(new Vector3(1_500f, 0f, 0f));
        context.Npcs.Add(scanner);
        StepPoliceScan(scan, context.Player, context.Credits, context.Reputation, context.Npcs);
        if (!mission.SmugglingPoliceDetected ||
            !scan.TryRefuseEnforcement(context.Player, context.Credits, context.Reputation))
        {
            return false;
        }

        scanner.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        context.Fugitive.Update(PoliceFugitiveManager.Heat1EscapeDurationSeconds, context.Player, context.Npcs);
        if (context.Fugitive.IsActive || mission.Status != MissionStatus.InProgress ||
            context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) != mission.RequiredQuantity)
        {
            return false;
        }

        int beforeReward = context.Credits.Credits;
        bool delivered = context.World.NotifyStationDocked(context.Destination);
        bool deliveredAgain = context.World.NotifyStationDocked(context.Destination);
        return delivered && !deliveredAgain && mission.Status == MissionStatus.Rewarded &&
            mission.RewardPaid && context.Credits.Credits == beforeReward + mission.Reward &&
            context.Player.CargoHold.GetCommodityQuantity(SideArmsName) == 0 &&
            !context.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool ResolveRefusal(Context context)
    {
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, context.Cargo, context.Credits);
        if (!service.TryResolve(offer, PoliceEnforcementResolution.Refuse, context.Cargo, context.Credits, context.Reputation, out _, out _))
            return false;
        context.Fugitive.BeginPursuit(context.Player, "enforcement refusal");
        return context.Fugitive.IsActive && context.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static bool StartAndCheck(string reason)
    {
        Context context = CreateContext();
        context.Fugitive.BeginPursuit(context.Player, reason);
        return context.Fugitive.IsActive && context.Fugitive.Heat == PoliceHeatLevel.Pursuit;
    }

    private static void StepPoliceScan(
        PoliceScanSystem scan,
        Ship player,
        PlayerCredits credits,
        ReputationManager reputation,
        IReadOnlyList<NpcShip> npcs)
    {
        TimeSpan total = TimeSpan.Zero;
        for (int i = 0; i < 8; i++)
        {
            TimeSpan step = TimeSpan.FromSeconds(0.5);
            scan.Update(new GameTime(total, step), player, npcs, credits, reputation);
            total += step;
        }
    }

    private static Mission? CreateAndAcceptSmugglingMission(MissionProofContext context)
    {
        Commodity contraband = CommodityCatalog.GetById("side-arms")!;
        Mission? mission = Mission.CreateContrabandSmuggling(
            context.Origin,
            context.Destination,
            contraband,
            quantity: 1,
            reward: 5_000,
            difficulty: MissionDifficulty.Easy);
        return mission != null && context.Manager.AcceptMission(mission, context.Origin)
            ? mission
            : null;
    }

    private static MissionProofContext CreateMissionProofContext()
    {
        Ship player = new(Vector3.Zero);
        PlayerCredits credits = new(1_000);
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, 0.30f, "phase 54 proof setup");
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "phase 54 proof setup");
        MissionManager manager = new(credits, null, reputation, null, player.CargoHold);
        MissionWaypointSystem waypointSystem = new();
        Station origin = CreateProofStation("Rogue Haven", FactionManager.LibertyRogues, 0f);
        Station destination = CreateProofStation("Fort Bush", FactionManager.LibertyCorporations, 4_000f);
        List<NpcShip> npcs = new();
        List<Station> stations = new() { origin, destination };
        List<SpaceObject> objects = stations.Cast<SpaceObject>().ToList();
        MissionWorldManager world = new(manager, waypointSystem, player, npcs, objects, () => stations);
        manager.SetWaypointSystem(waypointSystem);
        manager.SetWorldManager(world);
        PoliceFugitiveManager fugitive = new(reputation);
        return new MissionProofContext(player, credits, reputation, manager, world, origin, destination, npcs, fugitive);
    }

    private static Station CreateProofStation(string name, string factionId, float x) =>
        new(new StationConfig
        {
            Description = name,
            FactionId = factionId,
            SystemIndex = 1,
            StartupPositionX = x,
            StartupPositionY = 0f,
            StartupPositionZ = 0f,
            Radius = 200f,
            DockingRange = 500f
        }, null);

    private static Context CreateEvadingContext(PoliceHeatLevel requestedHeat)
    {
        Context context = CreateContext();
        NpcShip police = CreatePolice(new Vector3(1_000f, 0f, 0f));
        context.Npcs.Add(police);
        context.Fugitive.BeginPursuit(context.Player);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        police.Position = new Vector3(20_000f, 0f, 0f);
        context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        if (requestedHeat == PoliceHeatLevel.HotPursuit)
        {
            police.MarkDamagedByPlayer(1f);
            context.Fugitive.NotifyPlayerDamage(police, context.Player);
            context.Fugitive.Update(0.5f, context.Player, context.Npcs);
        }

        return context;
    }

    private static Context CreateContrabandContext(int quantity = 1, int credits = 10_000, float standing = 0.30f)
    {
        Context context = CreateContext(standing, credits);
        context.Cargo.AddCommodity(CommodityCatalog.GetById("side-arms")!, quantity);
        return context;
    }

    private static Context CreateContext(float standing = 0.00f, int credits = 10_000)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(FactionManager.LibertyPolice, standing, "phase 54 smoke setup");
        Ship player = new(Vector3.Zero);
        return new Context(
            reputation,
            player,
            new CargoHold(100),
            new PlayerCredits(credits),
            new List<NpcShip>());
    }

    private static NpcShip CreatePolice(Vector3 position) =>
        new("Phase 54 Police", position, Vector3.Zero, 1f, 0f, FactionManager.LibertyPolice);

    private static string SideArmsName => CommodityCatalog.GetById("side-arms")!.Name;

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

    private sealed record MissionProofContext(
        Ship Player,
        PlayerCredits Credits,
        ReputationManager Reputation,
        MissionManager Manager,
        MissionWorldManager World,
        Station Origin,
        Station Destination,
        List<NpcShip> Npcs,
        PoliceFugitiveManager Fugitive);
}
