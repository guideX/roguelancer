using Microsoft.Xna.Framework;
namespace Roguelancer.Settings {
    public class SettingsModelObject {
        private string _modelPath { get; set; }
        private Vector3 _modelScaling { get; set; }
        private bool _enabled { get; set; }
        private int _modelId { get; set; }
        private ModelType _modelType { get; set; }
        private bool _isPlayer { get; set; }
        public SettingsModelObject(
                string modelPath,
                Vector3 modelScaling,
                ModelType modelType,
                bool enabled,
                int modelId
            ) {
            _modelPath = modelPath;
            _modelScaling = modelScaling;
            _modelType = modelType;
            _enabled = enabled;
            _modelId = modelId;
        }
        public static SettingsModelObject Clone(SettingsModelObject oldObject) {
            return new SettingsModelObject(
                oldObject.modelPath,
                oldObject.modelScaling,
                oldObject.modelType,
                oldObject.enabled,
                oldObject.modelId
            );
        }
        public string modelPath { get { return _modelPath; } }
        public ModelType modelType { get { return _modelType; } }
        public Vector3 modelScaling { get { return _modelScaling; } }
        public bool enabled { get { return _enabled; } }
        public int modelId { get { return _modelId; } }
        public bool isPlayer { get { return _isPlayer; } set { _isPlayer = value; } }
    }
}
