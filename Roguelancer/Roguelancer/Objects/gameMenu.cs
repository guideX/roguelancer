using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework;
namespace Roguelancer.Objects {
    public enum HomeMenu {
        Nothing = 0,
        NewGame = 1,
        LoadGame = 2,
        Multiplayer = 3,
        Options = 4,
        Exit = 5
    }
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
                        var newGame = new MenuButton(game);
                        var options = new MenuButton(game);
                        newGame.SortId = 1;
                        newGame.Position = new Vector2(50, 200);
                        newGame.Text = "New Game";
                        newGame.TextPosition = newGame.Position;
                        options.SortId = 2;
                        options.Position = new Vector2(50, 290);
                        options.TextPosition = options.Position;
                        options.Text = "Options";
                        MenuButtons.Add(newGame);
                        MenuButtons.Add(options);
                        break;
                    case CurrentMenu.OptionsMenu:
                        var ret = new MenuButton(game);
                        ret.SortId = 1;
                        ret.Position = new Vector2(50, 200);
                        ret.Text = "Return";
                        ret.TextPosition = ret.Position;
                        MenuButtons.Add(ret);
                        break;
                }
            }
            foreach (var button in MenuButtons) {
                button.Update(game);
            }
        }
        public void Draw(RoguelancerGame game) {
            screenRectangle = new Rectangle(0, 0, screenWidth, screenHeight);
            game.graphics.lSpriteBatch.Draw(backgroundTexture, screenRectangle, Color.White);
            foreach (var button in MenuButtons) {
                button.Draw(game);
            }
        }
    }
}