using Microsoft.Xna.Framework;
using Roguelancer;
using Roguelancer.Helpers;
using Roguelancer.Models;
using Roguelancer.Objects;
using System.Collections.Generic;
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
        return results;
    }
    /// <summary>
    /// Target Next
    /// </summary>
    public static void TargetNextObject(this RoguelancerGame game) {
        var sensorObjects = GetAllSensorObjects(game, false);
        var playerShip = ShipHelper.GetPlayerShip(game);
        ///var b = false;
        //var b2 = false;
        var lastSensorObjectMatchesCurrentTarget = false;
        var itemChosen = false;
        for (var i = 0; i <= sensorObjects.Count - 1; i++) {
            /*
            var last = false;
            if (i == sensorObjects.Count - 1) {
                last = true;
            }*/
            var currentSensorObjectMatchesCurrentTarget = sensorObjects[i].Obj.Model.WorldObject.Description == playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget;
            if (lastSensorObjectMatchesCurrentTarget) {
                playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[i].Obj.Model.WorldObject.Description;
                DebugTextHelper.SetText(game, "Targeting " + sensorObjects[i].Obj.Model.WorldObject.Description, true);
                //playerShip.ShipModel.PlayerShipControl.Model
                itemChosen = true;
                return;
            }
            lastSensorObjectMatchesCurrentTarget = currentSensorObjectMatchesCurrentTarget;
        }
        if (!itemChosen) {
            var currentTarget = sensorObjects[0].Obj.Model.WorldObject.Description;
            playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = currentTarget;
            DebugTextHelper.SetText(game, "Targeting " + currentTarget, true);

        }
        /*
        foreach (var sensorObject in sensorObjects) {
            var currentSensorObjectMatchesCurrentTarget = sensorObject.Obj.Model.WorldObject.Description == playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget;
            if (lastSensorObjectMatchesCurrentTarget && !currentSensorObjectMatchesCurrentTarget) {
                playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObject.Obj.Model.WorldObject.Description;
                DebugTextHelper.SetText(game, "Targeting " + sensorObject.Obj.Model.WorldObject.Description, true);
                itemChosen = true;
                return;
            }
            lastSensorObjectMatchesCurrentTarget = sensorObject.Obj.Model.WorldObject.Description == playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget;
        }
        if (!itemChosen) {
            playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[0].Text;
            DebugTextHelper.SetText(game, "Targeting " + sensorObjects[0].Text, true);
        }
        //if (!b || (b && !b2)) {
        //playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[0].Text;
        //DebugTextHelper.SetText(game, "Targeting " + sensorObjects[0].Text, true);
        //}
        */
    }
    /// <summary>
    /// Move Forward
    /// </summary>
    /// <param name="game"></param>
    public static void MoveForward(this RoguelancerGame game, PlayerShipControlModel player, GameModel model) {
        game.Camera.Shake(player.ShakeValue, 0f, false);
        if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAmount) {
            model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
        } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAmount) {
            model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAddSpeed;
        } else {
            model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
        }
    }
}