using Roguelancer;
using Roguelancer.Helpers;
using Roguelancer.Models;
using Roguelancer.Objects;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
/// <summary>
/// Hud Objects Extension
/// </summary>
public static class HudObjectsExtension {
    /// <summary>
    /// Get All Sensor Objects
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public static List<HudSensorObject> GetAllSensorObjects(this RoguelancerGame game, bool includeShips = false) {
        var playerShip = ShipHelper.GetPlayerShip(game);
        var results = new List<HudSensorObject>();
        var shipID = 0;
        if (includeShips) {
            foreach (var ship in game.Objects.Model.Ships.Model.Ships) {
                shipID++;
                results.Add(new HudSensorObject() {
                    Obj = ship,
                    Distance = Vector3.Distance(playerShip.Model.Position, ship.Model.Position) / HudObject.DivisionDistanceValue,
                    Text = "Ship " + shipID.ToString(),
                    FontPosition = new Vector2(HudObject.TextLeft, HudObject.ImageTop),
                    //FontOrigin = 
                });
            }
        }
        var stationID = 0;
        foreach (var station in game.Objects.Model.Stations.Model.Stations) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = station,
                Distance = Vector3.Distance(playerShip.Model.Position, station.Model.Position) / HudObject.DivisionDistanceValue,
                Text = "Station " + stationID.ToString(),
                FontPosition = new Vector2(HudObject.TextLeft, HudObject.ImageTop),
                //FontOrigin = 
            });
        }
        foreach (var planet in game.Objects.Model.Planets.Model.Planets) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = planet,
                Distance = Vector3.Distance(playerShip.Model.Position, planet.Model.Position) / HudObject.DivisionDistanceValue,
                Text = "Planet " + stationID.ToString(),
                FontPosition = new Vector2(HudObject.TextLeft, HudObject.ImageTop),
                //FontOrigin = 
            });
        }
        return results;
    }
    /// <summary>
    /// Goto Currently Targeted Object 
    /// </summary>
    /// <param name="game"></param>
    public static void GotoCurrentlyTargetedObject(this RoguelancerGame game) {
        var playerShip = ShipHelper.GetPlayerShip(game);
        if (playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget != null) {
            playerShip.FaceObject(playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget);
            playerShip.GoingToObject = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget;
            playerShip.GoingTo = true;
            game.Input.InputItems.Toggles.Cruise = true;
            //playerShip.Model.Up.Y = 0f;
        } else {
            DebugTextHelper.SetText(game, "Nothing targetted.", true);
        }
    }
    /// <summary>
    /// Target Next
    /// </summary>
    public static void TargetNextObject(this RoguelancerGame game) {
        var sensorObjects = GetAllSensorObjects(game, false);
        var playerShip = ShipHelper.GetPlayerShip(game);
        var lastSensorObjectMatchesCurrentTarget = false;
        var itemChosen = false;
        for (var i = 0; i <= sensorObjects.Count - 1; i++) {
            var currentSensorObjectMatchesCurrentTarget = sensorObjects[i].Obj.Model == playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget;
            if (lastSensorObjectMatchesCurrentTarget) {
                playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[i].Obj.Model;
                DebugTextHelper.SetText(game, "Targeting " + sensorObjects[i].Obj.Model.WorldObject.Description, true);
                itemChosen = true;
                return;
            }
            lastSensorObjectMatchesCurrentTarget = currentSensorObjectMatchesCurrentTarget;
        }
        if (!itemChosen) {
            playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[0].Obj.Model;
            DebugTextHelper.SetText(game, "Targeting " + sensorObjects[0].Obj.Model.WorldObject.Description, true);
            itemChosen = true;
        }
    }
    /// <summary>
    /// Face Object
    /// </summary>
    /// <param name="theShipToFace"></param>
    /// <param name="faceToThis"></param>
    public static void FaceObject(this ShipObject theShipToFace, GameModel faceThis) {
        var desiredDirection = Vector3.Normalize(faceThis.Position - theShipToFace.Model.Position);
        theShipToFace.Model.Direction = desiredDirection;
        theShipToFace.Model.Direction.Z += .18f;
        //theShipToFace.Model.Up.Y = 0f;
    }
}