// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Rougelancer.Particle.ParticleSystem;
namespace Rougelancer.Particle.System {
    public class clsParticleManager : DrawableGameComponent {
        public readonly BlendState DefaultBlendState = BlendState.NonPremultiplied;
        private readonly Matrix InvertY = Matrix.CreateScale(1, -1, 1);
        private SpriteBatch spriteBatch;
        private BasicEffect basicEffect;
        private Dictionary<BlendState, List<clsIParticleSystem>> particleSystems = new Dictionary<BlendState, List<clsIParticleSystem>>();
        private Matrix View { get; set; }
        private Matrix Projection { get; set; }
        public int ParticleCount {
            get {
                int total = 0;
                foreach(List<clsIParticleSystem> particleSystemBatch in particleSystems.Values) {
                    foreach(clsIParticleSystem particleSystem in particleSystemBatch) {
                        if(particleSystem.Enabled) {
                            total += particleSystem.ParticleCount;
                        }
                    }
                }
                return total;
            }
        }
        public clsParticleManager(Game game) : base(game) {
        }
        protected override void LoadContent() {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            basicEffect = new BasicEffect(GraphicsDevice) {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false,
                World = InvertY,
                View = Matrix.Identity
            };
            base.LoadContent();
        }
        public override void Update(GameTime gameTime) {
            foreach(List<clsIParticleSystem> particleSystemBatch in particleSystems.Values) {
                foreach(clsIParticleSystem particleSystem in particleSystemBatch) {
                    if(particleSystem.Enabled) {
                        clsDynamicParticleSystem dynamicParticleSystem = particleSystem as clsDynamicParticleSystem;
                        if(dynamicParticleSystem != null) {
                            dynamicParticleSystem.Update(gameTime);
                        }
                    }
                }
            }
            base.Update(gameTime);
        }
        public override void Draw(GameTime gameTime) {
            basicEffect.Projection = Projection;
            foreach(KeyValuePair<BlendState, List<clsIParticleSystem>> particleSystemBatch in particleSystems) {
                spriteBatch.Begin(0, particleSystemBatch.Key, null, DepthStencilState.DepthRead, RasterizerState.CullNone, basicEffect);
                foreach(clsIParticleSystem particleSystem in particleSystemBatch.Value) {
                    if(particleSystem.Enabled) {
                        for(int i = 0; i < particleSystem.ParticleCount; i++) {
                            clsIParticle particle = particleSystem[i];
                            Vector3 viewSpacePosition = Vector3.Transform(particle.lPosition, View);
                            spriteBatch.Draw(particleSystem.Texture, new Vector2(viewSpacePosition.X, viewSpacePosition.Y), null, particle.lColor, particle.lAngle, particleSystem.TextureOrigin, particle.lScale, 0, viewSpacePosition.Z);
                        }
                    }
                }
                spriteBatch.End();
            }
            base.Draw(gameTime);
        }
        public void AddParticleSystem(clsIParticleSystem particleSystem) {
            AddParticleSystem(particleSystem, DefaultBlendState);
        }
        public void AddParticleSystem(clsIParticleSystem particleSystem, BlendState blendState) {
            if(particleSystems.ContainsKey(blendState)) {
                particleSystems[blendState].Add(particleSystem);
            } else {
                List<clsIParticleSystem> particleSystemBatch = new List<clsIParticleSystem>();
                particleSystemBatch.Add(particleSystem);
                particleSystems.Add(blendState, particleSystemBatch);
            }
        }
        public void SetMatrices(Matrix view, Matrix projection) {
            View = view;
            Projection = projection;
            //View = view * InvertY;
            //Projection = projection;
        }
    }
}