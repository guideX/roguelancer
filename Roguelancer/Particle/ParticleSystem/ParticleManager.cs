

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.ParticleSystem {
    /// <summary>
    /// Particle Manager
    /// </summary>
    public class ParticleManager : DrawableGameComponent {
        /// <summary>
        /// Default Blend State
        /// </summary>
        public readonly BlendState DefaultBlendState = BlendState.NonPremultiplied;
        /// <summary>
        /// InvertY
        /// </summary>
        private readonly Matrix InvertY = Matrix.CreateScale(1, -1, 1);
        /// <summary>
        /// Sprite Batch
        /// </summary>
        private SpriteBatch _spriteBatch;
        /// <summary>
        /// Basic Effect
        /// </summary>
        private BasicEffect basicEffect;
        /// <summary>
        /// Particle Systems
        /// </summary>
        private Dictionary<BlendState, List<IParticleSystem>> particleSystems = new Dictionary<BlendState, List<IParticleSystem>>();
        /// <summary>
        /// View
        /// </summary>
        private Matrix View { get; set; }
        /// <summary>
        /// Projection
        /// </summary>
        private Matrix Projection { get; set; }
        /// <summary>
        /// Particle Count
        /// </summary>
        public int ParticleCount {
            get {
                int total = 0;
                foreach(List<IParticleSystem> particleSystemBatch in particleSystems.Values) {
                    foreach(IParticleSystem particleSystem in particleSystemBatch) {
                        if(particleSystem.Enabled) {
                            total += particleSystem.ParticleCount;
                        }
                    }
                }
                return total;
            }
        }
        /// <summary>
        /// Particle Manager
        /// </summary>
        /// <param name="game"></param>
        public ParticleManager(Game game) : base(game) {
        }
        /// <summary>
        /// Load Content
        /// </summary>
        protected override void LoadContent() {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            basicEffect = new BasicEffect(GraphicsDevice) {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false,
                World = InvertY,
                View = Matrix.Identity
            };
            base.LoadContent();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime) {
            foreach(List<IParticleSystem> particleSystemBatch in particleSystems.Values) {
                foreach(IParticleSystem particleSystem in particleSystemBatch) {
                    if(particleSystem.Enabled) {
                        DynamicParticleSystem dynamicParticleSystem = particleSystem as DynamicParticleSystem;
                        if(dynamicParticleSystem != null) {
                            dynamicParticleSystem.Update(gameTime);
                        }
                    }
                }
            }
            base.Update(gameTime);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime) {
            basicEffect.Projection = Projection;
            foreach(KeyValuePair<BlendState, List<IParticleSystem>> particleSystemBatch in particleSystems) {
                _spriteBatch.Begin(0, particleSystemBatch.Key, null, DepthStencilState.DepthRead, RasterizerState.CullNone, basicEffect);
                foreach(IParticleSystem particleSystem in particleSystemBatch.Value) {
                    if (particleSystem.Enabled) {
                        for (var i = 0; i < particleSystem.ParticleCount; i++) {
                            var particle = particleSystem[i];
                            var viewSpacePosition = Vector3.Transform(particle.Position, View);
                            _spriteBatch.Draw(particleSystem.Texture, new Vector2(viewSpacePosition.X, viewSpacePosition.Y), null, particle.Color, particle.Angle, particleSystem.TextureOrigin, particle.Scale, 0, viewSpacePosition.Z);
                        }
                    }
                }
                _spriteBatch.End();
            }
            base.Draw(gameTime);
        }
        /// <summary>
        /// Add Particle System
        /// </summary>
        /// <param name="particleSystem"></param>
        public void AddParticleSystem(IParticleSystem particleSystem) {
            AddParticleSystem(particleSystem, DefaultBlendState);
        }
        /// <summary>
        /// Add Particle System
        /// </summary>
        /// <param name="particleSystem"></param>
        /// <param name="blendState"></param>
        public void AddParticleSystem(IParticleSystem particleSystem, BlendState blendState) {
            if (particleSystems.ContainsKey(blendState)) {
                particleSystems[blendState].Add(particleSystem);
            } else {
                var batch = new List<IParticleSystem>();
                batch.Add(particleSystem);
                particleSystems.Add(blendState, batch);
            }
        }
        /// <summary>
        /// Set Matrices
        /// </summary>
        /// <param name="view"></param>
        /// <param name="projection"></param>
        public void SetMatrices(Matrix view, Matrix projection) {
            View = view;
            Projection = projection;
        }
    }
}