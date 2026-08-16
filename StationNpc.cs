using System;
using Microsoft.Xna.Framework;

namespace Roguelancer;

/// <summary>
/// Station-only stationary character instance. Role identity and interaction
/// text are gameplay data; the replaceable CharacterAsset supplies only the
/// model, skeleton, materials, and animation clips.
/// </summary>
public sealed class StationNpc : IDisposable
{
    private readonly CharacterAsset _assets;
    private readonly CharacterAnimationController _animation;
    private readonly CharacterRenderer _renderer;
    private readonly float _animationStartTime;
    private readonly float _modelYawCorrectionDegrees;

    public StationNpc(
        string id,
        string displayName,
        CharacterAsset assets,
        Vector3 position,
        float yawDegrees,
        float interactionRadius,
        string dialogueText,
        float animationStartTime,
        bool interactive = true,
        Microsoft.Xna.Framework.Graphics.Effect? skinningEffect = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        Position = position;
        YawDegrees = yawDegrees;
        InteractionRadius = interactionRadius;
        DialogueText = dialogueText ?? string.Empty;
        _animationStartTime = animationStartTime;
        _modelYawCorrectionDegrees = assets.ModelConfiguration.RenderYawCorrectionDegrees;
        _animation = new CharacterAnimationController(assets.Model, assets.Animations);
        _renderer = new CharacterRenderer(assets);
        _renderer.SetGpuSkinningEffect(skinningEffect);
        InteractionLabel = DisplayName.ToUpperInvariant();
        IsInteractive = interactive;
        Reset();
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string InteractionLabel { get; }
    public Vector3 Position { get; }
    public float YawDegrees { get; private set; }
    public float InteractionRadius { get; }
    public string DialogueText { get; }
    public bool IsInteractive { get; }
    public CharacterAsset Assets => _assets;
    public CharacterAnimationController Animation => _animation;
    public CharacterRenderer Renderer => _renderer;
    public CharacterModelConfiguration ModelConfiguration => _assets.ModelConfiguration;
    public string StateLabel => _animation.State.ToString();
    public Matrix WorldMatrix =>
        Matrix.CreateRotationY(MathHelper.ToRadians(YawDegrees + _modelYawCorrectionDegrees)) *
        Matrix.CreateTranslation(Position);

    public void LoadGraphics(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice)
    {
        _renderer.LoadGraphics(graphicsDevice);
        UpdatePose();
    }

    public void Reset() => _animation.Reset(_animationStartTime);

    public void Update(float deltaSeconds) => _animation.Update(deltaSeconds);

    public void UpdatePose(PerformanceDiagnostics diagnostics = null)
    {
        if (diagnostics == null)
        {
            _renderer.UpdatePose(_animation.EvaluateLocalPose(stripJumpRootVertical: false));
            return;
        }

        System.Numerics.Matrix4x4[] localPose;
        using (diagnostics.Measure("station.animation.pose.evaluate"))
        {
            localPose = _animation.EvaluateLocalPose(stripJumpRootVertical: false);
        }
        using (diagnostics.Measure("station.animation.pose.upload"))
        {
            _renderer.UpdatePose(localPose, diagnostics);
        }
    }

    public void FacePlayer(Vector3 playerPosition)
    {
        Vector3 direction = playerPosition - Position;
        direction.Y = 0.0f;
        if (direction.LengthSquared() <= 0.0001f) return;
        YawDegrees = MathHelper.ToDegrees(MathF.Atan2(direction.X, direction.Z));
    }

    public void Dispose() => _renderer.Dispose();
}
