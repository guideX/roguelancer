using System.Buffers.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace Roguelancer;

// Roguelancer's station prototype intentionally keeps the GLB boundary local.
// MonoGame's content importer does not consume these development GLBs, so this
// reader decodes only the mesh, skin, embedded images, and animation channels
// needed by the generic PlayerCharacter path.
public sealed class CharacterGltfAsset
{
    public required string SourcePath { get; init; }
    public required List<CharacterGltfNode> Nodes { get; init; }
    public required List<CharacterGltfPrimitive> Primitives { get; init; }
    public required List<CharacterGltfMaterial> Materials { get; init; }
    public required List<byte[]?> ImageBytes { get; init; }
    public CharacterGltfSkin? Skin { get; init; }

    public static CharacterGltfAsset Load(string path, bool extractImages)
    {
        using CharacterGltfDocument document = CharacterGltfDocument.Read(path);
        return document.DecodeAsset(extractImages);
    }

    public static CharacterGltfAnimationClip LoadAnimation(string path)
    {
        using CharacterGltfDocument document = CharacterGltfDocument.Read(path);
        return document.DecodeAnimation();
    }

    public int FindRootLikeNode()
    {
        for (int i = 0; i < Nodes.Count; i++)
            if (Nodes[i].ParentIndex < 0 && IsRootLike(Nodes[i].Name)) return i;
        for (int i = 0; i < Nodes.Count; i++)
            if (Nodes[i].ParentIndex < 0) return i;
        return 0;
    }

    public static bool IsRootLike(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("root") || lower.Contains("armature") || lower.Contains("hips");
    }
}

public sealed class CharacterGltfNode
{
    public required string Name { get; init; }
    public required int ParentIndex { get; init; }
    public required int MeshIndex { get; init; }
    public required int SkinIndex { get; init; }
    public required Vector3 Translation { get; init; }
    public required Quaternion Rotation { get; init; }
    public required Vector3 Scale { get; init; }
    public Matrix4x4 LocalMatrix => Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Translation);
}

public readonly record struct CharacterGltfVertex(Vector3 Position, Vector3 Normal, Vector2 TexCoord, Vector4 Joints, Vector4 Weights);

public sealed class CharacterGltfPrimitive
{
    public required CharacterGltfVertex[] Vertices { get; init; }
    public required int[] Indices { get; init; }
    public required int MaterialIndex { get; init; }
}

public sealed class CharacterGltfMaterial
{
    public required Vector4 BaseColor { get; init; }
    public required int TextureIndex { get; init; }
}

public sealed class CharacterGltfSkin
{
    public required int[] JointNodeIndices { get; init; }
    public required Matrix4x4[] InverseBindMatrices { get; init; }
}

public sealed class CharacterGltfAnimationClip
{
    private readonly Dictionary<int, CharacterGltfNodeCurves> _curves;

    internal CharacterGltfAnimationClip(string name, float duration, Dictionary<int, CharacterGltfNodeCurves> curves)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Animation" : name;
        Duration = MathF.Max(duration, 0.001f);
        _curves = curves;
    }

    public string Name { get; }
    public float Duration { get; }
    public int ChannelCount => _curves.Count;

    public Matrix4x4 SampleLocal(CharacterGltfNode node, int nodeIndex, float time, int rootNodeIndex, bool stripRootVertical)
    {
        Vector3 translation = node.Translation;
        Quaternion rotation = node.Rotation;
        Vector3 scale = node.Scale;
        if (_curves.TryGetValue(nodeIndex, out CharacterGltfNodeCurves? curve))
        {
            if (curve.Translation is not null) translation = curve.Translation.Sample(time);
            if (curve.Rotation is not null) rotation = curve.Rotation.Sample(time);
            if (curve.Scale is not null) scale = curve.Scale.Sample(time);
        }

        if (nodeIndex == rootNodeIndex || CharacterGltfAsset.IsRootLike(node.Name) && node.ParentIndex < 1)
        {
            translation.X = node.Translation.X;
            translation.Z = node.Translation.Z;
            if (stripRootVertical) translation.Y = node.Translation.Y;
        }

        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation)) * Matrix4x4.CreateTranslation(translation);
    }
}

