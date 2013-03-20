// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
namespace Rougelancer.Particle.ParticleSystem {
    public class clsDynamicParticle : clsIParticle {
        public Vector3 lPosition { get; set; }
        public Vector3 lInitialPosition { get; internal set; }
        public Color lColor { get; set; }
        public Color lInitialColor { get; internal set; }
        public float lAngle { get; set; }
        public float lInitialAngle { get; internal set; }
        public float lScale { get; set; }
        public float InitialScale { get; internal set; }
        public Vector3? Velocity { get; set; }
        public Vector3? InitialVelocity { get; internal set; }
        public float? Rotation { get; set; }
        public float? InitialRotation { get; internal set; }
        public bool IsAffectable { get; set; }
        private TimeSpan? lifespan;
        public TimeSpan? RemainingLifetime { get; set; }
        public TimeSpan? Lifespan {
            get {
                return lifespan;
            }
            internal set {
                lifespan = value;
                RemainingLifetime = lifespan;
            }
        }
        public float? Age {
            get {
                if(RemainingLifetime.HasValue && Lifespan.HasValue) {
                    return (float)RemainingLifetime.Value.TotalSeconds / (float)Lifespan.Value.TotalSeconds;
                } else {
                    return null;
                }
            }
        }
    }
}