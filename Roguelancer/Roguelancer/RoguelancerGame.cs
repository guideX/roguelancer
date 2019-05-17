using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Actions;
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
        /// Hud
        /// </summary>
        public IHudObject Hud { get; set; }
        /// <summary>
        /// Graphics
        /// </summary>
        public IGameGraphics Graphics { get; set; }
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
        /// In Game Actions
        /// </summary>
        public InGameActions InGameActions { get; set; }
        /// <summary>
        /// Menu Actions
        /// </summary>
        public MenuActions MenuActions { get; set; }
        /// <summary>
        /// Bloom
        /// </summary>
        //public BloomHandler Bloom;
        /// <summary>
        /// Game Time
        /// </summary>
        public GameTime GameTime { get; set; }
        /// <summary>
        /// Star System
        /// </summary>
        public int CurrentStarSystemId { get; set; } = 1;
        /// <summary>
        /// Settings
        /// </summary>
        public IGameSettings Settings { get; set; } = new GameSettings();
        /// <summary>
        /// Camera
        /// </summary>
        public IGameCamera Camera { get; set; } = new GameCamera();
        /// <summary>
        /// Input
        /// </summary>
        public IInput Input { get; set; } = new Input();
        /// <summary>
        /// Debug Text
        /// </summary>
        public IDebugText DebugText { get; set; } = new DebugText();
        /// <summary>
        /// Game State
        /// </summary>
        public IGameStates GameState { get; set; } = new GameState();
        #endregion
        #region "public methods"
        public RoguelancerGame() {
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Graphics = new GameGraphics(this);
            //Bloom = new BloomHandler(this);
            Objects = new GameObjects(this);
            GameMenu = new GameMenuObject(this);
            Hud = new HudObject(this);
            InGameActions = new InGameActions(this);
            MenuActions = new MenuActions(this);
            GameMenu.CurrentMenu = CurrentMenu.HomeMenu;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        protected override void Initialize() {
            Camera.Initialize(this);
            //Bloom.Initialize(this);
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
            //Input.LoadContent(this);
            //Bloom.LoadContent(this);
            base.LoadContent();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime) {
            GameTime = gameTime;
            Input.Update(this);
            //Bloom.Update(this);
            Objects.Update(this);
            Camera.Update(this);
            DebugText.Update(this);
            GameMenu.Update(this);
            if (GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
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
            //Bloom.Draw(this);
            if (GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                if (GameState.Model.LastGameState != GameState.Model.CurrentGameState) {
                    GraphicsDevice.Clear(Color.Black);
                }
                Graphics.Draw(this);
                Objects.Draw(this);
                Hud.Draw(this);
            } else if (GameState.Model.CurrentGameState == GameStatesEnum.Menu) {
                if (GameState.Model.LastGameState != GameState.Model.CurrentGameState) {
                    GraphicsDevice.Clear(Color.Black);
                }
                GameMenu.Draw(this);
                Graphics.Draw(this);
            } else if (GameState.Model.CurrentGameState == GameStatesEnum.Docked) {
                GraphicsDevice.Clear(Color.Black);
                Objects.Draw(this);
            } else {
                GraphicsDevice.Clear(Color.Black);
            }
            DebugText.Draw(this);
            Graphics.Model.SpriteBatch.End();
            base.Draw(gameTime);
        }
        #endregion
    }
}