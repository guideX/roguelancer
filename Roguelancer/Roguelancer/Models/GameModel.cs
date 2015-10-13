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
        /// <param name="game"></param>
        public GameModel(RoguelancerGame game) {
            try {
                Velocity = Vector3.Zero;
                Position = new Vector3(.5f, MinimumAltitude, 0);
                Up = Vector3.Up;
                Right = Vector3.Right;
                CurrentThrust = 0.0f;
                Direction = Vector3.Forward;
                particleSystem = new clsParticleSystemHandler(game);
                ParticleSystemEnabled = false;
                Scale = 0f;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                if (ParticleSystemEnabled) {
                    particleSystem.Initialize(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                if (WorldObject.settingsModelObject.modelPath == "bullet") {
                    model = game.Objects.Bullets.BulletsModel;
                } else {
                    model = game.Content.Load<Model>(WorldObject.settingsModelObject.modelPath);
                }
                Position = WorldObject.startupPosition;
                Up = WorldObject.initialModelUp;
                Right = WorldObject.initialModelRight;
                Rotation = new Vector2(WorldObject.startupModelRotation.X, WorldObject.startupModelRotation.Y);
                Velocity = WorldObject.initialVelocity;
                CurrentThrust = WorldObject.initialCurrentThrust;
                Direction = WorldObject.initialDirection;
                if (ParticleSystemEnabled) {
                    particleSystem.LoadContent(game);
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
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                if (game.Input.InputItems.Toggles.ToggleCamera == false) {
                    World = Matrix.Identity;
                    World.Forward = Direction;
                    World.Up = Up;
                    World.Right = Right;
                    World.Translation = Position;
                }
                if (game.GameState.CurrentGameState != GameStates.Playing) {
                    CurrentThrust = 0.0f;
                    game.Input.InputItems.Toggles.Cruise = false;
                }
                if (ParticleSystemEnabled) {
                    particleSystem.Update(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                if (game.GameState.CurrentGameState == GameStates.Playing) {
                    var transforms = new Matrix[model.Bones.Count];
                    model.CopyAbsoluteBoneTransformsTo(transforms);
                    if (ParticleSystemEnabled) {
                        particleSystem.Draw(game, this);
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
                            basicEffect.View = game.Camera.View;
                            basicEffect.Projection = game.Camera.Projection;

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