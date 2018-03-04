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
    public static List<HudSensorObject> GetAllSensorObjects(this RoguelancerGame game, bool includeShips = false) {
        var playerShip = ShipHelper.GetPlayerShip(game);
        var results = new List<HudSensorObject>();
        var shipID = 0;
        if (includeShips) {
            foreach (var ship in game.Objects.Model.Ships.Model.Ships) {
                shipID++;
                results.Add(new HudSensorObject() {
                    Obj = ship,
                    Distance = Vector3.Distance(playerShip.Model.Position, ship.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                    Text = "Ship " + shipID.ToString(),
                    FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
                    //FontOrigin = 
                });
            }
        }
        var stationID = 0;
        foreach (var station in game.Objects.Model.Stations.Model.Stations) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = station,
                Distance = Vector3.Distance(playerShip.Model.Position, station.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                Text = "Station " + stationID.ToString(),
                FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
                //FontOrigin = 
            });
        }
        foreach (var planet in game.Objects.Model.Planets.Model.Planets) {
            stationID++;
            results.Add(new HudSensorObject() {
                Obj = planet,
                Distance = Vector3.Distance(playerShip.Model.Position, planet.Model.Position) / (int)HudEnums.DivisionDistanceValue,
                Text = "Planet " + stationID.ToString(),
                FontPosition = new Vector2((int)HudEnums.TextLeft, (int)HudEnums.ImageTop),
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
            var station = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation();
            if (station != null) {
                if (station != null & station.Model != null && station.Model.WorldObject != null) {
                    playerShip.FaceObject(playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget);
                    playerShip.ShipModel.GoingToObject = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation();
                    playerShip.ShipModel.GoingTo = true;
                    game.Input.InputItems.Toggles.Cruise = true;
                    //playerShip.Model.Up.Y = 0f;
                    DebugTextHelper.SetText(game, "Goto " + station.Model.WorldObject.Model.Description, true);
                } else {
                    DebugTextHelper.SetText(game, "Goto Failed", true);
                }
            }
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
                //playerShip.ShipModel.PlayerShipControl.Model.CurrentTargetStationObject = sensorObjects[i].Obj.Station; // sensorObjects[i].Obj.Model;
                DebugTextHelper.SetText(game, "Targeting " + sensorObjects[i].Obj.Model.WorldObject.Model.Description, true);
                itemChosen = true;
                return;
            }
            lastSensorObjectMatchesCurrentTarget = currentSensorObjectMatchesCurrentTarget;
        }
        if (!itemChosen) {
            playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[0].Obj.Model;
            DebugTextHelper.SetText(game, "Targeting " + sensorObjects[0].Obj.Model.WorldObject.Model.Description, true);
            itemChosen = true;
        }
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