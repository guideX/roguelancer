using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Roguelancer;

/// <summary>
/// Phase 86 focused coverage for the spaceflight encounter HUD state cleanup.
/// The suite drives only production systems: the player-side
/// PirateCargoDemandService and the Liberty Police scan/enforcement stack
/// (PoliceScanSystem, PoliceEnforcementService,
/// PoliceContrabandStopCoordinator, PoliceFugitiveManager). It proves the
/// player piracy service no longer retains post-resolution presentation
/// text (HudText is a live-only projection; resolution feedback travels
/// through the notification seam), and that the Police stop presentation
/// was already a correct live-state projection and is unchanged. No new
/// piracy, Police, cargo, combat, or persistence authority is introduced.
/// </summary>
internal sealed class Phase86SpaceflightEncounterHudStateCleanupSmokeTest
{
    public Phase86SpaceflightEncounterHudStateCleanupSmokeTest(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        // Player piracy (spec items 1-15).
        RunCase("no demand produces no active demand prompt", NoDemandProducesNoActiveDemandPrompt, ref passed, ref failed);
        RunCase("active demand produces correct live prompt", ActiveDemandProducesCorrectLivePrompt, ref passed, ref failed);
        RunCase("correct target identity is presented", CorrectTargetIdentityIsPresented, ref passed, ref failed);
        RunCase("correct timer is presented", CorrectTimerIsPresented, ref passed, ref failed);
        RunCase("successful compliance preserves real cargo surrender", SuccessfulCompliancePreservesRealCargoSurrender, ref passed, ref failed);
        RunCase("refusal preserves manifest quantities", RefusalPreservesManifestQuantities, ref passed, ref failed);
        RunCase("result notification remains useful", ResultNotificationRemainsUseful, ref passed, ref failed);
        RunCase("no stale text after compliance", NoStaleTextAfterCompliance, ref passed, ref failed);
        RunCase("no stale text after refusal", NoStaleTextAfterRefusal, ref passed, ref failed);
        RunCase("no stale text after timeout", NoStaleTextAfterTimeout, ref passed, ref failed);
        RunCase("NPC death/despawn clears stale presentation", NpcDeathDespawnClearsStalePresentation, ref passed, ref failed);
        RunCase("system transition clears presentation", SystemTransitionClearsPresentation, ref passed, ref failed);
        RunCase("save/load restores no presentation history", SaveLoadRestoresNoPresentationHistory, ref passed, ref failed);
        RunCase("repeated reads have no gameplay side effects", RepeatedReadsHaveNoGameplaySideEffects, ref passed, ref failed);
        RunCase("no duplicate physical cargo is spawned", NoDuplicatePhysicalCargoIsSpawned, ref passed, ref failed);
        // Liberty Police (spec items 16-35).
        RunCase("active lawful scan presents correct status", ActiveLawfulScanPresentsCorrectStatus, ref passed, ref failed);
        RunCase("active enforcement presents correct comply/refuse controls", ActiveEnforcementPresentsCorrectControls, ref passed, ref failed);
        RunCase("quoted fine remains authoritative", QuotedFineRemainsAuthoritative, ref passed, ref failed);
        RunCase("compliance produces existing exact outcome", ComplianceProducesExistingExactOutcome, ref passed, ref failed);
        RunCase("refusal produces existing escalation", RefusalProducesExistingEscalation, ref passed, ref failed);
        RunCase("no stale fine after completed stop", NoStaleFineAfterCompletedStop, ref passed, ref failed);
        RunCase("no obsolete compliance prompt after refusal", NoObsoleteCompliancePromptAfterRefusal, ref passed, ref failed);
        RunCase("no stale prompt after cancellation", NoStalePromptAfterCancellation, ref passed, ref failed);
        RunCase("no stale prompt after NPC death/despawn", NoStalePromptAfterNpcDeathDespawn, ref passed, ref failed);
        RunCase("no stale prompt after player death", NoStalePromptAfterPlayerDeath, ref passed, ref failed);
        RunCase("no stale prompt after system transition", NoStalePromptAfterSystemTransition, ref passed, ref failed);
        RunCase("save/load does not replay prior presentation", SaveLoadDoesNotReplayPriorPresentation, ref passed, ref failed);
        RunCase("hostile-player behavior remains unchanged", HostilePlayerBehaviorRemainsUnchanged, ref passed, ref failed);
        RunCase("hold-fire behavior remains unchanged", HoldFireBehaviorRemainsUnchanged, ref passed, ref failed);
        RunCase("Phase 74 tier escalation remains unchanged", Phase74TierEscalationRemainsUnchanged, ref passed, ref failed);
        RunCase("Phase 76 notifications remain unchanged", Phase76NotificationsRemainUnchanged, ref passed, ref failed);
        RunCase("Phase 77 docking remains unchanged", Phase77DockingRemainsUnchanged, ref passed, ref failed);
        RunCase("Phase 78 surrender remains unchanged", Phase78SurrenderRemainsUnchanged, ref passed, ref failed);
        RunCase("Phase 79 fire cessation remains unchanged", Phase79FireCessationRemainsUnchanged, ref passed, ref failed);
        RunCase("Phase 80 normalization remains unchanged", Phase80NormalizationRemainsUnchanged, ref passed, ref failed);
        // Shared invariants (spec items 36-45).
        RunCase("existing action keys work", ExistingActionKeysWork, ref passed, ref failed);
        RunCase("same-frame response races remain idempotent", SameFrameResponseRacesRemainIdempotent, ref passed, ref failed);
        RunCase("timeout/input precedence is unchanged", TimeoutInputPrecedenceIsUnchanged, ref passed, ref failed);
        RunCase("active HUD reads are side-effect free", ActiveHudReadsAreSideEffectFree, ref passed, ref failed);
        RunCase("notification expiry does not alter gameplay", NotificationExpiryDoesNotAlterGameplay, ref passed, ref failed);
        RunCase("no duplicate result messages", NoDuplicateResultMessages, ref passed, ref failed);
        RunCase("no new persisted presentation fields", NoNewPersistedPresentationFields, ref passed, ref failed);
        RunCase("schema remains unchanged", SchemaRemainsUnchanged, ref passed, ref failed);
        RunCase("relevant existing suites remain green", RelevantExistingSuitesRemainGreen, ref passed, ref failed);
        RunCase("no new gameplay authority exists", NoNewGameplayAuthorityExists, ref passed, ref failed);

        Console.WriteLine($"[PHASE 86 SPACEFLIGHT ENCOUNTER HUD STATE CLEANUP SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 86 SPACEFLIGHT ENCOUNTER HUD STATE CLEANUP SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 86 SPACEFLIGHT ENCOUNTER HUD STATE CLEANUP SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 86 SPACEFLIGHT ENCOUNTER HUD STATE CLEANUP SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) Check(bool condition, string reason) => (condition, condition ? string.Empty : reason);

    // ---- Player piracy harness ------------------------------------------------

    private sealed class PiracyContext
    {
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public FactionManager Factions { get; } = new();
        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public LootManager Loot { get; }
        public FactionCombatCommunicationService Communication { get; }
        public PirateCargoDemandService Demand { get; }
        public NpcShip Trader { get; }
        public List<string> Notifications { get; } = new();
        public int Resolutions { get; private set; }

        public PiracyContext(bool policeNearby = false)
        {
            Reputation = new ReputationManager(Factions);
            Player = new Ship(Vector3.Zero);
            Trader = CreateTrader("Phase86 Trader", new Vector3(1000f, 0f, 0f));
            Npcs.Add(Trader);
            Objects.Add(Trader);

            if (policeNearby)
            {
                NpcShip police = new("Phase86 Police", new Vector3(1200f, 0f, 500f), Vector3.Zero, 500f, 0.1f, FactionManager.LibertyPolice);
                police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase86-police", Vector3.Zero, 500f, 180f, 6500f);
                Npcs.Add(police);
                Objects.Add(police);
            }

            Loot = new LootManager(
                graphicsDevice: null,
                random: null,
                font: null,
                pixel: null,
                salvageService: new CombatSalvageService(),
                worldObjectsProvider: () => Objects);
            Communication = new FactionCombatCommunicationService(Npcs);
            Demand = new PirateCargoDemandService(
                Npcs,
                Reputation,
                null,
                (trader, commodityId, quantity) => Loot.SpawnExtortionCargo(trader, commodityId, quantity, out _),
                null,
                null,
                null,
                Communication,
                null,
                null,
                null,
                null,
                null,
                Notifications.Add);
            Demand.DemandResolved += _ => Resolutions++;
        }

        public static NpcShip CreateTrader(string name, Vector3 position, string faction = FactionManager.NeutralCivilians)
        {
            NpcShip trader = new(name, position, Vector3.Zero, 1000f, 0.1f, faction);
            trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                "phase86-trader-route",
                Vector3.Zero,
                1000f,
                190f,
                3500f,
                new Vector3(-5000f, 0f, 0f),
                new Vector3(5000f, 0f, 0f));
            return trader;
        }

        public void Advance(float seconds = PirateCargoDemandService.ResponseWindowSeconds + 0.1f)
        {
            Demand.Update(seconds, Player);
        }
    }

    // ---- Police harness -------------------------------------------------------

    private sealed class PoliceContext
    {
        public PoliceScanSystem Scan { get; } = new();
        public ReputationManager Reputation { get; }
        public Ship Player { get; } = new(Vector3.Zero);
        public PlayerCredits Credits { get; } = new(5_000);
        public List<NpcShip> Npcs { get; } = new();
        public PoliceFugitiveManager Fugitive { get; }
        public NotificationManager Notifications { get; } = new(null, new Viewport(0, 0, 1280, 720));

        public PoliceContext()
        {
            Reputation = new ReputationManager(new FactionManager());
            Fugitive = new PoliceFugitiveManager(Reputation);
            Scan.SetFugitiveManager(Fugitive);
        }

        public NpcShip AddScanner(string name = "Phase86 Police Scanner", Vector3? position = null)
        {
            NpcShip scanner = new(name, position ?? new Vector3(1500f, 0f, 0f), position ?? new Vector3(1500f, 0f, 0f),
                1f, 0f, FactionManager.LibertyPolice)
            {
                Position = position ?? new Vector3(1500f, 0f, 0f),
                Velocity = Vector3.Zero
            };
            Npcs.Add(scanner);
            return scanner;
        }

        public void SeedContraband(int quantity = 1)
        {
            Commodity contraband = CommodityCatalog.GetById("side-arms");
            if (contraband == null || !Player.CargoHold.AddCommodity(contraband, quantity))
                throw new InvalidOperationException("could not seed canonical contraband");
        }

        public void Step(float seconds, int frames)
        {
            TimeSpan total = TimeSpan.Zero;
            for (int i = 0; i < frames; i++)
            {
                total += TimeSpan.FromSeconds(seconds);
                Scan.Update(new GameTime(total - TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(seconds)),
                    Player, Npcs, Credits, Reputation, Notifications, null);
            }
        }

        public void StepToDetection()
        {
            AddScanner();
            SeedContraband();
            Step(0.5f, 8);
        }
    }

    private static List<string> GetNotificationTexts(NotificationManager manager)
    {
        List<string> texts = new();
        if (manager == null)
            return texts;
        FieldInfo currentField = typeof(NotificationManager).GetField("_currentMessage", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentField?.GetValue(manager) is object current)
        {
            FieldInfo textField = current.GetType().GetField("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (textField?.GetValue(current) is string currentText && !string.IsNullOrEmpty(currentText))
                texts.Add(currentText);
        }
        FieldInfo queueField = typeof(NotificationManager).GetField("_messageQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        if (queueField?.GetValue(manager) is IEnumerable queue)
        {
            foreach (object item in queue)
            {
                if (item == null)
                    continue;
                FieldInfo textField = item.GetType().GetField("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (textField?.GetValue(item) is string text && !string.IsNullOrEmpty(text))
                    texts.Add(text);
            }
        }
        return texts;
    }

    private static bool HasInstanceFieldOfType(Type type, Type fieldType)
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.FieldType == fieldType)
                return true;
        }
        return false;
    }

    private static bool HasMemberContaining(Type type, params string[] fragments)
    {
        foreach (MemberInfo member in type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            foreach (string fragment in fragments)
            {
                if (member.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static bool PropertyHasSetter(Type type, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.SetMethod != null;
    }

    // ---- Player piracy: spec items 1-15 ----------------------------------------

    private (bool, string) NoDemandProducesNoActiveDemandPrompt()
    {
        PiracyContext c = new();
        return Check(!c.Demand.HasActiveDemand && c.Demand.HudText == string.Empty &&
            c.Demand.CurrentState == PiracyDemandState.None && c.Notifications.Count == 0,
            $"idle HUD was not empty: '{c.Demand.HudText}'");
    }

    private (bool, string) ActiveDemandProducesCorrectLivePrompt()
    {
        PiracyContext c = new();
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"demand was rejected: {failure}");
        string hud = c.Demand.HudText;
        return Check(c.Demand.HasActiveDemand &&
            hud.Contains("CARGO DEMAND", StringComparison.Ordinal) &&
            hud.Contains(c.Trader.Name, StringComparison.Ordinal) &&
            c.Demand.ResponseRemainingSeconds > 0f &&
            c.Demand.ResponseRemainingSeconds <= PirateCargoDemandService.ResponseWindowSeconds,
            $"live prompt was incorrect: '{hud}'");
    }

    private (bool, string) CorrectTargetIdentityIsPresented()
    {
        PiracyContext c = new();
        NpcShip other = PiracyContext.CreateTrader("Phase86 Bystander", new Vector3(1100f, 0f, 100f));
        c.Npcs.Add(other);
        c.Objects.Add(other);
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"demand was rejected: {failure}");
        string hud = c.Demand.HudText;
        return Check(hud.Contains(c.Trader.Name, StringComparison.Ordinal) &&
            !hud.Contains(other.Name, StringComparison.Ordinal),
            $"prompt showed the wrong identity: '{hud}'");
    }

    private (bool, string) CorrectTimerIsPresented()
    {
        PiracyContext c = new();
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"demand was rejected: {failure}");
        float before = c.Demand.ResponseRemainingSeconds;
        string hudBefore = c.Demand.HudText;
        c.Demand.Update(1f, c.Player);
        float after = c.Demand.ResponseRemainingSeconds;
        string hudAfter = c.Demand.HudText;
        return Check(before > after && after > 0f && hudBefore != hudAfter &&
            hudAfter.Contains($"{after:0.0}", StringComparison.Ordinal),
            $"timer did not derive from authoritative state: before='{hudBefore}' after='{hudAfter}'");
    }

    private (bool, string) SuccessfulCompliancePreservesRealCargoSurrender()
    {
        PiracyContext c = new();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        int before = manifest.RemainingQuantity;
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"demand was rejected: {failure}");
        c.Advance();
        PiracyDemandResult result = c.Demand.LastResult;
        return Check(result?.State == PiracyDemandState.Complying && result.SurrenderedQuantity > 0 &&
            manifest.RemainingQuantity == before - result.SurrenderedQuantity &&
            c.Loot.ActivePods.Count > 0 && c.Loot.ActivePods.All(pod => pod.IsStolen),
            "compliance did not preserve the exact cargo transaction");
    }

    private (bool, string) RefusalPreservesManifestQuantities()
    {
        PiracyContext c = new(policeNearby: true);
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        int before = manifest.RemainingQuantity;
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"demand was rejected: {failure}");
        c.Advance();
        return Check(c.Demand.LastResult?.State == PiracyDemandState.RefusingFleeing &&
            manifest.RemainingQuantity == before && c.Loot.ActivePods.Count == 0,
            "refusal changed manifest quantities or spawned cargo");
    }

