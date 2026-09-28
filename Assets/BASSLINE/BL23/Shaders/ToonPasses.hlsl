#ifndef BL23_TOON_PASSES_INCLUDED
#define BL23_TOON_PASSES_INCLUDED

#include "ToonCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// ------------------------------------------------------------------ outline (inverted hull)
struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;   // xyz = smoothed normal (skinned like a tangent)
    half4  color      : COLOR;
    float2 uv0        : TEXCOORD0;
    float4 uv1        : TEXCOORD1; // w = outline reduction (0 = full outline)
    float3 uv2        : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float3 bindPos    : TEXCOORD0;
    half   fogFactor  : TEXCOORD1;
    float2 uv0        : TEXCOORD2;
    half3  vcol       : TEXCOORD3;
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings OutlineVert(OutlineAttributes v)
{
    OutlineVaryings o = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 n = v.tangentOS.xyz;
    if (dot(n, n) < 0.01) n = v.normalOS;
    n = normalize(n);
    float3 pos = BL_ApplyVertexFx(v.positionOS.xyz, v.normalOS, v.uv2);
    float4 cs = TransformObjectToHClip(pos);
    float3 nWS = TransformObjectToWorldNormal(n);
    float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
    float len = length(nCS);
    nCS = len > 1e-5 ? nCS / len : float2(0, 0);
    float width = _OutlineWidth * saturate(1.0 - v.uv1.w);
    // constant pixel width at mid range, shrinking with distance so far actors don't turn into ink blobs
    float w = min(cs.w, 7.0);
    float2 px = nCS * width * (2.0 / _ScreenParams.y) * w;
    px.x *= _ScreenParams.y / _ScreenParams.x;
    cs.xy += px;
    #if UNITY_REVERSED_Z
        cs.z -= _OutlineZ * 0.0001 * cs.w;
    #else
        cs.z += _OutlineZ * 0.0001 * cs.w;
    #endif
    o.positionCS = cs;
    o.bindPos = v.uv2;
    o.uv0 = TRANSFORM_TEX(v.uv0, _BaseMap);
    o.vcol = _UseVertexColor > 0.5 ? v.color.rgb : half3(1, 1, 1);
    o.fogFactor = ComputeFogFactor(cs.z);
    return o;
}

half4 OutlineFrag(OutlineVaryings i) : SV_Target
{
    // coloured line art: the ink takes a darkened, saturated version of the surface colour
    half3 alb = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, i.uv0, 2).rgb * _BaseColor.rgb * i.vcol;
    half3 ink = alb * alb * 0.55h;
    half3 c = lerp(_OutlineColor.rgb, max(_OutlineColor.rgb * 0.6h, ink), _OutlineTint);
    if (_BreakAmount > 0.001) c = lerp(c, _BreakColor.rgb, _BreakAmount * step(0.5, frac(_Time.y * 7.0)));
    c = MixFog(c, i.fogFactor);
    return half4(c, 1);
}

// ------------------------------------------------------------------ shadow caster
float3 _LightDirection;
float3 _LightPosition;

struct ShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float3 uv2        : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 ShadowVert(ShadowAttributes v) : SV_POSITION
{
    UNITY_SETUP_INSTANCE_ID(v);
    float3 pos = BL_ApplyVertexFx(v.positionOS.xyz, v.normalOS, v.uv2);
    float3 positionWS = TransformObjectToWorld(pos);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    positionCS = ApplyShadowClamping(positionCS);
    return positionCS;
}

half4 ShadowFrag() : SV_Target { return 0; }

// ------------------------------------------------------------------ depth only / depth normals
struct DepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float3 uv2        : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
    half3  normalWS   : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

DepthVaryings DepthVert(DepthAttributes v)
{
    DepthVaryings o = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 pos = BL_ApplyVertexFx(v.positionOS.xyz, v.normalOS, v.uv2);
    o.positionCS = TransformObjectToHClip(pos);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    return o;
}

half DepthFrag(DepthVaryings i) : SV_Target { return i.positionCS.z; }

half4 DepthNormalsFrag(DepthVaryings i) : SV_Target
{
    return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
}

#endif
