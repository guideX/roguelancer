#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Headless Phase 26 coverage for the shared faction access policy. It uses
/// the same EquipmentDealer/ShipDealer seams called by the legacy dock UI and
/// the 3D station service routes.
/// </summary>
internal sealed class FactionAccessSmokeTest
{
    private int _passed;
    private int _failed;

    public (int Passed, int Failed) Run()
    {
        Check("ordinary equipment remains available", OrdinaryEquipmentAvailable);
        Check("Friendly equipment locks below threshold", FriendlyEquipmentLocksBelowThreshold);
        Check("current reputation is shown", CurrentStandingIsShown);
        Check("required reputation is shown", RequiredStandingIsShown);
        Check("equipment uses its controlling faction", EquipmentUsesControllingFaction);
        Check("different faction contexts produce different access", FactionsProduceDifferentAccess);
        Check("threshold unlocks equipment", ThresholdUnlocksEquipment);
        Check("purchase revalidates live reputation", PurchaseRevalidatesReputation);
        Check("locked purchase does not charge credits", LockedPurchaseDoesNotCharge);
        Check("locked purchase does not grant equipment", LockedPurchaseDoesNotGrant);
        Check("valid purchase charges exactly once", ValidPurchaseChargesOnce);
        Check("valid purchase grants exactly once", ValidPurchaseGrantsOnce);
        Check("repeated input edge cannot double-purchase", RepeatedInputDoesNotDoublePurchase);
        Check("reputation loss does not confiscate equipment", ReputationLossKeepsOwnership);
        Check("owned equipment remains usable after reputation loss", OwnedEquipmentRemainsUsable);
        Check("temporary hostility blocks valid purchase", TemporaryHostilityBlocksPurchase);
        Check("temporary hostility expiry restores access", TemporaryHostilityExpiryRestoresAccess);
        Check("temporary hostility remains independent of persistent reputation", TemporaryHostilityIsIndependent);
        Check("service gate refuses sufficiently hostile standing", ServiceGateRefusesHostileStanding);
        Check("service becomes available after reputation improvement", ServiceReopensAfterImprovement);
        Check("Phase 25 bribe is reflected live", BribeUpdatesLiveAccess);
        Check("bribe ceiling cannot unlock Friendly equipment", BribeCeilingCannotUnlockFriendly);
        Check("mission reputation gates remain unchanged", MissionGatesRemainAuthoritative);
        Check("save/load preserves restricted ownership", SaveLoadPreservesRestrictedOwnership);
        Check("save/load recalculates new access", SaveLoadRecalculatesAccess);
        Check("reset restores baseline eligibility", ResetRestoresBaselineEligibility);
        Check("3D station route uses authoritative context", ThreeDRouteUsesAuthoritativeContext);
        Check("legacy station route uses authoritative purchase", LegacyRouteUsesAuthoritativePurchase);
        Check("ship dealer service uses station faction", ShipDealerUsesStationFaction);
        Check("prices do not vary with reputation", ReputationDoesNotChangePrice);

        Console.WriteLine($"[FACTION ACCESS SMOKE] RESULT: {_passed} passed, {_failed} failed");
        return (_passed, _failed);
    }

    private void Check(string label, Func<bool> assertion)
    {
        try
        {
            if (RunSilenced(assertion))
            {
                _passed++;
                Console.WriteLine($"[FACTION ACCESS SMOKE] PASS {label}");
            }
            else
            {
                _failed++;
                Console.WriteLine($"[FACTION ACCESS SMOKE] FAIL {label}: assertion returned false");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"[FACTION ACCESS SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static bool OrdinaryEquipmentAvailable()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.55f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        EquipmentDefinition ordinary = EquipmentCatalog.GetById("basic_missile_launcher")!;
        FactionAccessResult access = dealer.GetEquipmentAccess(ordinary);
        return access.IsAllowed && !ordinary.MinimumReputation.HasValue;
    }

    private static bool FriendlyEquipmentLocksBelowThreshold()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        FactionAccessResult access = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice)
            .GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon"));
        return !access.IsAllowed && access.MinimumStanding == ReputationManager.FriendlyThreshold;
    }

