using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Authoritative player mission state. Phase 11 intentionally keeps one
    /// active mission and separates objective completion from reward claiming.
    /// </summary>
    public class MissionManager
    {
        private readonly List<Mission> _activeMissions = new();
        private readonly List<Mission> _completedMissions = new();
        private readonly HashSet<NpcShip> _countedHostileKills = new();
        private readonly Random _random = new();
        private readonly PlayerCredits _playerCredits;
        private readonly NotificationManager _notificationManager;
        private readonly MarketManager _marketManager;
        private readonly CargoHold _cargoHold;
        private readonly Dictionary<string, Mission> _freightOffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Mission> _exportOffers = new(StringComparer.OrdinalIgnoreCase);
        private ReputationManager _reputationManager;
        private MissionWaypointSystem _waypointSystem;
        private MissionWorldManager _worldManager;

        private static readonly string[] DeliveryTargets =
        {
            "Medical Supplies", "H-Fuel Cells", "Construction Materials", "Food Rations"
        };

        private static readonly string[] DeliveryDestinations =
        {
            "Fort Bush", "Trenton Outpost", "Newark Station", "Rochester Base"
        };

        private static readonly string[] BountyTargets =
        {
            "Rogue Pilot", "Pirate Commander", "Outcast Smuggler", "Corsair Raider"
        };

        public const int FreightShortageThresholdPercent = 40;
        public const int FreightMinimumShortageUnits = 10;
        public const int FreightShortageSharePercent = 25;
        public const int FreightMaximumCargoVolume = 40;
        public const int FreightMaximumUnits = 40;
        public const int FreightMaximumReward = 100_000;

        public const int ExportSurplusThresholdPercent = 150;
        public const int ExportMinimumSurplusUnits = 20;
        public const int ExportSurplusSharePercent = 40;
        public const int ExportMaximumCargoVolume = 40;
        public const int ExportMaximumUnits = 40;
        public const int ExportMaximumReward = 100_000;
        public const int MarketOpportunityMaximumEntries = 8;

        public IReadOnlyList<Mission> ActiveMissions => _activeMissions.AsReadOnly();
        public IReadOnlyList<Mission> CompletedMissions => _completedMissions.AsReadOnly();
        public Mission ActiveMission => _activeMissions.FirstOrDefault();
        public Mission UnclaimedCompletedMission => _completedMissions.FirstOrDefault(mission =>
            mission != null && mission.Status == MissionStatus.Completed && !mission.RewardPaid);

        public MissionManager(
            PlayerCredits playerCredits,
            NotificationManager notificationManager,
            ReputationManager reputationManager = null,
            MarketManager marketManager = null,
            CargoHold cargoHold = null)
        {
            _playerCredits = playerCredits;
            _notificationManager = notificationManager;
            _reputationManager = reputationManager;
            _marketManager = marketManager;
            _cargoHold = cargoHold;
        }

        public void SetReputationManager(ReputationManager reputationManager) => _reputationManager = reputationManager;
        public void SetWaypointSystem(MissionWaypointSystem waypointSystem) => _waypointSystem = waypointSystem;
        public void SetWorldManager(MissionWorldManager worldManager) => _worldManager = worldManager;
        public void ShowNotification(string message, float durationSeconds = 3f) =>
            _notificationManager?.ShowMessage(message, durationSeconds);

        public void ClearState()
        {
            foreach (Mission mission in _activeMissions.Where(candidate => candidate?.Type == MissionType.FreightContract))
                ReleaseFreightReservation(mission);

            foreach (Mission mission in _activeMissions.Where(candidate => candidate?.Type == MissionType.ExportContract))
                _cargoHold?.ReleaseMissionCargoReservation(mission.Id);

            foreach (Mission mission in _activeMissions)
                _waypointSystem?.UnregisterMission(mission);

            _activeMissions.Clear();
            _completedMissions.Clear();
            _countedHostileKills.Clear();
            _freightOffers.Clear();
            _exportOffers.Clear();
            _worldManager?.ClearState();
        }

        public void RestoreState(IEnumerable<Mission> activeMissions, IEnumerable<Mission> completedMissions)
        {
            ClearState();

            Mission restoredActive = activeMissions?.FirstOrDefault(mission => mission != null &&
                mission.Status is MissionStatus.Accepted or MissionStatus.InProgress);
            if (restoredActive != null)
            {
                restoredActive.Status = MissionStatus.InProgress;
                _activeMissions.Add(restoredActive);
                RegisterFreightReservation(restoredActive);
                _waypointSystem?.RegisterMission(restoredActive);
            }

            if (completedMissions != null)
            {
                foreach (Mission mission in completedMissions)
                {
                    if (mission == null ||
                        (mission.RewardPaid && mission.Type != MissionType.FreightContract) ||
                        mission.Status == MissionStatus.Rewarded)
                        continue;
                    if (mission.Status == MissionStatus.Available || mission.Status == MissionStatus.InProgress)
                        mission.Status = MissionStatus.Completed;
                    _completedMissions.Add(mission);
                }
            }

            Console.WriteLine($"[MISSION] Restored {_activeMissions.Count} active and {_completedMissions.Count} unclaimed/completed missions");
        }

        /// <summary>Creates the fixed board jobs from static catalog metadata.</summary>
        public List<Mission> CreateBoardMissions(Station originStation = null)
        {
            string faction = originStation?.FactionId ?? FactionManager.LibertyCorporations;
            List<Mission> missions = MissionCatalog.CreateRuntimeMissions("Mission Board", faction);
            missions = missions.Where(mission => mission != null &&
                (mission.Type != MissionType.CourierDelivery ||
                 string.Equals(mission.SourceStationName, originStation?.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            missions.AddRange(GenerateFreightContracts(originStation));
            missions.AddRange(GenerateExportContracts(originStation));
            return missions;
        }

        /// <summary>
        /// Compatibility generator used by the older navigation smoke tests.
        /// It is not used by the Phase 11 physical board.
        /// </summary>
        public Mission GenerateRandomMission(string factionId = null, Station originStation = null)
        {
            IReadOnlyList<Station> stations = _worldManager?.GetKnownStations() ?? Array.Empty<Station>();
            MissionDifficulty difficulty = (MissionDifficulty)_random.Next(4);
            MissionType type = stations.Count > 0
                ? (MissionType)_random.Next(3, 5)
                : MissionType.Bounty;

            string target;
            string destination;
            string description;
            switch (type)
            {
                case MissionType.Delivery:
                    target = DeliveryTargets[_random.Next(DeliveryTargets.Length)];
                    destination = PickDestination(stations, originStation);
                    description = $"Deliver {target} to {destination}";
                    break;
                case MissionType.Escort:
                    target = "Trade Convoy";
                    destination = PickDestination(stations, originStation);
                    description = $"Escort {target} to {destination}";
                    break;
                default:
                    target = BountyTargets[_random.Next(BountyTargets.Length)];
                    destination = originStation?.Name ?? "Last seen near local traffic lanes";
                    description = $"Destroy {target}";
                    type = MissionType.Bounty;
                    break;
            }

            int difficultyValue = (int)difficulty;
            int reward = type switch
            {
                MissionType.Delivery => 1500 + difficultyValue * 750,
                MissionType.Escort => 2200 + difficultyValue * 850,
                _ => 1800 + difficultyValue * 950
            };

            Mission mission = new Mission(type, difficulty, target, destination, reward, 0f, description, factionId)
            {
                OfferedBy = originStation?.Name ?? FactionManager.GetFactionDisplayName(factionId)
            };
            if (type == MissionType.Bounty) mission.BountyTargetFactionId = FactionManager.LibertyRogues;
            return mission;
        }

        public List<Mission> GenerateJobBoardMissions(int count, string factionId = null, Station originStation = null)
        {
            return CreateBoardMissions(originStation)
                .Take(Math.Clamp(count, 0, 10))
                .ToList();
        }

        /// <summary>
        /// Returns a deterministic, bounded snapshot of meaningful live market
        /// conditions. Reading this list never creates missions or cargo.
        /// </summary>
        public IReadOnlyList<MarketOpportunity> GetMarketOpportunities(int count = MarketOpportunityMaximumEntries)
        {
            int boundedCount = Math.Clamp(count, 0, MarketOpportunityMaximumEntries);
            if (boundedCount == 0 || _marketManager == null || _worldManager == null)
                return Array.Empty<MarketOpportunity>();

            List<MarketOpportunity> opportunities = new();
            IReadOnlyList<Station> stations = _worldManager.GetKnownStations();
            foreach (Station station in stations)
            {
                foreach (StationMarketListing listing in _marketManager.GetListingsForStation(station) ?? new List<StationMarketListing>())
                {
                    Commodity commodity = listing?.Commodity;
                    if (!IsExportCommodity(commodity) || !listing.IsAvailable || listing.BaselineStock <= 0 ||
                        listing.Stock < 0 || listing.BaseBuyPrice <= 0 || listing.BaseSellPrice <= 0)
                    {
                        continue;
                    }

                    long shortage = (long)listing.BaselineStock - listing.Stock;
                    long surplus = (long)listing.Stock - listing.BaselineStock;
                    if (shortage >= FreightMinimumShortageUnits &&
                        listing.Stock < (long)listing.BaselineStock * FreightShortageThresholdPercent / 100L)
                    {
                        long severity = Math.Clamp(shortage * 10_000L / listing.BaselineStock, 0L, 10_000L);
                        int score = (int)Math.Clamp(severity * 100L + listing.DemandLevel * 10L, 0L, int.MaxValue);
                        opportunities.Add(new MarketOpportunity(
                            MarketOpportunityType.Shortage,
                            commodity,
                            station.Name,
                            string.Empty,
                            string.Empty,
                            score,
                            (int)Math.Min(shortage, int.MaxValue),
                            "SHORTAGE",
                            Math.Max(0, listing.BuyPrice - listing.SellPrice)));
                    }

                    if (surplus >= ExportMinimumSurplusUnits &&
                        listing.Stock > (long)listing.BaselineStock * ExportSurplusThresholdPercent / 100L)
                    {
                        long severity = Math.Clamp(surplus * 10_000L / listing.BaselineStock, 0L, 10_000L);
                        int score = (int)Math.Clamp(severity * 100L + listing.DemandLevel * 10L, 0L, int.MaxValue);
                        opportunities.Add(new MarketOpportunity(
                            MarketOpportunityType.Surplus,
                            commodity,
                            station.Name,
                            string.Empty,
                            string.Empty,
                            score,
                            (int)Math.Min(surplus, int.MaxValue),
                            "SURPLUS",
                            Math.Max(0, listing.BuyPrice - listing.SellPrice)));

                        if (TryBuildExportTerms(
                                station,
                                listing,
                                out Commodity exportCommodity,
                                out Station destination,
                                out int quantity,
                                out _))
                        {
                            StationMarketListing destinationListing = _marketManager.GetListingForCommodity(destination, exportCommodity);
                            long destinationShortage = destinationListing == null
                                ? 0L
                                : Math.Max(0L, (long)destinationListing.BaselineStock - destinationListing.Stock);
                            long destinationSeverity = destinationListing?.BaselineStock > 0
                                ? destinationShortage * 10_000L / destinationListing.BaselineStock
                                : 0L;
                            int pairingScore = (int)Math.Clamp(
                                severity * 100L + destinationSeverity * 125L + (destinationListing?.DemandLevel ?? 0) * 20L,
                                0L,
                                int.MaxValue);
                            opportunities.Add(new MarketOpportunity(
                                MarketOpportunityType.Pairing,
                                exportCommodity,
                                string.Empty,
                                station.Name,
                                destination.Name,
                                pairingScore,
                                quantity,
                                "FAVORABLE SPREAD",
                                Math.Max(0, (destinationListing?.SellPrice ?? 0) - listing.BuyPrice)));
                        }
                    }
                }
            }

            return opportunities
                .OrderByDescending(opportunity => opportunity.Score)
                .ThenBy(opportunity => opportunity.CommodityName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(opportunity => opportunity.OriginStationName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(opportunity => opportunity.StationName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(opportunity => opportunity.DestinationStationName, StringComparer.OrdinalIgnoreCase)
                .Take(boundedCount)
                .ToList();
        }

        public int GetFreightReservedQuantity(Mission mission)
        {
            return mission?.Type == MissionType.FreightContract && _cargoHold != null
                ? _cargoHold.GetMissionCargoQuantity(mission.Id)
                : 0;
        }

        public int GetExportIssuedQuantity(Mission mission)
        {
            return mission?.Type == MissionType.ExportContract && _cargoHold != null
                ? _cargoHold.GetMissionCargoQuantity(mission.Id)
                : 0;
        }

        private List<Mission> GenerateFreightContracts(Station destination)
        {
            List<Mission> offers = new();
            if (destination == null || _marketManager == null)
                return offers;

            IReadOnlyList<StationMarketListing> listings = _marketManager.GetListingsForStation(destination);
            HashSet<string> eligibleKeys = new(StringComparer.OrdinalIgnoreCase);
            foreach (StationMarketListing listing in listings ?? Array.Empty<StationMarketListing>())
            {
                if (!TryBuildFreightTerms(destination, listing, out Commodity commodity, out int quantity, out int reward))
                    continue;

                string key = BuildFreightOfferKey(destination, commodity);
                eligibleKeys.Add(key);
                if (_activeMissions.Any(mission => IsMatchingFreight(mission, destination, commodity)))
                    continue;

                if (!_freightOffers.TryGetValue(key, out Mission offer) ||
                    offer == null ||
                    offer.Status != MissionStatus.Available ||
                    offer.RequiredQuantity != quantity ||
                    offer.Reward != reward)
                {
                    offer = Mission.CreateFreightContract(
                        commodity,
                        destination,
                        quantity,
                        reward,
                        destination.Config?.SystemIndex ?? 0,
                        offeredBy: $"{destination.Name} Authority",
                        factionId: destination.FactionId);
                    _freightOffers[key] = offer;
                }

                if (offer != null)
                    offers.Add(offer);
            }

            foreach (string key in _freightOffers.Keys.Where(key => !eligibleKeys.Contains(key)).ToList())
                _freightOffers.Remove(key);

            return offers;
        }

        private List<Mission> GenerateExportContracts(Station origin)
        {
            List<Mission> offers = new();
            if (origin == null || _marketManager == null || _worldManager == null)
                return offers;

            IReadOnlyList<StationMarketListing> listings = _marketManager.GetListingsForStation(origin);
            HashSet<string> eligibleKeys = new(StringComparer.OrdinalIgnoreCase);
            foreach (StationMarketListing listing in listings ?? Array.Empty<StationMarketListing>())
            {
                if (!TryBuildExportTerms(
                        origin,
                        listing,
                        out Commodity commodity,
                        out Station destination,
                        out int quantity,
                        out int reward))
                {
                    continue;
                }

                string key = BuildExportOfferKey(origin, commodity, destination);
                eligibleKeys.Add(key);
                if (_activeMissions.Any(mission => IsMatchingExport(mission, origin, commodity, destination)))
                    continue;

                if (!_exportOffers.TryGetValue(key, out Mission offer) ||
                    offer == null ||
                    offer.Status != MissionStatus.Available ||
                    offer.RequiredQuantity != quantity ||
                    offer.Reward != reward ||
                    !string.Equals(offer.DestinationStationId, Mission.BuildStationIdentity(destination), StringComparison.OrdinalIgnoreCase))
                {
                    offer = Mission.CreateExportContract(
                        origin,
                        commodity,
                        destination,
                        quantity,
                        reward,
                        destination.Config?.SystemIndex ?? origin.Config?.SystemIndex ?? 0,
                        offeredBy: $"{origin.Name} Authority",
                        factionId: destination.FactionId);
                    _exportOffers[key] = offer;
                }

                if (offer != null)
                    offers.Add(offer);
            }

            foreach (string key in _exportOffers.Keys.Where(key => !eligibleKeys.Contains(key)).ToList())
                _exportOffers.Remove(key);

            return offers;
        }

        private bool TryBuildExportTerms(
            Station origin,
            StationMarketListing listing,
            out Commodity commodity,
            out Station destination,
            out int quantity,
            out int reward)
        {
            commodity = listing?.Commodity;
            destination = null;
            quantity = 0;
            reward = 0;
            if (origin == null || listing == null || !IsExportCommodity(commodity) ||
                !listing.IsAvailable || listing.BaseBuyPrice <= 0 || listing.BaseSellPrice <= 0 ||
                listing.BaselineStock <= 0 || listing.Stock < 0)
            {
                return false;
            }

            long surplus = (long)listing.Stock - listing.BaselineStock;
            long thresholdStock = (long)listing.BaselineStock * ExportSurplusThresholdPercent / 100L;
            if (surplus < ExportMinimumSurplusUnits || listing.Stock <= thresholdStock)
                return false;

            long requested = (surplus * ExportSurplusSharePercent + 99L) / 100L;
            int volumeBound = ExportMaximumCargoVolume / commodity.VolumePerUnit;
            long bounded = Math.Min(Math.Min(requested, ExportMaximumUnits), volumeBound);
            bounded = Math.Min(bounded, surplus);
            bounded = Math.Min(bounded, (long)listing.Stock - listing.BaselineStock);
            if (bounded <= 0)
                return false;

            quantity = (int)bounded;
            destination = FindBestExportDestination(origin, commodity, quantity);
            if (destination == null)
                return false;

            StationMarketListing destinationListing = _marketManager.GetListingForCommodity(destination, commodity);
            if (destinationListing == null)
                return false;

            reward = CalculateExportReward(listing, destinationListing, commodity, quantity);
            return reward > 0;
        }

        private Station FindBestExportDestination(Station origin, Commodity commodity, int quantity)
        {
            Station bestStation = null;
            long bestScore = long.MinValue;
            foreach (Station station in _worldManager.GetKnownStations())
            {
                if (station == null || string.Equals(
                        Mission.BuildStationIdentity(station),
                        Mission.BuildStationIdentity(origin),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                StationMarketListing listing = _marketManager.GetListingForCommodity(station, commodity);
                if (listing == null || !IsExportCommodity(listing.Commodity) ||
                    !listing.IsAvailable || listing.BaselineStock <= 0 ||
                    listing.Stock < 0 || listing.Stock >= listing.BaselineStock ||
                    listing.BaseBuyPrice <= 0 || listing.BaseSellPrice <= 0 ||
                    (long)listing.Stock + quantity > listing.MaximumStock)
                {
                    continue;
                }

                long shortageBasisPoints = ((long)listing.BaselineStock - listing.Stock) * 10_000L / listing.BaselineStock;
                long score = shortageBasisPoints * 1_000L + (long)listing.DemandLevel * 100L + listing.BuyPrice;
                if (score > bestScore ||
                    (score == bestScore && string.Compare(station.Name, bestStation?.Name, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    bestScore = score;
                    bestStation = station;
                }
            }

            return bestStation;
        }

        private static int CalculateExportReward(
            StationMarketListing originListing,
            StationMarketListing destinationListing,
            Commodity commodity,
            int quantity)
        {
            long originSurplus = Math.Max(0L, (long)originListing.Stock - originListing.BaselineStock);
            long destinationShortage = Math.Max(0L, (long)destinationListing.BaselineStock - destinationListing.Stock);
            long originSeverity = originListing.BaselineStock > 0
                ? Math.Clamp(originSurplus * 10_000L / originListing.BaselineStock, 0L, 10_000L)
                : 0L;
            long destinationSeverity = destinationListing.BaselineStock > 0
                ? Math.Clamp(destinationShortage * 10_000L / destinationListing.BaselineStock, 0L, 10_000L)
                : 0L;
            long premiumPercent = 35L + destinationSeverity * 25L / 10_000L + originSeverity * 15L / 10_000L +
                Math.Clamp(destinationListing.DemandLevel, 0, 10) * 2L;
            premiumPercent = Math.Clamp(premiumPercent, 35L, 100L);
            long rawReward = (long)commodity.BasePrice * quantity * (100L + premiumPercent) / 100L;
            return (int)Math.Clamp(rawReward, 500L, ExportMaximumReward);
        }

        private static string BuildExportOfferKey(Station origin, Commodity commodity, Station destination) =>
            $"{Mission.BuildStationIdentity(origin)}:{commodity?.Id ?? string.Empty}:{Mission.BuildStationIdentity(destination)}";

        private static bool IsMatchingExport(Mission mission, Station origin, Commodity commodity, Station destination)
        {
            return mission != null &&
                mission.Type == MissionType.ExportContract &&
                string.Equals(mission.OriginStationId, Mission.BuildStationIdentity(origin), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(mission.DestinationStationId, Mission.BuildStationIdentity(destination), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(mission.CommodityId, commodity?.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExportCommodity(Commodity commodity)
        {
            return commodity != null &&
                !string.IsNullOrWhiteSpace(commodity.Id) &&
                !string.IsNullOrWhiteSpace(commodity.Name) &&
                commodity.VolumePerUnit > 0 &&
                commodity.BasePrice > 0 &&
                !commodity.IsMissionCargo &&
                !commodity.IsContraband;
        }

        private bool TryBuildFreightTerms(
            Station destination,
            StationMarketListing listing,
            out Commodity commodity,
            out int quantity,
            out int reward)
        {
            commodity = listing?.Commodity;
            quantity = 0;
            reward = 0;
            if (destination == null || listing == null || commodity == null ||
                string.IsNullOrWhiteSpace(commodity.Id) || string.IsNullOrWhiteSpace(commodity.Name) ||
                commodity.VolumePerUnit <= 0 || commodity.BasePrice <= 0 ||
                commodity.IsMissionCargo || commodity.IsContraband ||
                !listing.IsAvailable || listing.BaseBuyPrice <= 0 || listing.BaseSellPrice <= 0 ||
                listing.BaselineStock <= 0 || listing.Stock < 0)
            {
                return false;
            }

            long shortage = (long)listing.BaselineStock - listing.Stock;
            long thresholdStock = (long)listing.BaselineStock * FreightShortageThresholdPercent / 100L;
            if (shortage < FreightMinimumShortageUnits || listing.Stock >= thresholdStock)
                return false;

            long requested = (shortage * FreightShortageSharePercent + 99L) / 100L;
            int volumeBound = FreightMaximumCargoVolume / commodity.VolumePerUnit;
            long bounded = Math.Min(Math.Min(requested, FreightMaximumUnits), volumeBound);
            if (bounded <= 0)
                return false;
            quantity = (int)bounded;

            long severityBasisPoints = Math.Clamp(shortage * 10_000L / listing.BaselineStock, 0L, 10_000L);
            long bonusPercent = 15L + severityBasisPoints * 30L / 10_000L;
            long rawReward = (long)commodity.BasePrice * quantity * (100L + bonusPercent) / 100L;
            rawReward = Math.Clamp(rawReward, 250L, FreightMaximumReward);

            if (listing.Stock > listing.MinimumStock && listing.BuyPrice > 0)
                rawReward = Math.Min(rawReward, Math.Max(1L, (long)listing.BuyPrice * quantity - 1L));

            reward = (int)Math.Clamp(rawReward, 1L, int.MaxValue);
            return reward > 0;
        }

        private static string BuildFreightOfferKey(Station station, Commodity commodity) =>
            $"{Mission.BuildStationIdentity(station)}:{commodity?.Id ?? string.Empty}";

        private static bool IsMatchingFreight(Mission mission, Station destination, Commodity commodity)
        {
            return mission != null &&
                mission.Type == MissionType.FreightContract &&
                string.Equals(mission.DestinationStationId, Mission.BuildStationIdentity(destination), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(mission.CommodityId, commodity?.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static string PickDestination(IReadOnlyList<Station> stations, Station origin)
        {
            Station destination = stations?.FirstOrDefault(station => station != null && !ReferenceEquals(station, origin))
                ?? stations?.FirstOrDefault();
            return destination?.Name ?? "Destination unavailable";
        }

        /// <summary>
        /// Validates and accepts exactly one mission. World binding happens
        /// before the authoritative active state is committed.
        /// </summary>
        public bool AcceptMission(Mission mission, Station originStation = null)
        {
            if (mission == null || mission.Status != MissionStatus.Available)
                return RejectAcceptance(mission, "mission is not available");
            if (ActiveMission != null)
                return RejectAcceptance(mission, "finish the active mission first");
            if (mission.Reward <= 0)
                return RejectAcceptance(mission, "reward is invalid");

            if (!string.IsNullOrWhiteSpace(mission.DefinitionId))
            {
                MissionDefinition definition = MissionCatalog.GetById(mission.DefinitionId);
                if (definition == null || definition.Type != mission.Type ||
                    definition.RewardCredits != mission.Reward ||
                    definition.TargetCount != mission.RequiredProgress)
                    return RejectAcceptance(mission, "mission definition is invalid");
            }

            if (mission.Type == MissionType.DestroyHostiles && mission.RequiredProgress <= 0)
                return RejectAcceptance(mission, "hostile target count is invalid");
            if (mission.Type == MissionType.ReachLocation && string.IsNullOrWhiteSpace(mission.TargetLocation))
                return RejectAcceptance(mission, "patrol target metadata is invalid");

            if (mission.Type == MissionType.CourierDelivery &&
                (string.IsNullOrWhiteSpace(mission.PackageId) || mission.PackageQuantity <= 0 ||
                 string.IsNullOrWhiteSpace(mission.SourceStationName) || string.IsNullOrWhiteSpace(mission.Destination)))
            {
                return RejectAcceptance(mission, "courier metadata is invalid");
            }

            if (mission.Type == MissionType.FreightContract)
            {
                Commodity freightCommodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
                if (freightCommodity == null || freightCommodity.IsMissionCargo || freightCommodity.IsContraband ||
                    freightCommodity.VolumePerUnit <= 0 || mission.RequiredQuantity <= 0 ||
                    string.IsNullOrWhiteSpace(mission.Destination) ||
                    string.IsNullOrWhiteSpace(mission.DestinationStationId) ||
                    _cargoHold == null)
                {
                    return RejectAcceptance(mission, "freight contract metadata or cargo authority is invalid");
                }
            }
            else if (mission.Type == MissionType.ExportContract)
            {
                Commodity exportCommodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
                if (exportCommodity == null || !IsExportCommodity(exportCommodity) ||
                    mission.RequiredQuantity <= 0 ||
                    mission.RequiredQuantity > ExportMaximumUnits ||
                    (long)mission.RequiredQuantity * exportCommodity.VolumePerUnit > ExportMaximumCargoVolume ||
                    string.IsNullOrWhiteSpace(mission.Destination) ||
                    string.IsNullOrWhiteSpace(mission.DestinationStationId) ||
                    _cargoHold == null || _marketManager == null || _worldManager == null ||
                    originStation == null)
                {
                    return RejectAcceptance(mission, "export contract metadata or cargo authority is invalid");
                }

                string acceptedOriginIdentity = Mission.BuildStationIdentity(originStation);
                if ((!string.IsNullOrWhiteSpace(mission.OriginStationId) &&
                     !string.Equals(mission.OriginStationId, acceptedOriginIdentity, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(mission.SourceStationName) &&
                     !string.Equals(mission.SourceStationName, originStation.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return RejectAcceptance(mission, "export cargo must be collected at its origin station");
                }
            }

            mission.SetOrigin(originStation);
            if (mission.Type == MissionType.CourierDelivery &&
                !string.Equals(mission.SourceStationName, mission.OriginStationName, StringComparison.OrdinalIgnoreCase))
            {
                return RejectAcceptance(mission, $"courier must be accepted at {mission.SourceStationName}");
            }

            if (mission.Type == MissionType.FreightContract && !RegisterFreightReservation(mission))
                return RejectAcceptance(mission, "freight reservation could not be registered");

            if (mission.Type == MissionType.ExportContract &&
                !TryIssueExportCargo(mission, originStation, out string exportFailureReason))
            {
                return RejectAcceptance(mission, exportFailureReason);
            }

            mission.AcceptedAtUtc = DateTime.UtcNow;
            mission.Status = MissionStatus.Accepted;

            if (_worldManager != null && !_worldManager.TryAcceptMission(mission, out string failureReason))
            {
                if (mission.Type == MissionType.ExportContract)
                {
                    TryRestoreExportShipment(mission, out _);
                }
                ReleaseFreightReservation(mission);
                mission.Status = MissionStatus.Available;
                _worldManager.OnMissionFinished(mission);
                return RejectAcceptance(mission, $"mission unavailable: {failureReason}");
            }

            _activeMissions.Add(mission);
            _waypointSystem?.RegisterMission(mission);
            mission.Status = MissionStatus.InProgress;
            _notificationManager?.ShowMessage($"Mission accepted: {mission.Title}", 3f);
            if (mission.Type == MissionType.CourierDelivery)
            {
                _notificationManager?.ShowMessage($"Mission cargo loaded: {mission.GetCargoLabel()}", 3f);
            }
            else if (mission.Type == MissionType.FreightContract)
            {
                _notificationManager?.ShowMessage(
                    $"Freight reserved: {GetFreightReservedQuantity(mission)}/{mission.RequiredQuantity} units",
                    3f);
            }
            else if (mission.Type == MissionType.ExportContract)
            {
                _notificationManager?.ShowMessage(
                    $"Export cargo loaded: {GetExportIssuedQuantity(mission)}/{mission.RequiredQuantity} units",
                    3f);
            }
            Console.WriteLine($"[MISSION] Accepted: {mission.GetSummary()} | Origin: {mission.OriginStationName}");
            return true;
        }

        private bool RejectAcceptance(Mission mission, string reason)
        {
            _notificationManager?.ShowMessage(reason, 3f);
            Console.WriteLine($"[MISSION] Rejected: {mission?.Title ?? "<null>"} | Reason: {reason}");
            return false;
        }

        private bool TryIssueExportCargo(Mission mission, Station origin, out string failureReason)
        {
            failureReason = string.Empty;
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            CargoHold cargo = _cargoHold;
            int quantity = mission?.RequiredQuantity ?? 0;
            if (mission == null || origin == null || commodity == null || cargo == null || quantity <= 0)
            {
                failureReason = "export cargo metadata is invalid";
                return false;
            }

            if (!cargo.CanFit(commodity, quantity))
            {
                failureReason = $"not enough cargo space for {commodity.Name} x{quantity}";
                return false;
            }

            StationMarketListing originListing = _marketManager.GetListingForCommodity(origin, commodity);
            if (originListing == null ||
                !_marketManager.CanRemoveSupply(origin, commodity, quantity, originListing.BaselineStock, out failureReason))
            {
                if (string.IsNullOrWhiteSpace(failureReason))
                    failureReason = $"{commodity.Name} surplus is no longer available at {origin.Name}";
                return false;
            }

            if (!_marketManager.TryRemoveSupply(origin, commodity, quantity, originListing.BaselineStock, out failureReason))
                return false;

            if (!cargo.AddMissionCargo(mission.Id, commodity, quantity))
            {
                _marketManager.TryAddSupply(origin, commodity, quantity, out _);
                failureReason = "export cargo could not be loaded; origin stock was restored";
                return false;
            }

            mission.IssuedCargoQuantity = quantity;
            mission.MissionCargoLoaded = true;
            mission.DeliveredQuantity = 0;
            return true;
        }

        private bool TryRestoreExportShipment(Mission mission, out string failureReason)
        {
            failureReason = string.Empty;
            if (mission?.Type != MissionType.ExportContract || _marketManager == null || _cargoHold == null)
            {
                failureReason = "export restoration authority is unavailable";
                return false;
            }

            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            Station origin = ResolveKnownStation(mission.OriginStationId, mission.OriginStationName);
            int quantity = mission.IssuedCargoQuantity > 0 ? mission.IssuedCargoQuantity : mission.RequiredQuantity;
            if (commodity == null || origin == null || quantity <= 0 ||
                !_cargoHold.HasMissionCargo(mission.Id, commodity.Id, quantity) ||
                _cargoHold.GetMissionCargoQuantity(mission.Id) != quantity)
            {
                failureReason = "issued export cargo is missing or corrupt";
                return false;
            }

            if (!_marketManager.CanAddSupply(origin, commodity, quantity, out failureReason))
                return false;

            if (!_cargoHold.RemoveMissionCargo(mission.Id, commodity, quantity))
            {
                failureReason = "issued export cargo could not be removed";
                return false;
            }

            if (!_marketManager.TryAddSupply(origin, commodity, quantity, out string restoreFailure))
            {
                _cargoHold.AddMissionCargo(mission.Id, commodity, quantity);
                failureReason = string.IsNullOrWhiteSpace(restoreFailure)
                    ? "origin stock could not be restored"
                    : restoreFailure;
                return false;
            }

            mission.MissionCargoLoaded = false;
            return true;
        }

        private Station ResolveKnownStation(string stationIdentity, string stationName)
        {
            IReadOnlyList<Station> stations = _worldManager?.GetKnownStations() ?? Array.Empty<Station>();
            if (!string.IsNullOrWhiteSpace(stationIdentity))
            {
                Station byIdentity = stations.FirstOrDefault(station =>
                    string.Equals(Mission.BuildStationIdentity(station), stationIdentity, StringComparison.OrdinalIgnoreCase));
                if (byIdentity != null)
                    return byIdentity;
            }

            return stations.FirstOrDefault(station =>
                !string.IsNullOrWhiteSpace(stationName) &&
                string.Equals(station.Name, stationName, StringComparison.OrdinalIgnoreCase));
        }

        public bool CancelMission(Mission mission, out string message)
        {
            message = string.Empty;
            if (mission == null || !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
            {
                message = "mission is not active";
                return false;
            }

            if (mission.Type == MissionType.ExportContract && !TryRestoreExportShipment(mission, out string restoreFailure))
            {
                message = string.IsNullOrWhiteSpace(restoreFailure)
                    ? "export shipment could not be restored; mission remains active"
                    : restoreFailure;
                return false;
            }

            ReleaseFreightReservation(mission);
            mission.Status = MissionStatus.Failed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);
            _worldManager?.OnMissionFinished(mission);
            _notificationManager?.ShowMessage($"Mission cancelled: {mission.Title}", 4f);
            message = "Mission cancelled.";
            return true;
        }

        /// <summary>
        /// Transitions a satisfied objective to Completed. Credits are not
        /// changed here; the originating station performs the reward claim.
        /// </summary>
        public void CompleteMission(Mission mission)
        {
            if (mission == null || !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
                return;

            mission.ObjectiveComplete = true;
            mission.Status = MissionStatus.Completed;
            _activeMissions.Remove(mission);
            _completedMissions.RemoveAll(existing => existing.Id == mission.Id);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);
            _worldManager?.OnMissionFinished(mission);
            string completionMessage = mission.Type == MissionType.CourierDelivery
                ? $"Cargo delivered - return to {mission.OriginStationName} to claim {mission.Reward:N0} CR"
                : mission.Type == MissionType.FreightContract
                    ? $"Freight delivered - +{mission.Reward:N0} CR"
                : mission.Type == MissionType.ExportContract
                    ? $"Export delivered - +{mission.Reward:N0} CR"
                : $"Objective complete - return to {mission.OriginStationName} to claim {mission.Reward:N0} CR";
            _notificationManager?.ShowMessage(completionMessage, 4f);
            Console.WriteLine($"[MISSION] Objective complete: {mission.Title} | Reward {(mission.Type is MissionType.FreightContract or MissionType.ExportContract ? "paid" : "pending")}: {mission.Reward:N0} CR");
        }

        public bool CompleteFreightMission(Mission mission, out string message)
        {
            message = string.Empty;
            if (mission == null || mission.Type != MissionType.FreightContract ||
                !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
            {
                message = "freight mission is not active";
                return false;
            }

            if (mission.Reward <= 0 || _playerCredits == null ||
                (long)_playerCredits.Credits + mission.Reward > int.MaxValue)
            {
                message = "freight reward transaction is invalid";
                return false;
            }

            CompleteMission(mission);
            mission.RewardPaid = true;
            _playerCredits.AddCredits(mission.Reward);
            _notificationManager?.ShowMessage($"Freight reward received: {mission.Reward:N0} CR", 4f);
            message = $"Freight reward received: {mission.Reward:N0} CR";
            return true;
        }

        public bool CanPayFreightReward(Mission mission, out string message)
        {
            message = string.Empty;
            if (mission == null || mission.Type != MissionType.FreightContract ||
                !ReferenceEquals(ActiveMission, mission) || mission.Reward <= 0 || _playerCredits == null)
            {
                message = "freight reward transaction is invalid";
                return false;
            }

            if ((long)_playerCredits.Credits + mission.Reward > int.MaxValue)
            {
                message = "credit total is invalid";
                return false;
            }

            return true;
        }

        public bool CompleteExportMission(Mission mission, out string message)
        {
            message = string.Empty;
            if (mission == null || mission.Type != MissionType.ExportContract ||
                !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
            {
                message = "export mission is not active";
                return false;
            }

            if (!CanPayExportReward(mission, out message))
                return false;

            CompleteMission(mission);
            mission.RewardPaid = true;
            _playerCredits.AddCredits(mission.Reward);
            _notificationManager?.ShowMessage($"Export reward received: {mission.Reward:N0} CR", 4f);
            message = $"Export reward received: {mission.Reward:N0} CR";
            return true;
        }

        public bool CanPayExportReward(Mission mission, out string message)
        {
            message = string.Empty;
            if (mission == null || mission.Type != MissionType.ExportContract ||
                !ReferenceEquals(ActiveMission, mission) || mission.Reward <= 0 || _playerCredits == null)
            {
                message = "export reward transaction is invalid";
                return false;
            }

            if ((long)_playerCredits.Credits + mission.Reward > int.MaxValue)
            {
                message = "credit total is invalid";
                return false;
            }

            return true;
        }

        private bool RegisterFreightReservation(Mission mission)
        {
            if (mission?.Type != MissionType.FreightContract || _cargoHold == null)
                return mission?.Type != MissionType.FreightContract;

            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            return commodity != null &&
                _cargoHold.RegisterFreightReservation(mission.Id, commodity, mission.RequiredQuantity);
        }

        public void ReleaseFreightReservation(Mission mission)
        {
            if (mission?.Type == MissionType.FreightContract)
                _cargoHold?.ReleaseMissionCargoReservation(mission.Id);
        }

        public bool TryClaimReward(Mission mission, Station station, out string message)
        {
            message = string.Empty;
            if (mission == null || mission.Status != MissionStatus.Completed)
            {
                message = "mission is not complete";
                return false;
            }
            if (mission.RewardPaid || mission.Status == MissionStatus.Rewarded)
            {
                message = "reward already claimed";
                return false;
            }

            string currentStationId = Mission.BuildStationIdentity(station);
            if (!string.Equals(currentStationId, mission.OriginStationId, StringComparison.OrdinalIgnoreCase))
            {
                message = $"return to {mission.OriginStationName} to claim the reward";
                return false;
            }
            if (mission.Reward <= 0 || _playerCredits == null)
            {
                message = "reward transaction is invalid";
                return false;
            }

            mission.RewardPaid = true;
            _playerCredits.AddCredits(mission.Reward);
            mission.Status = MissionStatus.Rewarded;
            _completedMissions.Remove(mission);
            _reputationManager?.AddReputation(
                mission.FactionId,
                0.12f,
                $"Mission rewarded: {mission.Title}");
            _notificationManager?.ShowMessage($"Mission reward received: {mission.Reward:N0} CR", 4f);
            Console.WriteLine($"[MISSION] Rewarded: {mission.Title} | +{mission.Reward:N0} CR");
            message = $"Mission reward received: {mission.Reward:N0} CR";
            return true;
        }

        public bool CanClaimRewardAt(Mission mission, Station station, out string reason)
        {
            reason = string.Empty;
            if (mission == null || mission.Status != MissionStatus.Completed)
            {
                reason = "No completed mission is waiting for payment.";
                return false;
            }
            if (mission.RewardPaid)
            {
                reason = "Reward already claimed.";
                return false;
            }

            if (!string.Equals(Mission.BuildStationIdentity(station), mission.OriginStationId, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Return to {mission.OriginStationName} to claim the reward.";
                return false;
            }

            return true;
        }

        public void FailMission(Mission mission, string reason)
        {
            if (mission == null || !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
                return;

            if (mission.Type == MissionType.ExportContract &&
                !TryRestoreExportShipment(mission, out string restoreFailure))
            {
                Console.WriteLine($"[MISSION] Export failure held: {restoreFailure}");
                return;
            }

            ReleaseFreightReservation(mission);
            mission.Status = MissionStatus.Failed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);
            _worldManager?.OnMissionFinished(mission);
            _notificationManager?.ShowMessage($"Mission failed: {reason}", 4f);
            Console.WriteLine($"[MISSION] Failed: {mission.Title} | Reason: {reason}");
        }

        public void Update(float deltaTime, bool playerDestroyed)
        {
            Mission mission = ActiveMission;
            if (mission == null) return;

            mission.ElapsedTime += Math.Max(0f, deltaTime);
            if (mission.IsExpired)
            {
                FailMission(mission, "Time ran out");
                return;
            }
            if (playerDestroyed)
            {
                FailMission(mission, "Ship destroyed");
                return;
            }
            if (mission.ObjectiveComplete)
                CompleteMission(mission);
        }

        public void FailAllActiveMissions(string reason)
        {
            if (ActiveMission != null) FailMission(ActiveMission, reason);
        }

        /// <summary>Legacy name-based hook retained for missile smoke coverage.</summary>
        public void NotifyTargetDestroyed(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName)) return;
            Mission mission = ActiveMission;
            if (mission?.Type == MissionType.Bounty &&
                !string.IsNullOrWhiteSpace(mission.Target) &&
                targetName.Contains(mission.Target, StringComparison.OrdinalIgnoreCase))
            {
                mission.ObjectiveComplete = true;
            }
        }

        public bool RecordHostileDestroyed(Mission mission, NpcShip destroyedShip)
        {
            if (mission == null || destroyedShip == null ||
                !ReferenceEquals(ActiveMission, mission) ||
                mission.Type != MissionType.DestroyHostiles ||
                !destroyedShip.WasDamagedByPlayer ||
                !_countedHostileKills.Add(destroyedShip))
                return false;

            mission.CurrentProgress = Math.Min(mission.RequiredProgress, mission.CurrentProgress + 1);
            Console.WriteLine($"[MISSION] Rogue Hunt progress {mission.CurrentProgress}/{mission.RequiredProgress}: {destroyedShip.Name}");
            if (mission.CurrentProgress >= mission.RequiredProgress)
            {
                mission.ObjectiveComplete = true;
                CompleteMission(mission);
            }
            return true;
        }

        /// <summary>Legacy delivery arrival hook; it now waits for reward claim.</summary>
        public void NotifyArrivedAtStation(string stationName)
        {
            Mission mission = ActiveMission;
            if (mission?.Type == MissionType.Delivery &&
                !string.IsNullOrWhiteSpace(stationName) &&
                stationName.Contains(mission.Destination ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                mission.ObjectiveComplete = true;
            }
        }
    }
}
