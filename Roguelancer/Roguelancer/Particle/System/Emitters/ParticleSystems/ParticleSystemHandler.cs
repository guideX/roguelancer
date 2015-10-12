// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Particle.System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Objects;
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Particle.System.ParticleSystems {
    public class clsParticleSystemHandler {
        private ParticleSystem.gParticleSystemSettings lSettings;
        private ParticleSystem lParticleSystem;
        public clsParticleSystemHandler(RoguelancerGame _Game) {
            lSettings = new ParticleSystem.gParticleSystemSettings();
            lSettings.pFireRingSystemParticles = 200;
            lSettings.pSmokePlumeParticles = 100;
            lSettings.pSmokeRingParticles = 100;
            lSettings.pCameraArc = -1;
            lSettings.pCameraRotation = 0;
            lSettings.pCameraDistance = 300;
            lSettings.pFire = false;
            lSettings.pEnabled = true;
            lSettings.pSmoke = false;
            lSettings.pSmokeRing = false;
            lSettings.pExplosions = false;
            lSettings.pProjectiles = false;
            lSettings.pExplosionTexture = "Textures\\Explosion";
            lSettings.pFireTexture = "Textures\\Fire";
            lSettings.pSmokeTexture = "Textures\\Smoke";
            lParticleSystem = new ParticleSystem(_Game);
            lParticleSystem.settings = lSettings;
        }
        public void Initialize(RoguelancerGame game) {
            lParticleSystem.Initialize(game);
        }
        public void LoadContent(RoguelancerGame _Game) {
            lParticleSystem.LoadContent(_Game);
        }
        public void Update(RoguelancerGame game) {
            lParticleSystem.Update(game.GameTime, lSettings, game.DebugText, game.Graphics);
        }
        public void Draw(RoguelancerGame _Game, GameModel model) {
            lParticleSystem.Draw(_Game);
        }
    }
}