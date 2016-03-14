// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    /// <summary>
    /// Game Menu
    /// </summary>
    public class GameMenu : IGameMenu {
        #region "public variables"
        /// <summary>
        /// Current Menu
        /// </summary>
        public CurrentMenu CurrentMenu { get; set; }
        /// <summary>
        /// Menu Buttons
        /// </summary>
        public List<MenuButton> MenuButtons { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Last Menu
        /// </summary>
        private CurrentMenu _lastMenu;
        /// <summary>
        /// Background Texture
        /// </summary>
        private Texture2D backgroundTexture;
        /// <summary>
        /// Screen Height
        /// </summary>
        private int screenHeight;
        /// <summary>
        /// Screen Width
        /// </summary>
        private int screenWidth;
        /// <summary>
        /// Screen Rectangle
        /// </summary>
        private Rectangle screenRectangle;
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameMenu() {
            MenuButtons = new List<MenuButton>(); // Create new List of MenuButton's
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            CurrentMenu = CurrentMenu.HomeMenu; // Set Current Menu to Home
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            backgroundTexture = game.Content.Load<Texture2D>(game.Settings.MenuBackgroundTexture);
            screenWidth = game.Graphics.GraphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
            screenHeight = game.Graphics.GraphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferHeight;
            CurrentMenu = CurrentMenu.HomeMenu;
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (CurrentMenu != _lastMenu) {
                _lastMenu = CurrentMenu;
                MenuButtons = new List<MenuButton>();
                switch (CurrentMenu) {
                    case CurrentMenu.HomeMenu:
                        MenuButtons.Add(new MenuButton(game, "New Game", "BUTTONS\\newgame") {
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true,
                            YOffset = 14
                        });
                        MenuButtons.Add(new MenuButton(game, "Load Game", "BUTTONS\\loadgame") {
                            SortId = 2,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true,
                            YOffset = 16
                        });
                        MenuButtons.Add(new MenuButton(game, "Multiplayer", "BUTTONS\\multiplayer") {
                            SortId = 3,
                            Position = new Vector2(50, 380),
                            TextPosition = new Vector2(50, 290),
                            Enabled = true,
                            YOffset = 30
                        });
                        MenuButtons.Add(new MenuButton(game, "Options", "BUTTONS\\options") {
                            SortId = 4,
                            Position = new Vector2(50, 470),
                            TextPosition = new Vector2(50, 350),
                            Enabled = true,
                            YOffset = 26
                        });
                        MenuButtons.Add(new MenuButton(game, "Exit", "BUTTONS\\exit") {
                            SortId = 5,
                            Position = new Vector2(50, 560),
                            TextPosition = new Vector2(50, 420),
                            Enabled = true,
                            YOffset = 28
                        });
                        break;
                    case CurrentMenu.OptionsMenu:
                        MenuButtons.Add(new MenuButton(game, "Keyboard Controls", "BUTTONS\\keyboard") {
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true,
                            YOffset = 14
                        });
                        MenuButtons.Add(new MenuButton(game, "Return", "BUTTONS\\return") {
                            SortId = 1,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true,
                            YOffset = 16
                        });
                        break;
                }

            }
            foreach (var button in MenuButtons) {
                button.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            screenRectangle = new Rectangle(0, 0, screenWidth, screenHeight);
            game.Graphics.SpriteBatch.Draw(backgroundTexture, screenRectangle, Color.White);
            foreach (var button in MenuButtons) {
                button.Draw(game);
            }
        }
        #endregion
    }
}