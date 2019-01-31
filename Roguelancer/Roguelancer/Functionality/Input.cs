using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Input
    /// </summary>
    public class Input : IInput {
        #region "public properties"
        /// <summary>
        /// Input Items
        /// </summary>
        public InputItemsModel InputItems { get; set; }
        /// <summary>
        /// Last Keyboard State
        /// </summary>
        public KeyboardState LastKeyboardState { get; set; }
        /// <summary>
        /// Current Keyboard State
        /// </summary>
        public KeyboardState CurrentKeyboardState { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        public Input() {
            InputItems = new InputItemsModel() {
                Toggles = new TogglesModel(),
                Keys = new KeyInputModel(),
                Mouse = new MouseInputModel()
            };
            LastKeyboardState = new KeyboardState();
            CurrentKeyboardState = new KeyboardState();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            // MOUSE
            InputItems.Mouse.State = Mouse.GetState();
            InputItems.Mouse.ScrollWheel = InputItems.Mouse.State.ScrollWheelValue * .0001f;
            LastKeyboardState = CurrentKeyboardState;
            CurrentKeyboardState = Keyboard.GetState();
            // GET KEY STATUS
            InputItems.Keys.UpdateKeysStatus(CurrentKeyboardState, LastKeyboardState);
            switch (game.GameState.Model.CurrentGameState) {
                case GameStatesEnum.Menu:
                    // START PLAYING
                    if (game.Settings.Model.KeyAssignments.CurrentGameState_Menu.FindWasKeyPressed(InputItems.Keys)) game.MenuActions.StartPlaying();
                    // EXIT GAME
                    if (game.Settings.Model.KeyAssignments.Exit.FindWasKeyPressed(InputItems.Keys)) game.Exit();
                    break;
                case GameStatesEnum.Playing:
                    // FACE TARGET
                    if(game.Settings.Model.KeyAssignments.FaceTarget.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.FaceCurrentTarget();
                    // UPDATE LEFT BUTTON
                    InputItems.Mouse.LeftButton = (InputItems.Mouse.State.LeftButton == ButtonState.Pressed) ? true : false;
                    // UPDATE RIGHT BUTTON
                    InputItems.Mouse.RightButton = (InputItems.Mouse.State.RightButton == ButtonState.Pressed) ? true : false;
                    // UPDATE MOUSE VECTOR
                    InputItems.Mouse.Vector = new Vector2(InputItems.Mouse.State.X, InputItems.Mouse.State.Y);
                    // EXIT
                    if (game.Settings.Model.KeyAssignments.Exit.FindWasKeyPressed(InputItems.Keys)) game.MenuActions.ExitMenu();
                    // GO TO MENU
                    if (game.Settings.Model.KeyAssignments.CurrentGameState_Playing.FindWasKeyPressed(InputItems.Keys)) game.MenuActions.GotoMenu();
                    // TARGET
                    if (game.Settings.Model.KeyAssignments.Target.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.TargetNextObject();
                    // GOTO
                    if (game.Settings.Model.KeyAssignments.Goto.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.GotoCurrentlyTargetedObject();
                    // TOGGLE MODE
                    if (game.Settings.Model.KeyAssignments.ToggleMode.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.ToggleMode();
                    // CRUISE
                    if (game.Settings.Model.KeyAssignments.Cruise.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.ToggleCruise();
                    // TOGGLE CAMERA
                    if (game.Settings.Model.KeyAssignments.ToggleCamera.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.ToggleCamera();
                    // MOUSE MODE
                    if (game.Settings.Model.KeyAssignments.MouseMode.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.MouseMode();
                    // FREE MOUSE MODE
                    if (game.Settings.Model.KeyAssignments.FreeMouseMode.FindWasKeyPressed(InputItems.Keys)) game.InGameActions.FreeMouseMode();
                    // SNAPSHOT
                    if (game.Input.InputItems.Toggles.CameraSnapshot) {
                        game.InGameActions.CameraSnapshot();
                    } else if (game.Input.InputItems.Toggles.RevertCamera) {
                        game.InGameActions.RevertCamera();
                    }
                    // DOCK
                    //var dock = game.Settings.Model.KeyAssignments.Dock.FindKeyboardStatus(game.Input.InputItems.Keys);
                    //if (dock.WasKeyPressed) {
                    //dock.IsKeyDown = false;
                    //game.InGameActions.Dock();
                    //}
                    break;
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        public void Dispose(RoguelancerGame game) {
            InputItems = null;
        }
        #endregion
    }
}