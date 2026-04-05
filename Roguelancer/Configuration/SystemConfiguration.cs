using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roguelancer.Configuration
{
    public class PlanetConfiguration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("radius")]
        public float Radius { get; set; }

        [JsonPropertyName("orbit_radius")]
        public float OrbitRadius { get; set; }

        [JsonPropertyName("orbit_speed")]
        public float OrbitSpeed { get; set; }

        [JsonPropertyName("position_x")]
        public float PositionX { get; set; } = 0f;

        [JsonPropertyName("position_y")]
        public float PositionY { get; set; } = 0f;

        [JsonPropertyName("position_z")]
        public float PositionZ { get; set; } = 0f;

        [JsonPropertyName("surface_model_asset")]
        public string SurfaceModelAsset { get; set; }

        [JsonPropertyName("clouds_model_asset")]
        public string CloudsModelAsset { get; set; }

        [JsonPropertyName("atmosphere_model_asset")]
        public string AtmosphereModelAsset { get; set; }

        [JsonPropertyName("surface_texture_asset")]
        public string SurfaceTextureAsset { get; set; }

        [JsonPropertyName("clouds_texture_asset")]
        public string CloudsTextureAsset { get; set; }

        [JsonPropertyName("atmosphere_texture_asset")]
        public string AtmosphereTextureAsset { get; set; }
    }

    public class SystemConfiguration
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("sun_position_x")]
        public float SunPositionX { get; set; }

        [JsonPropertyName("sun_position_y")]
        public float SunPositionY { get; set; }

        [JsonPropertyName("sun_position_z")]
        public float SunPositionZ { get; set; }

        [JsonPropertyName("sun_scale")]
        public float SunScale { get; set; }

        [JsonPropertyName("sun_intensity")]
        public float SunIntensity { get; set; }

        [JsonPropertyName("sun_rotation_speed")]
        public float SunRotationSpeed { get; set; }

        [JsonPropertyName("system_name")]
        public string SystemName { get; set; }

        [JsonPropertyName("planets")]
        public List<PlanetConfiguration> Planets { get; set; }
    }
}
