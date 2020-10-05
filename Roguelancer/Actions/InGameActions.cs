using Microsoft.Xna.Framework;
using Roguelancer.Enum;
using Roguelancer.Helpers;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Actions {
    /// <summary>
    /// In Game Actions
    /// </summary>
    public class InGameActions {
        /// <summary>
        /// Roguelancer Game
        /// </summary>
        private RoguelancerGame _game;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game"></param>
        public InGameActions(RoguelancerGame game) {
            _game = game;
        }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="playerShip"></param>
        /// <param name="dockingShip"></param>
        /// <param name="dockableObjectModel"></param>
        public void Dock(ShipObject playerShip, ShipObject dockingShip, DockableObjectModel dockableObjectModel) {
            if (dockingShip.ShipModel.Docked) {
                DebugTextHelper.SetText(_game, "Already docked");
                return;
            }
            if (dockingShip.ShipModel.PlayerShipControl.Model.CurrentTarget == null) {
                DebugTextHelper.SetText(_game, "No Target is currently selected.");
                return;
            }
            var dockTo = dockingShip.ShipModel.GoingToObject.Model;
            var distance = (int)Vector3.Distance(dockingShip.Model.Position, dockingShip.ShipModel.PlayerShipControl.Model.CurrentTarget.Position) / (int)HudEnums.DivisionDistanceValue;
            if (distance < (int)HudEnums.DockDistanceAccept) {
                dockingShip.Model.Velocity = new Vector3(0f, 0f, 0f);
                dockingShip.Model.CurrentThrust = 0f; // Set Current Thrust to Not Moving
                dockingShip.ShipModel.DockedTo = dockTo; // Set Docket To
                dockingShip.ShipModel.Docked = true; // Set Docked Value
                dockableObjectModel.DockedShips.Add(dockingShip); // Add to Docked Ships
                if (dockingShip == playerShip) { // If Docking Ship is Player Ship
                    _game.GameState.Model.CurrentGameState = Enum.GameStatesEnum.Docked; // Set Current Game State to Docked
                    DebugTextHelper.SetText(_game, "Docking To " + dockTo.WorldObject.Model.Description, true);
                }
            } else if (distance < (int)HudEnums.DockDistanceAccept * 2) {
                dockingShip.ShipModel.GoingToObject = dockingShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation(); // Set Docket To
                DebugTextHelper.SetText(_game, "Automatic Docking Initiated", true);
                playerShip.ShipModel.PlayerShipControl.Model.UseAutoDock = true;
            } else {
                _game.Input.InputItems.Toggles.Cruise = false;
                playerShip.Model.Velocity = Vector3.Zero;
                DebugTextHelper.SetText(_game, "Dock failed, destination is too far. " + distance.ToString(), true);
            }
        }
        /// <summary>
        /// Camera Snapshot
        /// </summary>
        public void CameraSnapshot() {
            _game.Input.InputItems.Toggles.CameraSnapshot = false;
            _game.CameraSnapshot = _game.Camera;
            DebugTextHelper.SetText(_game, "Camera Snapshot Mode On");
        }
        /// <summary>
        /// Revert Camera
        /// </summary>
        public void RevertCamera() {
            _game.Input.InputItems.Toggles.RevertCamera = false;
            _game.Camera = _game.CameraSnapshot;
            DebugTextHelper.SetText(_game, "Camera Snapshot Mode Off");
        }
        /// <summary>
        /// Free Mouse Mode
        /// </summary>
        /// <param name="game"></param>
        public void FreeMouseMode() {
            _game.Input.InputItems.Toggles.MouseMode = false;
            _game.Input.InputItems.Toggles.FreeMouseMode = true;
            DebugTextHelper.SetText(_game, "Free Flight Mode Enabled", true);
        }
        /// <summary>
        /// Mouse Mode
        /// </summary>
        /// <param name="game"></param>
        public void MouseMode() {
            _game.Input.InputItems.Toggles.MouseMode = true;
            _game.Input.InputItems.Toggles.FreeMouseMode = false;
            DebugTextHelper.SetText(_game, "Mouse Mode Enabled", true);
        }
        /// <summary>
        /// Toggle Camera
        /// </summary>
        /// <param name="game"></param>
        public void ToggleCamera() {
            if (_game.Input.InputItems.Toggles.ToggleCamera) {
                _game.Input.InputItems.Toggles.ToggleCamera = false;
                _game.Input.InputItems.Toggles.RevertCamera = true;
                DebugTextHelper.SetText(_game, "Revert Camera Mode", true);
            } else {
                _game.Input.InputItems.Toggles.ToggleCamera = true;
                _game.Input.InputItems.Toggles.CameraSnapshot = true;
                DebugTextHelper.SetText(_game, "Camera Snapshot Mode", true);
            }
        }
        /// <summary>
        /// Toggle Cruise
        /// </summary>
        /// <param name="game"></param>
        public void ToggleCruise() {
            if (_game.Input.InputItems.Toggles.Cruise) {
                _game.Input.InputItems.Toggles.Cruise = false;
                DebugTextHelper.SetText(_game, "Cruise Mode Off", true);
            } else {
                _game.Input.InputItems.Toggles.Cruise = true;
                DebugTextHelper.SetText(_game, "Cruise Mode On", true);
            }
        }
        /// <summary>
        /// Toggle Mode
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="game"></param>
        public void ToggleMode() {
            switch (_game.Camera.Model.Mode) {
                case GameCameraModeEnum.DogfightingMode:
                    DebugTextHelper.SetText(_game, "Switching to Experimental Mode", true);
                    _game.Camera.Model.Mode = GameCameraModeEnum.ExperimentalMode;
                    break;
                case GameCameraModeEnum.ExperimentalMode:
                    DebugTextHelper.SetText(_game, "Switching to Standard Mode", true);
                    _game.Camera.Model.Mode = GameCameraModeEnum.StandardMode;
                    break;
                case GameCameraModeEnum.StandardMode:
                    DebugTextHelper.SetText(_game, "Switching to Dogfighting Mode", true);
                    _game.Camera.Model.Mode = GameCameraModeEnum.DogfightingMode;
                    break;
            }
            DebugTextHelper.SetText(_game, "Toggle Mode", true);
        }
        /// <summary>
        /// Face Current Target
        /// </summary>
        public void FaceCurrentTarget() {
            var playerShip = ShipHelper.GetPlayerShip(_game.Objects.Model);
            if (playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget != null) {
                var station = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation();
                if (station != null && station.Model != null && station.Model.WorldObject != null) {
                    playerShip.FaceObject(playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget);
                    DebugTextHelper.SetText(_game, "Now Facing " + playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.Description);
                }
            }
        }
        /// <summary>
        /// Goto Currently Targeted Object 
        /// </summary>
        /// <param name="game"></param>
        public void GotoCurrentlyTargetedObject() {
            var playerShip = ShipHelper.GetPlayerShip(_game.Objects.Model);
            if (playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget != null) {
                var station = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation();
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
                }
            } else {
                DebugTextHelper.SetText(_game, "Nothing targetted.", true);
            }
        }
        /// <summary>
        /// Target Next
        /// </summary>
        public void TargetNextObject() {
            var sensorObjects = _game.Objects.Model.GetAllSensorObjects(false);
            var playerShip = ShipHelper.GetPlayerShip(_game.Objects.Model);
            var lastSensorObjectMatchesCurrentTarget = false;
            var itemChosen = false;
            for (var i = 0; i <= sensorObjects.Count - 1; i++) {
                var currentSensorObjectMatchesCurrentTarget = sensorObjects[i].Obj.Model == playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget;
                if (lastSensorObjectMatchesCurrentTarget) {
                    playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[i].Obj.Model;
                    //playerShip.ShipModel.PlayerShipControl.Model.CurrentTargetStationObject = sensorObjects[i].Obj.Station; // sensorObjects[i].Obj.Model;
                    DebugTextHelper.SetText(_game, "Targeting " + sensorObjects[i].Obj.Model.WorldObject.Model.Description, true);
                    itemChosen = true;
                    return;
                }
                lastSensorObjectMatchesCurrentTarget = currentSensorObjectMatchesCurrentTarget;
            }
            if (!itemChosen) {
                playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget = sensorObjects[0].Obj.Model;
                DebugTextHelper.SetText(_game, "Targeting " + sensorObjects[0].Obj.Model.WorldObject.Model.Description, true);
                itemChosen = true;
            }
        }
        /// <summary>
        /// Full Stop
        /// </summary>
        public void FullStop() {
            var playerShip = ShipHelper.GetPlayerShip(_game.Objects.Model);
            playerShip.Model.CurrentThrust = 0;
            playerShip.ShipModel.GoingTo = false;
            playerShip.ShipModel.GoingToObject = null;
            DebugTextHelper.SetText(_game, "Goto Completed", true);
            _game.Input.InputItems.Toggles.Cruise = false;
        }
    }
}