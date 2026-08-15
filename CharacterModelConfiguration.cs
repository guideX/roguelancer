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
    /// Its authored forward is local +Z (XNA Vector3.Backward), which already
    /// matches Roguelancer's logical +Z-forward convention. Keep the zero
    /// correction explicit so a future model can supply its own adjustment
    /// without reintroducing an Adam-specific render rotation.
    /// </summary>
    public static CharacterModelConfiguration AdamMixamo { get; } =
        new("Prototype Adam/Mixamo GuyWalking2.glb (normalized Y-up, local +Z)", Vector3.Backward, 0.0f);
}
