using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 65 proof for one-shot ambient security assignment, ordinary
/// NPC formation/combat integration, bounded save/load, and retirement.
/// </summary>
internal sealed class Phase65ShipmentSecuritySmokeTest
{
    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(HighRiskShipmentGetsBoundedDetail, "high-risk shipment gets at most two deterministic escorts", ref passed, ref failed);
        RunCase(NonQualifyingShipmentDecidesOnce, "non-qualifying shipment does not retry assignment", ref passed, ref failed);
        RunCase(SecurityPrioritizesMerchantThreat, "security prioritizes a credible hostile merchant threat", ref passed, ref failed);
        RunCase(SaveLoadDoesNotReplaceLostMember, "save/load preserves member death without replacement", ref passed, ref failed);
        RunCase(ScannerSecurityLabelIsBounded, "scanner security label is bounded", ref passed, ref failed);
        Console.WriteLine($"[PHASE 65 SHIPMENT SECURITY SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(
        Func<(bool Success, string FailureReason)> test,
        string label,
        ref int passed,
        ref int failed)
    {
        try
        {
            (bool Success, string FailureReason) result = RunSilenced(test);
            if (result.Success)
            {
                passed++;
                Console.WriteLine($"[PHASE 65 SHIPMENT SECURITY SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 65 SHIPMENT SECURITY SMOKE] FAIL {label}: {result.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 65 SHIPMENT SECURITY SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) HighRiskShipmentGetsBoundedDetail()
    {
        (NpcShip trader, EconomicShipment shipment) = CreateShipment(70);
        ShipmentSecurityManager manager = new();
        List<NpcShip> spawned = new();
        manager.Configure(
            (parent, member, position) =>
            {
                NpcShip guard = new(
                    member.StableIdentity,
                    position,
                    parent.Trader.Position,
                    900f,
                    0.2f,
                    member.FactionId);
                spawned.Add(guard);
                return guard;
            },
            _ => { },
            (_, _) => { },
            _ => FactionManager.LibertyCorporations);

        bool assigned = manager.TryEvaluateAndAssign(shipment);
        bool hasDetail = manager.TryGetDetail(shipment, out ShipmentSecurityDetail detail);
        bool stable = detail?.Members.Select(member => member.StableIdentity).Distinct(StringComparer.Ordinal).Count() == 2 &&
            detail.Members.All(member => member.Ship?.FormationLeader == trader);
        bool bounded = detail?.ActiveEscortCount == 2 && detail.MemberCount <= ShipmentSecurityManager.MaximumEscortsPerShipment &&
            spawned.Count == 2 && ShipmentSecurityManager.MaximumEscortsPerShipment <= 2;
        return assigned && hasDetail && stable && bounded
            ? Pass()
            : Fail($"assigned={assigned}, detail={detail?.MemberCount}, active={detail?.ActiveEscortCount}, spawned={spawned.Count}");
    }

    private (bool Success, string FailureReason) NonQualifyingShipmentDecidesOnce()
    {
        (_, EconomicShipment shipment) = CreateShipment(39);
        ShipmentSecurityManager manager = new();
        int spawnCalls = 0;
        manager.Configure((_, _, _) =>
        {
            spawnCalls++;
            return null;
        }, null, null);

        bool first = manager.TryEvaluateAndAssign(shipment);
        bool second = manager.TryEvaluateAndAssign(shipment);
        return !first && !second && shipment.SecurityAssignmentDecided && spawnCalls == 0 && manager.ActiveDetailCount == 0
            ? Pass()
            : Fail($"first={first}, second={second}, decided={shipment.SecurityAssignmentDecided}, spawns={spawnCalls}");
    }

    private (bool Success, string FailureReason) SecurityPrioritizesMerchantThreat()
    {
        (NpcShip trader, EconomicShipment shipment) = CreateShipment(70);
        ShipmentSecurityManager manager = new();
        manager.Configure(
            (parent, member, position) => new NpcShip(member.StableIdentity, position, parent.Trader.Position, 900f, 0.2f, member.FactionId),
            _ => { },
            (_, _) => { },
            _ => FactionManager.LibertyCorporations);
        if (!manager.TryEvaluateAndAssign(shipment) || !manager.TryGetDetail(shipment, out ShipmentSecurityDetail detail))
            return Fail("security detail did not spawn");

        NpcShip security = detail.Members.First().Ship;
        NpcShip rogue = new(
            "Phase65 Rogue",
            trader.Position + new Vector3(400f, 0f, 0f),
            trader.Position,
            900f,
            0.2f,
            FactionManager.LibertyRogues);
        rogue.SetEncounterState(TrafficEncounterState.AttackingTrader, trader.Position);
        NpcShip selected = manager.GetPriorityThreat(security, new[] { trader, security, rogue });
        return selected == rogue
            ? Pass()
            : Fail("credible hostile rogue was not selected for the security member");
    }

    private (bool Success, string FailureReason) SaveLoadDoesNotReplaceLostMember()
    {
        (NpcShip trader, EconomicShipment shipment) = CreateShipment(70);
        ShipmentSecurityManager original = new();
        List<NpcShip> firstSpawned = new();
        int firstRetirements = 0;
        original.Configure(
            (parent, member, position) =>
            {
                NpcShip guard = new(member.StableIdentity, position, parent.Trader.Position, 900f, 0.2f, member.FactionId);
                firstSpawned.Add(guard);
                return guard;
            },
            _ => { },
            (_, _) => firstRetirements++,
            _ => FactionManager.LibertyCorporations);
        if (!original.TryEvaluateAndAssign(shipment) || firstSpawned.Count != 2)
            return Fail("initial security detail did not produce two members");

        original.NotifyNpcDestroyed(firstSpawned[0]);
        List<SaveShipmentSecurityMemberData> savedMembers = original.CaptureState(shipment).ToList();
        if (savedMembers.Count != 2 || savedMembers.Count(member => !member.Alive) != 1)
            return Fail("member death was not captured as durable state");

        ShipmentSecurityManager restored = new();
        int restoreSpawnCalls = 0;
        int restoredRetirements = 0;
        restored.Configure(
            (parent, member, position) =>
            {
                restoreSpawnCalls++;
                return new NpcShip(member.StableIdentity, position, parent.Trader.Position, 900f, 0.2f, member.FactionId);
            },
            _ => { },
            (_, _) => restoredRetirements++,
            _ => FactionManager.LibertyCorporations);
        SaveEconomicShipmentData state = new()
        {
            SecurityAssignmentDecided = true,
            SecurityMembers = savedMembers
        };
        ShipmentSecurityDetail detail = null;
        bool rebound = restored.RestoreShipmentSecurity(shipment, state);
        bool noReplacement = rebound && restored.TryGetDetail(shipment, out detail) &&
            detail.MemberCount == 2 && detail.ActiveEscortCount == 1 && restoreSpawnCalls == 1;
        restored.NotifyShipmentSettled(shipment, "smoke complete");
        return noReplacement && restored.ActiveDetailCount == 0 && restoredRetirements == 1 && firstRetirements == 1
            ? Pass()
            : Fail($"rebound={rebound}, members={detail?.MemberCount}, active={detail?.ActiveEscortCount}, spawns={restoreSpawnCalls}, retirements={restoredRetirements}/{firstRetirements}");
    }

    private (bool Success, string FailureReason) ScannerSecurityLabelIsBounded()
    {
        PlayerTargetScanRouteInfo route = new(
            "phase65-route",
            "origin",
            "Origin",
            "destination",
            "Destination",
            70,
            "SEVERE",
            securityEscortCount: 99);
        return route.SecurityEscortCount == ShipmentSecurityManager.MaximumEscortsPerShipment &&
            route.SecurityLabel == "2 escort(s)"
            ? Pass()
            : Fail($"security count={route.SecurityEscortCount}, label={route.SecurityLabel}");
    }

    private static (NpcShip Trader, EconomicShipment Shipment) CreateShipment(int risk)
    {
        NpcShip trader = new(
            "Phase65 Merchant",
            Vector3.Zero,
            Vector3.Forward,
            900f,
            0.2f,
            FactionManager.NeutralCivilians);
        trader.ConfigureTrafficBehavior(
            TrafficZoneBehaviorType.TraderRoute,
            "phase65-route",
            Vector3.Zero,
            900f,
            190f,
            7_000f,
            new Vector3(-10_000f, 0f, 0f),
            new Vector3(10_000f, 0f, 0f));
        Commodity commodity = new(
            "phase65-high-value",
            "Phase 65 High Value",
            "Smoke commodity",
            2_500,
            1,
            false,
            "Industrial",
            Color.White);
        EconomicShipment shipment = new(
            trader,
            "phase65-route",
            "origin",
            "destination",
            "Origin",
            "Destination",
            true,
            new TraderCargoManifest(new[] { new TraderCargoStack(commodity, 2) }));
        shipment.EffectiveRouteRisk = risk;
        return (trader, shipment);
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static T RunSilenced<T>(Func<T> action)
    {
        System.IO.TextWriter original = Console.Out;
        using System.IO.StringWriter writer = new();
        Console.SetOut(writer);
        try
        {
            return action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
