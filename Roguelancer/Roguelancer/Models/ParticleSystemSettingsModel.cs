// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
namespace Roguelancer.Models {
    public class ParticleSystemSettingsModel {
        public int SmokePlumeParticles { get; set; }
        public int SmokeRingParticles { get; set; }
        public int FireRingSystemParticles { get; set; }
        public bool Enabled { get; set; }
        public bool Smoke { get; set; }
        public bool SmokeRing { get; set; }
        public bool Fire { get; set; }
        public bool Explosions { get; set; }
        public bool Projectiles { get; set; }
        public float CameraArc { get; set; }
        public float CameraRotation { get; set; }
        public float CameraDistance { get; set; }
        public string ExplosionTexture { get; set; }
        public string FireTexture { get; set; }
        public string SmokeTexture { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 Rotation { get; set; }
    }
}