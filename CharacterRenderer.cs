using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NMatrix = System.Numerics.Matrix4x4;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace Roguelancer;

/// <summary>CPU-skinned renderer for one replaceable character asset.</summary>
public sealed class CharacterRenderer : IDisposable
{
    private readonly CharacterGltfAsset _asset;
    private readonly List<SkinnedPart> _parts = new();
    private Texture2D?[] _textures = Array.Empty<Texture2D?>();
    private NMatrix[] _worldPose = Array.Empty<NMatrix>();
    private bool[] _resolvedPose = Array.Empty<bool>();
    private NMatrix[] _skinPose = Array.Empty<NMatrix>();
    private GraphicsDevice? _graphicsDevice;

    public CharacterRenderer(CharacterGltfAsset asset) => _asset = asset;

    public void LoadGraphics(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _textures = new Texture2D?[_asset.ImageBytes.Count];
        _worldPose = new NMatrix[_asset.Nodes.Count];
        _resolvedPose = new bool[_asset.Nodes.Count];
        _skinPose = _asset.Skin is null ? Array.Empty<NMatrix>() : new NMatrix[_asset.Skin.JointNodeIndices.Length];
        for (int i = 0; i < _textures.Length; i++)
        {
            byte[]? bytes = _asset.ImageBytes[i];
            if (bytes is null || bytes.Length == 0) continue;
            using MemoryStream stream = new(bytes, writable: false);
            _textures[i] = Texture2D.FromStream(graphicsDevice, stream);
        }

        foreach (CharacterGltfPrimitive primitive in _asset.Primitives)
        {
            IndexBuffer indices = new(graphicsDevice, IndexElementSize.ThirtyTwoBits, primitive.Indices.Length, BufferUsage.WriteOnly);
            indices.SetData(primitive.Indices);
            DynamicVertexBuffer vertices = new(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, primitive.Vertices.Length, BufferUsage.WriteOnly);
            _parts.Add(new SkinnedPart(primitive, vertices, indices));
        }
    }

    public void UpdatePose(NMatrix[] localPose)
    {
        if (_graphicsDevice is null) return;
        NMatrix Resolve(int index)
        {
            if (_resolvedPose[index]) return _worldPose[index];
            _worldPose[index] = _asset.Nodes[index].ParentIndex >= 0 ? localPose[index] * Resolve(_asset.Nodes[index].ParentIndex) : localPose[index];
            _resolvedPose[index] = true;
            return _worldPose[index];
        }
        Array.Clear(_resolvedPose, 0, _resolvedPose.Length);
        for (int i = 0; i < _asset.Nodes.Count; i++) Resolve(i);

        if (_asset.Skin is not null)
        {
            for (int joint = 0; joint < _skinPose.Length; joint++)
            {
                int node = _asset.Skin.JointNodeIndices[joint];
                _skinPose[joint] = _asset.Skin.InverseBindMatrices[joint] * _worldPose[node];
            }
        }

        foreach (SkinnedPart part in _parts)
        {
            VertexPositionNormalTexture[] output = part.PoseOutput;
            for (int i = 0; i < output.Length; i++)
            {
                CharacterGltfVertex source = part.Source.Vertices[i];
                NVector3 position = NVector3.Zero;
                NVector3 normal = NVector3.Zero;
                float totalWeight = 0.0f;
                if (_asset.Skin is not null)
                {
                    for (int influence = 0; influence < 4; influence++)
                    {
                        int joint = influence switch { 0 => (int)source.Joints.X, 1 => (int)source.Joints.Y, 2 => (int)source.Joints.Z, _ => (int)source.Joints.W };
                        float weight = influence switch { 0 => source.Weights.X, 1 => source.Weights.Y, 2 => source.Weights.Z, _ => source.Weights.W };
                        if (weight <= 0.00001f || joint < 0 || joint >= _asset.Skin.JointNodeIndices.Length) continue;
                        NMatrix skin = _skinPose[joint];
                        position += NVector3.Transform(source.Position, skin) * weight;
                        normal += NVector3.TransformNormal(source.Normal, skin) * weight;
                        totalWeight += weight;
                    }
                }
                if (totalWeight <= 0.00001f) { position = source.Position; normal = source.Normal; }
                else { position /= totalWeight; normal /= totalWeight; }
                if (normal.LengthSquared() < 0.00001f) normal = NVector3.UnitY;
                output[i] = new VertexPositionNormalTexture(ToXna(position), ToXna(NVector3.Normalize(normal)), new Vector2(source.TexCoord.X, 1.0f - source.TexCoord.Y));
            }
            part.Vertices.SetData(output, 0, output.Length, SetDataOptions.Discard);
        }
    }

    public void Draw(BasicEffect effect, Matrix world, Matrix view, Matrix projection, Color tint)
    {
        Draw(
            effect,
            world,
            view,
            projection,
            tint,
            new Vector3(0.46f, 0.50f, 0.62f),
            new Vector3(0.72f, 0.76f, 0.90f),
            new Vector3(-0.35f, -0.85f, -0.40f));
    }

    public void Draw(
        BasicEffect effect,
        Matrix world,
        Matrix view,
        Matrix projection,
        Color tint,
        Vector3 ambientLightColor,
        Vector3 diffuseLightColor,
        Vector3 lightDirection)
    {
        if (_graphicsDevice is null) return;
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
        foreach (SkinnedPart part in _parts)
        {
            CharacterGltfMaterial material = part.Source.MaterialIndex >= 0 && part.Source.MaterialIndex < _asset.Materials.Count ? _asset.Materials[part.Source.MaterialIndex] : new CharacterGltfMaterial { BaseColor = NVector4.One, TextureIndex = -1 };
            effect.Texture = material.TextureIndex >= 0 && material.TextureIndex < _textures.Length ? _textures[material.TextureIndex] : null;
            effect.DiffuseColor = new Vector3(tint.R / 255.0f * material.BaseColor.X, tint.G / 255.0f * material.BaseColor.Y, tint.B / 255.0f * material.BaseColor.Z);
            effect.Alpha = tint.A / 255.0f * material.BaseColor.W;
            effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.SetVertexBuffer(part.Vertices);
            _graphicsDevice.Indices = part.Indices;
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, part.Source.Indices.Length / 3);
        }
    }

    public void Dispose()
    {
        foreach (SkinnedPart part in _parts) part.Dispose();
        _parts.Clear();
        foreach (Texture2D? texture in _textures) texture?.Dispose();
        _textures = Array.Empty<Texture2D?>();
        _worldPose = Array.Empty<NMatrix>();
        _resolvedPose = Array.Empty<bool>();
        _skinPose = Array.Empty<NMatrix>();
    }

    private static Vector3 ToXna(NVector3 value) => new(value.X, value.Y, value.Z);
    private sealed class SkinnedPart : IDisposable
    {
        public SkinnedPart(CharacterGltfPrimitive source, DynamicVertexBuffer vertices, IndexBuffer indices)
        {
            Source = source;
            Vertices = vertices;
            Indices = indices;
            PoseOutput = new VertexPositionNormalTexture[source.Vertices.Length];
        }
        public CharacterGltfPrimitive Source { get; }
        public DynamicVertexBuffer Vertices { get; }
        public IndexBuffer Indices { get; }
        public VertexPositionNormalTexture[] PoseOutput { get; }
        public void Dispose() { Vertices.Dispose(); Indices.Dispose(); }
    }
}
