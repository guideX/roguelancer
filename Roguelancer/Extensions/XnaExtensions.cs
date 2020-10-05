using Microsoft.Xna.Framework;
public static class XnaExtensions {
    public static Vector2 ToVector2(this Vector3 obj) {
        return new Vector2(obj.X, obj.Y);
    }
}