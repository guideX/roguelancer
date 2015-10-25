// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Debug Text
    /// </summary>
    public class DebugText : IDebugText {
        #region "public variables"
        private bool _timerEnabled = false;
        /// <summary>
        /// Show Enabled
        /// </summary>
        private bool _showEnabled = false;
        /// <summary>
        /// Show Time
        /// </summary>
        private const int _showTime = 100;
        /// <summary>
        /// Current Show Time
        /// </summary>
        private int _currentShowTime { get; set; }
        /// <summary>
        /// Text
        /// </summary>
        private string _text;
        /// <summary>
        /// Get Text
        /// </summary>
        /// <returns></returns>
        public string GetText() {
            try {
                return _text;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Set Text
        /// </summary>
        /// <param name="game"></param>
        /// <param name="value"></param>
        public void SetText(RoguelancerGame game, string value, bool timerEnabled) {
            try {
                _timerEnabled = timerEnabled;
                if (value != null) {
                    _currentShowTime = 0;
                    _showEnabled = true;
                    _text = value;
                }
            } catch {
                throw;
            }
        }
        #endregion
        #region "private variables"
        /// <summary>
        /// Font
        /// </summary>
        private SpriteFont _font;
        /// <summary>
        /// Font Position
        /// </summary>
        private Vector2 _fontPosition;
        #endregion
        #region "public functions"
        /// <summary>
        /// Debug Text
        /// </summary>
        public DebugText() {
            try {
                _text = "";
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {}
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                _font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.Font);
                _fontPosition = new Vector2(game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Width / 2, game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Height / 2);
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
                if (_showEnabled) {
                    if (_timerEnabled) {
                        _currentShowTime++;
                        if (_currentShowTime > _showTime) {
                            _showEnabled = false;
                            _currentShowTime = 0;
                            _text = "";
                        }
                    }

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
                if(_showEnabled) {
                    var fontOrigin = _font.MeasureString(_text) / 2;
                    game.Graphics.SpriteBatch.DrawString(_font, _text, _fontPosition, Color.White, 0, fontOrigin, 3.0f, SpriteEffects.None, 0.5f);
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
}