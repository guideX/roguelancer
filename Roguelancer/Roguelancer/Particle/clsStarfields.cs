// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Objects;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle {
    class clsStarfields : IGame {
        int lAmountOfStarsPerSheet = 30;
        float lMaxPositionX = 1000000;
        float lMaxPositionY = 1000000;
        int lMaxPositionIncrementY = 10000;
        int lMaxPositionStartingY = 500000;
        int lMaxSize = 1000;
        List<clsParticleExplosion> lExplosions = new List<clsParticleExplosion>();
        Texture2D lExplosionTexture;
        Texture2D lExplosionColorsTexture;
        Effect lExplosionEffect;
        clsParticleStarSheet[] lStars = new clsParticleStarSheet[200];
        Effect lStarEffect;
        Texture2D lStarTexture;
        public clsStarfields() {
        }
        public void Initialize(clsGame _Game) {

        }
        public void LoadContent(clsGame _Game) {
            int n = 0;
            lExplosionTexture = _Game.Content.Load<Texture2D>("Textures\\Particle");
            lExplosionColorsTexture = _Game.Content.Load<Texture2D>("Textures\\ParticleColors");
            lExplosionEffect = _Game.Content.Load<Effect>("Effects\\Particle");
            lExplosionEffect.CurrentTechnique = lExplosionEffect.Techniques["Technique1"];
            lExplosionEffect.Parameters["theTexture"].SetValue(lExplosionTexture);
            lStarTexture = _Game.Content.Load<Texture2D>("textures\\stars");
            lStarEffect = lExplosionEffect.Clone();
            lStarEffect.CurrentTechnique = lStarEffect.Techniques["Technique1"];
            lStarEffect.Parameters["theTexture"].SetValue(lExplosionTexture);
            n = lMaxPositionStartingY;
            for(int i = 0; i < lStars.Length;++i) {
                n = n - lMaxPositionIncrementY;
                lStars[i] = new clsParticleStarSheet(_Game.graphics, new Vector3(lMaxPositionX, lMaxPositionY, n), lAmountOfStarsPerSheet, lStarTexture, lStarEffect, lMaxSize, _Game.camera);
            }
        }
        public void Draw(clsGame _Game) {
            for (int i = 0; i < lStars.Length; ++i) {
                lStars[i].Draw();
                foreach(clsParticleExplosion _Explosion in lExplosions) {
                    _Explosion.Draw(_Game.camera);
                }
            }
        }
        public void Update(clsGame _Game) {
            for(int i = 0; i < lStars.Length; ++i) {
                lStars[i].Update(_Game.camera, _Game.graphics.graphicsDeviceManager.GraphicsDevice);
            }
            for(int i = 0; i < lExplosions.Count; ++i) {
                lExplosions[i].Update(_Game.gameTime );
                if (lExplosions[i].IsDead) {
                    lExplosions.RemoveAt(i);
                    --i;
                }
            }
        }
    }
}