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
        /// Current Menu
        /// </summary>
        public CurrentMenu CurrentMenu { get; set; }
        /// <summary>
        /// Menu Buttons
        /// </summary>
        public List<MenuButton> MenuButtons { get; set; }
        /// <summary>
        /// Last Menu
        /// </summary>
        public CurrentMenu LastMenu { get; set; }
        /// <summary>
        /// Background Texture
        /// </summary>
        public Texture2D BackgroundTexture { get; set; }
        /// <summary>
        /// Screen Height
        /// </summary>
        public int ScreenHeight { get; set; }
        /// <summary>
        /// Screen Width
        /// </summary>
        public int ScreenWidth { get; set; }
        /// <summary>
        /// Screen Rectangle
        /// </summary>
        public Rectangle ScreenRectangle { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameMenuObject(RoguelancerGame game) {
            
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
            BackgroundTexture = game.Content.Load<Texture2D>(game.Settings.Model.MenuBackgroundTexture);
            ScreenWidth = game.GraphicsDevice.PresentationParameters.BackBufferWidth;
            ScreenHeight = game.GraphicsDevice.PresentationParameters.BackBufferHeight;
            CurrentMenu = CurrentMenu.HomeMenu;
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (CurrentMenu != LastMenu) {
                LastMenu = CurrentMenu;
                MenuButtons = new List<MenuButton>();
                switch (CurrentMenu) {
                    case CurrentMenu.HomeMenu:
                        MenuButtons.Add(new MenuButton(game, "New Game") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\newgame"),
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true
                        });
                        MenuButtons.Add(new MenuButton(game, "Load Game") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\loadgame"),
                            SortId = 2,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true
                        });
                        MenuButtons.Add(new MenuButton(game, "Multiplayer") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\multiplayer"),
                            SortId = 3,
                            Position = new Vector2(50, 380),
                            TextPosition = new Vector2(50, 290),
                            Enabled = true
                        });
                        MenuButtons.Add(new MenuButton(game, "Options") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\options"),
                            SortId = 4,
                            Position = new Vector2(50, 470),
                            TextPosition = new Vector2(50, 350),
                            Enabled = true
                        });
                        MenuButtons.Add(new MenuButton(game, "Exit") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\exit"),
                            SortId = 5,
                            Position = new Vector2(50, 560),
                            TextPosition = new Vector2(50, 420),
                            Enabled = true
                        });
                        break;
                    case CurrentMenu.OptionsMenu:
                        MenuButtons.Add(new MenuButton(game, "Keyboard Controls") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\keyboard"),
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 140),
                            Enabled = true
                        });
                        MenuButtons.Add(new MenuButton(game, "Return") {
                            Texture = game.Content.Load<Texture2D>("BUTTONS\\return"),
                            SortId = 1,
                            Position = new Vector2(50, 290),
                            TextPosition = new Vector2(50, 205),
                            Enabled = true
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
            ScreenRectangle = new Rectangle(0, 0, ScreenWidth, ScreenHeight);
            game.Graphics.Model.SpriteBatch.Draw(BackgroundTexture, ScreenRectangle, Color.White);
            foreach (var button in MenuButtons) {
                button.Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            MenuButtons = null;
            CurrentMenu = CurrentMenu.NotInitialized;
            LastMenu = CurrentMenu.NotInitialized;
            BackgroundTexture.Dispose();
            BackgroundTexture = null;
            ScreenHeight = 0;
            ScreenWidth = 0;
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