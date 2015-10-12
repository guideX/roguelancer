using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    public class MenuButton : IGame {
        //public bool AutoDown;
        //public int HeightIntegral;
        public int YOffset;
        public bool Enabled;
        public Texture2D Texture;
        public int SortId;
        public bool Clicked;
        public bool Down;
        private string Text;
        public Vector2 Position;
        public Vector2 TextPosition;
        private Rectangle _rectangle;
        private Rectangle _textRectangle;
        private Color _color;
        private Vector2 _size;
        private SpriteFont Font;
        public MenuButton(RoguelancerGame game, string text, string texturePath) {
            try {
                Text = text;
                Texture = game.Content.Load<Texture2D>(texturePath);
                Font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.font);
                _color = new Color(255, 255, 255, 255);
                _size = new Vector2(game.Graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Width / 4, game.GraphicsDevice.Viewport.Height / 15);
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
                if (game.GameState.currentGameState == GameState.GameStates.menu) {
                    var mouseRectangle = new Rectangle(game.Input.lInputItems.mouse.State.X, game.Input.lInputItems.mouse.State.Y, 1, 1);
                    _rectangle = new Rectangle((int)Position.X, (int)Position.Y, (int)_size.X, (int)_size.Y);
                    _textRectangle = new Rectangle((int)TextPosition.X, (int)TextPosition.Y, (int)_size.X - 80, (int)_size.Y - YOffset);
                    if (mouseRectangle.Intersects(_textRectangle)) {
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
                        if (game.Input.lInputItems.mouse.State.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed) {
                            Clicked = true;
                        }
                    } else if (_color.A < 255) {
                        _color.A += 5;
                        Clicked = false;
                    }
                    if (Clicked) {
                        switch (Text) {
                            case "New Game":
                                game.DebugText.Text = "Play Clicked";
                                if (game.GameState.currentGameState == GameState.GameStates.menu) {
                                    game.GameState.lastGameState = game.GameState.currentGameState;
                                    game.GameState.currentGameState = GameState.GameStates.playing;
                                    game.DebugText.Text = "";
                                    Clicked = false;
                                }
                                break;
                            case "Load Game":
                                game.DebugText.Text = "Load Game";
                                break;
                            case "Multiplayer":
                                game.DebugText.Text = "Multiplayer";
                                break;
                            case "Options":
                                game.GameMenu.CurrentMenu = CurrentMenu.OptionsMenu;
                                game.DebugText.Text = "Options Clicked";
                                game.GameState.currentGameState = GameState.GameStates.menu;
                                Clicked = false;
                                break;
                            case "Return":
                                game.DebugText.Text = "Return Clicked";
                                if (game.GameMenu.CurrentMenu == CurrentMenu.OptionsMenu) {
                                    game.GameMenu.CurrentMenu = CurrentMenu.HomeMenu;
                                    game.GameState.currentGameState = GameState.GameStates.menu;
                                    Clicked = false;
                                }
                                break;
                            case "Exit":
                                game.Exit();
                                Clicked = false;
                                break;
                        }
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
                if (Texture != null) {
                    var f = Font.MeasureString(Text);
                    game.Graphics.SpriteBatch.DrawString(Font, Text, TextPosition, Color.Red, 0, f, 3.0f, SpriteEffects.None, 0.5f);
                    game.Graphics.SpriteBatch.Draw(Texture, _rectangle, _color);
                }
            } catch (Exception ex) {
                throw ex;
            }
        }
    }
}