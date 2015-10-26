// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using System;
namespace Roguelancer.Objects {
    /// <summary>
    /// Hud Object
    /// </summary>
    public class HudObject : IGame {
        #region "private variables"
        /// <summary>
        /// Player Ship
        /// </summary>
        private Ship _playerShip;
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
        /// Screen Rectangle
        /// </summary>
        private Rectangle _screenRectangle;
        /// <summary>
        /// Model
        /// </summary>
        private HudModel _model;
        #endregion
        #region "private const"
        /// <summary>
        /// Font Increment
        /// </summary>
        private const int _fontIncrement = 30;
        /// <summary>
        /// Division Distance Value
        /// </summary>
        private const int _divisionDistanceValue = 300;
        /// <summary>
        /// Max Sensor Objects
        /// </summary>
        private const int _maxSensorObjects = 5;
        /// <summary>
        /// Max Distance
        /// </summary>
        private const int _maxDistance = 650;
        /// <summary>
        /// Update order Interval
        /// </summary>
        private const int _updateOrderInterval = 30;
        /// <summary>
        /// Text Left
        /// </summary>
        private const int _textLeft = 200;
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
                _font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.FontSmall);
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
                var text = "";
                var n = 0;
                var d = (double)0;
                var shipId = 0;
                _updateOrderInt++;
                if (_updateOrderInt > _updateOrderInterval) {
                    if (_playerShip == null) { _playerShip = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault(); }
                    game.DebugText.SetText(game, "X: " +  Convert.ToInt32(_playerShip.Model.Position.X).ToString() + ", Y: " + Convert.ToInt32(_playerShip.Model.Position.Y).ToString() + ", Z: " + Convert.ToInt32(_playerShip.Model.Position.Z).ToString() + "DX: " + Convert.ToInt32(_playerShip.Model.Direction.X).ToString() + "DY: " + Convert.ToInt32(_playerShip.Model.Direction.Y).ToString() + "DZ: " + Convert.ToInt32(_playerShip.Model.Direction.Z).ToString(), false);
                    _screenRectangle = new Rectangle(_imageLeft, _imageTop, _imageWidth, _imageHeight);
                    if (((game.Objects.Ships.Ships.Count + game.Objects.Stations.Stations.Count) - 1) != _model.SensorObjects.Count) {
                        _model.SensorObjects = new System.Collections.Generic.List<HudSensorObject>();
                        foreach (var ship in game.Objects.Ships.Ships) {
                            shipId++;
                            d = Vector3.Distance(_playerShip.Model.Position, ship.Model.Position) / _divisionDistanceValue;
                            text = "Ship " + shipId.ToString();
                            if (d != 0f) {
                                _model.SensorObjects.Add(new HudSensorObject() {
                                    Obj = ship,
                                    Text = text,
                                    FontPosition = new Vector2(_textLeft, _imageTop + n),
                                    Distance = d,
                                    FontOrigin = _font.MeasureString(text) / 2
                                });
                                n = n + _fontIncrement;
                            }
                        }
                        foreach (var station in game.Objects.Stations.Stations) {
                            d = (double)Vector3.Distance(_playerShip.Model.Position, station.Model.Position) / _divisionDistanceValue;
                            text = station.Model.WorldObject.Description;
                            _model.SensorObjects.Add(new HudSensorObject() {
                                Obj = station,
                                Text = text,
                                FontPosition = new Vector2(_textLeft, _imageTop + n),
                                Distance = d,
                                FontOrigin = _font.MeasureString(text) / 2
                            });
                            n = n + _fontIncrement;
                        }
                        _updateOrderInt = _updateOrderInterval - 1;
                    } else {
                        foreach (var so in _model.SensorObjects) {
                            so.Distance = (double)Vector3.Distance(_playerShip.Model.Position, so.Obj.Model.Position) / _divisionDistanceValue;
                            so.FontOrigin = _font.MeasureString(so.Text) / 2;
                            if (so.Distance < _maxDistance) {
                                so.FontPosition = new Vector2(_textLeft, _imageTop + n);
                                n = n + _fontIncrement;
                            }
                        }
                        _model.SensorObjects = _model.SensorObjects.OrderBy(s => s.Distance).ToList();
                    }
                    _updateOrderInt = 0;
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
                //game.Graphics.SpriteBatch.Draw(_sensor, _screenRectangle, Color.White);
                var n = 0;
                foreach (var sensorObject in _model.SensorObjects) {
                    if (n < _maxSensorObjects) {
                        var fontOrigin = _font.MeasureString(sensorObject.Text) / 2;
                        if (sensorObject.Distance != (double)0) {
                            if (sensorObject.Distance < _maxDistance) {
                                game.Graphics.SpriteBatch.DrawString(_font, sensorObject.Text + ": " + sensorObject.Distance.ToString("#.##"), sensorObject.FontPosition, Color.LightBlue, 0, sensorObject.FontOrigin, 2.0f, SpriteEffects.None, 0.5f);
                            }
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