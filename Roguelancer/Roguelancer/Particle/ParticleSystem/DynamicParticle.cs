// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.ParticleSystem {
    public class DynamicParticle : IParticle {
        public Vector3 Position { get; set; }
        public Vector3 InitialPosition { get; internal set; }
        public Color Color { get; set; }
        public Color InitialColor { get; internal set; }
        public float Angle { get; set; }
        public float InitialAngle { get; internal set; }
        public float Scale { get; set; }
        public float InitialScale { get; internal set; }
        public Vector3? Velocity { get; set; }
        public Vector3? InitialVelocity { get; internal set; }
        public float? Rotation { get; set; }
        public float? InitialRotation { get; internal set; }
        public bool IsAffectable { get; set; }
        public TimeSpan? RemainingLifetime { get; set; }
        private TimeSpan? _lifespan;
        public TimeSpan? lifespan {
            get {
                return _lifespan;
            }
            internal set {
                _lifespan = value;
                RemainingLifetime = _lifespan;
            }
        }
        public float? Age {
            get {
                if(RemainingLifetime.HasValue && lifespan.HasValue) {
                    return (float)RemainingLifetime.Value.TotalSeconds / (float)lifespan.Value.TotalSeconds;
                } else {
                    return null;
                }
            }
        }
    }
}