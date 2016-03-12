// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Particle System
    /// </summary>
    public interface IParticleSystem {
        /// <summary>
        /// Texture
        /// </summary>
        Texture2D Texture { get; }
        /// <summary>
        /// Texture Origin
        /// </summary>
        Vector2 TextureOrigin { get; }
        /// <summary>
        /// Enabled
        /// </summary>
        bool Enabled { get; }
        /// <summary>
        /// Capacity
        /// </summary>
        int Capacity { get; }
        /// <summary>
        /// Particle Count
        /// </summary>
        int ParticleCount { get; }
        /// <summary>
        /// This
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        IParticle this[int index] { get; }
        /// <summary>
        /// Remove At
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        bool RemoveAt(int index);
        /// <summary>
        /// Clear
        /// </summary>
        void Clear();
    }
}