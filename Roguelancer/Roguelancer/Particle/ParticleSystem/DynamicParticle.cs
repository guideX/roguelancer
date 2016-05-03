// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.ParticleSystem {
    /// <summary>
    /// Dynamic Particle
    /// </summary>
    public class DynamicParticle : IParticle {
        /// <summary>
        /// Position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Initial Position
        /// </summary>
        public Vector3 InitialPosition { get; internal set; }
        /// <summary>
        /// Color
        /// </summary>
        public Color Color { get; set; }
        /// <summary>
        /// Initial Color
        /// </summary>
        public Color InitialColor { get; internal set; }
        /// <summary>
        /// Angle
        /// </summary>
        public float Angle { get; set; }
        /// <summary>
        /// Initial Angle
        /// </summary>
        public float InitialAngle { get; internal set; }
        /// <summary>
        /// Scale
        /// </summary>
        public float Scale { get; set; }
        /// <summary>
        /// Initial Scale
        /// </summary>
        public float InitialScale { get; internal set; }
        /// <summary>
        /// Velocity
        /// </summary>
        public Vector3? Velocity { get; set; }
        /// <summary>
        /// Initial Velocity
        /// </summary>
        public Vector3? InitialVelocity { get; internal set; }
        /// <summary>
        /// Rotation
        /// </summary>
        public float? Rotation { get; set; }
        /// <summary>
        /// Initial Rotation
        /// </summary>
        public float? InitialRotation { get; internal set; }
        /// <summary>
        /// Is Affectable
        /// </summary>
        public bool IsAffectable { get; set; }
        /// <summary>
        /// Remaining Lifetime
        /// </summary>
        public TimeSpan? RemainingLifetime { get; set; }
        /// <summary>
        /// Life Span
        /// </summary>
        private TimeSpan? _lifespan;
        /// <summary>
        /// Life Span
        /// </summary>
        public TimeSpan? lifespan {
            get {
                return _lifespan;
            }
            internal set {
                _lifespan = value;
                RemainingLifetime = _lifespan;
            }
        }
        /// <summary>
        /// Age
        /// </summary>
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