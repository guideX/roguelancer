using Roguelancer.Enum;
namespace Roguelancer.Models {
    /// <summary>
    /// Settings Object Model
    /// </summary>
    public class SettingsObjectModel {
        /// <summary>
        /// Model Path
        /// </summary>
        public string ModelPath { get; set; }
        /// <summary>
        /// Model Scaling
        /// </summary>
        //public Vector3 ModelScaling { get; set; }
        /// <summary>
        /// Enabled
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// ModelId
        /// </summary>
        public int ModelId { get; set; }
        /// <summary>
        /// Model Type
        /// </summary>
        public ModelTypeEnum ModelType { get; set; }
        /// <summary>
        /// Is Player
        /// </summary>
        public bool IsPlayer { get; set; }
        /// <summary>
        /// Scaling
        /// </summary>
        public float Scaling { get; set; }
        /// <summary>
        /// Settings Object Model
        /// </summary>
        /// <param name="modelPath"></param>
        /// <param name="modelType"></param>
        /// <param name="enabled"></param>
        /// <param name="modelId"></param>
        public SettingsObjectModel(string modelPath, ModelTypeEnum modelType, bool enabled, int modelId, float scaling) {
            ModelPath = modelPath;
            ModelType = modelType;
            Enabled = enabled;
            ModelId = modelId;
            Scaling = scaling;
        }
    }
}