using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Roguelancer;

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
        _graphics = new CharacterGraphicsResources(Model);
    }

    public string AssetId { get; }
    public CharacterGltfAsset Model { get; }
    public IReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?> Animations { get; }
    public CharacterModelConfiguration ModelConfiguration { get; }
    internal CharacterGraphicsResources Graphics => _graphics;

    public void LoadGraphics(GraphicsDevice graphicsDevice) => _graphics.LoadGraphics(graphicsDevice);

    public void Dispose() => _graphics.Dispose();
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
        Console.WriteLine($"[CHARACTER ASSETS] Parsed shared asset '{assetId}' from {created.Model.SourcePath}; clips={created.Animations.Count}, nodes={created.Model.Nodes.Count}, primitives={created.Model.Primitives.Count}");
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
/// asset. CPU-skinned vertex buffers remain per renderer because each instance
/// writes a different pose every rendered frame.
/// </summary>
internal sealed class CharacterGraphicsResources : IDisposable
{
    private readonly CharacterGltfAsset _asset;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D?[] _textures = Array.Empty<Texture2D?>();
    private IndexBuffer?[] _indices = Array.Empty<IndexBuffer?>();

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

        _indices = new IndexBuffer?[_asset.Primitives.Count];
        for (int i = 0; i < _asset.Primitives.Count; i++)
        {
            CharacterGltfPrimitive primitive = _asset.Primitives[i];
            IndexBuffer indices = new(graphicsDevice, IndexElementSize.ThirtyTwoBits, primitive.Indices.Length, BufferUsage.WriteOnly);
            indices.SetData(primitive.Indices);
            _indices[i] = indices;
        }

        Console.WriteLine($"[CHARACTER ASSETS] Created shared graphics resources for '{_asset.SourcePath}'; textures={_textures.Length}, indexBuffers={_indices.Length}");
    }

    public Texture2D? GetTexture(int index)
    {
        return index >= 0 && index < _textures.Length ? _textures[index] : null;
    }

    public IndexBuffer GetIndexBuffer(int primitiveIndex)
    {
        if (primitiveIndex < 0 || primitiveIndex >= _indices.Length || _indices[primitiveIndex] is null)
            throw new InvalidOperationException("Character graphics resources have not been loaded.");
        return _indices[primitiveIndex]!;
    }

    public void Dispose()
    {
        foreach (IndexBuffer? indices in _indices) indices?.Dispose();
        foreach (Texture2D? texture in _textures) texture?.Dispose();
        _indices = Array.Empty<IndexBuffer?>();
        _textures = Array.Empty<Texture2D?>();
        _graphicsDevice = null;
    }
}
