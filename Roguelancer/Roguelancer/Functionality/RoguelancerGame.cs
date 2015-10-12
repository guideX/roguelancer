using Microsoft.Xna.Framework;
using Roguelancer.Bloom;
using Roguelancer.Enum;
using Roguelancer.Objects;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Settings;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Roguelancer Game
    /// </summary>
    public class RoguelancerGame : Microsoft.Xna.Framework.Game {
        /// <summary>
        /// Graphics
        /// </summary>
        public GameGraphics Graphics;
        /// <summary>
        /// Settings
        /// </summary>
        public GameSettings Settings;
        /// <summary>
        /// Camera
        /// </summary>
        public GameCamera Camera;
        /// <summary>
        /// Input
        /// </summary>
        public Input Input;
        /// <summary>
        /// Game Time
        /// </summary>
        public GameTime GameTime;
        /// <summary>
        /// Debug Text
        /// </summary>
        public DebugText DebugText;
        /// <summary>
        /// Game State
        /// </summary>
        public GameState GameState;
        /// <summary>
        /// Objects
        /// </summary>
        public GameObjects Objects;
        /// <summary>
        /// Camera Snapshot
        /// </summary>
        public GameCamera CameraSnapshot;
        /// <summary>
        /// Bloom
        /// </summary>
        private BloomHandler Bloom;
        /// <summary>
        /// Game Menu
        /// </summary>
        public GameMenu GameMenu;
        //private clsParticleSystemHandler particleSystem;
        //private clsSound lEngineNoise;
        /// <summary>
        /// Entry Point
        /// </summary>
        public RoguelancerGame() {
            Settings = new GameSettings();
            Content.RootDirectory = "Content";
            GameState = new GameState();
            IsMouseVisible = true;
            Camera = new GameCamera();
            Graphics = new GameGraphics(this);
            Graphics.Initialize(this);
            Bloom = new BloomHandler(this);
            Input = new Input();
            DebugText = new DebugText();
            Objects = new GameObjects(this);
            GameMenu = new GameMenu();
            GameMenu.CurrentMenu = CurrentMenu.HomeMenu;
            //particleSystem = new clsParticleSystemHandler(this);
            //lEngineNoise = new clsSound();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        protected override void Initialize() {
            Graphics.Initialize(this);
            Camera.Initialize(this);
            Bloom.Initialize(this);
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
            Bloom.LoadContent();
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
            GameTime = _GameTime;
            Input.Update(this);
            Bloom.Update(true);
            Objects.Update(this);
            Camera.UpdateCameraChaseTarget(this);
            Camera.Update(this);
            DebugText.Update(this);
            GameMenu.Update(this);
            //particleSystem.Update(_GameTime, debugText, graphics);
            base.Update(_GameTime);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="_GameTime"></param>
        protected override void Draw(GameTime _GameTime) {
            GameTime = _GameTime;
            Graphics.BeginSpriteBatch();
            Bloom.Draw();
            if(GameState.currentGameState == GameState.GameStates.playing) {
                if(GameState.lastGameState != GameState.currentGameState) {
                    Graphics.graphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                }
                Graphics.Draw(this);
                //particleSystem.Draw(this);
                Objects.Draw(this);
            } else if(GameState.currentGameState == GameState.GameStates.menu) {
                if(GameState.lastGameState != GameState.currentGameState) {
                    Graphics.graphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                }
                GameMenu.Draw(this);
                Graphics.Draw(this);
            }
            DebugText.Draw(this);
            Graphics.EndSpriteBatch();
            base.Draw(_GameTime);
        }
    }
}