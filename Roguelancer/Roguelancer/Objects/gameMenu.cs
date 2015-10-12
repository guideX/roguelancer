using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    /// <summary>
    /// Game Menu
    /// </summary>
    public class GameMenu : IGame {
        /// <summary>
        /// Current Menu
        /// </summary>
        public CurrentMenu CurrentMenu;
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
        /// <summary>
        /// Menu Buttons
        /// </summary>
        public List<MenuButton> MenuButtons;
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameMenu() {
            try {
                MenuButtons = new List<MenuButton>(); // Create new List of MenuButton's
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
                CurrentMenu = CurrentMenu.HomeMenu; // Set Current Menu to Home
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
                backgroundTexture = game.Content.Load<Texture2D>(game.settings.menuBackgroundTexture);
                screenWidth = game.graphics.graphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
                screenHeight = game.graphics.graphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferHeight;
                CurrentMenu = CurrentMenu.HomeMenu;
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
                                YOffset  = 14
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
                screenRectangle = new Rectangle(0, 0, screenWidth, screenHeight);
                game.graphics.SpriteBatch.Draw(backgroundTexture, screenRectangle, Color.White);
                foreach (var button in MenuButtons) {
                    button.Draw(game);
                }
            } catch {
                throw;
            }
        }
    }
}