internal sealed class CharacterGltfNodeCurves
{
    public CharacterGltfCurve<Vector3>? Translation { get; set; }
    public CharacterGltfCurve<Quaternion>? Rotation { get; set; }
    public CharacterGltfCurve<Vector3>? Scale { get; set; }
}

internal sealed class CharacterGltfCurve<T>
{
    private readonly float[] _times;
    private readonly T[] _values;
    private readonly bool _step;
    private readonly Func<T, T, float, T> _lerp;

    public CharacterGltfCurve(float[] times, T[] values, bool step, Func<T, T, float, T> lerp)
    {
        _times = times;
        _values = values;
        _step = step;
        _lerp = lerp;
    }

    public T Sample(float time)
    {
        if (_values.Length == 0) throw new InvalidOperationException("Animation curve has no values.");
        if (_values.Length == 1 || _times.Length == 1) return _values[0];
        time = Math.Clamp(time, _times[0], _times[^1]);
        int upper = Array.BinarySearch(_times, time);
        if (upper >= 0) return _values[Math.Min(upper, _values.Length - 1)];
        upper = ~upper;
        if (upper <= 0) return _values[0];
        if (upper >= _times.Length) return _values[^1];
        int lower = upper - 1;
        if (_step) return _values[lower];
        float range = _times[upper] - _times[lower];
        float amount = range <= float.Epsilon ? 0.0f : (time - _times[lower]) / range;
        return _lerp(_values[lower], _values[upper], amount);
    }
}

internal sealed class CharacterGltfDocument : IDisposable
{
    private readonly byte[] _binaryBytes;
    private readonly JsonDocument _jsonDocument;
    private readonly JsonElement _root;
    private readonly string _sourcePath;

    private CharacterGltfDocument(string sourcePath, byte[] binaryBytes, JsonDocument jsonDocument)
    {
        _sourcePath = sourcePath;
        _binaryBytes = binaryBytes;
        _jsonDocument = jsonDocument;
        _root = jsonDocument.RootElement;
    }

