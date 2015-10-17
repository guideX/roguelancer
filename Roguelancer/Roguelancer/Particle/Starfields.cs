﻿// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Settings;
namespace Roguelancer.Particle {
    /// <summary>
    /// Starfields
    /// </summary>
    public class Starfields : IGame {
        #region "private variables"
        /// <summary>
        /// Explosions
        /// </summary>
        private List<clsParticleExplosion> _explosions = new List<clsParticleExplosion>();
        /// <summary>
        /// Explosion Texture
        /// </summary>
        private Texture2D _explosionTexture;
        /// <summary>
        /// Explosion Colors Texture
        /// </summary>
        private Texture2D _explosionColorsTexture;
        /// <summary>
        /// Explosion Effect
        /// </summary>
        private Effect _explosionEffect;
        /// <summary>
        /// Stars
        /// </summary>
        private clsParticleStarSheet[] _stars;
        /// <summary>
        /// Star Effect
        /// </summary>
        private Effect _starEffect;
        /// <summary>
        /// Star Texture
        /// </summary>
        private Texture2D _starTexture;
        /// <summary>
        /// Star Settings
        /// </summary>
        private StarSettings _starSettings;
        #endregion
        #region "public functions"
        /// <summary>
        /// Starfields
        /// </summary>
        /// <param name="starSettings"></param>
        public Starfields(StarSettings starSettings) {
            try {
                _starSettings = starSettings;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                _stars = new clsParticleStarSheet[_starSettings.numberOfStarSheets];
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                var n = 0;
                _explosionTexture = game.Content.Load<Texture2D>("Textures\\Particle");
                _explosionColorsTexture = game.Content.Load<Texture2D>("Textures\\ParticleColors");
                _explosionEffect = game.Content.Load<Effect>("Effects\\Particle");
                _explosionEffect.CurrentTechnique = _explosionEffect.Techniques["Technique1"];
                _explosionEffect.Parameters["theTexture"].SetValue(_explosionTexture);
                _starTexture = game.Content.Load<Texture2D>("textures\\stars");
                _starEffect = _explosionEffect.Clone();
                _starEffect.CurrentTechnique = _starEffect.Techniques["Technique1"];
                _starEffect.Parameters["theTexture"].SetValue(_explosionTexture);
                n = _starSettings.maxPositionStartingY;
                for (int i = 0; i < _stars.Length; ++i) {
                    n = n - _starSettings.maxPositionIncrementY;
                    _stars[i] = new clsParticleStarSheet(game, new Vector3(_starSettings.maxPositionX, _starSettings.maxPositionY, n), _starSettings.amountOfStarsPerSheet, _starTexture, _starEffect, _starSettings.maxSize);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                for (int i = 0; i < _stars.Length; ++i) {
                    _stars[i].Draw(game);
                    foreach (clsParticleExplosion _Explosion in _explosions) {
                        _Explosion.Draw(game);
                    }
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                for (int i = 0; i < _stars.Length; ++i) {
                    _stars[i].Update(game);
                }
                for (int i = 0; i < _explosions.Count; ++i) {
                    _explosions[i].Update(game.GameTime);
                    if (_explosions[i].IsDead) {
                        _explosions.RemoveAt(i);
                        --i;
                    }
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
}