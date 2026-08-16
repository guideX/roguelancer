using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Roguelancer;

public readonly record struct StationGroundHit(bool Found, Vector3 Point, Vector3 Normal, float SlopeDegrees, string SurfaceLabel);
public readonly record struct StationCameraCollision(bool Hit, Vector3 Position, string ObstacleLabel);

public enum StationAirlockState
{
    Closed,
    Opening,
    Open
}

/// <summary>Purpose-built industrial bay for the temporary developer on-foot mode.</summary>
public sealed class StationTestScene : IDisposable
{
    private readonly List<StationSurface> _surfaces = new();
    private readonly List<StationCollider> _colliders = new();
    private readonly List<AirlockDoorPart> _airlockDoorParts = new();
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly List<Texture2D> _shipMaterialTextures = new();
    private readonly HashSet<Texture2D> _ownedTextures = new();
    private GraphicsDevice? _graphicsDevice;
    private BasicEffect? _effect;
    private bool _shipMaterialStateLogged;
    private StationAirlockState _airlockState = StationAirlockState.Closed;
    private float _airlockDoorProgress;
    private Ship? _parkedShip;
    private bool _hasParkedShipBounds;
    private Vector3 _parkedShipMinimum;
    private Vector3 _parkedShipMaximum;

    private const float AirlockAnimationSeconds = 0.75f;
    private const float AirlockTraversalProgress = 0.65f;

    public Vector3 SpawnPosition { get; } = new(-8.5f, 0.0f, -7.0f);
    public float SpawnYawDegrees { get; } = 42.0f;
    // These are station-local presentation coordinates. They are deliberately
    // separate from Ship.Position, which remains in the spaceflight system.
    public Vector3 DockedShipPosition { get; } = new(0.0f, 0.05f, 1.5f);
    public Matrix DockedShipOrientation { get; } = Matrix.Identity;
    public Vector3 AirlockInteractionPosition { get; } = new(0.0f, 0.0f, 14.25f);
    public Vector3 DockTechnicianPosition { get; } = new(-9.0f, 0.0f, 7.5f);
    public float DockTechnicianYawDegrees { get; } = 90.0f;
    public float StationAttendantYawDegrees { get; } = 180.0f;
    public Vector3 StationAttendantPosition { get; } = new(-3.5f, 0.0f, 32.9f);
    public Vector3 ShipDealerPosition { get; } = new(7.2f, 0.0f, 35.85f);
    public float ShipDealerYawDegrees { get; } = -90.0f;
    public Vector3 EquipmentInteractionPosition { get; } = new(7.2f, 0.0f, 42.0f);
    public Vector3 BarInteractionPosition { get; } = new(-7.2f, 0.0f, 42.0f);
    public string ShipScaleNote => "Uses Ship.Draw's shared 0.1 model scale and correction; bay units are treated as metres and the human capsule is 1.8m tall.";
    public Ship? ParkedShip => _parkedShip;
    public StationAirlockState AirlockState => _airlockState;
    public float AirlockDoorProgress => _airlockDoorProgress;
    public bool IsAirlockOpen => _airlockState == StationAirlockState.Open;
    public string AirlockActionLabel => _airlockState switch
    {
        StationAirlockState.Closed => "Press E to open",
        StationAirlockState.Opening => "Opening...",
        _ => "Open"
    };

    /// <summary>
    /// Refresh the physical station representation from the authoritative ship.
    /// The model-derived envelope is intentionally a small set of boxes rather
    /// than triangle collision, which keeps the on-foot controller bounded while
    /// avoiding a stale Scimitar-sized obstacle after a purchase.
    /// </summary>
    public bool RefreshParkedShip(Ship ship)
    {
        _parkedShip = ship;
        _colliders.RemoveAll(collider => collider.IsParkedShip);
        _hasParkedShipBounds = TryGetParkedShipBounds(ship, out _parkedShipMinimum, out _parkedShipMaximum);
        if (!_hasParkedShipBounds)
        {
            AddFallbackParkedShipColliders();
            return false;
        }

        AddParkedShipColliders(_parkedShipMinimum, _parkedShipMaximum);
        Console.WriteLine($"[STATION] Parked ship refreshed: {ship.DisplayName} bounds=({_parkedShipMinimum.X:0.00},{_parkedShipMinimum.Y:0.00},{_parkedShipMinimum.Z:0.00})..({_parkedShipMaximum.X:0.00},{_parkedShipMaximum.Y:0.00},{_parkedShipMaximum.Z:0.00})");
        return true;
    }

