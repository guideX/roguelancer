// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class clsInputItems {
        public clsMouse lMouse;
        public clsKeys lKeys;
        public clsToggles lToggles;
    }
    public class clsToggles {
        public bool lToggleCamera = false;
        public bool lRevertCamera = false;
        public bool lCameraSnapshot = false;
        public bool lCruise = false;
    }
    public class clsMouse {
        public float lScrollWheel;
        public bool lLeftButton;
        public bool lRightButton;
        public Vector2 lVector;
    }
    public class clsKeys {
        public bool lSpace;
        public bool lEscape;
        public bool lLeft;
        public bool lRight;
        public bool lUp;
        public bool lDown;
        public bool lW;
        public bool lS;
        public bool lTab;
        public bool lX;
        public bool lP;
        public bool lL;
        public bool lK;
        public bool lJ;
        public bool lC;
    }
    public class clsInput : intGame {
        public clsInputItems lInputItems = new clsInputItems();
        private KeyboardState lLastKeyboardState = new KeyboardState();
        private KeyboardState lCurrentKeyboardState = new KeyboardState();
        public clsInput() {
            lInputItems.lToggles = new clsToggles();
            lInputItems.lKeys = new clsKeys();
            lInputItems.lMouse = new clsMouse();
        }
        public void Initialize(clsGame _Game) {

        }
        public void LoadContent(clsGame _Game) {

        }
        public void Update(clsGame _Game) {
            MouseState _MouseState = Mouse.GetState();
            lInputItems.lMouse.lScrollWheel = _MouseState.ScrollWheelValue * .0001f;
            //_DebugText.Update(lInputItems.lMouse.lScrollWheel.ToString(), _SpriteBatch);
            lLastKeyboardState = lCurrentKeyboardState;
            lCurrentKeyboardState = Keyboard.GetState();
            if (lCurrentKeyboardState.IsKeyDown(Keys.C)) {
                if (lLastKeyboardState.IsKeyUp(Keys.C)) {
                    if (lInputItems.lToggles.lCruise == true) {
                        lInputItems.lToggles.lCruise = false;
                    } else {
                        lInputItems.lToggles.lCruise = true;
                    }
                }
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.Space)) {
                if (lLastKeyboardState.IsKeyUp(Keys.Space)) {
                    if (lInputItems.lToggles.lToggleCamera == true) {
                        lInputItems.lToggles.lToggleCamera = false;
                        lInputItems.lToggles.lRevertCamera = true;
                    } else {
                        lInputItems.lToggles.lToggleCamera = true;
                        lInputItems.lToggles.lCameraSnapshot = true;
                    }
                }
            }
            _Game.lDebugText.lText = lInputItems.lToggles.lToggleCamera.ToString() + " - " + lInputItems.lToggles.lToggleCamera.ToString() + " - " + lInputItems.lToggles.lToggleCamera;
            if (lCurrentKeyboardState.IsKeyDown(Keys.P)) {
                lInputItems.lKeys.lP = true;
            } else {
                lInputItems.lKeys.lP = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.L)) {
                lInputItems.lKeys.lL = true;
            } else {
                lInputItems.lKeys.lL = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.J)) {
                lInputItems.lKeys.lJ = true;
            } else {
                lInputItems.lKeys.lJ = false;
            } if (lCurrentKeyboardState.IsKeyDown(Keys.K)) {
                lInputItems.lKeys.lK = true;
            } else {
                lInputItems.lKeys.lK = false;
            } if (lCurrentKeyboardState.IsKeyDown(Keys.Left)) {
                lInputItems.lKeys.lLeft = true;
            } else {
                lInputItems.lKeys.lLeft = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.Right)) {
                lInputItems.lKeys.lRight = true;
            } else {
                lInputItems.lKeys.lRight = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.Up)) {
                lInputItems.lKeys.lUp = true;
            } else {
                lInputItems.lKeys.lUp = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.Down)) {
                lInputItems.lKeys.lDown = true;
            } else {
                lInputItems.lKeys.lDown = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.Tab)) {
                lInputItems.lKeys.lTab = true;
            } else {
                lInputItems.lKeys.lTab = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.S))
            {
                lInputItems.lKeys.lS = true;
            } else {
                lInputItems.lKeys.lS = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.X))
            {
                lInputItems.lKeys.lX = true;
            } else {
                lInputItems.lKeys.lX = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.Escape)) {
                lInputItems.lKeys.lEscape = true;
                _Game.Exit();
            } else {
                lInputItems.lKeys.lEscape = false;
            }
            if (lCurrentKeyboardState.IsKeyDown(Keys.W)) {
                lInputItems.lKeys.lW = true;
                
            } else {
                lInputItems.lKeys.lW = false;
            }
            if (Mouse.GetState().LeftButton == ButtonState.Pressed) {
                lInputItems.lMouse.lLeftButton = true;
            } else {
                lInputItems.lMouse.lLeftButton = false;
            }
            lInputItems.lMouse.lVector = new Vector2(_MouseState.X, _MouseState.Y);
        }
        public void Draw(clsGame _Game) {

        }
    }
}