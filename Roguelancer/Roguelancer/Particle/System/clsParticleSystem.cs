// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Functionality;
using Roguelancer.Particle.System.Affector;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Particle.System.Emitters;
using Roguelancer.Objects;
namespace Roguelancer.Particle.System {
    public class clsParticleSystem {
        private clsParticleManager lParticleManager;
        private gParticleSystemSettings lSettings;
        private Matrix lView;
        private Matrix lProjection;
        private Texture2D lExplosion;
        private Texture2D lFire;
        private Texture2D lSmoke;
        private clsFireParticleSystem lFireParticleSystem;
        private clsSmokeParticleSystem lSmokePlumeParticleSystem;
        private clsSmokeRingEmitter lSmokeRingEmitter;
        private clsSmokePlumeEmitter lSmokePlumeEmitter;
        private clsExplosionParticleSystem lExplosionParticleSystem;
        private clsExplosionSmokeParticleSystem lExplosionSmokeParticleSystem;
        private clsProjectileTrailParticleSystem lProjectileTrailParticleSystem;
        private List<clsProjectile> lProjectiles = new List<clsProjectile>();
        private TimeSpan lTimeToNextProjectile = TimeSpan.Zero;
        private TimeSpan lTargetElapsedTime;
        public clsParticleSystem(Microsoft.Xna.Framework.Game _Game, gParticleSystemSettings _Settings) {
            lSettings = _Settings;
            lTargetElapsedTime = TimeSpan.FromTicks(333333);
            lParticleManager = new clsParticleManager(_Game);
            _Game.Components.Add(lParticleManager);
        }
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
        public void Init() {
        }
        private void LoadTextures(ContentManager _Content) {
            lExplosion = _Content.Load<Texture2D>(lSettings.pExplosionTexture);
            lFire = _Content.Load<Texture2D>(lSettings.pFireTexture);
            lSmoke = _Content.Load<Texture2D>(lSettings.pSmokeTexture);
        }
        private void InitializeSystem() {
            lExplosionSmokeParticleSystem = new clsExplosionSmokeParticleSystem(100, lSmoke);
            lFireParticleSystem = new clsFireParticleSystem(500, lFire);
            lSmokePlumeParticleSystem = new clsSmokeParticleSystem(0, lSmoke);
            lSmokePlumeEmitter = new clsSmokePlumeEmitter(Vector3.Zero, 0);
            lProjectileTrailParticleSystem = new clsProjectileTrailParticleSystem(500, lSmoke);
            lExplosionSmokeParticleSystem.AddAffector(new clsVelocityAffector(Vector3.Down));
            lExplosionParticleSystem = new clsExplosionParticleSystem(100, lExplosion);
            lExplosionParticleSystem.AddAffector(new clsVelocityAffector(Vector3.Down));
            lSmokeRingEmitter = new clsSmokeRingEmitter(Vector3.Zero, 0);
        }
        public void LoadContent(ContentManager _Content) {
            if(lSettings.pEnabled == true) {
                LoadTextures(_Content);
                InitializeSystem();
                if(lSettings.pExplosions == true) {
                    lParticleManager.AddParticleSystem(lExplosionSmokeParticleSystem, BlendState.NonPremultiplied);
                    lParticleManager.AddParticleSystem(lExplosionParticleSystem, BlendState.Additive);
                }
                if(lSettings.pFire == true) {
                    lParticleManager.AddParticleSystem(lFireParticleSystem, BlendState.Additive);
                }
                if(lSettings.pSmoke == true) {
                    lSmokePlumeParticleSystem.AddEmitter(lSmokePlumeEmitter);
                    lParticleManager.AddParticleSystem(lSmokePlumeParticleSystem, BlendState.NonPremultiplied);
                }
                if(lSettings.pProjectiles == true) {
                    lParticleManager.AddParticleSystem(lProjectileTrailParticleSystem, BlendState.NonPremultiplied);
                }
                if(lSettings.pSmokeRing == true) {
                    lSmokePlumeParticleSystem.AddEmitter(lSmokeRingEmitter);
                }
            }
        }
        public void Update(GameTime _GameTime, gParticleSystemSettings _Settings, clsDebugText _DebugText, clsGraphics _Graphics) {
            lSettings = _Settings;
            if(lSettings.pEnabled == true) {
                if(lSettings.pSmokeRing == true) {
                    lSmokeRingEmitter.EmissionRate = lSettings.pSmokeRingParticles;
                }
                if(lSettings.pFire == true) {
                    lFireParticleSystem.EmissionRate = lSettings.pFireRingSystemParticles;
                }
                if(lSettings.pSmoke == true) {
                    lSmokePlumeEmitter.EmissionRate = lSettings.pSmokePlumeParticles;
                }
                if(lSettings.pExplosions == true) {
                    UpdateExplosions(_GameTime);
                }
                if(lSettings.pProjectiles == true) {
                    UpdateProjectiles(_GameTime);
                }
                lParticleManager.SetMatrices(lView, lProjection);
            }
        }
        public void Draw(clsGame _Game) {
            if(lSettings.pEnabled == true) {
                //_Game.lDebugText.lText = _Game.lShip.lDirection.X.ToString() + " - " + _Game.lShip.lDirection.Y.ToString() + " - " + _Game.lShip.lDirection.Z.ToString();
                float lAspectRatio = (float)_Game.graphics.lGDM.GraphicsDevice.Viewport.Width / (float)_Game.graphics.lGDM.GraphicsDevice.Viewport.Height;
                lView = Matrix.CreateTranslation(0, -25, 0) * Matrix.CreateRotationY(MathHelper.ToRadians(lSettings.pCameraRotation)) * Matrix.CreateRotationX(MathHelper.ToRadians(lSettings.pCameraArc)) * Matrix.CreateLookAt(new Vector3(0, 0, - lSettings.pCameraDistance), new Vector3(0, 0, 0), Vector3.Up);
                //lProjection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, lAspectRatio, 1, 10000);
                //lView = Matrix.CreateTranslation(0, 0, 0) * Matrix.CreateRotationY(MathHelper.ToRadians(_Ship.lPosition.X)) * Matrix.CreateRotationX(MathHelper.ToRadians(_Ship.lPosition.Y)) * Matrix.CreateLookAt(new Vector3(0, 0, -lSettings.pCameraDistance), new Vector3(0, 0, 0), Vector3.Up);
                lProjection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, lAspectRatio, 1, 10000);
            }
        }
        private void UpdateExplosions(GameTime _GameTime) {
            lTimeToNextProjectile -= _GameTime.ElapsedGameTime;
            if(lTimeToNextProjectile <= TimeSpan.Zero) {
                lProjectiles.Add(new clsProjectile(lExplosionParticleSystem, lExplosionSmokeParticleSystem, lProjectileTrailParticleSystem));
                lTimeToNextProjectile += TimeSpan.FromSeconds(1);
            }
        }
        private void UpdateProjectiles(GameTime _GameTime) {
            int i = 0;
            while(i < lProjectiles.Count) {
                if(!lProjectiles[i].Update(_GameTime)) {
                    lProjectileTrailParticleSystem.RemoveEmitter(lProjectiles[i].lTrailEmitter);
                    lProjectiles.RemoveAt(i);
                } else {
                    i++;
                }
            }
        }

    }
}