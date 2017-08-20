using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Helpers;
namespace Roguelancer.Objects {
    /// <summary>
    /// Hud Object
    /// </summary>
    public class HudObject : IHudObject {
        #region "public properties"
        /// <summary>
        /// Hud Object Model
        /// </summary>
        public HudObjectModel Model { get; set; }
        #endregion
        #region "public const"
        public const int DockDistanceAccept = 25;
        /// <summary>
        /// Division Distance Value
        /// </summary>
        public const int DivisionDistanceValue = 2000;
        #endregion
        #region "private const"
        /// <summary>
        /// Font Increment
        /// </summary>
        public const int FontIncrement = 30;
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
        public const int TextLeft = 200;
        /// <summary>
        /// Image Left
        /// </summary>
        public const int ImageLeft = 40;
        /// <summary>
        /// Image Top
        /// </summary>
        public const int ImageTop = 600;
        /// <summary>
        /// Screen Width
        /// </summary>
        private const int _imageWidth = 253;
        /// <summary>
        /// Screen Height
        /// </summary>
        private const int _imageHeight = 244;
        #endregion
        #region "public methods"
        /// <summary>
        /// Hud Object
        /// </summary>
        public HudObject(RoguelancerGame game) {
            Model = new HudObjectModel();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.PlayerShip = ShipHelper.GetPlayerShip(game); // Get Player Ship
            Model.Font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.Model.FontSmall);
            Model.Sensor = game.Content.Load<Texture2D>(game.Settings.Model.SensorTexture);
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
            Model.UpdateOrderInt++;
            if (Model.UpdateOrderInt > _updateOrderInterval) {
                if (Model.PlayerShip == null) { Model.PlayerShip = ShipHelper.GetPlayerShip(game); }
                Model.ScreenRectangle = new Rectangle(ImageLeft, ImageTop, _imageWidth, _imageHeight);
                if (((game.Objects.Model.Ships.Model.Ships.Count + game.Objects.Model.Stations.Model.Stations.Count) - 1) != Model.Model.SensorObjects.Count) {
                    Model.Model.SensorObjects = new System.Collections.Generic.List<HudSensorObject>();
                    foreach (var ship in game.Objects.Model.Ships.Model.Ships) {
                        shipId++;
                        d = Vector3.Distance(Model.PlayerShip.Model.Position, ship.Model.Position) / DivisionDistanceValue;
                        text = "Ship " + shipId.ToString();
                        if (d != 0f) {
                            Model.Model.SensorObjects.Add(new HudSensorObject() {
                                Obj = ship,
                                Text = text,
                                FontPosition = new Vector2(TextLeft, ImageTop + n),
                                Distance = d,
                                FontOrigin = Model.Font.MeasureString(text) / 2
                            });
                            n = n + FontIncrement;
                        }
                    }
                    foreach (var station in game.Objects.Model.Stations.Model.Stations) {
                        d = (double)Vector3.Distance(Model.PlayerShip.Model.Position, station.Model.Position) / DivisionDistanceValue;
                        text = station.Model.WorldObject.Description;
                        Model.Model.SensorObjects.Add(new HudSensorObject() {
                            Obj = station,
                            Text = text,
                            FontPosition = new Vector2(TextLeft, ImageTop + n),
                            Distance = d,
                            FontOrigin = Model.Font.MeasureString(text) / 2
                        });
                        n = n + FontIncrement;
                    }
                    foreach (var planet in game.Objects.Model.Planets.Model.Planets) {
                        d = (double)Vector3.Distance(Model.PlayerShip.Model.Position, planet.Model.Position) / DivisionDistanceValue;
                        text = planet.Model.WorldObject.Description;
                        Model.Model.SensorObjects.Add(new HudSensorObject() {
                            Obj = planet,
                            Text = text,
                            FontPosition = new Vector2(TextLeft, ImageTop + n),
                            Distance = d,
                            FontOrigin = Model.Font.MeasureString(text) / 2
                        });
                        n = n + FontIncrement;
                    }
                    Model.UpdateOrderInt = _updateOrderInterval - 1;
                } else {
                    foreach (var so in Model.Model.SensorObjects) {
                        so.Distance = (double)Vector3.Distance(Model.PlayerShip.Model.Position, so.Obj.Model.Position) / DivisionDistanceValue;
                        so.FontOrigin = Model.Font.MeasureString(so.Text) / 2;
                        if (so.Distance < _maxDistance) {
                            so.FontPosition = new Vector2(TextLeft, ImageTop + n);
                            n = n + FontIncrement;
                        }
                    }
                    Model.Model.SensorObjects = Model.Model.SensorObjects.OrderBy(s => s.Distance).ToList();
                }
                Model.UpdateOrderInt = 0;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            //game.Graphics.SpriteBatch.Draw(_sensor, _screenRectangle, Color.White);
            var n = 0;
            var playerShip = ShipHelper.GetPlayerShip(game);
            foreach (var sensorObject in Model.Model.SensorObjects) {
                if (n < _maxSensorObjects) {
                    var fontOrigin = Model.Font.MeasureString(sensorObject.Text) / 2;
                    if (sensorObject.Distance != (double)0) {
                        if (sensorObject.Distance < _maxDistance) {
                            var color = Color.LightBlue;
                            if (playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget != null && sensorObject.Text == playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.WorldObject.Description) {
                                color = Color.Red;
                            }
                            game.Graphics.Model.SpriteBatch.DrawString(Model.Font, sensorObject.Text + ": " + sensorObject.Distance.ToString("#.##"), sensorObject.FontPosition, color, 0, sensorObject.FontOrigin, 2.0f, SpriteEffects.None, 0.5f);
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