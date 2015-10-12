// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Settings;
using Roguelancer.Enum;
using Roguelancer.Functionality;
namespace Roguelancer.Models {
    /// <summary>
    /// Game Model
    /// </summary>
    public class GameModel : IGameModel {
        /// <summary>
        /// Scale
        /// </summary>
        public float Scale { get; set; }
        /// <summary>
        /// Use Scale
        /// </summary>
        public bool UseScale { get; set; }
        /// <summary>
        /// Model Mode
        /// </summary>
        public ModelModeEnum ModelMode { get; set; }
        /// <summary>
        /// Current Thrust
        /// </summary>
        public float CurrentThrust { get; set; }
        /// <summary>
        /// Velocity
        /// </summary>
        public Vector3 Velocity { get; set; }
        /// <summary>
        /// Rotation
        /// </summary>
        public Vector2 Rotation { get; set; }
        /// <summary>
        /// Up
        /// </summary>
        public Vector3 Up;
        /// <summary>
        /// Right
        /// </summary>
        public Vector3 Right;
        /// <summary>
        /// Position
        /// </summary>
        public Vector3 Position;
        /// <summary>
        /// Direction
        /// </summary>
        public Vector3 Direction;
        /// <summary>
        /// World
        /// </summary>
        public Matrix World;
        /// <summary>
        /// Minimum Altitude
        /// </summary>
        public float MinimumAltitude = 350.0f;
        /// <summary>
        /// World Object
        /// </summary>
        public ModelWorldObjects WorldObject { get; set; }
        /// <summary>
        /// Particle System Enabled
        /// </summary>
        public bool ParticleSystemEnabled { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        private Model model;
        /// <summary>
        /// Particle System
        /// </summary>
        private clsParticleSystemHandler particleSystem;
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="_Game"></param>
        public GameModel(RoguelancerGame _Game) {
            try {
                Velocity = Vector3.Zero;
                Position = new Vector3(.5f, MinimumAltitude, 0);
                Up = Vector3.Up;
                Right = Vector3.Right;
                CurrentThrust = 0.0f;
                Direction = Vector3.Forward;
                particleSystem = new clsParticleSystemHandler(_Game);
                ParticleSystemEnabled = false;
                Scale = 0f;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="_Game"></param>
        public void Initialize(RoguelancerGame _Game) {
            try {
                if (ParticleSystemEnabled) {
                    particleSystem.Initialize(_Game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="_Game"></param>
        public void LoadContent(RoguelancerGame _Game) {
            try {
                if (WorldObject.settingsModelObject.modelPath == "bullet") {
                    model = _Game.Objects.Bullets.BulletsModel;
                } else {
                    model = _Game.Content.Load<Model>(WorldObject.settingsModelObject.modelPath);
                }
                Position = WorldObject.startupPosition;
                Up = WorldObject.initialModelUp;
                Right = WorldObject.initialModelRight;
                Rotation = new Vector2(WorldObject.startupModelRotation.X, WorldObject.startupModelRotation.Y);
                Velocity = WorldObject.initialVelocity;
                CurrentThrust = WorldObject.initialCurrentThrust;
                Direction = WorldObject.initialDirection;
                if (ParticleSystemEnabled) {
                    particleSystem.LoadContent(_Game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update Position
        /// </summary>
        public void UpdatePosition() {
            try {
                Matrix rotationMatrix = Matrix.CreateFromAxisAngle(Right, Rotation.Y) * Matrix.CreateRotationY(Rotation.X);
                Direction = Vector3.TransformNormal(Direction, rotationMatrix);
                Up = Vector3.TransformNormal(Up, rotationMatrix);
                Direction.Normalize();
                Up.Normalize();
                Right = Vector3.Cross(Direction, Up);
                Up = Vector3.Cross(Right, Direction);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="_Game"></param>
        public void Update(RoguelancerGame _Game) {
            try {
                if (_Game.Input.lInputItems.toggles.toggleCamera == false) {
                    World = Matrix.Identity;
                    World.Forward = Direction;
                    World.Up = Up;
                    World.Right = Right;
                    World.Translation = Position;
                }
                if (_Game.GameState.currentGameState != GameState.GameStates.playing) {
                    CurrentThrust = 0.0f;
                    _Game.Input.lInputItems.toggles.cruise = false;
                }
                if (ParticleSystemEnabled) {
                    particleSystem.Update(_Game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="_Game"></param>
        public void Draw(RoguelancerGame _Game) {
            try {
                if (_Game.GameState.currentGameState == GameState.GameStates.playing) {
                    var transforms = new Matrix[model.Bones.Count];
                    model.CopyAbsoluteBoneTransformsTo(transforms);
                    if (ParticleSystemEnabled) {
                        particleSystem.Draw(_Game, this);
                    }
                    foreach (ModelMesh modelMesh in model.Meshes) {
                        foreach (BasicEffect basicEffect in modelMesh.Effects) {
                            basicEffect.Alpha = 1;
                            basicEffect.EnableDefaultLighting();
                            if (UseScale) {
                                basicEffect.World = Matrix.CreateScale(Scale) * transforms[modelMesh.ParentBone.Index] * World;
                            } else {
                                basicEffect.World = transforms[modelMesh.ParentBone.Index] * World;
                            }
                            basicEffect.View = _Game.Camera.View;
                            basicEffect.Projection = _Game.Camera.Projection;

                        }
                        modelMesh.Draw();
                    }
                }
            } catch {
                throw;
            }
        }
    }
}