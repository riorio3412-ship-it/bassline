#ifndef BL23_MANSION_COMMON_INCLUDED
#define BL23_MANSION_COMMON_INCLUDED

// ---------------------------------------------------------------------------------------------
// Globals driven by BL23.Game.Mansion.MansionLighting / MansionAtmosphere
// ---------------------------------------------------------------------------------------------
TEXTURE2D_ARRAY(_BL_AmbientTex); SAMPLER(sampler_BL_AmbientTex);
float4 _BL_AmbientParams;      // x = 1/width(m), y = 1/depth(m), z = global mult, w = enabled
float4 _BL_AmbientFloorY;      // floor split heights: x = -5 (courtroom|B1), y = -0.4 (B1|1F), z = 4.6 (1F|2F)
float4 _BL_CircuitPower0;      // circuits 0..3 electric power multipliers (0..1)
float4 _BL_CircuitPower1;      // circuits 4..7
float4 _BL_Globals;            // x = darkness 0..1, y = noise 0..1, z = tension 0..1, w = time scale
float4 _BL_Day;                // x = daylight 0..1 (0 at night), y = dusk / dawn warmth 0..1 (MansionView.Daylight)
TEXTURE2D(_BL_PlanarTex); SAMPLER(sampler_BL_PlanarTex);
float4 _BL_PlanarParams;       // x = plane height, y = active 0/1, z = strength, w = distortion

float BL_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}
float BL_Hash31(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}
float BL_ValueNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float a = BL_Hash21(i), b = BL_Hash21(i + float2(1, 0)), c = BL_Hash21(i + float2(0, 1)), d = BL_Hash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}
float BL_Fbm(float2 p)
{
    float v = 0, a = 0.5;
    for (int i = 0; i < 4; i++) { v += a * BL_ValueNoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
    return v;
}

// Soft highlight shoulder: values above "knee" roll off smoothly toward knee + 1/k instead of spiking into bloom.
half3 BL_SoftClamp(half3 c, half knee)
{
    half m = max(c.r, max(c.g, c.b));
    if (m <= knee) return c;
    half e = m - knee;
    return c * ((knee + e / (1.0 + e * 0.6)) / m);
}

float BL_CircuitPower(float idx)
{
    // -1 => magic / moon (always on), -2 => fire (only the darkness chapter dims it), 0..7 => electric circuit
    // multiplier (already includes darkness, set from C#).
    if (idx < -1.5) return saturate(1.0 - _BL_Globals.x);
    if (idx < -0.5) return 1.0;
    int i = (int)round(idx);
    float p = i < 4 ? _BL_CircuitPower0[i] : _BL_CircuitPower1[i - 4];
    return p;
}

float BL_FloorSlice(float y)
{
    return y < _BL_AmbientFloorY.x ? 0 : (y < _BL_AmbientFloorY.y ? 1 : (y < _BL_AmbientFloorY.z ? 2 : 3));
}

// Per-room ambient (dream "fill" light) sampled from the 0.5 m ambient lattice. Falls back to SH.
half3 BL_SampleAmbient(float3 positionWS, half3 normalWS)
{
    if (_BL_AmbientParams.w < 0.5)
    {
    #if defined(UNIVERSAL_LIGHTING_INCLUDED)
        return SampleSH(normalWS);
    #else
        return half3(0.05, 0.04, 0.06);
    #endif
    }
    float3 p = positionWS + normalWS * 0.3;
    float2 uv = float2(p.x * _BL_AmbientParams.x, p.z * _BL_AmbientParams.y);
    half3 amb = SAMPLE_TEXTURE2D_ARRAY_LOD(_BL_AmbientTex, sampler_BL_AmbientTex, uv, BL_FloorSlice(p.y), 0).rgb;
    // hemisphere: up-facing surfaces receive the most fill, ceilings fall into shadow (candle-lit feel)
    half hemi = 0.72 + 0.28 * normalWS.y;
    return amb * hemi * _BL_AmbientParams.z;
}

#endif
