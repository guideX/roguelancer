using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    public enum MissionType
    {
        ReachLocation,
        DestroyHostiles,
        Delivery,
        CourierDelivery,
        // Phase 60 canonical identity. FreightContract remains an enum alias
        // so Phase 15 saves/callers continue to resolve the same mission.
        EmergencySupply,
        FreightContract = EmergencySupply,
        ExportContract,
        Bounty,
        Escort,
        TradeLaneDisruption,
        TradeLaneDefense,
        ConvoyEscort,
        ConvoyRaid,
        ShipmentInterdiction,
        ContrabandSmuggling
    }

    public enum TradeLaneDefenseStage
    {
        EnRoute,
        AttackActive,
        AwaitingRecovery,
        Successful,
        Failed
    }

    public enum ConvoyEscortStage
    {
        Rendezvous,
        Escorting,
        EncounterActive,
        ApproachingDestination,
        Successful,
        Failed
    }

    public enum ConvoyRaidStage
    {
        EnRoute,
        InterceptionActive,
        CargoRecovery,
        Successful,
        Failed
    }

    public enum ShipmentInterdictionStage
    {
        Intercept,
        Recover,
        Return,
        Successful,
        Failed
    }

    public enum ContrabandSmugglingStage
    {
        EnRoute,
        DeliveryReady,
        Successful,
        Failed
    }

    public enum MissionDifficulty
    {
        Easy,
        Medium,
        Hard,
        Deadly
    }

    /// <summary>
    /// Explicit mission lifecycle. Active remains a source-compatible alias
    /// for the pre-Phase-11 navigation code.
    /// </summary>
    public enum MissionStatus
    {
        Available,
        Accepted,
        InProgress,
        Active = InProgress,
        Completed,
        Failed,
        Rewarded
    }

    public sealed class MissionDefinition
    {
        public MissionDefinition(
            string id,
            string title,
            string description,
            MissionType type,
            int rewardCredits,
            string targetLocation,
            int targetCount = 1,
            int targetSystemIndex = 0,
            string targetFactionId = null,
            string sourceStationName = null,
            string destinationStationName = null,
            string packageId = null,
            int packageQuantity = 0,
            int packageVolume = 0,
            MissionDifficulty difficulty = MissionDifficulty.Easy,
            float? minimumEmployerReputation = null,
            float? maximumEmployerReputation = null)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Type = type;
            RewardCredits = rewardCredits;
            TargetLocation = targetLocation ?? string.Empty;
            TargetCount = targetCount;
            TargetSystemIndex = targetSystemIndex;
            TargetFactionId = FactionManager.NormalizeFactionId(targetFactionId);
            SourceStationName = sourceStationName ?? string.Empty;
            DestinationStationName = destinationStationName ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            PackageQuantity = packageQuantity;
            PackageVolume = packageVolume;
            Difficulty = difficulty;
            MinimumEmployerReputation = NormalizeRequirement(minimumEmployerReputation);
            MaximumEmployerReputation = NormalizeRequirement(maximumEmployerReputation);
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public MissionType Type { get; }
        public int RewardCredits { get; }
        public string TargetLocation { get; }
        public int TargetCount { get; }
        public int TargetSystemIndex { get; }
        public string TargetFactionId { get; }
        public string SourceStationName { get; }
        public string DestinationStationName { get; }
        public string PackageId { get; }
        public int PackageQuantity { get; }
        public int PackageVolume { get; }
        public MissionDifficulty Difficulty { get; }
        public float? MinimumEmployerReputation { get; }
        public float? MaximumEmployerReputation { get; }

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(Id)) { reason = "definition id is empty"; return false; }
            if (string.IsNullOrWhiteSpace(Title)) { reason = "definition title is empty"; return false; }
            if (string.IsNullOrWhiteSpace(Description)) { reason = "definition description is empty"; return false; }
            if (RewardCredits <= 0) { reason = "reward must be positive"; return false; }
            if (TargetCount <= 0) { reason = "target count must be positive"; return false; }
            if ((Type == MissionType.ReachLocation || Type == MissionType.DestroyHostiles) &&
                string.IsNullOrWhiteSpace(TargetLocation))
            {
                reason = "prototype target location is empty";
                return false;
            }

            if (Type == MissionType.CourierDelivery)
            {
                if (string.IsNullOrWhiteSpace(SourceStationName)) { reason = "courier source station is empty"; return false; }
                if (string.IsNullOrWhiteSpace(DestinationStationName)) { reason = "courier destination station is empty"; return false; }
                if (string.IsNullOrWhiteSpace(PackageId)) { reason = "courier package id is empty"; return false; }
                if (PackageQuantity <= 0) { reason = "courier package quantity must be positive"; return false; }
                if (PackageVolume < 0) { reason = "courier package volume cannot be negative"; return false; }
            }

            reason = string.Empty;
            return true;
        }

        private static float? NormalizeRequirement(float? value)
        {
            if (!value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value))
                return null;

            return Math.Clamp(MathF.Round(value.Value, 4, MidpointRounding.AwayFromZero),
                ReputationManager.MinimumStanding,
                ReputationManager.MaximumStanding);
        }
    }

    /// <summary>Fixed Phase 11 board catalog; UI code does not own metadata.</summary>
    public static class MissionCatalog
    {
        public const string PatrolSweepId = "patrol-sweep";
        public const string RogueHuntId = "rogue-hunt";
        public const string PriorityDispatchId = "priority-dispatch";
        public const string TradeLaneDisruptionId = "trade-lane-disruption";
        public const string TradeLaneDefenseId = "trade-lane-defense";
        public const string ConvoyEscortId = "convoy-escort";
        public const string ConvoyRaidId = "convoy-raid";
        public const string ShipmentInterdictionId = "shipment-interdiction";
        public const string ContrabandSmugglingId = "contraband-smuggling";

        private static readonly IReadOnlyList<MissionDefinition> Definitions = new[]
        {
            new MissionDefinition(
                PatrolSweepId,
                "Patrol Sweep",
                "Check the patrol marker outside the originating station.",
                MissionType.ReachLocation,
                1500,
                "Origin station patrol marker"),
            new MissionDefinition(
                RogueHuntId,
                "Rogue Hunt",
                "Clear a small mission-designated rogue flight near the station.",
                MissionType.DestroyHostiles,
                4000,
                "Mission rogue flight",
                targetCount: 3,
                targetFactionId: FactionManager.LibertyRogues),
            new MissionDefinition(
                PriorityDispatchId,
                "Priority Dispatch",
                "Deliver a sealed data package to Buffalo Base.",
                MissionType.CourierDelivery,
                2500,
                "Buffalo Base",
                targetSystemIndex: 1,
                targetFactionId: FactionManager.LibertyCorporations,
                sourceStationName: "Newark Station",
                destinationStationName: "Buffalo Base",
                packageId: "sealed-data-package",
                packageQuantity: 1,
                packageVolume: 1,
                difficulty: MissionDifficulty.Hard,
                minimumEmployerReputation: ReputationManager.FriendlyThreshold)
        };

        public static IReadOnlyList<MissionDefinition> All => Definitions;

        public static MissionDefinition GetById(string id)
        {
            return Definitions.FirstOrDefault(definition =>
                string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static List<Mission> CreateRuntimeMissions(string offeredBy = "Mission Board", string factionId = null)
        {
            return Definitions.Select(definition => Mission.FromDefinition(definition, offeredBy, factionId)).ToList();
        }

        public static bool Validate(out string reason)
        {
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (MissionDefinition definition in Definitions)
            {
                if (!definition.IsValid(out reason)) return false;
                if (!ids.Add(definition.Id))
                {
                    reason = $"duplicate mission id '{definition.Id}'";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Runtime mission state. Definition metadata is copied at acceptance;
    /// progress, origin, target binding, and reward state live here.
    /// </summary>
    public class Mission
    {
        private static int _nextId = 1;

        public int Id { get; }
        public string DefinitionId { get; private set; }
        public string Title { get; }
        public MissionType Type { get; }
        public MissionDifficulty Difficulty { get; }
        public MissionStatus Status { get; set; }
        public string Target { get; }
        public string Destination { get; }
        public int Reward { get; }
        public int RewardCredits => Reward;
        public float TimeLimit { get; }
        public float ElapsedTime { get; set; }
        public string Description { get; }
        public string OfferedBy { get; set; }
        public string FactionId { get; set; }
        public string BountyTargetFactionId { get; set; }
        public float? MinimumEmployerReputation { get; set; }
        public float? MaximumEmployerReputation { get; set; }

        public string TargetLocation { get; set; }
        public int TargetSystemIndex { get; set; }
        public int TargetCount { get; set; }
        public int RequiredProgress { get; set; }
        public int CurrentProgress { get; set; }
        public int ObjectiveRadius { get; set; } = 500;

        public string OriginStationId { get; set; } = string.Empty;
        public string OriginStationName { get; set; } = string.Empty;
        public int OriginSystemIndex { get; set; }
        public string SourceStationName { get; set; } = string.Empty;
        public string DestinationStationId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public int PackageQuantity { get; set; }
        public int PackageVolume { get; set; }
        public bool MissionCargoLoaded { get; set; }
        public int DeliveredQuantity { get; set; }
        public string CommodityId { get; set; } = string.Empty;
        public int RequiredQuantity { get; set; }
        public int IssuedCargoQuantity { get; set; }
        public ContrabandSmugglingStage SmugglingStage { get; set; } = ContrabandSmugglingStage.EnRoute;
        public bool SmugglingPoliceDetected { get; set; }
        public int SmugglingJettisonedQuantity { get; set; }
        public DateTime AcceptedAtUtc { get; set; }
        public bool RewardPaid { get; set; }
        /// <summary>
        /// Fixed when the mission is accepted. Older saves with no field use
        /// MissionManager's deterministic type/difficulty fallback.
        /// </summary>
        public float ReputationReward { get; set; }
        public bool ReputationRewardApplied { get; set; }

        public bool ObjectiveComplete { get; set; }
        public Vector3? TargetPosition { get; set; }
        public SpaceObject TargetSpaceObject { get; set; }

        // Trade-lane disruption missions persist canonical target identity. The
        // lane/ring object itself is always rebound from these IDs at runtime.
        public string TargetLaneId { get; set; } = string.Empty;
        public string TargetSegmentId { get; set; } = string.Empty;
        public int TargetRingIndex { get; set; } = -1;
        public float HoldDurationSeconds { get; set; }
        public float HoldProgressSeconds { get; set; }
        public bool PlayerDisruptionObserved { get; set; }
        public double LastQualifiedDisruptionAtSeconds { get; set; } = -1d;
        public float PoliceReputationPenalty { get; set; }
        public bool PoliceConsequenceApplied { get; set; }
        public bool SecurityResponseTriggered { get; set; }
        public string FailureReason { get; set; } = string.Empty;

        // Defense attackers are transient world objects. These fields keep
        // the durable encounter contract saveable and deterministic.
        public TradeLaneDefenseStage DefenseStage { get; set; } = TradeLaneDefenseStage.EnRoute;
        public int DefenseAttackForceSize { get; set; }
        public int DefenseAttackersRemaining { get; set; }
        public float DefenseActivationRadius { get; set; }
        public float DefenseFailureHoldSeconds { get; set; }
        public float DefenseFailureHoldProgressSeconds { get; set; }
        public bool DefenseActivationStarted { get; set; }

        // Convoy escort state is durable mission data. Convoy and Rogue NPC
        // instances are transient and are reconstructed from these bounded
        // identities after a load or world rebind.
        public string ConvoyRouteId { get; set; } = string.Empty;
        public string ConvoyRouteLaneId { get; set; } = string.Empty;
        public string ConvoyRouteSegmentId { get; set; } = string.Empty;
        public TradeLaneDirection ConvoyRouteDirection { get; set; } = TradeLaneDirection.Forward;
        public string ConvoyFactionId { get; set; } = FactionManager.LibertyCorporations;
        public string ConvoyHostileFactionId { get; set; } = FactionManager.LibertyRogues;
        public string ConvoyShipArchetype { get; set; } = "Transport Ship Alpha";
        public int ConvoyRouteRingIndex { get; set; } = -1;
        public int ConvoyEncounterRingIndex { get; set; } = -1;
        public int ConvoyShipCount { get; set; }
        public int ConvoyRequiredSurvivors { get; set; } = 1;
        public int ConvoySurvivors { get; set; }
        public int ConvoyDestroyedCount { get; set; }
        public int ConvoyDestroyedMask { get; set; }
        public int ConvoyArrivedCount { get; set; }
        public int ConvoyArrivedMask { get; set; }
        public int ConvoyAttackForceSize { get; set; }
        public int ConvoyAttackersRemaining { get; set; }
        public ConvoyEscortStage ConvoyStage { get; set; } = ConvoyEscortStage.Rendezvous;
        public float ConvoyRendezvousRadius { get; set; } = 2200f;
        public float ConvoyAbandonmentRadius { get; set; } = 9000f;
        public float ConvoyAbandonmentGraceSeconds { get; set; } = 25f;
        public float ConvoyAbandonmentProgressSeconds { get; set; }
        public float ConvoyArrivalRadius { get; set; } = 3000f;
        public bool ConvoyRouteStarted { get; set; }
        public bool ConvoyEncounterActivated { get; set; }
        public bool ConvoyEncounterResolved { get; set; }
        public bool ConvoyEncounterSpawnAttempted { get; set; }
        public Vector3? ConvoyRendezvousPosition { get; set; }
        public Vector3? ConvoyEncounterPosition { get; set; }
        public Vector3? ConvoyDestinationPosition { get; set; }

        // Phase 63 dynamic escort metadata points at one real Phase 59
        // economic shipment. The convoy fields above remain the shared
        // lifecycle/state surface; no mission cargo is created here.
        public bool IsEconomicEscort { get; set; }
        public bool IsEconomicInterdiction { get; set; }
        public string EconomicShipmentTraderIdentity { get; set; } = string.Empty;
        public string EconomicShipmentRouteId { get; set; } = string.Empty;
        public int EconomicShipmentValue { get; set; }
        public int EconomicRouteRisk { get; set; }
        public string EconomicShortageLabel { get; set; } = string.Empty;
        public long EconomicOfferExpiresMilliseconds { get; set; }

        public int InterdictionSourceAvailableQuantity { get; set; }
        public int InterdictionReleasedQuantity { get; set; }
        public int InterdictionCargoLostQuantity { get; set; }
        public int InterdictionCargoRecoveredQuantity { get; set; }
        public int InterdictionRemainingPossibleQuantity { get; set; }
        public ShipmentInterdictionStage InterdictionStage { get; set; } = ShipmentInterdictionStage.Intercept;
        public bool InterdictionTargetDestroyed { get; set; }
        public bool InterdictionTargetDelivered { get; set; }

        public bool IsExpired => TimeLimit > 0 && ElapsedTime >= TimeLimit;
        public float TimeRemaining => TimeLimit > 0 ? Math.Max(0, TimeLimit - ElapsedTime) : -1;
        public bool IsActive => Status is MissionStatus.Accepted or MissionStatus.InProgress;
        public bool HasUnclaimedReward => Status == MissionStatus.Completed && !RewardPaid;
        public bool IsTradeLaneDisruptionMission() => Type == MissionType.TradeLaneDisruption;
        public bool IsTradeLaneDefenseMission() => Type == MissionType.TradeLaneDefense;
        public bool IsConvoyEscortMission() => Type == MissionType.ConvoyEscort;
        public bool IsConvoyRaidMission() => Type == MissionType.ConvoyRaid;
        public bool IsShipmentInterdictionMission() => Type == MissionType.ShipmentInterdiction;

        // Raid state is intentionally separate from Phase 50 escort state.
        // Route movement still belongs to TradeLane/TrafficManager, while the
        // cargo allocation and lifecycle masks belong only to this mission.
        public string RaidRouteId { get; set; } = string.Empty;
        public string RaidRouteLaneId { get; set; } = string.Empty;
        public string RaidRouteSegmentId { get; set; } = string.Empty;
        public TradeLaneDirection RaidRouteDirection { get; set; } = TradeLaneDirection.Forward;
        public string RaidConvoyFactionId { get; set; } = FactionManager.LibertyCorporations;
        public string RaidShipArchetype { get; set; } = "Transport Ship Alpha";
        public int RaidRouteRingIndex { get; set; } = -1;
        public int RaidInterceptionRingIndex { get; set; } = -1;
        public int RaidShipCount { get; set; }
        public int RaidDestroyedCount { get; set; }
        public int RaidEscapedCount { get; set; }
        public int RaidDestroyedMask { get; set; }
        public int RaidEscapedMask { get; set; }
        public int RaidCargoReleasedMask { get; set; }
        public int RaidCargoLostQuantity { get; set; }
        public int RaidCargoReleasedQuantity { get; set; }
        public int RaidCargoRecoveredQuantity { get; set; }
        public string RaidCommodityId { get; set; } = string.Empty;
        public int RaidRequiredQuantity { get; set; }
        public int RaidTotalAllocatedQuantity { get; set; }
        public int RaidRemainingPossibleQuantity { get; set; }
        public List<int> RaidCargoAllocation { get; } = new();
        public ConvoyRaidStage RaidStage { get; set; } = ConvoyRaidStage.EnRoute;
        public bool RaidRouteStarted { get; set; }
        public bool RaidInterceptionActivated { get; set; }
        public Vector3? RaidInterceptionPosition { get; set; }
        public Vector3? RaidDestinationPosition { get; set; }

        public Mission(
            MissionType type,
            MissionDifficulty difficulty,
            string target,
            string destination,
            int reward,
            float timeLimit,
            string description,
            string factionId = null,
            string title = null)
            : this(
                0,
                string.Empty,
                title ?? description,
                type,
                difficulty,
                MissionStatus.Available,
                target,
                destination,
                reward,
                timeLimit,
                description,
                string.Empty,
                factionId,
                destination,
                0,
                1,
                0,
                1,
                false,
                null,
                string.Empty,
                string.Empty,
                0,
                DateTime.MinValue,
                false)
        {
        }

        public static Mission CreateFreightContract(
            Commodity commodity,
            Station destination,
            int requiredQuantity,
            int reward,
            int targetSystemIndex,
            string offeredBy = "Mission Board",
            string factionId = null)
        {
            return CreateEmergencySupplyContract(
                commodity,
                destination,
                requiredQuantity,
                reward,
                targetSystemIndex,
                suggestedSource: null,
                offeredBy: offeredBy,
                factionId: factionId);
        }

        public static Mission CreateEmergencySupplyContract(
            Commodity commodity,
            Station destination,
            int requiredQuantity,
            int reward,
            int targetSystemIndex,
            Station suggestedSource = null,
            string offeredBy = "Mission Board",
            string factionId = null)
        {
            if (commodity == null || destination == null || requiredQuantity <= 0 || reward <= 0)
            {
                return null;
            }

            Mission mission = new Mission(
                MissionType.EmergencySupply,
                requiredQuantity >= 8 ? MissionDifficulty.Hard :
                    requiredQuantity >= 5 ? MissionDifficulty.Medium : MissionDifficulty.Easy,
                commodity.Name,
                destination.Name,
                reward,
                0f,
                BuildEmergencySupplyDescription(destination, commodity, requiredQuantity, suggestedSource),
                factionId ?? destination.FactionId,
                title: "Emergency Supply")
            {
                OfferedBy = offeredBy ?? "Mission Board",
                SourceStationName = suggestedSource?.Name ?? string.Empty,
                CommodityId = commodity.Id,
                RequiredQuantity = requiredQuantity,
                TargetLocation = destination.Name,
                TargetSystemIndex = Math.Max(0, targetSystemIndex),
                DestinationStationId = BuildStationIdentity(destination),
                RequiredProgress = requiredQuantity,
                TargetCount = requiredQuantity
            };

            return mission;
        }

        private static string BuildEmergencySupplyDescription(
            Station destination,
            Commodity commodity,
            int quantity,
            Station suggestedSource)
        {
            string source = suggestedSource == null
                ? string.Empty
                : $" Suggested source: {suggestedSource.Name}.";
            return $"{destination.Name} is experiencing a shortage of {commodity.Name}. Deliver {quantity} {commodity.Name} to the station.{source}";
        }

        public static Mission CreateExportContract(
            Station origin,
            Commodity commodity,
            Station destination,
            int quantity,
            int reward,
            int targetSystemIndex,
            string offeredBy = "Mission Board",
            string factionId = null)
        {
            if (origin == null || commodity == null || destination == null ||
                quantity <= 0 || reward <= 0 ||
                string.Equals(BuildStationIdentity(origin), BuildStationIdentity(destination), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Mission mission = new Mission(
                MissionType.ExportContract,
                quantity >= 20 ? MissionDifficulty.Medium : MissionDifficulty.Easy,
                commodity.Name,
                destination.Name,
                reward,
                0f,
                $"{origin.Name} has excess {commodity.Name}. Transport {quantity} {commodity.Name} to {destination.Name}. Cargo supplied on acceptance.",
                factionId ?? destination.FactionId,
                title: "Bulk Export Contract")
            {
                OfferedBy = offeredBy ?? "Mission Board",
                OriginStationId = BuildStationIdentity(origin),
                OriginStationName = origin.Name ?? string.Empty,
                OriginSystemIndex = origin.Config?.SystemIndex ?? 0,
                SourceStationName = origin.Name ?? string.Empty,
                CommodityId = commodity.Id,
                RequiredQuantity = quantity,
                TargetLocation = destination.Name,
                TargetSystemIndex = Math.Max(0, targetSystemIndex),
                DestinationStationId = BuildStationIdentity(destination),
                RequiredProgress = quantity,
                TargetCount = quantity
            };

            return mission;
        }

        public static Mission CreateContrabandSmuggling(
            Station origin,
            Station destination,
            Commodity commodity,
            int quantity,
            int reward,
            MissionDifficulty difficulty,
            string offeredBy = "Liberty Rogue Contact")
        {
            if (origin == null || destination == null || commodity == null || quantity <= 0 || reward <= 0 ||
                string.Equals(BuildStationIdentity(origin), BuildStationIdentity(destination), StringComparison.OrdinalIgnoreCase) ||
                !commodity.IsContraband || commodity.IsMissionCargo || commodity.VolumePerUnit <= 0)
            {
                return null;
            }

            string destinationName = destination.Name ?? string.Empty;
            string commodityName = commodity.Name ?? commodity.Id;
            Mission mission = new Mission(
                MissionType.ContrabandSmuggling,
                difficulty,
                commodityName,
                destinationName,
                reward,
                GetSmugglingTimeLimit(difficulty),
                $"Move {quantity} {commodityName} from {origin.Name} to {destinationName}. Liberty Police cargo inspections are active along the route.",
                FactionManager.LibertyRogues,
                title: $"Smuggle {commodityName}")
            {
                DefinitionId = MissionCatalog.ContrabandSmugglingId,
                OfferedBy = offeredBy ?? "Liberty Rogue Contact",
                OriginStationId = BuildStationIdentity(origin),
                OriginStationName = origin.Name ?? string.Empty,
                OriginSystemIndex = origin.Config?.SystemIndex ?? 0,
                SourceStationName = origin.Name ?? string.Empty,
                CommodityId = commodity.Id,
                RequiredQuantity = quantity,
                TargetLocation = destinationName,
                TargetSystemIndex = destination.Config?.SystemIndex ?? origin.Config?.SystemIndex ?? 0,
                DestinationStationId = BuildStationIdentity(destination),
                RequiredProgress = quantity,
                TargetCount = quantity,
                SmugglingStage = ContrabandSmugglingStage.EnRoute
            };
            mission.SetOrigin(origin);
            return mission;
        }

        private static float GetSmugglingTimeLimit(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 600f,
            MissionDifficulty.Medium => 750f,
            _ => 900f
        };

        public static Mission CreateTradeLaneDisruption(
            string laneId,
            string segmentId,
            int ringIndex,
            string laneName,
            Vector3 targetPosition,
            int targetSystemIndex,
            MissionDifficulty difficulty,
            int reward,
            float holdDurationSeconds,
            string description,
            float policeReputationPenalty,
            string offeredBy = "Liberty Rogue Contact")
        {
            if (string.IsNullOrWhiteSpace(laneId) || string.IsNullOrWhiteSpace(segmentId) ||
                ringIndex < 0 || string.IsNullOrWhiteSpace(laneName) || reward <= 0 ||
                holdDurationSeconds <= 0f || float.IsNaN(holdDurationSeconds) ||
                float.IsInfinity(holdDurationSeconds) || !TradeLaneStateSanitizer.IsFinite(targetPosition))
            {
                return null;
            }

            Mission mission = new Mission(
                MissionType.TradeLaneDisruption,
                difficulty,
                $"{laneName} [{segmentId}]",
                laneName,
                reward,
                0f,
                description,
                FactionManager.LibertyRogues,
                title: "Trade-Lane Disruption")
            {
                DefinitionId = MissionCatalog.TradeLaneDisruptionId,
                OfferedBy = offeredBy ?? "Liberty Rogue Contact",
                TargetLocation = laneName,
                TargetSystemIndex = Math.Max(0, targetSystemIndex),
                TargetCount = 1,
                RequiredProgress = 1,
                TargetPosition = targetPosition,
                TargetLaneId = laneId.Trim(),
                TargetSegmentId = segmentId.Trim(),
                TargetRingIndex = ringIndex,
                HoldDurationSeconds = Math.Clamp(holdDurationSeconds, 1f, 30f),
                HoldProgressSeconds = 0f,
                PoliceReputationPenalty = Math.Clamp(policeReputationPenalty, -0.05f, 0f)
            };
            return mission;
        }

        public static Mission CreateTradeLaneDefense(
            string laneId,
            string segmentId,
            int ringIndex,
            string laneName,
            Vector3 targetPosition,
            int targetSystemIndex,
            MissionDifficulty difficulty,
            int reward,
            int attackForceSize,
            float activationRadius,
            float failureHoldSeconds,
            string description,
            string offeredBy = "Liberty Police")
        {
            if (string.IsNullOrWhiteSpace(laneId) || string.IsNullOrWhiteSpace(segmentId) ||
                ringIndex < 0 || string.IsNullOrWhiteSpace(laneName) || reward <= 0 ||
                attackForceSize < 2 || attackForceSize > 4 ||
                activationRadius <= 0f || float.IsNaN(activationRadius) || float.IsInfinity(activationRadius) ||
                failureHoldSeconds <= 0f || float.IsNaN(failureHoldSeconds) ||
                float.IsInfinity(failureHoldSeconds) || !TradeLaneStateSanitizer.IsFinite(targetPosition))
            {
                return null;
            }

            Mission mission = new Mission(
                MissionType.TradeLaneDefense,
                difficulty,
                $"{laneName} [{segmentId}]",
                laneName,
                reward,
                0f,
                description,
                FactionManager.LibertyPolice,
                title: "Trade-Lane Defense")
            {
                DefinitionId = MissionCatalog.TradeLaneDefenseId,
                OfferedBy = offeredBy ?? "Liberty Police",
                TargetLocation = laneName,
                TargetSystemIndex = Math.Max(0, targetSystemIndex),
                TargetCount = attackForceSize,
                RequiredProgress = attackForceSize,
                TargetPosition = targetPosition,
                TargetLaneId = laneId.Trim(),
                TargetSegmentId = segmentId.Trim(),
                TargetRingIndex = ringIndex,
                DefenseStage = TradeLaneDefenseStage.EnRoute,
                DefenseAttackForceSize = attackForceSize,
                DefenseAttackersRemaining = 0,
                DefenseActivationRadius = Math.Clamp(activationRadius, 250f, 5000f),
                DefenseFailureHoldSeconds = Math.Clamp(failureHoldSeconds, 1f, 30f),
                DefenseFailureHoldProgressSeconds = 0f,
                DefenseActivationStarted = false
            };
            return mission;
        }

        public static Mission CreateConvoyEscort(
            string routeId,
            string laneId,
            string segmentId,
            TradeLaneDirection direction,
            string laneName,
            Station origin,
            Station destination,
            Vector3 rendezvousPosition,
            Vector3 encounterPosition,
            int encounterRingIndex,
            int convoySize,
            int attackForceSize,
            int reward,
            MissionDifficulty difficulty,
            string description,
            string offeredBy)
        {
            if (string.IsNullOrWhiteSpace(routeId) || string.IsNullOrWhiteSpace(laneId) ||
                string.IsNullOrWhiteSpace(segmentId) || string.IsNullOrWhiteSpace(laneName) ||
                origin == null || destination == null || ReferenceEquals(origin, destination) ||
                !TradeLaneStateSanitizer.IsFinite(rendezvousPosition) ||
                !TradeLaneStateSanitizer.IsFinite(encounterPosition) || encounterRingIndex < 1 ||
                convoySize is < 1 or > 4 || attackForceSize is < 2 or > 6 || reward <= 0)
            {
                return null;
            }

            Mission mission = new Mission(
                MissionType.ConvoyEscort,
                difficulty,
                "Merchant convoy",
                destination.Name,
                reward,
                0f,
                description,
                FactionManager.NormalizeFactionId(origin.FactionId),
                title: "Convoy Escort")
            {
                DefinitionId = MissionCatalog.ConvoyEscortId,
                OfferedBy = offeredBy ?? origin.Name ?? "Commercial Operations",
                TargetLocation = laneName,
                TargetSystemIndex = destination.Config?.SystemIndex ?? origin.Config?.SystemIndex ?? 0,
                TargetCount = convoySize,
                RequiredProgress = 1,
                TargetPosition = rendezvousPosition,
                DestinationStationId = BuildStationIdentity(destination),
                ConvoyRouteId = routeId.Trim(),
                ConvoyRouteLaneId = laneId.Trim(),
                ConvoyRouteSegmentId = segmentId.Trim(),
                ConvoyRouteDirection = direction,
                ConvoyFactionId = FactionManager.LibertyCorporations,
                ConvoyHostileFactionId = FactionManager.LibertyRogues,
                ConvoyShipArchetype = "Transport Ship Alpha",
                ConvoyRouteRingIndex = -1,
                ConvoyEncounterRingIndex = encounterRingIndex,
                ConvoyShipCount = convoySize,
                ConvoyRequiredSurvivors = 1,
                ConvoySurvivors = convoySize,
                ConvoyDestroyedCount = 0,
                ConvoyDestroyedMask = 0,
                ConvoyArrivedCount = 0,
                ConvoyArrivedMask = 0,
                ConvoyAttackForceSize = attackForceSize,
                ConvoyAttackersRemaining = 0,
                ConvoyStage = ConvoyEscortStage.Rendezvous,
                ConvoyRendezvousRadius = 2200f,
                ConvoyAbandonmentRadius = 9000f,
                ConvoyAbandonmentGraceSeconds = 25f,
                ConvoyAbandonmentProgressSeconds = 0f,
                ConvoyArrivalRadius = 3000f,
                ConvoyRouteStarted = false,
                ConvoyEncounterActivated = false,
                ConvoyEncounterResolved = false,
                ConvoyEncounterSpawnAttempted = false,
                ConvoyRendezvousPosition = rendezvousPosition,
                ConvoyEncounterPosition = encounterPosition,
                ConvoyDestinationPosition = destination.Position
            };
            mission.SetOrigin(origin);
            return mission;
        }

        public static Mission CreateEconomicEscort(
            EconomicShipment shipment,
            Station origin,
            Station destination,
            Vector3 rendezvousPosition,
            Vector3 encounterPosition,
            int routeRisk,
            int reward,
            MissionDifficulty difficulty,
            string shortageLabel,
            string offeredBy)
        {
            if (shipment == null || origin == null || destination == null ||
                string.IsNullOrWhiteSpace(shipment.TraderIdentity) ||
                string.IsNullOrWhiteSpace(shipment.RouteId) ||
                !TradeLaneStateSanitizer.IsFinite(rendezvousPosition) ||
                !TradeLaneStateSanitizer.IsFinite(encounterPosition) || reward <= 0)
                return null;

            Mission mission = CreateConvoyEscort(
                shipment.RouteId,
                shipment.RouteId,
                $"economic-escort:{shipment.RouteId}",
                shipment.RouteTowardEnd ? TradeLaneDirection.Forward : TradeLaneDirection.Reverse,
                $"Trade route {shipment.RouteId}",
                origin,
                destination,
                rendezvousPosition,
                encounterPosition,
                1,
                1,
                routeRisk >= TradeRouteRiskManager.SevereRiskThreshold ? 3 : 2,
                reward,
                difficulty,
                $"Protect the live trader shipment from {origin.Name} to {destination.Name}. " +
                $"Cargo value: {shipment.InitialManifestValue:N0} CR. Route condition: {TradeRouteRiskManager.GetRiskLabel(routeRisk)} ({routeRisk}/100)." +
                (string.IsNullOrWhiteSpace(shortageLabel) ? string.Empty : $" Destination demand: {shortageLabel}."),
                offeredBy);
            if (mission == null)
                return null;

            mission.IsEconomicEscort = true;
            mission.EconomicShipmentTraderIdentity = shipment.TraderIdentity;
            mission.EconomicShipmentRouteId = shipment.RouteId;
            mission.EconomicShipmentValue = Math.Max(0, shipment.InitialManifestValue);
            mission.EconomicRouteRisk = Math.Clamp(routeRisk, 0, TradeRouteRiskManager.MaximumRiskScore);
            mission.EconomicShortageLabel = shortageLabel ?? string.Empty;
            return mission;
        }

        public static Mission CreateConvoyRaid(
            string routeId,
            string laneId,
            string segmentId,
            TradeLaneDirection direction,
            string laneName,
            Station origin,
            Station destination,
            Vector3 interceptionPosition,
            int interceptionRingIndex,
            int convoySize,
            Commodity commodity,
            int requiredQuantity,
            IReadOnlyList<int> cargoAllocation,
            int reward,
            MissionDifficulty difficulty,
            string description,
            string offeredBy)
        {
            if (string.IsNullOrWhiteSpace(routeId) || string.IsNullOrWhiteSpace(laneId) ||
                string.IsNullOrWhiteSpace(segmentId) || string.IsNullOrWhiteSpace(laneName) ||
                origin == null || destination == null || ReferenceEquals(origin, destination) ||
                commodity == null || string.IsNullOrWhiteSpace(commodity.Id) ||
                commodity.VolumePerUnit <= 0 || requiredQuantity <= 0 ||
                !TradeLaneStateSanitizer.IsFinite(interceptionPosition) || interceptionRingIndex < 1 ||
                convoySize is < 2 or > 4 || reward <= 0 || cargoAllocation == null ||
                cargoAllocation.Count != convoySize || cargoAllocation.Any(quantity => quantity <= 0))
            {
                return null;
            }

            int totalAllocated = cargoAllocation.Sum();
            if (totalAllocated < requiredQuantity || totalAllocated > 40)
                return null;

            Mission mission = new Mission(
                MissionType.ConvoyRaid,
                difficulty,
                "Commercial convoy",
                destination.Name,
                reward,
                0f,
                description,
                FactionManager.LibertyRogues,
                title: "Cargo Interdiction")
            {
                DefinitionId = MissionCatalog.ConvoyRaidId,
                OfferedBy = offeredBy ?? "Liberty Rogue Contact",
                TargetLocation = laneName,
                TargetSystemIndex = destination.Config?.SystemIndex ?? origin.Config?.SystemIndex ?? 0,
                TargetCount = requiredQuantity,
                RequiredProgress = requiredQuantity,
                TargetPosition = interceptionPosition,
                DestinationStationId = BuildStationIdentity(destination),
                RaidRouteId = routeId.Trim(),
                RaidRouteLaneId = laneId.Trim(),
                RaidRouteSegmentId = segmentId.Trim(),
                RaidRouteDirection = direction,
                RaidConvoyFactionId = FactionManager.LibertyCorporations,
                RaidShipArchetype = "Transport Ship Alpha",
                RaidRouteRingIndex = -1,
                RaidInterceptionRingIndex = interceptionRingIndex,
                RaidShipCount = convoySize,
                RaidDestroyedCount = 0,
                RaidEscapedCount = 0,
                RaidDestroyedMask = 0,
                RaidEscapedMask = 0,
                RaidCargoReleasedMask = 0,
                RaidCargoLostQuantity = 0,
                RaidCargoReleasedQuantity = 0,
                RaidCargoRecoveredQuantity = 0,
                RaidCommodityId = commodity.Id,
                CommodityId = commodity.Id,
                RequiredQuantity = requiredQuantity,
                RaidRequiredQuantity = requiredQuantity,
                RaidTotalAllocatedQuantity = totalAllocated,
                RaidRemainingPossibleQuantity = totalAllocated,
                RaidStage = ConvoyRaidStage.EnRoute,
                RaidRouteStarted = true,
                RaidInterceptionActivated = false,
                RaidInterceptionPosition = interceptionPosition,
                RaidDestinationPosition = destination.Position
            };
            mission.RaidCargoAllocation.AddRange(cargoAllocation.Select(quantity => Math.Max(0, quantity)));
            mission.SetOrigin(origin);
            return mission;
        }

        public static Mission CreateEconomicInterdiction(
            EconomicShipment shipment,
            Station origin,
            Station destination,
            Commodity commodity,
            int requiredQuantity,
            int reward,
            MissionDifficulty difficulty,
            int routeRisk,
            string shortageLabel,
            long offerExpiresMilliseconds,
            string offeredBy = "Liberty Rogue Contact")
        {
            if (shipment == null || origin == null || destination == null || commodity == null ||
                requiredQuantity <= 0 || reward <= 0 || string.IsNullOrWhiteSpace(shipment.TraderIdentity) ||
                string.IsNullOrWhiteSpace(shipment.RouteId) || commodity.IsContraband || commodity.IsMissionCargo ||
                commodity.VolumePerUnit <= 0 || shipment.RemainingQuantity <= 0 ||
                string.IsNullOrWhiteSpace(shipment.DestinationStationId))
            {
                return null;
            }

            int systemIndex = destination.Config?.SystemIndex ?? origin.Config?.SystemIndex ?? 0;
            Mission mission = new Mission(
                MissionType.ShipmentInterdiction,
                difficulty,
                shipment.Trader?.Name ?? "Live economic trader",
                destination.Name,
                reward,
                0f,
                $"Intercept the live {shipment.Trader?.Name ?? "commercial trader"} en route to {destination.Name}. " +
                $"Recover {requiredQuantity} {commodity.Name} from the real shipment. " +
                $"Estimated shipment value: {shipment.RemainingManifestValue:N0} CR. " +
                $"Route risk: {TradeRouteRiskManager.GetRiskLabel(routeRisk)} ({routeRisk}/100)." +
                (shipment.ActiveSecurityEscortCount > 0
                    ? $" Security: {shipment.ActiveSecurityEscortCount} escort(s)."
                    : string.Empty) +
                (string.IsNullOrWhiteSpace(shortageLabel) ? string.Empty : $" Destination demand: {shortageLabel}."),
                FactionManager.LibertyRogues,
                title: "Shipment Interdiction")
            {
                DefinitionId = MissionCatalog.ShipmentInterdictionId,
                OfferedBy = offeredBy ?? "Liberty Rogue Contact",
                TargetLocation = shipment.Trader?.Name ?? "Live economic trader",
                TargetSystemIndex = Math.Max(0, systemIndex),
                TargetCount = requiredQuantity,
                RequiredProgress = requiredQuantity,
                CommodityId = commodity.Id,
                RequiredQuantity = requiredQuantity,
                DestinationStationId = shipment.DestinationStationId,
                EconomicShipmentTraderIdentity = shipment.TraderIdentity,
                EconomicShipmentRouteId = shipment.RouteId,
                EconomicShipmentValue = Math.Max(0, shipment.RemainingManifestValue),
                EconomicRouteRisk = Math.Clamp(routeRisk, 0, TradeRouteRiskManager.MaximumRiskScore),
                EconomicShortageLabel = shortageLabel ?? string.Empty,
                EconomicOfferExpiresMilliseconds = Math.Max(0L, offerExpiresMilliseconds),
                IsEconomicInterdiction = true,
                InterdictionSourceAvailableQuantity = Math.Max(0, GetManifestQuantity(shipment, commodity.Id)),
                InterdictionRemainingPossibleQuantity = Math.Max(0, GetManifestQuantity(shipment, commodity.Id)),
                InterdictionStage = ShipmentInterdictionStage.Intercept
            };
            mission.SetOrigin(origin);
            return mission;
        }

        private static int GetManifestQuantity(EconomicShipment shipment, string commodityId) =>
            shipment?.Manifest?.Stacks
                .Where(stack => string.Equals(stack?.Commodity?.Id, commodityId, StringComparison.OrdinalIgnoreCase))
                .Sum(stack => Math.Max(0, stack.Quantity)) ?? 0;

        private Mission(
            int id,
            string definitionId,
            string title,
            MissionType type,
            MissionDifficulty difficulty,
            MissionStatus status,
            string target,
            string destination,
            int reward,
            float timeLimit,
            string description,
            string offeredBy,
            string factionId,
            string targetLocation,
            int targetSystemIndex,
            int targetCount,
            int currentProgress,
            int requiredProgress,
            bool objectiveComplete,
            Vector3? targetPosition,
            string originStationId,
            string originStationName,
            int originSystemIndex,
            DateTime acceptedAtUtc,
            bool rewardPaid,
            string sourceStationName = "",
            string destinationStationId = "",
            string packageId = "",
            int packageQuantity = 0,
            int packageVolume = 0,
            bool missionCargoLoaded = false,
            int deliveredQuantity = 0,
            string commodityId = "",
            int requiredQuantity = 0,
            int issuedCargoQuantity = 0,
            float reputationReward = 0f,
            bool reputationRewardApplied = false,
            float? minimumEmployerReputation = null,
            float? maximumEmployerReputation = null)
        {
            Id = id > 0 ? id : _nextId++;
            if (_nextId <= Id) _nextId = Id + 1;
            DefinitionId = definitionId ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(title) ? description ?? string.Empty : title;
            Type = type;
            Difficulty = difficulty;
            Status = status;
            Target = target ?? string.Empty;
            Destination = destination ?? string.Empty;
            Reward = Math.Max(0, reward);
            TimeLimit = Math.Max(0f, timeLimit);
            ElapsedTime = 0f;
            Description = description ?? string.Empty;
            OfferedBy = offeredBy ?? string.Empty;
            FactionId = FactionManager.NormalizeFactionId(factionId);
            BountyTargetFactionId = string.Empty;
            TargetLocation = targetLocation ?? string.Empty;
            TargetSystemIndex = Math.Max(0, targetSystemIndex);
            TargetCount = Math.Max(1, targetCount);
            RequiredProgress = Math.Max(1, requiredProgress);
            CurrentProgress = Math.Clamp(currentProgress, 0, RequiredProgress);
            ObjectiveComplete = objectiveComplete || CurrentProgress >= RequiredProgress;
            TargetPosition = targetPosition;
            OriginStationId = originStationId ?? string.Empty;
            OriginStationName = originStationName ?? string.Empty;
            OriginSystemIndex = Math.Max(0, originSystemIndex);
            SourceStationName = sourceStationName ?? string.Empty;
            DestinationStationId = destinationStationId ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            PackageQuantity = Math.Max(0, packageQuantity);
            PackageVolume = Math.Max(0, packageVolume);
            MissionCargoLoaded = missionCargoLoaded;
            DeliveredQuantity = Math.Max(0, deliveredQuantity);
            CommodityId = commodityId ?? string.Empty;
            RequiredQuantity = Math.Max(0, requiredQuantity);
            IssuedCargoQuantity = Math.Max(0, issuedCargoQuantity);
            AcceptedAtUtc = acceptedAtUtc;
            RewardPaid = rewardPaid;
            ReputationReward = reputationReward;
            ReputationRewardApplied = reputationRewardApplied;
            MinimumEmployerReputation = NormalizeRequirement(minimumEmployerReputation);
            MaximumEmployerReputation = NormalizeRequirement(maximumEmployerReputation);
        }

        public static Mission FromDefinition(MissionDefinition definition, string offeredBy = "Mission Board", string factionId = null)
        {
            if (definition == null) return null;
            return new Mission(
                0,
                definition.Id,
                definition.Title,
                definition.Type,
                definition.Difficulty,
                MissionStatus.Available,
                definition.TargetLocation,
                definition.TargetLocation,
                definition.RewardCredits,
                0f,
                definition.Description,
                offeredBy,
                factionId ?? definition.TargetFactionId,
                definition.TargetLocation,
                definition.TargetSystemIndex,
                definition.TargetCount,
                0,
                definition.TargetCount,
                false,
                null,
                string.Empty,
                string.Empty,
                0,
                DateTime.MinValue,
                false,
                definition.SourceStationName,
                string.Empty,
                definition.PackageId,
                definition.PackageQuantity,
                definition.PackageVolume,
                false,
                0,
                string.Empty,
                0,
                0,
                0f,
                false,
                definition.MinimumEmployerReputation,
                definition.MaximumEmployerReputation);
        }

        public static Mission CreateRestored(
            int id,
            MissionType type,
            MissionDifficulty difficulty,
            MissionStatus status,
            string target,
            string destination,
            int reward,
            float timeLimit,
            string description,
            string offeredBy,
            string factionId,
            float elapsedTime,
            bool objectiveComplete)
        {
            Mission mission = new Mission(
                id,
                string.Empty,
                description,
                type,
                difficulty,
                status,
                target,
                destination,
                reward,
                timeLimit,
                description,
                offeredBy,
                factionId,
                destination,
                0,
                1,
                objectiveComplete ? 1 : 0,
                1,
                objectiveComplete,
                null,
                string.Empty,
                string.Empty,
                0,
                DateTime.MinValue,
                false);
            mission.ElapsedTime = Math.Max(0f, elapsedTime);
            return mission;
        }

        public static Mission CreateRestored(
            int id,
            string definitionId,
            string title,
            MissionType type,
            MissionDifficulty difficulty,
            MissionStatus status,
            string target,
            string destination,
            int reward,
            float timeLimit,
            string description,
            string offeredBy,
            string factionId,
            float elapsedTime,
            bool objectiveComplete,
            string targetLocation,
            int targetSystemIndex,
            int targetCount,
            int currentProgress,
            int requiredProgress,
            int objectiveRadius,
            string originStationId,
            string originStationName,
            int originSystemIndex,
            DateTime acceptedAtUtc,
            bool rewardPaid,
            SaveVector3Data targetPosition,
            string sourceStationName = "",
            string destinationStationId = "",
            string packageId = "",
            int packageQuantity = 0,
            int packageVolume = 0,
            bool missionCargoLoaded = false,
            int deliveredQuantity = 0,
            string commodityId = "",
            int requiredQuantity = 0,
            int issuedCargoQuantity = 0,
            float reputationReward = 0f,
            bool reputationRewardApplied = false,
            float? minimumEmployerReputation = null,
            float? maximumEmployerReputation = null,
            string targetLaneId = "",
            string targetSegmentId = "",
            int targetRingIndex = -1,
            float holdDurationSeconds = 0f,
            float holdProgressSeconds = 0f,
            bool playerDisruptionObserved = false,
            double lastQualifiedDisruptionAtSeconds = -1d,
            float policeReputationPenalty = 0f,
            bool policeConsequenceApplied = false,
            bool securityResponseTriggered = false,
            string failureReason = "",
            TradeLaneDefenseStage defenseStage = TradeLaneDefenseStage.EnRoute,
            int defenseAttackForceSize = 0,
            int defenseAttackersRemaining = 0,
            float defenseActivationRadius = 0f,
            float defenseFailureHoldSeconds = 0f,
            float defenseFailureHoldProgressSeconds = 0f,
            bool defenseActivationStarted = false,
            string convoyRouteId = "",
            string convoyRouteLaneId = "",
            string convoyRouteSegmentId = "",
            TradeLaneDirection convoyRouteDirection = TradeLaneDirection.Forward,
            string convoyFactionId = "",
            string convoyHostileFactionId = "",
            string convoyShipArchetype = "",
            int convoyRouteRingIndex = -1,
            int convoyEncounterRingIndex = -1,
            int convoyShipCount = 0,
            int convoyRequiredSurvivors = 1,
            int convoySurvivors = 0,
            int convoyDestroyedCount = 0,
            int convoyDestroyedMask = 0,
            int convoyArrivedCount = 0,
            int convoyArrivedMask = 0,
            int convoyAttackForceSize = 0,
            int convoyAttackersRemaining = 0,
            ConvoyEscortStage convoyStage = ConvoyEscortStage.Rendezvous,
            float convoyRendezvousRadius = 2200f,
            float convoyAbandonmentRadius = 9000f,
            float convoyAbandonmentGraceSeconds = 25f,
            float convoyAbandonmentProgressSeconds = 0f,
            float convoyArrivalRadius = 3000f,
            bool convoyRouteStarted = false,
            bool convoyEncounterActivated = false,
            bool convoyEncounterResolved = false,
            bool convoyEncounterSpawnAttempted = false,
            SaveVector3Data convoyRendezvousPosition = null,
            SaveVector3Data convoyEncounterPosition = null,
            SaveVector3Data convoyDestinationPosition = null,
            string raidRouteId = "",
            string raidRouteLaneId = "",
            string raidRouteSegmentId = "",
            TradeLaneDirection raidRouteDirection = TradeLaneDirection.Forward,
            string raidConvoyFactionId = "",
            string raidShipArchetype = "",
            int raidRouteRingIndex = -1,
            int raidInterceptionRingIndex = -1,
            int raidShipCount = 0,
            int raidDestroyedCount = 0,
            int raidEscapedCount = 0,
            int raidDestroyedMask = 0,
            int raidEscapedMask = 0,
            int raidCargoReleasedMask = 0,
            int raidCargoLostQuantity = 0,
            int raidCargoReleasedQuantity = 0,
            int raidCargoRecoveredQuantity = 0,
            string raidCommodityId = "",
            int raidRequiredQuantity = 0,
            int raidTotalAllocatedQuantity = 0,
            int raidRemainingPossibleQuantity = 0,
            ConvoyRaidStage raidStage = ConvoyRaidStage.EnRoute,
            bool raidRouteStarted = false,
            bool raidInterceptionActivated = false,
            SaveVector3Data raidInterceptionPosition = null,
            SaveVector3Data raidDestinationPosition = null,
            IReadOnlyList<int> raidCargoAllocation = null,
            ContrabandSmugglingStage smugglingStage = ContrabandSmugglingStage.EnRoute,
            bool smugglingPoliceDetected = false,
            int smugglingJettisonedQuantity = 0,
            bool economicEscort = false,
            string economicShipmentTraderIdentity = "",
            string economicShipmentRouteId = "",
            int economicShipmentValue = 0,
            int economicRouteRisk = 0,
            string economicShortageLabel = "",
            long economicOfferExpiresMilliseconds = 0L)
        {
            Mission mission = new Mission(
                id,
                definitionId,
                title,
                type,
                difficulty,
                status,
                target,
                destination,
                reward,
                timeLimit,
                description,
                offeredBy,
                factionId,
                targetLocation,
                targetSystemIndex,
                targetCount,
                currentProgress,
                requiredProgress,
                objectiveComplete,
                targetPosition?.ToVector3(),
                originStationId,
                originStationName,
                originSystemIndex,
                acceptedAtUtc,
                rewardPaid,
                sourceStationName,
                destinationStationId,
                packageId,
                packageQuantity,
                packageVolume,
                missionCargoLoaded,
                deliveredQuantity,
                commodityId,
                requiredQuantity,
                issuedCargoQuantity,
                reputationReward,
                reputationRewardApplied,
                minimumEmployerReputation,
                maximumEmployerReputation);
            mission.ElapsedTime = Math.Max(0f, elapsedTime);
            mission.ObjectiveRadius = Math.Clamp(objectiveRadius <= 0 ? 500 : objectiveRadius, 1, 10000);
            mission.TargetLaneId = targetLaneId ?? string.Empty;
            mission.TargetSegmentId = targetSegmentId ?? string.Empty;
            mission.TargetRingIndex = targetRingIndex;
            mission.HoldDurationSeconds = float.IsNaN(holdDurationSeconds) || float.IsInfinity(holdDurationSeconds)
                ? 0f
                : Math.Clamp(holdDurationSeconds, 0f, 30f);
            mission.HoldProgressSeconds = float.IsNaN(holdProgressSeconds) || float.IsInfinity(holdProgressSeconds)
                ? 0f
                : Math.Clamp(holdProgressSeconds, 0f, mission.HoldDurationSeconds);
            mission.PlayerDisruptionObserved = playerDisruptionObserved;
            mission.LastQualifiedDisruptionAtSeconds = double.IsNaN(lastQualifiedDisruptionAtSeconds) || double.IsInfinity(lastQualifiedDisruptionAtSeconds)
                ? -1d
                : lastQualifiedDisruptionAtSeconds;
            mission.PoliceReputationPenalty = Math.Clamp(policeReputationPenalty, -0.05f, 0f);
            mission.PoliceConsequenceApplied = policeConsequenceApplied;
            mission.SecurityResponseTriggered = securityResponseTriggered;
            mission.FailureReason = failureReason ?? string.Empty;
            mission.DefenseStage = Enum.IsDefined(typeof(TradeLaneDefenseStage), defenseStage)
                ? defenseStage
                : TradeLaneDefenseStage.EnRoute;
            mission.DefenseAttackForceSize = Math.Clamp(defenseAttackForceSize, 0, 4);
            mission.DefenseAttackersRemaining = Math.Clamp(defenseAttackersRemaining, 0, mission.DefenseAttackForceSize);
            mission.DefenseActivationRadius = float.IsNaN(defenseActivationRadius) || float.IsInfinity(defenseActivationRadius)
                ? 0f
                : Math.Clamp(defenseActivationRadius, 0f, 5000f);
            mission.DefenseFailureHoldSeconds = float.IsNaN(defenseFailureHoldSeconds) || float.IsInfinity(defenseFailureHoldSeconds)
                ? 0f
                : Math.Clamp(defenseFailureHoldSeconds, 0f, 30f);
            mission.DefenseFailureHoldProgressSeconds = float.IsNaN(defenseFailureHoldProgressSeconds) || float.IsInfinity(defenseFailureHoldProgressSeconds)
                ? 0f
                : Math.Clamp(defenseFailureHoldProgressSeconds, 0f, mission.DefenseFailureHoldSeconds);
            mission.DefenseActivationStarted = defenseActivationStarted;
            mission.ConvoyRouteId = convoyRouteId ?? string.Empty;
            mission.ConvoyRouteLaneId = convoyRouteLaneId ?? string.Empty;
            mission.ConvoyRouteSegmentId = convoyRouteSegmentId ?? string.Empty;
            mission.ConvoyRouteDirection = Enum.IsDefined(typeof(TradeLaneDirection), convoyRouteDirection)
                ? convoyRouteDirection
                : TradeLaneDirection.Forward;
            mission.ConvoyFactionId = FactionManager.NormalizeFactionId(
                string.IsNullOrWhiteSpace(convoyFactionId) ? FactionManager.LibertyCorporations : convoyFactionId);
            mission.ConvoyHostileFactionId = FactionManager.NormalizeFactionId(
                string.IsNullOrWhiteSpace(convoyHostileFactionId) ? FactionManager.LibertyRogues : convoyHostileFactionId);
            mission.ConvoyShipArchetype = string.IsNullOrWhiteSpace(convoyShipArchetype)
                ? "Transport Ship Alpha"
                : convoyShipArchetype.Trim();
            mission.ConvoyRouteRingIndex = convoyRouteRingIndex;
            mission.ConvoyEncounterRingIndex = convoyEncounterRingIndex;
            mission.ConvoyShipCount = Math.Clamp(convoyShipCount, 0, 4);
            mission.ConvoyRequiredSurvivors = Math.Clamp(convoyRequiredSurvivors, 1, Math.Max(1, mission.ConvoyShipCount));
            mission.ConvoySurvivors = Math.Clamp(convoySurvivors, 0, mission.ConvoyShipCount);
            mission.ConvoyDestroyedCount = Math.Clamp(convoyDestroyedCount, 0, mission.ConvoyShipCount);
            int convoyMaskLimit = mission.ConvoyShipCount >= 4 ? 15 : (1 << mission.ConvoyShipCount) - 1;
            mission.ConvoyDestroyedMask = convoyDestroyedMask & convoyMaskLimit;
            mission.ConvoyArrivedCount = Math.Clamp(convoyArrivedCount, 0, mission.ConvoyShipCount);
            mission.ConvoyArrivedMask = convoyArrivedMask & convoyMaskLimit;
            mission.ConvoyAttackForceSize = Math.Clamp(convoyAttackForceSize, 0, 6);
            mission.ConvoyAttackersRemaining = Math.Clamp(convoyAttackersRemaining, 0, mission.ConvoyAttackForceSize);
            mission.ConvoyStage = Enum.IsDefined(typeof(ConvoyEscortStage), convoyStage)
                ? convoyStage
                : ConvoyEscortStage.Rendezvous;
            mission.ConvoyRendezvousRadius = SanitizeConvoyRadius(convoyRendezvousRadius, 2200f, 250f, 5000f);
            mission.ConvoyAbandonmentRadius = SanitizeConvoyRadius(convoyAbandonmentRadius, 9000f, 2500f, 20000f);
            mission.ConvoyAbandonmentGraceSeconds = SanitizeConvoyRadius(convoyAbandonmentGraceSeconds, 25f, 5f, 60f);
            mission.ConvoyAbandonmentProgressSeconds = SanitizeConvoyRadius(
                convoyAbandonmentProgressSeconds, 0f, 0f, mission.ConvoyAbandonmentGraceSeconds);
            mission.ConvoyArrivalRadius = SanitizeConvoyRadius(convoyArrivalRadius, 3000f, 500f, 10000f);
            mission.ConvoyRouteStarted = convoyRouteStarted;
            mission.ConvoyEncounterActivated = convoyEncounterActivated;
            mission.ConvoyEncounterResolved = convoyEncounterResolved;
            mission.ConvoyEncounterSpawnAttempted = convoyEncounterSpawnAttempted;
            mission.ConvoyRendezvousPosition = convoyRendezvousPosition?.ToVector3();
            mission.ConvoyEncounterPosition = convoyEncounterPosition?.ToVector3();
            mission.ConvoyDestinationPosition = convoyDestinationPosition?.ToVector3();
            mission.IsEconomicEscort = economicEscort;
            mission.EconomicShipmentTraderIdentity = economicShipmentTraderIdentity ?? string.Empty;
            mission.EconomicShipmentRouteId = economicShipmentRouteId ?? string.Empty;
            mission.EconomicShipmentValue = Math.Max(0, economicShipmentValue);
            mission.EconomicRouteRisk = Math.Clamp(economicRouteRisk, 0, TradeRouteRiskManager.MaximumRiskScore);
            mission.EconomicShortageLabel = economicShortageLabel ?? string.Empty;
            mission.EconomicOfferExpiresMilliseconds = Math.Max(0L, economicOfferExpiresMilliseconds);
            mission.RaidRouteId = raidRouteId ?? string.Empty;
            mission.RaidRouteLaneId = raidRouteLaneId ?? string.Empty;
            mission.RaidRouteSegmentId = raidRouteSegmentId ?? string.Empty;
            mission.RaidRouteDirection = Enum.IsDefined(typeof(TradeLaneDirection), raidRouteDirection)
                ? raidRouteDirection
                : TradeLaneDirection.Forward;
            mission.RaidConvoyFactionId = FactionManager.NormalizeFactionId(
                string.IsNullOrWhiteSpace(raidConvoyFactionId) ? FactionManager.LibertyCorporations : raidConvoyFactionId);
            mission.RaidShipArchetype = string.IsNullOrWhiteSpace(raidShipArchetype)
                ? "Transport Ship Alpha"
                : raidShipArchetype.Trim();
            mission.RaidRouteRingIndex = raidRouteRingIndex;
            mission.RaidInterceptionRingIndex = raidInterceptionRingIndex;
            mission.RaidShipCount = Math.Clamp(raidShipCount, 0, 4);
            int raidMaskLimit = mission.RaidShipCount >= 4
                ? 15
                : mission.RaidShipCount > 0 ? (1 << mission.RaidShipCount) - 1 : 0;
            mission.RaidDestroyedCount = Math.Clamp(raidDestroyedCount, 0, mission.RaidShipCount);
            mission.RaidEscapedCount = Math.Clamp(raidEscapedCount, 0, mission.RaidShipCount);
            mission.RaidDestroyedMask = raidDestroyedMask & raidMaskLimit;
            mission.RaidEscapedMask = raidEscapedMask & raidMaskLimit;
            mission.RaidCargoReleasedMask = raidCargoReleasedMask & raidMaskLimit;
            mission.RaidCargoLostQuantity = Math.Max(0, raidCargoLostQuantity);
            mission.RaidCargoReleasedQuantity = Math.Max(0, raidCargoReleasedQuantity);
            mission.RaidCargoRecoveredQuantity = Math.Max(0, raidCargoRecoveredQuantity);
            mission.RaidCommodityId = string.IsNullOrWhiteSpace(raidCommodityId)
                ? commodityId ?? string.Empty
                : raidCommodityId.Trim();
            mission.RaidRequiredQuantity = Math.Max(0, raidRequiredQuantity > 0 ? raidRequiredQuantity : requiredQuantity);
            mission.RaidTotalAllocatedQuantity = Math.Max(0, raidTotalAllocatedQuantity);
            mission.RaidRemainingPossibleQuantity = Math.Max(0, raidRemainingPossibleQuantity);
            mission.RaidStage = Enum.IsDefined(typeof(ConvoyRaidStage), raidStage)
                ? raidStage
                : ConvoyRaidStage.EnRoute;
            mission.RaidRouteStarted = raidRouteStarted;
            mission.RaidInterceptionActivated = raidInterceptionActivated;
            mission.RaidInterceptionPosition = raidInterceptionPosition?.ToVector3();
            mission.RaidDestinationPosition = raidDestinationPosition?.ToVector3();
            if (raidCargoAllocation != null)
            {
                foreach (int quantity in raidCargoAllocation.Take(4))
                    mission.RaidCargoAllocation.Add(Math.Max(0, quantity));
            }
            mission.SmugglingStage = Enum.IsDefined(typeof(ContrabandSmugglingStage), smugglingStage)
                ? smugglingStage
                : ContrabandSmugglingStage.EnRoute;
            mission.SmugglingPoliceDetected = smugglingPoliceDetected;
            mission.SmugglingJettisonedQuantity = Math.Max(0, smugglingJettisonedQuantity);
            return mission;
        }

        private static float SanitizeConvoyRadius(float value, float fallback, float minimum, float maximum)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : Math.Clamp(value, minimum, maximum);
        }

        public void SetOrigin(Station station)
        {
            OriginStationName = station?.Name ?? "Station Test Bay";
            OriginStationId = station == null ? "station-test-bay" : BuildStationIdentity(station);
            OriginSystemIndex = station?.Config?.SystemIndex ?? 0;
            if (TargetSystemIndex <= 0) TargetSystemIndex = OriginSystemIndex;
        }

        public static string BuildStationIdentity(Station station)
        {
            if (station == null) return "station-test-bay";
            return $"{station.Config?.SystemIndex ?? 0}:{station.Name}";
        }

        public string GetSummary() =>
            $"[{GetTypeLabel()}] {GetObjectiveText()} | Reward: {Reward:N0} CR | Client: {GetClientLabel()}";

    public string GetDetailedDescription() =>
            $"Type: {GetTypeLabel()}\nObjective: {GetObjectiveText()}\nReward: {Reward:N0} CR\nClient: {GetClientLabel()}\nStatus: {GetStatusLabel()}";

        public bool HasReputationRequirement => MinimumEmployerReputation.HasValue || MaximumEmployerReputation.HasValue;

        private static float? NormalizeRequirement(float? value)
        {
            if (!value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value))
                return null;

            return Math.Clamp(MathF.Round(value.Value, 4, MidpointRounding.AwayFromZero),
                ReputationManager.MinimumStanding,
                ReputationManager.MaximumStanding);
        }

        public static bool IsDeliveryType(MissionType type) =>
            type is MissionType.Delivery or MissionType.CourierDelivery or MissionType.FreightContract or MissionType.ExportContract or MissionType.ContrabandSmuggling;

        public string GetTypeLabel() => Type switch
        {
            MissionType.ReachLocation => "REACH LOCATION",
            MissionType.DestroyHostiles => "DESTROY HOSTILES",
            MissionType.Delivery => "DELIVERY",
            MissionType.CourierDelivery => "COURIER",
            MissionType.FreightContract => "EMERGENCY SUPPLY",
            MissionType.ExportContract => "BULK EXPORT",
            MissionType.Bounty => "BOUNTY",
            MissionType.Escort => "ESCORT",
            MissionType.TradeLaneDisruption => "TRADE-LANE DISRUPTION",
            MissionType.TradeLaneDefense => "TRADE-LANE DEFENSE",
            MissionType.ConvoyEscort => "CONVOY ESCORT",
            MissionType.ConvoyRaid => "CARGO INTERDICTION",
            MissionType.ShipmentInterdiction => "SHIPMENT INTERDICTION",
            MissionType.ContrabandSmuggling => "CONTRABAND SMUGGLING",
            _ => "MISSION"
        };

        public string GetRiskLabel() => Difficulty switch
        {
            MissionDifficulty.Easy => "LOW RISK",
            MissionDifficulty.Medium => "MODERATE RISK",
            MissionDifficulty.Hard => "HIGH RISK",
            MissionDifficulty.Deadly => "EXTREME RISK",
            _ => "UNKNOWN"
        };

        public string GetClientLabel() => !string.IsNullOrWhiteSpace(OfferedBy)
            ? OfferedBy.Trim()
            : FactionManager.GetFactionDisplayName(FactionId);

        public string GetEscortStatusLabel() => Status switch
        {
            MissionStatus.Available => "Available",
            MissionStatus.Accepted or MissionStatus.InProgress => "In Progress",
            MissionStatus.Completed => "Arrived",
            MissionStatus.Failed => "Failed",
            MissionStatus.Rewarded => "Rewarded",
            _ => "Unknown"
        };

        public string GetStatusLabel() => Status switch
        {
            MissionStatus.Available => "Available",
            MissionStatus.Accepted => "Accepted",
            MissionStatus.InProgress => "Active",
            MissionStatus.Completed => RewardPaid ? "Rewarded" : "Completed - reward pending",
            MissionStatus.Failed => "Failed",
            MissionStatus.Rewarded => "Rewarded",
            _ => "Unknown"
        };

        public string GetCargoLabel()
        {
            bool raid = Type == MissionType.ConvoyRaid;
            bool interdiction = Type == MissionType.ShipmentInterdiction;
            string cargoId = raid ? RaidCommodityId : interdiction ? CommodityId : PackageId;
            int quantity = raid ? RaidRequiredQuantity : interdiction ? RequiredQuantity : PackageQuantity;
            Commodity commodity = CommodityCatalog.GetByIdOrName(cargoId);
            string label = commodity?.Name ?? (string.IsNullOrWhiteSpace(cargoId) ? "Mission package" : cargoId);
            return quantity > 0 ? $"{label} x{quantity}" : label;
        }

        public string GetEscortShipName() =>
            $"{(string.IsNullOrWhiteSpace(Target) ? "Escort Convoy" : Target.Trim())} {Id}";

        public string GetTargetLabel()
        {
            if (Type == MissionType.DestroyHostiles)
                return string.IsNullOrWhiteSpace(TargetLocation) ? "Mission rogue flight" : TargetLocation;
            if (Type == MissionType.CourierDelivery)
                return GetCargoLabel();
            if (Type == MissionType.FreightContract)
            {
                Commodity commodity = CommodityCatalog.GetByIdOrName(CommodityId);
                return commodity == null
                    ? $"{RequiredQuantity:N0} units"
                    : $"{commodity.Name} x{RequiredQuantity:N0}";
            }
            if (Type == MissionType.ExportContract)
            {
                Commodity commodity = CommodityCatalog.GetByIdOrName(CommodityId);
                return commodity == null
                    ? $"{RequiredQuantity:N0} units"
                    : $"{commodity.Name} x{RequiredQuantity:N0}";
            }
            if (Type == MissionType.ContrabandSmuggling)
            {
                Commodity commodity = CommodityCatalog.GetByIdOrName(CommodityId);
                return commodity == null
                    ? $"{RequiredQuantity:N0} units"
                    : $"{commodity.Name} x{RequiredQuantity:N0}";
            }
            if (Type == MissionType.TradeLaneDisruption)
                return string.IsNullOrWhiteSpace(TargetSegmentId) ? TargetLocation : $"{TargetLocation} / {TargetSegmentId}";
            if (Type == MissionType.TradeLaneDefense)
                return string.IsNullOrWhiteSpace(TargetSegmentId) ? TargetLocation : $"{TargetLocation} / {TargetSegmentId}";
            if (Type == MissionType.ConvoyEscort)
                return $"Merchant convoy ({Math.Max(0, ConvoySurvivors)} / {Math.Max(0, ConvoyShipCount)})";
            if (Type == MissionType.ConvoyRaid)
            {
                Commodity raidCommodity = CommodityCatalog.GetByIdOrName(RaidCommodityId);
                return raidCommodity == null
                    ? $"Recover {RaidRequiredQuantity:N0} cargo units"
                    : $"{raidCommodity.Name} x{RaidRequiredQuantity:N0}";
            }
            if (Type == MissionType.ShipmentInterdiction)
            {
                Commodity commodity = CommodityCatalog.GetByIdOrName(CommodityId);
                return commodity == null
                    ? $"Recover {RequiredQuantity:N0} cargo units"
                    : $"{commodity.Name} x{RequiredQuantity:N0}";
            }
            if (!string.IsNullOrWhiteSpace(Target))
            {
                if (Type == MissionType.Escort && TargetSpaceObject is NpcShip escortShip && !escortShip.IsDestroyed)
                    return string.IsNullOrWhiteSpace(escortShip.Name) ? GetEscortShipName() : escortShip.Name.Trim();
                return Target.Trim();
            }

            return Type switch
            {
                MissionType.Bounty => "Target signal unresolved",
                MissionType.Escort => "Escort signal unresolved",
                MissionType.ReachLocation => TargetLocation,
                _ => "Cargo unavailable"
            };
        }

        public string GetDestinationLabel() => !string.IsNullOrWhiteSpace(Destination)
            ? Destination.Trim()
            : Type is MissionType.Escort or MissionType.ConvoyEscort or MissionType.ConvoyRaid or MissionType.ShipmentInterdiction or MissionType.Delivery or MissionType.CourierDelivery or MissionType.FreightContract or MissionType.ExportContract or MissionType.ContrabandSmuggling ? "Destination unavailable" : "Location unavailable";

        public string GetTargetFactionLabel() => FactionManager.GetFactionDisplayName(
            string.IsNullOrWhiteSpace(BountyTargetFactionId) ? FactionId : BountyTargetFactionId);

        public string GetObjectiveText() => Type switch
        {
            MissionType.ReachLocation => $"Reach {GetDestinationLabel()}",
            MissionType.DestroyHostiles => $"Destroy hostiles: {CurrentProgress} / {RequiredProgress}",
            MissionType.Delivery => $"Deliver {GetTargetLabel()} to {GetDestinationLabel()}",
            MissionType.CourierDelivery => $"Deliver package to {GetDestinationLabel()}",
            MissionType.FreightContract => $"Deliver {GetTargetLabel()} to {GetDestinationLabel()}",
            MissionType.ExportContract => $"Haul {GetTargetLabel()} from {OriginStationName} to {GetDestinationLabel()}",
            MissionType.ContrabandSmuggling => $"Deliver {GetTargetLabel()} to {GetDestinationLabel()} without a police seizure",
            MissionType.Bounty => $"Destroy {GetTargetLabel()}",
            MissionType.Escort => $"Escort {GetTargetLabel()} to {GetDestinationLabel()}",
            MissionType.TradeLaneDisruption => $"Disrupt {GetTargetLabel()} and hold it offline",
            MissionType.TradeLaneDefense => $"Defend {GetTargetLabel()} against Liberty Rogues",
            MissionType.ConvoyEscort => $"Escort merchant convoy ({ConvoyShipCount} freighters) from {OriginStationName} to {GetDestinationLabel()}",
            MissionType.ConvoyRaid => $"Interdict convoy and recover {GetTargetLabel()} along the {TargetLocation} corridor",
            MissionType.ShipmentInterdiction => $"Intercept {GetTargetLabel()} and recover the cargo for {OriginStationName}",
            _ => Description
        };

        public string GetHudHeadline() => Title;

        public string GetHudFallbackLine() => Type switch
        {
            MissionType.ReachLocation => TargetPosition.HasValue ? $"Reach {GetDestinationLabel()}" : "Patrol marker unresolved",
            MissionType.DestroyHostiles => $"Hostiles destroyed: {CurrentProgress} / {RequiredProgress}",
            MissionType.Bounty => string.IsNullOrWhiteSpace(Target) ? "Target signal unresolved" : string.Empty,
            MissionType.Delivery => string.IsNullOrWhiteSpace(Destination) ? "Destination unavailable" : string.Empty,
            MissionType.CourierDelivery => string.IsNullOrWhiteSpace(Destination) ? "Destination unavailable" : "Deliver package to destination",
            MissionType.FreightContract => string.IsNullOrWhiteSpace(Destination) ? "Destination unavailable" : $"Deliver {GetTargetLabel()} to destination",
            MissionType.ExportContract => string.IsNullOrWhiteSpace(Destination) ? "Destination unavailable" : $"Haul {GetTargetLabel()} to destination",
            MissionType.ContrabandSmuggling => GetSmugglingHudStatus(),
            MissionType.Escort => string.IsNullOrWhiteSpace(Destination) ? "Destination unavailable" : string.Empty,
            MissionType.TradeLaneDisruption => GetTradeLaneHudStatus(),
            MissionType.TradeLaneDefense => GetTradeLaneDefenseHudStatus(),
            MissionType.ConvoyEscort => GetConvoyEscortHudStatus(),
            MissionType.ConvoyRaid => GetConvoyRaidHudStatus(),
            MissionType.ShipmentInterdiction => GetShipmentInterdictionHudStatus(),
            _ => string.Empty
        };

        public string GetHudProgressLine() => Type switch
        {
            MissionType.DestroyHostiles => $"Hostiles destroyed: {CurrentProgress} / {RequiredProgress}",
            MissionType.ReachLocation => $"Reach {GetDestinationLabel()}",
            MissionType.CourierDelivery => ObjectiveComplete ? "Cargo delivered" : $"Deliver package to {GetDestinationLabel()}",
            MissionType.FreightContract => ObjectiveComplete ? "Supply delivered" : $"Deliver {GetTargetLabel()} to {GetDestinationLabel()}",
            MissionType.ExportContract => ObjectiveComplete ? "Export delivered" : $"Haul {GetTargetLabel()} to {GetDestinationLabel()}",
            MissionType.ContrabandSmuggling => GetSmugglingHudStatus(),
            MissionType.TradeLaneDisruption => GetTradeLaneHudStatus(),
            MissionType.TradeLaneDefense => GetTradeLaneDefenseHudStatus(),
            MissionType.ConvoyEscort => GetConvoyEscortHudStatus(),
            MissionType.ConvoyRaid => GetConvoyRaidHudStatus(),
            MissionType.ShipmentInterdiction => GetShipmentInterdictionHudStatus(),
            _ => GetObjectiveText()
        };

        public string GetTradeLaneHudStatus()
        {
            if (Type != MissionType.TradeLaneDisruption)
                return string.Empty;
            if (ObjectiveComplete)
                return "COMPLETE";
            if (!PlayerDisruptionObserved || HoldProgressSeconds <= 0f)
                return "TARGET OPERATIONAL";
            return $"TARGET DISRUPTED | HOLD {HoldProgressSeconds:0.0} / {HoldDurationSeconds:0.0}s";
        }

        public string GetTradeLaneDefenseHudStatus()
        {
            if (Type != MissionType.TradeLaneDefense)
                return string.Empty;
            if (Status == MissionStatus.Failed || DefenseStage == TradeLaneDefenseStage.Failed)
                return "DEFENSE FAILED";
            if (ObjectiveComplete || DefenseStage == TradeLaneDefenseStage.Successful)
                return "DEFENSE SUCCESSFUL";
            if (DefenseStage == TradeLaneDefenseStage.EnRoute)
                return $"PROCEED TO {TargetLocation} / {TargetSegmentId}";
            if (DefenseStage == TradeLaneDefenseStage.AwaitingRecovery)
                return $"LANE DISRUPTED — RECOVER {DefenseFailureHoldProgressSeconds:0.0} / {DefenseFailureHoldSeconds:0.0}s";
            return $"DEFEND LANE | ROGUES REMAINING: {Math.Max(0, DefenseAttackersRemaining)}";
        }

        public string GetConvoyEscortHudStatus()
        {
            if (Type != MissionType.ConvoyEscort)
                return string.Empty;
            if (Status == MissionStatus.Failed || ConvoyStage == ConvoyEscortStage.Failed)
                return "ESCORT FAILED";
            if (ObjectiveComplete || ConvoyStage == ConvoyEscortStage.Successful)
                return "ESCORT SUCCESSFUL";
            if (ConvoyStage == ConvoyEscortStage.Rendezvous)
                return "PROCEED TO CONVOY RENDEZVOUS";
            if (ConvoyStage == ConvoyEscortStage.EncounterActive)
            {
                string threat = ConvoyAttackersRemaining > 0 ? "ROGUE INTERCEPTORS DETECTED" : "INTERCEPTION RESOLVED";
                return ConvoyAbandonmentProgressSeconds > 0f
                    ? $"RETURN TO CONVOY — ABANDONMENT IN {Math.Max(0f, ConvoyAbandonmentGraceSeconds - ConvoyAbandonmentProgressSeconds):0}s"
                    : $"{threat} | CONVOY SHIPS REMAINING: {Math.Max(0, ConvoySurvivors)} / {Math.Max(0, ConvoyShipCount)}";
            }
            if (ConvoyStage == ConvoyEscortStage.ApproachingDestination)
                return $"CONVOY APPROACHING DESTINATION | SHIPS REMAINING: {Math.Max(0, ConvoySurvivors)} / {Math.Max(0, ConvoyShipCount)}";
            if (ConvoyAbandonmentProgressSeconds > 0f)
                return $"RETURN TO CONVOY — ABANDONMENT IN {Math.Max(0f, ConvoyAbandonmentGraceSeconds - ConvoyAbandonmentProgressSeconds):0}s";
            return $"ESCORT CONVOY TO DESTINATION | SHIPS REMAINING: {Math.Max(0, ConvoySurvivors)} / {Math.Max(0, ConvoyShipCount)}";
        }

        public string GetConvoyRaidHudStatus()
        {
            if (Type != MissionType.ConvoyRaid)
                return string.Empty;
            if (Status == MissionStatus.Failed || RaidStage == ConvoyRaidStage.Failed)
                return "CARGO RAID FAILED";
            if (ObjectiveComplete || RaidStage == ConvoyRaidStage.Successful)
                return "CONTRACT CARGO SECURED — RETURN TO EMPLOYER";

            Commodity commodity = CommodityCatalog.GetByIdOrName(RaidCommodityId);
            string cargoName = commodity?.Name ?? RaidCommodityId;
            if (RaidStage == ConvoyRaidStage.EnRoute)
                return $"PROCEED TO INTERCEPTION POINT | TARGET: {cargoName}";
            if (RaidStage == ConvoyRaidStage.InterceptionActive)
                return $"INTERCEPT TRANSPORTS | {cargoName}: {Math.Max(0, RaidCargoRecoveredQuantity)} / {Math.Max(0, RaidRequiredQuantity)}";
            return $"INTERDICTED CARGO: {Math.Max(0, RaidCargoRecoveredQuantity)} / {Math.Max(0, RaidRequiredQuantity)} | REMAINING POSSIBLE: {Math.Max(0, RaidRemainingPossibleQuantity)}";
        }

        public string GetShipmentInterdictionHudStatus()
        {
            if (Type != MissionType.ShipmentInterdiction)
                return string.Empty;
            Commodity commodity = CommodityCatalog.GetByIdOrName(CommodityId);
            string cargoName = commodity?.Name ?? CommodityId;
            int recovered = Math.Max(0, CurrentProgress);
            int required = Math.Max(0, RequiredQuantity);
            if (Status == MissionStatus.Failed || InterdictionStage == ShipmentInterdictionStage.Failed)
                return "INTERDICTION FAILED";
            if (ObjectiveComplete || InterdictionStage == ShipmentInterdictionStage.Successful)
                return $"RETURN TO {OriginStationName} | {cargoName}: {recovered} / {required}";
            if (InterdictionTargetDelivered)
                return $"SHIPMENT DELIVERED — RECOVERED: {cargoName} {recovered} / {required}";
            if (InterdictionTargetDestroyed)
                return $"RECOVER CARGO | {cargoName}: {recovered} / {required} | POSSIBLE: {Math.Max(0, InterdictionRemainingPossibleQuantity)}";
            return $"INTERDICTION TARGET | {cargoName}: {recovered} / {required} | IN TRANSIT TO {GetDestinationLabel()}";
        }

        public string GetSmugglingHudStatus()
        {
            if (Type != MissionType.ContrabandSmuggling)
                return string.Empty;
            if (Status == MissionStatus.Failed || SmugglingStage == ContrabandSmugglingStage.Failed)
                return "SMUGGLING CONTRACT FAILED";
            if (RewardPaid || SmugglingStage == ContrabandSmugglingStage.Successful)
                return "CONTRABAND DELIVERED — REWARD PAID";

            Commodity commodity = CommodityCatalog.GetByIdOrName(CommodityId);
            string cargoName = commodity?.Name ?? CommodityId;
            int remaining = Math.Max(0, RequiredQuantity - DeliveredQuantity);
            string warning = SmugglingPoliceDetected ? "POLICE ALERT ACTIVE" : "AVOID POLICE SCANS";
            return $"{warning} | {cargoName}: {remaining:N0} TO DELIVER | DESTINATION: {GetDestinationLabel()}";
        }
    }
}
