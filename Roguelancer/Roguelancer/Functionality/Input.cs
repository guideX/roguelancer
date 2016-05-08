// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
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
        #endregion
        #region "private properties"
        /// <summary>
        /// Last Keyboard State
        /// </summary>
        private KeyboardState LastKeyboardState { get; set; }
        /// <summary>
        /// Current Keyboard State
        /// </summary>
        private KeyboardState CurrentKeyboardState { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        public Input() {
            InputItems = new InputItemsModel();
            InputItems.Toggles = new TogglesModel();
            InputItems.Keys = new KeyInputModel();
            InputItems.Mouse = new MouseInputModel();
            LastKeyboardState = new KeyboardState();
            CurrentKeyboardState = new KeyboardState();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.Settings.Model.CameraSettings.Model.FieldOfView < 80 && game.Settings.Model.CameraSettings.Model.FieldOfView > 180) {
                game.Settings.Model.CameraSettings.Model.FieldOfView = +game.Input.InputItems.Mouse.ScrollWheel;
            }
            InputItems.Mouse.State = Mouse.GetState();
            InputItems.Mouse.ScrollWheel = InputItems.Mouse.State.ScrollWheelValue * .0001f;
            LastKeyboardState = CurrentKeyboardState;
            CurrentKeyboardState = Keyboard.GetState();
            if (CurrentKeyboardState.IsKeyDown(Keys.D1)) {
                InputItems.Keys.One = true;
            } else {
                InputItems.Keys.One = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.D2)) {
                InputItems.Keys.Two = true;
            } else {
                InputItems.Keys.Two = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.D3)) {
                InputItems.Keys.Three = true;
            } else {
                InputItems.Keys.Three = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.U)) {
                InputItems.Keys.U = true;
            } else {
                InputItems.Keys.U = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.LeftControl)) {
                InputItems.Keys.ControlLeft = true;
            } else {
                InputItems.Keys.ControlLeft = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.RightControl)) {
                InputItems.Keys.ControlRight = true;
            } else {
                InputItems.Keys.ControlRight = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.F)) {
                InputItems.Keys.F = true;
            } else {
                InputItems.Keys.F = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.M)) {
                InputItems.Keys.M = true;
            } else {
                InputItems.Keys.M = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.F12)) {
                InputItems.Keys.F12 = true;
            } else {
                InputItems.Keys.F12 = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.F10)) {
                InputItems.Keys.F10 = true;
            } else {
                InputItems.Keys.F10 = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.F9)) {
                InputItems.Keys.F9 = true;
            } else {
                InputItems.Keys.F9 = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.Escape)) {
                InputItems.Keys.Escape = true;
                game.Exit();
            } else {
                InputItems.Keys.Escape = false;
            }
            if (CurrentKeyboardState.IsKeyDown(Keys.C)) {
                InputItems.Keys.C = true;
            } else {
                InputItems.Keys.C = false;
            }
            if (game.GameState.Model.CurrentGameState == GameStates.Playing) {
                if (CurrentKeyboardState.IsKeyDown(Keys.C)) {
                    if (LastKeyboardState.IsKeyUp(Keys.C)) {
                        if (InputItems.Toggles.Cruise) {
                            InputItems.Toggles.Cruise = false;
                        } else {
                            InputItems.Toggles.Cruise = true;
                        }
                    }
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.Space)) {
                    if (LastKeyboardState.IsKeyUp(Keys.Space)) {
                        if (InputItems.Toggles.ToggleCamera) {
                            InputItems.Toggles.ToggleCamera = false;
                            InputItems.Toggles.RevertCamera = true;
                        } else {
                            InputItems.Toggles.ToggleCamera = true;
                            InputItems.Toggles.CameraSnapshot = true;
                        }
                    }
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.P)) {
                    InputItems.Keys.P = true;
                } else {
                    InputItems.Keys.P = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.L)) {
                    InputItems.Keys.L = true;
                } else {
                    InputItems.Keys.L = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.J)) {
                    InputItems.Keys.J = true;
                } else {
                    InputItems.Keys.J = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.K)) {
                    InputItems.Keys.K = true;
                } else {
                    InputItems.Keys.K = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.Left)) {
                    InputItems.Keys.Left = true;
                } else {
                    InputItems.Keys.Left = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.Right)) {
                    InputItems.Keys.Right = true;
                } else {
                    InputItems.Keys.Right = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.Up)) {
                    InputItems.Keys.Up = true;
                } else {
                    InputItems.Keys.Up = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.D)) {
                    InputItems.Keys.D = true;
                } else {
                    InputItems.Keys.D = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.Down)) {
                    InputItems.Keys.Down = true;
                } else {
                    InputItems.Keys.Down = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.Tab)) {
                    InputItems.Keys.Tab = true;
                } else {
                    InputItems.Keys.Tab = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.S)) {
                    InputItems.Keys.S = true;
                } else {
                    InputItems.Keys.S = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.Z)) {
                    InputItems.Keys.Z = true;
                } else {
                    InputItems.Keys.Z = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.X)) {
                    InputItems.Keys.X = true;
                } else {
                    InputItems.Keys.X = false;
                }
                if (CurrentKeyboardState.IsKeyDown(Keys.W)) {
                    InputItems.Keys.W = true;
                } else {
                    InputItems.Keys.W = false;
                }
                if (InputItems.Mouse.State.LeftButton == ButtonState.Pressed) {
                    InputItems.Mouse.LeftButton = true;
                } else {
                    InputItems.Mouse.LeftButton = false;
                }
                if (InputItems.Mouse.State.RightButton == ButtonState.Pressed) {
                    InputItems.Mouse.RightButton = true;
                } else {
                    InputItems.Mouse.RightButton = false;
                }
                InputItems.Mouse.Vector = new Vector2(InputItems.Mouse.State.X, InputItems.Mouse.State.Y);
            }
            if (game.Input.InputItems.Toggles.CameraSnapshot) {
                game.Input.InputItems.Toggles.CameraSnapshot = false;
                game.CameraSnapshot = game.Camera;
            } else if (game.Input.InputItems.Toggles.RevertCamera) {
                game.Input.InputItems.Toggles.RevertCamera = false;
                game.Camera = game.CameraSnapshot;
            }
            if (game.Input.InputItems.Keys.M) {
                game.Input.InputItems.Toggles.MouseMode = true;
                game.Input.InputItems.Toggles.FreeMouseMode = false;
            }
            if (game.Input.InputItems.Keys.F) {
                game.Input.InputItems.Toggles.MouseMode = false;
                game.Input.InputItems.Toggles.FreeMouseMode = true;
            }
            if (game.Input.InputItems.Keys.F10) {
                if (game.GameState.Model.CurrentGameState == GameStates.Menu) {
                    game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
                    game.GameState.Model.CurrentGameState = GameStates.Playing;
                }
            }
            if (game.Input.InputItems.Keys.F9) {
                if (game.GameState.Model.CurrentGameState == GameStates.Playing) {
                    game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
                    game.GameState.Model.CurrentGameState = GameStates.Menu;
                }
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