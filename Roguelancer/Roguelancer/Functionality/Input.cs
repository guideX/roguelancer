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
            InputItems = new InputItemsModel();
            InputItems.Toggles = new TogglesModel();
            InputItems.Keys = new KeyInputModel();
            InputItems.Mouse = new MouseInputModel();
            LastKeyboardState = new KeyboardState();
            CurrentKeyboardState = new KeyboardState();
            
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            // Mouse
            if (game.Settings.Model.CameraSettings.Model.FieldOfView < 80 && game.Settings.Model.CameraSettings.Model.FieldOfView > 180) game.Settings.Model.CameraSettings.Model.FieldOfView = +game.Input.InputItems.Mouse.ScrollWheel;
            InputItems.Mouse.State = Mouse.GetState();
            InputItems.Mouse.ScrollWheel = InputItems.Mouse.State.ScrollWheelValue * .0001f;
            LastKeyboardState = CurrentKeyboardState;
            CurrentKeyboardState = Keyboard.GetState();
            // Get Keys Status
            InputItems.Keys.Tab = Keys.Tab.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Escape = Keys.Escape.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Space = Keys.Space.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Left = Keys.Left.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Up = Keys.Up.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Right = Keys.Right.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Down = Keys.Down.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D0 = Keys.D0.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D1 = Keys.D1.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D2 = Keys.D2.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D3 = Keys.D3.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D4 = Keys.D4.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D5 = Keys.D5.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D6 = Keys.D6.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D7 = Keys.D7.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D8 = Keys.D8.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D9 = Keys.D9.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.A = Keys.A.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.B = Keys.B.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.C = Keys.C.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.D = Keys.D.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.E = Keys.E.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F = Keys.F.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.G = Keys.G.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.H = Keys.H.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.I = Keys.I.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.J = Keys.J.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.K = Keys.K.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.L = Keys.L.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.M = Keys.M.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.N = Keys.N.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.O = Keys.O.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.P = Keys.P.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Q = Keys.Q.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.R = Keys.R.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.S = Keys.S.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.T = Keys.T.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.U = Keys.U.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.V = Keys.V.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.W = Keys.W.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.X = Keys.X.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Y = Keys.Y.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.Z = Keys.Z.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad0 = Keys.NumPad0.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad1 = Keys.NumPad1.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad2 = Keys.NumPad2.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad3 = Keys.NumPad3.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad4 = Keys.NumPad4.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad5 = Keys.NumPad5.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad6 = Keys.NumPad6.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad7 = Keys.NumPad7.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad8 = Keys.NumPad8.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.NumPad9 = Keys.NumPad9.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F1 = Keys.F1.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F2 = Keys.F2.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F3 = Keys.F3.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F4 = Keys.F4.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F5 = Keys.F5.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F6 = Keys.F6.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F7 = Keys.F7.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F8 = Keys.F8.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F9 = Keys.F9.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F10 = Keys.F10.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F11 = Keys.F11.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.F12 = Keys.F12.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.LeftControl = Keys.LeftControl.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            InputItems.Keys.RightControl = Keys.RightControl.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
            switch (game.GameState.Model.CurrentGameState) {
                case GameStatesEnum.Menu:
                    // Start Playing
                    if (game.Settings.Model.KeyAssignments.CurrentGameState_Menu.FindKeyBoardStatus(InputItems.Keys).WasKeyPressed) {
                        game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
                        game.GameState.Model.CurrentGameState = GameStatesEnum.Playing;
                    }
                    // Exit Game
                    if (game.Settings.Model.KeyAssignments.Exit.FindKeyBoardStatus(InputItems.Keys).WasKeyPressed) {
                        game.Exit();
                    }
                    break;
                case GameStatesEnum.Playing:
                    // Mouse
                    if (InputItems.Keys.Escape.IsKeyDown) {
                        game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
                        game.GameState.Model.CurrentGameState = GameStatesEnum.Playing;
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
                    // Keyboard
                    // Go To Menu
                    if (game.Settings.Model.KeyAssignments.CurrentGameState_Playing.FindKeyBoardStatus(InputItems.Keys).WasKeyPressed) {
                        //if (game.Input.InputItems.Keys.F9.WasKeyPressed) {
                        game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
                        game.GameState.Model.CurrentGameState = GameStatesEnum.Menu;
                    }
                    if (InputItems.Keys.T.WasKeyPressed) game.TargetNextObject();
                    if (InputItems.Keys.G.WasKeyPressed) game.GotoCurrentlyTargetedObject();
                    if (InputItems.Keys.I.WasKeyPressed) {
                        this.ToggleMode(game);
                    }
                    if (InputItems.Keys.C.WasKeyPressed) {
                        if (InputItems.Toggles.Cruise) {
                            InputItems.Toggles.Cruise = false;
                        } else {
                            InputItems.Toggles.Cruise = true;
                        }
                    }
                    if (CurrentKeyboardState.IsKeyDown(Keys.Space) && LastKeyboardState.IsKeyUp(Keys.Space)) {
                        if (InputItems.Toggles.ToggleCamera) {
                            InputItems.Toggles.ToggleCamera = false;
                            InputItems.Toggles.RevertCamera = true;
                            DebugTextHelper.SetText(game, "Revert Camera Mode", true);
                        } else {
                            InputItems.Toggles.ToggleCamera = true;
                            InputItems.Toggles.CameraSnapshot = true;
                            DebugTextHelper.SetText(game, "Camera Snapshot Mode", true);
                        }
                    }
                    if (game.Input.InputItems.Toggles.CameraSnapshot) {
                        game.Input.InputItems.Toggles.CameraSnapshot = false;
                        game.CameraSnapshot = game.Camera;
                    } else if (game.Input.InputItems.Toggles.RevertCamera) {
                        game.Input.InputItems.Toggles.RevertCamera = false;
                        game.Camera = game.CameraSnapshot;
                    }
                    if (game.Input.InputItems.Keys.M.WasKeyPressed) {
                        game.Input.InputItems.Toggles.MouseMode = true;
                        game.Input.InputItems.Toggles.FreeMouseMode = false;
                        DebugTextHelper.SetText(game, "Mouse Mode Enabled", true);
                    }
                    if (game.Input.InputItems.Keys.F.WasKeyPressed) {
                        game.Input.InputItems.Toggles.MouseMode = false;
                        game.Input.InputItems.Toggles.FreeMouseMode = true;
                        DebugTextHelper.SetText(game, "Free Flight Mode Enabled", true);
                    }
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
/*
InputItems.Keys.Back = Keys.Back.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Enter = Keys.Enter.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Pause = Keys.Pause.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.CapsLock = Keys.CapsLock.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.PageUp = Keys.PageUp.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.PageDown = Keys.PageDown.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.End = Keys.End.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Home = Keys.Home.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Select = Keys.Select.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Print = Keys.Print.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Execute = Keys.Execute.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.PrintScreen = Keys.PrintScreen.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Insert = Keys.Insert.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Delete = Keys.Delete.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Help = Keys.Help.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.LeftWindows = Keys.LeftWindows.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.RightWindows = Keys.RightWindows.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Multiply = Keys.Multiply.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Add = Keys.Add.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Separator = Keys.Separator.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Subtract = Keys.Separator.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Decimal = Keys.Decimal.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Divide = Keys.Divide.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.NumLock = Keys.NumLock.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Scroll = Keys.Scroll.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.LeftShift = Keys.LeftShift.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.RightShift = Keys.RightShift.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.LeftAlt = Keys.LeftAlt.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.RightAlt = Keys.RightAlt.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemSemicolon = Keys.OemSemicolon.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemPlus = Keys.OemPlus.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemComma = Keys.OemComma.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemMinus = Keys.OemMinus.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemPeriod = Keys.OemPeriod.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemQuestion = Keys.OemQuestion.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemTilde = Keys.OemTilde.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.ChatPadGreen = Keys.ChatPadGreen.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.ChatPadOrange = Keys.ChatPadOrange.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemOpenBrackets = Keys.OemOpenBrackets.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemPipe = Keys.OemPipe.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemCloseBrackets = Keys.OemCloseBrackets.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemQuotes = Keys.OemQuotes.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Oem8 = Keys.Oem8.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemBackslash = Keys.OemBackslash.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.ProcessKey = Keys.ProcessKey.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Attn = Keys.Attn.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Crsel = Keys.Crsel.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Exsel = Keys.Exsel.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.EraseEof = Keys.EraseEof.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Zoom = Keys.Zoom.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Pa1 = Keys.Pa1.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.OemClear = Keys.OemClear.GetKeyboardKeyStatusModel(CurrentKeyboardState, LastKeyboardState);
InputItems.Keys.Back.WasKeyPressed = Keys.Back.WasKeypressed(game);
InputItems.Keys.T = Keys.T.WasKeypressed(game);
InputItems.Keys.G = Keys.G.WasKeypressed(game);
if (InputItems.Keys.T) game.TargetNextObject();
if (CurrentKeyboardState.IsKeyDown(Keys.D1)) {
InputItems.Keys.D1.IsKeyDown = true;
} else {
InputItems.Keys.D1.IsKeyDown = false;
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

if (CurrentKeyboardState.IsKeyDown(Keys.C)) {
    InputItems.Keys.C = true;
} else {
    InputItems.Keys.C = false;
}
if (CurrentKeyboardState.IsKeyDown(Keys.H)) {
    InputItems.Keys.H = true;
} else {
    InputItems.Keys.H = false;
}
if (CurrentKeyboardState.IsKeyDown(Keys.E)) {
    InputItems.Keys.E = true;
} else {
    InputItems.Keys.E = false;
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
InputItems.Keys.D = Keys.D.WasKeypressed(game);
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
InputItems.OldKeys = InputItems.Keys;
*/
