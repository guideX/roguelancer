using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace Roguelancer;

internal static class CharacterSkinningConstants
{
    // Reach exposes 256 float4 vertex-constant registers. Keeping the palette
    // below 64 matrices leaves room for world/view/projection and lighting
    // parameters. Adam's largest primitive uses 47 joints after remapping.
    public const int MaxBonePaletteSize = 48;
}

internal readonly struct CharacterGpuVertex : IVertexType
{
    public CharacterGpuVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate, Vector4 blendIndices, Vector4 blendWeights)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
        BlendIndices = blendIndices;
        BlendWeights = blendWeights;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector2 TextureCoordinate;
    public readonly Vector4 BlendIndices;
    public readonly Vector4 BlendWeights;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0));

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

internal sealed class CharacterGpuPrimitive : IDisposable
{
    public CharacterGpuPrimitive(VertexBuffer vertices, IndexBuffer indices, int[] skinJointIndices)
    {
        Vertices = vertices;
        Indices = indices;
        SkinJointIndices = skinJointIndices;
    }

    public VertexBuffer Vertices { get; }
    public IndexBuffer Indices { get; }
    public int[] SkinJointIndices { get; }

    public void Dispose() => Vertices.Dispose();
}

public readonly record struct CharacterLocalBounds(Vector3 Center, float Radius);

/// <summary>
/// Immutable source data for one replaceable character definition. The parsed
/// GLB model, skeleton, animation clips, materials, and texture bytes are
/// intentionally owned once and referenced by every runtime instance.
/// </summary>
public sealed class CharacterAsset : IDisposable
{
    private readonly CharacterGraphicsResources _graphics;

    public CharacterAsset(
        string assetId,
        CharacterGltfAsset model,
        IReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?> animations,
        CharacterModelConfiguration modelConfiguration)
    {
        if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("An asset id is required.", nameof(assetId));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        ModelConfiguration = modelConfiguration ?? throw new ArgumentNullException(nameof(modelConfiguration));

        Dictionary<CharacterAnimationState, CharacterGltfAnimationClip?> animationCopy = new(animations);
        Animations = new ReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?>(animationCopy);
        AssetId = assetId;
        LocalBounds = CalculateLocalBounds(Model);
        _graphics = new CharacterGraphicsResources(Model);
    }

    public string AssetId { get; }
    public CharacterGltfAsset Model { get; }
    public IReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?> Animations { get; }
    public CharacterModelConfiguration ModelConfiguration { get; }
    public CharacterLocalBounds LocalBounds { get; }
    internal CharacterGraphicsResources Graphics => _graphics;

    public void LoadGraphics(GraphicsDevice graphicsDevice) => _graphics.LoadGraphics(graphicsDevice);

    public void Dispose() => _graphics.Dispose();

    private static CharacterLocalBounds CalculateLocalBounds(CharacterGltfAsset model)
    {
        NVector3 min = new(float.MaxValue);
        NVector3 max = new(float.MinValue);
        bool foundVertex = false;
        foreach (CharacterGltfPrimitive primitive in model.Primitives)
        {
            foreach (CharacterGltfVertex vertex in primitive.Vertices)
            {
                min = NVector3.Min(min, vertex.Position);
                max = NVector3.Max(max, vertex.Position);
                foundVertex = true;
            }
        }

        if (!foundVertex) return new CharacterLocalBounds(Vector3.Zero, 1.0f);

        NVector3 center = (min + max) * 0.5f;
        float radius = 0.0f;
        foreach (CharacterGltfPrimitive primitive in model.Primitives)
            foreach (CharacterGltfVertex vertex in primitive.Vertices)
                radius = MathF.Max(radius, NVector3.Distance(center, vertex.Position));

        // The bind-pose bounds are expanded conservatively for animated hands,
        // feet, and root motion while keeping the culler intentionally simple.
        return new CharacterLocalBounds(new Vector3(center.X, center.Y, center.Z), radius + 0.75f);
    }
}

/// <summary>
/// Small bounded cache for development character assets. It deliberately
/// avoids becoming a general content system; the key is the replaceable
/// character asset identity used by station runtime construction.
/// </summary>
public sealed class CharacterAssetCache : IDisposable
{
    private readonly Dictionary<string, CharacterAsset> _assets = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _assets.Count;

