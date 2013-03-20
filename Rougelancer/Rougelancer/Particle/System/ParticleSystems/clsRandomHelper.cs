// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
namespace Rougelancer.Particle.ParticleSystem {
    public static class clsRandomHelper {
        private static Random lRandom = new Random();
        public static Random Random {
            get { return lRandom; }
        }
        public static float Float() {
            return (float)Random.NextDouble();
        }
        public static float FloatBetween(float _Min, float _Max) {
            return _Min + (float)(Random.NextDouble() * (_Max - _Min));
        }
        public static int IntBetween(int _Min, int _Max) {
            return _Min + (int)(Random.NextDouble() * (_Max - _Min));
        }
        public static bool Boolean() {
            return Random.Next(2) == 0;
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