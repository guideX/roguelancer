using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roguelancer
{
    /// <summary>
    /// Simple space object that can be targeted / navigated to.
    /// </summary>
    public class SpaceObject
    {
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public float Radius { get; set; } = 200f; // approximate size for arrival distance
        public Model Model { get; set; } // optional

        public SpaceObject(string name, Vector3 position, float radius = 200f)
        {
            Name = name;
            Position = position;
            Radius = radius;
        }
    }
}
