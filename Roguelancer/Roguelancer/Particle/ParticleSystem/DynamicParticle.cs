// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    public class DynamicParticle : IParticle {
        public Vector3 position { get; set; }
        public Vector3 initialPosition { get; internal set; }
        public Color color { get; set; }
        public Color initialColor { get; internal set; }
        public float angle { get; set; }
        public float initialAngle { get; internal set; }
        public float scale { get; set; }
        public float initialScale { get; internal set; }
        public Vector3? velocity { get; set; }
        public Vector3? initialVelocity { get; internal set; }
        public float? Rotation { get; set; }
        public float? InitialRotation { get; internal set; }
        public bool IsAffectable { get; set; }
        public TimeSpan? remainingLifetime { get; set; }
        private TimeSpan? _lifespan;
        public TimeSpan? lifespan {
            get {
                return _lifespan;
            }
            internal set {
                _lifespan = value;
                remainingLifetime = _lifespan;
            }
        }
        public float? Age {
            get {
                if(remainingLifetime.HasValue && lifespan.HasValue) {
                    return (float)remainingLifetime.Value.TotalSeconds / (float)lifespan.Value.TotalSeconds;
                } else {
                    return null;
                }
            }
        }
    }
}