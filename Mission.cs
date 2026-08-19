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
        FreightContract,
        ExportContract,
        Bounty,
        Escort
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
            int packageVolume = 0)
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
    }

    /// <summary>Fixed Phase 11 board catalog; UI code does not own metadata.</summary>
    public static class MissionCatalog
    {
        public const string PatrolSweepId = "patrol-sweep";
        public const string RogueHuntId = "rogue-hunt";
        public const string PriorityDispatchId = "priority-dispatch";

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
                packageVolume: 1)
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
        public string DefinitionId { get; }
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
        public DateTime AcceptedAtUtc { get; set; }
        public bool RewardPaid { get; set; }

        public bool ObjectiveComplete { get; set; }
        public Vector3? TargetPosition { get; set; }
        public SpaceObject TargetSpaceObject { get; set; }

        public bool IsExpired => TimeLimit > 0 && ElapsedTime >= TimeLimit;
        public float TimeRemaining => TimeLimit > 0 ? Math.Max(0, TimeLimit - ElapsedTime) : -1;
        public bool IsActive => Status is MissionStatus.Accepted or MissionStatus.InProgress;
        public bool HasUnclaimedReward => Status == MissionStatus.Completed && !RewardPaid;

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
            if (commodity == null || destination == null || requiredQuantity <= 0 || reward <= 0)
            {
                return null;
            }

            Mission mission = new Mission(
                MissionType.FreightContract,
                requiredQuantity >= 20 ? MissionDifficulty.Medium : MissionDifficulty.Easy,
                commodity.Name,
                destination.Name,
                reward,
                0f,
                $"{destination.Name} is experiencing a {commodity.Name} shortage. Deliver {requiredQuantity} {commodity.Name}.",
                factionId ?? destination.FactionId,
                title: $"{commodity.Name} Supply Contract")
            {
                OfferedBy = offeredBy ?? "Mission Board",
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
            int issuedCargoQuantity = 0)
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
        }

        public static Mission FromDefinition(MissionDefinition definition, string offeredBy = "Mission Board", string factionId = null)
        {
            if (definition == null) return null;
            return new Mission(
                0,
                definition.Id,
                definition.Title,
                definition.Type,
                MissionDifficulty.Easy,
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
                0);
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
            int issuedCargoQuantity = 0)
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
                issuedCargoQuantity);
            mission.ElapsedTime = Math.Max(0f, elapsedTime);
            mission.ObjectiveRadius = Math.Clamp(objectiveRadius <= 0 ? 500 : objectiveRadius, 1, 10000);
            return mission;
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

    public static bool IsDeliveryType(MissionType type) =>
            type is MissionType.Delivery or MissionType.CourierDelivery or MissionType.FreightContract or MissionType.ExportContract;

        public string GetTypeLabel() => Type switch
        {
            MissionType.ReachLocation => "REACH LOCATION",
            MissionType.DestroyHostiles => "DESTROY HOSTILES",
            MissionType.Delivery => "DELIVERY",
            MissionType.CourierDelivery => "COURIER",
            MissionType.FreightContract => "FREIGHT CONTRACT",
            MissionType.ExportContract => "BULK EXPORT",
            MissionType.Bounty => "BOUNTY",
            MissionType.Escort => "ESCORT",
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
            Commodity commodity = CommodityCatalog.GetByIdOrName(PackageId);
            string label = commodity?.Name ?? (string.IsNullOrWhiteSpace(PackageId) ? "Mission package" : PackageId);
            return PackageQuantity > 0 ? $"{label} x{PackageQuantity}" : label;
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
            : Type is MissionType.Escort or MissionType.Delivery or MissionType.CourierDelivery or MissionType.FreightContract or MissionType.ExportContract ? "Destination unavailable" : "Location unavailable";

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
            MissionType.Bounty => $"Destroy {GetTargetLabel()}",
            MissionType.Escort => $"Escort {GetTargetLabel()} to {GetDestinationLabel()}",
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
            MissionType.Escort => string.IsNullOrWhiteSpace(Destination) ? "Destination unavailable" : string.Empty,
            _ => string.Empty
        };

        public string GetHudProgressLine() => Type switch
        {
            MissionType.DestroyHostiles => $"Hostiles destroyed: {CurrentProgress} / {RequiredProgress}",
            MissionType.ReachLocation => $"Reach {GetDestinationLabel()}",
            MissionType.CourierDelivery => ObjectiveComplete ? "Cargo delivered" : $"Deliver package to {GetDestinationLabel()}",
            MissionType.FreightContract => ObjectiveComplete ? "Freight delivered" : $"Deliver {GetTargetLabel()} to {GetDestinationLabel()}",
            MissionType.ExportContract => ObjectiveComplete ? "Export delivered" : $"Haul {GetTargetLabel()} to {GetDestinationLabel()}",
            _ => GetObjectiveText()
        };
    }
}
