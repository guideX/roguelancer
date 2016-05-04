// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    /// <summary>
    /// Menu Button
    /// </summary>
    public class MenuButton : IGame {
        #region "public variables"
        /// <summary>
        /// Y Offset
        /// </summary>
        public int YOffset { get; set; }
        /// <summary>
        /// Enabled
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// Texture
        /// </summary>
        public Texture2D Texture { get; set; }
        /// <summary>
        /// Sort Id
        /// </summary>
        public int SortId { get; set; }
        /// <summary>
        /// Clicked
        /// </summary>
        public bool Clicked { get; set; }
        /// <summary>
        /// Down
        /// </summary>
        public bool Down { get; set; }
        /// <summary>
        /// Position
        /// </summary>
        public Vector2 Position { get; set; }
        /// <summary>
        /// Text Position
        /// </summary>
        public Vector2 TextPosition;
        #endregion
        #region "private variables"
        /// <summary>
        /// Text
        /// </summary>
        private string _text;
        /// <summary>
        /// Rectangle
        /// </summary>
        private Rectangle _rectangle;
        /// <summary>
        /// Text Rectangle
        /// </summary>
        private Rectangle _textRectangle;
        /// <summary>
        /// Color
        /// </summary>
        private Color _color;
        /// <summary>
        /// Size
        /// </summary>
        private Vector2 _size;
        /// <summary>
        /// Font
        /// </summary>
        private SpriteFont Font;
        #endregion
        #region "public functions"
        /// <summary>
        /// Menu Button
        /// </summary>
        /// <param name="game"></param>
        /// <param name="text"></param>
        /// <param name="texturePath"></param>
        public MenuButton(RoguelancerGame game, string text, string texturePath) {
            _text = text;
            Texture = game.Content.Load<Texture2D>(texturePath);
            Font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.Font);
            _color = new Color(255, 255, 255, 255);
            _size = new Vector2(game.GraphicsDevice.Viewport.Width / 4, game.GraphicsDevice.Viewport.Height / 15);
        }
        public void Initialize(RoguelancerGame game) { } // NEVER CALLED
        public void LoadContent(RoguelancerGame game) { } // NEVER CALLED
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStates.Menu) {
                var mouseRectangle = new Rectangle(game.Input.InputItems.Mouse.State.X, game.Input.InputItems.Mouse.State.Y, 1, 1);
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
                    if (game.Input.InputItems.Mouse.State.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed) {
                        Clicked = true;
                    }
                } else if (_color.A < 255) {
                    _color.A += 5;
                    Clicked = false;
                }
                if (Clicked) {
                    switch (_text) {
                        case "New Game":
                            if (game.GameState.Model.CurrentGameState == GameStates.Menu) {
                                game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
                                game.GameState.Model.CurrentGameState = GameStates.Playing;
                                Clicked = false;
                            }
                            break;
                        case "Load Game":
                            break;
                        case "Multiplayer":
                            break;
                        case "Options":
                            game.GameMenu.Model.CurrentMenu = CurrentMenu.OptionsMenu;
                            game.GameState.Model.CurrentGameState = GameStates.Menu;
                            Clicked = false;
                            break;
                        case "Return":
                            if (game.GameMenu.Model.CurrentMenu == CurrentMenu.OptionsMenu) {
                                game.GameMenu.Model.CurrentMenu = CurrentMenu.HomeMenu;
                                game.GameState.Model.CurrentGameState = GameStates.Menu;
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
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (Texture != null) {
                var f = Font.MeasureString(_text);
                game.Graphics.Model.SpriteBatch.DrawString(Font, _text, TextPosition, Color.Red, 0, f, 3.0f, SpriteEffects.None, 0.5f);
                game.Graphics.Model.SpriteBatch.Draw(Texture, _rectangle, _color);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
        }
        #endregion
    }
}