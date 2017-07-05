

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Models.Particle {
    /// <summary>
    /// Particle Star Sheet Model
    /// </summary>
    public class ParticleStarSheetModel {
        /// <summary>
        /// Verts
        /// </summary>
        public VertexPositionTexture[] Verts { get; set; }
        /// <summary>
        /// Vertex Color Array
        /// </summary>
        public Color[] VertexColorArray { get; set; }
        /// <summary>
        /// Particle Vertex Buffer
        /// </summary>
        public VertexBuffer ParticleVertexBuffer { get; set; }
        /// <summary>
        /// Max Position
        /// </summary>
        public Vector3 MaxPosition { get; set; }
        /// <summary>
        /// Max Particles
        /// </summary>
        public int MaxParticles { get; set; }
        /// <summary>
        /// Rnd
        /// </summary>
        //public static Random RRnd = new Random();
        public Random Rnd { get; set; }
        /// <summary>
        /// Particle Effect
        /// </summary>
        public Effect ParticleEffect;
        /// <summary>
        /// Particle Colors texture
        /// </summary>
        public Texture2D ParticleColorsTexture;
        /// <summary>
        /// Particle Star Sheet Model
        /// </summary>
        public ParticleStarSheetModel() {
            Rnd = new Random();
        }
    }
}