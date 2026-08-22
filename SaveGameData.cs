using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roguelancer
{
    /// <summary>
    /// Versioned save schema for single-player progression.
    /// </summary>
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 8;

        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonPropertyName("player_credits")]
        public int PlayerCredits { get; set; }

        [JsonPropertyName("current_system_index")]
        public int CurrentSystemIndex { get; set; } = 1;

        [JsonPropertyName("current_ship_name")]
        public string CurrentShipName { get; set; } = string.Empty;

        [JsonPropertyName("player_position")]
        public SaveVector3Data PlayerPosition { get; set; } = new SaveVector3Data();

        [JsonPropertyName("player_velocity")]
        public SaveVector3Data PlayerVelocity { get; set; } = new SaveVector3Data();

        [JsonPropertyName("player_forward")]
        public SaveVector3Data PlayerForward { get; set; } = new SaveVector3Data(0f, 0f, -1f);

        [JsonPropertyName("owned_equipment")]
        public List<SaveOwnedEquipmentData> OwnedEquipment { get; set; } = new();

        [JsonPropertyName("mounted_equipment")]
        public List<SaveMountedEquipmentData> MountedEquipment { get; set; } = new();

        [JsonPropertyName("cargo")]
        public List<SaveCargoItemData> Cargo { get; set; } = new();

        [JsonPropertyName("faction_reputation")]
        public List<SaveFactionReputationData> FactionReputation { get; set; } = new();

        [JsonPropertyName("active_missions")]
        public List<SaveMissionData> ActiveMissions { get; set; } = new();

        [JsonPropertyName("completed_missions")]
        public List<SaveMissionData> CompletedMissions { get; set; } = new();

        [JsonPropertyName("station_markets")]
        public List<SaveMarketStateData> StationMarkets { get; set; } = new();

        [JsonPropertyName("market_elapsed_ms")]
        public long MarketElapsedMilliseconds { get; set; }

        [JsonPropertyName("market_intelligence")]
        public List<SaveMarketIntelligenceData> MarketIntelligence { get; set; } = new();

        /// <summary>
        /// Optional player-created trade route. Null keeps older saves fully
        /// compatible and intentionally stores no UI formatting strings.
        /// </summary>
        [JsonPropertyName("trade_plan")]
        public SaveTradePlanData TradePlan { get; set; }
    }

    /// <summary>
    /// Serializable 3D vector payload.
    /// </summary>
    public sealed class SaveVector3Data
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        public SaveVector3Data()
        {
        }

        public SaveVector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static SaveVector3Data From(Vector3 value)
        {
            return new SaveVector3Data(Sanitize(value.X), Sanitize(value.Y), Sanitize(value.Z));
        }

        public Vector3 ToVector3(Vector3 fallback = default)
        {
            return new Vector3(
                Sanitize(X, fallback.X),
                Sanitize(Y, fallback.Y),
                Sanitize(Z, fallback.Z));
        }

        private static float Sanitize(float value, float fallback = 0f)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    /// <summary>
    /// Owned equipment entry stored in a save file.
    /// </summary>
    public sealed class SaveOwnedEquipmentData
    {
        [JsonPropertyName("equipment_id")]
        public string EquipmentId { get; set; } = string.Empty;

        [JsonPropertyName("equipment_type")]
        public EquipmentType EquipmentType { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Mounted equipment entry stored in a save file.
    /// </summary>
    public sealed class SaveMountedEquipmentData
    {
        [JsonPropertyName("hardpoint_id")]
        public string HardpointId { get; set; } = string.Empty;

        [JsonPropertyName("equipment_id")]
        public string EquipmentId { get; set; } = string.Empty;

        [JsonPropertyName("equipment_type")]
        public EquipmentType EquipmentType { get; set; }
    }

    /// <summary>
    /// Cargo stack entry stored in a save file.
    /// </summary>
    public sealed class SaveCargoItemData
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("mission_id")]
        public int MissionId { get; set; }

        [JsonPropertyName("mission_bound")]
        public bool MissionBound { get; set; }
    }

    /// <summary>
    /// Reputation entry stored in a save file.
    /// </summary>
    public sealed class SaveFactionReputationData
    {
        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        [JsonPropertyName("standing")]
        public float Standing { get; set; }
    }

    /// <summary>
    /// Mission entry stored in a save file.
    /// </summary>
    public sealed class SaveMissionData
    {
        [JsonPropertyName("definition_id")]
        public string DefinitionId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("mission_id")]
        public int MissionId { get; set; }

        [JsonPropertyName("type")]
        public MissionType Type { get; set; }

        [JsonPropertyName("difficulty")]
        public MissionDifficulty Difficulty { get; set; }

        [JsonPropertyName("status")]
        public MissionStatus Status { get; set; }

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonPropertyName("reward")]
        public int Reward { get; set; }

        [JsonPropertyName("time_limit")]
        public float TimeLimit { get; set; }

        [JsonPropertyName("elapsed_time")]
        public float ElapsedTime { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("offered_by")]
        public string OfferedBy { get; set; } = string.Empty;

        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        [JsonPropertyName("objective_complete")]
        public bool ObjectiveComplete { get; set; }

        [JsonPropertyName("target_location")]
        public string TargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("target_system_index")]
        public int TargetSystemIndex { get; set; }

        [JsonPropertyName("target_count")]
        public int TargetCount { get; set; }

        [JsonPropertyName("current_progress")]
        public int CurrentProgress { get; set; }

        [JsonPropertyName("required_progress")]
        public int RequiredProgress { get; set; }

        [JsonPropertyName("objective_radius")]
        public int ObjectiveRadius { get; set; } = 500;

        [JsonPropertyName("origin_station_id")]
        public string OriginStationId { get; set; } = string.Empty;

        [JsonPropertyName("origin_station_name")]
        public string OriginStationName { get; set; } = string.Empty;

        [JsonPropertyName("origin_system_index")]
        public int OriginSystemIndex { get; set; }

        [JsonPropertyName("accepted_at_utc")]
        public string AcceptedAtUtc { get; set; } = string.Empty;

        [JsonPropertyName("reward_paid")]
        public bool RewardPaid { get; set; }

        [JsonPropertyName("target_position")]
        public SaveVector3Data TargetPosition { get; set; }

        [JsonPropertyName("source_station_name")]
        public string SourceStationName { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_id")]
        public string DestinationStationId { get; set; } = string.Empty;

        [JsonPropertyName("package_id")]
        public string PackageId { get; set; } = string.Empty;

        [JsonPropertyName("package_quantity")]
        public int PackageQuantity { get; set; }

        [JsonPropertyName("package_volume")]
        public int PackageVolume { get; set; }

        [JsonPropertyName("mission_cargo_loaded")]
        public bool MissionCargoLoaded { get; set; }

        [JsonPropertyName("delivered_quantity")]
        public int DeliveredQuantity { get; set; }

        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("required_quantity")]
        public int RequiredQuantity { get; set; }

        [JsonPropertyName("issued_cargo_quantity")]
        public int IssuedCargoQuantity { get; set; }
    }

    /// <summary>
    /// Runtime station market snapshot stored in a save file.
    /// </summary>
    public sealed class SaveMarketStateData
    {
        [JsonPropertyName("station_key")]
        public string StationKey { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("listings")]
        public List<SaveMarketListingData> Listings { get; set; } = new();
    }

    /// <summary>
    /// Individual station market listing snapshot stored in a save file.
    /// </summary>
    public sealed class SaveMarketListingData
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        /// <summary>
        /// Legacy Phase 13 field retained for source compatibility. Prices are
        /// derived from current stock and station configuration in Phase 14 and
        /// are intentionally not serialized.
        /// </summary>
        [JsonIgnore]
        public int BuyPrice { get; set; }

        /// <summary>
        /// Legacy Phase 13 field retained for source compatibility. See
        /// <see cref="BuyPrice"/>.
        /// </summary>
        [JsonIgnore]
        public int SellPrice { get; set; }

        [JsonPropertyName("stock")]
        public int Stock { get; set; }

        [JsonPropertyName("demand_level")]
        public int DemandLevel { get; set; }

        [JsonPropertyName("is_available")]
        public bool IsAvailable { get; set; }

        [JsonPropertyName("recovery_remainder_ms")]
        public long RecoveryRemainderMilliseconds { get; set; }

        [JsonPropertyName("immediate_sell_price_ceiling")]
        public int ImmediateSellPriceCeiling { get; set; }
    }

    /// <summary>
    /// A player's last observed quote. This is deliberately separate from the
    /// authoritative runtime market snapshot above.
    /// </summary>
    public sealed class SaveMarketIntelligenceData
    {
        [JsonPropertyName("station_id")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("system_index")]
        public int SystemIndex { get; set; }

        [JsonPropertyName("station_position")]
        public SaveVector3Data StationPosition { get; set; } = new();

        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("stock")]
        public int Stock { get; set; }

        [JsonPropertyName("buy_price")]
        public int BuyPrice { get; set; }

        [JsonPropertyName("sell_price")]
        public int SellPrice { get; set; }

        [JsonPropertyName("baseline_stock")]
        public int BaselineStock { get; set; }

        [JsonPropertyName("demand_level")]
        public int DemandLevel { get; set; }

        [JsonPropertyName("market_condition")]
        public string MarketCondition { get; set; } = string.Empty;

        [JsonPropertyName("observed_at_ms")]
        public long ObservedAtMilliseconds { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    public sealed class SaveTradePlanData
    {
        [JsonPropertyName("source_station_id")]
        public string SourceStationId { get; set; } = string.Empty;

        [JsonPropertyName("source_station_name")]
        public string SourceStationName { get; set; } = string.Empty;

        [JsonPropertyName("source_system_index")]
        public int SourceSystemIndex { get; set; }

        [JsonPropertyName("destination_station_id")]
        public string DestinationStationId { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_name")]
        public string DestinationStationName { get; set; } = string.Empty;

        [JsonPropertyName("destination_system_index")]
        public int DestinationSystemIndex { get; set; }

        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("commodity_name")]
        public string CommodityName { get; set; } = string.Empty;

        [JsonPropertyName("source_buy_price")]
        public int SourceBuyPrice { get; set; }

        [JsonPropertyName("destination_sell_price")]
        public int DestinationSellPrice { get; set; }

        [JsonPropertyName("source_observed_at_ms")]
        public long SourceObservedAtMilliseconds { get; set; }

        [JsonPropertyName("destination_observed_at_ms")]
        public long DestinationObservedAtMilliseconds { get; set; }

        [JsonPropertyName("stage")]
        public TradePlanStage Stage { get; set; }

        [JsonPropertyName("route_distance_units")]
        public float RouteDistanceUnits { get; set; }

        [JsonPropertyName("route_hops")]
        public int RouteHops { get; set; }

        [JsonPropertyName("opportunity_score")]
        public int OpportunityScore { get; set; }

        [JsonPropertyName("suggested_quantity")]
        public int SuggestedQuantity { get; set; }

        [JsonPropertyName("initial_ordinary_quantity")]
        public int InitialOrdinaryQuantity { get; set; }

        [JsonPropertyName("acquired_quantity")]
        public int AcquiredQuantity { get; set; }

        [JsonPropertyName("purchased_quantity")]
        public int PurchasedQuantity { get; set; }

        [JsonPropertyName("sold_quantity")]
        public int SoldQuantity { get; set; }

        [JsonPropertyName("actual_source_buy_price")]
        public int ActualSourceBuyPrice { get; set; }

        [JsonPropertyName("actual_destination_sell_price")]
        public int ActualDestinationSellPrice { get; set; }

        [JsonPropertyName("cargo_acquired")]
        public bool CargoAcquired { get; set; }
    }
}
