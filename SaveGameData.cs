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
        public const int CurrentSchemaVersion = 1;

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

        [JsonPropertyName("buy_price")]
        public int BuyPrice { get; set; }

        [JsonPropertyName("sell_price")]
        public int SellPrice { get; set; }

        [JsonPropertyName("stock")]
        public int Stock { get; set; }

        [JsonPropertyName("demand_level")]
        public int DemandLevel { get; set; }

        [JsonPropertyName("is_available")]
        public bool IsAvailable { get; set; }
    }
}
