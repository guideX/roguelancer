using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Bloom;
using Roguelancer.Objects;
using Roguelancer.Particle;
using Roguelancer.Particle.System.ParticleSystems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Storage;
namespace Roguelancer.Functionality {
    public class clsGame : Microsoft.Xna.Framework.Game {
        public clsGraphics lGraphics;
        public clsShip lShip;
        public clsSettings lSettings;
        private clsStarfields lStars;
        public clsCamera lCamera;
        public clsInput lInput;
        public clsDebugText lDebugText;
        private clsCamera lCameraSnapshot;
        private clsBloomHandler lBloom;
        private clsPlanet lEarth;
        //private clsParticleSystemHandler lParticleSystem;
        //private clsSound lEngineNoise;
        public clsGame() {
            lSettings = new clsSettings();
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            lBloom = new clsBloomHandler(this);
            lStars = new clsStarfields();
            lInput = new clsInput();
            lCamera = new clsCamera();
            lShip = new clsShip(true);
            lGraphics = new clsGraphics();
            lGraphics.Initialize(this);
            lDebugText = new clsDebugText();
            lEarth = new clsPlanet();
            //lEngineNoise = new clsSound();
            //lParticleSystem = new clsParticleSystemHandler(this);
        }
        protected override void Initialize() {
            base.Initialize();
            lCamera.Initialize(this);
            lInput.Initialize(this);
            lEarth.Initialize(this);
            //lParticleSystem.Initialize();
            //lEngineNoise.Initialize(this);
        }
        protected override void LoadContent() {
            lGraphics.LoadContent();
            lStars.LoadContent(this);
            lShip.LoadContent(this);
            lDebugText.LoadContent(this);
            lDebugText.Update(this);
            lBloom.LoadContent();
            lEarth.modelPath = lSettings.lEarth.shipModel;
            lEarth.startPosition = lSettings.lEarth.shipStartupPosition;
            lEarth.LoadContent(this);
            //lEngineNoise.soundPath = "engine";
            //lParticleSystem.LoadContent(this);
        }
        protected override void Update(GameTime _GameTime) {
            lStars.Update(lCamera, lGraphics);
            lInput.Update(this);
            lShip.Update(_GameTime, this);
            lBloom.Update(true);
            if (lInput.lInputItems.lToggles.lCameraSnapshot == true) {
                lInput.lInputItems.lToggles.lCameraSnapshot = false;
                lCameraSnapshot = lCamera;
            } else if (lInput.lInputItems.lToggles.lRevertCamera == true) {
                lInput.lInputItems.lToggles.lRevertCamera = false;
                lCamera = lCameraSnapshot;
            }
            lCamera.UpdateCameraChaseTarget(lGraphics, lShip);
            lCamera.Update(_GameTime);
            lEarth.Update(this);
            //lParticleSystem.Update(_GameTime, lDebugText, lGraphics);
            //lEngineNoise.Update(this);
            base.Update(_GameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            lBloom.Draw();
            //lParticleSystem.Draw(this);
            lGraphics.BeginSpriteBatch();
            lGraphics.Draw();
            lDebugText.Draw(this);
            lStars.Draw(lCamera);
            lShip.Draw(this);
            lGraphics.EndSpriteBatch();
            lEarth.Draw(this);
            lEarth.Draw(this);
            //lEngineNoise.Draw(this);
            base.Draw(_GameTime);
        }
    }
}