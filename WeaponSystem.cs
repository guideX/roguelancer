using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Manages weapon projectiles (blaster bolts) and rendering
    /// </summary>
    public class WeaponSystem
    {
        private class Projectile
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Size;
        }
        
        private class MuzzleFlash
        {
            public Vector3 Position;
            public float Life;
            public float MaxLife;
            public float Size;
        }
        
        private readonly List<Projectile> _projectiles = new List<Projectile>(256);
        private readonly List<MuzzleFlash> _muzzleFlashes = new List<MuzzleFlash>(8);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Texture2D _texture;
        
        // Weapon stats
        public float ProjectileSpeed = 80000f; // Very fast blaster bolts
        public float ProjectileLife = 2.0f; // 2 second lifespan
        public float ProjectileSize = 8f; // Bolt size
        public Color ProjectileColor = new Color(255, 100, 50); // Orange-red blaster
        
        // Muzzle flash
        private float MuzzleFlashLife = 0.08f; // Quick flash
        private float MuzzleFlashSize = 15f;
        private Color MuzzleFlashColor = new Color(255, 255, 255, 128); // White flash with alpha
        
        // Batching buffers
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        
        public WeaponSystem(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            _texture = CreateBlasterTexture(graphicsDevice, 16);
            _effect.Texture = _texture;
        }
        
        private Texture2D CreateBlasterTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxDist = size / 2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    // Sharp bright center for blaster bolt look
                    float a = 1f - d;
                    a = (float)Math.Pow(a, 4); // Very sharp falloff
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }
        
        public void Fire(Vector3 origin, Vector3 direction, Vector3 shipVelocity)
        {
            Projectile projectile = new Projectile
            {
                Position = origin,
                Velocity = direction * ProjectileSpeed + shipVelocity, // Inherit ship velocity
                Life = 0f,
                MaxLife = ProjectileLife,
                Color = ProjectileColor,
                Size = ProjectileSize
            };
            
            _projectiles.Add(projectile);
            
            // Add muzzle flash at gun position
            MuzzleFlash flash = new MuzzleFlash
            {
                Position = origin,
                Life = 0f,
                MaxLife = MuzzleFlashLife,
                Size = MuzzleFlashSize
            };
            _muzzleFlashes.Add(flash);
            
            Console.WriteLine($"?? Blaster fired! Projectiles: {_projectiles.Count}, Flashes: {_muzzleFlashes.Count}");
        }
        
        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update and remove old projectiles
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = _projectiles[i];
                p.Life += dt;
                
                if (p.Life >= p.MaxLife)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }
                
                p.Position += p.Velocity * dt;
            }
            
            // Update muzzle flashes (fade quickly)
            for (int i = _muzzleFlashes.Count - 1; i >= 0; i--)
            {
                MuzzleFlash flash = _muzzleFlashes[i];
                flash.Life += dt;
                
                if (flash.Life >= flash.MaxLife)
                {
                    _muzzleFlashes.RemoveAt(i);
                }
            }
        }
        
        public void Draw(Camera camera)
        {
            // Draw projectiles
            DrawProjectiles(camera);
            
            // Draw muzzle flashes
            DrawMuzzleFlashes(camera);
        }
        
        private void DrawProjectiles(Camera camera)
        {
            int count = _projectiles.Count;
            if (count == 0) return;
            
            // Ensure buffers sized
            int requiredVertices = count * 4;
            int requiredIndices = count * 6;
            if (_vertexBuffer.Length < requiredVertices) _vertexBuffer = new VertexPositionColorTexture[requiredVertices];
            if (_indexBuffer.Length < requiredIndices) _indexBuffer = new short[requiredIndices];
            
            Vector3 camPos = camera.Position;
            
            // Build billboards
            for (int i = 0; i < count; i++)
            {
                Projectile p = _projectiles[i];
                
                // Age-based fade
                float t = p.Life / p.MaxLife;
                float alpha = (1f - t); // Fade out over time
                Color color = p.Color * alpha;
                
                // Stretch bolt based on velocity (motion blur effect)
                Vector3 forward = camPos - p.Position;
                if (forward.LengthSquared() < 0.00001f) forward = Vector3.Forward;
                forward.Normalize();
                
                Vector3 right = Vector3.Cross(Vector3.Up, forward);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right;
                else right.Normalize();
                
                Vector3 up = Vector3.Cross(forward, right);
                
                // Make bolt elongated in direction of travel
                float sizeX = p.Size;
                float sizeY = p.Size * 3f; // 3x longer for bolt shape
                
                // Build quad
                int vBase = i * 4;
                int iBase = i * 6;
                
                _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(p.Position + (-right * sizeX - up * sizeY), color, new Vector2(0, 1));
                _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(p.Position + (right * sizeX - up * sizeY), color, new Vector2(1, 1));
                _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(p.Position + (-right * sizeX + up * sizeY), color, new Vector2(0, 0));
                _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(p.Position + (right * sizeX + up * sizeY), color, new Vector2(1, 0));
                
                _indexBuffer[iBase + 0] = (short)(vBase + 0);
                _indexBuffer[iBase + 1] = (short)(vBase + 1);
                _indexBuffer[iBase + 2] = (short)(vBase + 2);
                _indexBuffer[iBase + 3] = (short)(vBase + 2);
                _indexBuffer[iBase + 4] = (short)(vBase + 1);
                _indexBuffer[iBase + 5] = (short)(vBase + 3);
            }
            
            // Save render states
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];
            
            // Additive blending for bright bolts
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertexBuffer, 0, count * 4,
                    _indexBuffer, 0, count * 2
                );
            }
            
            // Restore render states
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }
        
        private void DrawMuzzleFlashes(Camera camera)
        {
            int count = _muzzleFlashes.Count;
            if (count == 0) return;
            
            // Ensure buffers sized
            int requiredVertices = count * 4;
            int requiredIndices = count * 6;
            if (_vertexBuffer.Length < requiredVertices) _vertexBuffer = new VertexPositionColorTexture[requiredVertices];
            if (_indexBuffer.Length < requiredIndices) _indexBuffer = new short[requiredIndices];
            
            Vector3 camPos = camera.Position;
            
            // Build billboards
            for (int i = 0; i < count; i++)
            {
                MuzzleFlash flash = _muzzleFlashes[i];
                
                // Quick fade
                float t = flash.Life / flash.MaxLife;
                float alpha = 1f - t;
                Color color = new Color(255, 200, 100) * alpha; // Bright yellow-white flash
                
                Vector3 forward = camPos - flash.Position;
                if (forward.LengthSquared() < 0.00001f) forward = Vector3.Forward;
                forward.Normalize();
                
                Vector3 right = Vector3.Cross(Vector3.Up, forward);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right;
                else right.Normalize();
                
                Vector3 up = Vector3.Cross(forward, right);
                
                float size = flash.Size * (1f + t * 0.5f); // Expands slightly
                
                // Build quad
                int vBase = i * 4;
                int iBase = i * 6;
                
                _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(flash.Position + (-right - up) * size, color, new Vector2(0, 1));
                _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(flash.Position + (right - up) * size, color, new Vector2(1, 1));
                _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(flash.Position + (-right + up) * size, color, new Vector2(0, 0));
                _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(flash.Position + (right + up) * size, color, new Vector2(1, 0));
                
                _indexBuffer[iBase + 0] = (short)(vBase + 0);
                _indexBuffer[iBase + 1] = (short)(vBase + 1);
                _indexBuffer[iBase + 2] = (short)(vBase + 2);
                _indexBuffer[iBase + 3] = (short)(vBase + 2);
                _indexBuffer[iBase + 4] = (short)(vBase + 1);
                _indexBuffer[iBase + 5] = (short)(vBase + 3);
            }
            
            // Save render states
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];
            
            // Additive blending for bright flash
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertexBuffer, 0, count * 4,
                    _indexBuffer, 0, count * 2
                );
            }
            
            // Restore render states
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }
    }
}
