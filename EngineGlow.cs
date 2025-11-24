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
        private Texture2D _glowTexture;
        
        private struct GlowQuad
        {
            public Vector3 Position;
            public float Size;
            public Color Color;
        }

        public EngineGlow(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            
            // Create a simple glow texture
            _glowTexture = CreateGlowTexture(graphicsDevice, 64);
            _effect.Texture = _glowTexture;
        }

        private Texture2D CreateGlowTexture(GraphicsDevice device, int size)
        {
            Texture2D texture = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center) / (size / 2f);
                    
                    // Create radial gradient
                    float alpha = Math.Max(0, 1f - distance);
                    alpha = (float)Math.Pow(alpha, 2); // Softer falloff
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetData(data);
            return texture;
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
                
                // Much larger size - was 1.5-3, now 5-15
                float size = 5f + intensity * 10f;
                
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
    }
}
