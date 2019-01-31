using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Helpers;
using Roguelancer.Enum;
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
            Model.PlayerShip = ShipHelper.GetPlayerShip(game.Objects.Model); // Get Player Ship
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
            if (Model.UpdateOrderInt > (int)HudEnums.UpdateOrderInterval) {
                if (Model.PlayerShip == null) { Model.PlayerShip = ShipHelper.GetPlayerShip(game.Objects.Model); }
                Model.ScreenRectangle = new Rectangle((int)HudEnums.ImageLeft, (int)HudEnums.ImageTop, (int)HudEnums.ImageWidth, (int)HudEnums.ImageHeight);
                if (game.Objects.GetObjectCount() != Model.Model.SensorObjects.Count) {
                    Model.Model.SensorObjects = new System.Collections.Generic.List<HudSensorObject>();
                    foreach (var ship in game.Objects.Model.Ships.Model.Ships) {
                        shipId++;
                        d = Vector3.Distance(Model.PlayerShip.Model.Position, ship.Model.Position) / (int)HudEnums.DivisionDistanceValue;
                        text = "Ship " + shipId.ToString();
                        if (d != 0f) {
                            Model.Model.SensorObjects.Add(new HudSensorObject() {
                                Obj = ship,
                                Text = text,
                                FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop + n),
                                Distance = d,
                                FontOrigin = Model.Font.MeasureString(text) / 2
                            });
                            n = n + (int)HudEnums.FontIncrement;
                        }
                    }
                    foreach (var station in game.Objects.Model.Stations.Model.Stations) {
                        d = (double)Vector3.Distance(Model.PlayerShip.Model.Position, station.Model.Position) / (int)HudEnums.DivisionDistanceValue;
                        text = station.Model.WorldObject.Model.Description;
                        Model.Model.SensorObjects.Add(new HudSensorObject() {
                            Obj = station,
                            Text = text,
                            FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop + n),
                            Distance = d,
                            FontOrigin = Model.Font.MeasureString(text) / 2
                        });
                        n = n + (int)HudEnums.FontIncrement;
                    }
                    foreach (var planet in game.Objects.Model.Planets.Model.Planets) {
                        d = (double)Vector3.Distance(Model.PlayerShip.Model.Position, planet.Model.Position) / (int)HudEnums.DivisionDistanceValue;
                        text = planet.Model.WorldObject.Model.Description;
                        Model.Model.SensorObjects.Add(new HudSensorObject() {
                            Obj = planet,
                            Text = text,
                            FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop + n),
                            Distance = d,
                            FontOrigin = Model.Font.MeasureString(text) / 2
                        });
                        n = n + (int)HudEnums.FontIncrement;
                    }
                    foreach (var dockingRing in game.Objects.Model.DockingRings.Model.DockingRings) {
                        d = (double)Vector3.Distance(Model.PlayerShip.Model.Position, dockingRing.Model.Position) / (int)HudEnums.DivisionDistanceValue;
                        text = dockingRing.Model.WorldObject.Model.Description;
                        Model.Model.SensorObjects.Add(new HudSensorObject() {
                            Obj = dockingRing,
                            Text = text,
                            FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop + n),
                            Distance = d,
                            FontOrigin = Model.Font.MeasureString(text) / 2
                        });
                        n = n + (int)HudEnums.FontIncrement;
                    }
                    foreach (var jumpHole in game.Objects.Model.JumpHoles.Model.JumpHoles) {
                        d = (double)Vector3.Distance(Model.PlayerShip.Model.Position, jumpHole.Model.Position) / (int)HudEnums.DivisionDistanceValue;
                        text = jumpHole.Model.WorldObject.Model.Description;
                        Model.Model.SensorObjects.Add(new HudSensorObject() {
                            Obj = jumpHole,
                            Text = text,
                            FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop + n),
                            Distance = d,
                            FontOrigin = Model.Font.MeasureString(text) / 2
                        });
                        n = n + (int)HudEnums.FontIncrement;
                    }
                    Model.UpdateOrderInt = (int)HudEnums.UpdateOrderInterval - 1;
                } else {
                    foreach (var so in Model.Model.SensorObjects) {
                        so.Distance = (double)Vector3.Distance(Model.PlayerShip.Model.Position, so.Obj.Model.Position) / (int)HudEnums.DivisionDistanceValue;
                        so.FontOrigin = Model.Font.MeasureString(so.Text) / 2;
                        if (so.Distance < (int)HudEnums.MaxDistance) {
                            so.FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop + n);
                            n = n + (int)HudEnums.FontIncrement;
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
            var playerShip = ShipHelper.GetPlayerShip(game.Objects.Model);
            foreach (var sensorObject in Model.Model.SensorObjects) {
                if (n < (int)HudEnums.MaxSensorObjects) {
                    var fontOrigin = Model.Font.MeasureString(sensorObject.Text) / 2;
                    if (sensorObject.Distance != (double)0) {
                        if (sensorObject.Distance < (int)HudEnums.MaxDistance) {
                            var color = Color.LightBlue;
                            if (playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget != null && sensorObject.Text == playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.WorldObject.Model.Description) {
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
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
        }
        #endregion
    }
}