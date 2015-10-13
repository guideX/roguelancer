// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Functionality;
using Roguelancer.Particle.System.Affector;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Particle.System.Emitters;
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
        private clsParticleManager _particleManager;
        private Matrix _view;
        private Matrix _projection;
        private Texture2D _explosion;
        private Texture2D _fire;
        private Texture2D _smoke;
        private FireParticleSystem _fireParticleSystem;
        private SmokeParticleSystem _smokePlumeParticleSystem;
        private SmokeRingEmitter _smokeRingEmitter;
        private SmokePlumeEmitter _smokePlumeEmitter;
        private ExplosionParticleSystem _explosionParticleSystem;
        private ExplosionSmokeParticleSystem _explosionSmokeParticleSystem;
        private clsProjectileTrailParticleSystem _projectileTrailParticleSystem;
        private List<Projectile> _projectiles = new List<Projectile>();
        private TimeSpan _timeToNextProjectile = TimeSpan.Zero;
        private TimeSpan _targetElapsedTime;
        public struct gFireParticleSettings {
            public Color fInitialColor;
            public float fScale;
            public float fRadius;
            public float fHeight;
        }
        public ParticleSystem(RoguelancerGame game) {
            _targetElapsedTime = TimeSpan.FromTicks(333333);
            _particleManager = new clsParticleManager(game);
            game.Components.Add(_particleManager);
        }
        public void Initialize(RoguelancerGame game) {
        }
        private void LoadTextures(RoguelancerGame game) {
            _explosion = game.Content.Load<Texture2D>(Settings.ExplosionTexture);
            _fire = game.Content.Load<Texture2D>(Settings.FireTexture);
            _smoke = game.Content.Load<Texture2D>(Settings.SmokeTexture);
        }
        public void InitializeSystem() {
            _explosionSmokeParticleSystem = new ExplosionSmokeParticleSystem(100, _smoke);
            _fireParticleSystem = new FireParticleSystem(500, _fire);
            _smokePlumeParticleSystem = new SmokeParticleSystem(0, _smoke);
            _smokePlumeEmitter = new SmokePlumeEmitter(Vector3.Zero, 0);
            _projectileTrailParticleSystem = new clsProjectileTrailParticleSystem(500, _smoke);
            _explosionSmokeParticleSystem.AddAffector(new clsVelocityAffector(Vector3.Down));
            _explosionParticleSystem = new ExplosionParticleSystem(100, _explosion);
            _explosionParticleSystem.AddAffector(new clsVelocityAffector(Vector3.Down));
            _smokeRingEmitter = new SmokeRingEmitter(Vector3.Zero, 0);
        }
        public void LoadContent(RoguelancerGame game) {
            if(Settings.Enabled == true) {
                LoadTextures(game);
                InitializeSystem();
                if(Settings.Explosions == true) {
                    _particleManager.AddParticleSystem(_explosionSmokeParticleSystem, BlendState.NonPremultiplied);
                    _particleManager.AddParticleSystem(_explosionParticleSystem, BlendState.Additive);
                }
                if(Settings.Fire == true) {
                    _particleManager.AddParticleSystem(_fireParticleSystem, BlendState.Additive);
                }
                if(Settings.Smoke == true) {
                    _smokePlumeParticleSystem.AddEmitter(_smokePlumeEmitter);
                    _particleManager.AddParticleSystem(_smokePlumeParticleSystem, BlendState.NonPremultiplied);
                }
                if(Settings.Projectiles == true) {
                    _particleManager.AddParticleSystem(_projectileTrailParticleSystem, BlendState.NonPremultiplied);
                }
                if(Settings.SmokeRing == true) {
                    _smokePlumeParticleSystem.AddEmitter(_smokeRingEmitter);
                }
            }
        }
        public void Update(RoguelancerGame game) {
            if(Settings.Enabled == true) {
                if(Settings.SmokeRing == true) {
                    _smokeRingEmitter.EmissionRate = Settings.SmokeRingParticles;
                }
                if(Settings.Fire == true) {
                    _fireParticleSystem.emissionRate = Settings.FireRingSystemParticles;
                }
                if(Settings.Smoke == true) {
                    _smokePlumeEmitter._emissionRate = Settings.SmokePlumeParticles;
                }
                if(Settings.Explosions == true) {
                    UpdateExplosions(game);
                }
                if(Settings.Projectiles == true) {
                    UpdateProjectiles(game);
                }
                _particleManager.SetMatrices(_view, _projection);
            }
        }
        public void Draw(RoguelancerGame game) {
            if(Settings.Enabled == true) {
                float lAspectRatio = (float)game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Width / (float)game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Height;
                //_view = Matrix.CreateTranslation(Settings.Position);
                //_projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, lAspectRatio, 1, 10);
                _view = Matrix.CreateTranslation(0, -25, 0) * Matrix.CreateRotationY(MathHelper.ToRadians(0f)) * Matrix.CreateRotationX(MathHelper.ToRadians(2)) * Matrix.CreateLookAt(new Vector3(0, 0, - Settings.CameraDistance), new Vector3(0, 0, 0), Vector3.Up);
                //_view = 
                //Matrix.CreateTranslation(Settings.Position.X, Settings.Position.Y, Settings.Position.Z) * 
                //Matrix.CreateRotationY(MathHelper.ToRadians(Settings.Rotation.Y)) * 
                //Matrix.CreateRotationX(MathHelper.ToRadians(Settings.Rotation.X)) * 
                //Matrix.CreateLookAt(new Vector3(0, 0, - Settings.CameraDistance),
                //new Vector3(0, 0, 0), Vector3.Up);
                _projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, lAspectRatio, 1, 10000);
                //_view = Settings.View;
                //_projection = Settings.Projection;
            }
        }
        public void UpdateExplosions(RoguelancerGame game) {
            _timeToNextProjectile -= game.GameTime.ElapsedGameTime;
            if(_timeToNextProjectile <= TimeSpan.Zero) {
                _projectiles.Add(new Projectile(_explosionParticleSystem, _explosionSmokeParticleSystem, _projectileTrailParticleSystem));
                _timeToNextProjectile += TimeSpan.FromSeconds(1);
            }
        }
        public void UpdateProjectiles(RoguelancerGame game) {
            int i = 0;
            while(i < _projectiles.Count) {
                if(!_projectiles[i].Update(game.GameTime)) {
                    _projectileTrailParticleSystem.RemoveEmitter(_projectiles[i].lTrailEmitter);
                    _projectiles.RemoveAt(i);
                } else {
                    i++;
                }
            }
        }
    }
}