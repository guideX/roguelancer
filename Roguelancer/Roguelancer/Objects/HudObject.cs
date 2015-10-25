using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    /// <summary>
    /// Hud Object
    /// </summary>
    public class HudObject : IGame {
        #region "private variables"
        private Ship _playerShip;
        /// <summary>
        /// Font
        /// </summary>
        private SpriteFont _font;
        /// <summary>
        /// Sensor
        /// </summary>
        private Texture2D _sensor;
        /// <summary>
        /// Image Left
        /// </summary>
        private const int _imageLeft = 40;
        /// <summary>
        /// Image Top
        /// </summary>
        private const int _imageTop = 600;
        /// <summary>
        /// Screen Width
        /// </summary>
        private const int _imageWidth = 253;
        /// <summary>
        /// Screen Height
        /// </summary>
        private const int _imageHeight = 244;
        /// <summary>
        /// Screen Rectangle
        /// </summary>
        private Rectangle _screenRectangle;
        private HudModel _model;
        #endregion
        #region "public functions"
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                _model = new HudModel();
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
                _playerShip = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                _font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.Font);
                _sensor = game.Content.Load<Texture2D>(game.Settings.SensorTexture);
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
                var n = 0;
                if(_playerShip == null) { _playerShip = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault(); }
                _screenRectangle = new Rectangle(_imageLeft, _imageTop, _imageWidth, _imageHeight);
                if (game.Objects.Ships.Ships.Count != _model.Ships.Count) {
                    _model.Ships = new System.Collections.Generic.List<HudObjectShip>();
                    foreach(var ship in game.Objects.Ships.Ships) {
                        _model.Ships.Add(new HudObjectShip() {
                            Ship = ship,
                            Common = new HudObjectCommon() {
                                Text = "Ship",
                                FontPosition = new Vector2(_imageLeft, _imageTop + n),
                                Distance = 0f
                            }
                        });
                        n = n + 30;
                    }
                } else {
                    foreach (var ship in _model.Ships) {
                        ship.Common.Distance = (double)Vector3.Distance(_playerShip.model.Position, ship.Ship.model.Position) / 300;
                    }
                }
                /*
                if (game.Objects.Stations.Stations.Count != _model.Stations.Count) {
                    _model.Stations = new System.Collections.Generic.List<HudObjectStation>();
                    foreach (var station in game.Objects.Stations.Stations) {
                        _model.Stations.Add(new HudObjectStation() {
                            Station = station,
                            Common = new HudObjectCommon() {
                                Text = "Station",
                                FontPosition = new Vector2(_imageLeft, _imageTop + n)
                            }
                        });
                        n = n + 30;
                    }
                }
                */
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
                //game.Graphics.SpriteBatch.Draw(_sensor, _screenRectangle, Color.White);
                foreach(var ship in _model.Ships) {
                    var fontOrigin = _font.MeasureString(ship.Common.Text) / 2;
                    //game.Graphics.SpriteBatch.DrawString(_font, ship.Common.Text + ": " + ship.Common.Distance.ToString() + " ... " + ship.Ship.model.Position.X.ToString() + " - " + ship.Ship.model.Position.Y.ToString() + " - " + ship.Ship.model.Position.Z.ToString(), ship.Common.FontPosition, Color.White, 0, fontOrigin, 3.0f, SpriteEffects.None, 0.5f);
                    game.Graphics.SpriteBatch.DrawString(_font, ship.Common.Text + ": " + ship.Common.Distance.ToString("#.##"), ship.Common.FontPosition, Color.Green, 0, fontOrigin, 3.0f, SpriteEffects.None, 0.5f);
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
}