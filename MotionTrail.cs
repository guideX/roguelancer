using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Breadcrumb trail showing where the ship has been
    /// </summary>
    public class MotionTrail
    {
        private class Breadcrumb
        {
            public Vector3 Position;
            public float Age;
            public float MaxAge = 10f; // 10 seconds
        }
        
        private readonly List<Breadcrumb> _breadcrumbs = new List<Breadcrumb>();
        private readonly GraphicsDevice _device;
        private readonly BasicEffect _effect;
        private float _spawnTimer = 0f;
        private const float SpawnInterval = 0.5f; // Every 0.5 seconds
        
        public MotionTrail(GraphicsDevice device)
        {
            _device = device;
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }
        
        public void Update(float deltaTime, Vector3 shipPosition, float shipSpeed)
        {
            // Age and remove old breadcrumbs
            for (int i = _breadcrumbs.Count - 1; i >= 0; i--)
            {
                _breadcrumbs[i].Age += deltaTime;
                if (_breadcrumbs[i].Age >= _breadcrumbs[i].MaxAge)
                {
                    _breadcrumbs.RemoveAt(i);
                }
            }
            
            // Spawn new breadcrumb if moving
            if (shipSpeed > 1f)
            {
                _spawnTimer += deltaTime;
                if (_spawnTimer >= SpawnInterval)
                {
                    _spawnTimer = 0f;
                    _breadcrumbs.Add(new Breadcrumb { Position = shipPosition, Age = 0f });
                }
            }
        }
        
        public void Draw(Matrix view, Matrix projection)
        {
            if (_breadcrumbs.Count < 2) return;
            
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            
            // Draw lines connecting breadcrumbs
            VertexPositionColor[] vertices = new VertexPositionColor[_breadcrumbs.Count * 2 - 2];
            
            for (int i = 0; i < _breadcrumbs.Count - 1; i++)
            {
                float fade = 1f - (_breadcrumbs[i].Age / _breadcrumbs[i].MaxAge);
                Color color = Color.Yellow * fade;
                
                vertices[i * 2] = new VertexPositionColor(_breadcrumbs[i].Position, color);
                vertices[i * 2 + 1] = new VertexPositionColor(_breadcrumbs[i + 1].Position, color);
            }
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
            }
        }
    }
}
