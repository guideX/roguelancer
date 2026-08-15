using Microsoft.Xna.Framework;

namespace Roguelancer;

/// <summary>
/// Asset-specific orientation metadata kept at the render boundary. Gameplay
/// yaw remains logical; this correction only affects the model world transform.
/// </summary>
public sealed class CharacterModelConfiguration
{
    public CharacterModelConfiguration(string assetLabel, Vector3 localForward, float renderYawCorrectionDegrees)
    {
        AssetLabel = assetLabel;
        LocalForward = Vector3.Normalize(localForward);
        RenderYawCorrectionDegrees = renderYawCorrectionDegrees;
    }

    public string AssetLabel { get; }
    public Vector3 LocalForward { get; }
    public float RenderYawCorrectionDegrees { get; }

    public Vector3 GetRenderedForward(float logicalYawDegrees)
    {
        Matrix correction = Matrix.CreateRotationY(MathHelper.ToRadians(logicalYawDegrees + RenderYawCorrectionDegrees));
        return Vector3.Normalize(Vector3.TransformNormal(LocalForward, correction));
    }

    /// <summary>
    /// The normalized Mixamo GLB used by the temporary station character.
    /// The mesh's authored forward is local -Z (XNA Vector3.Forward). The
    /// correction is kept at the render boundary, matching WalkingAnimationLab;
    /// gameplay yaw and collision remain in Roguelancer's +Z-forward convention.
    /// </summary>
    public static CharacterModelConfiguration AdamMixamo { get; } =
        new("Adam/Mixamo GuyWalking2.glb (normalized Y-up, local -Z)", Vector3.Forward, 180.0f);
}
