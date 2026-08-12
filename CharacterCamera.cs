using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Roguelancer;

public sealed class CharacterCamera
{
    private const float PreferredDistance = 6.0f;
    private const float CameraRadius = 0.30f;
    private readonly GraphicsDevice _graphicsDevice;
    private Vector3 _smoothedPosition;
    private Vector3 _smoothedTarget;
    private bool _initialized;
    private float _yaw;
    private float _pitch = -12.0f;

    public CharacterCamera(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60.0f), graphicsDevice.Viewport.AspectRatio, 0.05f, 250.0f);
    }

    public Matrix View { get; private set; }
    public Matrix Projection { get; private set; }
    public Vector3 Position { get; private set; }
    public Vector3 Target { get; private set; }
    public float YawDegrees => _yaw;

    public void Reset(Vector3 playerPosition, float playerYawDegrees)
    {
        _yaw = playerYawDegrees;
        _pitch = -12.0f;
        _initialized = false;
        Update(playerPosition, 0.0f, recenterMouse: true);
    }

    public void Update(Vector3 playerPosition, float deltaSeconds, bool recenterMouse)
        => Update(playerPosition, deltaSeconds, recenterMouse, null);

    public void Update(Vector3 playerPosition, float deltaSeconds, bool recenterMouse, StationTestScene? collisionScene)
    {
        Point center = new(_graphicsDevice.Viewport.Width / 2, _graphicsDevice.Viewport.Height / 2);
        MouseState mouse = Mouse.GetState();
        if (recenterMouse)
        {
            _yaw -= (mouse.X - center.X) * 0.12f;
            _pitch = MathHelper.Clamp(_pitch - (mouse.Y - center.Y) * 0.10f, -65.0f, 35.0f);
            Mouse.SetPosition(center.X, center.Y);
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Left)) _yaw -= 95.0f * deltaSeconds;
        if (Keyboard.GetState().IsKeyDown(Keys.Right)) _yaw += 95.0f * deltaSeconds;
        if (Keyboard.GetState().IsKeyDown(Keys.PageUp)) _pitch = MathHelper.Clamp(_pitch + 55.0f * deltaSeconds, -65.0f, 35.0f);
        if (Keyboard.GetState().IsKeyDown(Keys.PageDown)) _pitch = MathHelper.Clamp(_pitch - 55.0f * deltaSeconds, -65.0f, 35.0f);

        Vector3 target = playerPosition + new Vector3(0.0f, 1.05f, 0.0f);
        Vector3 direction = ViewDirection(_yaw, _pitch);
        Vector3 desiredPosition = target - direction * PreferredDistance;
        StationCameraCollision collision = collisionScene?.ResolveCameraPosition(target, desiredPosition, CameraRadius)
            ?? new StationCameraCollision(false, desiredPosition, string.Empty);
        Vector3 collisionSafePosition = collision.Position;
        if (!_initialized)
        {
            _smoothedPosition = collisionSafePosition;
            _smoothedTarget = target;
            _initialized = true;
        }
        else if (deltaSeconds > 0.0f)
        {
            float positionRate = collision.Hit ? 24.0f : 8.0f;
            float blend = 1.0f - MathF.Exp(-positionRate * deltaSeconds);
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, collisionSafePosition, blend);
            // Clamp the smoothed position every frame as well. This prevents a
            // frame of interpolation from placing the camera on the far side
            // of a wall when the player rotates into it.
            if (collisionScene != null)
            {
                StationCameraCollision smoothedCollision = collisionScene.ResolveCameraPosition(target, _smoothedPosition, CameraRadius);
                _smoothedPosition = smoothedCollision.Position;
            }
            _smoothedTarget = Vector3.Lerp(_smoothedTarget, target, blend);
        }
        else
        {
            _smoothedPosition = collisionSafePosition;
            _smoothedTarget = target;
        }

        Position = _smoothedPosition;
        Target = _smoothedTarget;
        View = Matrix.CreateLookAt(Position, Target, Vector3.Up);
        Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60.0f), _graphicsDevice.Viewport.AspectRatio, 0.05f, 250.0f);
    }

    public Vector3 MovementForward => ForwardFromYaw(_yaw);
    public Vector3 MovementRight
    {
        get
        {
            Vector3 forward = MovementForward;
            return new Vector3(forward.Z, 0.0f, -forward.X);
        }
    }

    private static Vector3 ForwardFromYaw(float yawDegrees)
    {
        float radians = MathHelper.ToRadians(yawDegrees);
        return Vector3.Normalize(new Vector3(MathF.Sin(radians), 0.0f, MathF.Cos(radians)));
    }

    private static Vector3 ViewDirection(float yawDegrees, float pitchDegrees)
    {
        Vector3 horizontal = ForwardFromYaw(yawDegrees);
        float pitch = MathHelper.ToRadians(pitchDegrees);
        return Vector3.Normalize(new Vector3(horizontal.X * MathF.Cos(pitch), MathF.Sin(pitch), horizontal.Z * MathF.Cos(pitch)));
    }
}
