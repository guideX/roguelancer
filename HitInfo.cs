using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Information about a weapon hit
    /// </summary>
    public struct HitInfo
    {
        public Vector3 Position;
        public Vector3 Direction;
        public Color WeaponColor;
        public WeaponType WeaponType;
        public float Damage;
    }
}
