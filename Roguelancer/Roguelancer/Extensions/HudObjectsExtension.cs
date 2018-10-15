using Roguelancer;
using Roguelancer.Helpers;
using Roguelancer.Models;
using Roguelancer.Objects;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Roguelancer.Enum;
/// <summary>
/// Hud Objects Extension
/// </summary>
public static class HudObjectsExtension {
    /// <summary>
    /// Get All Sensor Objects
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public static List<HudSensorObject> GetAllSensorObjects(this GameObjectsModel gameObjects, bool includeShips = false) {
        var playerShip = ShipHelper.GetPlayerShip(gameObjects);
        var results = new List<HudSensorObject>();
        var shipID = 0;
        if (includeShips) {
            foreach (var ship in gameObjects.Ships.Objects) {
                shipID++;
                results.Add(new HudSensorObject() {
                    Obj = ship,
                    Distance = Vector3.Distance(playerShip.Model.Position, ship.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                    Text = "Ship " + shipID.ToString(),
                    FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
                });
            }
        }
        var stationID = 0;
        foreach (var station in gameObjects.Stations.Objects) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = station,
                Distance = Vector3.Distance(playerShip.Model.Position, station.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                Text = "Station " + stationID.ToString(),
                FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
            });
        }
        foreach (var jumpHole in gameObjects.JumpHoles.Objects) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = jumpHole,
                Distance = Vector3.Distance(playerShip.Model.Position, jumpHole.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                Text = "Jump Hole " + stationID.ToString(),
                FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
            });
        }
        foreach (var planet in gameObjects.Planets.Objects) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = planet,
                Distance = Vector3.Distance(playerShip.Model.Position, planet.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                Text = "Planet " + stationID.ToString(),
                FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
            });
        }
        foreach (var dockingRing in gameObjects.DockingRings.Objects) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = dockingRing,
                Distance = Vector3.Distance(playerShip.Model.Position, dockingRing.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                Text = "Docking Ring " + stationID.ToString(),
                FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
            });
        }
        return results;
    }
    /// <summary>
    /// Face Object
    /// </summary>
    /// <param name="theShipToFace"></param>
    /// <param name="faceToThis"></param>
    public static void FaceObject(this ShipObject theShipToFace, GameModel faceThis) {
        if (faceThis != null && theShipToFace != null) {
            var desiredDirection = Vector3.Normalize(faceThis.Position - theShipToFace.Model.Position);
            theShipToFace.Model.Direction = desiredDirection;
            theShipToFace.Model.Direction.Z += .18f;
            //theShipToFace.Model.Up.Y = 0f;
        }
    }
}