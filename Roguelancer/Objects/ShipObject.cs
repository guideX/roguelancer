using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Enum;
using Roguelancer.Particle.System.Emitters.ParticleSystems;
using Roguelancer.Particle.System.Emitters;
using Microsoft.Xna.Framework;
namespace Roguelancer.Objects {
    /// <summary>
    /// Ship
    /// </summary>
    public class ShipObject : IGame, ISensorObject, IDockableShip {
        #region "public properties"
        /// <summary>
        /// Ship Model
        /// </summary>
        public ShipModel ShipModel { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Ship
        /// </summary>
        /// <param name="game"></param>
        public ShipObject(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            //Model.Initialize();
            if (ShipModel.PlayerShipControl.Model.UseInput) {
                ShipModel.PlayerShipControl = new PlayerShipControl();
            }
            
            // Initialize engine glow particle system
            InitializeEngineGlow(game);
        }
        
        /// <summary>
        /// Initialize Engine Glow
        /// </summary>
        /// <param name="game"></param>
        private void InitializeEngineGlow(RoguelancerGame game) {
            try {
                // Create engine glow particle system with fire texture
                var particleTexture = game.Content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("Textures/fire");
                ShipModel.EngineGlowSystem = new EngineGlowParticleSystem(500, particleTexture);
                ShipModel.EngineGlowSystem.Initialize(game.GraphicsDevice);
                
                // Add engine emitter (positioned behind the ship)
                // You can adjust the offset based on your ship model's engine position
                var engineOffset = new Vector3(0, 0, -2.0f); // Behind the ship
                var engineEmitter = new EngineGlowEmitter(
                    Model.Position + engineOffset,
                    Model.Direction,
                    new Color(255, 150, 50), // Orange-yellow engine glow
                    60 // particles per second
                );
                
                ShipModel.EngineGlowSystem.AddEmitter(engineEmitter);
                ShipModel.EngineEmitters.Add(engineEmitter);
            } catch {
                // If engine glow initialization fails, continue without it
                ShipModel.EngineGlowSystem = null;
            }
        }
        
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                if (ShipModel.PlayerShipControl.Model.UseInput) {
                    ShipModel.PlayerShipControl.UpdateModel(Model, game);
                    //if (!game.Input.InputItems.Toggles.ToggleCamera) {
                    Model.Update(game);
                    //}
                } else {
                    Model.Update(game);
                }
                
                // Update engine glow
                UpdateEngineGlow(game);
            }
        }
        
        /// <summary>
        /// Update Engine Glow based on thrust
        /// </summary>
        /// <param name="game"></param>
        private void UpdateEngineGlow(RoguelancerGame game) {
            if (ShipModel.EngineGlowSystem == null || ShipModel.EngineEmitters.Count == 0) return;
            
            // Update engine emitter positions and thrust
            foreach (var emitter in ShipModel.EngineEmitters) {
                // Position engine behind the ship (adjust offset as needed for your models)
                var engineOffset = Model.Direction * -2.0f;
                emitter.Position = Model.Position + engineOffset;
                emitter.Direction = Model.Direction;
                emitter.Thrust = Model.CurrentThrust;
                
                // Check if afterburner is active
                if (ShipModel.PlayerShipControl.Model.UseInput) {
                    emitter.IsAfterburner = game.Input.InputItems.Keys.Tab.IsKeyDown && 
                                           Model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount;
                } else {
                    emitter.IsAfterburner = Model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount;
                }
            }
            
            // Update the particle system
            ShipModel.EngineGlowSystem.Update(game.GameTime);
        }
        
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (!ShipModel.Docked) {
                Model.Draw(game);
                
                // Draw engine glow
                if (ShipModel.EngineGlowSystem != null && Model.CurrentThrust != 0) {
                    ShipModel.EngineGlowSystem.Draw(game.Camera.Model.View, game.Camera.Model.Projection);
                }
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            ShipModel.Docked = false;
            Model.Dispose(game);
            
            // Dispose engine glow
            if (ShipModel.EngineGlowSystem != null) {
                ShipModel.EngineEmitters.Clear();
                ShipModel.EngineGlowSystem = null;
            }
            
            Model = null;
            ShipModel = null;
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            ShipModel = new ShipModel();
            Model = new GameModel(game, null, null, ModelTypeEnum.Ship, Model?.Description);
        }
        #endregion
    }
}