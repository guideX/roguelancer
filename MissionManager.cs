using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    public sealed class MissionEligibilityResult
    {
        public bool IsEligible { get; init; }
        public bool IsReputationLocked { get; init; }
        public string Reason { get; init; } = string.Empty;
        public string EmployerFactionId { get; init; } = FactionManager.NeutralCivilians;
        public float CurrentStanding { get; init; }
        public float? MinimumStanding { get; init; }
        public float? MaximumStanding { get; init; }
    }

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
        private MarketIntelligence _marketIntelligence;
        private MarketRouteAuthority _routeAuthority = new();
        private readonly CargoHold _cargoHold;
        private readonly Dictionary<string, Mission> _freightOffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Mission> _exportOffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Mission> _tradeLaneDisruptionOffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Mission> _tradeLaneDefenseOffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Mission> _convoyEscortOffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Mission> _convoyRaidOffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Mission> _smugglingOffers = new(StringComparer.OrdinalIgnoreCase);
        private ReputationManager _reputationManager;
        private MissionWaypointSystem _waypointSystem;
        private MissionWorldManager _worldManager;

        public string LastAcceptanceFailureReason { get; private set; } = string.Empty;

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

        // Phase 60 emergency supply policy. The old Freight names remain
        // source-compatible for Phase 15 callers and tests.
        public const int FreightShortageThresholdPercent = MarketShortagePolicy.EnterThresholdPercent;
        public const int FreightRecoveryThresholdPercent = MarketShortagePolicy.RecoveryThresholdPercent;
        public const int FreightMinimumShortageUnits = MarketShortagePolicy.MinimumDeficitUnits;
        public const int FreightShortageSharePercent = 35;
        public const int FreightMaximumCargoVolume = 40;
        public const int FreightMaximumUnits = 10;
        public const int FreightMaximumReward = 100_000;

        public const int ExportSurplusThresholdPercent = 150;
        public const int ExportMinimumSurplusUnits = 20;
        public const int ExportSurplusSharePercent = 40;
        public const int ExportMaximumCargoVolume = 40;
        public const int ExportMaximumUnits = 40;
        public const int ExportMaximumReward = 100_000;
        public const int MarketOpportunityMaximumEntries = 8;
        public const float MissionFailureReputationPenalty = -0.02f;
        public const float TradeLaneDisruptionHoldSeconds = 10f;
        public const float TradeLaneDefenseHoldSeconds = 10f;
        public const float TradeLaneEasyPolicePenalty = -0.02f;
        public const float TradeLaneMediumPolicePenalty = -0.03f;
        public const float TradeLaneHardPolicePenalty = -0.05f;

        public static float GetMissionReputationReward(Mission mission)
        {
            if (mission == null)
                return 0.01f;

            if (mission.ReputationReward > 0f &&
                !float.IsNaN(mission.ReputationReward) &&
                !float.IsInfinity(mission.ReputationReward))
            {
                return Math.Clamp(MathF.Round(mission.ReputationReward, 4, MidpointRounding.AwayFromZero), 0.01f, 0.05f);
            }

            float baseReward = mission.Type switch
            {
                MissionType.Bounty or MissionType.DestroyHostiles => 0.035f,
                MissionType.TradeLaneDisruption => 0.020f,
                MissionType.TradeLaneDefense => 0.025f,
                MissionType.ConvoyEscort => 0.030f,
                MissionType.ConvoyRaid => 0.035f,
                MissionType.ContrabandSmuggling => 0.040f,
                MissionType.Escort => 0.030f,
                MissionType.EmergencySupply or MissionType.ExportContract => 0.025f,
                MissionType.CourierDelivery => 0.020f,
                MissionType.Delivery => 0.018f,
                _ => 0.015f
            };

            float difficultyBonus = mission.Difficulty switch
            {
                MissionDifficulty.Medium => 0.005f,
                MissionDifficulty.Hard => 0.010f,
                MissionDifficulty.Deadly => 0.015f,
                _ => 0f
            };

            return Math.Clamp(MathF.Round(baseReward + difficultyBonus, 4, MidpointRounding.AwayFromZero), 0.01f, 0.05f);
        }

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
            CargoHold cargoHold = null,
            MarketIntelligence marketIntelligence = null)
        {
            _playerCredits = playerCredits;
            _notificationManager = notificationManager;
            _reputationManager = reputationManager;
            _marketManager = marketManager;
            _cargoHold = cargoHold;
            _marketIntelligence = marketIntelligence;
        }

        public void SetReputationManager(ReputationManager reputationManager) => _reputationManager = reputationManager;
        public void SetWaypointSystem(MissionWaypointSystem waypointSystem) => _waypointSystem = waypointSystem;
        public void SetWorldManager(MissionWorldManager worldManager) => _worldManager = worldManager;

        /// <summary>
        /// Receives the cargo authority's attribution report after a Police
        /// seizure. MissionManager owns the mission consequence; the Police
        /// service never reaches into mission status directly.
        /// </summary>
        public void NotifyPoliceConfiscation(IReadOnlyList<MissionCargoConfiscation> confiscations)
        {
            if (confiscations == null || confiscations.Count == 0)
                return;

            foreach (int missionId in confiscations
                         .Where(entry => entry != null && entry.MissionId > 0 && entry.Quantity > 0)
                         .Select(entry => entry.MissionId)
                         .Distinct()
                         .ToList())
            {
                Mission mission = _activeMissions.FirstOrDefault(candidate => candidate?.Id == missionId);
                if (mission?.Type != MissionType.ContrabandSmuggling)
                {
                    // No active mission may retain a stale reservation after a
                    // world-level seizure, even if a caller supplied one.
                    _cargoHold?.ConvertMissionCargoToOrdinary(missionId);
                    continue;
                }

                mission.MissionCargoLoaded = (_cargoHold?.GetMissionCargoQuantity(mission.Id) ?? 0) > 0;
                mission.IssuedCargoQuantity = _cargoHold?.GetMissionCargoQuantity(mission.Id) ?? 0;
                FailMission(mission, "Liberty Police confiscated the smuggling cargo");
            }
        }

        /// <summary>
        /// Compliance can follow a post-detection jettison, in which case the
        /// confiscation report is empty but the contract is still impossible.
        /// Re-check the authoritative reservation once after the paid
        /// enforcement transaction so the mission cannot later revive.
        /// </summary>
        public void NotifyPoliceEnforcementCompliance()
        {
            Mission mission = ActiveMission;
            if (mission?.Type != MissionType.ContrabandSmuggling || _cargoHold == null)
                return;

            if (_cargoHold.GetMissionCargoQuantity(mission.Id) < mission.RequiredQuantity)
                FailMission(mission, "Smuggling cargo was lost before Police compliance");
        }
        public void SetMarketIntelligence(MarketIntelligence marketIntelligence) => _marketIntelligence = marketIntelligence;
        public void SetRouteAuthority(MarketRouteAuthority routeAuthority) => _routeAuthority = routeAuthority ?? new MarketRouteAuthority();
        public bool MeetsReputationRequirement(string factionId, float minimumStanding) =>
            _reputationManager?.MeetsReputationRequirement(factionId, minimumStanding) ?? true;
        public MissionEligibilityResult GetMissionEligibility(Mission mission)
        {
            if (mission == null)
            {
                return new MissionEligibilityResult
                {
                    IsEligible = false,
                    IsReputationLocked = false,
                    Reason = "mission is unavailable"
                };
            }

            string employerFactionId = FactionManager.NormalizeFactionId(mission.FactionId);
            float currentStanding = _reputationManager?.GetStanding(employerFactionId) ?? 0f;
            if (_reputationManager == null)
            {
                return new MissionEligibilityResult
                {
                    IsEligible = true,
                    EmployerFactionId = employerFactionId,
                    CurrentStanding = currentStanding,
                    MinimumStanding = mission.MinimumEmployerReputation,
                    MaximumStanding = mission.MaximumEmployerReputation
                };
            }

            if (_reputationManager.IsHostile(employerFactionId))
            {
                return new MissionEligibilityResult
                {
                    IsEligible = false,
                    IsReputationLocked = true,
                    Reason = "NO WORK AVAILABLE — HOSTILE STANDING",
                    EmployerFactionId = employerFactionId,
                    CurrentStanding = currentStanding,
                    MinimumStanding = mission.MinimumEmployerReputation,
                    MaximumStanding = mission.MaximumEmployerReputation
                };
            }

            if (mission.MinimumEmployerReputation.HasValue &&
                !_reputationManager.MeetsRequirement(employerFactionId, mission.MinimumEmployerReputation.Value))
            {
                ReputationBand requiredBand = ReputationManager.GetMinimumRequirementBand(mission.MinimumEmployerReputation.Value);
                return new MissionEligibilityResult
                {
                    IsEligible = false,
                    IsReputationLocked = true,
                    Reason = $"REPUTATION TOO LOW — REQUIRES {ReputationPresentation.FormatBand(requiredBand)}",
                    EmployerFactionId = employerFactionId,
                    CurrentStanding = currentStanding,
                    MinimumStanding = mission.MinimumEmployerReputation,
                    MaximumStanding = mission.MaximumEmployerReputation
                };
            }

            if (mission.MaximumEmployerReputation.HasValue &&
                !_reputationManager.MeetsMaximumRequirement(employerFactionId, mission.MaximumEmployerReputation.Value))
            {
                ReputationBand maximumBand = ReputationManager.GetBandForStanding(mission.MaximumEmployerReputation.Value);
                return new MissionEligibilityResult
                {
                    IsEligible = false,
                    IsReputationLocked = true,
                    Reason = $"REPUTATION TOO HIGH — REQUIRES {ReputationPresentation.FormatBand(maximumBand)} OR LOWER",
                    EmployerFactionId = employerFactionId,
                    CurrentStanding = currentStanding,
                    MinimumStanding = mission.MinimumEmployerReputation,
                    MaximumStanding = mission.MaximumEmployerReputation
                };
            }

            return new MissionEligibilityResult
            {
                IsEligible = true,
                EmployerFactionId = employerFactionId,
                CurrentStanding = currentStanding,
                MinimumStanding = mission.MinimumEmployerReputation,
                MaximumStanding = mission.MaximumEmployerReputation
            };
        }

        /// <summary>
        /// Single mission-acceptance authority used by both UI and mutation.
        /// Reputation checks happen before any cargo, market, or mission state
        /// changes are possible.
        /// </summary>
        public bool CanPlayerAcceptMission(Mission mission, out string reason, Station originStation = null)
        {
            reason = string.Empty;
            if (mission == null || mission.Status != MissionStatus.Available)
            {
                reason = "mission is not available";
                return false;
            }
            if (ActiveMission != null)
            {
                reason = "finish the active mission first";
                return false;
            }
            if (mission.Reward <= 0)
            {
                reason = "reward is invalid";
                return false;
            }

            MissionEligibilityResult eligibility = GetMissionEligibility(mission);
            if (!eligibility.IsEligible)
            {
                reason = eligibility.Reason;
                return false;
            }

            if (mission.Type == MissionType.ContrabandSmuggling)
            {
                Commodity smugglingCommodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
                string acceptedOriginIdentity = Mission.BuildStationIdentity(originStation);
                if (originStation == null ||
                    !string.Equals(FactionManager.NormalizeFactionId(originStation.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(FactionManager.NormalizeFactionId(mission.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(mission.OriginStationId) &&
                     !string.Equals(mission.OriginStationId, acceptedOriginIdentity, StringComparison.OrdinalIgnoreCase)) ||
                    smugglingCommodity == null || !smugglingCommodity.IsContraband || smugglingCommodity.IsMissionCargo ||
                    mission.RequiredQuantity <= 0 || mission.RequiredQuantity > FreightMaximumUnits ||
                    _cargoHold == null || !_cargoHold.CanFit(smugglingCommodity, mission.RequiredQuantity))
                {
                    reason = smugglingCommodity == null
                        ? "smuggling commodity is invalid"
                        : $"not enough cargo space for {smugglingCommodity.Name} x{mission.RequiredQuantity}";
                    return false;
                }
            }

            return true;
        }
        public void ShowNotification(string message, float durationSeconds = 3f) =>
            _notificationManager?.ShowMessage(message, durationSeconds);

        public void ClearState()
        {
            foreach (Mission mission in _activeMissions.Where(candidate => candidate?.Type == MissionType.FreightContract))
            {
                ReleaseFreightReservation(mission);
                _marketManager?.ReleaseSupplyContractCapacity(mission.Id);
            }

            foreach (Mission mission in _activeMissions.Where(candidate => candidate?.Type == MissionType.ExportContract))
                _cargoHold?.ReleaseMissionCargoReservation(mission.Id);

            foreach (Mission mission in _activeMissions.Where(candidate => candidate?.Type == MissionType.ConvoyRaid))
                ReleaseConvoyRaidCargo(mission);

            foreach (Mission mission in _activeMissions.Where(candidate => candidate?.Type == MissionType.ContrabandSmuggling))
                ReleaseSmugglingCargo(mission);

            foreach (Mission mission in _activeMissions)
                _waypointSystem?.UnregisterMission(mission);

            _activeMissions.Clear();
            _completedMissions.Clear();
            _countedHostileKills.Clear();
            _freightOffers.Clear();
            _exportOffers.Clear();
            _tradeLaneDisruptionOffers.Clear();
            _tradeLaneDefenseOffers.Clear();
            _convoyEscortOffers.Clear();
            _convoyRaidOffers.Clear();
            _smugglingOffers.Clear();
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
                if (restoredActive.Type == MissionType.TradeLaneDisruption)
                {
                    // Phase 47 disruption is transient world state. Loading an
                    // active mission restores identity, never a stale hold.
                    restoredActive.HoldProgressSeconds = 0f;
                    restoredActive.PlayerDisruptionObserved = false;
                    restoredActive.LastQualifiedDisruptionAtSeconds = -1d;
                    restoredActive.SecurityResponseTriggered = false;
                }
                else if (restoredActive.Type == MissionType.TradeLaneDefense)
                {
                    // Attackers are transient. Keep the durable stage and let
                    // MissionWorldManager reconstruct one bounded group when
                    // an in-progress encounter is rebound.
                    restoredActive.DefenseAttackersRemaining = 0;
                    restoredActive.DefenseFailureHoldProgressSeconds = 0f;
                }
                else if (restoredActive.Type == MissionType.ConvoyEscort)
                {
                    // Convoy and Rogue objects are transient. The durable
                    // masks/stage and remaining wave count survive so an
                    // interrupted encounter can rebuild one bounded force;
                    // completed encounters never respawn their attackers.
                    restoredActive.ConvoyAttackersRemaining = restoredActive.ConvoyStage == ConvoyEscortStage.EncounterActive &&
                        restoredActive.ConvoyEncounterActivated && !restoredActive.ConvoyEncounterResolved
                        ? Math.Clamp(restoredActive.ConvoyAttackersRemaining > 0
                            ? restoredActive.ConvoyAttackersRemaining
                            : restoredActive.ConvoyAttackForceSize, 1, 6)
                        : 0;
                    restoredActive.ConvoyAbandonmentProgressSeconds = 0f;
                }
                _activeMissions.Add(restoredActive);
                if (restoredActive.Type == MissionType.FreightContract)
                {
                    // Phase 15 freight saves may contain reserved cargo. The
                    // Phase 60 contract is fungible, so release that old
                    // attribution while preserving its provenance aggregate.
                    _cargoHold?.ConvertMissionCargoToOrdinary(restoredActive.Id);
                    Station destination = ResolveKnownStation(restoredActive.DestinationStationId, restoredActive.Destination);
                    Commodity commodity = CommodityCatalog.GetByIdOrName(restoredActive.CommodityId);
                    _marketManager?.TryReserveSupplyContractCapacity(
                        restoredActive.Id,
                        destination,
                        commodity,
                        restoredActive.RequiredQuantity,
                        out _);
                }
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
            missions.AddRange(GenerateContrabandSmugglingMissions(originStation));
            missions.AddRange(GenerateFreightContracts(originStation));
            missions.AddRange(GenerateExportContracts(originStation));
            missions.AddRange(GenerateTradeLaneDisruptionMissions(originStation));
            missions.AddRange(GenerateTradeLaneDefenseMissions(originStation));
            missions.AddRange(GenerateConvoyEscortMissions(originStation));
            missions.AddRange(GenerateConvoyRaidMissions(originStation));
            return missions;
        }

        public List<Mission> GenerateContrabandSmugglingMissions(Station originStation)
        {
            List<Mission> offers = new();
            _smugglingOffers.Clear();
            if (originStation == null || _worldManager == null ||
                !string.Equals(
                    FactionManager.NormalizeFactionId(originStation.FactionId),
                    FactionManager.LibertyRogues,
                    StringComparison.OrdinalIgnoreCase))
            {
                return offers;
            }

            List<Station> destinations = _worldManager.GetKnownStations()
                .Where(station => station != null &&
                    !string.Equals(Mission.BuildStationIdentity(station), Mission.BuildStationIdentity(originStation), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(FactionManager.NormalizeFactionId(station.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase))
                .OrderBy(station => Vector3.DistanceSquared(originStation.Position, station.Position))
                .ThenBy(station => station.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(station => Mission.BuildStationIdentity(station), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (destinations.Count == 0)
                return offers;

            List<Commodity> contraband = CommodityCatalog.All
                .Where(commodity => commodity != null && commodity.IsContraband && !commodity.IsMissionCargo && commodity.VolumePerUnit > 0)
                .OrderBy(commodity => commodity.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (contraband.Count == 0)
                return offers;

            MissionDifficulty[] difficulties = { MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard };
            for (int i = 0; i < difficulties.Length; i++)
            {
                MissionDifficulty difficulty = difficulties[i];
                int stableHash = StableStringHash($"smuggling|{Mission.BuildStationIdentity(originStation)}|{difficulty}");
                Station destination = destinations[stableHash % destinations.Count];
                Commodity commodity = contraband[stableHash % contraband.Count];
                int requestedQuantity = difficulty switch
                {
                    MissionDifficulty.Hard => 8,
                    MissionDifficulty.Medium => 5,
                    _ => 3
                };
                int quantity = Math.Min(requestedQuantity, FreightMaximumCargoVolume / Math.Max(1, commodity.VolumePerUnit));
                if (quantity <= 0)
                    continue;

                int reward = difficulty switch
                {
                    MissionDifficulty.Hard => 13_000,
                    MissionDifficulty.Medium => 8_500,
                    _ => 5_000
                };
                Mission offer = Mission.CreateContrabandSmuggling(
                    originStation,
                    destination,
                    commodity,
                    quantity,
                    reward,
                    difficulty);
                if (offer == null)
                    continue;

                string key = $"{Mission.BuildStationIdentity(originStation)}:{destination.Name}:{commodity.Id}:{difficulty}";
                _smugglingOffers[key] = offer;
                offers.Add(offer);
            }

            return offers;
        }

        public List<Mission> GenerateTradeLaneDisruptionMissions(Station originStation)
        {
            List<Mission> offers = new();
            if (originStation == null || !string.Equals(
                    FactionManager.NormalizeFactionId(originStation.FactionId),
                    FactionManager.LibertyRogues,
                    StringComparison.OrdinalIgnoreCase) || _worldManager == null)
            {
                return offers;
            }

            IReadOnlyList<TradeLane> lanes = _worldManager.GetTradeLanes();
            if (lanes == null || lanes.Count == 0)
                return offers;

            List<TradeLane> eligibleLanes = lanes
                .Where(lane => lane != null && lane.Config != null && lane.ForwardRings.Count >= 3 &&
                    ((originStation.Config?.SystemIndex ?? 0) <= 0 || lane.Config.SystemIndex <= 0 ||
                     lane.Config.SystemIndex == originStation.Config.SystemIndex))
                .Where(lane => lane != null && !lane.IsBroken && lane.CanUseRoute(TradeLaneDirection.Forward))
                .OrderBy(lane => lane.LaneId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (eligibleLanes.Count == 0)
                return offers;

            string originIdentity = Mission.BuildStationIdentity(originStation);
            MissionDifficulty[] difficulties = { MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard };
            for (int i = 0; i < difficulties.Length; i++)
            {
                MissionDifficulty difficulty = difficulties[i];
                TradeLane lane = eligibleLanes[i % eligibleLanes.Count];
                int intermediateCount = lane.ForwardRings.Count - 2;
                int ringIndex = 1 + ((i * 2 + StableStringHash(lane.LaneId)) % intermediateCount + intermediateCount) % intermediateCount;
                if (lane.GetDisruptionState(ringIndex) != TradeLaneDisruptionState.Operational)
                    continue;

                string segmentId = $"{lane.LaneId}:ring:{ringIndex}";
                if (_activeMissions.Any(active => active != null && active.IsTradeLaneDisruptionMission() &&
                        string.Equals(active.TargetLaneId, lane.LaneId, StringComparison.OrdinalIgnoreCase) &&
                        active.TargetRingIndex == ringIndex))
                {
                    continue;
                }

                string key = $"{originIdentity}|{difficulty}|{lane.LaneId}|{ringIndex}";
                if (!_tradeLaneDisruptionOffers.TryGetValue(key, out Mission offer) ||
                    offer == null || offer.Status != MissionStatus.Available)
                {
                    float recovery = TradeLaneStateSanitizer.Positive(
                        lane.Config.DisruptionRecoverySeconds,
                        TradeLane.DefaultRecoveryDurationSeconds);
                    float hold = Math.Min(TradeLaneDisruptionHoldSeconds, Math.Max(1f, recovery * 0.75f));
                    offer = Mission.CreateTradeLaneDisruption(
                        lane.LaneId,
                        segmentId,
                        ringIndex,
                        lane.Config.Name,
                        lane.ForwardRings[ringIndex].Position,
                        lane.Config.SystemIndex,
                        difficulty,
                        GetTradeLaneReward(difficulty),
                        hold,
                        GetTradeLaneFlavor(i, lane.Config.Name),
                        GetTradeLanePolicePenalty(difficulty),
                        offeredBy: $"{originStation.Name} Rogue Contact");
                    if (offer == null)
                        continue;

                    offer.MinimumEmployerReputation = GetTradeLaneMinimumReputation(difficulty);
                    offer.SetOrigin(originStation);
                    _tradeLaneDisruptionOffers[key] = offer;
                }

                offers.Add(offer);
            }

            return offers;
        }

        public List<Mission> GenerateTradeLaneDefenseMissions(Station originStation)
        {
            List<Mission> offers = new();
            if (originStation == null || !string.Equals(
                    FactionManager.NormalizeFactionId(originStation.FactionId),
                    FactionManager.LibertyPolice,
                    StringComparison.OrdinalIgnoreCase) || _worldManager == null)
            {
                return offers;
            }

            IReadOnlyList<TradeLane> lanes = _worldManager.GetTradeLanes();
            if (lanes == null || lanes.Count == 0)
                return offers;

            List<TradeLane> eligibleLanes = lanes
                .Where(lane => lane != null && lane.Config != null && lane.ForwardRings.Count >= 3 &&
                    ((originStation.Config?.SystemIndex ?? 0) <= 0 || lane.Config.SystemIndex <= 0 ||
                     lane.Config.SystemIndex == originStation.Config.SystemIndex))
                .Where(lane => lane != null && !lane.IsBroken && lane.CanUseRoute(TradeLaneDirection.Forward))
                .OrderBy(lane => lane.LaneId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (eligibleLanes.Count == 0)
                return offers;

            string originIdentity = Mission.BuildStationIdentity(originStation);
            MissionDifficulty[] difficulties = { MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard };
            for (int i = 0; i < difficulties.Length; i++)
            {
                MissionDifficulty difficulty = difficulties[i];
                TradeLane lane = eligibleLanes[i % eligibleLanes.Count];
                int intermediateCount = lane.ForwardRings.Count - 2;
                int ringIndex = 1 + ((i * 2 + 1 + StableStringHash(lane.LaneId)) % intermediateCount + intermediateCount) % intermediateCount;
                if (lane.GetDisruptionState(ringIndex) != TradeLaneDisruptionState.Operational)
                    continue;

                string segmentId = $"{lane.LaneId}:ring:{ringIndex}";
                if (_activeMissions.Any(active => active != null && active.IsTradeLaneDefenseMission() &&
                        string.Equals(active.TargetLaneId, lane.LaneId, StringComparison.OrdinalIgnoreCase) &&
                        active.TargetRingIndex == ringIndex))
                {
                    continue;
                }

                string key = $"{originIdentity}|{difficulty}|{lane.LaneId}|{ringIndex}";
                if (!_tradeLaneDefenseOffers.TryGetValue(key, out Mission offer) ||
                    offer == null || offer.Status != MissionStatus.Available)
                {
                    float failureHold = Math.Min(
                        TradeLaneDefenseHoldSeconds,
                        Math.Max(1f, TradeLaneStateSanitizer.Positive(
                            lane.Config.DisruptionRecoverySeconds,
                            TradeLane.DefaultRecoveryDurationSeconds) * 0.75f));
                    offer = Mission.CreateTradeLaneDefense(
                        lane.LaneId,
                        segmentId,
                        ringIndex,
                        lane.Config.Name,
                        lane.ForwardRings[ringIndex].Position,
                        lane.Config.SystemIndex,
                        difficulty,
                        GetTradeLaneDefenseReward(difficulty),
                        GetTradeLaneDefenseAttackForceSize(difficulty),
                        GetTradeLaneDefenseActivationRadius(difficulty),
                        failureHold,
                        GetTradeLaneDefenseFlavor(i, lane.Config.Name),
                        offeredBy: $"{originStation.Name} Police Operations");
                    if (offer == null)
                        continue;

                    offer.MinimumEmployerReputation = GetTradeLaneDefenseMinimumReputation(difficulty);
                    offer.SetOrigin(originStation);
                    _tradeLaneDefenseOffers[key] = offer;
                }

                offers.Add(offer);
            }

            return offers;
        }

        public List<Mission> GenerateConvoyEscortMissions(Station originStation)
        {
            List<Mission> offers = new();
            if (originStation == null || _worldManager == null)
                return offers;

            string originFaction = FactionManager.NormalizeFactionId(originStation.FactionId);
            if (originFaction != FactionManager.LibertyPolice &&
                originFaction != FactionManager.LibertyCorporations &&
                originFaction != FactionManager.NeutralCivilians)
            {
                return offers;
            }

            IReadOnlyList<TradeLane> lanes = _worldManager.GetTradeLanes();
            IReadOnlyList<Station> stations = _worldManager.GetKnownStations();
            if (lanes == null || lanes.Count == 0 || stations == null || stations.Count < 2)
                return offers;

            int systemIndex = originStation.Config?.SystemIndex ?? 0;
            var candidates = new List<(TradeLane Lane, TradeLaneDirection Direction, Station Destination, float Length)>();
            foreach (TradeLane lane in lanes)
            {
                if (lane?.Config == null || lane.IsBroken || lane.Config.SystemIndex != systemIndex)
                    continue;

                foreach (TradeLaneDirection direction in new[] { TradeLaneDirection.Forward, TradeLaneDirection.Reverse })
                {
                    IReadOnlyList<TradelaneRing> route = lane.GetRouteRings(direction);
                    if (route == null || route.Count < 3 || !lane.CanUseRoute(direction))
                        continue;

                    TradelaneRing exit = lane.GetExitRing(direction);
                    if (exit == null)
                        continue;

                    Station destination = stations
                        .Where(candidate => candidate != null && !ReferenceEquals(candidate, originStation) &&
                            !string.Equals(Mission.BuildStationIdentity(candidate), Mission.BuildStationIdentity(originStation), StringComparison.OrdinalIgnoreCase) &&
                            (candidate.Config?.SystemIndex ?? systemIndex) == systemIndex)
                        .Select(candidate => new
                        {
                            Station = candidate,
                            Distance = Vector3.Distance(exit.Position, candidate.Position)
                        })
                        .Where(candidate => candidate.Distance <= Math.Max(4500f, lane.Config.DockingRange * 5f))
                        .OrderBy(candidate => candidate.Distance)
                        .ThenBy(candidate => candidate.Station.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(candidate => candidate.Station)
                        .FirstOrDefault();
                    if (destination == null)
                        continue;

                    float length = Vector3.Distance(route[0].Position, route[route.Count - 1].Position);
                    candidates.Add((lane, direction, destination, length));
                }
            }

            candidates = candidates
                .OrderBy(candidate => candidate.Length)
                .ThenBy(candidate => candidate.Lane.LaneId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Direction)
                .ToList();
            if (candidates.Count == 0)
                return offers;

            string employerFaction = originFaction == FactionManager.LibertyPolice
                ? FactionManager.LibertyPolice
                : FactionManager.LibertyCorporations;
            string originIdentity = Mission.BuildStationIdentity(originStation);
            MissionDifficulty[] difficulties = { MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard };
            for (int i = 0; i < difficulties.Length; i++)
            {
                MissionDifficulty difficulty = difficulties[i];
                var candidate = candidates[Math.Min(i, candidates.Count - 1)];
                TradeLane lane = candidate.Lane;
                IReadOnlyList<TradelaneRing> route = lane.GetRouteRings(candidate.Direction);
                if (route == null || route.Count < 3)
                    continue;

                int convoySize = GetConvoyEscortShipCount(difficulty);
                int attackForceSize = GetConvoyEscortAttackForceSize(difficulty);
                int encounterRingIndex = Math.Clamp(route.Count / 2, 1, route.Count - 2);
                Vector3 entryPosition = lane.GetRingTravelPosition(route[0], candidate.Direction);
                Vector3 travelDirection = lane.GetTravelForward(candidate.Direction);
                Vector3 rendezvousPosition = entryPosition - travelDirection * 900f;
                Vector3 encounterPosition = lane.GetRingTravelPosition(route[encounterRingIndex], candidate.Direction);
                string segmentId = $"{lane.LaneId}:escort:{candidate.Direction.ToString().ToLowerInvariant()}";
                string routeId = $"{lane.LaneId}:{candidate.Direction.ToString().ToLowerInvariant()}:{route.Count}";
                string key = $"{originIdentity}|{difficulty}|{routeId}|{Mission.BuildStationIdentity(candidate.Destination)}";

                if (_activeMissions.Any(active => active != null && active.IsConvoyEscortMission() &&
                        string.Equals(active.ConvoyRouteId, routeId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!_convoyEscortOffers.TryGetValue(key, out Mission offer) ||
                    offer == null || offer.Status != MissionStatus.Available)
                {
                    offer = Mission.CreateConvoyEscort(
                        routeId,
                        lane.LaneId,
                        segmentId,
                        candidate.Direction,
                        lane.Config.Name,
                        originStation,
                        candidate.Destination,
                        rendezvousPosition,
                        encounterPosition,
                        encounterRingIndex,
                        convoySize,
                        attackForceSize,
                        GetConvoyEscortReward(difficulty),
                        difficulty,
                        GetConvoyEscortFlavor(i, lane.Config.Name, candidate.Destination.Name),
                        offeredBy: $"{originStation.Name} {(employerFaction == FactionManager.LibertyPolice ? "Police Operations" : "Commercial Operations")}");
                    if (offer == null)
                        continue;

                    offer.FactionId = employerFaction;
                    offer.MinimumEmployerReputation = GetConvoyEscortMinimumReputation(difficulty);
                    offer.SetOrigin(originStation);
                    _convoyEscortOffers[key] = offer;
                }

                offers.Add(offer);
            }

            return offers;
        }

        public List<Mission> GenerateConvoyRaidMissions(Station originStation)
        {
            List<Mission> offers = new();
            if (originStation == null || _worldManager == null ||
                FactionManager.NormalizeFactionId(originStation.FactionId) != FactionManager.LibertyRogues)
            {
                return offers;
            }

            IReadOnlyList<TradeLane> lanes = _worldManager.GetTradeLanes();
            IReadOnlyList<Station> stations = _worldManager.GetKnownStations();
            if (lanes == null || lanes.Count == 0 || stations == null || stations.Count < 2)
                return offers;

            int systemIndex = originStation.Config?.SystemIndex ?? 0;
            var candidates = new List<(TradeLane Lane, TradeLaneDirection Direction, Station Destination, float Length)>();
            foreach (TradeLane lane in lanes)
            {
                if (lane?.Config == null || lane.IsBroken || lane.Config.SystemIndex != systemIndex)
                    continue;

                foreach (TradeLaneDirection direction in new[] { TradeLaneDirection.Forward, TradeLaneDirection.Reverse })
                {
                    IReadOnlyList<TradelaneRing> route = lane.GetRouteRings(direction);
                    TradelaneRing exit = lane.GetExitRing(direction);
                    if (route == null || route.Count < 3 || exit == null || !lane.CanUseRoute(direction))
                        continue;

                    Station destination = stations
                        .Where(candidate => candidate != null && !ReferenceEquals(candidate, originStation) &&
                            (candidate.Config?.SystemIndex ?? systemIndex) == systemIndex &&
                            FactionManager.NormalizeFactionId(candidate.FactionId) != FactionManager.LibertyRogues)
                        .Select(candidate => new
                        {
                            Station = candidate,
                            Distance = Vector3.Distance(exit.Position, candidate.Position)
                        })
                        .Where(candidate => candidate.Distance <= Math.Max(4500f, lane.Config.DockingRange * 5f))
                        .OrderBy(candidate => candidate.Distance)
                        .ThenBy(candidate => candidate.Station.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(candidate => candidate.Station)
                        .FirstOrDefault();
                    if (destination == null)
                        continue;

                    candidates.Add((lane, direction, destination,
                        Vector3.Distance(route[0].Position, route[route.Count - 1].Position)));
                }
            }

            candidates = candidates
                .OrderBy(candidate => candidate.Length)
                .ThenBy(candidate => candidate.Lane.LaneId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Direction)
                .ToList();
            if (candidates.Count == 0)
                return offers;

            string originIdentity = Mission.BuildStationIdentity(originStation);
            MissionDifficulty[] difficulties = { MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard };
            for (int i = 0; i < difficulties.Length; i++)
            {
                MissionDifficulty difficulty = difficulties[i];
                var candidate = candidates[i % candidates.Count];
                TradeLane lane = candidate.Lane;
                IReadOnlyList<TradelaneRing> route = lane.GetRouteRings(candidate.Direction);
                if (route == null || route.Count < 3)
                    continue;

                int shipCount = GetConvoyRaidShipCount(difficulty);
                int requiredQuantity = GetConvoyRaidRequiredQuantity(difficulty);
                int[] allocation = GetConvoyRaidCargoAllocation(difficulty);
                int interceptionRingIndex = Math.Clamp(route.Count / 2, 1, route.Count - 2);
                Vector3 interceptionPosition = lane.GetRingTravelPosition(route[interceptionRingIndex], candidate.Direction);
                Commodity commodity = SelectConvoyRaidCommodity(lane, candidate.Direction, i);
                if (commodity == null || allocation.Length != shipCount || allocation.Sum() < requiredQuantity)
                    continue;

                string routeId = $"{lane.LaneId}:{candidate.Direction.ToString().ToLowerInvariant()}:{route.Count}";
                string segmentId = $"{lane.LaneId}:raid:{candidate.Direction.ToString().ToLowerInvariant()}";
                string key = $"{originIdentity}|{difficulty}|{routeId}|{Mission.BuildStationIdentity(candidate.Destination)}|{commodity.Id}";
                if (_activeMissions.Any(active => active != null && active.IsConvoyRaidMission() &&
                        string.Equals(active.RaidRouteId, routeId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!_convoyRaidOffers.TryGetValue(key, out Mission offer) || offer == null || offer.Status != MissionStatus.Available)
                {
                    offer = Mission.CreateConvoyRaid(
                        routeId,
                        lane.LaneId,
                        segmentId,
                        candidate.Direction,
                        lane.Config.Name,
                        originStation,
                        candidate.Destination,
                        interceptionPosition,
                        interceptionRingIndex,
                        shipCount,
                        commodity,
                        requiredQuantity,
                        allocation,
                        GetConvoyRaidReward(difficulty),
                        difficulty,
                        GetConvoyRaidFlavor(i, lane.Config.Name, candidate.Destination.Name, commodity.Name, requiredQuantity),
                        offeredBy: $"{originStation.Name} Rogue Contact");
                    if (offer == null)
                        continue;

                    offer.MinimumEmployerReputation = GetConvoyRaidMinimumReputation(difficulty);
                    _convoyRaidOffers[key] = offer;
                }

                offers.Add(offer);
            }

            return offers;
        }

        private static Commodity SelectConvoyRaidCommodity(TradeLane lane, TradeLaneDirection direction, int variant)
        {
            List<Commodity> eligible = CommodityCatalog.All
                .Where(commodity => commodity != null && !string.IsNullOrWhiteSpace(commodity.Id) &&
                    !string.IsNullOrWhiteSpace(commodity.Name) && commodity.BasePrice > 0 &&
                    commodity.VolumePerUnit > 0 && !commodity.IsContraband && !commodity.IsMissionCargo)
                .OrderBy(commodity => commodity.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (eligible.Count == 0)
                return null;

            int hash = StableStringHash($"raid|{lane?.LaneId}|{direction}|{variant}");
            return eligible[hash % eligible.Count];
        }

        private static int GetConvoyRaidShipCount(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 4,
            MissionDifficulty.Medium => 3,
            _ => 2
        };

        private static int GetConvoyRaidRequiredQuantity(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 8,
            MissionDifficulty.Medium => 5,
            _ => 3
        };

        private static int[] GetConvoyRaidCargoAllocation(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => new[] { 3, 3, 3, 2 },
            MissionDifficulty.Medium => new[] { 3, 3, 2 },
            _ => new[] { 3, 2 }
        };

        private static int GetConvoyRaidReward(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 15_000,
            MissionDifficulty.Medium => 10_000,
            _ => 6_000
        };

        private static float GetConvoyRaidMinimumReputation(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => ReputationManager.FriendlyThreshold,
            MissionDifficulty.Medium => 0f,
            _ => 0f
        };

        private static string GetConvoyRaidFlavor(int variant, string laneName, string destinationName, string commodityName, int requiredQuantity)
        {
            string[] flavors =
            {
                $"A corporate shipment of {commodityName} is moving through the {laneName} corridor toward {destinationName}. Intercept the convoy and recover at least {requiredQuantity} units.",
                $"The Rogues want {requiredQuantity} {commodityName} units off the {laneName} route before the transports reach {destinationName}. Hit the convoy, crack the holds, and collect the cargo.",
                $"Commercial traffic is carrying {commodityName} through {laneName}. Interdict the shipment, recover {requiredQuantity} units from the wreckage, and get back to Rogue space."
            };
            return flavors[Math.Abs(variant) % flavors.Length];
        }

        private static int GetConvoyEscortShipCount(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 4,
            MissionDifficulty.Medium => 3,
            _ => 2
        };

        private static int GetConvoyEscortAttackForceSize(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 5,
            MissionDifficulty.Medium => 3,
            _ => 2
        };

        private static int GetConvoyEscortReward(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 13_000,
            MissionDifficulty.Medium => 8_500,
            _ => 5_000
        };

        private static float GetConvoyEscortMinimumReputation(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => ReputationManager.FriendlyThreshold,
            _ => 0f
        };

        private static string GetConvoyEscortFlavor(int variant, string laneName, string destinationName)
        {
            string[] flavors =
            {
                $"A civilian freight convoy requires protection en route to {destinationName}. Liberty Rogue activity has been reported along the {laneName} corridor.",
                $"Merchant ships need an armed escort through the {laneName}. Rendezvous, stay with the convoy, and get the survivors to {destinationName}.",
                $"Commercial traffic is being hunted on the {laneName}. Protect the convoy through the Rogue interception and deliver it to {destinationName}."
            };
            return flavors[Math.Abs(variant) % flavors.Length];
        }

        private static int GetTradeLaneReward(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Medium => 5_500,
            MissionDifficulty.Hard => 9_000,
            _ => 3_000
        };

        private static float GetTradeLanePolicePenalty(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Medium => TradeLaneMediumPolicePenalty,
            MissionDifficulty.Hard => TradeLaneHardPolicePenalty,
            _ => TradeLaneEasyPolicePenalty
        };

        private static float GetTradeLaneMinimumReputation(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Medium => 0f,
            MissionDifficulty.Hard => ReputationManager.FriendlyThreshold,
            _ => 0f
        };

        private static string GetTradeLaneFlavor(int variant, string laneName)
        {
            string[] flavors =
            {
                $"Cripple the {laneName} commercial corridor and open a window for Rogue raiders.",
                $"Interrupt Liberty shipping on the {laneName}; make the Police explain the dead lane.",
                $"Isolate transports on the {laneName} and embarrass Liberty security before the convoy reroutes."
            };
            return flavors[Math.Abs(variant) % flavors.Length];
        }

        private static int GetTradeLaneDefenseReward(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Medium => 6_500,
            MissionDifficulty.Hard => 10_500,
            MissionDifficulty.Deadly => 12_000,
            _ => 3_500
        };

        private static int GetTradeLaneDefenseAttackForceSize(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Medium => 3,
            MissionDifficulty.Hard => 4,
            MissionDifficulty.Deadly => 4,
            _ => 2
        };

        private static float GetTradeLaneDefenseActivationRadius(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard => 1500f,
            MissionDifficulty.Medium => 1350f,
            _ => 1200f
        };

        private static float GetTradeLaneDefenseMinimumReputation(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Medium => 0f,
            MissionDifficulty.Hard => ReputationManager.FriendlyThreshold,
            _ => 0f
        };

        private static string GetTradeLaneDefenseFlavor(int variant, string laneName)
        {
            string[] flavors =
            {
                $"Liberty Rogues are preparing an attack on the {laneName} trade corridor. Reach the designated segment and keep the route operational.",
                $"Police intelligence reports a Rogue disruption attempt on the {laneName}. Hold the assigned lane segment until the threat is broken.",
                $"Protect shipping through the {laneName}; intercept the Liberty Rogue attackers before they take the designated segment offline."
            };
            return flavors[Math.Abs(variant) % flavors.Length];
        }

        private static int StableStringHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty)
                    hash = hash * 31 + character;
                return hash & int.MaxValue;
            }
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
            ConfigureGeneratedReputationRequirement(mission);
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
                    MarketShortageState shortageState = _marketManager.GetShortageState(station, commodity);
                    if (shortage >= FreightMinimumShortageUnits && shortageState?.IsShortage == true)
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

        /// <summary>
        /// Player-facing market knowledge query. It compares only exact quotes
        /// the player has observed at visited stations. The older
        /// GetMarketOpportunities method above remains the bounded omniscient
        /// diagnostic path used by Phase 16 tests and development tooling.
        /// </summary>
        public IReadOnlyList<MarketOpportunity> GetKnownMarketOpportunities(int count = MarketOpportunityMaximumEntries)
        {
            int boundedCount = Math.Clamp(count, 0, MarketOpportunityMaximumEntries);
            if (boundedCount == 0 || _marketManager == null || _marketIntelligence == null)
                return Array.Empty<MarketOpportunity>();

            _marketIntelligence.RefreshCurrentStation();
            long now = _marketManager.ElapsedMilliseconds;
            List<MarketOpportunity> opportunities = new();
            IReadOnlyList<MarketKnowledgeStation> stations = _marketIntelligence.KnownStations;

            foreach (MarketKnowledgeStation station in stations)
            {
                foreach (MarketObservation observation in station.Observations)
                {
                    if (!IsExportCommodity(observation?.Commodity) || observation.BaselineStock <= 0 ||
                        observation.Stock < 0 || observation.BuyPrice <= 0 || observation.SellPrice <= 0)
                        continue;

                    long shortage = Math.Max(0L, (long)observation.BaselineStock - observation.Stock);
                    long surplus = Math.Max(0L, (long)observation.Stock - observation.BaselineStock);
                    MarketObservationAgeBand age = observation.GetAgeBand(now);
                    int ageFactor = GetAgeFactor(age);
                    if (shortage >= FreightMinimumShortageUnits &&
                        observation.Stock < (long)observation.BaselineStock * FreightShortageThresholdPercent / 100L)
                    {
                        int score = (int)Math.Clamp(
                            shortage * 100L * ageFactor / 100L + observation.DemandLevel * 10L,
                            0L,
                            int.MaxValue);
                        opportunities.Add(new MarketOpportunity(
                            MarketOpportunityType.Shortage,
                            observation.Commodity,
                            observation.StationName,
                            string.Empty,
                            string.Empty,
                            score,
                            (int)Math.Min(shortage, int.MaxValue),
                            $"SHORTAGE ({age.ToString().ToUpperInvariant()})",
                            Math.Max(0, observation.BuyPrice - observation.SellPrice),
                            station.StationId,
                            string.Empty,
                            age.ToString().ToUpperInvariant(),
                            string.Empty));
                    }

                    if (surplus >= ExportMinimumSurplusUnits &&
                        observation.Stock > (long)observation.BaselineStock * ExportSurplusThresholdPercent / 100L)
                    {
                        int score = (int)Math.Clamp(
                            surplus * 100L * ageFactor / 100L + observation.DemandLevel * 10L,
                            0L,
                            int.MaxValue);
                        opportunities.Add(new MarketOpportunity(
                            MarketOpportunityType.Surplus,
                            observation.Commodity,
                            observation.StationName,
                            string.Empty,
                            string.Empty,
                            score,
                            (int)Math.Min(surplus, int.MaxValue),
                            $"SURPLUS ({age.ToString().ToUpperInvariant()})",
                            Math.Max(0, observation.BuyPrice - observation.SellPrice),
                            station.StationId,
                            string.Empty,
                            age.ToString().ToUpperInvariant(),
                            string.Empty));
                    }
                }
            }

            foreach (MarketKnowledgeStation origin in stations)
            {
                foreach (MarketKnowledgeStation destination in stations)
                {
                    if (origin == null || destination == null ||
                        string.Equals(origin.StationId, destination.StationId, StringComparison.OrdinalIgnoreCase) ||
                        !_routeAuthority.TryGetRoute(origin, destination, out MarketRouteMetric route))
                        continue;

                    foreach (MarketObservation source in origin.Observations)
                    {
                        if (!IsExportCommodity(source?.Commodity) || source.BaselineStock <= 0 ||
                            source.Stock < (long)source.BaselineStock * 25L / 100L || source.BuyPrice <= 0)
                            continue;
                        if (!destination.TryGetObservation(source.Commodity.Id, out MarketObservation target) ||
                            !IsExportCommodity(target.Commodity) || target.SellPrice <= 0 || target.DemandLevel <= 0)
                            continue;

                        int spread = target.SellPrice - source.BuyPrice;
                        int minimumSpread = Math.Max(10, (int)Math.Ceiling(source.BuyPrice * 0.05d));
                        if (spread < minimumSpread) continue;

                        MarketObservationAgeBand sourceAge = source.GetAgeBand(now);
                        MarketObservationAgeBand destinationAge = target.GetAgeBand(now);
                        int score = CalculateKnownRouteScore(source, target, route, sourceAge, destinationAge);
                        opportunities.Add(new MarketOpportunity(
                            MarketOpportunityType.TradeRoute,
                            source.Commodity,
                            string.Empty,
                            origin.StationName,
                            destination.StationName,
                            score,
                            Math.Max(1, Math.Min(source.Stock, source.BaselineStock)),
                            "TRADE ROUTE",
                            spread,
                            origin.StationId,
                            destination.StationId,
                            sourceAge.ToString().ToUpperInvariant(),
                            destinationAge.ToString().ToUpperInvariant(),
                            (long)Math.Max(0f, route.DistanceUnits),
                            route.JumpCount));
                    }
                }
            }

            foreach (MarketMissionIntel intel in _marketIntelligence.MissionIntel)
            {
                Commodity commodity = _marketManager.ResolveCommodity(intel.CommodityId);
                if (commodity == null || commodity.IsMissionCargo) continue;
                opportunities.Add(new MarketOpportunity(
                    intel.Condition.Equals("SURPLUS", StringComparison.OrdinalIgnoreCase)
                        ? MarketOpportunityType.Surplus
                        : MarketOpportunityType.Shortage,
                    commodity,
                    intel.StationName,
                    string.Empty,
                    string.Empty,
                    50 + intel.Reward / 100,
                    intel.Quantity,
                    $"MISSION INTEL: {intel.Condition}",
                    0,
                    intel.StationId));
            }

            return opportunities
                .GroupBy(opportunity => $"{opportunity.Type}|{opportunity.CommodityId}|{opportunity.OriginStationId}|{opportunity.DestinationStationId}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(opportunity => opportunity.Score).First())
                .OrderByDescending(opportunity => opportunity.Score)
                .ThenBy(opportunity => opportunity.CommodityName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(opportunity => opportunity.OriginStationName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(opportunity => opportunity.DestinationStationName, StringComparer.OrdinalIgnoreCase)
                .Take(boundedCount)
                .ToList();
        }

        private static int CalculateKnownRouteScore(
            MarketObservation source,
            MarketObservation target,
            MarketRouteMetric route,
            MarketObservationAgeBand sourceAge,
            MarketObservationAgeBand destinationAge)
        {
            int stockFactor = source.BaselineStock > 0
                ? Math.Clamp(source.Stock * 100 / source.BaselineStock, 50, 150)
                : 50;
            int demandFactor = 100 + Math.Clamp(target.DemandLevel, 0, 10) * 5;
            int volumeFactor = Math.Clamp(100 / Math.Max(1, source.Commodity.VolumePerUnit), 25, 100);
            int ageFactor = GetAgeFactor(sourceAge) * GetAgeFactor(destinationAge) / 100;
            long distancePenalty = 100L + (long)Math.Ceiling(Math.Max(0f, route.DistanceUnits) / 1000f) * 2L + route.JumpCount * 40L;
            long numerator = (long)Math.Max(0, target.SellPrice - source.BuyPrice) * stockFactor *
                demandFactor * volumeFactor * ageFactor * 1000L;
            long denominator = 100L * 100L * 100L * 100L * Math.Max(1L, distancePenalty);
            return (int)Math.Clamp(numerator / Math.Max(1L, denominator), 1L, int.MaxValue);
        }

        private static int GetAgeFactor(MarketObservationAgeBand age) => age switch
        {
            MarketObservationAgeBand.Current => 100,
            MarketObservationAgeBand.Recent => 85,
            _ => 60
        };

        public int GetFreightReservedQuantity(Mission mission)
        {
            return mission?.Type == MissionType.FreightContract && _cargoHold != null
                ? _cargoHold.GetMissionCargoQuantity(mission.Id)
                : 0;
        }

        public int GetEmergencySupplyEligibleQuantity(Mission mission)
        {
            if (mission?.Type != MissionType.FreightContract || _cargoHold == null)
                return 0;

            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            return commodity == null ? 0 : _cargoHold.GetSellableCleanCommodityQuantity(commodity.Name);
        }

        public int GetExportIssuedQuantity(Mission mission)
        {
            return mission?.Type == MissionType.ExportContract && _cargoHold != null
                ? _cargoHold.GetMissionCargoQuantity(mission.Id)
                : 0;
        }

        public int GetSmugglingCargoQuantity(Mission mission)
        {
            return mission?.Type == MissionType.ContrabandSmuggling && _cargoHold != null
                ? _cargoHold.GetMissionCargoQuantity(mission.Id)
                : 0;
        }

        private List<Mission> GenerateFreightContracts(Station destination)
        {
            List<Mission> offers = new();
            if (destination == null || _marketManager == null || _worldManager == null ||
                !_marketManager.HasMarketConfigForStation(destination))
                return offers;

            IReadOnlyList<MarketShortageState> shortages = _marketManager
                .GetShortageStatesForStation(destination, matureOnly: true);
            HashSet<string> eligibleKeys = new(StringComparer.OrdinalIgnoreCase);
            foreach (MarketShortageState shortage in shortages)
            {
                if (!TryBuildEmergencySupplyTerms(
                        destination,
                        shortage,
                        out Commodity commodity,
                        out int quantity,
                        out int reward,
                        out Station suggestedSource))
                    continue;

                string key = BuildFreightOfferKey(destination, commodity);
                eligibleKeys.Add(key);
                if (_activeMissions.Any(mission => IsMatchingFreight(mission, destination, commodity)))
                    continue;

                if (!_freightOffers.TryGetValue(key, out Mission offer) ||
                    offer == null ||
                    offer.Status != MissionStatus.Available ||
                    offer.RequiredQuantity != quantity ||
                    offer.Reward != reward ||
                    !string.Equals(offer.SourceStationName, suggestedSource?.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    offer = Mission.CreateEmergencySupplyContract(
                        commodity,
                        destination,
                        quantity,
                        reward,
                        destination.Config?.SystemIndex ?? 0,
                        suggestedSource: suggestedSource,
                        offeredBy: $"{destination.Name} Authority",
                        factionId: _marketManager.GetMarketFactionId(destination));
                    ConfigureGeneratedReputationRequirement(offer);
                    _freightOffers[key] = offer;
                }

                if (offer != null)
                    offers.Add(offer);
            }

            foreach (string key in _freightOffers.Keys.Where(key => !eligibleKeys.Contains(key)).ToList())
                _freightOffers.Remove(key);

            return offers;
        }

        private bool TryBuildEmergencySupplyTerms(
            Station destination,
            MarketShortageState shortage,
            out Commodity commodity,
            out int quantity,
            out int reward,
            out Station suggestedSource)
        {
            commodity = shortage?.Commodity;
            quantity = 0;
            reward = 0;
            suggestedSource = null;
            if (destination == null || shortage == null || !shortage.IsShortage || !shortage.IsMature ||
                !IsExportCommodity(commodity) || shortage.Deficit < FreightMinimumShortageUnits)
            {
                return false;
            }

            int volumeBound = FreightMaximumCargoVolume / Math.Max(1, commodity.VolumePerUnit);
            int usefulDeficit = Math.Min(shortage.Deficit, Math.Max(0, shortage.TargetStock - shortage.CurrentStock));
            int requested = (int)Math.Min(
                int.MaxValue,
                ((long)usefulDeficit * FreightShortageSharePercent + 99L) / 100L);
            int maximumQuantity = Math.Min(FreightMaximumUnits, volumeBound);
            if (maximumQuantity < 2)
                return false;

            quantity = Math.Clamp(requested, 2, maximumQuantity);
            quantity = Math.Min(quantity, usefulDeficit);
            if (quantity <= 0)
                return false;

            suggestedSource = FindBestSupplySource(destination, commodity, quantity);
            StationMarketListing destinationListing = _marketManager.GetListingForCommodity(destination, commodity);
            reward = CalculateEmergencySupplyReward(destinationListing, commodity, quantity, shortage);
            return destinationListing != null && reward > 0;
        }

        private Station FindBestSupplySource(Station destination, Commodity commodity, int quantity)
        {
            Station best = null;
            long bestScore = long.MinValue;
            string destinationIdentity = Mission.BuildStationIdentity(destination);
            foreach (Station station in _worldManager.GetKnownStations())
            {
                if (station == null || string.Equals(Mission.BuildStationIdentity(station), destinationIdentity, StringComparison.OrdinalIgnoreCase) ||
                    !_marketManager.HasMarketConfigForStation(station))
                    continue;

                StationMarketListing listing = _marketManager.GetListingForCommodity(station, commodity);
                if (!IsExportCommodity(listing?.Commodity) || !listing.IsAvailable || listing.BuyPrice <= 0 ||
                    listing.Stock - listing.MinimumStock < quantity)
                    continue;

                long surplus = Math.Max(0L, (long)listing.Stock - listing.BaselineStock);
                long score = surplus * 1_000_000L - (long)listing.BuyPrice * 1_000L -
                    (long)Math.Max(0, station.Config?.SystemIndex ?? 0);
                if (best == null || score > bestScore ||
                    (score == bestScore && string.Compare(station.Name, best.Name, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = station;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int CalculateEmergencySupplyReward(
            StationMarketListing destinationListing,
            Commodity commodity,
            int quantity,
            MarketShortageState shortage)
        {
            long severityPremiumPercent = shortage?.TargetStock > 0
                ? 20L + Math.Clamp((long)shortage.Deficit * 30L * 100L / shortage.TargetStock / 100L, 0L, 30L)
                : 20L;
            long rawReward = (long)commodity.BasePrice * quantity * (120L + severityPremiumPercent) / 100L + 250L;
            if (destinationListing?.BuyPrice > 0)
                rawReward = Math.Max(rawReward, (long)destinationListing.BuyPrice * quantity + 250L);
            return (int)Math.Clamp(rawReward, 500L, FreightMaximumReward);
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
                        factionId: origin.FactionId);
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
            if (!CanPlayerAcceptMission(mission, out string acceptanceReason, originStation))
                return RejectAcceptance(mission, acceptanceReason);

            if (!string.IsNullOrWhiteSpace(mission.DefinitionId))
            {
                MissionDefinition definition = MissionCatalog.GetById(mission.DefinitionId);
                if (mission.Type is not MissionType.TradeLaneDisruption and not MissionType.TradeLaneDefense and not MissionType.ConvoyEscort and not MissionType.ConvoyRaid and not MissionType.ContrabandSmuggling &&
                    (definition == null || definition.Type != mission.Type ||
                    definition.RewardCredits != mission.Reward ||
                    definition.TargetCount != mission.RequiredProgress))
                    return RejectAcceptance(mission, "mission definition is invalid");
            }

            if (mission.Type == MissionType.DestroyHostiles && mission.RequiredProgress <= 0)
                return RejectAcceptance(mission, "hostile target count is invalid");
            if (mission.Type == MissionType.ReachLocation && string.IsNullOrWhiteSpace(mission.TargetLocation))
                return RejectAcceptance(mission, "patrol target metadata is invalid");

            if (mission.Type == MissionType.TradeLaneDisruption)
            {
                if (!string.Equals(FactionManager.NormalizeFactionId(mission.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                    originStation == null || !string.Equals(FactionManager.NormalizeFactionId(originStation.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(mission.TargetLaneId) || string.IsNullOrWhiteSpace(mission.TargetSegmentId) ||
                    mission.TargetRingIndex < 0 || mission.HoldDurationSeconds <= 0f)
                {
                    return RejectAcceptance(mission, "trade-lane disruption metadata or Rogue employer is invalid");
                }
            }

            if (mission.Type == MissionType.TradeLaneDefense)
            {
                if (!string.Equals(
                        FactionManager.NormalizeFactionId(mission.FactionId),
                        FactionManager.LibertyPolice,
                        StringComparison.OrdinalIgnoreCase) ||
                    originStation == null ||
                    !string.Equals(
                        FactionManager.NormalizeFactionId(originStation.FactionId),
                        FactionManager.LibertyPolice,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(mission.TargetLaneId) ||
                    string.IsNullOrWhiteSpace(mission.TargetSegmentId) ||
                    mission.TargetRingIndex <= 0 ||
                    mission.DefenseAttackForceSize is < 2 or > 4 ||
                    mission.DefenseFailureHoldSeconds <= 0f)
                {
                    return RejectAcceptance(mission, "trade-lane defense metadata or Police employer is invalid");
                }
            }

            if (mission.Type == MissionType.ConvoyEscort)
            {
                string employerFaction = FactionManager.NormalizeFactionId(mission.FactionId);
                string originFaction = FactionManager.NormalizeFactionId(originStation?.FactionId);
                if (originStation == null ||
                    (employerFaction != FactionManager.LibertyPolice && employerFaction != FactionManager.LibertyCorporations) ||
                    (originFaction != FactionManager.LibertyPolice && originFaction != FactionManager.LibertyCorporations &&
                     originFaction != FactionManager.NeutralCivilians) ||
                    string.IsNullOrWhiteSpace(mission.ConvoyRouteId) ||
                    string.IsNullOrWhiteSpace(mission.ConvoyRouteLaneId) ||
                    string.IsNullOrWhiteSpace(mission.ConvoyRouteSegmentId) ||
                    FactionManager.NormalizeFactionId(mission.ConvoyFactionId) != FactionManager.LibertyCorporations ||
                    FactionManager.NormalizeFactionId(mission.ConvoyHostileFactionId) != FactionManager.LibertyRogues ||
                    mission.ConvoyShipCount is < 2 or > 4 ||
                    mission.ConvoyRequiredSurvivors is < 1 or > 4 ||
                    mission.ConvoyAttackForceSize is < 2 or > 6 ||
                    !mission.ConvoyRendezvousPosition.HasValue ||
                    !mission.ConvoyEncounterPosition.HasValue ||
                    !mission.ConvoyDestinationPosition.HasValue ||
                    !TradeLaneStateSanitizer.IsFinite(mission.ConvoyRendezvousPosition.Value) ||
                    !TradeLaneStateSanitizer.IsFinite(mission.ConvoyEncounterPosition.Value) ||
                    !TradeLaneStateSanitizer.IsFinite(mission.ConvoyDestinationPosition.Value))
                {
                    return RejectAcceptance(mission, "convoy escort metadata or commercial employer is invalid");
                }
            }

            if (mission.Type == MissionType.ConvoyRaid)
            {
                string employerFaction = FactionManager.NormalizeFactionId(mission.FactionId);
                string originFaction = FactionManager.NormalizeFactionId(originStation?.FactionId);
                if (originStation == null || employerFaction != FactionManager.LibertyRogues ||
                    originFaction != FactionManager.LibertyRogues ||
                    string.IsNullOrWhiteSpace(mission.RaidRouteId) ||
                    string.IsNullOrWhiteSpace(mission.RaidRouteLaneId) ||
                    string.IsNullOrWhiteSpace(mission.RaidRouteSegmentId) ||
                    FactionManager.NormalizeFactionId(mission.RaidConvoyFactionId) != FactionManager.LibertyCorporations ||
                    mission.RaidShipCount is < 2 or > 4 ||
                    mission.RaidRequiredQuantity is < 1 or > 20 ||
                    mission.RaidCargoAllocation.Count != mission.RaidShipCount ||
                    mission.RaidCargoAllocation.Any(quantity => quantity <= 0) ||
                    mission.RaidTotalAllocatedQuantity != mission.RaidCargoAllocation.Sum() ||
                    mission.RaidTotalAllocatedQuantity < mission.RaidRequiredQuantity ||
                    mission.RaidTotalAllocatedQuantity > 40 ||
                    !mission.RaidInterceptionPosition.HasValue ||
                    !mission.RaidDestinationPosition.HasValue ||
                    !TradeLaneStateSanitizer.IsFinite(mission.RaidInterceptionPosition.Value) ||
                    !TradeLaneStateSanitizer.IsFinite(mission.RaidDestinationPosition.Value) ||
                    CommodityCatalog.GetByIdOrName(mission.RaidCommodityId) is not Commodity raidCommodity ||
                    raidCommodity.IsMissionCargo || raidCommodity.IsContraband || raidCommodity.VolumePerUnit <= 0 ||
                    string.IsNullOrWhiteSpace(mission.Destination) || string.IsNullOrWhiteSpace(mission.DestinationStationId))
                {
                    return RejectAcceptance(mission, "cargo-interdiction metadata or Rogue employer is invalid");
                }
            }

            if (mission.Type == MissionType.ContrabandSmuggling)
            {
                Commodity smugglingCommodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
                string originIdentity = Mission.BuildStationIdentity(originStation);
                if (originStation == null ||
                    FactionManager.NormalizeFactionId(originStation.FactionId) != FactionManager.LibertyRogues ||
                    FactionManager.NormalizeFactionId(mission.FactionId) != FactionManager.LibertyRogues ||
                    (!string.IsNullOrWhiteSpace(mission.OriginStationId) &&
                     !string.Equals(mission.OriginStationId, originIdentity, StringComparison.OrdinalIgnoreCase)) ||
                    string.IsNullOrWhiteSpace(mission.Destination) || string.IsNullOrWhiteSpace(mission.DestinationStationId) ||
                    string.Equals(originIdentity, mission.DestinationStationId, StringComparison.OrdinalIgnoreCase) ||
                    smugglingCommodity == null || !smugglingCommodity.IsContraband || smugglingCommodity.IsMissionCargo ||
                    smugglingCommodity.VolumePerUnit <= 0 || mission.RequiredQuantity <= 0 ||
                    mission.RequiredQuantity > FreightMaximumUnits || _cargoHold == null ||
                    !_cargoHold.CanFit(smugglingCommodity, mission.RequiredQuantity))
                {
                    return RejectAcceptance(mission, "smuggling metadata, Rogue origin, or cargo capacity is invalid");
                }

                Station resolvedDestination = ResolveKnownStation(mission.DestinationStationId, mission.Destination);
                if (resolvedDestination == null)
                    return RejectAcceptance(mission, "smuggling destination is unavailable");
            }

            if (mission.Type == MissionType.CourierDelivery &&
                (string.IsNullOrWhiteSpace(mission.PackageId) || mission.PackageQuantity <= 0 ||
                 string.IsNullOrWhiteSpace(mission.SourceStationName) || string.IsNullOrWhiteSpace(mission.Destination)))
            {
                return RejectAcceptance(mission, "courier metadata is invalid");
            }

            if (mission.Type == MissionType.FreightContract)
            {
                Commodity freightCommodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
                bool allowLegacyRewardOnlyFreight = _marketManager == null && _worldManager == null;
                if (freightCommodity == null || freightCommodity.IsMissionCargo || freightCommodity.IsContraband ||
                    freightCommodity.VolumePerUnit <= 0 || mission.RequiredQuantity <= 0 ||
                    string.IsNullOrWhiteSpace(mission.Destination) ||
                    string.IsNullOrWhiteSpace(mission.DestinationStationId) ||
                    _cargoHold == null ||
                    (!allowLegacyRewardOnlyFreight && (_marketManager == null || _worldManager == null)))
                {
                    return RejectAcceptance(mission, "freight contract metadata or cargo authority is invalid");
                }

                Station destination = ResolveKnownStation(mission.DestinationStationId, mission.Destination);
                string acceptedAt = Mission.BuildStationIdentity(originStation);
                if (originStation == null ||
                    !string.Equals(acceptedAt, mission.DestinationStationId, StringComparison.OrdinalIgnoreCase) ||
                    (!allowLegacyRewardOnlyFreight &&
                     (destination == null ||
                      !_marketManager.HasMarketConfigForStation(destination) ||
                      _marketManager.GetShortageState(destination, freightCommodity)?.IsShortage != true)))
                {
                    return RejectAcceptance(mission, "this emergency supply contract is no longer an active shortage");
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
            if (mission.Type == MissionType.FreightContract && _marketManager != null)
            {
                Station destination = ResolveKnownStation(mission.DestinationStationId, mission.Destination);
                Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
                if (!_marketManager.TryReserveSupplyContractCapacity(
                        mission.Id,
                        destination,
                        commodity,
                        mission.RequiredQuantity,
                        out string capacityFailure))
                {
                    return RejectAcceptance(mission, capacityFailure);
                }
            }
            if (mission.Type == MissionType.CourierDelivery &&
                !string.Equals(mission.SourceStationName, mission.OriginStationName, StringComparison.OrdinalIgnoreCase))
            {
                return RejectAcceptance(mission, $"courier must be accepted at {mission.SourceStationName}");
            }

            if (mission.Type == MissionType.ExportContract &&
                !TryIssueExportCargo(mission, originStation, out string exportFailureReason))
            {
                return RejectAcceptance(mission, exportFailureReason);
            }

            if (mission.Type == MissionType.ContrabandSmuggling &&
                !TryIssueSmugglingCargo(mission, out string smugglingFailureReason))
            {
                return RejectAcceptance(mission, smugglingFailureReason);
            }

            mission.AcceptedAtUtc = DateTime.UtcNow;
            mission.ReputationReward = GetMissionReputationReward(mission);
            mission.ReputationRewardApplied = false;
            mission.Status = MissionStatus.Accepted;

            if (_worldManager != null && !_worldManager.TryAcceptMission(mission, out string failureReason))
            {
                if (mission.Type == MissionType.ExportContract)
                {
                    TryRestoreExportShipment(mission, out _);
                }
                else if (mission.Type == MissionType.ContrabandSmuggling)
                {
                    TryRemoveIssuedSmugglingCargo(mission, out _);
                }
                ReleaseFreightReservation(mission);
                _marketManager?.ReleaseSupplyContractCapacity(mission.Id);
                mission.Status = MissionStatus.Available;
                _worldManager.OnMissionFinished(mission);
                return RejectAcceptance(mission, $"mission unavailable: {failureReason}");
            }

            _activeMissions.Add(mission);
            _waypointSystem?.RegisterMission(mission);
            mission.Status = MissionStatus.InProgress;
            _marketIntelligence?.RecordMissionIntel(mission);
            _notificationManager?.ShowMessage($"Mission accepted: {mission.Title}", 3f);
            if (mission.Type == MissionType.CourierDelivery)
            {
                _notificationManager?.ShowMessage($"Mission cargo loaded: {mission.GetCargoLabel()}", 3f);
            }
            else if (mission.Type == MissionType.FreightContract)
            {
                _notificationManager?.ShowMessage(
                    $"Supply cargo: {GetEmergencySupplyEligibleQuantity(mission)}/{mission.RequiredQuantity} eligible clean units",
                    3f);
            }
            else if (mission.Type == MissionType.ExportContract)
            {
                _notificationManager?.ShowMessage(
                    $"Export cargo loaded: {GetExportIssuedQuantity(mission)}/{mission.RequiredQuantity} units",
                    3f);
            }
            else if (mission.Type == MissionType.ContrabandSmuggling)
            {
                _notificationManager?.ShowMessage(
                    $"Smuggling cargo loaded: {GetSmugglingCargoQuantity(mission)}/{mission.RequiredQuantity} units",
                    3f);
            }
            Console.WriteLine($"[MISSION] Accepted: {mission.GetSummary()} | Origin: {mission.OriginStationName}");
            LastAcceptanceFailureReason = string.Empty;
            return true;
        }

        private bool RejectAcceptance(Mission mission, string reason)
        {
            LastAcceptanceFailureReason = string.IsNullOrWhiteSpace(reason) ? "mission acceptance rejected" : reason;
            _notificationManager?.ShowMessage(reason, 3f);
            Console.WriteLine($"[MISSION] Rejected: {mission?.Title ?? "<null>"} | Reason: {reason}");
            return false;
        }

        private static void ConfigureGeneratedReputationRequirement(Mission mission)
        {
            if (mission == null || mission.HasReputationRequirement)
                return;

            if (mission.Difficulty == MissionDifficulty.Hard)
                mission.MinimumEmployerReputation = ReputationManager.FriendlyThreshold;
            else if (mission.Difficulty == MissionDifficulty.Deadly)
                mission.MinimumEmployerReputation = ReputationManager.AlliedThreshold;
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

        private bool TryIssueSmugglingCargo(Mission mission, out string failureReason)
        {
            failureReason = string.Empty;
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            if (mission == null || commodity == null || !commodity.IsContraband || commodity.IsMissionCargo ||
                commodity.VolumePerUnit <= 0 || mission.RequiredQuantity <= 0 || _cargoHold == null)
            {
                failureReason = "smuggling cargo metadata is invalid";
                return false;
            }

            if (!_cargoHold.CanFit(commodity, mission.RequiredQuantity))
            {
                failureReason = $"not enough cargo space for {commodity.Name} x{mission.RequiredQuantity}";
                return false;
            }

            if (!_cargoHold.AddMissionCargo(mission.Id, commodity, mission.RequiredQuantity))
            {
                failureReason = "smuggling cargo could not be loaded";
                return false;
            }

            mission.IssuedCargoQuantity = mission.RequiredQuantity;
            mission.MissionCargoLoaded = true;
            mission.DeliveredQuantity = 0;
            mission.SmugglingStage = ContrabandSmugglingStage.EnRoute;
            mission.SmugglingPoliceDetected = false;
            mission.SmugglingJettisonedQuantity = 0;
            return true;
        }

        private bool TryRemoveIssuedSmugglingCargo(Mission mission, out string failureReason)
        {
            failureReason = string.Empty;
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            int quantity = mission != null && mission.IssuedCargoQuantity > 0
                ? mission.IssuedCargoQuantity
                : mission?.RequiredQuantity ?? 0;
            if (mission?.Type != MissionType.ContrabandSmuggling || commodity == null || quantity <= 0 || _cargoHold == null)
            {
                failureReason = "smuggling cargo rollback metadata is invalid";
                return false;
            }

            if (!_cargoHold.RemoveMissionCargo(mission.Id, commodity, quantity))
            {
                failureReason = "smuggling cargo rollback failed";
                return false;
            }

            mission.MissionCargoLoaded = false;
            mission.IssuedCargoQuantity = 0;
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
            _marketManager?.ReleaseSupplyContractCapacity(mission.Id);
            ReleaseConvoyRaidCargo(mission);
            ReleaseSmugglingCargo(mission);
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
            string completionMessage = mission.Type == MissionType.ContrabandSmuggling
                ? $"Smuggling cargo delivered - +{mission.Reward:N0} CR"
                : mission.Type == MissionType.ConvoyRaid
                ? $"Cargo secured - return to {mission.OriginStationName} to claim {mission.Reward:N0} CR"
                : mission.Type == MissionType.CourierDelivery
                ? $"Cargo delivered - return to {mission.OriginStationName} to claim {mission.Reward:N0} CR"
                : mission.Type == MissionType.FreightContract
                    ? $"Emergency supply delivered - +{mission.Reward:N0} CR"
                : mission.Type == MissionType.ExportContract
                    ? $"Export delivered - +{mission.Reward:N0} CR"
                : $"Objective complete - return to {mission.OriginStationName} to claim {mission.Reward:N0} CR";
            _notificationManager?.ShowMessage(completionMessage, 4f);
            Console.WriteLine($"[MISSION] Objective complete: {mission.Title} | Reward {(mission.Type is MissionType.FreightContract or MissionType.ExportContract or MissionType.ContrabandSmuggling ? "paid" : "pending")}: {mission.Reward:N0} CR");
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

            _marketManager?.ReleaseSupplyContractCapacity(mission.Id);
            CompleteMission(mission);
            mission.RewardPaid = true;
            _playerCredits.AddCredits(mission.Reward);
            ApplyMissionReputationReward(mission);
            _notificationManager?.ShowMessage($"Emergency supply reward received: {mission.Reward:N0} CR", 4f);
            message = $"Emergency supply reward received: {mission.Reward:N0} CR";
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
            ApplyMissionReputationReward(mission);
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

        public bool CompleteSmugglingMission(Mission mission, Station station, out string message)
        {
            message = string.Empty;
            if (mission == null || mission.Type != MissionType.ContrabandSmuggling ||
                !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
            {
                message = "smuggling mission is not active";
                return false;
            }

            if (station == null || !string.Equals(
                    Mission.BuildStationIdentity(station),
                    mission.DestinationStationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                message = $"deliver the cargo at {mission.GetDestinationLabel()}";
                return false;
            }

            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            int quantity = mission.RequiredQuantity;
            if (commodity == null || !commodity.IsContraband || commodity.IsMissionCargo || quantity <= 0 ||
                mission.IssuedCargoQuantity != quantity || _cargoHold == null ||
                !_cargoHold.HasMissionCargo(mission.Id, commodity.Id, quantity) ||
                _cargoHold.GetMissionCargoQuantity(mission.Id) != quantity)
            {
                message = "smuggling cargo is missing or corrupt";
                return false;
            }

            if (_playerCredits == null || mission.Reward <= 0 ||
                (long)_playerCredits.Credits + mission.Reward > int.MaxValue)
            {
                message = "smuggling reward transaction is invalid";
                return false;
            }

            if (!_cargoHold.RemoveMissionCargo(mission.Id, commodity, quantity))
            {
                message = "smuggling cargo could not be verified for payment";
                return false;
            }

            mission.DeliveredQuantity = quantity;
            mission.MissionCargoLoaded = false;
            mission.ObjectiveComplete = true;
            mission.SmugglingStage = ContrabandSmugglingStage.Successful;
            CompleteMission(mission);
            mission.RewardPaid = true;
            _playerCredits.AddCredits(mission.Reward);
            ApplyMissionReputationReward(mission);
            mission.Status = MissionStatus.Rewarded;
            _completedMissions.Remove(mission);
            _notificationManager?.ShowMessage($"Smuggling delivered — +{mission.Reward:N0} CR", 4f);
            message = $"Smuggling reward received: {mission.Reward:N0} CR";
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

        private void ReleaseConvoyRaidCargo(Mission mission)
        {
            if (mission?.Type == MissionType.ConvoyRaid)
                _cargoHold?.ConvertMissionCargoToOrdinary(mission.Id);
        }

        private void ReleaseSmugglingCargo(Mission mission)
        {
            if (mission?.Type != MissionType.ContrabandSmuggling || _cargoHold == null)
                return;

            _cargoHold.ConvertMissionCargoToOrdinary(mission.Id);
            mission.MissionCargoLoaded = false;
            mission.SmugglingStage = ContrabandSmugglingStage.Failed;
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

            if (mission.Type == MissionType.ConvoyRaid)
            {
                Commodity raidCommodity = CommodityCatalog.GetByIdOrName(mission.RaidCommodityId);
                int recovered = _cargoHold?.GetMissionCargoQuantity(mission.Id) ?? 0;
                if (raidCommodity == null || recovered < mission.RaidRequiredQuantity)
                {
                    message = $"recover {Math.Max(0, mission.RaidRequiredQuantity - recovered)} more units of {mission.RaidCommodityId}";
                    return false;
                }

                if (!_cargoHold.RemoveMissionCargoQuantity(
                        mission.Id,
                        raidCommodity,
                        mission.RaidRequiredQuantity,
                        out int removed) || removed != mission.RaidRequiredQuantity)
                {
                    message = "mission cargo could not be verified for payment";
                    return false;
                }

                mission.RaidCargoRecoveredQuantity = Math.Max(0, recovered - removed);
                _cargoHold.ConvertMissionCargoToOrdinary(mission.Id);
            }

            mission.RewardPaid = true;
            _playerCredits.AddCredits(mission.Reward);
            mission.Status = MissionStatus.Rewarded;
            _completedMissions.Remove(mission);
            ApplyMissionReputationReward(mission);
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
            _marketManager?.ReleaseSupplyContractCapacity(mission.Id);
            ReleaseConvoyRaidCargo(mission);
            ReleaseSmugglingCargo(mission);
            mission.FailureReason = string.IsNullOrWhiteSpace(reason) ? "mission failed" : reason;
            if (mission.Type == MissionType.ConvoyEscort)
                mission.ConvoyStage = ConvoyEscortStage.Failed;
            if (mission.Type == MissionType.ConvoyRaid)
                mission.RaidStage = ConvoyRaidStage.Failed;
            if (mission.Type == MissionType.ContrabandSmuggling)
                mission.SmugglingStage = ContrabandSmugglingStage.Failed;
            mission.Status = MissionStatus.Failed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);
            _worldManager?.OnMissionFinished(mission);
            _reputationManager?.AdjustReputation(
                mission.FactionId,
                MissionFailureReputationPenalty,
                ReputationChangeReason.MissionFailed);
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

        public bool RecordTradeLaneDisruption(Mission mission, TradeLaneDisruptionInfo disruption)
        {
            if (mission == null || disruption == null ||
                !ReferenceEquals(ActiveMission, mission) ||
                mission.Type != MissionType.TradeLaneDisruption ||
                disruption.Source != TradeLaneDisruptionSource.Player ||
                !string.Equals(mission.TargetLaneId, disruption.LaneId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(mission.TargetSegmentId, disruption.SegmentId, StringComparison.OrdinalIgnoreCase) ||
                mission.PoliceConsequenceApplied)
            {
                return false;
            }

            mission.PoliceConsequenceApplied = true;
            if (_reputationManager != null && Math.Abs(mission.PoliceReputationPenalty) >= ReputationManager.Precision)
            {
                _reputationManager.TemporaryHostility.RecordHostileAction(
                    FactionManager.LibertyPolice,
                    "trade-lane infrastructure disruption",
                    FactionCombatConsequenceService.TemporaryHostilityDurationSeconds,
                    causedByPlayerAggression: true);
                _reputationManager.AdjustReputationDirect(
                    FactionManager.LibertyPolice,
                    mission.PoliceReputationPenalty,
                    ReputationChangeReason.TradeLaneDisrupted);
            }

            Console.WriteLine($"[MISSION] Player-attributed trade-lane disruption qualified: {disruption.SegmentId} (mission #{mission.Id})");
            return true;
        }

        private void ApplyMissionReputationReward(Mission mission)
        {
            if (mission == null || mission.ReputationRewardApplied)
                return;

            mission.ReputationReward = GetMissionReputationReward(mission);
            mission.ReputationRewardApplied = true;
            _reputationManager?.AdjustReputation(
                mission.FactionId,
                mission.ReputationReward,
                ReputationChangeReason.MissionCompleted);
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
