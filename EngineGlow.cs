using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Engine glow effect for ship thrusters
    /// </summary>
    public class EngineGlow
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        
        private const float BaseGlowSize = 20.0f; // Base size of the glow sprite
        private const float MaxGlowSize = 40.0f;  // Max size at full intensity
        private const float GlowAspectRatio = 3.0f; // Glow is wider than it is tall

        public EngineGlow(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void DrawEngineGlows(Ship ship, Camera camera, float throttle)
        {
            if (ship.Model == null) return;
            
            // Show glow even with low throttle, just make it dimmer
            float minGlow = 0.2f; // Minimum idle glow
            float actualIntensity = Math.Max(minGlow, throttle);
            
            // Save current render states
            var oldBlendState = _graphicsDevice.BlendState;
            var oldDepthStencilState = _graphicsDevice.DepthStencilState;
            var oldRasterizerState = _graphicsDevice.RasterizerState;
            
            // Calculate engine positions relative to ship
            Vector3 shipPosition = ship.Position;
            
            // Apply the same model correction as in Ship.Draw
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            
            // Define engine positions in model space (larger offset behind ship)
            List<Vector3> engineOffsets = new List<Vector3>
            {
                new Vector3(-2.5f, 0f, 5f),  // Left engine (further behind and out)
                new Vector3(2.5f, 0f, 5f),   // Right engine (further behind and out)
                new Vector3(0f, 0f, 5.5f)    // Center engine glow (optional)
            };
            
            // Transform engine positions to world space
            Matrix shipTransform = modelCorrection * ship.Orientation * Matrix.CreateTranslation(shipPosition);
            
            // Set render states for glowing particles
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            
            foreach (var offset in engineOffsets)
            {
                Vector3 engineWorldPos = Vector3.Transform(offset, shipTransform);
                
                // Glow intensity based on throttle - much brighter colors
                float intensity = actualIntensity;
                Color glowColor = new Color(
                    0.2f + intensity * 1.0f,  // More red
                    0.4f + intensity * 0.8f,  // Some green
                    1.0f,                      // Full blue for engine glow
                    intensity * 0.9f           // High alpha
                );
                
                // Scale glow size to match 0.1x ship scale
                float size = (5f + intensity * 10f) * 0.1f; // Match ship's 0.1x scale
                
                DrawBillboard(engineWorldPos, size, glowColor, camera.Position);
            }
            
            // Restore render states
            _graphicsDevice.BlendState = oldBlendState;
            _graphicsDevice.DepthStencilState = oldDepthStencilState;
            _graphicsDevice.RasterizerState = oldRasterizerState;
        }

        private void DrawBillboard(Vector3 position, float size, Color color, Vector3 cameraPosition)
        {
            // Create billboard that always faces camera
            Vector3 forward = Vector3.Normalize(cameraPosition - position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));
            Vector3 up = Vector3.Cross(forward, right);
            
            // Create quad vertices
            VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[4];
            
            vertices[0] = new VertexPositionColorTexture(
                position + (-right - up) * size, color, new Vector2(0, 1));
            vertices[1] = new VertexPositionColorTexture(
                position + (right - up) * size, color, new Vector2(1, 1));
            vertices[2] = new VertexPositionColorTexture(
                position + (-right + up) * size, color, new Vector2(0, 0));
            vertices[3] = new VertexPositionColorTexture(
                position + (right + up) * size, color, new Vector2(1, 0));
            
            short[] indices = { 0, 1, 2, 2, 1, 3 };
            
            _effect.World = Matrix.Identity;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    vertices,
                    0,
                    4,
                    indices,
                    0,
                    2
                );
            }
        }
        
        private void DrawGlow(GraphicsDevice graphicsDevice, Camera camera, Vector3 enginePosition, float intensity, Color color)
        {
            // Draw the glow for a single engine
            // ? FIX: Reduce glow size by 50%
            float size = MathHelper.Lerp(BaseGlowSize, MaxGlowSize, intensity) * 0.5f;
            
            // Calculate screen position of the engine
            Vector3 screenPos = graphicsDevice.Viewport.Project(enginePosition, camera.Projection, camera.View, Matrix.Identity);
            
            // TODO: Implement actual glow drawing logic here using sprite batch or similar
        }
    }
}
