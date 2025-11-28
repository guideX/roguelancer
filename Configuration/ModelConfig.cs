using System.Text.Json.Serialization;

namespace Roguelancer.Configuration {
    /// <summary>
    /// Configuration for a 3D model
    /// </summary>
    public class ModelConfig {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public int Type { get; set; } = 0;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("model_scaling")]
        public float ModelScaling { get; set; } = 1.0f;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }
}
