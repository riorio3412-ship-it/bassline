#ifndef BL23_TOON_COMMON_INCLUDED
#define BL23_TOON_COMMON_INCLUDED

// BL23 character toon shading - shared declarations (SRP batcher compatible: every pass uses this CBUFFER).
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
TEXTURE2D(_FaceAtlas);  SAMPLER(sampler_FaceAtlas);
TEXTURE2D(_FaceFx);     SAMPLER(sampler_FaceFx);
TEXTURE2D(_DecalTex);   SAMPLER(sampler_DecalTex);
TEXTURE2D(_FaceMask);   SAMPLER(sampler_FaceMask);   // GLB faces: 1 = real facial skin / eye / mouth, 0 = hair (per pixel, face uv)

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _ShadeColor;
    half4  _Shade2Color;
    half   _RampThreshold;
    half   _RampSmooth;
    half   _Ramp2Threshold;
    half   _ShadowReceive;
    half4  _RimColor;
    half   _RimPower;
    half   _RimStrength;
    half   _GlossThreshold;
    half   _GlossStrength;
    half4  _GlossColor;
    half   _HairRing;
    half   _ShadeSat;
    half   _SkinSSS;
    half   _MaskMode;
    half   _OutlineTint;
    half4  _SSSColor;
    half   _UseVertexColor;
    half   _CavityStrength;
    half   _AmbientStrength;
    half4  _OutlineColor;
    half   _OutlineWidth;
    half   _OutlineZ;
    half   _Emission;
    half   _Cutoff;
    half4  _EmissionColor;
    half   _BloodAmount;
    half   _Wet;
    half   _BreakAmount;
    half   _WoundStrength;
    half4  _BloodColor;
    half4  _BreakColor;
    float4 _Wound0;
    float4 _Wound1;
    float4 _Wound2;
    float4 _Wound3;
    float4 _Wound4;
    float4 _Wound5;
    float4 _Wound6;
    float4 _Wound7;
    half   _PatternType;
    half   _DecalOn;
    half   _Bruise;
    half   _Pad0;
    half4  _PatternColor;
    float4 _PatternParams;
    float4 _DecalRect;
    float4 _FaceEye;
    float4 _FaceBrow;
    float4 _FaceMouth;
    float4 _FaceFxRect;
    float4 _FaceState0;
    float4 _FaceState1;
    half4  _FaceCover;   // rgb: skin colour used to paint over scanned (GLB) facial features, a: 1 = GLB overlay mode
    float4 _DarkFace;    // x shadow creep, y eye glow, z kind (1 hollow grin, 2 cornered stare, 3 veiled smirk), w time
    half4  _DarkFaceColor;
    float4 _FaceStrain;  // motion track: x flush (reddening), y pallor, z gasp (unused by the shader)
CBUFFER_END

// ---------------------------------------------------------------- noise
float BL_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float BL_Noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = BL_Hash13(i);
    float n100 = BL_Hash13(i + float3(1, 0, 0));
    float n010 = BL_Hash13(i + float3(0, 1, 0));
    float n110 = BL_Hash13(i + float3(1, 1, 0));
    float n001 = BL_Hash13(i + float3(0, 0, 1));
    float n101 = BL_Hash13(i + float3(1, 0, 1));
    float n011 = BL_Hash13(i + float3(0, 1, 1));
    float n111 = BL_Hash13(i + float3(1, 1, 1));
    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
}

float BL_Fbm(float3 p)
{
    float a = 0.5, s = 0.0;
    for (int i = 0; i < 4; i++) { s += a * BL_Noise3(p); p = p * 2.03 + 11.7; a *= 0.5; }
    return s;
}

// ---------------------------------------------------------------- vertex FX (BREAK glitch)
float3 BL_ApplyVertexFx(float3 positionOS, float3 normalOS, float3 bindPos)
{
    if (_BreakAmount > 0.001)
    {
        float t = floor(_Time.y * 14.0);
        float slice = floor(bindPos.y * 22.0);
        float h = BL_Hash13(float3(slice, t, 3.7));
        float on = step(1.0 - 0.28 * _BreakAmount, h);
        float3 dir = float3(BL_Hash13(float3(slice, t, 1.1)) - 0.5, 0, BL_Hash13(float3(slice, t, 7.3)) - 0.5);
        positionOS += dir * on * 0.09 * _BreakAmount;
        positionOS += normalOS * (sin(_Time.y * 23.0 + bindPos.y * 40.0) * 0.004 * _BreakAmount);
    }
    return positionOS;
}

// ---------------------------------------------------------------- blood / wounds
float BL_WoundMask1(float3 p, float4 w, float n)
{
    if (w.w <= 0.0001) return 0;
    float d = distance(p, w.xyz);
    float r = w.w * (0.65 + 0.7 * n);
    return saturate((r - d) / max(r * 0.35, 0.002));
}

