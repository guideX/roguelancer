using Microsoft.Xna.Framework;
using Roguelancer.Enum;
namespace Roguelancer.Settings {
    /// <summary>
    /// Settings Model Object
    /// </summary>
    public class SettingsModelObject {
        /// <summary>
        /// Model Path
        /// </summary>
        private string _modelPath { get; set; }
        /// <summary>
        /// Model Scaling
        /// </summary>
        private Vector3 _modelScaling { get; set; }
        /// <summary>
        /// Enabled
        /// </summary>
        private bool _enabled { get; set; }
        /// <summary>
        /// ModelId
        /// </summary>
        private int _modelId { get; set; }
        /// <summary>
        /// Model Type
        /// </summary>
        private ModelType _modelType { get; set; }
        /// <summary>
        /// Is Player
        /// </summary>
        private bool _isPlayer { get; set; }
        /// <summary>
        /// Settings Model Object 
        /// </summary>
        /// <param name="modelPath"></param>
        /// <param name="modelType"></param>
        /// <param name="enabled"></param>
        /// <param name="modelId"></param>
        public SettingsModelObject(
                string modelPath,
                ModelType modelType,
                bool enabled,
                int modelId
            ) {
            try {
                _modelPath = modelPath;
                _modelType = modelType;
                _enabled = enabled;
                _modelId = modelId;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Clone
        /// </summary>
        /// <param name="oldObject"></param>
        /// <returns></returns>
        public static SettingsModelObject Clone(SettingsModelObject oldObject) {
            return new SettingsModelObject(
                oldObject.modelPath,
                oldObject.modelType,
                oldObject.enabled,
                oldObject.modelId
            );
        }
        /// <summary>
        /// Model Path
        /// </summary>
        public string modelPath { get { return _modelPath; } }
        /// <summary>
        /// Model Type
        /// </summary>
        public ModelType modelType { get { return _modelType; } }
        /// <summary>
        /// Enabled
        /// </summary>
        public bool enabled { get { return _enabled; } }
        /// <summary>
        /// Model Id
        /// </summary>
        public int modelId { get { return _modelId; } }
        /// <summary>
        /// Is Player
        /// </summary>
        public bool isPlayer { get { return _isPlayer; } set { _isPlayer = value; } }
    }
}
