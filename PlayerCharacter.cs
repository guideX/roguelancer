using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Roguelancer;

/// <summary>Temporary station player instance. The model is replaceable; movement is not Adam-specific.</summary>
public sealed class PlayerCharacter
{
    private const float WalkSpeed = 3.0f;
    private const float RunSpeed = 5.4f;
    private const float StrafeSpeed = 2.6f;
    private const float JumpVelocity = 4.5f;
    private const float Gravity = -18.0f;
    private readonly StationTestScene _scene;
    private readonly CharacterAsset _assets;
    private readonly CharacterAnimationController _animation;
    private readonly CharacterRenderer _renderer;
    private readonly CharacterModelConfiguration _modelConfiguration;
    private KeyboardState _previousKeyboard;

    public PlayerCharacter(
        CharacterAsset assets,
        StationTestScene scene)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _animation = new CharacterAnimationController(assets.Model, assets.Animations);
        _renderer = new CharacterRenderer(assets);
        _modelConfiguration = assets.ModelConfiguration;
        ResetToSpawn();
    }

    public PlayerCharacter(
        CharacterGltfAsset asset,
        IReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?> clips,
        StationTestScene scene,
        CharacterModelConfiguration? modelConfiguration = null)
        : this(
            new CharacterAsset(
                "inline-character",
                asset,
                clips,
                modelConfiguration ?? CharacterModelConfiguration.AdamMixamo),
            scene)
    {
    }

    public Vector3 Position { get; private set; }
    public float YawDegrees { get; private set; }
    public bool IsGrounded { get; private set; }
    public float VerticalVelocity { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.Up;
    public Vector3 GroundPoint { get; private set; }
    public Vector3 RequestedMovement { get; private set; }
    public Vector3 RequestedMovementDirection { get; private set; }
    public Vector3 ActualHorizontalVelocity { get; private set; }
    public float SlopeAngleDegrees { get; private set; }
    public string SurfaceLabel { get; private set; } = "Bay floor";
    public float CapsuleRadius => CapsuleControllerMath.Radius;
    public float CapsuleHeight => CapsuleControllerMath.StandingHeight;
    public CharacterAnimationController Animation => _animation;
    public CharacterRenderer Renderer => _renderer;
    public CharacterAsset Assets => _assets;
    public CharacterModelConfiguration ModelConfiguration => _modelConfiguration;
    public Vector3 LogicalForward => ForwardFromYaw(YawDegrees);
    public Vector3 RenderedModelForward => _modelConfiguration.GetRenderedForward(YawDegrees);
    public bool CapsuleDebugVisible { get; set; }
    public string StateLabel => _animation.State.ToString();

    public void LoadGraphics(GraphicsDevice graphicsDevice)
    {
        _renderer.LoadGraphics(graphicsDevice);
        UpdatePose();
    }

    public void ResetToSpawn()
    {
        Position = _scene.SpawnPosition;
        YawDegrees = _scene.SpawnYawDegrees;
        GroundPoint = Position;
        GroundNormal = Vector3.Up;
        RequestedMovement = Vector3.Zero;
        RequestedMovementDirection = Vector3.Zero;
        ActualHorizontalVelocity = Vector3.Zero;
        SlopeAngleDegrees = 0.0f;
        SurfaceLabel = "Bay floor";
        IsGrounded = true;
        VerticalVelocity = 0.0f;
        _animation.Reset();
        _previousKeyboard = Keyboard.GetState();
    }

    public void Update(KeyboardState keyboard, CharacterCamera camera, float deltaSeconds, PerformanceDiagnostics diagnostics)
    {
        bool forwardHeld = keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up);
        bool backwardHeld = keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down);
        bool leftHeld = keyboard.IsKeyDown(Keys.A);
        bool rightHeld = keyboard.IsKeyDown(Keys.D);
        bool runHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        bool jumpPressed = keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space);

        CharacterAnimationState inputState = SelectAnimationState(forwardHeld, backwardHeld, leftHeld, rightHeld, runHeld);
        using (diagnostics.Measure("station.animation.state"))
        {
            if (!_animation.IsOneShot)
            {
                if (jumpPressed && IsGrounded)
                {
                    VerticalVelocity = JumpVelocity;
                    IsGrounded = false;
                    _animation.SetState(CharacterAnimationState.Jump);
                }
                else
                {
                    _animation.SetState(inputState);
                }
            }
        }

        Vector3 movement = Vector3.Zero;
        if (forwardHeld && !backwardHeld) movement += camera.MovementForward * (runHeld ? RunSpeed : WalkSpeed);
        else if (backwardHeld && !forwardHeld) movement -= camera.MovementForward * WalkSpeed;
        if (leftHeld && !rightHeld) movement -= camera.MovementRight * StrafeSpeed;
        else if (rightHeld && !leftHeld) movement += camera.MovementRight * StrafeSpeed;
        RequestedMovement = movement;
        RequestedMovementDirection = movement.LengthSquared() > 0.0001f ? Vector3.Normalize(movement) : Vector3.Zero;
        Vector3 previousPosition = Position;
        using (diagnostics.Measure("station.controller.collision"))
        {
            if (movement.LengthSquared() > 0.0001f)
            {
                if (IsGrounded) movement = CapsuleControllerMath.ProjectMovementAlongGround(movement, GroundNormal);
                Vector3 desired = Position + movement * deltaSeconds;
                Position = _scene.ResolveMovement(Position, desired, CapsuleHeight, IsGrounded, GroundNormal, out _);
                if (forwardHeld != backwardHeld)
                {
                    float targetYaw = MathHelper.ToDegrees(MathF.Atan2(camera.MovementForward.X, camera.MovementForward.Z));
                    YawDegrees = RotateTowards(YawDegrees, targetYaw, 540.0f * deltaSeconds);
                }
            }

            UpdateVerticalMovement(deltaSeconds);
        }
        ActualHorizontalVelocity = deltaSeconds > 0.0001f
            ? new Vector3((Position.X - previousPosition.X) / deltaSeconds, 0.0f, (Position.Z - previousPosition.Z) / deltaSeconds)
            : Vector3.Zero;
        if (Position.Y < -3.0f || _scene.IsOutOfBounds(Position)) ResetToSpawn();

        using (diagnostics.Measure("station.animation.time"))
        {
            _animation.Update(deltaSeconds);
        }
        if (_animation.IsFinished && IsGrounded) _animation.SetState(inputState);
        _previousKeyboard = keyboard;
    }

    public void UpdatePose(PerformanceDiagnostics diagnostics = null)
    {
        if (diagnostics == null)
        {
            _renderer.UpdatePose(_animation.EvaluateLocalPose(stripJumpRootVertical: true));
            return;
        }

        System.Numerics.Matrix4x4[] localPose;
        using (diagnostics.Measure("station.animation.pose.evaluate"))
        {
            localPose = _animation.EvaluateLocalPose(stripJumpRootVertical: true);
        }
        using (diagnostics.Measure("station.animation.pose.upload"))
        {
            _renderer.UpdatePose(localPose);
        }
    }

    public Matrix WorldMatrix =>
        Matrix.CreateRotationY(MathHelper.ToRadians(YawDegrees + _modelConfiguration.RenderYawCorrectionDegrees)) *
        Matrix.CreateTranslation(Position);

    public void DrawDebug(GraphicsDevice graphicsDevice, BasicEffect effect, Matrix view, Matrix projection)
    {
        if (!CapsuleDebugVisible) return;
        List<VertexPositionColor> vertices = new();
        int slices = 20;
        for (int i = 0; i < slices; i++)
        {
            float a0 = MathHelper.TwoPi * i / slices;
            float a1 = MathHelper.TwoPi * (i + 1) / slices;
            AddLine(vertices, Position + new Vector3(MathF.Cos(a0) * CapsuleRadius, CapsuleRadius, MathF.Sin(a0) * CapsuleRadius), Position + new Vector3(MathF.Cos(a1) * CapsuleRadius, CapsuleRadius, MathF.Sin(a1) * CapsuleRadius), Color.Cyan);
            AddLine(vertices, Position + new Vector3(MathF.Cos(a0) * CapsuleRadius, CapsuleHeight - CapsuleRadius, MathF.Sin(a0) * CapsuleRadius), Position + new Vector3(MathF.Cos(a1) * CapsuleRadius, CapsuleHeight - CapsuleRadius, MathF.Sin(a1) * CapsuleRadius), Color.Cyan);
            AddLine(vertices, Position + new Vector3(MathF.Cos(a0) * CapsuleRadius, CapsuleRadius, MathF.Sin(a0) * CapsuleRadius), Position + new Vector3(MathF.Cos(a0) * CapsuleRadius, CapsuleHeight - CapsuleRadius, MathF.Sin(a0) * CapsuleRadius), Color.Cyan);
        }
        AddLine(vertices, Position, Position + Vector3.Up * 0.70f, Color.Yellow);
        AddLine(vertices, Position + Vector3.Up * 0.02f, Position + Vector3.Up * 0.02f + GroundNormal * 0.65f, Color.Lime);
        effect.World = Matrix.Identity;
        effect.View = view;
        effect.Projection = projection;
        effect.TextureEnabled = false;
        effect.VertexColorEnabled = true;
        effect.CurrentTechnique.Passes[0].Apply();
        graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices.ToArray(), 0, vertices.Count / 2);
        effect.VertexColorEnabled = false;
    }

    private void UpdateVerticalMovement(float deltaSeconds)
    {
        StationGroundHit support = _scene.GetGround(Position);
        if (IsGrounded)
        {
            if (support.Found && support.Point.Y - Position.Y <= CapsuleControllerMath.MaxStepHeight + CapsuleControllerMath.GroundSnapDistance && Position.Y - support.Point.Y <= CapsuleControllerMath.GroundSnapDistance)
            {
                Position = new Vector3(Position.X, support.Point.Y, Position.Z);
                GroundPoint = support.Point;
                GroundNormal = support.Normal;
                SlopeAngleDegrees = support.SlopeDegrees;
                SurfaceLabel = support.SurfaceLabel;
                VerticalVelocity = 0.0f;
                return;
            }
            IsGrounded = false;
        }

        Vector3 previous = Position;
        VerticalVelocity += Gravity * deltaSeconds;
        Position += Vector3.Up * VerticalVelocity * deltaSeconds;
        support = _scene.GetGround(Position);
        if (support.Found && VerticalVelocity <= 0.0f && previous.Y >= support.Point.Y - CapsuleControllerMath.GroundContactTolerance && Position.Y <= support.Point.Y + CapsuleControllerMath.GroundContactTolerance)
        {
            Position = new Vector3(Position.X, support.Point.Y, Position.Z);
            VerticalVelocity = 0.0f;
            IsGrounded = true;
            GroundPoint = support.Point;
            GroundNormal = support.Normal;
            SlopeAngleDegrees = support.SlopeDegrees;
            SurfaceLabel = support.SurfaceLabel;
        }
        else
        {
            IsGrounded = false;
            SurfaceLabel = "Airborne";
        }
    }

    private static CharacterAnimationState SelectAnimationState(bool forward, bool backward, bool left, bool right, bool run)
    {
        if (forward && !backward) return run ? CharacterAnimationState.RunForward : CharacterAnimationState.WalkForward;
        if (backward && !forward) return CharacterAnimationState.WalkBackward;
        if (left && !right) return CharacterAnimationState.StrafeLeft;
        if (right && !left) return CharacterAnimationState.StrafeRight;
        return CharacterAnimationState.Idle;
    }

    private static Vector3 ForwardFromYaw(float yawDegrees)
    {
        // Station gameplay uses +Z as logical forward at yaw 0; positive yaw
        // turns toward +X, matching CharacterCamera's convention.
        float radians = MathHelper.ToRadians(yawDegrees);
        return Vector3.Normalize(new Vector3(MathF.Sin(radians), 0.0f, MathF.Cos(radians)));
    }

    private static float RotateTowards(float current, float target, float maxDelta)
    {
        float delta = MathHelper.WrapAngle(MathHelper.ToRadians(target - current));
        float result = current + MathHelper.ToDegrees(MathHelper.Clamp(delta, -MathHelper.ToRadians(maxDelta), MathHelper.ToRadians(maxDelta)));
        return MathHelper.WrapAngle(MathHelper.ToRadians(result)) * 180.0f / MathF.PI;
    }

    private static void AddLine(List<VertexPositionColor> vertices, Vector3 start, Vector3 end, Color color) { vertices.Add(new VertexPositionColor(start, color)); vertices.Add(new VertexPositionColor(end, color)); }
}