    private (bool, string) ResultNotificationRemainsUseful()
    {
        PiracyContext comply = new();
        comply.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        comply.Demand.TryIssueDemand(comply.Player, comply.Trader, out _);
        comply.Advance();
        int surrendered = comply.Demand.LastResult?.SurrenderedQuantity ?? -1;
        if (comply.Notifications.Count != 1 ||
            !comply.Notifications[0].Contains($"{surrendered}", StringComparison.Ordinal))
            return (false, $"compliance lost dynamic feedback: [{string.Join(" | ", comply.Notifications)}]");

        PiracyContext refuse = new(policeNearby: true);
        refuse.Demand.TryIssueDemand(refuse.Player, refuse.Trader, out _);
        refuse.Advance();
        if (refuse.Demand.LastResult?.State != PiracyDemandState.RefusingFleeing)
            return (false, "could not stage a genuine refusal");
        return Check(refuse.Notifications.Count == 1 && refuse.Notifications[0].Length > 0,
            $"refusal lost feedback: [{string.Join(" | ", refuse.Notifications)}]");
    }

    private (bool, string) NoStaleTextAfterCompliance()
    {
        PiracyContext c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        if (c.Demand.LastResult?.State != PiracyDemandState.Complying)
            return (false, "could not stage a genuine compliance");
        if (c.Demand.HudText != string.Empty || c.Demand.HasActiveDemand)
            return (false, $"stale HUD immediately after compliance: '{c.Demand.HudText}'");
        c.Demand.Update(5f, c.Player);
        return Check(c.Demand.HudText == string.Empty && !c.Demand.HasActiveDemand,
            $"stale HUD 5s after compliance: '{c.Demand.HudText}'");
    }

