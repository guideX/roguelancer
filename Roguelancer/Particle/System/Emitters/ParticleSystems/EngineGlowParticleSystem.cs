using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Particle.System.Affectors;

namespace Roguelancer.Particle.System.Emitters.ParticleSystems {
    /// <summary>
    /// Engine Glow Particle System
    /// Creates a glowing engine trail effect when ships are accelerating
    /// </summary>
    public class EngineGlowParticleSystem : DynamicParticleSystem {
        private SpriteBatch _spriteBatch;
        
        /// <summary>
        /// Blend Mode for rendering (default is Additive for glowing effect)
        /// </summary>
        public BlendState BlendMode { get; set; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="maxParticles">Maximum number of particles</param>
        /// <param name="particleTexture">Texture for the particles</param>
        public EngineGlowParticleSystem(int maxParticles, Texture2D particleTexture) : base(maxParticles, particleTexture) {
            // Add fade out affector for smooth trailing effect
            AddAffector(new Fadeout());
            
            // Set blend state for additive blending (glowing effect)
            BlendMode = BlendState.Additive;
        }

        /// <summary>
        /// Initialize with graphics device
        /// </summary>
        public void Initialize() {
            // Initialization handled by base class
        }
        
        /// <summary>
        /// Initialize with graphics device
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering</param>
        public void Initialize(GraphicsDevice graphicsDevice) {
            _spriteBatch = new SpriteBatch(graphicsDevice);
        }
        
        /// <summary>
        /// Draw the particles
        /// </summary>
        /// <param name="view">View matrix</param>
        /// <param name="projection">Projection matrix</param>
        public void Draw(Matrix view, Matrix projection) {
            if (_spriteBatch == null || !Enabled || ParticleCount == 0) return;

            var graphicsDevice = _spriteBatch.GraphicsDevice;
            
            // Save current states
            var oldBlendState = graphicsDevice.BlendState;
            var oldDepthStencilState = graphicsDevice.DepthStencilState;
            
            // Set states for particle rendering
            graphicsDevice.BlendState = BlendMode;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendMode, SamplerState.LinearClamp, DepthStencilState.DepthRead, RasterizerState.CullNone);
            
            // Draw each particle
            foreach (var particle in LiveParticles) {
                // Project 3D position to 2D screen space
                var worldPos = new Vector3(particle.Position.X, particle.Position.Y, particle.Position.Z);
                var screenPos = graphicsDevice.Viewport.Project(worldPos, projection, view, Matrix.Identity);
                
                // Only draw if in front of camera
                if (screenPos.Z >= 0 && screenPos.Z <= 1) {
                    var screenPos2D = new Vector2(screenPos.X, screenPos.Y);
                    var scale = particle.Scale;
                    
                    // Adjust scale based on distance for depth perception
                    scale *= (1.0f - screenPos.Z * 0.5f);
                    
                    _spriteBatch.Draw(
                        Texture,
                        screenPos2D,
                        null,
                        particle.Color,
                        particle.Angle,
                        TextureOrigin,
                        scale,
                        SpriteEffects.None,
                        0
                    );
                }
            }
            
            _spriteBatch.End();
            
            // Restore states
            graphicsDevice.BlendState = oldBlendState;
            graphicsDevice.DepthStencilState = oldDepthStencilState;
        }

        /// <summary>
        /// Add an engine glow particle
        /// </summary>
        /// <param name="position">Position of the engine</param>
        /// <param name="direction">Direction the ship is facing</param>
        /// <param name="thrust">Current thrust level (0.0 to 1.0)</param>
        /// <param name="engineColor">Color of the engine glow</param>
        public void AddEngineGlow(Vector3 position, Vector3 direction, float thrust, Color engineColor) {
            if (thrust <= 0) return;

            // Scale particle properties based on thrust
            var velocity = -direction * thrust * RandomHelper.FloatBetween(0.5f, 1.5f);
            var scale = thrust * RandomHelper.FloatBetween(0.15f, 0.35f);
            var lifetime = TimeSpan.FromSeconds(RandomHelper.FloatBetween(0.1f, 0.3f));
            
            // Add some variation to the position for a more organic look
            var positionVariation = new Vector3(
                RandomHelper.FloatBetween(-0.3f, 0.3f),
                RandomHelper.FloatBetween(-0.3f, 0.3f),
                RandomHelper.FloatBetween(-0.3f, 0.3f)
            );

            // Interpolate color based on thrust (hotter = more thrust)
            var particleColor = Color.Lerp(
                new Color(engineColor.R / 2, engineColor.G / 2, engineColor.B / 2),
                engineColor,
                thrust
            );

            AddParticle(
                position + positionVariation,
                particleColor,
                velocity,
                RandomHelper.FloatBetween(-0.05f, 0.05f),
                lifetime,
                true,
                RandomHelper.FloatBetween(0.0f, MathHelper.TwoPi),
                scale
            );
        }

        /// <summary>
        /// Add afterburner glow (more intense effect)
        /// </summary>
        /// <param name="position">Position of the engine</param>
        /// <param name="direction">Direction the ship is facing</param>
        /// <param name="thrust">Current thrust level</param>
        public void AddAfterburnerGlow(Vector3 position, Vector3 direction, float thrust) {
            if (thrust <= 0) return;

            // Afterburner creates more particles with blue-white color
            var velocity = -direction * thrust * RandomHelper.FloatBetween(1.0f, 2.5f);
            var scale = thrust * RandomHelper.FloatBetween(0.2f, 0.5f);
            var lifetime = TimeSpan.FromSeconds(RandomHelper.FloatBetween(0.15f, 0.4f));
            
            var positionVariation = new Vector3(
                RandomHelper.FloatBetween(-0.5f, 0.5f),
                RandomHelper.FloatBetween(-0.5f, 0.5f),
                RandomHelper.FloatBetween(-0.5f, 0.5f)
            );

            // Bright blue-white for afterburner
            var particleColor = Color.Lerp(
                new Color(100, 150, 255),
                new Color(200, 220, 255),
                RandomHelper.FloatBetween(0f, 1f)
            );

            AddParticle(
                position + positionVariation,
                particleColor,
                velocity,
                RandomHelper.FloatBetween(-0.1f, 0.1f),
                lifetime,
                true,
                RandomHelper.FloatBetween(0.0f, MathHelper.TwoPi),
                scale
            );
        }
    }
}
