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
using Roguelancer.Interfaces;
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
        //private clsPlanet lEarth;
        private clsPlanetCollection lPlanets;
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
            //lEarth = new clsPlanet();
            lPlanets = new clsPlanetCollection();
            
            //lEngineNoise = new clsSound();
            //lParticleSystem = new clsParticleSystemHandler(this);
        }
        protected override void Initialize() {
            lCamera.Initialize(this);
            lInput.Initialize(this);
            //lEarth.Initialize(this);
            lShip.Initialize(this);
            lPlanets.Initialize(this);
            //lParticleSystem.Initialize();
            //lEngineNoise.Initialize(this);
            base.Initialize();
        }
        protected override void LoadContent() {
            lGraphics.LoadContent(this);
            lStars.LoadContent(this);
            lShip.lModel.modelPath = lSettings.shipTexture;
            lShip.LoadContent(this);
            lDebugText.LoadContent(this);
            lDebugText.Update(this);
            lBloom.LoadContent();
            lPlanets.LoadContent(this);
            //lEngineNoise.soundPath = "engine";
            //lParticleSystem.LoadContent(this);
            base.LoadContent();
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
            lPlanets.Update(this);
            //lEarth.Update(this);
            //lParticleSystem.Update(_GameTime, lDebugText, lGraphics);
            //lEngineNoise.Update(this);
            base.Update(_GameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            lBloom.Draw();
            lGraphics.BeginSpriteBatch();
            lGraphics.Draw(this);
            lDebugText.Draw(this);
            lStars.Draw(lCamera);
            lShip.Draw(this);
            lGraphics.EndSpriteBatch();
            lPlanets.Draw(this);
            //lEarth.Draw(this);
            //lParticleSystem.Draw(this);
            //lEngineNoise.Draw(this);
            base.Draw(_GameTime);
        }
    }
}