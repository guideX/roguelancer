﻿using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Particle {
    /// <summary>
    /// Starfields
    /// </summary>
    public class Starfields : IGame {
        #region "private properties"
        /// <summary>
        /// Explosions
        /// </summary>
        private List<ParticleExplosion> _explosions = new List<ParticleExplosion>();
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
        private ParticleStarSheet[] _stars;
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
        private StarSettingsModel _starSettings;
        #endregion
        #region "public methods"
        /// <summary>
        /// Starfields
        /// </summary>
        /// <param name="starSettings"></param>
        public Starfields(StarSettingsModel starSettings) {
            _starSettings = starSettings;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            _stars = new ParticleStarSheet[_starSettings.NumberOfStarSheets];
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            long n = 0;
            _explosionTexture = game.Content.Load<Texture2D>("Textures\\Particle");
            _explosionColorsTexture = game.Content.Load<Texture2D>("Textures\\ParticleColors");
            _explosionEffect = game.Content.Load<Effect>("Effects\\Particle");
            _explosionEffect.CurrentTechnique = _explosionEffect.Techniques["Technique1"];
            _explosionEffect.Parameters["theTexture"].SetValue(_explosionTexture);
            _starTexture = game.Content.Load<Texture2D>("textures\\stars");
            _starEffect = _explosionEffect.Clone();
            _starEffect.CurrentTechnique = _starEffect.Techniques["Technique1"];
            _starEffect.Parameters["theTexture"].SetValue(_explosionTexture);
            n = _starSettings.MaxPositionStartingY;
            for (var i = 0; i < _stars.Length; ++i) {
                n = n - _starSettings.MaxPositionIncrementY;
                _stars[i] = new ParticleStarSheet(game, new Vector3(_starSettings.MaxPositionX, _starSettings.MaxPositionY, n), _starSettings.AmountOfStarsPerSheet, _starTexture, _starEffect, _starSettings.MaxSize);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            for (var i = 0; i < _stars.Length; ++i) {
                _stars[i].Update(game);
            }
            for (var i = 0; i < _explosions.Count; ++i) {
                _explosions[i].Update(game.GameTime);
                if (_explosions[i].IsDead) {
                    _explosions.RemoveAt(i);
                    --i;
                }
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            for (var i = 0; i < _stars.Length; ++i) {
                _stars[i].Draw(game);
                foreach (ParticleExplosion explosion in _explosions) {
                    explosion.Draw(game);
                }
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
        }
        #endregion
    }
}