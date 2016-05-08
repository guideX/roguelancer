// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Bloom;
using Roguelancer.Enum;
using Roguelancer.Functionality;
using Roguelancer.Helpers;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
using Roguelancer.Settings;
namespace Roguelancer {
    /// <summary>
    /// Roguelancer Game
    /// </summary>
    public class RoguelancerGame : Game {
        #region "public properties"
        /// <summary>
        /// Star System
        /// </summary>
        public int StarSystemId { get; set; }
        /// <summary>
        /// Hud
        /// </summary>
        public IHudObject Hud { get; set; }
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
        public IGameStates GameState { get; set; }
        /// <summary>
        /// Objects
        /// </summary>
        public IGameObjects Objects { get; set; }
        /// <summary>
        /// Camera Snapshot
        /// </summary>
        public IGameCamera CameraSnapshot { get; set; }
        /// <summary>
        /// Game Menu
        /// </summary>
        public IGameMenu GameMenu { get; set; }
        /// <summary>
        /// Game Time
        /// </summary>
        public GameTime GameTime { get; set; }
        #endregion
        #region "private properties"
        /// <summary>
        /// Bloom
        /// </summary>
        private BloomHandler _bloom;
        /// <summary>
        /// Entry Point
        /// </summary>
        #endregion
        #region "public methods"
        public RoguelancerGame() {
            Settings = new GameSettings(this);
            Content.RootDirectory = "Content";
            GameState = new GameState(this);
            IsMouseVisible = true;
            Camera = new GameCamera(this);
            Graphics = new GameGraphics(this);
            //Graphics.Initialize(this);
            _bloom = new BloomHandler(this);
            Input = new Input();
            DebugText = new DebugText(this);
            Objects = new GameObjects(this);
            GameMenu = new GameMenu(this);
            Hud = new HudObject(this);
            GameMenu.Model.CurrentMenu = CurrentMenu.HomeMenu;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        protected override void Initialize() {
            //Graphics.Initialize(this);
            Camera.Initialize(this);
            _bloom.Initialize(this);
            //Input.Initialize(this);
            Objects.Initialize(this);
            GameMenu.Initialize(this);
            Hud.Initialize(this);
            base.Initialize();
        }
        /// <summary>
        /// Load Content
        /// </summary>
        protected override void LoadContent() {
            Graphics.LoadContent(this);
            DebugText.LoadContent(this);
            DebugText.Update(this);
            Objects.LoadContent(this);
            DebugTextHelper.SetText(this, ".", false);
            GameMenu.LoadContent(this);
            Hud.LoadContent(this);
            base.LoadContent();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime) {
            GameTime = gameTime;
            Input.Update(this);
            _bloom.Update(this);
            Objects.Update(this);
            Camera.Update(this);
            DebugText.Update(this);
            GameMenu.Update(this);
            if (GameState.Model.CurrentGameState == GameStates.Playing) {
                Hud.Update(this);
            }
            base.Update(gameTime);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Draw(GameTime gameTime) {
            GameTime = gameTime;
            Graphics.Model.SpriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.AnisotropicWrap, DepthStencilState.Default, RasterizerState.CullNone);
            _bloom.Draw(this);
            if (GameState.Model.CurrentGameState == GameStates.Playing) {
                if (GameState.Model.LastGameState != GameState.Model.CurrentGameState) {
                    GraphicsDevice.Clear(Color.Black);
                }
                Graphics.Draw(this);
                Objects.Draw(this);
                Hud.Draw(this);
            } else if (GameState.Model.CurrentGameState == GameStates.Menu) {
                if (GameState.Model.LastGameState != GameState.Model.CurrentGameState) {
                    GraphicsDevice.Clear(Color.Black);
                }
                GameMenu.Draw(this);
                Graphics.Draw(this);
            } else {
                GraphicsDevice.Clear(Color.Black);
                //} else if (GameState.CurrentGameState == GameStates.Docked) {
            }
            DebugText.Draw(this);
            Graphics.Model.SpriteBatch.End();
            base.Draw(gameTime);
        }
        #endregion
    }
}