// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    public static class RandomHelper {
        private static Random lRandom = new Random();
        public static Random Rnd {
            get { return lRandom; }
        }
        public static float Float() {
            return (float)Rnd.NextDouble();
        }
        public static float FloatBetween(float _Min, float _Max) {
            return _Min + (float)(Rnd.NextDouble() * (_Max - _Min));
        }
        public static int IntBetween(int _Min, int _Max) {
            return _Min + (int)(Rnd.NextDouble() * (_Max - _Min));
        }
        public static bool Boolean() {
            return Rnd.Next(2) == 0;
        }
        public static Vector3 Vector3Between(Vector3 _Min, Vector3 _Max) {
            return Vector3.Lerp(_Min, _Max, Float());
        }
        public static Vector3 NormalizedVector3() {
            Vector3 vector = new Vector3(FloatBetween(-1.0f, 1.0f), FloatBetween(-1.0f, 1.0f), FloatBetween(-1.0f, 1.0f));
            vector.Normalize();
            return vector;
        }
        public static Color Color() {
            return new Color(Float(), Float(), Float());
        }
        public static Color ColorWithAlpha() {
            return new Color(Float(), Float(), Float(), Float());
        }
        public static Color ColorBetween(Color _Min, Color _Max) {
            return Microsoft.Xna.Framework.Color.Lerp(_Min, _Max, Float());
        }
    }
}