float BL_BloodMask(float3 bindPos)
{
    float n = BL_Fbm(bindPos * 19.0);
    float m = 0;
    m = max(m, BL_WoundMask1(bindPos, _Wound0, n));
    m = max(m, BL_WoundMask1(bindPos, _Wound1, n));
    m = max(m, BL_WoundMask1(bindPos, _Wound2, n));
    m = max(m, BL_WoundMask1(bindPos, _Wound3, n));
    m = max(m, BL_WoundMask1(bindPos, _Wound4, n));
    m = max(m, BL_WoundMask1(bindPos, _Wound5, n));
    m = max(m, BL_WoundMask1(bindPos, _Wound6, n));
    m = max(m, BL_WoundMask1(bindPos, _Wound7, n));
    m *= _WoundStrength;
    // spatter (SetBloodied): noise speckles + larger splashes on the front
    if (_BloodAmount > 0.001)
    {
        float sp = BL_Noise3(bindPos * 55.0 + 3.1);
        float big = BL_Fbm(bindPos * 7.0 + 17.0);
        float front = saturate(0.6 + bindPos.z * 6.0);
        float s1 = step(1.0 - 0.22 * _BloodAmount, sp) * front;
        float s2 = smoothstep(0.72 - 0.3 * _BloodAmount, 0.76 - 0.3 * _BloodAmount, big) * saturate(_BloodAmount * 1.4);
        m = max(m, max(s1, s2 * front));
    }
    return saturate(m);
}

// ---------------------------------------------------------------- procedural cloth patterns (bind space, meters)
half3 BL_ApplyPattern(half3 albedo, float3 bp)
{
    int type = (int)(_PatternType + 0.5);
    if (type <= 0) return albedo;
    float sc = max(_PatternParams.x, 0.0001);
    float ang = atan2(bp.x, bp.z);          // around the body axis
    float arc = ang * 0.16;                  // ~ arc length on a torso
    half3 pc = _PatternColor.rgb;
    float m = 0;
    if (type == 1) // pinstripe (vertical)
    {
        float u = frac(arc / sc);
        float w = _PatternParams.y;
        m = smoothstep(1.0 - w - 0.05, 1.0 - w + 0.05, 1.0 - abs(u - 0.5) * 2.0);
    }
    else if (type == 2) // lace: scalloped holes + floral dots
    {
        float2 q = float2(arc, bp.y) / sc;
        float2 c = frac(q) - 0.5;
        float r = length(c);
        float petals = 0.28 + 0.07 * cos(atan2(c.y, c.x) * 6.0);
        m = smoothstep(petals + 0.03, petals, r) * (1.0 - smoothstep(0.1, 0.07, r));
        float2 c2 = frac(q + 0.5) - 0.5;
        m = max(m, smoothstep(0.08, 0.05, length(c2)));
    }
    else if (type == 3) // brocade / damask: ogee lattice + medallions
    {
        float2 q = float2(arc, bp.y) / sc;
        float2 g = float2(q.x, q.y * 0.7);
        float2 c = frac(g) - 0.5;
        float ogee = abs(abs(c.x) - 0.25 * (1.0 + cos(c.y * 6.2831)));
        float med = length(c * float2(1.0, 1.4));
        float petals = 0.18 + 0.06 * cos(atan2(c.y, c.x) * 4.0);
        m = max(smoothstep(0.035, 0.015, ogee), smoothstep(petals, petals - 0.03, med) * 0.9);
        m *= 0.85;
    }
    else if (type == 4) // oil stains
    {
        float n = BL_Fbm(bp * (1.0 / sc));
        m = smoothstep(0.58, 0.64, n) * 0.85;
        float n2 = BL_Noise3(bp * (3.0 / sc) + 7.0);
        m = max(m, smoothstep(0.83, 0.87, n2) * 0.7);
    }
    else if (type == 5) // faded / washed
    {
        float n = BL_Fbm(bp * (1.0 / sc) + 2.0);
        return albedo * (0.88 + 0.28 * n);
    }
    else if (type == 6) // ripped (knee slits showing skin)
    {
        float kneeY = _PatternParams.z;
        float2 q = float2(bp.x * 1.0, bp.y - kneeY);
        float front = step(0.0, bp.z + 0.01);
        float slit = 0;
        [unroll] for (int i = 0; i < 3; i++)
        {
            float yy = q.y - (i - 1) * 0.028;
            float xx = frac(bp.x * 9.0) - 0.5;
            slit = max(slit, smoothstep(0.006, 0.0, abs(yy)) * smoothstep(0.42, 0.2, abs(xx)));
        }
        m = slit * front * step(abs(q.y), 0.06);
    }
    else if (type == 7) // horizontal stripes (stockings)
    {
        float v = frac(bp.y / sc);
        m = step(0.5, v);
    }
    else if (type == 8) // knit rib
    {
        float u = frac(arc / sc);
        return albedo * (0.9 + 0.12 * smoothstep(0.2, 0.5, abs(u - 0.5) * 2.0));
    }
    else if (type == 9) // fur clumps
    {
        float n = BL_Fbm(bp * (1.0 / sc));
        float streak = BL_Noise3(float3(bp.x * 60.0, bp.y * 9.0, bp.z * 60.0));
        return albedo * (0.78 + 0.35 * n) * (0.9 + 0.15 * streak);
    }
    else if (type == 10) // denim twill
    {
        float d = frac((bp.y + arc) / sc);
        return albedo * (0.92 + 0.1 * step(0.5, d));
    }
    else if (type == 11) // tape measure ticks
    {
        float v = frac(_PatternParams.z > 0.5 ? bp.x / sc : bp.y / sc);
        m = step(0.85, v);
    }
    return lerp(albedo, pc, m * _PatternColor.a);
}

#endif
