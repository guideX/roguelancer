// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    /// <summary>
    /// Game Menu
    /// </summary>
    public class GameMenu : IGameMenu {
        #region "public variables"
        /// <summary>
        /// Model
        /// </summary>
        public GameMenuModel Model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameMenu(RoguelancerGame game) {
            Model = new GameMenuModel();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.CurrentMenu = CurrentMenu.HomeMenu; // Set Current Menu to Home
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.BackgroundTexture = game.Content.Load<Texture2D>(game.Settings.Model.MenuBackgroundTexture);
            Model.ScreenWidth = game.GraphicsDevice.PresentationParameters.BackBufferWidth;
            Model.ScreenHeight = game.GraphicsDevice.PresentationParameters.BackBufferHeight;
            Model.CurrentMenu = CurrentMenu.HomeMenu;
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (Model.CurrentMenu != Model.LastMenu) {
                Model.LastMenu = Model.CurrentMenu;
                Model.MenuButtons = new List<MenuButton>();
                switch (Model.CurrentMenu) {
                    case CurrentMenu.HomeMenu:
                        Model.MenuButtons.Add(new MenuButton(game, "New Game", "BUTTONS\\newgame") {
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true,
                            YOffset = 14
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Load Game", "BUTTONS\\loadgame") {
                            SortId = 2,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true,
                            YOffset = 16
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Multiplayer", "BUTTONS\\multiplayer") {
                            SortId = 3,
                            Position = new Vector2(50, 380),
                            TextPosition = new Vector2(50, 290),
                            Enabled = true,
                            YOffset = 30
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Options", "BUTTONS\\options") {
                            SortId = 4,
                            Position = new Vector2(50, 470),
                            TextPosition = new Vector2(50, 350),
                            Enabled = true,
                            YOffset = 26
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Exit", "BUTTONS\\exit") {
                            SortId = 5,
                            Position = new Vector2(50, 560),
                            TextPosition = new Vector2(50, 420),
                            Enabled = true,
                            YOffset = 28
                        });
                        break;
                    case CurrentMenu.OptionsMenu:
                        Model.MenuButtons.Add(new MenuButton(game, "Keyboard Controls", "BUTTONS\\keyboard") {
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true,
                            YOffset = 14
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Return", "BUTTONS\\return") {
                            SortId = 1,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true,
                            YOffset = 16
                        });
                        break;
                }
            }
            foreach (var button in Model.MenuButtons) {
                button.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            Model.ScreenRectangle = new Rectangle(0, 0, Model.ScreenWidth, Model.ScreenHeight);
            game.Graphics.Model.SpriteBatch.Draw(Model.BackgroundTexture, Model.ScreenRectangle, Color.White);
            foreach (var button in Model.MenuButtons) {
                button.Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Model.MenuButtons = null;
            Model.CurrentMenu = CurrentMenu.NotInitialized;
            Model.LastMenu = CurrentMenu.NotInitialized;
            Model.BackgroundTexture.Dispose();
            Model.BackgroundTexture = null;
            Model.ScreenHeight = 0;
            Model.ScreenWidth = 0;
        }
        #endregion
    }
}