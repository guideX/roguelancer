// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Interfaces;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Settings;
using Roguelancer.Enum;
using Roguelancer.Functionality;
namespace Roguelancer.Models {
    public class GameModel : IGame {
        public float scale { get; set; }
        public bool useScale { get; set; }
        public ModelModeEnum modelMode { get; set; }
        public float currentThrust { get; set; }
        public Vector3 velocity { get; set; }
        public Vector2 rotation { get; set; }
        public Vector3 up;
        public Vector3 right;
        public Vector3 position;
        public Vector3 direction;
        public Matrix world;
        public float minimumAltitude = 350.0f;
        private Model model;
        public ModelWorldObjects worldObject { get; set; }
        private clsParticleSystemHandler particleSystem;
        public bool particleSystemEnabled { get; set; }
        public GameModel(RoguelancerGame _Game) {
            velocity = Vector3.Zero;
            position = new Vector3(.5f, minimumAltitude, 0);
            up = Vector3.Up;
            right = Vector3.Right;
            currentThrust = 0.0f;
            direction = Vector3.Forward;
            particleSystem = new clsParticleSystemHandler(_Game);
            particleSystemEnabled = false;
            scale = 0f;
        }
        public void Initialize(RoguelancerGame _Game) {
            if (particleSystemEnabled) {
                particleSystem.Initialize(_Game);
            }
        }
        public void LoadContent(RoguelancerGame _Game) {
            if (worldObject.settingsModelObject.modelPath == "bullet") {
                model = _Game.objects.Bullets.BulletsModel;
            } else {
                model = _Game.Content.Load<Model>(worldObject.settingsModelObject.modelPath);
            }
            position = worldObject.startupPosition;
            up = worldObject.initialModelUp;
            right = worldObject.initialModelRight;
            rotation= new Vector2(worldObject.startupModelRotation.X, worldObject.startupModelRotation.Y);
            velocity = worldObject.initialVelocity;
            currentThrust = worldObject.initialCurrentThrust;
            direction = worldObject.initialDirection;
            if (particleSystemEnabled) {
                particleSystem.LoadContent(_Game);
            }
        }
        public void UpdatePosition() {
            Matrix rotationMatrix = Matrix.CreateFromAxisAngle(right, rotation.Y) * Matrix.CreateRotationY(rotation.X);
            direction = Vector3.TransformNormal(direction, rotationMatrix);
            up = Vector3.TransformNormal(up, rotationMatrix);
            direction.Normalize();
            up.Normalize();
            right = Vector3.Cross(direction, up);
            up = Vector3.Cross(right, direction);
        }
        public void Update(RoguelancerGame _Game) {
            if (_Game.input.lInputItems.toggles.toggleCamera == false) {
                world = Matrix.Identity;
                world.Forward = direction;
                world.Up = up;
                world.Right = right;
                world.Translation = position;
            }
            if (_Game.gameState.currentGameState != GameState.GameStates.playing) {
                currentThrust = 0.0f;
                _Game.input.lInputItems.toggles.cruise = false;
            }
            if (particleSystemEnabled) {
                particleSystem.Update(_Game);
            }
        }
        public void Draw(RoguelancerGame _Game) {
            if (_Game.gameState.currentGameState == GameState.GameStates.playing) {
                var transforms = new Matrix[model.Bones.Count];
                model.CopyAbsoluteBoneTransformsTo(transforms);
                if (particleSystemEnabled) {
                    particleSystem.Draw(_Game, this);
                }
                foreach (ModelMesh modelMesh in model.Meshes) {
                    foreach (BasicEffect basicEffect in modelMesh.Effects) {
                        basicEffect.Alpha = 1;
                        basicEffect.EnableDefaultLighting();
                        if (useScale) {
                            basicEffect.World = Matrix.CreateScale(scale) * transforms[modelMesh.ParentBone.Index] * world;
                        } else {
                            basicEffect.World = transforms[modelMesh.ParentBone.Index] * world;
                        }
                        basicEffect.View = _Game.camera.view;
                        basicEffect.Projection = _Game.camera.projection;
                        
                    }
                    modelMesh.Draw();
                }
            }
        }
    }
}