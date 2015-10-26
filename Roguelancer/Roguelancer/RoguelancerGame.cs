// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Bloom;
using Roguelancer.Enum;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
using Roguelancer.Settings;
namespace Roguelancer {
    /// <summary>
    /// Roguelancer Game
    /// </summary>
    public class RoguelancerGame : Game {
        #region "public variables"
        /// <summary>
        /// Star System
        /// </summary>
        public int StarSystemId { get; set; }
        /// <summary>
        /// Hud
        /// </summary>
        public HudObject Hud { get; set; }
        /// <summary>
        /// Graphics
        /// </summary>
        public IGameGraphics Graphics { get; set; }
        /// <summary>
        /// Settings
        /// </summary>
        public IGameSettings Settings { get; set; }
        /// <summary>
        /// Camera
        /// </summary>
        public IGameCamera Camera { get; set; }
        /// <summary>
        /// Input
        /// </summary>
        public IInput Input { get; set; }
        /// <summary>
        /// Debug Text
        /// </summary>
        public IDebugText DebugText { get; set; }
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
        /// Game Time
        /// </summary>
        public GameTime GameTime { get; set; }
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
            Hud = new HudObject();
            GameMenu.CurrentMenu = CurrentMenu.HomeMenu;
            StarSystemId = 0;
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
            Hud.Initialize(this);
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
            DebugText.SetText(this, ".", false);
            GameMenu.LoadContent(this);
            Hud.LoadContent(this);
            base.LoadContent();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime) {
            try {
                GameTime = gameTime;
                Input.Update(this);
                _bloom.Update(true);
                Objects.Update(this);
                Camera.Update(this);
                DebugText.Update(this);
                GameMenu.Update(this);
                if (GameState.CurrentGameState == GameStates.Playing) {
                    Hud.Update(this);
                }
                base.Update(gameTime);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Draw(GameTime gameTime) {
            try {
                GameTime = gameTime;
                Graphics.SpriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.AnisotropicWrap, DepthStencilState.Default, RasterizerState.CullNone);
                _bloom.Draw();
                if (GameState.CurrentGameState == GameStates.Playing) {
                    if (GameState.LastGameState != GameState.CurrentGameState) {
                        Graphics.GraphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                    }
                    Graphics.Draw(this);
                    Objects.Draw(this);
                    Hud.Draw(this);
                } else if (GameState.CurrentGameState == GameStates.Menu) {
                    if (GameState.LastGameState != GameState.CurrentGameState) {
                        Graphics.GraphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                    }
                    GameMenu.Draw(this);
                    Graphics.Draw(this);
                } else if (GameState.CurrentGameState == GameStates.Docked) {
                    
                }
                DebugText.Draw(this);
                Graphics.SpriteBatch.End();
                base.Draw(gameTime);
            } catch {
                throw;
            }
        }
        #endregion
    }
}