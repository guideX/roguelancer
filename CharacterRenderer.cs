using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NMatrix = System.Numerics.Matrix4x4;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace Roguelancer;

/// <summary>
/// Renderer for one replaceable character instance. GPU skinning is preferred
/// when a compatible effect is supplied; the original CPU path remains
/// available for diagnostics and unsupported assets.
/// </summary>
public sealed class CharacterRenderer : IDisposable
{
    private readonly CharacterGltfAsset _asset;
    private readonly CharacterGraphicsResources _sharedGraphics;
    private readonly bool _ownsGraphicsResources;
    private readonly CharacterLocalBounds _localBounds;
    private readonly List<SkinnedPart> _parts = new();
    private NMatrix[] _worldPose = Array.Empty<NMatrix>();
    private bool[] _resolvedPose = Array.Empty<bool>();
    private NMatrix[] _skinPose = Array.Empty<NMatrix>();
    private GraphicsDevice? _graphicsDevice;
    private Effect? _gpuSkinningEffect;
    private bool _gpuSkinningEnabled;
    private Matrix[] _gpuPalette = new Matrix[CharacterSkinningConstants.MaxBonePaletteSize];

    public CharacterRenderer(CharacterAsset asset)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));
        _asset = asset.Model;
        _sharedGraphics = asset.Graphics;
        _localBounds = asset.LocalBounds;
    }

    // Kept for small tools/tests that construct a renderer directly. The
    // station runtime uses CharacterAsset so source and graphics resources are
    // shared across the player and NPC instances.
    public CharacterRenderer(CharacterGltfAsset asset)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _sharedGraphics = new CharacterGraphicsResources(asset);
        _ownsGraphicsResources = true;
        _localBounds = CalculateLocalBounds(asset);
    }

    public bool IsGpuSkinningEnabled => _gpuSkinningEnabled;
    public int SourceVertexCount { get; private set; }
    public int PrimitiveCount => _asset.Primitives.Count;
    public int DrawCallCount => _asset.Primitives.Count;
    public int SourceVertexBytesPerFrame => SourceVertexCount * VertexPositionNormalTexture.VertexDeclaration.VertexStride;
    public int BonePaletteUploadBytesPerFrame => _gpuSkinningEnabled
        ? _asset.Primitives.Count * CharacterSkinningConstants.MaxBonePaletteSize * 16 * sizeof(float)
        : 0;

    public void SetGpuSkinningEffect(Effect? effect)
    {
        if (_graphicsDevice is not null) throw new InvalidOperationException("The skinning effect must be selected before graphics are loaded.");
        _gpuSkinningEffect = effect;
    }

    public void LoadGraphics(GraphicsDevice graphicsDevice) => LoadGraphics(graphicsDevice, null);

    public void LoadGraphics(GraphicsDevice graphicsDevice, Effect? gpuSkinningEffect)
    {
        if (_graphicsDevice is not null) return;
        if (gpuSkinningEffect is not null) _gpuSkinningEffect = gpuSkinningEffect;
        _sharedGraphics.LoadGraphics(graphicsDevice);
        _graphicsDevice = graphicsDevice;
        _gpuSkinningEnabled = _gpuSkinningEffect is not null && _sharedGraphics.GpuSkinningSupported;
        _worldPose = new NMatrix[_asset.Nodes.Count];
        _resolvedPose = new bool[_asset.Nodes.Count];
        _skinPose = _asset.Skin is null ? Array.Empty<NMatrix>() : new NMatrix[_asset.Skin.JointNodeIndices.Length];

        SourceVertexCount = 0;
        foreach (CharacterGltfPrimitive primitive in _asset.Primitives) SourceVertexCount += primitive.Vertices.Length;

        // The GPU path uses immutable shared source buffers. Allocate the CPU
        // fallback only when it is selected, so normal multi-character runs do
        // not retain a dynamic vertex stream per instance.
        if (!_gpuSkinningEnabled)
        {
            for (int primitiveIndex = 0; primitiveIndex < _asset.Primitives.Count; primitiveIndex++)
            {
                CharacterGltfPrimitive primitive = _asset.Primitives[primitiveIndex];
                DynamicVertexBuffer vertices = new(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, primitive.Vertices.Length, BufferUsage.WriteOnly);
                _parts.Add(new SkinnedPart(primitive, vertices, _sharedGraphics.GetIndexBuffer(primitiveIndex), _asset.Skin is not null));
            }
        }

        Console.WriteLine($"[CHARACTER RENDERER] asset={_asset.SourcePath} vertices={SourceVertexCount} primitives={PrimitiveCount} draws={DrawCallCount} joints={_skinPose.Length} mode={(_gpuSkinningEnabled ? "gpu" : "cpu")}");
    }

    public bool IsVisible(Matrix world, BoundingFrustum frustum)
    {
        Vector3 center = Vector3.Transform(_localBounds.Center, world);
        float scaleX = new Vector3(world.M11, world.M12, world.M13).Length();
        float scaleY = new Vector3(world.M21, world.M22, world.M23).Length();
        float scaleZ = new Vector3(world.M31, world.M32, world.M33).Length();
        float scale = MathF.Max(scaleX, MathF.Max(scaleY, scaleZ));
        BoundingSphere sphere = new(center, MathF.Max(0.01f, _localBounds.Radius * scale));
        return frustum.Contains(sphere) != ContainmentType.Disjoint;
    }

    public void UpdatePose(NMatrix[] localPose, PerformanceDiagnostics? diagnostics = null)
    {
        if (_graphicsDevice is null) return;
        using (Measure(diagnostics, "station.character.hierarchy"))
        {
            NMatrix Resolve(int index)
            {
                if (_resolvedPose[index]) return _worldPose[index];
                _worldPose[index] = _asset.Nodes[index].ParentIndex >= 0
                    ? localPose[index] * Resolve(_asset.Nodes[index].ParentIndex)
                    : localPose[index];
                _resolvedPose[index] = true;
                return _worldPose[index];
            }

            Array.Clear(_resolvedPose, 0, _resolvedPose.Length);
            for (int i = 0; i < _asset.Nodes.Count; i++) Resolve(i);
        }

        using (Measure(diagnostics, "station.character.skin.matrices"))
        {
            if (_asset.Skin is not null)
            {
                for (int joint = 0; joint < _skinPose.Length; joint++)
                {
                    int node = _asset.Skin.JointNodeIndices[joint];
                    _skinPose[joint] = _asset.Skin.InverseBindMatrices[joint] * _worldPose[node];
                }
            }
        }

        if (_gpuSkinningEnabled)
        {
            diagnostics?.AddCounter("station.characters.skinned");
            diagnostics?.AddCounter("station.character.bone.matrices", _skinPose.Length);
            return;
        }

        int verticesSkinned = 0;
        using (Measure(diagnostics, "station.character.cpu.skin"))
        {
            foreach (SkinnedPart part in _parts)
            {
                VertexPositionNormalTexture[] output = part.PoseOutput;
                for (int i = 0; i < output.Length; i++)
                {
                    CpuSkinVertex source = part.PreparedVertices[i];
                    NVector3 position;
                    NVector3 normal;
                    if (source.HasSkin)
                    {
                        position = NVector3.Zero;
                        normal = NVector3.Zero;
                        AccumulateInfluence(source.Position, source.Normal, source.Joint0, source.Weight0, ref position, ref normal);
                        AccumulateInfluence(source.Position, source.Normal, source.Joint1, source.Weight1, ref position, ref normal);
                        AccumulateInfluence(source.Position, source.Normal, source.Joint2, source.Weight2, ref position, ref normal);
                        AccumulateInfluence(source.Position, source.Normal, source.Joint3, source.Weight3, ref position, ref normal);
                    }
                    else
                    {
                        position = source.Position;
                        normal = source.Normal;
                    }

                    if (normal.LengthSquared() < 0.00001f) normal = NVector3.UnitY;
                    output[i] = new VertexPositionNormalTexture(
                        ToXna(position),
                        ToXna(NVector3.Normalize(normal)),
                        source.TextureCoordinate);
                    verticesSkinned++;
                }
            }
        }

        using (Measure(diagnostics, "station.character.vertex.upload"))
        {
            foreach (SkinnedPart part in _parts)
                part.Vertices.SetData(part.PoseOutput, 0, part.PoseOutput.Length, SetDataOptions.Discard);
        }

        diagnostics?.AddCounter("station.characters.skinned");
        diagnostics?.AddCounter("station.character.vertices.skinned", verticesSkinned);
        diagnostics?.AddCounter("station.character.vertex.upload.calls.per.character", _parts.Count);
        diagnostics?.AddCounter("station.character.vertex.upload.bytes.per.character", SourceVertexBytesPerFrame);
    }

    public void Draw(BasicEffect effect, Matrix world, Matrix view, Matrix projection, Color tint, PerformanceDiagnostics? diagnostics = null)
    {
        Draw(
            effect,
            world,
            view,
            projection,
            tint,
            new Vector3(0.46f, 0.50f, 0.62f),
            new Vector3(0.72f, 0.76f, 0.90f),
            new Vector3(-0.35f, -0.85f, -0.40f),
            diagnostics);
    }

    public void Draw(
        BasicEffect effect,
        Matrix world,
        Matrix view,
        Matrix projection,
        Color tint,
        Vector3 ambientLightColor,
        Vector3 diffuseLightColor,
        Vector3 lightDirection,
        PerformanceDiagnostics? diagnostics = null)
    {
        if (_graphicsDevice is null) return;
        if (_gpuSkinningEnabled)
        {
            DrawGpu(world, view, projection, tint, ambientLightColor, diffuseLightColor, lightDirection, diagnostics);
            return;
        }

        using (Measure(diagnostics, "station.character.effect.setup"))
        {
            effect.World = world;
            effect.View = view;
            effect.Projection = projection;
            effect.LightingEnabled = true;
            effect.EnableDefaultLighting();
            effect.PreferPerPixelLighting = true;
            effect.SpecularPower = 14.0f;
            effect.AmbientLightColor = ambientLightColor;
            effect.DirectionalLight0.Direction = lightDirection;
            effect.DirectionalLight0.DiffuseColor = diffuseLightColor;
            effect.DirectionalLight0.SpecularColor = new Vector3(0.30f, 0.32f, 0.38f);
            effect.DirectionalLight0.Enabled = true;
            effect.DirectionalLight1.Enabled = false;
            effect.DirectionalLight2.Enabled = false;
            effect.TextureEnabled = true;
        }

        foreach (SkinnedPart part in _parts)
        {
            CharacterGltfMaterial material = GetMaterial(part.Source.MaterialIndex);
            using (Measure(diagnostics, "station.character.effect.setup"))
            {
                effect.Texture = _sharedGraphics.GetTexture(material.TextureIndex);
                effect.DiffuseColor = TintColor(tint, material.BaseColor);
                effect.Alpha = tint.A / 255.0f * material.BaseColor.W;
                effect.CurrentTechnique.Passes[0].Apply();
            }
            using (Measure(diagnostics, "station.character.draw.submit"))
            {
                _graphicsDevice.SetVertexBuffer(part.Vertices);
                _graphicsDevice.Indices = part.Indices;
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, part.Source.Indices.Length / 3);
            }
            diagnostics?.AddCounter("station.character.draw.calls.per.character");
        }
    }

    private void DrawGpu(
        Matrix world,
        Matrix view,
        Matrix projection,
        Color tint,
        Vector3 ambientLightColor,
        Vector3 diffuseLightColor,
        Vector3 lightDirection,
        PerformanceDiagnostics? diagnostics)
    {
        Effect effect = _gpuSkinningEffect!;
        using (Measure(diagnostics, "station.character.effect.setup"))
        {
            effect.Parameters["World"]?.SetValue(world);
            effect.Parameters["View"]?.SetValue(view);
            effect.Parameters["Projection"]?.SetValue(projection);
            effect.Parameters["AmbientLightColor"]?.SetValue(ambientLightColor);
            effect.Parameters["DiffuseLightColor"]?.SetValue(diffuseLightColor);
            effect.Parameters["LightDirection"]?.SetValue(lightDirection);
            effect.Parameters["TintColor"]?.SetValue(tint.ToVector4());
        }

        for (int primitiveIndex = 0; primitiveIndex < _asset.Primitives.Count; primitiveIndex++)
        {
            CharacterGpuPrimitive part = _sharedGraphics.GetGpuPrimitive(primitiveIndex);
            CharacterGltfMaterial material = GetMaterial(_asset.Primitives[primitiveIndex].MaterialIndex);
            using (Measure(diagnostics, "station.character.bone.upload"))
            {
                for (int i = 0; i < part.SkinJointIndices.Length; i++)
                    _gpuPalette[i] = ToXna(_skinPose[part.SkinJointIndices[i]]);
                effect.Parameters["Bones"]?.SetValue(_gpuPalette);
            }
            using (Measure(diagnostics, "station.character.effect.setup"))
            {
                effect.Parameters["BaseColor"]?.SetValue(ToXna(material.BaseColor));
                effect.Parameters["Texture"]?.SetValue(_sharedGraphics.GetTextureOrWhite(material.TextureIndex));
                effect.CurrentTechnique.Passes[0].Apply();
            }
            using (Measure(diagnostics, "station.character.draw.submit"))
            {
                _graphicsDevice!.SetVertexBuffer(part.Vertices);
                _graphicsDevice.Indices = part.Indices;
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _asset.Primitives[primitiveIndex].Indices.Length / 3);
            }
            diagnostics?.AddCounter("station.character.draw.calls.per.character");
        }
        diagnostics?.AddCounter("station.character.bone.upload.bytes.per.character", BonePaletteUploadBytesPerFrame);
    }

    public void Dispose()
    {
        foreach (SkinnedPart part in _parts) part.Dispose();
        _parts.Clear();
        _worldPose = Array.Empty<NMatrix>();
        _resolvedPose = Array.Empty<bool>();
        _skinPose = Array.Empty<NMatrix>();
        _gpuPalette = Array.Empty<Matrix>();
        if (_ownsGraphicsResources) _sharedGraphics.Dispose();
    }

    private void AccumulateInfluence(NVector3 position, NVector3 normal, int joint, float weight, ref NVector3 skinnedPosition, ref NVector3 skinnedNormal)
    {
        if (weight <= 0.00001f || joint < 0 || joint >= _skinPose.Length) return;
        NMatrix skin = _skinPose[joint];
        skinnedPosition += NVector3.Transform(position, skin) * weight;
        skinnedNormal += NVector3.TransformNormal(normal, skin) * weight;
    }

    private CharacterGltfMaterial GetMaterial(int materialIndex)
    {
        return materialIndex >= 0 && materialIndex < _asset.Materials.Count
            ? _asset.Materials[materialIndex]
            : new CharacterGltfMaterial { BaseColor = NVector4.One, TextureIndex = -1 };
    }

    private static PerformanceDiagnostics.SectionScope Measure(PerformanceDiagnostics? diagnostics, string name)
        => diagnostics is null ? default : diagnostics.Measure(name);

    private static Vector3 TintColor(Color tint, NVector4 baseColor) => new(
        tint.R / 255.0f * baseColor.X,
        tint.G / 255.0f * baseColor.Y,
        tint.B / 255.0f * baseColor.Z);

    private static Vector3 ToXna(NVector3 value) => new(value.X, value.Y, value.Z);

    private static Vector4 ToXna(NVector4 value) => new(value.X, value.Y, value.Z, value.W);

    private static Matrix ToXna(NMatrix value) => new(
        value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44);

    private static CharacterLocalBounds CalculateLocalBounds(CharacterGltfAsset asset)
    {
        NVector3 min = new(float.MaxValue);
        NVector3 max = new(float.MinValue);
        bool found = false;
        foreach (CharacterGltfPrimitive primitive in asset.Primitives)
            foreach (CharacterGltfVertex vertex in primitive.Vertices)
            {
                min = NVector3.Min(min, vertex.Position);
                max = NVector3.Max(max, vertex.Position);
                found = true;
            }
        if (!found) return new CharacterLocalBounds(Vector3.Zero, 1.0f);
        NVector3 center = (min + max) * 0.5f;
        float radius = 0.0f;
        foreach (CharacterGltfPrimitive primitive in asset.Primitives)
            foreach (CharacterGltfVertex vertex in primitive.Vertices)
                radius = MathF.Max(radius, NVector3.Distance(center, vertex.Position));
        return new CharacterLocalBounds(new Vector3(center.X, center.Y, center.Z), radius + 0.75f);
    }

    private sealed class SkinnedPart : IDisposable
    {
        public SkinnedPart(CharacterGltfPrimitive source, DynamicVertexBuffer vertices, IndexBuffer indices, bool hasSkin)
        {
            Source = source;
            Vertices = vertices;
            Indices = indices;
            PoseOutput = new VertexPositionNormalTexture[source.Vertices.Length];
            PreparedVertices = new CpuSkinVertex[source.Vertices.Length];
            for (int i = 0; i < PreparedVertices.Length; i++) PreparedVertices[i] = CpuSkinVertex.Create(source.Vertices[i], hasSkin);
        }

        public CharacterGltfPrimitive Source { get; }
        public DynamicVertexBuffer Vertices { get; }
        public IndexBuffer Indices { get; }
        public VertexPositionNormalTexture[] PoseOutput { get; }
        public CpuSkinVertex[] PreparedVertices { get; }
        public void Dispose() => Vertices.Dispose();
    }

    private readonly struct CpuSkinVertex
    {
        private CpuSkinVertex(
            NVector3 position,
            NVector3 normal,
            Vector2 textureCoordinate,
            bool hasSkin,
            int joint0,
            int joint1,
            int joint2,
            int joint3,
            float weight0,
            float weight1,
            float weight2,
            float weight3)
        {
            Position = position;
            Normal = normal;
            TextureCoordinate = textureCoordinate;
            HasSkin = hasSkin;
            Joint0 = joint0;
            Joint1 = joint1;
            Joint2 = joint2;
            Joint3 = joint3;
            Weight0 = weight0;
            Weight1 = weight1;
            Weight2 = weight2;
            Weight3 = weight3;
        }

        public readonly NVector3 Position;
        public readonly NVector3 Normal;
        public readonly Vector2 TextureCoordinate;
        public readonly bool HasSkin;
        public readonly int Joint0;
        public readonly int Joint1;
        public readonly int Joint2;
        public readonly int Joint3;
        public readonly float Weight0;
        public readonly float Weight1;
        public readonly float Weight2;
        public readonly float Weight3;

        public static CpuSkinVertex Create(CharacterGltfVertex source, bool hasSkin)
        {
            int joint0 = (int)source.Joints.X;
            int joint1 = (int)source.Joints.Y;
            int joint2 = (int)source.Joints.Z;
            int joint3 = (int)source.Joints.W;
            float weight0 = source.Weights.X;
            float weight1 = source.Weights.Y;
            float weight2 = source.Weights.Z;
            float weight3 = source.Weights.W;
            float total = MathF.Max(0.00001f, weight0 + weight1 + weight2 + weight3);
            return new CpuSkinVertex(
                source.Position,
                source.Normal,
                new Vector2(source.TexCoord.X, 1.0f - source.TexCoord.Y),
                hasSkin && total > 0.00001f,
                joint0,
                joint1,
                joint2,
                joint3,
                weight0 / total,
                weight1 / total,
                weight2 / total,
                weight3 / total);
        }
    }
}
