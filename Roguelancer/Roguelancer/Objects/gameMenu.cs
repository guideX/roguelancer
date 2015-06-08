using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework;
namespace Roguelancer.Objects {
    /*
    public enum OptionsMenu {
        KeyboardControls = 1,
        ReturnToMainMenu = 2
    }
    public enum HomeMenu {
        NewGame = 1,
        LoadGame = 2,
        Multiplayer = 3,
        Options = 4,
        Exit = 5
    }*/
    public enum CurrentMenu {
        HomeMenu = 1,
        OptionsMenu = 2
    }
    public class GameMenu : IGame {
        public CurrentMenu CurrentMenu;
        private CurrentMenu _lastMenu;
        private Texture2D backgroundTexture;
        private int screenHeight;
        private int screenWidth;
        private Rectangle screenRectangle;
        public List<MenuButton> MenuButtons;
        public GameMenu() {
            MenuButtons = new List<MenuButton>();
        }
        public void Initialize(RoguelancerGame game) {
            CurrentMenu = Objects.CurrentMenu.HomeMenu;
        }
        public void LoadContent(RoguelancerGame game) {
            backgroundTexture = game.Content.Load<Texture2D>(game.settings.menuBackgroundTexture);
            screenWidth = game.graphics.graphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
            screenHeight = game.graphics.graphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferHeight;
            CurrentMenu = Objects.CurrentMenu.HomeMenu;
        }
        public void Update(RoguelancerGame game) {
            if (CurrentMenu != _lastMenu) {
                _lastMenu = CurrentMenu;
                MenuButtons = new List<MenuButton>();
                switch (CurrentMenu) {
                    case Objects.CurrentMenu.HomeMenu:
                        MenuButtons.Add(new MenuButton(game, "Play") { 
                            SortId = 1, 
                            Position = new Vector2(50, 200), 
                            TextPosition = new Vector2(50, 200), 
                            Enabled = true 
                        });
                        MenuButtons.Add(new MenuButton(game, "Options") { 
                            SortId = 2, 
                            Position = new Vector2(50, 290), 
                            TextPosition = new Vector2(50, 290), 
                            Enabled = true 
                        });
                        MenuButtons.Add(new MenuButton(game, "Exit") { 
                            SortId = 3, 
                            Position = new Vector2(50, 380), 
                            TextPosition = new Vector2(50, 380), 
                            Enabled = true 
                        });
                        break;
                    case Objects.CurrentMenu.OptionsMenu:
                        MenuButtons.Add(new MenuButton(game, "Keyboard Controls") {
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 200),
                            Enabled = true
                        });
                        MenuButtons.Add(new MenuButton(game, "Return") {
                            SortId = 1,
                            Position = new Vector2(50, 200),
                            TextPosition = new Vector2(50, 200),
                            Enabled = true
                        });
                        break;
                }
                         
            }
            foreach (var button in MenuButtons) {
                button.Update(game);
            }
        }
        public void Draw(RoguelancerGame game) {
            screenRectangle = new Rectangle(0, 0, screenWidth, screenHeight);
            game.graphics.SpriteBatch.Draw(backgroundTexture, screenRectangle, Color.White);
            foreach (var button in MenuButtons) {
                button.Draw(game);
            }
        }
    }
}