    public static CharacterGltfDocument Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67)
            throw new InvalidDataException($"Not a GLB file: {path}");
        int jsonLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        int jsonStart = 20;
        using MemoryStream jsonStream = new(bytes, jsonStart, jsonLength, writable: false);
        JsonDocument json = JsonDocument.Parse(jsonStream);
        int binaryHeader = jsonStart + jsonLength;
        int binaryLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(binaryHeader));
        int binaryStart = binaryHeader + 8;
        return new CharacterGltfDocument(path, bytes.AsSpan(binaryStart, binaryLength).ToArray(), json);
    }

    public CharacterGltfAsset DecodeAsset(bool extractImages)
    {
        List<CharacterGltfNode> nodes = DecodeNodes();
        List<CharacterGltfMaterial> materials = DecodeMaterials();
        return new CharacterGltfAsset
        {
            SourcePath = _sourcePath,
            Nodes = nodes,
            Materials = materials,
            Primitives = DecodePrimitives(nodes),
            Skin = DecodeSkin(),
            ImageBytes = extractImages ? DecodeImages(materials) : new List<byte[]?>(),
        };
    }

    public CharacterGltfAnimationClip DecodeAnimation()
    {
        if (!_root.TryGetProperty("animations", out JsonElement animations) || animations.GetArrayLength() == 0)
            return new CharacterGltfAnimationClip("None", 0.001f, new Dictionary<int, CharacterGltfNodeCurves>());

        JsonElement animation = animations[0];
        JsonElement samplers = animation.GetProperty("samplers");
        Dictionary<int, CharacterGltfNodeCurves> curves = new();
        float duration = 0.001f;
        foreach (JsonElement channel in animation.GetProperty("channels").EnumerateArray())
        {
            JsonElement target = channel.GetProperty("target");
            int nodeIndex = target.GetProperty("node").GetInt32();
            string path = target.GetProperty("path").GetString() ?? string.Empty;
            JsonElement sampler = samplers[channel.GetProperty("sampler").GetInt32()];
            float[] times = ReadFloats(sampler.GetProperty("input").GetInt32());
            if (times.Length > 0) duration = MathF.Max(duration, times[^1]);
            bool step = sampler.TryGetProperty("interpolation", out JsonElement interpolation) && string.Equals(interpolation.GetString(), "STEP", StringComparison.OrdinalIgnoreCase);
            CharacterGltfNodeCurves curve = curves.TryGetValue(nodeIndex, out CharacterGltfNodeCurves? existing) ? existing : curves[nodeIndex] = new CharacterGltfNodeCurves();
            int output = sampler.GetProperty("output").GetInt32();
            if (string.Equals(path, "translation", StringComparison.OrdinalIgnoreCase))
                curve.Translation = new CharacterGltfCurve<Vector3>(times, ReadVectors(output, 3).Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray(), step, Vector3.Lerp);
            else if (string.Equals(path, "scale", StringComparison.OrdinalIgnoreCase))
                curve.Scale = new CharacterGltfCurve<Vector3>(times, ReadVectors(output, 3).Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray(), step, Vector3.Lerp);
            else if (string.Equals(path, "rotation", StringComparison.OrdinalIgnoreCase))
                curve.Rotation = new CharacterGltfCurve<Quaternion>(times, ReadVectors(output, 4).Select(v => Quaternion.Normalize(new Quaternion(v.X, v.Y, v.Z, v.W))).ToArray(), step, Quaternion.Slerp);
        }

        string name = animation.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? "Animation" : "Animation";
        return new CharacterGltfAnimationClip(name, duration, curves);
    }

    private List<CharacterGltfNode> DecodeNodes()
    {
        JsonElement array = _root.GetProperty("nodes");
        List<CharacterGltfNode> nodes = new();
        foreach (JsonElement node in array.EnumerateArray())
        {
            Vector3 translation = ReadVector3(node, "translation", Vector3.Zero);
            Quaternion rotation = ReadQuaternion(node, "rotation", Quaternion.Identity);
            Vector3 scale = ReadVector3(node, "scale", Vector3.One);
            if (node.TryGetProperty("matrix", out JsonElement matrix))
            {
                float[] values = matrix.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                Matrix4x4 local = new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]);
                Matrix4x4.Decompose(local, out scale, out rotation, out translation);
            }
            nodes.Add(new CharacterGltfNode
            {
                Name = node.TryGetProperty("name", out JsonElement nodeName) ? nodeName.GetString() ?? $"Node{nodes.Count}" : $"Node{nodes.Count}",
                ParentIndex = -1,
                MeshIndex = node.TryGetProperty("mesh", out JsonElement mesh) ? mesh.GetInt32() : -1,
                SkinIndex = node.TryGetProperty("skin", out JsonElement skin) ? skin.GetInt32() : -1,
                Translation = translation,
                Rotation = rotation,
                Scale = scale,
            });
        }
        for (int parent = 0; parent < array.GetArrayLength(); parent++)
        {
            if (!array[parent].TryGetProperty("children", out JsonElement children)) continue;
            foreach (JsonElement child in children.EnumerateArray())
            {
                int childIndex = child.GetInt32();
                CharacterGltfNode old = nodes[childIndex];
                nodes[childIndex] = new CharacterGltfNode
                {
                    Name = old.Name, ParentIndex = parent, MeshIndex = old.MeshIndex, SkinIndex = old.SkinIndex,
                    Translation = old.Translation, Rotation = old.Rotation, Scale = old.Scale,
                };
            }
        }
        return nodes;
    }

    private List<CharacterGltfMaterial> DecodeMaterials()
    {
        List<CharacterGltfMaterial> materials = new();
        if (!_root.TryGetProperty("materials", out JsonElement array)) return materials;
        foreach (JsonElement material in array.EnumerateArray())
        {
            Vector4 color = Vector4.One;
            int texture = -1;
            if (material.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr))
            {
                if (pbr.TryGetProperty("baseColorFactor", out JsonElement factor))
                {
                    float[] values = factor.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                    if (values.Length == 4) color = new Vector4(values[0], values[1], values[2], values[3]);
                }
                if (pbr.TryGetProperty("baseColorTexture", out JsonElement textureInfo)) texture = textureInfo.GetProperty("index").GetInt32();
            }
            materials.Add(new CharacterGltfMaterial { BaseColor = color, TextureIndex = texture });
        }
        return materials;
    }

    private List<CharacterGltfPrimitive> DecodePrimitives(IReadOnlyList<CharacterGltfNode> nodes)
    {
        List<CharacterGltfPrimitive> primitives = new();
        JsonElement meshes = _root.GetProperty("meshes");
        foreach (CharacterGltfNode node in nodes)
        {
            if (node.MeshIndex < 0) continue;
            foreach (JsonElement primitive in meshes[node.MeshIndex].GetProperty("primitives").EnumerateArray())
            {
                JsonElement attributes = primitive.GetProperty("attributes");
                float[] positions = ReadFloats(attributes.GetProperty("POSITION").GetInt32());
                float[] normals = attributes.TryGetProperty("NORMAL", out JsonElement normal) ? ReadFloats(normal.GetInt32()) : new float[positions.Length];
                float[] uvs = attributes.TryGetProperty("TEXCOORD_0", out JsonElement uv) ? ReadFloats(uv.GetInt32()) : new float[positions.Length / 3 * 2];
                int[] joints = attributes.TryGetProperty("JOINTS_0", out JsonElement joint) ? ReadIntegers(joint.GetInt32()) : new int[positions.Length / 3 * 4];
                float[] weights = attributes.TryGetProperty("WEIGHTS_0", out JsonElement weight) ? ReadFloats(weight.GetInt32()) : BuildRigidWeights(positions.Length / 3);
                int count = positions.Length / 3;
                CharacterGltfVertex[] vertices = new CharacterGltfVertex[count];
                for (int i = 0; i < count; i++)
                    vertices[i] = new CharacterGltfVertex(new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]), new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]), new Vector2(uvs[i * 2], uvs[i * 2 + 1]), new Vector4(joints[i * 4], joints[i * 4 + 1], joints[i * 4 + 2], joints[i * 4 + 3]), new Vector4(weights[i * 4], weights[i * 4 + 1], weights[i * 4 + 2], weights[i * 4 + 3]));
                int[] indices = primitive.TryGetProperty("indices", out JsonElement index) ? ReadIntegers(index.GetInt32()) : Enumerable.Range(0, count).ToArray();
                primitives.Add(new CharacterGltfPrimitive { Vertices = vertices, Indices = indices, MaterialIndex = primitive.TryGetProperty("material", out JsonElement material) ? material.GetInt32() : -1 });
            }
        }
        return primitives;
    }

    private CharacterGltfSkin? DecodeSkin()
    {
        if (!_root.TryGetProperty("skins", out JsonElement skins) || skins.GetArrayLength() == 0) return null;
        JsonElement skin = skins[0];
        int[] joints = skin.GetProperty("joints").EnumerateArray().Select(j => j.GetInt32()).ToArray();
        Matrix4x4[] inverse = Enumerable.Repeat(Matrix4x4.Identity, joints.Length).ToArray();
        if (skin.TryGetProperty("inverseBindMatrices", out JsonElement accessor))
        {
            float[] values = ReadFloats(accessor.GetInt32());
            for (int i = 0; i < joints.Length && i * 16 + 15 < values.Length; i++)
            {
                int o = i * 16;
                inverse[i] = new Matrix4x4(values[o], values[o + 1], values[o + 2], values[o + 3], values[o + 4], values[o + 5], values[o + 6], values[o + 7], values[o + 8], values[o + 9], values[o + 10], values[o + 11], values[o + 12], values[o + 13], values[o + 14], values[o + 15]);
            }
        }
        return new CharacterGltfSkin { JointNodeIndices = joints, InverseBindMatrices = inverse };
    }

    private List<byte[]?> DecodeImages(IReadOnlyList<CharacterGltfMaterial> materials)
    {
        int textureCount = _root.TryGetProperty("textures", out JsonElement textures) ? textures.GetArrayLength() : 0;
        List<byte[]?> result = Enumerable.Repeat<byte[]?>(null, textureCount).ToList();
        if (!_root.TryGetProperty("images", out JsonElement images)) return result;
        foreach (int textureIndex in materials.Where(m => m.TextureIndex >= 0).Select(m => m.TextureIndex).Distinct())
        {
            if (textureIndex < 0 || textureIndex >= textureCount) continue;
            int imageIndex = textures[textureIndex].GetProperty("source").GetInt32();
            JsonElement image = images[imageIndex];
            if (image.TryGetProperty("bufferView", out JsonElement viewElement))
            {
                JsonElement view = _root.GetProperty("bufferViews")[viewElement.GetInt32()];
                int offset = view.TryGetProperty("byteOffset", out JsonElement byteOffset) ? byteOffset.GetInt32() : 0;
                result[textureIndex] = _binaryBytes.AsSpan(offset, view.GetProperty("byteLength").GetInt32()).ToArray();
            }
        }
        return result;
    }

    private float[] ReadFloats(int accessorIndex)
    {
        AccessorInfo accessor = GetAccessor(accessorIndex);
        float[] values = new float[accessor.Count * accessor.ComponentCount];
        int size = ComponentSize(accessor.ComponentType);
        int stride = accessor.Stride == 0 ? size * accessor.ComponentCount : accessor.Stride;
        int start = accessor.BufferViewOffset + accessor.ByteOffset;
        for (int item = 0; item < accessor.Count; item++)
            for (int component = 0; component < accessor.ComponentCount; component++)
                values[item * accessor.ComponentCount + component] = ReadFloat(start + item * stride + component * size, accessor.ComponentType, accessor.Normalized);
        return values;
    }

    private Vector4[] ReadVectors(int accessorIndex, int expectedComponents)
    {
        AccessorInfo accessor = GetAccessor(accessorIndex);
        float[] values = ReadFloats(accessorIndex);
        Vector4[] result = new Vector4[accessor.Count];
        for (int i = 0; i < result.Length; i++)
        {
            int o = i * accessor.ComponentCount;
            result[i] = new Vector4(accessor.ComponentCount > 0 ? values[o] : 0, accessor.ComponentCount > 1 ? values[o + 1] : 0, accessor.ComponentCount > 2 ? values[o + 2] : 0, accessor.ComponentCount > 3 ? values[o + 3] : 1);
        }
        return result;
    }

    private int[] ReadIntegers(int accessorIndex)
    {
        AccessorInfo accessor = GetAccessor(accessorIndex);
        int[] result = new int[accessor.Count * accessor.ComponentCount];
        int size = ComponentSize(accessor.ComponentType);
        int stride = accessor.Stride == 0 ? size * accessor.ComponentCount : accessor.Stride;
        int start = accessor.BufferViewOffset + accessor.ByteOffset;
        for (int item = 0; item < accessor.Count; item++)
            for (int component = 0; component < accessor.ComponentCount; component++)
                result[item * accessor.ComponentCount + component] = ReadInteger(start + item * stride + component * size, accessor.ComponentType);
        return result;
    }

    private AccessorInfo GetAccessor(int index)
    {
        JsonElement accessor = _root.GetProperty("accessors")[index];
        int viewOffset = 0;
        int stride = 0;
        if (accessor.TryGetProperty("bufferView", out JsonElement viewElement))
        {
            JsonElement view = _root.GetProperty("bufferViews")[viewElement.GetInt32()];
            viewOffset = view.TryGetProperty("byteOffset", out JsonElement offset) ? offset.GetInt32() : 0;
            stride = view.TryGetProperty("byteStride", out JsonElement byteStride) ? byteStride.GetInt32() : 0;
        }
        return new AccessorInfo(viewOffset, accessor.TryGetProperty("byteOffset", out JsonElement byteOffset) ? byteOffset.GetInt32() : 0, accessor.GetProperty("componentType").GetInt32(), accessor.GetProperty("count").GetInt32(), ComponentCount(accessor.GetProperty("type").GetString() ?? "SCALAR"), accessor.TryGetProperty("normalized", out JsonElement normalized) && normalized.GetBoolean(), stride);
    }

    private float ReadFloat(int offset, int componentType, bool normalized) => componentType switch
    {
        5126 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(_binaryBytes.AsSpan(offset))),
        5121 => normalized ? _binaryBytes[offset] / 255.0f : _binaryBytes[offset],
        5123 => normalized ? BinaryPrimitives.ReadUInt16LittleEndian(_binaryBytes.AsSpan(offset)) / 65535.0f : BinaryPrimitives.ReadUInt16LittleEndian(_binaryBytes.AsSpan(offset)),
        5125 => BinaryPrimitives.ReadUInt32LittleEndian(_binaryBytes.AsSpan(offset)),
        _ => throw new NotSupportedException($"Unsupported glTF component type {componentType}.")
    };

    private int ReadInteger(int offset, int componentType) => componentType switch
    {
        5121 => _binaryBytes[offset],
        5123 => BinaryPrimitives.ReadUInt16LittleEndian(_binaryBytes.AsSpan(offset)),
        5125 => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_binaryBytes.AsSpan(offset))),
        5126 => checked((int)ReadFloat(offset, componentType, false)),
        _ => throw new NotSupportedException($"Unsupported glTF integer component type {componentType}.")
    };

    private static int ComponentSize(int type) => type switch { 5121 => 1, 5123 => 2, 5125 or 5126 => 4, _ => throw new NotSupportedException($"Unsupported glTF component type {type}.") };
    private static int ComponentCount(string type) => type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT2" => 4, "MAT3" => 9, "MAT4" => 16, _ => throw new NotSupportedException($"Unsupported glTF accessor type {type}.") };
    private static float[] BuildRigidWeights(int count) { float[] result = new float[count * 4]; for (int i = 0; i < count; i++) result[i * 4] = 1.0f; return result; }
    private static Vector3 ReadVector3(JsonElement parent, string property, Vector3 fallback) { if (!parent.TryGetProperty(property, out JsonElement value)) return fallback; float[] v = value.EnumerateArray().Select(x => x.GetSingle()).ToArray(); return v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : fallback; }
    private static Quaternion ReadQuaternion(JsonElement parent, string property, Quaternion fallback) { if (!parent.TryGetProperty(property, out JsonElement value)) return fallback; float[] v = value.EnumerateArray().Select(x => x.GetSingle()).ToArray(); return v.Length >= 4 ? Quaternion.Normalize(new Quaternion(v[0], v[1], v[2], v[3])) : fallback; }

    public void Dispose() => _jsonDocument.Dispose();
    private readonly record struct AccessorInfo(int BufferViewOffset, int ByteOffset, int ComponentType, int Count, int ComponentCount, bool Normalized, int Stride);
}