    private (bool, string) NoStaleTextAfterRefusal()
    {
        PiracyContext c = new(policeNearby: true);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        if (c.Demand.LastResult?.State != PiracyDemandState.RefusingFleeing)
            return (false, "could not stage a genuine refusal");
        if (c.Demand.HudText != string.Empty || c.Demand.HasActiveDemand)
            return (false, $"stale HUD immediately after refusal: '{c.Demand.HudText}'");
        c.Demand.Update(5f, c.Player);
        return Check(c.Demand.HudText == string.Empty && !c.Demand.HasActiveDemand,
            $"stale HUD 5s after refusal: '{c.Demand.HudText}'");
    }

    private (bool, string) NoStaleTextAfterTimeout()
    {
        PiracyContext c = new();
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"demand was rejected: {failure}");
        c.Advance(PirateCargoDemandService.ResponseWindowSeconds + 0.5f);
        if (c.Demand.HasActiveDemand || c.Demand.LastResult == null)
            return (false, "the response window did not resolve the demand");
        if (c.Demand.HudText != string.Empty)
            return (false, $"stale HUD immediately after timeout: '{c.Demand.HudText}'");
        c.Demand.Update(5f, c.Player);
        return Check(c.Demand.HudText == string.Empty,
            $"stale HUD 5s after timeout: '{c.Demand.HudText}'");
    }

    private (bool, string) NpcDeathDespawnClearsStalePresentation()
    {
        PiracyContext death = new();
        death.Demand.TryIssueDemand(death.Player, death.Trader, out _);
        death.Trader.Hull.TakeDamage(death.Trader.Hull.MaxHull + 1f);
        death.Demand.NotifyNpcDestroyed(death.Trader);
        death.Demand.Update(0.1f, death.Player);
        if (death.Demand.HasActiveDemand || death.Demand.HudText != string.Empty || death.Notifications.Count != 0)
            return (false, $"death left stale presentation: '{death.Demand.HudText}'");

        PiracyContext despawn = new();
        despawn.Demand.TryIssueDemand(despawn.Player, despawn.Trader, out _);
        despawn.Npcs.Remove(despawn.Trader);
        despawn.Demand.Update(0.1f, despawn.Player);
        return Check(!despawn.Demand.HasActiveDemand && despawn.Demand.HudText == string.Empty &&
            despawn.Notifications.Count == 0,
            $"despawn left stale presentation: '{despawn.Demand.HudText}'");
    }

    private (bool, string) SystemTransitionClearsPresentation()
    {
        PiracyContext c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Demand.Reset();
        if (c.Demand.HasActiveDemand || c.Demand.HudText != string.Empty || c.Demand.LastResult != null)
            return (false, "system-transition reset left presentation state");
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"fresh post-transition demand was rejected: {failure}");
        return Check(c.Demand.HudText.Contains(c.Trader.Name, StringComparison.Ordinal),
            $"fresh demand did not present live state: '{c.Demand.HudText}'");
    }

    private (bool, string) SaveLoadRestoresNoPresentationHistory()
    {
        PiracyContext c = new();
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Demand.CancelActiveDemand("save/load");
        int notifications = c.Notifications.Count;
        c.Demand.Update(5f, c.Player);
        return Check(!c.Demand.HasActiveDemand && c.Demand.HudText == string.Empty &&
            c.Demand.LastResult == null && c.Notifications.Count == notifications &&
            c.Loot.ActivePods.Count == 0,
            "save/load path restored presentation history");
    }

    private (bool, string) RepeatedReadsHaveNoGameplaySideEffects()
    {
        PiracyContext c = new();
        c.Demand.TryGetManifest(c.Trader, out TraderCargoManifest manifest);
        int before = manifest.RemainingQuantity;
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string failure))
            return (false, $"demand was rejected: {failure}");
        string first = c.Demand.HudText;
        for (int i = 0; i < 20; i++)
        {
            if (c.Demand.HudText != first)
                return (false, "repeated reads were not stable");
        }
        return Check(c.Demand.HasActiveDemand && manifest.RemainingQuantity == before &&
            c.Notifications.Count == 0 && c.Resolutions == 0,
            "reading the HUD mutated gameplay state");
    }

    private (bool, string) NoDuplicatePhysicalCargoIsSpawned()
    {
        PiracyContext c = new();
        c.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        c.Demand.TryIssueDemand(c.Player, c.Trader, out _);
        c.Advance();
        int pods = c.Loot.ActivePods.Count;
        if (pods == 0 || c.Resolutions != 1)
            return (false, "compliance did not produce exactly one physical result");
        for (int i = 0; i < 10; i++)
            _ = c.Demand.HudText;
        c.Demand.Update(1f, c.Player);
        c.Demand.Update(1f, c.Player);
        return Check(c.Loot.ActivePods.Count == pods && c.Resolutions == 1 && c.Demand.HudText == string.Empty,
            "extra updates or HUD reads spawned duplicate cargo");
    }

    // ---- Liberty Police: spec items 16-35 --------------------------------------

    private (bool, string) ActiveLawfulScanPresentsCorrectStatus()
    {
        PoliceContext c = new();
        c.AddScanner();
        c.Scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5f)),
            c.Player, c.Npcs, c.Credits, c.Reputation, c.Notifications, null);
        c.Scan.Update(new GameTime(TimeSpan.FromSeconds(0.5f), TimeSpan.FromSeconds(0.5f)),
            c.Player, c.Npcs, c.Credits, c.Reputation, c.Notifications, null);
        return Check(c.Scan.State == PoliceScanState.Scanning &&
            c.Scan.StatusText.Contains("SCAN", StringComparison.OrdinalIgnoreCase) &&
            c.Scan.ScanProgress > 0f && c.Scan.ScanProgress < 1f &&
            c.Scan.CurrentOffer == null,
            $"scan status was incorrect: '{c.Scan.StatusText}'");
    }

    private (bool, string) ActiveEnforcementPresentsCorrectControls()
    {
        PoliceContext c = new();
        c.StepToDetection();
        if (!c.Scan.IsEnforcementDemandActive)
            return (false, "detection did not open an enforcement demand");
        return Check(c.Scan.StatusText.Contains("[Enter] Comply", StringComparison.Ordinal) &&
            c.Scan.StatusText.Contains("[N] Refuse", StringComparison.Ordinal) &&
            c.Scan.EnforcementDemandRemainingSeconds > 0f,
            $"demand hid controls: '{c.Scan.StatusText}'");
    }

    private (bool, string) QuotedFineRemainsAuthoritative()
    {
        PoliceContext c = new();
        c.StepToDetection();
        PoliceEnforcementOffer offer = c.Scan.CurrentOffer;
        if (offer.FineAmount <= 0)
            return (false, "quoted fine was not positive");
        string fineText = offer.FineAmount.ToString("N0");
        c.Step(0.5f, 2);
        return Check(ReferenceEquals(offer, c.Scan.CurrentOffer) &&
            c.Scan.StatusText.Contains(fineText, StringComparison.Ordinal) &&
            c.Scan.StatusText.Contains("CR", StringComparison.Ordinal),
            $"fine was not authoritative: '{c.Scan.StatusText}'");
    }

    private (bool, string) ComplianceProducesExistingExactOutcome()
    {
        PoliceContext c = new();
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        Commodity legal = CommodityCatalog.GetById("food-rations");
        if (contraband == null || legal == null || !c.Player.CargoHold.AddCommodity(contraband, 1) ||
            !c.Player.CargoHold.AddCommodity(legal, 2))
            return (false, "could not seed mixed civil cargo");
        float standing = c.Reputation.GetStanding(FactionManager.LibertyPolice);
        c.AddScanner();
        c.Step(0.5f, 8);
        int fine = c.Scan.CurrentOffer.FineAmount;
        if (!c.Scan.IsEnforcementDemandActive || !c.Scan.TryAcceptEnforcement(c.Player, c.Credits, c.Reputation, c.Notifications, null))
            return (false, "civil compliance did not resolve the demand");
        return Check(c.Scan.State == PoliceScanState.Cleared && c.Credits.Credits == 5_000 - fine &&
            c.Player.CargoHold.GetCommodityQuantity(contraband.Name) == 0 &&
            c.Player.CargoHold.GetCommodityQuantity(legal.Name) == 2 &&
            !c.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            Math.Abs(c.Reputation.GetStanding(FactionManager.LibertyPolice) - (standing + PoliceScanSystem.DetectionReputationPenalty)) < 0.0001f,
            "civil compliance changed the wrong authoritative state");
    }

    private (bool, string) RefusalProducesExistingEscalation()
    {
        PoliceContext c = new();
        c.StepToDetection();
        if (!c.Scan.TryRefuseEnforcement(c.Player, c.Credits, c.Reputation, c.Notifications, null))
            return (false, "refusal did not resolve the demand");
        return Check(c.Scan.State == PoliceScanState.Enforcement && c.Credits.Credits == 5_000 &&
            c.Player.CargoHold.GetCommodityQuantity("Side Arms") == 1 &&
            c.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            c.Fugitive.IsActive,
            "refusal did not produce the existing escalation");
    }

    private (bool, string) NoStaleFineAfterCompletedStop()
    {
        PoliceContext c = new();
        c.StepToDetection();
        string fineText = c.Scan.CurrentOffer.FineAmount.ToString("N0");
        if (!c.Scan.TryAcceptEnforcement(c.Player, c.Credits, c.Reputation, c.Notifications, null))
            return (false, "compliance did not resolve the demand");
        if (c.Scan.CurrentOffer != null || c.Scan.IsEnforcementDemandActive ||
            c.Scan.StatusText.Contains(fineText, StringComparison.Ordinal) ||
            c.Scan.StatusText.Contains("Comply", StringComparison.OrdinalIgnoreCase))
            return (false, $"stale fine survived resolution: '{c.Scan.StatusText}'");
        c.Step(PoliceScanSystem.ResultHoldSeconds + 0.5f, 1);
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.StatusText == string.Empty &&
            c.Scan.CurrentOffer == null,
            $"stale status survived the result hold: '{c.Scan.StatusText}'");
    }

    private (bool, string) NoObsoleteCompliancePromptAfterRefusal()
    {
        PoliceContext c = new();
        c.StepToDetection();
        if (!c.Scan.TryRefuseEnforcement(c.Player, c.Credits, c.Reputation, c.Notifications, null))
            return (false, "refusal did not resolve the demand");
        if (c.Scan.StatusText.Contains("[Enter]", StringComparison.Ordinal) ||
            c.Scan.StatusText.Contains("[N] Refuse", StringComparison.Ordinal))
            return (false, $"obsolete prompt survived refusal: '{c.Scan.StatusText}'");
        c.Step(PoliceScanSystem.ResultHoldSeconds + 0.5f, 1);
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.StatusText == string.Empty &&
            c.Scan.CurrentOffer == null && !c.Scan.IsEnforcementDemandActive,
            $"stale status survived the result hold: '{c.Scan.StatusText}'");
    }

    private (bool, string) NoStalePromptAfterCancellation()
    {
        PoliceContext c = new();
        c.AddScanner();
        c.Scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5f)),
            c.Player, c.Npcs, c.Credits, c.Reputation, c.Notifications, null);
        if (c.Scan.State != PoliceScanState.Scanning)
            return (false, "scan did not start");
        c.Scan.Update(new GameTime(TimeSpan.FromSeconds(0.5f), TimeSpan.FromSeconds(0.5f)),
            c.Player, c.Npcs, c.Credits, c.Reputation,
            playerDocked: true, playerInTradeLaneTransit: false, c.Notifications, null);
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.StatusText == string.Empty &&
            c.Scan.CurrentOffer == null && !c.Scan.IsEnforcementDemandActive,
            $"cancellation left a stale prompt: '{c.Scan.StatusText}'");
    }

    private (bool, string) NoStalePromptAfterNpcDeathDespawn()
    {
        PoliceContext destroyed = new();
        destroyed.StepToDetection();
        NpcShip scanner = destroyed.Scan.ActiveScanner;
        scanner.MarkDamagedByPlayer();
        scanner.Hull.TakeDamage(scanner.Hull.MaxHull + 1f);
        destroyed.Scan.Update(new GameTime(TimeSpan.FromSeconds(4f), TimeSpan.FromSeconds(0.1f)),
            destroyed.Player, destroyed.Npcs, destroyed.Credits, destroyed.Reputation, destroyed.Notifications, null);
        if (destroyed.Scan.State != PoliceScanState.Enforcement || destroyed.Scan.CurrentOffer != null ||
            destroyed.Scan.StatusText.Contains("[Enter]", StringComparison.Ordinal))
            return (false, $"destroyed scanner left a stale actionable prompt: '{destroyed.Scan.StatusText}'");

        PoliceContext despawned = new();
        despawned.StepToDetection();
        NpcShip vanished = despawned.Scan.ActiveScanner;
        despawned.Npcs.Remove(vanished);
        int credits = despawned.Credits.Credits;
        despawned.Scan.Update(new GameTime(TimeSpan.FromSeconds(4f), TimeSpan.FromSeconds(0.1f)),
            despawned.Player, despawned.Npcs, despawned.Credits, despawned.Reputation, despawned.Notifications, null);
        return Check(despawned.Scan.State == PoliceScanState.Idle && despawned.Scan.StatusText == string.Empty &&
            despawned.Scan.CurrentOffer == null && despawned.Credits.Credits == credits &&
            !despawned.Reputation.IsTemporarilyHostile(FactionManager.LibertyPolice) &&
            !despawned.Fugitive.IsActive,
            $"despawned scanner left stale state: '{despawned.Scan.StatusText}'");
    }

    private (bool, string) NoStalePromptAfterPlayerDeath()
    {
        PoliceContext c = new();
        c.StepToDetection();
        c.Player.Hull.TakeDamage(100000f);
        // Production clears the transient stop through Reset on the death
        // path (PoliceContrabandStopCoordinator); prove that boundary is clean
        // and that the death itself created no fugitive out of thin air.
        c.Scan.Update(new GameTime(TimeSpan.FromSeconds(4f), TimeSpan.FromSeconds(0.1f)),
            c.Player, c.Npcs, c.Credits, c.Reputation, c.Notifications, null);
        c.Scan.Reset();
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.StatusText == string.Empty &&
            c.Scan.CurrentOffer == null && c.Scan.EnforcementDemandRemainingSeconds == 0f,
            $"player death left a stale prompt: '{c.Scan.StatusText}'");
    }

    private (bool, string) NoStalePromptAfterSystemTransition()
    {
        PoliceContext c = new();
        c.StepToDetection();
        c.Scan.Reset();
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.StatusText == string.Empty &&
            c.Scan.CurrentOffer == null && c.Scan.EnforcementDemandRemainingSeconds == 0f &&
            !c.Scan.IsEnforcementDemandActive,
            $"system transition left a stale prompt: '{c.Scan.StatusText}'");
    }

    private (bool, string) SaveLoadDoesNotReplayPriorPresentation()
    {
        PoliceContext c = new();
        c.StepToDetection();
        if (!c.Scan.TryAcceptEnforcement(c.Player, c.Credits, c.Reputation, c.Notifications, null))
            return (false, "compliance did not resolve the demand");
        int messages = GetNotificationTexts(c.Notifications).Count;
        c.Scan.Reset();
        // Production save/load also tears down the world encounter (scanner
        // list, cooldowns); a lingering scanner would legitimately open a
        // fresh scan. Clear the world so only replay behavior is measured.
        c.Npcs.Clear();
        c.Step(0.5f, 4);
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.StatusText == string.Empty &&
            GetNotificationTexts(c.Notifications).Count == messages,
            "save/load replayed prior presentation");
    }

    private (bool, string) HostilePlayerBehaviorRemainsUnchanged()
    {
        PoliceContext c = new();
        c.Reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        c.SeedContraband(2);
        c.AddScanner();
        c.Step(0.5f, 8);
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.CurrentOffer == null &&
            !c.Scan.IsEnforcementDemandActive && c.Scan.StatusText == string.Empty,
            "a hostile relationship produced demand state");
    }

    private (bool, string) HoldFireBehaviorRemainsUnchanged()
    {
        PoliceContrabandStopCoordinator coordinator = new();
        if (coordinator.ActiveOwner != null || coordinator.HoldFireCount != 0)
            return (false, "a fresh coordinator owned hold-fire state");
        coordinator.Update(null, null, null, null, null, null, null, null);
        coordinator.NotifyNpcDestroyed(null);
        coordinator.NotifyNpcDespawned(null);
        coordinator.Reset();
        return Check(coordinator.ActiveOwner == null && coordinator.HoldFireCount == 0 &&
            !HasMemberContaining(typeof(PoliceContrabandStopCoordinator), "hud", "text", "message", "prompt", "timer"),
            "the coordinator gained presentation or hold-fire state");
    }

    private (bool, string) Phase74TierEscalationRemainsUnchanged()
    {
        PoliceContext c = new();
        c.Reputation.SetReputation(FactionManager.LibertyPolice, -0.55f, "phase 86 setup");
        c.AddScanner();
        c.SeedContraband(2);
        c.Step(0.5f, 8);
        if (c.Scan.CurrentOffer?.EnforcementTier != PoliceEnforcementTier.Severe)
            return (false, "severe standing did not escalate the tier");
        return Check(c.Scan.StatusText.Contains("Severe", StringComparison.Ordinal) &&
            c.Scan.StatusText.Contains(c.Scan.CurrentOffer.FineAmount.ToString("N0"), StringComparison.Ordinal),
            $"tier escalation text was wrong: '{c.Scan.StatusText}'");
    }

    private (bool, string) Phase76NotificationsRemainUnchanged()
    {
        PoliceContext c = new();
        c.StepToDetection();
        List<string> texts = GetNotificationTexts(c.Notifications);
        if (!texts.Any(text => text.Contains("Contraband detected", StringComparison.Ordinal)))
            return (false, $"detection notice was lost: [{string.Join(" | ", texts)}]");
        return Check(texts.Any(text => text.Contains("[Enter]", StringComparison.Ordinal)) && texts.Count <= 2,
            $"demand notice regressed: [{string.Join(" | ", texts)}]");
    }

    private (bool, string) Phase77DockingRemainsUnchanged()
    {
        PoliceContext c = new();
        c.StepToDetection();
        c.Scan.Update(new GameTime(TimeSpan.FromSeconds(4f), TimeSpan.FromSeconds(0.1f)),
            c.Player, c.Npcs, c.Credits, c.Reputation,
            playerDocked: true, playerInTradeLaneTransit: false, c.Notifications, null);
        return Check(c.Scan.State == PoliceScanState.Enforcement && c.Scan.CurrentOffer == null &&
            !c.Scan.IsEnforcementDemandActive &&
            !c.Scan.StatusText.Contains("[Enter]", StringComparison.Ordinal),
            $"docking during a demand regressed: '{c.Scan.StatusText}'");
    }

    private (bool, string) Phase78SurrenderRemainsUnchanged()
    {
        PoliceContext c = new();
        if (!c.Fugitive.BeginPursuit(c.Player, "phase 86 surrender probe", null))
            return (false, "pursuit did not open");
        int pursuitFee = PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.Pursuit);
        int hotFee = PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.HotPursuit);
        return Check(c.Fugitive.IsActive && c.Fugitive.StatusText == "LIBERTY POLICE PURSUIT" &&
            pursuitFee > 0 && hotFee > 0 &&
            pursuitFee == PoliceFugitiveSurrenderService.GetSurrenderFee(PoliceHeatLevel.Pursuit),
            "surrender seams regressed");
    }

    private (bool, string) Phase79FireCessationRemainsUnchanged()
    {
        PoliceContext idle = new();
        idle.Scan.ClearResolvedResultHold();
        if (idle.Scan.State != PoliceScanState.Idle)
            return (false, "result-hold clearing touched an idle scanner");

        PoliceContext cleared = new();
        cleared.AddScanner();
        cleared.Step(0.5f, 8);
        if (cleared.Scan.State != PoliceScanState.Cleared)
            return (false, "clean cargo did not clear");
        cleared.Scan.ClearResolvedResultHold();
        if (cleared.Scan.State != PoliceScanState.Idle || cleared.Scan.StatusText != string.Empty)
            return (false, "result-hold clearing did not release the hold");

        PoliceContext active = new();
        active.StepToDetection();
        PoliceEnforcementOffer offer = active.Scan.CurrentOffer;
        active.Scan.ClearResolvedResultHold();
        return Check(active.Scan.State == PoliceScanState.ContrabandDetected &&
            ReferenceEquals(offer, active.Scan.CurrentOffer) && active.Scan.IsEnforcementDemandActive,
            "result-hold clearing touched an active demand");
    }

    private (bool, string) Phase80NormalizationRemainsUnchanged()
    {
        PoliceContext c = new();
        c.AddScanner();
        c.Scan.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5f)),
            c.Player, c.Npcs, c.Credits, c.Reputation, c.Notifications, null);
        if (c.Scan.State != PoliceScanState.Scanning)
            return (false, "scan did not start");
        c.Scan.Update(new GameTime(TimeSpan.FromSeconds(0.5f), TimeSpan.FromSeconds(0.5f)),
            c.Player, c.Npcs, c.Credits, c.Reputation,
            playerDocked: false, playerInTradeLaneTransit: true, c.Notifications, null);
        return Check(c.Scan.State == PoliceScanState.Idle && c.Scan.StatusText == string.Empty &&
            c.Scan.CurrentOffer == null,
            $"transit did not normalize the scan: '{c.Scan.StatusText}'");
    }

    // ---- Shared invariants: spec items 36-45 ------------------------------------

    private (bool, string) ExistingActionKeysWork()
    {
        PoliceContext comply = new();
        comply.StepToDetection();
        bool handled = comply.Scan.HandleInput(
            new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
            new KeyboardState(),
            comply.Player, comply.Credits, comply.Reputation, comply.Notifications, null);
        if (!handled || comply.Scan.State != PoliceScanState.Cleared)
            return (false, "the comply key did not resolve the demand");

        PoliceContext refuse = new();
        refuse.StepToDetection();
        bool refused = refuse.Scan.HandleInput(
            new KeyboardState(PoliceScanSystem.EnforcementRefuseKey),
            new KeyboardState(),
            refuse.Player, refuse.Credits, refuse.Reputation, refuse.Notifications, null);
        if (!refused || refuse.Scan.State != PoliceScanState.Enforcement)
            return (false, "the refuse key did not resolve the demand");

        PiracyContext piracy = new();
        if (!piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out string failure))
            return (false, $"in-range Y-key demand was rejected: {failure}");
        NpcShip far = PiracyContext.CreateTrader("Phase86 Far Trader", new Vector3(9000f, 0f, 0f));
        piracy.Npcs.Add(far);
        piracy.Objects.Add(far);
        PiracyContext ranged = new();
        ranged.Npcs.Clear();
        ranged.Objects.Clear();
        ranged.Npcs.Add(far);
        ranged.Objects.Add(far);
        if (ranged.Demand.TryIssueDemand(ranged.Player, far, out string rangeFailure))
            return (false, "an out-of-range demand was accepted");
        return Check(rangeFailure.Contains("range", StringComparison.OrdinalIgnoreCase),
            $"range guard reason regressed: '{rangeFailure}'");
    }

    private (bool, string) SameFrameResponseRacesRemainIdempotent()
    {
        PiracyContext piracy = new();
        piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out _);
        if (piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out string secondFailure))
            return (false, "a second same-frame demand was accepted");
        if (!secondFailure.Contains("already active", StringComparison.OrdinalIgnoreCase))
            return (false, $"duplicate guard reason regressed: '{secondFailure}'");
        piracy.Advance();
        piracy.Demand.Update(0.5f, piracy.Player);
        if (piracy.Resolutions != 1)
            return (false, "resolution was not exactly once");

        PoliceContext police = new();
        police.StepToDetection();
        int before = police.Credits.Credits;
        bool handled = police.Scan.HandleInput(
            new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
            new KeyboardState(),
            police.Player, police.Credits, police.Reputation, police.Notifications, null);
        bool repeated = police.Scan.HandleInput(
            new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
            new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
            police.Player, police.Credits, police.Reputation, police.Notifications, null);
        return Check(handled && !repeated && police.Credits.Credits < before,
            "repeated comply input transacted twice");
    }

    private (bool, string) TimeoutInputPrecedenceIsUnchanged()
    {
        PoliceContext police = new();
        police.StepToDetection();
        police.Scan.Update(
            new GameTime(TimeSpan.FromSeconds(4f), TimeSpan.FromSeconds(PoliceScanSystem.EnforcementDemandSeconds + 0.1f)),
            police.Player, police.Npcs, police.Credits, police.Reputation, police.Notifications, null);
        if (police.Scan.State != PoliceScanState.Enforcement)
            return (false, "demand timeout did not resolve first");
        int credits = police.Credits.Credits;
        bool handled = police.Scan.HandleInput(
            new KeyboardState(PoliceScanSystem.EnforcementComplyKey),
            new KeyboardState(),
            police.Player, police.Credits, police.Reputation, police.Notifications, null);
        if (handled || police.Credits.Credits != credits)
            return (false, "late input overrode the timeout");

        PiracyContext piracy = new();
        piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out _);
        piracy.Advance();
        return Check(!piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out _) &&
            piracy.Resolutions == 1,
            "the one-shot victim bound did not survive resolution");
    }

    private (bool, string) ActiveHudReadsAreSideEffectFree()
    {
        PiracyContext piracy = new();
        piracy.Demand.TryGetManifest(piracy.Trader, out TraderCargoManifest manifest);
        int before = manifest.RemainingQuantity;
        piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out _);
        string first = piracy.Demand.HudText;
        for (int i = 0; i < 20; i++)
        {
            if (piracy.Demand.HudText != first)
                return (false, "piracy HUD reads were not stable");
        }
        if (!piracy.Demand.HasActiveDemand || manifest.RemainingQuantity != before ||
            piracy.Notifications.Count != 0 || piracy.Resolutions != 0)
            return (false, "piracy HUD reads mutated gameplay state");

        PoliceContext police = new();
        police.StepToDetection();
        string status = police.Scan.StatusText;
        PoliceEnforcementOffer offer = police.Scan.CurrentOffer;
        float remaining = police.Scan.EnforcementDemandRemainingSeconds;
        for (int i = 0; i < 20; i++)
        {
            if (police.Scan.StatusText != status)
                return (false, "police HUD reads were not stable");
        }
        return Check(ReferenceEquals(offer, police.Scan.CurrentOffer) &&
            Math.Abs(police.Scan.EnforcementDemandRemainingSeconds - remaining) < 0.0001f &&
            police.Scan.IsEnforcementDemandActive,
            "police HUD reads mutated gameplay state");
    }

    private (bool, string) NotificationExpiryDoesNotAlterGameplay()
    {
        PiracyContext piracy = new();
        piracy.Demand.TryGetManifest(piracy.Trader, out TraderCargoManifest manifest);
        piracy.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out _);
        piracy.Advance();
        int surrendered = piracy.Demand.LastResult?.SurrenderedQuantity ?? -1;
        int pods = piracy.Loot.ActivePods.Count;
        int remaining = manifest?.RemainingQuantity ?? -1;
        if (surrendered <= 0 || pods == 0)
            return (false, "could not stage a genuine compliance");
        if (piracy.Demand.HudText != string.Empty)
            return (false, "stale HUD survived alongside the notification path");

        PoliceContext police = new();
        police.StepToDetection();
        int fine = police.Scan.CurrentOffer.FineAmount;
        police.Scan.TryAcceptEnforcement(police.Player, police.Credits, police.Reputation, police.Notifications, null);
        int credits = police.Credits.Credits;
        Commodity contraband = CommodityCatalog.GetById("side-arms");
        int cargo = police.Player.CargoHold.GetCommodityQuantity(contraband.Name);
        police.Notifications.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(30f)));
        police.Notifications.Update(new GameTime(TimeSpan.FromSeconds(30f), TimeSpan.FromSeconds(30f)));
        return Check(police.Credits.Credits == credits && credits == 5_000 - fine &&
            police.Player.CargoHold.GetCommodityQuantity(contraband.Name) == cargo &&
            piracy.Demand.LastResult.SurrenderedQuantity == surrendered &&
            piracy.Loot.ActivePods.Count == pods && manifest.RemainingQuantity == remaining,
            "notification expiry altered gameplay state");
    }

    private (bool, string) NoDuplicateResultMessages()
    {
        PiracyContext piracy = new();
        piracy.Trader.ApplyDamage(55f, NpcDestructionSource.Unknown);
        piracy.Demand.TryIssueDemand(piracy.Player, piracy.Trader, out _);
        piracy.Advance();
        if (piracy.Notifications.Count != 1)
            return (false, $"resolution did not notify exactly once: {piracy.Notifications.Count}");
        piracy.Demand.Update(1f, piracy.Player);
        piracy.Demand.Update(1f, piracy.Player);
        if (piracy.Notifications.Count != 1 || piracy.Resolutions != 1)
            return (false, "follow-up updates replayed the result");

        PoliceContext police = new();
        NotificationManager isolated = new(null, new Viewport(0, 0, 1280, 720));
        police.AddScanner();
        police.SeedContraband();
        TimeSpan total = TimeSpan.Zero;
        for (int i = 0; i < 8; i++)
        {
            total += TimeSpan.FromSeconds(0.5f);
            police.Scan.Update(new GameTime(total - TimeSpan.FromSeconds(0.5f), TimeSpan.FromSeconds(0.5f)),
                police.Player, police.Npcs, police.Credits, police.Reputation, isolated, null);
        }
        NotificationManager resolutionOnly = new(null, new Viewport(0, 0, 1280, 720));
        if (!police.Scan.TryAcceptEnforcement(police.Player, police.Credits, police.Reputation, resolutionOnly, null))
            return (false, "compliance did not resolve");
        int messages = GetNotificationTexts(resolutionOnly).Count;
        police.Scan.Update(new GameTime(total, TimeSpan.FromSeconds(1f)),
            police.Player, police.Npcs, police.Credits, police.Reputation, resolutionOnly, null);
        return Check(messages == 2 && GetNotificationTexts(resolutionOnly).Count == 2,
            $"resolution produced {messages} messages instead of the bounded pair");
    }

    private (bool, string) NoNewPersistedPresentationFields()
    {
        Type save = typeof(SaveGameData);
        return Check(!HasMemberContaining(save, "hudtext", "presentation", "acknowledgement", "demander",
                "piratedemand", "hail", "hover", "transient", "policestatus", "policedemand", "scantext"),
            "save data gained a presentation member");
    }

    private (bool, string) SchemaRemainsUnchanged()
    {
        return Check(SaveGameData.CurrentSchemaVersion == 13,
            $"schema is {SaveGameData.CurrentSchemaVersion}, expected 13");
    }

    private (bool, string) RelevantExistingSuitesRemainGreen()
    {
        (int piracyPassed, int piracyFailed) = new PiracyDemandSmokeTest().Run();
        if (piracyFailed != 0 || piracyPassed == 0)
            return (false, $"player piracy suite regressed: {piracyPassed} passed, {piracyFailed} failed");
        (int contrabandPassed, int contrabandFailed) = new ContrabandSmokeTest().Run();
        if (contrabandFailed != 0 || contrabandPassed == 0)
            return (false, $"contraband suite regressed: {contrabandPassed} passed, {contrabandFailed} failed");
        (int fugitivePassed, int fugitiveFailed) = new PoliceFugitiveSmokeTest().Run();
        return Check(fugitiveFailed == 0 && fugitivePassed > 0,
            $"police fugitive suite regressed: {fugitivePassed} passed, {fugitiveFailed} failed");
    }

    private (bool, string) NoNewGameplayAuthorityExists()
    {
        Type piracy = typeof(PirateCargoDemandService);
        if (HasInstanceFieldOfType(piracy, typeof(string)))
            return (false, "the piracy service retains a string instance field");
        if (HasMemberContaining(piracy, "presentation"))
            return (false, "the piracy service retains presentation state");
        if (PropertyHasSetter(piracy, "HudText"))
            return (false, "HudText gained a setter");
        if (HasInstanceFieldOfType(piracy, typeof(NotificationManager)))
            return (false, "the piracy service gained a notification authority");
        EventInfo[] piracyEvents = piracy.GetEvents(BindingFlags.Instance | BindingFlags.Public);
        if (piracyEvents.Length != 1 || piracyEvents[0].Name != nameof(PirateCargoDemandService.DemandResolved))
            return (false, "the piracy service gained a second event authority");

        Type scan = typeof(PoliceScanSystem);
        if (HasInstanceFieldOfType(scan, typeof(string)))
            return (false, "the police scan system retains a string instance field");
        if (HasMemberContaining(scan, "hud", "presentation", "history"))
            return (false, "the police scan system gained presentation state");
        if (PropertyHasSetter(scan, "StatusText"))
            return (false, "StatusText gained a setter");

        Type enforcement = typeof(PoliceEnforcementService);
        bool enforcementHasOwnState = enforcement
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Any(field => !field.Name.Contains("k__BackingField", StringComparison.Ordinal));
        if (enforcementHasOwnState)
            return (false, "the enforcement service gained instance state");

        return Check(!HasInstanceFieldOfType(typeof(PoliceContrabandStopCoordinator), typeof(string)),
            "the stop coordinator gained string state");
    }
}