    private static bool CurrentStandingIsShown()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        FactionAccessResult access = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice)
            .GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon"));
        return access.BuildCurrentStandingLine().Contains("-0.25", StringComparison.Ordinal) &&
            access.BuildCurrentStandingLine().Contains("UNFRIENDLY", StringComparison.Ordinal);
    }

    private static bool RequiredStandingIsShown()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        FactionAccessResult access = dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon"));
        return access.BuildRequirementLine(reputation).Contains("FRIENDLY", StringComparison.Ordinal) &&
            access.BuildRequirementLine(reputation).Contains("+0.20", StringComparison.Ordinal);
    }

    private static bool EquipmentUsesControllingFaction()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        FactionAccessResult policeAccess = dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon"));
        FactionAccessResult rogueAccess = dealer.GetEquipmentAccess(EquipmentCatalog.GetById("rogue_blaster"));
        return policeAccess.FactionId == FactionManager.LibertyPolice &&
            rogueAccess.FactionId == FactionManager.LibertyRogues;
    }

    private static bool FactionsProduceDifferentAccess()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        reputation.SetReputation(FactionManager.LibertyRogues, 0.35f, "phase 26 smoke setup");
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        bool pulseLocked = !dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed;
        bool rogueOpen = dealer.GetEquipmentAccess(EquipmentCatalog.GetById("rogue_blaster")).IsAllowed;
        reputation.SetReputation(FactionManager.LibertyRogues, -0.25f, "phase 26 smoke setup");
        bool rogueLocks = !dealer.GetEquipmentAccess(EquipmentCatalog.GetById("rogue_blaster")).IsAllowed;
        return pulseLocked && rogueOpen && rogueLocks;
    }

    private static bool ThresholdUnlocksEquipment()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold, "mission reward");
        return dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed;
    }

    private static bool PurchaseRevalidatesReputation()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        Ship ship = new(Vector3.Zero);
        PlayerCredits credits = new(pulse.Price + 10);
        reputation.SetReputation(FactionManager.LibertyPolice, -0.25f, "live reputation changed");
        return !dealer.TryBuyEquipment(pulse, credits, ship, out string message) &&
            message.Contains("locked", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LockedPurchaseDoesNotCharge()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        Ship ship = new(Vector3.Zero);
        PlayerCredits credits = new(pulse.Price + 10);
        int before = credits.Credits;
        dealer.TryBuyEquipment(pulse, credits, ship, out _);
        return credits.Credits == before;
    }

    private static bool LockedPurchaseDoesNotGrant()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        Ship ship = new(Vector3.Zero);
        dealer.TryBuyEquipment(pulse, new PlayerCredits(pulse.Price + 10), ship, out _);
        return ship.Loadout.GetOwnedCount(pulse.Id) == 0;
    }

    private static bool ValidPurchaseChargesOnce()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        PlayerCredits credits = new(pulse.Price * 2);
        int before = credits.Credits;
        return dealer.TryBuyEquipment(pulse, credits, new Ship(Vector3.Zero), out _) &&
            credits.Credits == before - pulse.Price;
    }

    private static bool ValidPurchaseGrantsOnce()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        Ship ship = new(Vector3.Zero);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        return dealer.TryBuyEquipment(pulse, new PlayerCredits(pulse.Price), ship, out _) &&
            ship.Loadout.GetOwnedCount(pulse.Id) == 1;
    }

    private static bool RepeatedInputDoesNotDoublePurchase()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        PlayerCredits credits = new(pulse.Price * 2);
        Ship ship = new(Vector3.Zero);
        StationDockUI ui = CreateLegacyUi(dealer, reputation, credits, StationFor(FactionManager.LibertyPolice));
        KeyboardState current = new(Keys.Enter);
        KeyboardState previous = new();
        bool first = ui.HandleEquipmentDealerInput(current, previous, credits, ship);
        bool second = ui.HandleEquipmentDealerInput(current, current, credits, ship);
        return first && !second && credits.Credits == pulse.Price && ship.Loadout.GetOwnedCount(pulse.Id) == 1;
    }

    private static bool ReputationLossKeepsOwnership()
    {
        (ReputationManager reputation, EquipmentDealer dealer, EquipmentDefinition pulse, Ship ship) = BuyRestrictedPulse();
        reputation.SetReputation(FactionManager.LibertyPolice, -0.25f, "mission failure");
        return ship.Loadout.GetOwnedCount(pulse.Id) == 1 && dealer.GetEquipmentAccess(pulse).IsAllowed == false;
    }

    private static bool OwnedEquipmentRemainsUsable()
    {
        (ReputationManager reputation, EquipmentDealer dealer, EquipmentDefinition pulse, Ship ship) = BuyRestrictedPulse();
        if (!dealer.TryMountEquipment(pulse, ship, out _))
            return false;
        reputation.SetReputation(FactionManager.LibertyPolice, -0.25f, "mission failure");
        if (!dealer.TryUnmountEquipment(pulse, ship, out _))
            return false;
        return dealer.TryMountEquipment(pulse, ship, out _) &&
            ship.GetPrimaryMountedGun()?.Id == pulse.Id;
    }

    private static bool TemporaryHostilityBlocksPurchase()
    {
        (ReputationManager reputation, EquipmentDealer dealer, EquipmentDefinition pulse, Ship ship) = BuyRestrictedPulseSetup();
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        PlayerCredits credits = new(pulse.Price);
        bool purchased = dealer.TryBuyEquipment(pulse, credits, ship, out string message);
        return !purchased && message.Contains("hostile", StringComparison.OrdinalIgnoreCase) &&
            credits.Credits == pulse.Price;
    }

    private static bool TemporaryHostilityExpiryRestoresAccess()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        return dealer.GetEquipmentAccess(pulse).IsAllowed &&
            dealer.TryBuyEquipment(pulse, new PlayerCredits(pulse.Price), new Ship(Vector3.Zero), out _);
    }

    private static bool TemporaryHostilityIsIndependent()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
        bool stillPositive = Nearly(reputation.GetStanding(FactionManager.LibertyPolice), ReputationManager.FriendlyThreshold);
        bool stillHostile = reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        reputation.UpdateTemporaryHostility(TemporaryHostilityManager.DefaultDurationSeconds);
        return stillPositive && stillHostile && !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
    }

    private static bool ServiceGateRefusesHostileStanding()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        EquipmentDealer equipment = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        ShipDealer ships = new(reputation);
        ships.SetDockedStation(StationFor(FactionManager.LibertyPolice));
        return !equipment.CanUseService(out string equipmentMessage) &&
            !ships.CanUseService(out string shipMessage) &&
            equipmentMessage.Contains("locked", StringComparison.OrdinalIgnoreCase) &&
            shipMessage.Contains("locked", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ServiceReopensAfterImprovement()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        reputation.AdjustReputation(FactionManager.LibertyPolice, 0.15f, ReputationChangeReason.MissionCompleted);
        return dealer.CanUseService(out _) && Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.55f);
    }

    private static bool BribeUpdatesLiveAccess()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        PlayerCredits credits = new(100_000);
        BarNpc contact = BarNpc.GenerateBarNpcs().First(npc => npc.Name == "Elena Vasquez");
        FactionBribeOffer offer = FactionBribeService.GetOfferForContact(
            contact,
            FactionManager.LibertyPolice,
            reputation,
            credits)!;
        if (offer == null || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;
        FactionAccessResult access = dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon"));
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), FactionBribeService.BriberyCeiling) &&
            Nearly(access.CurrentStanding, FactionBribeService.BriberyCeiling);
    }

    private static bool BribeCeilingCannotUnlockFriendly()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        PlayerCredits credits = new(100_000);
        BarNpc contact = BarNpc.GenerateBarNpcs().First(npc => npc.Name == "Elena Vasquez");
        FactionBribeOffer offer = FactionBribeService.GetOfferForContact(contact, FactionManager.LibertyPolice, reputation, credits)!;
        if (offer == null || !FactionBribeService.TryPurchase(offer, reputation, credits, out _, out _))
            return false;
        return !dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed &&
            reputation.GetStanding(FactionManager.LibertyPolice) < ReputationManager.FriendlyThreshold;
    }

    private static bool MissionGatesRemainAuthoritative()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, FactionBribeService.BriberyCeiling);
        MissionManager missions = new(new PlayerCredits(0), null, reputation);
        Mission mission = CreateMission(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        bool locked = !missions.GetMissionEligibility(mission).IsEligible;
        reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold, "mission reward");
        return locked && missions.GetMissionEligibility(mission).IsEligible;
    }

    private static bool SaveLoadPreservesRestrictedOwnership()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguelancer-phase26-{Guid.NewGuid():N}.json");
        try
        {
            (ReputationManager reputation, EquipmentDealer dealer, EquipmentDefinition pulse, Ship ship) = BuyRestrictedPulse();
            if (!dealer.TryMountEquipment(pulse, ship, out _))
                return false;

            SaveGameManager manager = new(path);
            SaveGameData data = new()
            {
                PlayerCredits = 10_000,
                OwnedEquipment = manager.CaptureOwnedEquipment(ship.Loadout),
                MountedEquipment = manager.CaptureMountedEquipment(ship.Loadout),
                FactionReputation = manager.CaptureReputation(reputation)
            };
            if (!manager.TrySave(data, out _) || !manager.TryLoad(out SaveGameData loaded, out _))
                return false;

            ShipLoadout restored = manager.BuildLoadout(loaded, out _);
            ReputationManager restoredReputation = new(new FactionManager());
            manager.ApplyReputation(restoredReputation, loaded);
            return restored.GetOwnedCount(pulse.Id) == 1 &&
                restored.GetMountedCount(pulse.Id) == 1 &&
                Nearly(restoredReputation.GetStanding(FactionManager.LibertyPolice), ReputationManager.FriendlyThreshold);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static bool SaveLoadRecalculatesAccess()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguelancer-phase26-access-{Guid.NewGuid():N}.json");
        try
        {
            ReputationManager source = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
            SaveGameManager manager = new(path);
            SaveGameData data = new() { FactionReputation = manager.CaptureReputation(source) };
            if (!manager.TrySave(data, out _) || !manager.TryLoad(out SaveGameData loaded, out _))
                return false;
            ReputationManager restored = new(new FactionManager());
            manager.ApplyReputation(restored, loaded);
            EquipmentDealer dealer = CreateEquipmentDealer(restored, FactionManager.LibertyPolice);
            bool openAtSavedStanding = dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed;
            restored.SetReputation(FactionManager.LibertyPolice, -0.25f, "current save state changed");
            bool lockedAtCurrentStanding = !dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed;
            return openAtSavedStanding && lockedAtCurrentStanding;
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static bool ResetRestoresBaselineEligibility()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        if (!dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed)
            return false;
        reputation.ResetToNewGame();
        return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f) &&
            !dealer.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed;
    }

    private static bool ThreeDRouteUsesAuthoritativeContext()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        Station station = StationFor(FactionManager.LibertyPolice);
        Ship ship = new(Vector3.Zero);
        StationSession session = StationSession.CreateRealDocked(station, ship, 1);
        EquipmentDealer equipment = new(reputation);
        ShipDealer ships = new(reputation);
        equipment.SetDockedStation(session.DockedStation);
        ships.SetDockedStation(session.DockedStation);
        return session.IsRealDockedSession &&
            equipment.CurrentStation == station &&
            ships.CurrentStation == station &&
            !equipment.GetEquipmentAccess(EquipmentCatalog.GetById("liberty_pulse_cannon")).IsAllowed;
    }

    private static bool LegacyRouteUsesAuthoritativePurchase()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        EquipmentDealer dealer = new(reputation);
        PlayerCredits credits = new(pulse.Price);
        Ship ship = new(Vector3.Zero);
        StationDockUI ui = CreateLegacyUi(dealer, reputation, credits, StationFor(FactionManager.LibertyPolice));
        int before = credits.Credits;
        bool result = ui.HandleEquipmentDealerInput(new KeyboardState(Keys.Enter), new KeyboardState(), credits, ship);
        return !result && credits.Credits == before && ship.Loadout.GetOwnedCount(pulse.Id) == 0;
    }

    private static bool ShipDealerUsesStationFaction()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.70f);
        ShipDealer dealer = new(reputation);
        Station police = StationFor(FactionManager.LibertyPolice);
        Station rogue = StationFor(FactionManager.LibertyRogues);
        dealer.SetDockedStation(police);
        bool policeBlocked = !dealer.GetServiceAccess().IsAllowed;
        reputation.SetReputation(FactionManager.LibertyRogues, -0.70f, "phase 26 smoke setup");
        dealer.SetDockedStation(rogue);
        bool rogueBlocked = !dealer.GetServiceAccess().IsAllowed;
        reputation.SetReputation(FactionManager.LibertyRogues, -0.55f, "mission reward");
        return policeBlocked && rogueBlocked && dealer.GetServiceAccess().IsAllowed &&
            dealer.GetServiceAccess().FactionId == FactionManager.LibertyRogues;
    }

    private static bool ReputationDoesNotChangePrice()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, -0.25f);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        int price = pulse.Price;
        reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold, "mission reward");
        return dealer.GetResaleValue(pulse) == (int)MathF.Floor(price * EquipmentDealer.ResaleRate) &&
            pulse.Price == price;
    }

    private static (ReputationManager Reputation, EquipmentDealer Dealer, EquipmentDefinition Pulse, Ship Ship) BuyRestrictedPulse()
    {
        (ReputationManager reputation, EquipmentDealer dealer, EquipmentDefinition pulse, Ship ship) = BuyRestrictedPulseSetup();
        if (!dealer.TryBuyEquipment(pulse, new PlayerCredits(pulse.Price), ship, out string message))
            throw new InvalidOperationException(message);
        return (reputation, dealer, pulse, ship);
    }

    private static (ReputationManager Reputation, EquipmentDealer Dealer, EquipmentDefinition Pulse, Ship Ship) BuyRestrictedPulseSetup()
    {
        ReputationManager reputation = NewReputation(FactionManager.LibertyPolice, ReputationManager.FriendlyThreshold);
        EquipmentDealer dealer = CreateEquipmentDealer(reputation, FactionManager.LibertyPolice);
        EquipmentDefinition pulse = EquipmentCatalog.GetById("liberty_pulse_cannon")!;
        Ship ship = new(Vector3.Zero);
        ship.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
        return (reputation, dealer, pulse, ship);
    }

    private static EquipmentDealer CreateEquipmentDealer(ReputationManager reputation, string factionId)
    {
        EquipmentDealer dealer = new(reputation);
        dealer.SetDockedStation(StationFor(factionId));
        return dealer;
    }

    private static StationDockUI CreateLegacyUi(
        EquipmentDealer equipmentDealer,
        ReputationManager reputation,
        PlayerCredits credits,
        Station station)
    {
        StationDockUI ui = new(
            null,
            null,
            new ShipDealer(reputation),
            new CommodityDealer(),
            new MissionManager(credits, null, reputation),
            reputation,
            equipmentDealer);
        if (!ui.DockAtStation(station))
            throw new InvalidOperationException(ui.LastDockingDeniedReason);
        ui.NavigateToArea(StationArea.Dealer);
        return ui;
    }

    private static Station StationFor(string factionId)
    {
        return new Station(new StationConfig
        {
            Description = $"Phase 26 {factionId} station",
            FactionId = factionId,
            Radius = 100f,
            DockingRange = 100f,
            DockingApproachDistance = 50f
        }, null);
    }

    private static ReputationManager NewReputation(string factionId, float value)
    {
        ReputationManager reputation = new(new FactionManager());
        reputation.SetReputation(factionId, value, "phase 26 smoke setup");
        return reputation;
    }

    private static Mission CreateMission(string factionId, float minimumStanding)
    {
        return new Mission(
            MissionType.Delivery,
            MissionDifficulty.Easy,
            "Phase 26 mission",
            "Destination",
            1_000,
            0f,
            "Phase 26 reputation gate.",
            factionId)
        {
            MinimumEmployerReputation = minimumStanding
        };
    }

    private static bool Nearly(float left, float right) => Math.Abs(left - right) < ReputationManager.Precision;

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter previous = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            return action();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Test cleanup must not hide the assertion result.
        }
    }
}
