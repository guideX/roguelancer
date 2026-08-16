// Bounded Reach-profile character skinning effect.
// A primitive's source joint indices are remapped at asset-load time, so the
// fixed palette stays below the Reach vertex-constant limit.

float4x4 Bones[48];
float4x4 World;
float4x4 View;
float4x4 Projection;
float3 AmbientLightColor;
float3 DiffuseLightColor;
float3 LightDirection;
float4 BaseColor;
float4 TintColor;

texture Texture;
sampler2D TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float4 BlendIndices : BLENDINDICES0;
    float4 BlendWeights : BLENDWEIGHT0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float3 WorldNormal : TEXCOORD1;
    float2 TexCoord : TEXCOORD0;
};

float4x4 BuildSkinMatrix(float4 indices, float4 weights)
{
    return Bones[(int)indices.x] * weights.x
        + Bones[(int)indices.y] * weights.y
        + Bones[(int)indices.z] * weights.z
        + Bones[(int)indices.w] * weights.w;
}

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    float4x4 skin = BuildSkinMatrix(input.BlendIndices, input.BlendWeights);
    float4 skinnedPosition = mul(input.Position, skin);
    float3 skinnedNormal = mul(float4(input.Normal, 0.0), skin).xyz;
    float4 worldPosition = mul(skinnedPosition, World);
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = normalize(mul(float4(skinnedNormal, 0.0), World).xyz);
    output.TexCoord = input.TexCoord;
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float4 color = tex2D(TextureSampler, input.TexCoord) * BaseColor * TintColor;
    float3 lightDirectionToSurface = normalize(-LightDirection);
    float diffuse = saturate(dot(normalize(input.WorldNormal), lightDirectionToSurface));
    float3 lighting = AmbientLightColor + DiffuseLightColor * diffuse;
    return float4(color.rgb * lighting, color.a);
}

technique CharacterSkinning
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VertexShaderFunction();
        PixelShader = compile ps_4_0_level_9_1 PixelShaderFunction();
    }
}
