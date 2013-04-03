using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rougelancer.Bloom;
using Rougelancer.Objects;
using Rougelancer.Particle;
using Rougelancer.Particle.System.ParticleSystems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Storage;
namespace Rougelancer.Functionality {
    public class clsGame : Microsoft.Xna.Framework.Game {
        public clsGraphics lGraphics;
        public clsShip lShip;
        public clsSettings lSettings;
        private clsParticleSystemHandler lParticleSystem;
        private clsStarfields lStars;
        public clsCamera lCamera;
        private clsInput lInput;
        private clsInputItems lInputItems;
        private clsDebugText lDebugText;
        private clsCamera lCameraSnapshot;
        private clsBloomHandler lBloom;
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
            lInputItems = new clsInputItems();
            lGraphics.Init(this);
            lDebugText = new clsDebugText();
            lParticleSystem = new clsParticleSystemHandler(this);
        }
        protected override void Initialize() {
            base.Initialize();
            lCamera.Init(this);
            lInput.Init();
            lParticleSystem.Init();
        }
        protected override void LoadContent() {
            lGraphics.LoadContent();
            lStars.LoadContent(this);
            lShip.LoadContent(this);
            lDebugText.LoadContent(this);
            lDebugText.Update(this);
            lBloom.LoadContent();
            lParticleSystem.LoadContent(this);
        }
        protected override void Update(GameTime _GameTime) {
            lStars.Update(lCamera, lGraphics);
            lInputItems = lInput.Update(this, lDebugText, lGraphics.lSpriteBatch);
            lShip.Update(_GameTime, lGraphics, lInputItems, lDebugText, lCamera);
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
            lParticleSystem.Update(_GameTime, lDebugText, lGraphics);
            base.Update(_GameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            lBloom.Draw();
            lParticleSystem.Draw(lGraphics, lCamera, lShip);
            lGraphics.BeginSpriteBatch();
            lGraphics.Draw();
            lDebugText.Draw();
            lStars.Draw(lCamera);
            lShip.Draw(lCamera);
            lGraphics.EndSpriteBatch();
            base.Draw(_GameTime);
        }
    }
}

