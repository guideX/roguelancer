using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Settings;
using Roguelancer.Enum;
using Roguelancer.Particle.System;
namespace Roguelancer.Models {
    /// <summary>
    /// Game Model
    /// </summary>
    public class GameModel : IGameModel {
        #region "public properties"
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
        //public ModelModeEnum ModelMode { get; set; }
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
        public float MinimumAltitude { get; set; }
        /// <summary>
        /// World Object
        /// </summary>
        public ModelWorldObjects WorldObject { get; set; }
        /// <summary>
        /// Particle System
        /// </summary>
        public ParticleSystem ParticleSystem { get; set; }
        #endregion
        #region "private properties"
        /// <summary>
        /// Model
        /// </summary>
        private Model _model;
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public GameModel(RoguelancerGame game, ParticleSystemSettingsModel particleSystemSettings) {
            MinimumAltitude = -350.0f;
            Velocity = Vector3.Zero;
            Position = new Vector3(.5f, MinimumAltitude, 0);
            //Position = new Vector3(0, MinimumAltitude, 0);
            Up = Vector3.Up;
            Right = Vector3.Right;
            CurrentThrust = 0.0f;
            Direction = Vector3.Forward;
            if (particleSystemSettings == null) { particleSystemSettings = new ParticleSystemSettingsModel(); }
            if (particleSystemSettings.Enabled) {
                ParticleSystem = new ParticleSystem(game);
                ParticleSystem.Settings = particleSystemSettings;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            if (ParticleSystem != null) {
                if (ParticleSystem.Settings.Enabled) {
                    ParticleSystem.Initialize(game);
                }
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            if (WorldObject.SettingsModelObject.modelPath == "bullet") {
                _model = game.Objects.Model.Bullets.BulletsModel;
            } else {
                _model = game.Content.Load<Model>(WorldObject.SettingsModelObject.modelPath);
            }
            Position = WorldObject.StartupPosition;
            Up = WorldObject.InitialModelUp;
            Right = WorldObject.InitialModelRight;
            Rotation = new Vector2(WorldObject.StartupRotation.X, WorldObject.StartupRotation.Y);
            Velocity = WorldObject.InitialVelocity;
            CurrentThrust = WorldObject.InitialCurrentThrust;
            Scale = WorldObject.Scaling;
            if (Scale != 0f) {
                UseScale = true;
            }
            Direction = WorldObject.InitialDirection;
            if (ParticleSystem != null) {
                if (ParticleSystem.Settings.Enabled) {
                    ParticleSystem.LoadContent(game);
                }
            }
        }
        /// <summary>
        /// Update Position
        /// </summary>
        public void UpdatePosition() {
            var rotationMatrix = Matrix.CreateFromAxisAngle(Right, Rotation.Y) * Matrix.CreateRotationY(Rotation.X);
            Direction = Vector3.TransformNormal(Direction, rotationMatrix);
            Up = Vector3.TransformNormal(Up, rotationMatrix);
            Direction.Normalize();
            Up.Normalize();
            Right = Vector3.Cross(Direction, Up);
            Up = Vector3.Cross(Right, Direction);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (!game.Input.InputItems.Toggles.ToggleCamera) {
                World = Matrix.Identity;
                World.Forward = Direction;
                World.Up = Up;
                World.Right = Right;
                World.Translation = Position;
            }
            if (game.GameState.Model.CurrentGameState != GameStatesEnum.Playing) {
                CurrentThrust = 0.0f;
                game.Input.InputItems.Toggles.Cruise = false;
            }
            if (ParticleSystem != null) {
                if (ParticleSystem.Settings.Enabled) {
                    ParticleSystem.Update(game);
                    ParticleSystem.Settings.Position = Position;
                    ParticleSystem.Settings.Rotation = Rotation;
                }
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                var transforms = new Matrix[_model.Bones.Count];
                _model.CopyAbsoluteBoneTransformsTo(transforms);
                if (ParticleSystem != null) {
                    if (ParticleSystem.Settings.Enabled) {
                        ParticleSystem.Draw(game);
                    }
                }
                foreach (ModelMesh mm in _model.Meshes) {
                    foreach (BasicEffect be in mm.Effects) {
                        be.Alpha = 1;
                        be.EnableDefaultLighting();
                        if (UseScale) {
                            be.World = Matrix.CreateScale(Scale) * transforms[mm.ParentBone.Index] * World;
                        } else {
                            be.World = transforms[mm.ParentBone.Index] * World;
                        }
                        be.View = game.Camera.Model.View;
                        be.Projection = game.Camera.Model.Projection;
                    }
                    mm.Draw();
                }
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Scale = new float();
            UseScale = false;
            CurrentThrust = new float();
            Velocity = new Vector3();
            Rotation = new Vector2();
            Up = new Vector3();
            Right = new Vector3();
            Position = new Vector3();
            Direction = new Vector3();
            World = new Matrix();
            MinimumAltitude = new float();
            if (ParticleSystem != null) {
                ParticleSystem.Settings = new ParticleSystemSettingsModel();
                ParticleSystem = new ParticleSystem(game);
            }
            _model = null;
        }
        #endregion
    }
}