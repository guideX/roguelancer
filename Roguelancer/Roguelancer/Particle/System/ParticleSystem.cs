

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Particle.System.Affector;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Particle.System.Emitters;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Models;
namespace Roguelancer.Particle.System {
    /// <summary>
    /// Particle System
    /// </summary>
    public class ParticleSystem {
        /// <summary>
        /// Settings
        /// </summary>
        public ParticleSystemSettingsModel Settings { get; set; }
        /// <summary>
        /// Particle Manager
        /// </summary>
        private ParticleManager _particleManager;
        /// <summary>
        /// View
        /// </summary>
        private Matrix _view;
        /// <summary>
        /// Projection
        /// </summary>
        private Matrix _projection;
        /// <summary>
        /// Explosion
        /// </summary>
        private Texture2D _explosion;
        /// <summary>
        /// Fire
        /// </summary>
        private Texture2D _fire;
        /// <summary>
        /// Smoke
        /// </summary>
        private Texture2D _smoke;
        /// <summary>
        /// Fire Particle System
        /// </summary>
        private FireParticleSystem _fireParticleSystem;
        /// <summary>
        /// Smoke Plume Emitter
        /// </summary>
        private SmokeParticleSystem _smokePlumeParticleSystem;
        /// <summary>
        /// Smoke Ring Emitter
        /// </summary>
        private SmokeRingEmitter _smokeRingEmitter;
        /// <summary>
        /// Smoke Plume Emitter
        /// </summary>
        private SmokePlumeEmitter _smokePlumeEmitter;
        /// <summary>
        /// Explosion Particle System
        /// </summary>
        private ExplosionParticleSystem _explosionParticleSystem;
        /// <summary>
        /// Explosion Smoke Particle System
        /// </summary>
        private ExplosionSmokeParticleSystem _explosionSmokeParticleSystem;
        /// <summary>
        /// Projectile Trail Particle System
        /// </summary>
        private ProjectileTrailParticleSystem _projectileTrailParticleSystem;
        /// <summary>
        /// Projectiles
        /// </summary>
        private List<Projectile> _projectiles = new List<Projectile>();
        /// <summary>
        /// Time to Next Projectile
        /// </summary>
        private TimeSpan _timeToNextProjectile = TimeSpan.Zero;
        /// <summary>
        /// Target Elapsed Time
        /// </summary>
        private TimeSpan _targetElapsedTime;
        /// <summary>
        /// Fire Particle Settings
        /// </summary>
        public struct FireParticleSettings {
            public Color InitialColor;
            public float Scale;
            public float Radius;
            public float Height;
        } 
        /// <summary>
        /// Particle System
        /// </summary>
        /// <param name="game"></param>
        public ParticleSystem(RoguelancerGame game) {
            _targetElapsedTime = TimeSpan.FromTicks(333333);
            _particleManager = new ParticleManager(game);
            game.Components.Add(_particleManager);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
        }
        /// <summary>
        /// Load Textures
        /// </summary>
        /// <param name="game"></param>
        private void LoadTextures(RoguelancerGame game) {
            _explosion = game.Content.Load<Texture2D>(Settings.ExplosionTexture);
            _fire = game.Content.Load<Texture2D>(Settings.FireTexture);
            _smoke = game.Content.Load<Texture2D>(Settings.SmokeTexture);
        }
        /// <summary>
        /// Initialize System
        /// </summary>
        public void InitializeSystem() {
            _explosionSmokeParticleSystem = new ExplosionSmokeParticleSystem(100, _smoke);
            _fireParticleSystem = new FireParticleSystem(500, _fire);
            _smokePlumeParticleSystem = new SmokeParticleSystem(0, _smoke);
            _smokePlumeEmitter = new SmokePlumeEmitter(Vector3.Zero, 0);
            _projectileTrailParticleSystem = new ProjectileTrailParticleSystem(500, _smoke);
            _explosionSmokeParticleSystem.AddAffector(new VelocityAffector(Vector3.Down));
            _explosionParticleSystem = new ExplosionParticleSystem(100, _explosion);
            _explosionParticleSystem.AddAffector(new VelocityAffector(Vector3.Down));
            _smokeRingEmitter = new SmokeRingEmitter(Vector3.Zero, 0);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            if(Settings.Enabled) {
                LoadTextures(game);
                InitializeSystem();
                if(Settings.Explosions) {
                    _particleManager.AddParticleSystem(_explosionSmokeParticleSystem, BlendState.NonPremultiplied);
                    _particleManager.AddParticleSystem(_explosionParticleSystem, BlendState.Additive);
                }
                if(Settings.Fire) {
                    _particleManager.AddParticleSystem(_fireParticleSystem, BlendState.Additive);
                }
                if(Settings.Smoke) {
                    _smokePlumeParticleSystem.AddEmitter(_smokePlumeEmitter);
                    _particleManager.AddParticleSystem(_smokePlumeParticleSystem, BlendState.NonPremultiplied);
                }
                if(Settings.Projectiles) {
                    _particleManager.AddParticleSystem(_projectileTrailParticleSystem, BlendState.NonPremultiplied);
                }
                if(Settings.SmokeRing) {
                    _smokePlumeParticleSystem.AddEmitter(_smokeRingEmitter);
                }
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if(Settings.Enabled) {
                if(Settings.SmokeRing) {
                    _smokeRingEmitter.EmissionRate = Settings.SmokeRingParticles;
                }
                if(Settings.Fire) {
                    _fireParticleSystem.emissionRate = Settings.FireRingSystemParticles;
                }
                if(Settings.Smoke) {
                    _smokePlumeEmitter._emissionRate = Settings.SmokePlumeParticles;
                }
                if(Settings.Explosions) {
                    UpdateExplosions(game);
                }
                if(Settings.Projectiles) {
                    UpdateProjectiles(game);
                }
                _particleManager.SetMatrices(_view, _projection);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if(Settings.Enabled) {
                float lAspectRatio = (float)game.GraphicsDevice.Viewport.Width / (float)game.Graphics.Model.GraphicsDeviceManager.GraphicsDevice.Viewport.Height;
                _view = 
                    Matrix.CreateTranslation(0, -25, 0) * 
                    Matrix.CreateRotationY(MathHelper.ToRadians(Settings.CameraRotation)) * 
                    Matrix.CreateRotationX(MathHelper.ToRadians(Settings.CameraArc)) * 
                    Matrix.CreateLookAt(new Vector3(0, 0, - Settings.CameraDistance), 
                    new Vector3(0, 0, 0), Vector3.Up);
                _projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, lAspectRatio, 1, 10000);
            }
        }
        /// <summary>
        /// Update Explosions
        /// </summary>
        /// <param name="game"></param>
        public void UpdateExplosions(RoguelancerGame game) {
            _timeToNextProjectile -= game.GameTime.ElapsedGameTime;
            if(_timeToNextProjectile <= TimeSpan.Zero) {
                _projectiles.Add(new Projectile(_explosionParticleSystem, _explosionSmokeParticleSystem, _projectileTrailParticleSystem));
                _timeToNextProjectile += TimeSpan.FromSeconds(1);
            }
        }
        /// <summary>
        /// Update Projectiles
        /// </summary>
        /// <param name="game"></param>
        public void UpdateProjectiles(RoguelancerGame game) {
            int i = 0;
            while(i < _projectiles.Count) {
                if(!_projectiles[i].Update(game.GameTime)) {
                    _projectileTrailParticleSystem.RemoveEmitter(_projectiles[i].TrailEmitter);
                    _projectiles.RemoveAt(i);
                } else {
                    i++;
                }
            }
        }
    }
}