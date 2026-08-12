using System;
using Microsoft.Xna.Framework;

namespace Roguelancer;

/// <summary>Small, engine-independent helpers used by the station-test capsule.</summary>
public static class CapsuleControllerMath
{
    public const float Radius = 0.28f;
    public const float StandingHeight = 1.80f;
    public const float CrouchedHeight = 1.20f;
    public const float MaxWalkableSlopeDegrees = 45.0f;
    public const float MaxStepHeight = 0.32f;
    public const float GroundSnapDistance = 0.12f;
    public const float GroundContactTolerance = 0.06f;
    public const float Skin = 0.025f;

    public static float SlopeAngleDegrees(Vector3 normal)
    {
        if (normal.LengthSquared() < 0.000001f) return 90.0f;
        normal.Normalize();
        return MathF.Acos(MathHelper.Clamp(normal.Y, -1.0f, 1.0f)) * 180.0f / MathF.PI;
    }

    public static bool IsWalkable(Vector3 normal) =>
        SlopeAngleDegrees(normal) <= MaxWalkableSlopeDegrees + 0.5f;

    public static Vector3 ProjectMovementAlongGround(Vector3 movement, Vector3 normal)
    {
        if (movement.LengthSquared() < 0.000001f || normal.Y < 0.0001f) return movement;
        normal.Normalize();
        Vector3 projected = movement - normal * Vector3.Dot(movement, normal);
        Vector3 horizontal = new(projected.X, 0.0f, projected.Z);
        if (horizontal.LengthSquared() < 0.000001f) return Vector3.Zero;
        horizontal.Normalize();
        return horizontal * movement.Length();
    }

    public static Vector3 SlideAlongWall(Vector3 movement, Vector3 wallNormal)
    {
        if (wallNormal.LengthSquared() < 0.000001f) return movement;
        wallNormal.Normalize();
        float intoWall = Vector3.Dot(movement, wallNormal);
        return intoWall < 0.0f ? movement - wallNormal * intoWall : movement;
    }
}
