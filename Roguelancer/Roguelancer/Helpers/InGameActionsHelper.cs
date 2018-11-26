using Roguelancer.Enum;
namespace Roguelancer.Helpers {
    /// <summary>
    /// In Game Actions
    /// </summary>
    public static class InGameActionsHelper {
        /// <summary>
        /// Camera Snapshot
        /// </summary>
        public static void CameraSnapshot(RoguelancerGame game) {
            game.Input.InputItems.Toggles.CameraSnapshot = false;
            //game.Graphics.Snap
            //game.CameraSnapshot = game.Graphics.Model.Camer;
        }
        /// <summary>
        /// Revert Camera
        /// </summary>
        public static void RevertCamera(RoguelancerGame game) {
            game.Input.InputItems.Toggles.RevertCamera = false;
            //game.Camera = game.CameraSnapshot;
        }
        /// <summary>
        /// Free Mouse Mode
        /// </summary>
        /// <param name="game"></param>
        public static void FreeMouseMode(RoguelancerGame game) {
            game.Input.InputItems.Toggles.MouseMode = false;
            game.Input.InputItems.Toggles.FreeMouseMode = true;
            DebugTextHelper.SetText(game, "Free Flight Mode Enabled", true);
        }
        /// <summary>
        /// Mouse Mode
        /// </summary>
        /// <param name="game"></param>
        public static void MouseMode(RoguelancerGame game) {
            game.Input.InputItems.Toggles.MouseMode = true;
            game.Input.InputItems.Toggles.FreeMouseMode = false;
            DebugTextHelper.SetText(game, "Mouse Mode Enabled", true);
        }
        /// <summary>
        /// Toggle Camera
        /// </summary>
        /// <param name="game"></param>
        public static void ToggleCamera(RoguelancerGame game) {
            if (game.Input.InputItems.Toggles.ToggleCamera) {
                game.Input.InputItems.Toggles.ToggleCamera = false;
                game.Input.InputItems.Toggles.RevertCamera = true;
                DebugTextHelper.SetText(game, "Revert Camera Mode", true);
            } else {
                game.Input.InputItems.Toggles.ToggleCamera = true;
                game.Input.InputItems.Toggles.CameraSnapshot = true;
                DebugTextHelper.SetText(game, "Camera Snapshot Mode", true);
            }
        }
        /// <summary>
        /// Toggle Cruise
        /// </summary>
        /// <param name="game"></param>
        public static void ToggleCruise(RoguelancerGame game) {
            if (game.Input.InputItems.Toggles.Cruise) {
                game.Input.InputItems.Toggles.Cruise = false;
                DebugTextHelper.SetText(game, "Cruise Mode Off", true);
            } else {
                game.Input.InputItems.Toggles.Cruise = true;
                DebugTextHelper.SetText(game, "Cruise Mode On", true);
            }
        }
        /// <summary>
        /// Toggle Mode
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="game"></param>
        public static void ToggleMode(RoguelancerGame game) {
            switch (game.Graphics.Model.Mode) {
                case GameCameraModeEnum.DogfightingMode:
                    DebugTextHelper.SetText(game, "Switching to Experimental Mode", true);
                    game.Graphics.Model.Mode = GameCameraModeEnum.ExperimentalMode;
                    break;
                case GameCameraModeEnum.ExperimentalMode:
                    DebugTextHelper.SetText(game, "Switching to Standard Mode", true);
                    game.Graphics.Model.Mode = GameCameraModeEnum.StandardMode;
                    break;
                case GameCameraModeEnum.StandardMode:
                    DebugTextHelper.SetText(game, "Switching to Dogfighting Mode", true);
                    game.Graphics.Model.Mode = GameCameraModeEnum.DogfightingMode;
                    break;
            }
            DebugTextHelper.SetText(game, "Toggle Mode", true);
        }
        /// <summary>
        /// Goto Currently Targeted Object 
        /// </summary>
        /// <param name="game"></param>
        public static void GotoCurrentlyTargetedObject(RoguelancerGame game) {
            var playerShip = ShipHelper.GetPlayerShip(game.Objects.Model);
            if (playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget != null) {
                var currentTarget = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget;
                if (currentTarget != null) {
                    playerShip.FaceObject(playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget);
                    playerShip.ShipModel.GoingToObject = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation();
                    playerShip.ShipModel.GoingTo = true;
                    game.Input.InputItems.Toggles.Cruise = true;
                    //playerShip.Model.Up.Y = 0f;
                    DebugTextHelper.SetText(game, "Goto " + currentTarget.WorldObject.Model.Description, true);
                    /*
                    var station = currentTarget.GetStation();
                    if (station != null) {
                        if (station != null & station.Model != null && station.Model.WorldObject != null) {
                            playerShip.FaceObject(playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget);
                            playerShip.ShipModel.GoingToObject = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation();
                            playerShip.ShipModel.GoingTo = true;
                            _game.Input.InputItems.Toggles.Cruise = true;
                            //playerShip.Model.Up.Y = 0f;
                            DebugTextHelper.SetText(_game, "Goto " + station.Model.WorldObject.Model.Description, true);
                        } else {
                            DebugTextHelper.SetText(_game, "Goto Failed", true);
                        }
                    } else {
                        DebugTextHelper.SetText(_game, "Goto Failed", true);
                    }
                    */
                }
            } else {
                DebugTextHelper.SetText(game, "Nothing targetted.", true);
            }
        }
        /// <summary>
        /// Target Next
        /// </summary>
        public static void TargetNextObject(RoguelancerGame game) {
            var sensorObjects = game.Objects.Model.GetAllSensorObjects(false);
            var playerShip = ShipHelper.GetPlayerShip(game.Objects.Model);
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
        /// Full Stop
        /// </summary>
        public static void FullStop(RoguelancerGame game) {
            var playerShip = ShipHelper.GetPlayerShip(game.Objects.Model);
            playerShip.Model.CurrentThrust = 0;
            playerShip.ShipModel.GoingTo = false;
            playerShip.ShipModel.GoingToObject = null;
            DebugTextHelper.SetText(game, "Goto Completed", true);
            game.Input.InputItems.Toggles.Cruise = false;
        }
    }
}