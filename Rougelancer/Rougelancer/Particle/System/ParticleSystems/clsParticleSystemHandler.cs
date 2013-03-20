﻿// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rougelancer.Particle.System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Rougelancer.Functionality;
using Rougelancer.Objects;
namespace Rougelancer.Particle.System.ParticleSystems {
    public class clsParticleSystemHandler {
        private clsParticleSystem.gParticleSystemSettings lSettings;
        private clsParticleSystem lParticleSystem;
        public clsParticleSystemHandler(Game _Game) {
            lSettings = new clsParticleSystem.gParticleSystemSettings();
            lSettings.pFireRingSystemParticles = 200;
            lSettings.pSmokePlumeParticles = 100;
            lSettings.pSmokeRingParticles = 100;
            lSettings.pCameraArc = -1;
            lSettings.pCameraRotation = 0;
            lSettings.pCameraDistance = 300;
            lSettings.pFire = true;
            lSettings.pEnabled = true;
            lSettings.pSmoke = true;
            lSettings.pSmokeRing = true;
            lSettings.pExplosions = true;
            lSettings.pProjectiles = false;
            lSettings.pExplosionTexture = "Textures\\Explosion";
            lSettings.pFireTexture = "Textures\\Fire";
            lSettings.pSmokeTexture = "Textures\\Smoke";
            lParticleSystem = new clsParticleSystem(_Game, lSettings);
        }
        public void Init() {
            lParticleSystem.Init();
        }
        public void LoadContent(ContentManager _Content) {
            lParticleSystem.LoadContent(_Content);
        }
        public void Update(GameTime _GameTime, clsDebugText _DebugText, clsGraphics _Graphics) {
            lParticleSystem.Update(_GameTime, lSettings, _DebugText, _Graphics);
        }
        public void Draw(clsGraphics _Graphics, clsCamera _Camera, clsShip _Ship) {
            lParticleSystem.Draw(_Graphics, _Camera, _Ship);
        }
    }
}