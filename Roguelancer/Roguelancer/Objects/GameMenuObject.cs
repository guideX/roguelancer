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
    public class GameMenuObject : IGameMenu {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public GameMenuModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameMenuObject() {
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
                        Model.MenuButtons.Add(new MenuButton(game, "New Game") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\newgame"),
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Load Game") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\loadgame"),
                            SortId = 2,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Multiplayer") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\multiplayer"),
                            SortId = 3,
                            Position = new Vector2(50, 380),
                            TextPosition = new Vector2(50, 290),
                            Enabled = true
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Options") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\options"),
                            SortId = 4,
                            Position = new Vector2(50, 470),
                            TextPosition = new Vector2(50, 350),
                            Enabled = true
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Exit") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\exit"),
                            SortId = 5,
                            Position = new Vector2(50, 560),
                            TextPosition = new Vector2(50, 420),
                            Enabled = true
                        });
                        break;
                    case CurrentMenu.OptionsMenu:
                        Model.MenuButtons.Add(new MenuButton(game, "Keyboard Controls") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\keyboard"),
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true
                        });
                        Model.MenuButtons.Add(new MenuButton(game, "Return") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\return"),
                            SortId = 1,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true
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
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
        }
        #endregion
    }
}