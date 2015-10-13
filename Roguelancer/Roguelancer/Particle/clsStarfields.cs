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
using Roguelancer.Settings;
namespace Roguelancer.Particle {
    public class Starfields : IGame {
        private List<clsParticleExplosion> explosions = new List<clsParticleExplosion>();
        private Texture2D explosionTexture;
        private Texture2D explosionColorsTexture;
        private Effect explosionEffect;
        private clsParticleStarSheet[] stars;
        private Effect starEffect;
        private Texture2D starTexture;
        private StarSettings starSettings;
        public Starfields(StarSettings _starSettings) {
            starSettings = _starSettings;
        }
        public void Initialize(RoguelancerGame game) {
            stars = new clsParticleStarSheet[starSettings.numberOfStarSheets];
        }
        public void LoadContent(RoguelancerGame game) {
            int n = 0;
            explosionTexture = game.Content.Load<Texture2D>("Textures\\Particle");
            explosionColorsTexture = game.Content.Load<Texture2D>("Textures\\ParticleColors");
            explosionEffect = game.Content.Load<Effect>("Effects\\Particle");
            explosionEffect.CurrentTechnique = explosionEffect.Techniques["Technique1"];
            explosionEffect.Parameters["theTexture"].SetValue(explosionTexture);
            starTexture = game.Content.Load<Texture2D>("textures\\stars");
            starEffect = explosionEffect.Clone();
            starEffect.CurrentTechnique = starEffect.Techniques["Technique1"];
            starEffect.Parameters["theTexture"].SetValue(explosionTexture);
            n = starSettings.maxPositionStartingY;
            for(int i = 0; i < stars.Length;++i) {
                n = n - starSettings.maxPositionIncrementY;
                stars[i] = new clsParticleStarSheet(game, new Vector3(starSettings.maxPositionX, starSettings.maxPositionY, n), starSettings.amountOfStarsPerSheet, starTexture, starEffect, starSettings.maxSize);
            }
        }
        public void Draw(RoguelancerGame game) {
            for (int i = 0; i < stars.Length; ++i) {
                stars[i].Draw(game);
                foreach(clsParticleExplosion _Explosion in explosions) {
                    _Explosion.Draw(game);
                }
            }
        }
        public void Update(RoguelancerGame game) {
            for(int i = 0; i < stars.Length; ++i) {
                stars[i].Update(game);
            }
            for(int i = 0; i < explosions.Count; ++i) {
                explosions[i].Update(game.GameTime );
                if (explosions[i].IsDead) {
                    explosions.RemoveAt(i);
                    --i;
                }
            }
        }
    }
}