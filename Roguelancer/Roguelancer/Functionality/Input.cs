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
    public class InputItems {
        public clsMouse mouse;
        public clsKeys keys;
        public Toggles toggles;
    }
    public class Toggles {
        public bool freeMouseMode = false;
        public bool mouseMode = true;
        public bool toggleCamera = false;
        public bool revertCamera = false;
        public bool cameraSnapshot = false;
        public bool cruise = false;
    }
    public class clsMouse {
        public float lScrollWheel;
        public bool lLeftButton;
        public bool lRightButton;
        public Vector2 lVector;
        public MouseState State;
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
        public bool lF10;
        public bool lF12;
        public bool lF9;
        public bool lM;
        public bool lF;
    }
    public class Input : IGame {
        public InputItems lInputItems = new InputItems();
        private KeyboardState lLastKeyboardState = new KeyboardState();
        private KeyboardState lCurrentKeyboardState = new KeyboardState();
        public Input() {
            lInputItems.toggles = new Toggles();
            lInputItems.keys = new clsKeys();
            lInputItems.mouse = new clsMouse();
        }
        public void Initialize(RoguelancerGame _Game) {

        }
        public void LoadContent(RoguelancerGame _Game) {

        }
        public void Update(RoguelancerGame _Game) {
            if(_Game.settings.cameraSettings.fieldOfView < 80 && _Game.settings.cameraSettings.fieldOfView > 180) {
                _Game.settings.cameraSettings.fieldOfView = +_Game.input.lInputItems.mouse.lScrollWheel;
            }
            lInputItems.mouse.State = Mouse.GetState();
            lInputItems.mouse.lScrollWheel = lInputItems.mouse.State.ScrollWheelValue * .0001f;
            lLastKeyboardState = lCurrentKeyboardState;
            lCurrentKeyboardState = Keyboard.GetState();
            if(lCurrentKeyboardState.IsKeyDown(Keys.F)) {
                lInputItems.keys.lF = true;
            } else {
                lInputItems.keys.lF = false;
            }
            if(lCurrentKeyboardState.IsKeyDown(Keys.M)) {
                lInputItems.keys.lM = true;
            } else {
                lInputItems.keys.lM = false;
            }
            if(lCurrentKeyboardState.IsKeyDown(Keys.F12)) {
                lInputItems.keys.lF12 = true;
            } else {
                lInputItems.keys.lF12 = false;
            }
            if(lCurrentKeyboardState.IsKeyDown(Keys.F10)) {
                lInputItems.keys.lF10 = true;
            } else {
                lInputItems.keys.lF10 = false;
            }
            if(lCurrentKeyboardState.IsKeyDown(Keys.F9)) {
                lInputItems.keys.lF9 = true;
            } else {
                lInputItems.keys.lF9 = false;
            }
            if(lCurrentKeyboardState.IsKeyDown(Keys.Escape)) {
                lInputItems.keys.lEscape = true;
                _Game.Exit();
            } else {
                lInputItems.keys.lEscape = false;
            }
            if(_Game.gameState.currentGameState == GameState.GameStates.playing) {
                if(lCurrentKeyboardState.IsKeyDown(Keys.C)) {
                    if(lLastKeyboardState.IsKeyUp(Keys.C)) {
                        if(lInputItems.toggles.cruise == true) {
                            lInputItems.toggles.cruise = false;
                        } else {
                            lInputItems.toggles.cruise = true;
                        }
                    }
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.Space)) {
                    if(lLastKeyboardState.IsKeyUp(Keys.Space)) {
                        if(lInputItems.toggles.toggleCamera == true) {
                            lInputItems.toggles.toggleCamera = false;
                            lInputItems.toggles.revertCamera = true;
                        } else {
                            lInputItems.toggles.toggleCamera = true;
                            lInputItems.toggles.cameraSnapshot = true;
                        }
                    }
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.P)) {
                    lInputItems.keys.lP = true;
                } else {
                    lInputItems.keys.lP = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.L)) {
                    lInputItems.keys.lL = true;
                } else {
                    lInputItems.keys.lL = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.J)) {
                    lInputItems.keys.lJ = true;
                } else {
                    lInputItems.keys.lJ = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.K)) {
                    lInputItems.keys.lK = true;
                } else {
                    lInputItems.keys.lK = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.Left)) {
                    lInputItems.keys.lLeft = true;
                } else {
                    lInputItems.keys.lLeft = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.Right)) {
                    lInputItems.keys.lRight = true;
                } else {
                    lInputItems.keys.lRight = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.Up)) {
                    lInputItems.keys.lUp = true;
                } else {
                    lInputItems.keys.lUp = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.Down)) {
                    lInputItems.keys.lDown = true;
                } else {
                    lInputItems.keys.lDown = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.Tab)) {
                    lInputItems.keys.lTab = true;
                } else {
                    lInputItems.keys.lTab = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.S)) {
                    lInputItems.keys.lS = true;
                } else {
                    lInputItems.keys.lS = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.X)) {
                    lInputItems.keys.lX = true;
                } else {
                    lInputItems.keys.lX = false;
                }
                if(lCurrentKeyboardState.IsKeyDown(Keys.W)) {
                    lInputItems.keys.lW = true;

                } else {
                    lInputItems.keys.lW = false;
                }
                if(Mouse.GetState().LeftButton == ButtonState.Pressed) {
                    lInputItems.mouse.lLeftButton = true;
                } else {
                    lInputItems.mouse.lLeftButton = false;
                }
                lInputItems.mouse.lVector = new Vector2(lInputItems.mouse.State.X, lInputItems.mouse.State.Y);
            }
            if(_Game.input.lInputItems.toggles.cameraSnapshot == true) {
                _Game.input.lInputItems.toggles.cameraSnapshot = false;
                _Game.cameraSnapshot = _Game.camera;
            } else if(_Game.input.lInputItems.toggles.revertCamera == true) {
                _Game.input.lInputItems.toggles.revertCamera = false;
                _Game.camera = _Game.cameraSnapshot;
            }
            if(_Game.input.lInputItems.keys.lM) {
                _Game.input.lInputItems.toggles.mouseMode = true;
                _Game.input.lInputItems.toggles.freeMouseMode = false;
            }
            if(_Game.input.lInputItems.keys.lF) {
                _Game.input.lInputItems.toggles.mouseMode = false;
                _Game.input.lInputItems.toggles.freeMouseMode = true;
            }
            if(_Game.input.lInputItems.keys.lF10) {
                if(_Game.gameState.currentGameState == GameState.GameStates.menu) {
                    _Game.gameState.lastGameState = _Game.gameState.currentGameState;
                    _Game.gameState.currentGameState = GameState.GameStates.playing;
                    _Game.debugText.text = "";
                }
            }
            if(_Game.input.lInputItems.keys.lF9) {
                if(_Game.gameState.currentGameState == GameState.GameStates.playing) {
                    _Game.gameState.lastGameState = _Game.gameState.currentGameState;
                    _Game.gameState.currentGameState = GameState.GameStates.menu;
                    _Game.debugText.text = _Game.settings.menuText;
                }
            }
            //if(_Game.input.lInputItems.keys.lF12) {
                //if(_Game.gameState.currentGameState == GameState.GameStates.menu) {
                    //_Game.settings = new GameSettings();
                    //_Game.objects.Reset(_Game);
                    //_Game.gameState.lastGameState = _Game.gameState.currentGameState;
                    //_Game.gameState.currentGameState = GameState.GameStates.playing;
                    //_Game.debugText.text = "";
                //}
            //}
        }
        public void Draw(RoguelancerGame _Game) {
        }
    }
}