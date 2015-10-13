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
namespace Roguelancer.Particle.System {
    public class ParticleSystem {
        private clsParticleManager lParticleManager;
        public gParticleSystemSettings settings { get; set; }
        private Matrix view;
        private Matrix projection;
        private Texture2D explosion;
        private Texture2D fire;
        private Texture2D smoke;
        private FireParticleSystem fireParticleSystem;
        private SmokeParticleSystem smokePlumeParticleSystem;
        private SmokeRingEmitter smokeRingEmitter;
        private SmokePlumeEmitter smokePlumeEmitter;
        private ExplosionParticleSystem explosionParticleSystem;
        private ExplosionSmokeParticleSystem explosionSmokeParticleSystem;
        private clsProjectileTrailParticleSystem projectileTrailParticleSystem;
        private List<Projectile> projectiles = new List<Projectile>();
        private TimeSpan timeToNextProjectile = TimeSpan.Zero;
        private TimeSpan targetElapsedTime;
        public struct gFireParticleSettings {
            public Color fInitialColor;
            public float fScale;
            public float fRadius;
            public float fHeight;
        }
        public struct gParticleSystemSettings {
            public int pSmokePlumeParticles;
            public int pSmokeRingParticles;
            public int pFireRingSystemParticles;
            public bool pEnabled;
            public bool pSmoke;
            public bool pSmokeRing;
            public bool pFire;
            public bool pExplosions;
            public bool pProjectiles;
            public float pCameraArc;
            public float pCameraRotation;
            public float pCameraDistance;
            public string pExplosionTexture;
            public string pFireTexture;
            public string pSmokeTexture;
        }
        public ParticleSystem(RoguelancerGame game) {
            targetElapsedTime = TimeSpan.FromTicks(333333);
            lParticleManager = new clsParticleManager(game);
            game.Components.Add(lParticleManager);
        }
        public void Initialize(RoguelancerGame game) {
        }
        private void LoadTextures(RoguelancerGame game) {
            explosion = game.Content.Load<Texture2D>(settings.pExplosionTexture);
            fire = game.Content.Load<Texture2D>(settings.pFireTexture);
            smoke = game.Content.Load<Texture2D>(settings.pSmokeTexture);
        }
        public void InitializeSystem() {
            explosionSmokeParticleSystem = new ExplosionSmokeParticleSystem(100, smoke);
            fireParticleSystem = new FireParticleSystem(500, fire);
            smokePlumeParticleSystem = new SmokeParticleSystem(0, smoke);
            smokePlumeEmitter = new SmokePlumeEmitter(Vector3.Zero, 0);
            projectileTrailParticleSystem = new clsProjectileTrailParticleSystem(500, smoke);
            explosionSmokeParticleSystem.AddAffector(new clsVelocityAffector(Vector3.Down));
            explosionParticleSystem = new ExplosionParticleSystem(100, explosion);
            explosionParticleSystem.AddAffector(new clsVelocityAffector(Vector3.Down));
            smokeRingEmitter = new SmokeRingEmitter(Vector3.Zero, 0);
        }
        public void LoadContent(RoguelancerGame game) {
            if(settings.pEnabled == true) {
                LoadTextures(game);
                InitializeSystem();
                if(settings.pExplosions == true) {
                    lParticleManager.AddParticleSystem(explosionSmokeParticleSystem, BlendState.NonPremultiplied);
                    lParticleManager.AddParticleSystem(explosionParticleSystem, BlendState.Additive);
                }
                if(settings.pFire == true) {
                    lParticleManager.AddParticleSystem(fireParticleSystem, BlendState.Additive);
                }
                if(settings.pSmoke == true) {
                    smokePlumeParticleSystem.AddEmitter(smokePlumeEmitter);
                    lParticleManager.AddParticleSystem(smokePlumeParticleSystem, BlendState.NonPremultiplied);
                }
                if(settings.pProjectiles == true) {
                    lParticleManager.AddParticleSystem(projectileTrailParticleSystem, BlendState.NonPremultiplied);
                }
                if(settings.pSmokeRing == true) {
                    smokePlumeParticleSystem.AddEmitter(smokeRingEmitter);
                }
            }
        }
        public void Update(GameTime _GameTime, gParticleSystemSettings _Settings, DebugText _DebugText, GameGraphics _Graphics) {
            settings = _Settings;
            if(settings.pEnabled == true) {
                if(settings.pSmokeRing == true) {
                    smokeRingEmitter.EmissionRate = settings.pSmokeRingParticles;
                }
                if(settings.pFire == true) {
                    fireParticleSystem.emissionRate = settings.pFireRingSystemParticles;
                }
                if(settings.pSmoke == true) {
                    smokePlumeEmitter._emissionRate = settings.pSmokePlumeParticles;
                }
                if(settings.pExplosions == true) {
                    UpdateExplosions(_GameTime);
                }
                if(settings.pProjectiles == true) {
                    UpdateProjectiles(_GameTime);
                }
                lParticleManager.SetMatrices(view, projection);
            }
        }
        public void Draw(RoguelancerGame game) {
            if(settings.pEnabled == true) {
                float lAspectRatio = (float)game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Width / (float)game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Height;
                view = Matrix.CreateTranslation(0, -25, 0) * Matrix.CreateRotationY(MathHelper.ToRadians(settings.pCameraRotation)) * Matrix.CreateRotationX(MathHelper.ToRadians(settings.pCameraArc)) * Matrix.CreateLookAt(new Vector3(0, 0, - settings.pCameraDistance), new Vector3(0, 0, 0), Vector3.Up);
                projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, lAspectRatio, 1, 10000);
            }
        }
        public void UpdateExplosions(GameTime _GameTime) {
            timeToNextProjectile -= _GameTime.ElapsedGameTime;
            if(timeToNextProjectile <= TimeSpan.Zero) {
                projectiles.Add(new Projectile(explosionParticleSystem, explosionSmokeParticleSystem, projectileTrailParticleSystem));
                timeToNextProjectile += TimeSpan.FromSeconds(1);
            }
        }
        public void UpdateProjectiles(GameTime _GameTime) {
            int i = 0;
            while(i < projectiles.Count) {
                if(!projectiles[i].Update(_GameTime)) {
                    projectileTrailParticleSystem.RemoveEmitter(projectiles[i].lTrailEmitter);
                    projectiles.RemoveAt(i);
                } else {
                    i++;
                }
            }
        }
    }
}