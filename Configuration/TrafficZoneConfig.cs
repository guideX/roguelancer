using Microsoft.Xna.Framework;
using System.Text.Json.Serialization;

namespace Roguelancer.Configuration
{
    /// <summary>
    /// Lightweight living-world traffic behaviors for encounters and ambient NPC movement.
    /// </summary>
    public enum TrafficZoneBehaviorType
    {
        LawfulPatrol,
        TraderRoute,
        PirateAmbush,
        StationTraffic
    }

    /// <summary>
    /// Configures a compact NPC traffic zone.
    /// </summary>
    public sealed class TrafficZoneConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("system_index")]
        public int SystemIndex { get; set; }

        [JsonPropertyName("center_x")]
        public float CenterX { get; set; }

        [JsonPropertyName("center_y")]
        public float CenterY { get; set; }

        [JsonPropertyName("center_z")]
        public float CenterZ { get; set; }

        [JsonPropertyName("radius")]
        public float Radius { get; set; } = 2000f;

        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = Roguelancer.FactionManager.NeutralCivilians;

        [JsonPropertyName("ship_description")]
        public string ShipDescription { get; set; } = string.Empty;

        [JsonPropertyName("min_ships")]
        public int MinShips { get; set; } = 1;

        [JsonPropertyName("max_ships")]
        public int MaxShips { get; set; } = 3;

        [JsonPropertyName("spawn_interval")]
        public float SpawnInterval { get; set; } = 20f;

        [JsonPropertyName("behavior_type")]
        public Roguelancer.TrafficZoneBehaviorType BehaviorType { get; set; } = Roguelancer.TrafficZoneBehaviorType.LawfulPatrol;

        [JsonPropertyName("route_start_x")]
        public float? RouteStartX { get; set; }

        [JsonPropertyName("route_start_y")]
        public float? RouteStartY { get; set; }

        [JsonPropertyName("route_start_z")]
        public float? RouteStartZ { get; set; }

        [JsonPropertyName("route_end_x")]
        public float? RouteEndX { get; set; }

        [JsonPropertyName("route_end_y")]
        public float? RouteEndY { get; set; }

        [JsonPropertyName("route_end_z")]
        public float? RouteEndZ { get; set; }

        [JsonIgnore]
        public Vector3 Center => new Vector3(CenterX, CenterY, CenterZ);

        [JsonIgnore]
        public Vector3? RouteStart => RouteStartX.HasValue && RouteStartY.HasValue && RouteStartZ.HasValue
            ? new Vector3(RouteStartX.Value, RouteStartY.Value, RouteStartZ.Value)
            : null;

        [JsonIgnore]
        public Vector3? RouteEnd => RouteEndX.HasValue && RouteEndY.HasValue && RouteEndZ.HasValue
            ? new Vector3(RouteEndX.Value, RouteEndY.Value, RouteEndZ.Value)
            : null;
    }
}
