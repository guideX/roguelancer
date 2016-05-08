// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
namespace Roguelancer.Models {
    /// <summary>
    /// Particle System Settings Model
    /// </summary>
    public class ParticleSystemSettingsModel {
        /// <summary>
        /// Smoke Plume Particles
        /// </summary>
        public int SmokePlumeParticles { get; set; }
        /// <summary>
        /// Smoke Ring Particles
        /// </summary>
        public int SmokeRingParticles { get; set; }
        /// <summary>
        /// Fire Ring System Particles
        /// </summary>
        public int FireRingSystemParticles { get; set; }
        /// <summary>
        /// Enabled
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// Smoke
        /// </summary>
        public bool Smoke { get; set; }
        /// <summary>
        /// Smoke Ring
        /// </summary>
        public bool SmokeRing { get; set; }
        /// <summary>
        /// Fire
        /// </summary>
        public bool Fire { get; set; }
        /// <summary>
        /// Explosions
        /// </summary>
        public bool Explosions { get; set; }
        /// <summary>
        /// Projectiles
        /// </summary>
        public bool Projectiles { get; set; }
        /// <summary>
        /// Camera Arc
        /// </summary>
        public float CameraArc { get; set; }
        /// <summary>
        /// Camera Rotation
        /// </summary>
        public float CameraRotation { get; set; }
        /// <summary>
        /// Camera Distance
        /// </summary>
        public float CameraDistance { get; set; }
        /// <summary>
        /// Explosion Texture
        /// </summary>
        public string ExplosionTexture { get; set; }
        /// <summary>
        /// Fire Texture
        /// </summary>
        public string FireTexture { get; set; }
        /// <summary>
        /// Smoke Texture
        /// </summary>
        public string SmokeTexture { get; set; }
        /// <summary>
        /// Position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Rotation
        /// </summary>
        public Vector2 Rotation { get; set; }
    }
}