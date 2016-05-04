// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
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
    public class HudObject : IHudObject {
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
        #region "public const"
        public const int DockDistanceAccept = 20;
        /// <summary>
        /// Division Distance Value
        /// </summary>
        public const int DivisionDistanceValue = 2000;
        #endregion
        #region "private const"
        /// <summary>
        /// Font Increment
        /// </summary>
        private const int _fontIncrement = 30;
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
            _model = new HudModel();
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            _playerShip = game.Objects.Model.Ships.GetPlayerShip(game); // Get Player Ship
            _font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.FontSmall);
            _sensor = game.Content.Load<Texture2D>(game.Settings.SensorTexture);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            var text = "";
            var n = 0;
            var d = (double)0;
            var shipId = 0;
            _updateOrderInt++;
            if (_updateOrderInt > _updateOrderInterval) {
                if (_playerShip == null) { _playerShip = game.Objects.Model.Ships.GetPlayerShip(game); }
                _screenRectangle = new Rectangle(_imageLeft, _imageTop, _imageWidth, _imageHeight);
                if (((game.Objects.Model.Ships.Model.Ships.Count + game.Objects.Model.Stations.Stations.Count) - 1) != _model.SensorObjects.Count) {
                    _model.SensorObjects = new System.Collections.Generic.List<HudSensorObject>();
                    foreach (var ship in game.Objects.Model.Ships.Model.Ships) {
                        shipId++;
                        d = Vector3.Distance(_playerShip.Model.Position, ship.Model.Position) / DivisionDistanceValue;
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
                    foreach (var station in game.Objects.Model.Stations.Stations) {
                        d = (double)Vector3.Distance(_playerShip.Model.Position, station.Model.Position) / DivisionDistanceValue;
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
                        so.Distance = (double)Vector3.Distance(_playerShip.Model.Position, so.Obj.Model.Position) / DivisionDistanceValue;
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
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            //game.Graphics.SpriteBatch.Draw(_sensor, _screenRectangle, Color.White);
            var n = 0;
            foreach (var sensorObject in _model.SensorObjects) {
                if (n < _maxSensorObjects) {
                    var fontOrigin = _font.MeasureString(sensorObject.Text) / 2;
                    if (sensorObject.Distance != (double)0) {
                        if (sensorObject.Distance < _maxDistance) {
                            game.Graphics.Model.SpriteBatch.DrawString(_font, sensorObject.Text + ": " + sensorObject.Distance.ToString("#.##"), sensorObject.FontPosition, Color.LightBlue, 0, sensorObject.FontOrigin, 2.0f, SpriteEffects.None, 0.5f);
                        }
                    }
                }
                n++;
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
        }
        #endregion
    }
}