    public CharacterAsset GetOrAdd(string assetId, Func<CharacterAsset> factory)
    {
        if (_assets.TryGetValue(assetId, out CharacterAsset? existing)) return existing;

        CharacterAsset created = factory();
        _assets.Add(assetId, created);
        int vertexCount = 0;
        int indexCount = 0;
        foreach (CharacterGltfPrimitive primitive in created.Model.Primitives)
        {
            vertexCount += primitive.Vertices.Length;
            indexCount += primitive.Indices.Length;
        }
        Console.WriteLine($"[CHARACTER ASSETS] Parsed shared asset '{assetId}' from {created.Model.SourcePath}; clips={created.Animations.Count}, nodes={created.Model.Nodes.Count}, joints={created.Model.Skin?.JointNodeIndices.Length ?? 0}, vertices={vertexCount}, indices={indexCount}, primitives={created.Model.Primitives.Count}");
        return created;
    }

    public void Dispose()
    {
        foreach (CharacterAsset asset in _assets.Values) asset.Dispose();
        _assets.Clear();
    }
}

/// <summary>
/// Graphics resources that are immutable across instances of one character
/// asset. GPU-skinned source buffers and indices are shared; the CPU fallback's
/// dynamic output buffers remain per renderer because each instance writes a
/// different pose every rendered frame.
/// </summary>
internal sealed class CharacterGraphicsResources : IDisposable
{
    private readonly CharacterGltfAsset _asset;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D?[] _textures = Array.Empty<Texture2D?>();
    private IndexBuffer?[] _indices = Array.Empty<IndexBuffer?>();
    private CharacterGpuPrimitive?[] _gpuPrimitives = Array.Empty<CharacterGpuPrimitive?>();
    private Texture2D? _whiteTexture;

    public bool GpuSkinningSupported { get; private set; }

    public CharacterGraphicsResources(CharacterGltfAsset asset) => _asset = asset;

    public void LoadGraphics(GraphicsDevice graphicsDevice)
    {
        if (_graphicsDevice is not null) return;

        _graphicsDevice = graphicsDevice;
        _textures = new Texture2D?[_asset.ImageBytes.Count];
        for (int i = 0; i < _textures.Length; i++)
        {
            byte[]? bytes = _asset.ImageBytes[i];
            if (bytes is null || bytes.Length == 0) continue;
            using MemoryStream stream = new(bytes, writable: false);
            _textures[i] = Texture2D.FromStream(graphicsDevice, stream);
        }

        _whiteTexture = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _whiteTexture.SetData(new[] { Color.White });

        _indices = new IndexBuffer?[_asset.Primitives.Count];
        _gpuPrimitives = new CharacterGpuPrimitive?[_asset.Primitives.Count];
        GpuSkinningSupported = true;
        for (int i = 0; i < _asset.Primitives.Count; i++)
        {
            CharacterGltfPrimitive primitive = _asset.Primitives[i];
            IndexBuffer indices = new(graphicsDevice, IndexElementSize.ThirtyTwoBits, primitive.Indices.Length, BufferUsage.WriteOnly);
            indices.SetData(primitive.Indices);
            _indices[i] = indices;

            CharacterGpuPrimitive? gpuPrimitive = BuildGpuPrimitive(graphicsDevice, primitive, indices);
            _gpuPrimitives[i] = gpuPrimitive;
            if (gpuPrimitive is null) GpuSkinningSupported = false;
        }

        if (!GpuSkinningSupported)
        {
            foreach (CharacterGpuPrimitive? primitive in _gpuPrimitives) primitive?.Dispose();
            Array.Clear(_gpuPrimitives, 0, _gpuPrimitives.Length);
        }

        int maxPalette = 0;
        foreach (CharacterGpuPrimitive? primitive in _gpuPrimitives)
            if (primitive is not null) maxPalette = Math.Max(maxPalette, primitive.SkinJointIndices.Length);
        Console.WriteLine($"[CHARACTER ASSETS] Created shared graphics resources for '{_asset.SourcePath}'; textures={_textures.Length}, indexBuffers={_indices.Length}, gpuSkinning={GpuSkinningSupported}, maxPalette={maxPalette}");
    }

    public Texture2D? GetTexture(int index)
    {
        return index >= 0 && index < _textures.Length ? _textures[index] : null;
    }

    public Texture2D GetTextureOrWhite(int index)
    {
        return GetTexture(index) ?? _whiteTexture ?? throw new InvalidOperationException("Character graphics resources have not been loaded.");
    }

