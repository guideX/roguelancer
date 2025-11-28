//-----------------------------------------------------------------------------
// SunEffect.fx
//
// Description: A shader for rendering a procedural, animated sun with a
//              turbulent surface and a glowing corona.
//-----------------------------------------------------------------------------

//-----------------------------------------------------------------------------
// Globals
//-----------------------------------------------------------------------------
float4x4 World;
float4x4 View;
float4x4 Projection;
float Time;
float3 CameraPosition;

texture NoiseTexture;
sampler2D NoiseSampler = sampler_state
{
    Texture = <NoiseTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

float3 SunColor = float3(1.0, 0.8, 0.4); // Bright yellow-orange
float CoronaFalloff = 4.0;
float CoronaIntensity = 0.8;
float SurfaceIntensity = 1.2;
float DistortionScale = 0.1;
float NoiseScale = 3.0;

//-----------------------------------------------------------------------------
// Structures
//-----------------------------------------------------------------------------
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 WorldPos : TEXCOORD1;
};

//-----------------------------------------------------------------------------
// Vertex Shader
//-----------------------------------------------------------------------------
VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

    // Transform the vertex position into world space, then screen space.
    output.WorldPos = mul(input.Position, World).xyz;
    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    
    output.TexCoord = input.TexCoord;
    
    return output;
}

//-----------------------------------------------------------------------------
// Noise Function
//-----------------------------------------------------------------------------
// 4-tap 2D noise function. Samples a noise texture at different scales and times
// to create a turbulent, animated effect.
float noise(float2 uv, float time_offset)
{
    float t = Time * 0.1 + time_offset;
    
    // Scroll texture coordinates at different speeds and directions
    float2 uv1 = uv + float2(t * 0.1, t * 0.05);
    float2 uv2 = uv - float2(t * 0.07, t * 0.12);
    float2 uv3 = uv + float2(t * 0.15, -t * 0.08);
    float2 uv4 = uv - float2(-t * 0.09, -t * 0.11);

    // Sample noise texture at different scales
    float n1 = tex2D(NoiseSampler, uv1 * NoiseScale * 1.0).r;
    float n2 = tex2D(NoiseSampler, uv2 * NoiseScale * 2.5).r;
    float n3 = tex2D(NoiseSampler, uv3 * NoiseScale * 5.0).r;
    float n4 = tex2D(NoiseSampler, uv4 * NoiseScale * 10.0).r;

    // Combine noise samples with different weights
    return (n1 * 0.5) + (n2 * 0.25) + (n3 * 0.125) + (n4 * 0.0625);
}

//-----------------------------------------------------------------------------
// Pixel Shader
//-----------------------------------------------------------------------------
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    // Calculate distance from the center of the sun (in texture space)
    float2 centered_uv = input.TexCoord - 0.5;
    float dist = length(centered_uv * 2.0); // Range [0, 1] from center to edge

    // --- Create animated, distorted noise for the surface ---
    float2 distortion = float2(
        noise(input.TexCoord + float2(0.1, 0.1), 1.0),
        noise(input.TexCoord - float2(0.1, 0.1), 1.5)
    ) * 2.0 - 1.0; // Range [-1, 1]

    float2 distorted_uv = input.TexCoord + distortion * DistortionScale * (1.0 - dist);
    float surface_noise = noise(distorted_uv, 0.0);

    // --- Sun Surface ---
    // Use a power function on distance to create a sharp edge
    float surface_mask = 1.0 - pow(dist, 2.0);
    float3 surface_color = SunColor * surface_noise * SurfaceIntensity;
    
    // --- Corona ---
    // Use a different power function for a softer, larger glow
    float corona_mask = pow(1.0 - saturate(dist * 0.95), CoronaFalloff); // Saturate to avoid negative values
    float3 corona_color = SunColor * CoronaIntensity * corona_mask;

    // --- Limb Darkening/Brightening ---
    // Make the edges of the sun appear brighter (limb brightening)
    float limb_factor = pow(1.0 - dist, 3.0);
    float3 limb_color = SunColor * limb_factor * 2.0;

    // --- Final Color Composition ---
    // Combine layers: corona is the base, surface is on top, limb is added for edge glow
    float3 final_color = corona_color + (surface_color * surface_mask) + (limb_color * surface_mask);

    // The final alpha is the combination of the masks, ensuring it's solid in the middle and fades out
    float alpha = saturate(corona_mask + surface_mask);

    return float4(final_color, alpha);
}


//-----------------------------------------------------------------------------
// Technique
//-----------------------------------------------------------------------------
technique SunTechnique
{
    pass P0
    {
        VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 PixelShaderFunction();
    }
}
