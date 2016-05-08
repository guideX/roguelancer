// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    /// <summary>
    /// Random Helper
    /// </summary>
    public static class RandomHelper {
        /// <summary>
        /// Random
        /// </summary>
        private static Random _random = new Random();
        /// <summary>
        /// Rnd
        /// </summary>
        public static Random Rnd {
            get { return _random; }
        }
        /// <summary>
        /// Float
        /// </summary>
        /// <returns></returns>
        public static float Float() {
            return (float)Rnd.NextDouble();
        }
        /// <summary>
        /// Float Between
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float FloatBetween(float min, float max) {
            return min + (float)(Rnd.NextDouble() * (max - min));
        }
        /// <summary>
        /// Int Between
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int IntBetween(int min, int max) {
            return min + (int)(Rnd.NextDouble() * (max - min));
        }
        /// <summary>
        /// Boolean
        /// </summary>
        /// <returns></returns>
        public static bool Boolean() {
            return Rnd.Next(2) == 0;
        }
        /// <summary>
        /// Vector3 Between
        /// </summary>
        /// <param name="_Min"></param>
        /// <param name="_Max"></param>
        /// <returns></returns>
        public static Vector3 Vector3Between(Vector3 _Min, Vector3 _Max) {
            return Vector3.Lerp(_Min, _Max, Float());
        }
        /// <summary>
        /// Normalized Vector 3
        /// </summary>
        /// <returns></returns>
        public static Vector3 NormalizedVector3() {
            Vector3 vector = new Vector3(FloatBetween(-1.0f, 1.0f), FloatBetween(-1.0f, 1.0f), FloatBetween(-1.0f, 1.0f));
            vector.Normalize();
            return vector;
        }
        /// <summary>
        /// Color
        /// </summary>
        /// <returns></returns>
        public static Color Color() {
            return new Color(Float(), Float(), Float());
        }
        /// <summary>
        /// Color with Alpha
        /// </summary>
        /// <returns></returns>
        public static Color ColorWithAlpha() {
            return new Color(Float(), Float(), Float(), Float());
        }
        /// <summary>
        /// Color Between
        /// </summary>
        /// <param name="_Min"></param>
        /// <param name="_Max"></param>
        /// <returns></returns>
        public static Color ColorBetween(Color _Min, Color _Max) {
            return Microsoft.Xna.Framework.Color.Lerp(_Min, _Max, Float());
        }
    }
}