    public IndexBuffer GetIndexBuffer(int primitiveIndex)
    {
        if (primitiveIndex < 0 || primitiveIndex >= _indices.Length || _indices[primitiveIndex] is null)
            throw new InvalidOperationException("Character graphics resources have not been loaded.");
        return _indices[primitiveIndex]!;
    }

    public CharacterGpuPrimitive GetGpuPrimitive(int primitiveIndex)
    {
        if (!GpuSkinningSupported || primitiveIndex < 0 || primitiveIndex >= _gpuPrimitives.Length || _gpuPrimitives[primitiveIndex] is null)
            throw new InvalidOperationException("GPU character skinning is not available for this asset.");
        return _gpuPrimitives[primitiveIndex]!;
    }

    private CharacterGpuPrimitive? BuildGpuPrimitive(GraphicsDevice graphicsDevice, CharacterGltfPrimitive primitive, IndexBuffer indices)
    {
        Dictionary<int, int> remap = new();
        List<int> sourceJoints = new();
        CharacterGpuVertex[] vertices = new CharacterGpuVertex[primitive.Vertices.Length];
        CharacterGltfSkin? skin = _asset.Skin;

        for (int i = 0; i < primitive.Vertices.Length; i++)
        {
            CharacterGltfVertex source = primitive.Vertices[i];
            int local0 = 0;
            int local1 = 0;
            int local2 = 0;
            int local3 = 0;
            float weight0 = 0.0f;
            float weight1 = 0.0f;
            float weight2 = 0.0f;
            float weight3 = 0.0f;
            float totalWeight = 0.0f;
            for (int influence = 0; influence < 4; influence++)
            {
                int joint = GetJointComponent(source.Joints, influence);
                float weight = GetWeightComponent(source.Weights, influence);
                if (skin is null || weight <= 0.00001f || joint < 0 || joint >= skin.JointNodeIndices.Length) continue;
                if (!remap.TryGetValue(joint, out int localJoint))
                {
                    if (sourceJoints.Count >= CharacterSkinningConstants.MaxBonePaletteSize) return null;
                    localJoint = sourceJoints.Count;
                    remap.Add(joint, localJoint);
                    sourceJoints.Add(joint);
                }
                switch (influence)
                {
                    case 0: local0 = localJoint; weight0 = weight; break;
                    case 1: local1 = localJoint; weight1 = weight; break;
                    case 2: local2 = localJoint; weight2 = weight; break;
                    default: local3 = localJoint; weight3 = weight; break;
                }
                totalWeight += weight;
            }

            if (totalWeight > 0.00001f)
            {
                weight0 /= totalWeight;
                weight1 /= totalWeight;
                weight2 /= totalWeight;
                weight3 /= totalWeight;
            }

            vertices[i] = new CharacterGpuVertex(
                new Vector3(source.Position.X, source.Position.Y, source.Position.Z),
                new Vector3(source.Normal.X, source.Normal.Y, source.Normal.Z),
                new Vector2(source.TexCoord.X, 1.0f - source.TexCoord.Y),
                new Vector4(local0, local1, local2, local3),
                new Vector4(weight0, weight1, weight2, weight3));
        }

        VertexBuffer gpuVertices = new(graphicsDevice, CharacterGpuVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        gpuVertices.SetData(vertices);
        return new CharacterGpuPrimitive(gpuVertices, indices, sourceJoints.ToArray());
    }

    private static int GetJointComponent(NVector4 value, int component) => component switch
    {
        0 => (int)value.X,
        1 => (int)value.Y,
        2 => (int)value.Z,
        _ => (int)value.W,
    };

    private static float GetWeightComponent(System.Numerics.Vector4 value, int component) => component switch
    {
        0 => value.X,
        1 => value.Y,
        2 => value.Z,
        _ => value.W,
    };

    public void Dispose()
    {
        foreach (CharacterGpuPrimitive? primitive in _gpuPrimitives) primitive?.Dispose();
        foreach (IndexBuffer? indices in _indices) indices?.Dispose();
        foreach (Texture2D? texture in _textures) texture?.Dispose();
        _whiteTexture?.Dispose();
        _indices = Array.Empty<IndexBuffer?>();
        _textures = Array.Empty<Texture2D?>();
        _gpuPrimitives = Array.Empty<CharacterGpuPrimitive?>();
        _whiteTexture = null;
        GpuSkinningSupported = false;
        _graphicsDevice = null;
    }
}
