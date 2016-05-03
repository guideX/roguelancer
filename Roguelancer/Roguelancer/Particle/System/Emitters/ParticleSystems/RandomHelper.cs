// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    /// <summary>
    /// Random Helper
    /// </summary>
    public static class RandomHelper {
        /// <summary>
        /// Rnd
        /// </summary>
        private static Random _rnd = new Random();
        /// <summary>
        /// Rnd
        /// </summary>
        public static Random Rnd {
            get { return _rnd; }
        }
        /// <summary>
        /// Float
        /// </summary>
        /// <returns></returns>
        public static float? Float() {
            float f;
            if (float.TryParse(_rnd.NextDouble().ToString(), out f)) {
                return f;
            } else {
                return null;
            }
        }
        /// <summary>
        /// Float Between
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float FloatBetween(float min, float max) {
            return min + (float)(_rnd.NextDouble() * (max - min));
        }
        /// <summary>
        /// In Between
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int IntBetween(int min, int max) {
            return min + (int)(_rnd.NextDouble() * (max - min));
        }
        /// <summary>
        /// Boolean
        /// </summary>
        /// <returns></returns>
        public static bool Boolean() {
            return _rnd.Next(2) == 0;
        }
        /// <summary>
        /// Vector3 Between
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static Vector3? Vector3Between(Vector3 _Min, Vector3 _Max) {
            var f = Float();
            if (f != null) {
                return Vector3.Lerp(_Min, _Max, f.Value);
            } else {
                return null;
            }
        }
        /// <summary>
        /// Normalized Vector3
        /// </summary>
        /// <returns></returns>
        public static Vector3 NormalizedVector3() {
            var vector = new Vector3(FloatBetween(-1.0f, 1.0f), FloatBetween(-1.0f, 1.0f), FloatBetween(-1.0f, 1.0f));
            vector.Normalize();
            return vector;
        }
        /// <summary>
        /// Color
        /// </summary>
        /// <returns></returns>
        public static Color? Color() {
            var f1 = Float(); var f2 = Float(); var f3 = Float();
            if (f1 != null && f2 != null && f3 != null) {
                return new Color(f1.Value, f2.Value, f3.Value);
            } else {
                return null;
            }
        }
        /// <summary>
        /// Color With Alpha
        /// </summary>
        /// <returns></returns>
        public static Color? ColorWithAlpha() {
            var f1 = Float(); var f2 = Float(); var f3 = Float(); var f4 = Float();
            if (f1 != null && f2 != null && f3 != null && f4 != null) {
                return new Color(f1.Value, f2.Value, f3.Value, f4.Value);
            } else {
                return null;
            }
        }
        /// <summary>
        /// Color Between
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static Color? ColorBetween(Color min, Color max) {
            var f = Float();
            if (f != null) {
                return Microsoft.Xna.Framework.Color.Lerp(min, max, f.Value);
            } else {
                return null;
            }
        }
    }
}