using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roguelancer
{
    /// <summary>
    /// Simple 3D marker for visual reference
    /// </summary>
    public class Marker3D
    {
        public Vector3 Position { get; set; }
        public Color Color { get; set; }
        public float Size { get; set; }
        
        private VertexPositionColor[] _vertices;
        private BasicEffect _effect;
        private GraphicsDevice _device;
        
        public Marker3D(GraphicsDevice device, Vector3 position, Color color, float size = 100f)
        {
            _device = device;
            Position = position;
            Color = color;
            Size = size;
            
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            
            BuildMarker();
        }
        
        private void BuildMarker()
        {
            // Create a 3D cross marker (6 vertices = 3 lines)
            _vertices = new VertexPositionColor[6];
            
            // X axis line (red tinted)
            _vertices[0] = new VertexPositionColor(Position + new Vector3(-Size, 0, 0), Color);
            _vertices[1] = new VertexPositionColor(Position + new Vector3(Size, 0, 0), Color);
            
            // Y axis line (green tinted)
            _vertices[2] = new VertexPositionColor(Position + new Vector3(0, -Size, 0), Color);
            _vertices[3] = new VertexPositionColor(Position + new Vector3(0, Size, 0), Color);
            
            // Z axis line (blue tinted)
            _vertices[4] = new VertexPositionColor(Position + new Vector3(0, 0, -Size), Color);
            _vertices[5] = new VertexPositionColor(Position + new Vector3(0, 0, Size), Color);
        }
        
        public void Draw(Matrix view, Matrix projection)
        {
            _effect.World = Matrix.Identity;
            _effect.View = view;
            _effect.Projection = projection;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.LineList, _vertices, 0, 3);
            }
        }
    }
}
