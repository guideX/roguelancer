using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Enum;
using Roguelancer.Helpers;
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
                    if (game.Settings.Model.KeyAssignments.CurrentGameState_Menu.FindWasKeyPressed(InputItems.Keys)) MenuActionsHelper.StartPlaying(game);
                    
                    // EXIT GAME
                    if (game.Settings.Model.KeyAssignments.Exit.FindWasKeyPressed(InputItems.Keys)) game.Exit();
                    break;
                case GameStatesEnum.Playing:
                    
                    // UPDATE LEFT BUTTON
                    InputItems.Mouse.LeftButton = (InputItems.Mouse.State.LeftButton == ButtonState.Pressed) ? true : false;
                    
                    // UPDATE RIGHT BUTTON
                    InputItems.Mouse.RightButton = (InputItems.Mouse.State.RightButton == ButtonState.Pressed) ? true : false;
                    
                    // UPDATE MOUSE VECTOR
                    InputItems.Mouse.Vector = new Vector2(InputItems.Mouse.State.X, InputItems.Mouse.State.Y);
                    
                    // EXIT
                    if (game.Settings.Model.KeyAssignments.Exit.FindWasKeyPressed(InputItems.Keys)) MenuActionsHelper.ExitMenu(game);
                    
                    // GO TO MENU
                    if (game.Settings.Model.KeyAssignments.CurrentGameState_Playing.FindWasKeyPressed(InputItems.Keys)) MenuActionsHelper.GotoMenu(game);
                    
                    // TARGET
                    if (game.Settings.Model.KeyAssignments.Target.FindWasKeyPressed(InputItems.Keys)) InGameActionsHelper.TargetNextObject(game);
                    
                    // GOTO
                    if (game.Settings.Model.KeyAssignments.Goto.FindWasKeyPressed(InputItems.Keys)) InGameActionsHelper.GotoCurrentlyTargetedObject(game);
                    
                    // TOGGLE MODE
                    if (game.Settings.Model.KeyAssignments.ToggleMode.FindWasKeyPressed(InputItems.Keys)) InGameActionsHelper.ToggleMode(game);
                    
                    // CRUISE
                    if (game.Settings.Model.KeyAssignments.Cruise.FindWasKeyPressed(InputItems.Keys)) InGameActionsHelper.ToggleCruise(game);
                    
                    // TOGGLE CAMERA
                    if (game.Settings.Model.KeyAssignments.ToggleCamera.FindWasKeyPressed(InputItems.Keys)) InGameActionsHelper.ToggleCamera(game);
                    
                    // MOUSE MODE
                    if (game.Settings.Model.KeyAssignments.MouseMode.FindWasKeyPressed(InputItems.Keys)) InGameActionsHelper.MouseMode(game);
                    
                    // FREE MOUSE MODE
                    if (game.Settings.Model.KeyAssignments.FreeMouseMode.FindWasKeyPressed(InputItems.Keys)) InGameActionsHelper.FreeMouseMode(game);
                    
                    // SNAPSHOT
                    if (game.Input.InputItems.Toggles.CameraSnapshot)
                        InGameActionsHelper.CameraSnapshot(game);
                    else if (game.Input.InputItems.Toggles.RevertCamera)
                        InGameActionsHelper.RevertCamera(game);
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