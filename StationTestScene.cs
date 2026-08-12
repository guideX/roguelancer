using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Roguelancer;

public readonly record struct StationGroundHit(bool Found, Vector3 Point, Vector3 Normal, float SlopeDegrees, string SurfaceLabel);

/// <summary>Purpose-built industrial bay for the temporary developer on-foot mode.</summary>
public sealed class StationTestScene : IDisposable
{
    private readonly List<StationSurface> _surfaces = new();
    private readonly List<StationCollider> _colliders = new();
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly HashSet<Texture2D> _ownedTextures = new();
    private GraphicsDevice? _graphicsDevice;
    private BasicEffect? _effect;

    public Vector3 SpawnPosition { get; } = new(-10.0f, 0.0f, -10.0f);
    public float SpawnYawDegrees { get; } = 0.0f;
    public Vector3 DockedShipPosition { get; } = new(0.0f, 0.05f, 1.5f);
    public string ShipScaleNote => "Uses Ship.Draw's existing 0.1 model scale; bay units are treated as metres and the human capsule is 1.8m tall.";

    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice) { TextureEnabled = true, LightingEnabled = true, VertexColorEnabled = false };
        LoadTexture(content, "structure", "Textures/Texturelabs_Metal_278S", new Color(50, 58, 68));
        LoadTexture(content, "floor", "Textures/Texturelabs_Metal_278S", new Color(76, 82, 88));
        LoadTexture(content, "accent", "Textures/Texturelabs_Metal_278S", new Color(58, 48, 48));
        LoadTexture(content, "hazard", "Textures/hazard_stripes", Color.White);
        LoadTexture(content, "glow", "Textures/glow_strip", Color.White);
        LoadTexture(content, "door", "Textures/door", Color.White);
        BuildLayout();
    }

    public void Draw(Matrix view, Matrix projection)
    {
        if (_graphicsDevice is null || _effect is null) return;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;
        _effect.VertexColorEnabled = false;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
        foreach (StationSurface surface in _surfaces)
        {
            _effect.Texture = surface.Texture;
            _effect.TextureEnabled = true;
            _effect.LightingEnabled = !surface.Unlit;
            _effect.DiffuseColor = surface.Tint.ToVector3();
            _effect.Alpha = 1.0f;
            if (surface.Unlit)
            {
                _effect.LightingEnabled = false;
                _effect.DiffuseColor = surface.Tint.ToVector3();
            }
            else
            {
                _effect.EnableDefaultLighting();
                _effect.AmbientLightColor = new Vector3(0.28f, 0.31f, 0.36f);
                _effect.DirectionalLight0.Direction = new Vector3(-0.35f, -0.85f, -0.40f);
                _effect.DirectionalLight0.DiffuseColor = new Vector3(0.68f, 0.72f, 0.82f);
                _effect.DirectionalLight0.Enabled = true;
            }
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, surface.Vertices, 0, surface.Vertices.Length, surface.Indices, 0, surface.Indices.Length / 3);
        }
    }

    public void DrawShipModel(Model model, Matrix modelCorrection, Matrix view, Matrix projection)
    {
        if (model is null || _graphicsDevice is null) return;
        Matrix world = Matrix.CreateScale(0.1f) * Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi) * modelCorrection * Matrix.CreateRotationY(MathHelper.Pi) * Matrix.CreateTranslation(DockedShipPosition);
        foreach (ModelMesh mesh in model.Meshes)
        {
            foreach (BasicEffect effect in mesh.Effects)
            {
                effect.World = world;
                effect.View = view;
                effect.Projection = projection;
                effect.EnableDefaultLighting();
                effect.PreferPerPixelLighting = true;
                effect.SpecularPower = 16.0f;
                effect.DirectionalLight0.Direction = new Vector3(-0.35f, -0.85f, -0.40f);
                effect.DirectionalLight0.DiffuseColor = new Vector3(0.92f, 0.88f, 0.80f);
                effect.DirectionalLight0.SpecularColor = new Vector3(0.55f, 0.55f, 0.60f);
                effect.AmbientLightColor = new Vector3(0.24f, 0.25f, 0.29f);
            }
            mesh.Draw();
        }
    }

    public Vector3 ResolveMovement(Vector3 current, Vector3 desired, float capsuleHeight, bool grounded, Vector3 groundNormal, out Vector3 wallNormal)
    {
        Vector3 result = current;
        wallNormal = Vector3.Zero;
        Vector3 xCandidate = new(desired.X, result.Y, result.Z);
        if (!TryResolveAxis(result, xCandidate, capsuleHeight, grounded, ref result, ref wallNormal, axisX: true)) result.X = current.X;
        Vector3 zCandidate = new(result.X, result.Y, desired.Z);
        if (!TryResolveAxis(result, zCandidate, capsuleHeight, grounded, ref result, ref wallNormal, axisX: false)) result.Z = current.Z;
        if (grounded && groundNormal.LengthSquared() > 0.01f)
        {
            Vector3 movement = result - current;
            Vector3 slid = CapsuleControllerMath.SlideAlongWall(movement, wallNormal);
            result = new Vector3(current.X + slid.X, result.Y, current.Z + slid.Z);
        }
        return result;
    }

    public StationGroundHit GetGround(Vector3 position)
    {
        float height = 0.0f;
        string label = "Bay floor";
        Vector3 normal = Vector3.Up;
        if (position.X >= 8.0f && position.X <= 17.0f)
        {
            if (position.Z >= 6.5f && position.Z <= 15.0f)
            {
                height = 0.45f;
                label = "Raised maintenance walkway";
            }
            else if (position.Z >= 3.5f && position.Z < 6.5f)
            {
                float amount = (position.Z - 3.5f) / 3.0f;
                height = MathHelper.Clamp(amount, 0.0f, 1.0f) * 0.45f;
                normal = Vector3.Normalize(new Vector3(0.0f, 3.0f, -0.45f));
                label = "Walkway ramp";
            }
        }
        return new StationGroundHit(true, new Vector3(position.X, height, position.Z), normal, CapsuleControllerMath.SlopeAngleDegrees(normal), label);
    }

    public bool IsOutOfBounds(Vector3 position) => position.X < -17.0f || position.X > 17.0f || position.Z < -17.0f || position.Z > 17.0f;

    public void Dispose()
    {
        foreach (Texture2D texture in _ownedTextures) texture.Dispose();
        _ownedTextures.Clear();
        _textures.Clear();
        _effect?.Dispose();
        _effect = null;
        _surfaces.Clear();
        _colliders.Clear();
    }

    private void LoadTexture(ContentManager content, string key, string assetName, Color fallbackColor)
    {
        try
        {
            _textures[key] = content.Load<Texture2D>(assetName);
            Console.WriteLine($"[STATION TEST] Loaded texture {assetName}");
        }
        catch (Exception ex)
        {
            Texture2D fallback = new(_graphicsDevice!, 1, 1);
            fallback.SetData(new[] { fallbackColor });
            _textures[key] = fallback;
            _ownedTextures.Add(fallback);
            Console.WriteLine($"[STATION TEST] Texture fallback for {assetName}: {ex.Message}");
        }
    }

    private void BuildLayout()
    {
        _surfaces.Clear();
        _colliders.Clear();

        // A compact service bay: the floor, ceiling, back wall, and side walls define a room.
        AddBox(new Vector3(0, -0.12f, 0), new Vector3(36, 0.24f, 36), "floor", 5.0f, false, false);
        AddBox(new Vector3(0, 5.0f, 17.8f), new Vector3(36, 10.0f, 0.4f), "structure", 3.0f, false, true);
        AddBox(new Vector3(-17.8f, 5.0f, 0), new Vector3(0.4f, 10.0f, 36), "structure", 3.0f, false, true);
        AddBox(new Vector3(17.8f, 5.0f, 0), new Vector3(0.4f, 10.0f, 36), "structure", 3.0f, false, true);
        AddBox(new Vector3(0, 10.0f, 0), new Vector3(36, 0.35f, 36), "structure", 3.0f, false, true);

        // Docking pad and perimeter hazard markings.
        AddBox(new Vector3(0, 0.035f, 0.8f), new Vector3(16.0f, 0.07f, 12.0f), "floor", 2.0f, false, false);
        AddBox(new Vector3(0, 0.08f, -5.15f), new Vector3(16.0f, 0.035f, 0.55f), "hazard", 5.0f, true, false);
        AddBox(new Vector3(0, 0.08f, 6.75f), new Vector3(16.0f, 0.035f, 0.55f), "hazard", 5.0f, true, false);
        AddBox(new Vector3(-7.75f, 0.08f, 0.8f), new Vector3(0.55f, 0.035f, 11.9f), "hazard", 4.0f, true, false);
        AddBox(new Vector3(7.75f, 0.08f, 0.8f), new Vector3(0.55f, 0.035f, 11.9f), "hazard", 4.0f, true, false);

        // Raised walkway/ramp to the large airlock-style exit.
        AddBox(new Vector3(12.5f, 0.25f, 10.75f), new Vector3(9.0f, 0.5f, 8.5f), "floor", 3.0f, false, false);
        AddRamp(new Vector3(12.5f, 0.0f, 5.0f), new Vector3(9.0f, 0.45f, 3.0f), "floor");
        AddBox(new Vector3(12.5f, 0.54f, 6.5f), new Vector3(9.0f, 0.06f, 0.45f), "hazard", 3.0f, true, false);

        // Structural ribs and ceiling services.
        for (int z = -14; z <= 14; z += 7)
        {
            AddBox(new Vector3(-14.5f, 5.0f, z), new Vector3(0.55f, 10.0f, 0.55f), "accent", 1.0f, false, true);
            AddBox(new Vector3(14.5f, 5.0f, z), new Vector3(0.55f, 10.0f, 0.55f), "accent", 1.0f, false, true);
            AddBox(new Vector3(0, 9.25f, z), new Vector3(29.5f, 0.55f, 0.55f), "accent", 2.0f, false, true);
        }
        AddBox(new Vector3(-6.5f, 9.65f, -1.5f), new Vector3(8.0f, 0.08f, 0.30f), "glow", 1.0f, true, false);
        AddBox(new Vector3(6.5f, 9.65f, -1.5f), new Vector3(8.0f, 0.08f, 0.30f), "glow", 1.0f, true, false);
        AddBox(new Vector3(12.5f, 9.55f, 10.5f), new Vector3(6.0f, 0.08f, 0.30f), "glow", 1.0f, true, false);

        // Closed airlock frame and panel; the closed door is solid for this phase.
        AddBox(new Vector3(-5.0f, 3.0f, 17.35f), new Vector3(0.75f, 6.0f, 0.65f), "accent", 1.0f, false, true);
        AddBox(new Vector3(5.0f, 3.0f, 17.35f), new Vector3(0.75f, 6.0f, 0.65f), "accent", 1.0f, false, true);
        AddBox(new Vector3(0, 6.1f, 17.35f), new Vector3(10.25f, 0.75f, 0.65f), "accent", 2.0f, false, true);
        AddBox(new Vector3(0, 2.7f, 17.2f), new Vector3(8.8f, 5.0f, 0.20f), "door", 1.0f, false, true);
        AddBox(new Vector3(-4.1f, 3.0f, 17.0f), new Vector3(0.18f, 3.2f, 0.10f), "glow", 1.0f, true, false);
        AddBox(new Vector3(4.1f, 3.0f, 17.0f), new Vector3(0.18f, 3.2f, 0.10f), "glow", 1.0f, true, false);
        AddBox(new Vector3(0, 5.0f, 17.0f), new Vector3(8.4f, 0.18f, 0.10f), "hazard", 2.0f, true, false);

        // Service alcove, tool carts, and a few intentionally simple crates.
        AddBox(new Vector3(-14.0f, 2.4f, 7.0f), new Vector3(0.4f, 4.8f, 9.0f), "accent", 2.0f, false, true);
        AddBox(new Vector3(-12.7f, 0.55f, 10.7f), new Vector3(3.0f, 1.1f, 1.5f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-12.7f, 1.55f, 10.7f), new Vector3(2.7f, 0.08f, 1.2f), "hazard", 1.0f, true, false);
        AddBox(new Vector3(14.4f, 0.8f, -10.5f), new Vector3(2.0f, 1.6f, 2.0f), "accent", 1.0f, false, true);
        AddBox(new Vector3(11.7f, 0.55f, -12.0f), new Vector3(1.3f, 1.1f, 1.3f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-13.0f, 0.6f, -12.5f), new Vector3(1.7f, 1.2f, 1.7f), "structure", 1.0f, false, true);

        // A bounded approximation of the parked ship footprint. The visual model remains the real ship.
        _colliders.Add(new StationCollider(new Vector2(-6.2f, -1.8f), new Vector2(6.2f, 6.0f), 0.0f, 3.2f, "parked ship service envelope"));
    }

    private bool TryResolveAxis(Vector3 current, Vector3 candidate, float capsuleHeight, bool grounded, ref Vector3 result, ref Vector3 wallNormal, bool axisX)
    {
        foreach (StationCollider collider in _colliders)
        {
            if (!OverlapsVertical(candidate.Y, capsuleHeight, collider) || !CircleOverlapsBox(candidate, CapsuleControllerMath.Radius, collider)) continue;
            float step = collider.MaxY - current.Y;
            if (grounded && step > 0.0f && step <= CapsuleControllerMath.MaxStepHeight)
            {
                result = new Vector3(candidate.X, collider.MaxY, candidate.Z);
                continue;
            }
            Vector2 center = new(candidate.X, candidate.Z);
            Vector2 nearest = new(MathHelper.Clamp(center.X, collider.Min.X, collider.Max.X), MathHelper.Clamp(center.Y, collider.Min.Y, collider.Max.Y));
            Vector2 difference = center - nearest;
            if (difference.LengthSquared() > 0.00001f)
            {
                difference.Normalize();
                wallNormal = new Vector3(difference.X, 0.0f, difference.Y);
            }
            else wallNormal = axisX ? new Vector3(candidate.X > (collider.Min.X + collider.Max.X) * 0.5f ? 1 : -1, 0, 0) : new Vector3(0, 0, candidate.Z > (collider.Min.Y + collider.Max.Y) * 0.5f ? 1 : -1);
            return false;
        }
        result = candidate;
        return true;
    }

    private static bool OverlapsVertical(float bottom, float height, StationCollider collider) => bottom < collider.MaxY - CapsuleControllerMath.Skin && bottom + height > collider.MinY + CapsuleControllerMath.Skin;
    private static bool CircleOverlapsBox(Vector3 position, float radius, StationCollider collider)
    {
        Vector2 nearest = new(MathHelper.Clamp(position.X, collider.Min.X, collider.Max.X), MathHelper.Clamp(position.Z, collider.Min.Y, collider.Max.Y));
        return Vector2.DistanceSquared(new Vector2(position.X, position.Z), nearest) < radius * radius;
    }

    private void AddBox(Vector3 center, Vector3 size, string textureKey, float uvRepeat, bool unlit, bool solid)
    {
        Vector3 min = center - size * 0.5f;
        Vector3 max = center + size * 0.5f;
        List<VertexPositionNormalTexture> vertices = new();
        List<int> indices = new();
        AddFace(vertices, indices, new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z), Vector3.Backward, uvRepeat);
        AddFace(vertices, indices, new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), Vector3.Forward, uvRepeat);
        AddFace(vertices, indices, new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), Vector3.Left, uvRepeat);
        AddFace(vertices, indices, new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), Vector3.Right, uvRepeat);
        AddFace(vertices, indices, new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), Vector3.Up, uvRepeat);
        AddFace(vertices, indices, new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z), Vector3.Down, uvRepeat);
        _surfaces.Add(new StationSurface(vertices.ToArray(), indices.ToArray(), _textures[textureKey], unlit, GetSurfaceTint(textureKey)));
        if (solid) _colliders.Add(new StationCollider(new Vector2(min.X, min.Z), new Vector2(max.X, max.Z), min.Y, max.Y, textureKey));
    }

    private void AddRamp(Vector3 center, Vector3 size, string textureKey)
    {
        Vector3 min = center - size * 0.5f;
        Vector3 max = center + size * 0.5f;
        Vector3 lowFront = new(min.X, min.Y, min.Z);
        Vector3 highBack = new(min.X, max.Y, max.Z);
        Vector3 lowFrontRight = new(max.X, min.Y, min.Z);
        Vector3 highBackRight = new(max.X, max.Y, max.Z);
        Vector3 normal = Vector3.Normalize(new Vector3(0, size.Z, -size.Y));
        List<VertexPositionNormalTexture> vertices = new();
        List<int> indices = new();
        AddFace(vertices, indices, lowFront, lowFrontRight, highBackRight, highBack, normal, 2.0f);
        AddFace(vertices, indices, new Vector3(min.X, min.Y, max.Z), highBack, highBackRight, new Vector3(max.X, min.Y, max.Z), Vector3.Backward, 1.0f);
        AddFace(vertices, indices, new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), lowFrontRight, Vector3.Down, 1.0f);
        AddFace(vertices, indices, lowFront, highBack, new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, min.Y, min.Z), Vector3.Left, 1.0f);
        AddFace(vertices, indices, lowFrontRight, new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), highBackRight, Vector3.Right, 1.0f);
        _surfaces.Add(new StationSurface(vertices.ToArray(), indices.ToArray(), _textures[textureKey], false, GetSurfaceTint(textureKey)));
    }

    private static void AddFace(List<VertexPositionNormalTexture> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float repeat)
    {
        int start = vertices.Count;
        vertices.Add(new VertexPositionNormalTexture(a, normal, new Vector2(0, repeat)));
        vertices.Add(new VertexPositionNormalTexture(b, normal, new Vector2(repeat, repeat)));
        vertices.Add(new VertexPositionNormalTexture(c, normal, new Vector2(repeat, 0)));
        vertices.Add(new VertexPositionNormalTexture(d, normal, new Vector2(0, 0)));
        indices.Add(start); indices.Add(start + 1); indices.Add(start + 2); indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
    }

    private static Color GetSurfaceTint(string textureKey) => textureKey switch
    {
        "floor" => new Color(66, 72, 80),
        "structure" => new Color(48, 56, 66),
        "accent" => new Color(70, 52, 54),
        _ => Color.White
    };

    private sealed record StationSurface(VertexPositionNormalTexture[] Vertices, int[] Indices, Texture2D Texture, bool Unlit, Color Tint);
    private sealed record StationCollider(Vector2 Min, Vector2 Max, float MinY, float MaxY, string Label);
}
