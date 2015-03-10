using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
namespace Roguelancer.Objects {
    public class MenuButton : IGame {
        public bool Enabled;
        public Texture2D Texture;
        public int SortId;
        public bool Clicked;
        public bool Down;
        public string Text;
        public Vector2 Position;
        public Vector2 TextPosition;
        private Rectangle _rectangle;
        private Color _color;
        private Vector2 _size;
        private SpriteFont Font;
        private SpriteBatch SpriteBatch;
        public MenuButton(RoguelancerGame game) {
            try {
                Font = game.Content.Load<SpriteFont>("FONTS\\" + game.settings.font);
                Texture = game.Content.Load<Texture2D>("BUTTONS\\menubutton");
                _color = new Color(255, 255, 255, 255);
                _size = new Vector2(game.graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Width / 4, game.GraphicsDevice.Viewport.Height / 15);
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Initialize(RoguelancerGame game) { } // NEVER CALLED
        public void LoadContent(RoguelancerGame game) { } // NEVER CALLED
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                var mouseRectangle = new Rectangle(game.input.lInputItems.mouse.State.X, game.input.lInputItems.mouse.State.Y, 1, 1);
                SpriteBatch = game.graphics.lSpriteBatch;
                _rectangle = new Rectangle((int)Position.X, (int)Position.Y, (int)_size.X, (int)_size.Y);
                mouseRectangle.Y = mouseRectangle.Y + 90;
                if (mouseRectangle.Intersects(_rectangle)) {
                    if (_color.A == 255) {
                        Down = false;
                    }
                    if (_color.A == 0) {
                        Down = true;
                    }
                    if (Down) {
                        _color.A += 5;
                    } else {
                        _color.A -= 5;
                    }
                    if (game.input.lInputItems.mouse.State.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed) {
                        Clicked = true;
                        //game.gameMenu.MenuButton_Click(Text, game);
                    }
                } else if (_color.A < 255) {
                    _color.A += 5;
                    Clicked = false;
                }
                if (Clicked) {
                    switch (Text) {
                        case "New Game":
                            game.debugText.text = "New Game Clicked";
                            if (game.gameState.currentGameState == GameState.GameStates.menu) {
                                game.gameState.lastGameState = game.gameState.currentGameState;
                                game.gameState.currentGameState = GameState.GameStates.playing;
                                game.debugText.text = "";
                            }
                            break;
                        case "Options":
                            game.gameMenu.CurrentMenu = CurrentMenu.OptionsMenu;
                            game.debugText.text = "Options Clicked";
                            break;
                        case "Return":
                            game.debugText.text = "Return Clicked";
                            if (game.gameMenu.CurrentMenu == CurrentMenu.OptionsMenu) {
                                game.gameMenu.CurrentMenu = CurrentMenu.HomeMenu;
                            }
                            break;
                    }
                }
            } catch (Exception ex) {
                throw ex;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                var f = Font.MeasureString(Text);
                SpriteBatch.Draw(Texture, _rectangle, _color);
                SpriteBatch.DrawString(Font, Text, TextPosition, Color.Black, 0, f, 3.0f, SpriteEffects.None, 0.5f);
            } catch (Exception ex) {
                throw ex;
            }
        }
    }
}