    /// <summary>
    /// Centralized boarding approximation derived from the currently parked
    /// model bounds. Model-specific cockpit metadata can replace this later
    /// without changing the interaction flow.
    /// </summary>
    public Vector3 GetBoardingPoint(Ship ship)
    {
        if (ship != null && (!_hasParkedShipBounds || !ReferenceEquals(ship, _parkedShip)))
        {
            _hasParkedShipBounds = TryGetParkedShipBounds(ship, out _parkedShipMinimum, out _parkedShipMaximum);
        }

        if (!_hasParkedShipBounds)
        {
            return DockedShipPosition + Vector3.Right * 6.8f;
        }

        float boardingX = MathHelper.Clamp(_parkedShipMaximum.X + 1.35f, -15.5f, 15.5f);
        float boardingZ = MathHelper.Clamp((_parkedShipMinimum.Z + _parkedShipMaximum.Z) * 0.5f, -4.5f, 5.5f);
        return new Vector3(boardingX, 0.05f, boardingZ);
    }

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
        LoadShipMaterialTextures(content);
        BuildLayout();
    }

    public void ResetSession()
    {
        _airlockState = StationAirlockState.Closed;
        _airlockDoorProgress = 0.0f;
        UpdateAirlockDoorGeometry();
    }

    public bool TryOpenAirlock()
    {
        if (_airlockState != StationAirlockState.Closed) return false;
        _airlockState = StationAirlockState.Opening;
        Console.WriteLine("[STATION] Airlock opening");
        return true;
    }

    public void Update(float deltaSeconds)
    {
        if (_airlockState != StationAirlockState.Opening) return;

        _airlockDoorProgress = MathHelper.Clamp(
            _airlockDoorProgress + MathF.Max(0.0f, deltaSeconds) / AirlockAnimationSeconds,
            0.0f,
            1.0f);
        if (_airlockDoorProgress >= 1.0f)
        {
            _airlockDoorProgress = 1.0f;
            _airlockState = StationAirlockState.Open;
            Console.WriteLine("[STATION] Airlock open; player and camera collision cleared");
        }
        UpdateAirlockDoorGeometry();
    }

    public void Draw(Matrix view, Matrix projection)
    {
        if (_graphicsDevice is null || _effect is null) return;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;
        _effect.VertexColorEnabled = false;
        _graphicsDevice.BlendState = BlendState.Opaque;
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
                _effect.AmbientLightColor = new Vector3(0.32f, 0.36f, 0.43f);
                _effect.DirectionalLight0.Direction = new Vector3(-0.35f, -0.85f, -0.40f);
                _effect.DirectionalLight0.DiffuseColor = new Vector3(0.68f, 0.73f, 0.84f);
                _effect.DirectionalLight0.SpecularColor = new Vector3(0.24f, 0.27f, 0.34f);
                _effect.DirectionalLight0.Enabled = true;
                _effect.DirectionalLight1.Enabled = false;
                _effect.DirectionalLight2.Enabled = false;
            }
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, surface.Vertices, 0, surface.Vertices.Length, surface.Indices, 0, surface.Indices.Length / 3);

            if (surface.Emissive)
            {
                _graphicsDevice.BlendState = BlendState.Additive;
                _effect.LightingEnabled = false;
                _effect.DiffuseColor = surface.Tint.ToVector3();
                _effect.Alpha = 0.38f;
                _effect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, surface.Vertices, 0, surface.Vertices.Length, surface.Indices, 0, surface.Indices.Length / 3);
                _graphicsDevice.BlendState = BlendState.Opaque;
            }
        }
    }

    public void DrawShipModel(Ship ship, Matrix view, Matrix projection, Vector3 lightDirection)
    {
        if (ship?.Model is null || _graphicsDevice is null)
        {
            if (!_shipMaterialStateLogged)
            {
                _shipMaterialStateLogged = true;
                Console.WriteLine("[STATION TEST] Parked player ship could not render: the authoritative ship has no model.");
            }
            return;
        }

        Model model = ship.Model;
        Matrix world = Ship.CreateModelWorldMatrix(DockedShipPosition, DockedShipOrientation, ship.ModelRotationCorrection);
        int texturedEffects = 0;
        int effectCount = 0;
        List<string> materialDiagnostics = new();
        int effectSlot = 0;
        foreach (ModelMesh mesh in model.Meshes)
        {
            foreach (BasicEffect effect in mesh.Effects)
            {
                effectCount++;
                materialDiagnostics.Add($"{mesh.Name ?? "unnamed"}:{effect.DiffuseColor}");
                Ship.ConfigureModelEffect(
                    effect,
                    world,
                    view,
                    projection,
                    lightDirection,
                    new Vector3(0.78f, 0.78f, 0.86f),
                    new Vector3(0.40f, 0.40f, 0.46f),
                    new Vector3(0.16f, 0.17f, 0.21f));
                // ModelProcessor supplies each mesh's imported BasicEffect
                // texture; re-enable it explicitly for this presentation pass
                // without replacing the material binding.
                int materialIndex = effectSlot / 2;
                bool isScimitar = ship.ModelPath?.Contains("scimitar", StringComparison.OrdinalIgnoreCase) == true;
                if (isScimitar && effect.Texture == null && materialIndex < _shipMaterialTextures.Count)
                    effect.Texture = _shipMaterialTextures[materialIndex];
                effect.TextureEnabled = effect.Texture != null;
                if (effect.Texture != null && effect.TextureEnabled) texturedEffects++;
                effectSlot++;
            }
            mesh.Draw();
        }
        if (!_shipMaterialStateLogged)
        {
            _shipMaterialStateLogged = true;
            Console.WriteLine($"[STATION TEST] Parked ship material pass: name={ship.DisplayName}, model={ship.ModelPath}, meshes={model.Meshes.Count}, effects={effectCount}, texturedEffects={texturedEffects}");
            Console.WriteLine($"[STATION TEST] Imported ship effect materials: {string.Join(" | ", materialDiagnostics)}");
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

    public bool IsOutOfBounds(Vector3 position) => position.X < -17.0f || position.X > 17.0f || position.Z < -17.0f || position.Z > 48.0f;

    /// <summary>
    /// Sweeps a small camera sphere along the pivot-to-desired segment against the
    /// same box representation used by the capsule controller. The result is a
    /// safe point just before the first obstruction, with a small visual skin.
    /// </summary>
    public StationCameraCollision ResolveCameraPosition(Vector3 pivot, Vector3 desired, float cameraRadius = 0.30f, float safetyOffset = 0.12f, float minimumDistance = 0.58f)
    {
        Vector3 delta = desired - pivot;
        float desiredDistance = delta.Length();
        if (desiredDistance <= 0.0001f) return new StationCameraCollision(false, desired, string.Empty);

        Vector3 direction = delta / desiredDistance;
        float nearestDistance = desiredDistance;
        string nearestLabel = string.Empty;
        foreach (StationCollider collider in _colliders)
        {
            if (!collider.BlocksCamera || !IsColliderActive(collider)) continue;

            Vector3 min = new(collider.Min.X - cameraRadius, collider.MinY - cameraRadius, collider.Min.Y - cameraRadius);
            Vector3 max = new(collider.Max.X + cameraRadius, collider.MaxY + cameraRadius, collider.Max.Y + cameraRadius);
            if (!SegmentIntersectsAabb(pivot, delta, min, max, out float entry)) continue;

            float hitDistance = MathHelper.Clamp(entry * desiredDistance - safetyOffset, minimumDistance, desiredDistance);
            if (hitDistance < nearestDistance)
            {
                nearestDistance = hitDistance;
                nearestLabel = collider.Label;
            }
        }

        bool hit = !string.IsNullOrEmpty(nearestLabel);
        return new StationCameraCollision(hit, pivot + direction * nearestDistance, nearestLabel);
    }

    public void Dispose()
    {
        foreach (Texture2D texture in _ownedTextures) texture.Dispose();
        _ownedTextures.Clear();
        _textures.Clear();
        _effect?.Dispose();
        _effect = null;
        _surfaces.Clear();
        _colliders.Clear();
        _airlockDoorParts.Clear();
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

    private void LoadShipMaterialTextures(ContentManager content)
    {
        string[] materialNames =
        {
            "hull1", "hull2", "hullblack", "equip", "intake1", "intake2", "grill",
            "mount", "metal", "wing", "cockpit1", "cockpit2", "screens"
        };
        foreach (string materialName in materialNames)
        {
            string assetName = $"SHIPS/scimitar/Scimitar2_{materialName}_0";
            try
            {
                _shipMaterialTextures.Add(content.Load<Texture2D>(assetName));
                Console.WriteLine($"[STATION TEST] Loaded ship material {assetName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STATION TEST] Ship material fallback for {assetName}: {ex.Message}");
            }
        }
    }

    private void BuildLayout()
    {
        _surfaces.Clear();
        _colliders.Clear();
        _airlockDoorParts.Clear();

        // The existing docking bay remains the anchor of the scene. Its rear
        // boundary now opens into a compact station interior that continues to
        // use the same 1-unit = 1-metre collision and presentation scale.
        AddBox(new Vector3(0, -0.12f, 15.0f), new Vector3(36, 0.24f, 66), "floor", 5.0f, false, false);
        AddBox(new Vector3(-17.8f, 5.0f, 15.0f), new Vector3(0.4f, 10.0f, 66), "structure", 3.0f, false, true);
        AddBox(new Vector3(17.8f, 5.0f, 15.0f), new Vector3(0.4f, 10.0f, 66), "structure", 3.0f, false, true);
        AddBox(new Vector3(0, 10.0f, 15.0f), new Vector3(36, 0.35f, 66), "structure", 3.0f, false, true);
        AddBox(new Vector3(0, 5.0f, 48.8f), new Vector3(36, 10.0f, 0.4f), "structure", 3.0f, false, true);

        // Docking pad and perimeter hazard markings.
        AddBox(new Vector3(0, 0.035f, 0.8f), new Vector3(16.0f, 0.07f, 12.0f), "floor", 2.0f, false, false);
        AddBox(new Vector3(0, 0.08f, -5.15f), new Vector3(16.0f, 0.035f, 0.55f), "hazard", 5.0f, true, false);
        AddBox(new Vector3(0, 0.08f, 6.75f), new Vector3(16.0f, 0.035f, 0.55f), "hazard", 5.0f, true, false);
        AddBox(new Vector3(-7.75f, 0.08f, 0.8f), new Vector3(0.55f, 0.035f, 11.9f), "hazard", 4.0f, true, false);
        AddBox(new Vector3(7.75f, 0.08f, 0.8f), new Vector3(0.55f, 0.035f, 11.9f), "hazard", 4.0f, true, false);

        // A restrained overhead service gantry frames the parked craft and
        // gives the docking perimeter a functional maintenance purpose.
        AddBox(new Vector3(-6.6f, 2.0f, 5.7f), new Vector3(0.30f, 4.0f, 0.30f), "accent", 1.0f, false, true);
        AddBox(new Vector3(6.6f, 2.0f, 5.7f), new Vector3(0.30f, 4.0f, 0.30f), "accent", 1.0f, false, true);
        AddBox(new Vector3(0.0f, 4.0f, 5.7f), new Vector3(13.5f, 0.30f, 0.30f), "accent", 1.0f, false, true);
        AddBox(new Vector3(0.0f, 3.78f, 5.48f), new Vector3(9.5f, 0.08f, 0.12f), "glow", 1.0f, true, false, emissive: true);

        // Raised walkway/ramp to the large airlock-style exit. The walkway is
        // camera-blocking even though its top surface is handled by GetGround.
        AddBox(new Vector3(12.5f, 0.25f, 10.75f), new Vector3(9.0f, 0.5f, 8.5f), "floor", 3.0f, false, false, blocksCamera: true);
        AddRamp(new Vector3(12.5f, 0.0f, 5.0f), new Vector3(9.0f, 0.45f, 3.0f), "floor");
        AddBox(new Vector3(12.5f, 0.54f, 6.5f), new Vector3(9.0f, 0.06f, 0.45f), "hazard", 3.0f, true, false);

        // Safe pedestrian route: a darker lane leaves the pad, climbs the ramp,
        // then turns across the back of the bay toward the closed airlock.
        AddBox(new Vector3(9.15f, 0.025f, -6.0f), new Vector3(1.7f, 0.05f, 18.0f), "structure", 2.0f, false, false);
        AddBox(new Vector3(9.15f, 0.07f, -6.0f), new Vector3(0.08f, 0.025f, 17.0f), "glow", 2.0f, true, false, emissive: true);
        AddBox(new Vector3(9.15f, 0.07f, 15.45f), new Vector3(13.0f, 0.05f, 1.75f), "structure", 2.0f, false, false);
        AddBox(new Vector3(3.0f, 0.10f, 15.45f), new Vector3(11.5f, 0.025f, 0.08f), "glow", 2.0f, true, false, emissive: true);

        // Low railings make the raised route read as a deliberate pedestrian
        // crossing while leaving the ramp and airlock approach open.
        AddBox(new Vector3(8.15f, 1.0f, 10.75f), new Vector3(0.18f, 1.0f, 7.2f), "accent", 1.0f, false, true);
        AddBox(new Vector3(16.85f, 1.0f, 10.75f), new Vector3(0.18f, 1.0f, 7.2f), "accent", 1.0f, false, true);
        AddBox(new Vector3(8.2f, 1.55f, 10.75f), new Vector3(0.25f, 0.12f, 7.2f), "glow", 2.0f, true, false, emissive: true);

        // Structural ribs and ceiling services.
        for (int z = -14; z <= 14; z += 7)
        {
            AddBox(new Vector3(-14.5f, 5.0f, z), new Vector3(0.55f, 10.0f, 0.55f), "accent", 1.0f, false, true);
            AddBox(new Vector3(14.5f, 5.0f, z), new Vector3(0.55f, 10.0f, 0.55f), "accent", 1.0f, false, true);
            AddBox(new Vector3(0, 9.25f, z), new Vector3(29.5f, 0.55f, 0.55f), "accent", 2.0f, false, true);
        }
        AddBox(new Vector3(-6.5f, 9.65f, -1.5f), new Vector3(8.0f, 0.08f, 0.30f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(6.5f, 9.65f, -1.5f), new Vector3(8.0f, 0.08f, 0.30f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(12.5f, 9.55f, 10.5f), new Vector3(6.0f, 0.08f, 0.30f), "glow", 1.0f, true, false, emissive: true);

        // Functional airlock frame and upward-sliding panel. The panel's
        // surface moves during Update while its collider is disabled only once
        // a standing player can safely traverse the opening.
        AddBox(new Vector3(-5.0f, 3.0f, 17.35f), new Vector3(0.75f, 6.0f, 0.65f), "accent", 1.0f, false, true);
        AddBox(new Vector3(5.0f, 3.0f, 17.35f), new Vector3(0.75f, 6.0f, 0.65f), "accent", 1.0f, false, true);
        AddBox(new Vector3(0, 6.1f, 17.35f), new Vector3(10.25f, 0.75f, 0.65f), "accent", 2.0f, false, true);
        AddBox(new Vector3(0, 2.7f, 17.2f), new Vector3(8.8f, 5.0f, 0.20f), "door", 1.0f, false, true, airlockDoor: true);
        AddBox(new Vector3(-4.1f, 3.0f, 17.0f), new Vector3(0.18f, 3.2f, 0.10f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(4.1f, 3.0f, 17.0f), new Vector3(0.18f, 3.2f, 0.10f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(0, 5.0f, 17.0f), new Vector3(8.4f, 0.18f, 0.10f), "hazard", 2.0f, true, false);
        AddBox(new Vector3(0.0f, 0.18f, 16.45f), new Vector3(10.4f, 0.18f, 1.15f), "structure", 2.0f, false, false);
        AddBox(new Vector3(-5.55f, 2.0f, 16.55f), new Vector3(0.30f, 4.0f, 1.0f), "accent", 1.0f, false, true);
        AddBox(new Vector3(5.55f, 2.0f, 16.55f), new Vector3(0.30f, 4.0f, 1.0f), "accent", 1.0f, false, true);
        AddBox(new Vector3(0.0f, 4.2f, 16.55f), new Vector3(10.8f, 0.30f, 1.0f), "accent", 1.0f, false, true);

        // Service corridor: a deliberately short, five-metre-wide transition
        // between the harsh bay and the pedestrian concourse.
        AddBox(new Vector3(0.0f, 0.03f, 23.4f), new Vector3(6.0f, 0.08f, 11.8f), "structure", 2.0f, false, false);
        AddBox(new Vector3(-2.95f, 2.1f, 23.6f), new Vector3(0.35f, 4.2f, 12.4f), "structure", 2.0f, false, true);
        AddBox(new Vector3(2.95f, 2.1f, 23.6f), new Vector3(0.35f, 4.2f, 12.4f), "structure", 2.0f, false, true);
        AddBox(new Vector3(-2.65f, 2.55f, 22.1f), new Vector3(0.12f, 2.6f, 3.0f), "accent", 1.0f, false, true);
        AddBox(new Vector3(2.65f, 2.55f, 25.4f), new Vector3(0.12f, 2.6f, 3.0f), "accent", 1.0f, false, true);
        AddBox(new Vector3(-2.70f, 3.6f, 23.5f), new Vector3(0.10f, 0.10f, 10.0f), "glow", 3.0f, true, false, emissive: true);
        AddBox(new Vector3(2.70f, 3.6f, 23.5f), new Vector3(0.10f, 0.10f, 10.0f), "glow", 3.0f, true, false, emissive: true);
        AddBox(new Vector3(0.0f, 3.85f, 23.5f), new Vector3(5.2f, 0.18f, 0.22f), "glow", 2.0f, true, false, emissive: true);
        AddBox(new Vector3(-2.25f, 2.2f, 27.2f), new Vector3(0.32f, 4.0f, 0.32f), "accent", 1.0f, false, true);
        AddBox(new Vector3(2.25f, 2.2f, 27.2f), new Vector3(0.32f, 4.0f, 0.32f), "accent", 1.0f, false, true);
        AddBox(new Vector3(0.0f, 4.0f, 29.0f), new Vector3(5.9f, 0.35f, 0.35f), "accent", 1.0f, false, true);

        // Main concourse: roughly 20m wide by 18m deep, with the corridor
        // opening centered on its near wall and enough room for future NPCs.
        AddBox(new Vector3(-10.0f, 4.5f, 38.5f), new Vector3(0.35f, 9.0f, 18.0f), "structure", 2.0f, false, true);
        AddBox(new Vector3(10.0f, 4.5f, 38.5f), new Vector3(0.35f, 9.0f, 18.0f), "structure", 2.0f, false, true);
        AddBox(new Vector3(0.0f, 4.5f, 47.4f), new Vector3(20.35f, 9.0f, 0.35f), "structure", 2.0f, false, true);
        AddBox(new Vector3(-6.4f, 4.5f, 29.5f), new Vector3(7.2f, 9.0f, 0.35f), "structure", 2.0f, false, true);
        AddBox(new Vector3(6.4f, 4.5f, 29.5f), new Vector3(7.2f, 9.0f, 0.35f), "structure", 2.0f, false, true);
        AddBox(new Vector3(0.0f, 7.8f, 30.0f), new Vector3(19.5f, 0.30f, 0.35f), "accent", 1.0f, false, true);
        AddBox(new Vector3(0.0f, 6.8f, 34.0f), new Vector3(16.0f, 0.16f, 0.22f), "glow", 3.0f, true, false, emissive: true);
        AddBox(new Vector3(0.0f, 6.8f, 43.5f), new Vector3(16.0f, 0.16f, 0.22f), "glow", 3.0f, true, false, emissive: true);
        AddBox(new Vector3(-8.8f, 4.0f, 34.2f), new Vector3(0.45f, 8.0f, 0.45f), "accent", 1.0f, false, true);
        AddBox(new Vector3(8.8f, 4.0f, 34.2f), new Vector3(0.45f, 8.0f, 0.45f), "accent", 1.0f, false, true);
        AddBox(new Vector3(-8.8f, 4.0f, 44.0f), new Vector3(0.45f, 8.0f, 0.45f), "accent", 1.0f, false, true);
        AddBox(new Vector3(8.8f, 4.0f, 44.0f), new Vector3(0.45f, 8.0f, 0.45f), "accent", 1.0f, false, true);

        // Station-access checkpoint and attendant position near the corridor.
        AddBox(new Vector3(-3.5f, 0.55f, 32.1f), new Vector3(3.8f, 1.1f, 0.75f), "accent", 1.0f, false, true);
        AddBox(new Vector3(-3.5f, 1.35f, 32.1f), new Vector3(2.5f, 0.08f, 0.50f), "glow", 1.0f, true, false, emissive: true);

        // Ship Dealer: an open counter, terminal, customer side, and a clear
        // sightline back toward the corridor/docking infrastructure.
        AddBox(new Vector3(7.0f, 0.55f, 35.0f), new Vector3(5.0f, 1.1f, 0.75f), "accent", 1.0f, false, true);
        AddBox(new Vector3(7.0f, 1.35f, 35.0f), new Vector3(4.2f, 0.08f, 0.50f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(8.7f, 1.65f, 36.8f), new Vector3(0.35f, 3.3f, 2.4f), "structure", 1.0f, false, true);
        AddBox(new Vector3(8.35f, 1.95f, 36.0f), new Vector3(0.08f, 1.2f, 1.45f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(5.0f, 0.45f, 37.8f), new Vector3(2.2f, 0.9f, 0.55f), "structure", 1.0f, false, true);

        // Equipment is a recognizable service kiosk used by the on-foot terminal.
        AddBox(new Vector3(7.65f, 1.5f, 42.1f), new Vector3(3.2f, 3.0f, 0.55f), "structure", 1.0f, false, true);
        AddBox(new Vector3(7.65f, 3.2f, 42.1f), new Vector3(2.6f, 0.10f, 0.12f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(6.05f, 0.9f, 42.1f), new Vector3(0.55f, 1.8f, 2.8f), "accent", 1.0f, false, true);
        AddBox(new Vector3(5.72f, 1.55f, 42.1f), new Vector3(0.08f, 0.8f, 1.3f), "glow", 1.0f, true, false, emissive: true);

        // Bar placeholder: framed common-area entrance with a closed dark
        // boundary suggesting a larger social room beyond this phase.
        AddBox(new Vector3(-8.55f, 2.4f, 41.8f), new Vector3(0.40f, 4.8f, 3.8f), "accent", 1.0f, false, true);
        AddBox(new Vector3(-5.95f, 2.4f, 41.8f), new Vector3(0.40f, 4.8f, 3.8f), "accent", 1.0f, false, true);
        AddBox(new Vector3(-7.25f, 4.8f, 41.8f), new Vector3(3.0f, 0.45f, 3.8f), "accent", 1.0f, false, true);
        AddBox(new Vector3(-7.25f, 2.45f, 43.55f), new Vector3(2.3f, 4.7f, 0.20f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-7.25f, 4.0f, 40.0f), new Vector3(2.2f, 0.12f, 0.14f), "glow", 1.0f, true, false, emissive: true);

        // Service alcove, tool carts, and a few intentionally simple crates.
        AddBox(new Vector3(-14.0f, 2.4f, 7.0f), new Vector3(0.4f, 4.8f, 9.0f), "accent", 2.0f, false, true);
        AddBox(new Vector3(-13.3f, 1.4f, 3.0f), new Vector3(2.6f, 2.8f, 0.55f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-13.0f, 2.8f, 3.0f), new Vector3(2.0f, 0.12f, 0.40f), "glow", 1.0f, true, false, emissive: true);
        AddBox(new Vector3(-11.2f, 0.55f, 8.5f), new Vector3(2.3f, 1.1f, 1.25f), "accent", 1.0f, false, true);
        AddBox(new Vector3(-11.2f, 1.15f, 8.5f), new Vector3(1.9f, 0.08f, 0.92f), "hazard", 1.0f, true, false);
        AddBox(new Vector3(-10.0f, 0.18f, 8.5f), new Vector3(0.16f, 0.30f, 1.4f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-12.4f, 0.18f, 8.5f), new Vector3(0.16f, 0.30f, 1.4f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-12.7f, 0.55f, 10.7f), new Vector3(3.0f, 1.1f, 1.5f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-12.7f, 1.55f, 10.7f), new Vector3(2.7f, 0.08f, 1.2f), "hazard", 1.0f, true, false);
        AddBox(new Vector3(14.4f, 0.8f, -10.5f), new Vector3(2.0f, 1.6f, 2.0f), "accent", 1.0f, false, true);
        AddBox(new Vector3(11.7f, 0.55f, -12.0f), new Vector3(1.3f, 1.1f, 1.3f), "structure", 1.0f, false, true);
        AddBox(new Vector3(-13.0f, 0.6f, -12.5f), new Vector3(1.7f, 1.2f, 1.7f), "structure", 1.0f, false, true);

        // Initial fallback until the authoritative model is available. Entry
        // immediately refreshes this with model-derived dimensions.
        AddFallbackParkedShipColliders();
    }

    private void AddFallbackParkedShipColliders()
    {
        AddParkedShipColliders(
            new Vector3(-5.75f, 0.0f, -0.95f),
            new Vector3(5.75f, 2.45f, 4.65f));
    }

    private void AddParkedShipColliders(Vector3 minimum, Vector3 maximum)
    {
        float minX = MathHelper.Clamp(MathF.Min(minimum.X, maximum.X), -15.5f, 15.5f);
        float maxX = MathHelper.Clamp(MathF.Max(minimum.X, maximum.X), -15.5f, 15.5f);
        float minZ = MathHelper.Clamp(MathF.Min(minimum.Z, maximum.Z), -6.0f, 8.0f);
        float maxZ = MathHelper.Clamp(MathF.Max(minimum.Z, maximum.Z), -6.0f, 8.0f);
        float minY = MathHelper.Clamp(MathF.Min(minimum.Y, maximum.Y), 0.0f, 1.0f);
        float maxY = MathHelper.Clamp(MathF.Max(minimum.Y, maximum.Y), 1.0f, 7.5f);
        if (maxX - minX < 1.5f) maxX = minX + 1.5f;
        if (maxZ - minZ < 1.5f) maxZ = minZ + 1.5f;

        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;
        float centralHalfWidth = MathHelper.Clamp((maxX - minX) * 0.28f, 1.2f, 4.5f);
        float centralMinZ = MathHelper.Lerp(minZ, centerZ, 0.18f);
        float centralMaxZ = MathHelper.Lerp(maxZ, centerZ, 0.82f);
        _colliders.Add(new StationCollider(
            new Vector2(centerX - centralHalfWidth, centralMinZ),
            new Vector2(centerX + centralHalfWidth, centralMaxZ),
            minY,
            maxY,
            "parked ship central hull",
            true,
            true,
            false,
            true));

        float wingWidth = MathHelper.Clamp((maxX - minX) * 0.22f, 0.8f, 2.6f);
        _colliders.Add(new StationCollider(
            new Vector2(minX, minZ),
            new Vector2(MathHelper.Min(centerX - centralHalfWidth, minX + wingWidth), maxZ),
            minY,
            MathHelper.Min(maxY, 3.0f),
            "parked ship port wing",
            true,
            true,
            false,
            true));
        _colliders.Add(new StationCollider(
            new Vector2(MathHelper.Max(centerX + centralHalfWidth, maxX - wingWidth), minZ),
            new Vector2(maxX, maxZ),
            minY,
            MathHelper.Min(maxY, 3.0f),
            "parked ship starboard wing",
            true,
            true,
            false,
            true));
    }

    private bool TryGetParkedShipBounds(Ship ship, out Vector3 minimum, out Vector3 maximum)
    {
        minimum = Vector3.Zero;
        maximum = Vector3.Zero;
        if (ship?.Model == null) return false;

        Matrix world = Ship.CreateModelWorldMatrix(DockedShipPosition, DockedShipOrientation, ship.ModelRotationCorrection);
        bool found = false;
        foreach (ModelMesh mesh in ship.Model.Meshes)
        {
            Matrix meshWorld = (mesh.ParentBone?.Transform ?? Matrix.Identity) * world;
            Vector3 meshCenter = Vector3.Transform(mesh.BoundingSphere.Center, meshWorld);
            float radius = mesh.BoundingSphere.Radius * 0.1f;
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)) continue;

            Vector3 meshMinimum = meshCenter - new Vector3(radius);
            Vector3 meshMaximum = meshCenter + new Vector3(radius);
            if (!found)
            {
                minimum = meshMinimum;
                maximum = meshMaximum;
                found = true;
            }
            else
            {
                minimum = Vector3.Min(minimum, meshMinimum);
                maximum = Vector3.Max(maximum, meshMaximum);
            }
        }

        if (!found ||
            float.IsNaN(minimum.X) || float.IsInfinity(minimum.X) ||
            float.IsNaN(maximum.X) || float.IsInfinity(maximum.X))
        {
            return false;
        }

        // Imported model spheres are useful for relative sizing but can include
        // large source-space padding. Keep the resulting service envelope within
        // the bay's pedestrian scale while retaining the model-derived center
        // and relative span for different hulls.
        Vector3 boundsCenter = (minimum + maximum) * 0.5f;
        float width = MathHelper.Clamp(maximum.X - minimum.X, 5.0f, 14.0f);
        float depth = MathHelper.Clamp(maximum.Z - minimum.Z, 5.0f, 11.0f);
        float height = MathHelper.Clamp(maximum.Y - minimum.Y, 2.0f, 5.0f);
        boundsCenter.X = MathHelper.Clamp(boundsCenter.X, -3.0f, 3.0f);
        boundsCenter.Z = MathHelper.Clamp(boundsCenter.Z, -1.5f, 3.0f);
        minimum = new Vector3(boundsCenter.X - width * 0.5f, 0.0f, boundsCenter.Z - depth * 0.5f);
        maximum = new Vector3(boundsCenter.X + width * 0.5f, height, boundsCenter.Z + depth * 0.5f);
        return true;
    }

    private bool TryResolveAxis(Vector3 current, Vector3 candidate, float capsuleHeight, bool grounded, ref Vector3 result, ref Vector3 wallNormal, bool axisX)
    {
        foreach (StationCollider collider in _colliders)
        {
            if (!collider.BlocksPlayer || !IsColliderActive(collider)) continue;
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

    private bool IsColliderActive(StationCollider collider)
    {
        return !collider.IsAirlockDoor || _airlockDoorProgress < AirlockTraversalProgress;
    }

    private void UpdateAirlockDoorGeometry()
    {
        if (_airlockDoorParts.Count == 0) return;

        Vector3 offset = Vector3.Up * (_airlockDoorProgress * 5.25f);
        foreach (AirlockDoorPart part in _airlockDoorParts)
        {
            for (int i = 0; i < part.Surface.Vertices.Length; i++)
            {
                VertexPositionNormalTexture vertex = part.Surface.Vertices[i];
                part.Surface.Vertices[i] = new VertexPositionNormalTexture(
                    part.ClosedPositions[i] + offset,
                    vertex.Normal,
                    vertex.TextureCoordinate);
            }
        }
    }

    private void AddBox(Vector3 center, Vector3 size, string textureKey, float uvRepeat, bool unlit, bool solid, bool blocksCamera = false, bool emissive = false, bool airlockDoor = false)
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
        StationSurface surface = new(vertices.ToArray(), indices.ToArray(), _textures[textureKey], unlit, emissive, GetSurfaceTint(textureKey));
        _surfaces.Add(surface);
        if (airlockDoor)
        {
            Vector3[] closedPositions = new Vector3[surface.Vertices.Length];
            for (int i = 0; i < closedPositions.Length; i++) closedPositions[i] = surface.Vertices[i].Position;
            _airlockDoorParts.Add(new AirlockDoorPart(surface, closedPositions));
        }
        if (solid || blocksCamera) _colliders.Add(new StationCollider(new Vector2(min.X, min.Z), new Vector2(max.X, max.Z), min.Y, max.Y, textureKey, solid, solid || blocksCamera, airlockDoor));
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
        _surfaces.Add(new StationSurface(vertices.ToArray(), indices.ToArray(), _textures[textureKey], false, false, GetSurfaceTint(textureKey)));
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
        "floor" => new Color(88, 94, 104),
        "structure" => new Color(68, 78, 92),
        "accent" => new Color(86, 64, 66),
        "glow" => new Color(255, 215, 150),
        _ => Color.White
    };

    private static bool SegmentIntersectsAabb(Vector3 origin, Vector3 delta, Vector3 min, Vector3 max, out float entry)
    {
        float tMin = 0.0f;
        float tMax = 1.0f;
        for (int axis = 0; axis < 3; axis++)
        {
            float originAxis = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            float deltaAxis = axis == 0 ? delta.X : axis == 1 ? delta.Y : delta.Z;
            float minAxis = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            float maxAxis = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;
            if (MathF.Abs(deltaAxis) < 0.00001f)
            {
                if (originAxis < minAxis || originAxis > maxAxis)
                {
                    entry = 0.0f;
                    return false;
                }
                continue;
            }

            float inverse = 1.0f / deltaAxis;
            float t0 = (minAxis - originAxis) * inverse;
            float t1 = (maxAxis - originAxis) * inverse;
            if (t0 > t1) (t0, t1) = (t1, t0);
            tMin = MathF.Max(tMin, t0);
            tMax = MathF.Min(tMax, t1);
            if (tMin > tMax)
            {
                entry = 0.0f;
                return false;
            }
        }

        entry = tMin;
        return tMax >= 0.0f && tMin <= 1.0f;
    }

    private sealed record StationSurface(VertexPositionNormalTexture[] Vertices, int[] Indices, Texture2D Texture, bool Unlit, bool Emissive, Color Tint);
    private sealed record AirlockDoorPart(StationSurface Surface, Vector3[] ClosedPositions);
    private sealed record StationCollider(
        Vector2 Min,
        Vector2 Max,
        float MinY,
        float MaxY,
        string Label,
        bool BlocksPlayer = false,
        bool BlocksCamera = false,
        bool IsAirlockDoor = false,
        bool IsParkedShip = false);
}
