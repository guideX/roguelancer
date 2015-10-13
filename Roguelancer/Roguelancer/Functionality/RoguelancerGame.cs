using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Bloom;
using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Settings;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Roguelancer Game
    /// </summary>
    public class RoguelancerGame : Game {
        #region "public variables"
        /// <summary>
        /// Graphics
        /// </summary>
        public GameGraphics Graphics { get; set; }
        /// <summary>
        /// Settings
        /// </summary>
        public GameSettings Settings { get; set; }
        /// <summary>
        /// Camera
        /// </summary>
        public IGameCamera Camera { get; set; }
        /// <summary>
        /// Input
        /// </summary>
        public Input Input { get; set; }
        /// <summary>
        /// Game Time
        /// </summary>
        public GameTime GameTime { get; set; }
        /// <summary>
        /// Debug Text
        /// </summary>
        public DebugText DebugText { get; set; }
        /// <summary>
        /// Game State
        /// </summary>
        public GameState GameState { get; set; }
        /// <summary>
        /// Objects
        /// </summary>
        public GameObjects Objects { get; set; }
        /// <summary>
        /// Camera Snapshot
        /// </summary>
        public IGameCamera CameraSnapshot { get; set; }
        /// <summary>
        /// Game Menu
        /// </summary>
        public GameMenu GameMenu { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Bloom
        /// </summary>
        private BloomHandler _bloom;
        private clsParticleSystemHandler particleSystem;
        //private clsSound lEngineNoise;
        /// <summary>
        /// Entry Point
        /// </summary>
        #endregion
        #region "public functions"
        public RoguelancerGame() {
            Settings = new GameSettings();
            Content.RootDirectory = "Content";
            GameState = new GameState();
            IsMouseVisible = true;
            Camera = new GameCamera();
            Graphics = new GameGraphics(this);
            Graphics.Initialize(this);
            _bloom = new BloomHandler(this);
            Input = new Input();
            DebugText = new DebugText();
            Objects = new GameObjects(this);
            GameMenu = new GameMenu();
            GameMenu.CurrentMenu = CurrentMenu.HomeMenu;
            particleSystem = new clsParticleSystemHandler(this);
            //lEngineNoise = new clsSound();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        protected override void Initialize() {
            Graphics.Initialize(this);
            Camera.Initialize(this);
            _bloom.Initialize(this);
            Input.Initialize(this);
            Objects.Initialize(this);
            GameMenu.Initialize(this);
            //particleSystem.Initialize();
            //lEngineNoise.Initialize(this);
            base.Initialize();
        }
        /// <summary>
        /// Load Content
        /// </summary>
        protected override void LoadContent() {
            Camera.LoadContent(this);
            Graphics.LoadContent(this);
            _bloom.LoadContent();
            DebugText.LoadContent(this);
            DebugText.Update(this);
            Objects.LoadContent(this);
            DebugText.Text = Settings.menuText;
            GameMenu.LoadContent(this);
            //particleSystem.LoadContent(this);
            //lEngineNoise.soundPath = "engine";
            base.LoadContent();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="_GameTime"></param>
        protected override void Update(GameTime _GameTime) {
            try {
                GameTime = _GameTime;
                Input.Update(this);
                _bloom.Update(true);
                Objects.Update(this);
                //Camera.UpdateCameraChaseTarget(this);
                Camera.Update(this);
                DebugText.Update(this);
                GameMenu.Update(this);
                //particleSystem.Update(_GameTime, debugText, graphics);
                base.Update(_GameTime);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="_GameTime"></param>
        protected override void Draw(GameTime _GameTime) {
            try {
                GameTime = _GameTime;
                Graphics.SpriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.AnisotropicWrap, DepthStencilState.Default, RasterizerState.CullNone);
                //Graphics.BeginSpriteBatch();
                _bloom.Draw();
                if (GameState.CurrentGameState == GameStates.Playing) {
                    if (GameState.LastGameState != GameState.CurrentGameState) {
                        Graphics.GraphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                    }
                    Graphics.Draw(this);
                    //particleSystem.Draw(this);
                    Objects.Draw(this);
                } else if (GameState.CurrentGameState == GameStates.Menu) {
                    if (GameState.LastGameState != GameState.CurrentGameState) {
                        Graphics.GraphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                    }
                    GameMenu.Draw(this);
                    Graphics.Draw(this);
                }
                DebugText.Draw(this);
                //Graphics.EndSpriteBatch();
                Graphics.SpriteBatch.End();
                base.Draw(_GameTime);
            } catch {
                throw;
            }
        }
        #endregion
    }
}