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
        private const int _maxSensorObjects = 5;
        /// <summary>
        /// Player Ship
        /// </summary>
        private Ship _playerShip;
        /// <summary>
        /// Max Distance
        /// </summary>
        private int _maxDistance = 500;
        /// <summary>
        /// Update order Interval
        /// </summary>
        private const int _updateOrderInterval = 60;
        /// <summary>
        /// Update Order Int
        /// </summary>
        private int _updateOrderInt;
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
        /// <summary>
        /// Model
        /// </summary>
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
                if ((game.Objects.Ships.Ships.Count + game.Objects.Stations.Stations.Count) != _model.SensorObjects.Count) {
                    _model.SensorObjects = new System.Collections.Generic.List<HudSensorObject>();
                    foreach(var ship in game.Objects.Ships.Ships) {
                        _model.SensorObjects.Add(new HudSensorObject() {
                            Obj = ship,
                            Text = "Ship",
                            FontPosition = new Vector2(_imageLeft, _imageTop + n),
                            Distance = 0f
                        });
                        n = n + 30;
                    }
                    foreach (var station in game.Objects.Stations.Stations) {
                        _model.SensorObjects.Add(new HudSensorObject() {
                            Obj = station,
                            Text = "Station",
                            FontPosition = new Vector2(_imageLeft, _imageTop + n),
                            Distance = 0f
                        });
                        n = n + 30;
                    }
                    _updateOrderInt = _updateOrderInterval - 1;
                } else {
                    foreach (var so in _model.SensorObjects) {
                        so.Distance = (double)Vector3.Distance(_playerShip.Model.Position, so.Obj.Model.Position) / 300;
                        if (so.Distance < _maxDistance) {
                            so.FontPosition = new Vector2(_imageLeft, _imageTop + n);
                            n = n + 30;
                        }
                    }
                    _updateOrderInt++;
                    if (_updateOrderInt > _updateOrderInterval) {
                        _model.SensorObjects = _model.SensorObjects.OrderBy(s => s.Distance).ToList();
                        _updateOrderInt = 0;
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
                game.Graphics.SpriteBatch.Draw(_sensor, _screenRectangle, Color.White);
                var n = 0;
                foreach (var sensorObject in _model.SensorObjects) {
                    if (n < _maxSensorObjects) {
                        var fontOrigin = _font.MeasureString(sensorObject.Text) / 2;
                        if (sensorObject.Distance < _maxDistance) {
                            game.Graphics.SpriteBatch.DrawString(_font, sensorObject.Text + ": " + sensorObject.Distance.ToString("#.##"), sensorObject.FontPosition, Color.Blue, 0, fontOrigin, 3.0f, SpriteEffects.None, 0.5f);
                        }
                    }
                    n++;
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
}