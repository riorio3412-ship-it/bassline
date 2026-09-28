#ifndef BL23_TOON_FORWARD_INCLUDED
#define BL23_TOON_FORWARD_INCLUDED

#include "ToonCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    half4  color      : COLOR;
    float2 uv0        : TEXCOORD0;
    float4 uv1        : TEXCOORD1;   // face uv (xy), frontness (z), outline reduction (w)
    float3 uv2        : TEXCOORD2;   // bind-pose position (pattern / blood anchoring)
    float2 uv3        : TEXCOORD3;   // per-vertex material masks (scanned actors): x hair, y skin
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv0        : TEXCOORD0;
    float4 uv1        : TEXCOORD1;
    float3 bindPos    : TEXCOORD2;
    float3 positionWS : TEXCOORD3;
    half3  normalWS   : TEXCOORD4;
    half4  color      : TEXCOORD5;
    half   fogFactor  : TEXCOORD6;
    half2  masks      : TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ToonVert(Attributes v)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 pos = BL_ApplyVertexFx(v.positionOS.xyz, v.normalOS, v.uv2);
    VertexPositionInputs vp = GetVertexPositionInputs(pos);
    o.positionCS = vp.positionCS;
    o.positionWS = vp.positionWS;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.uv0 = TRANSFORM_TEX(v.uv0, _BaseMap);
    o.uv1 = v.uv1;
    o.bindPos = v.uv2;
    o.masks = (half2)v.uv3;
    o.color = v.color;
    o.fogFactor = ComputeFogFactor(vp.positionCS.z);
    return o;
}

// ------------------------------------------------------------------ face atlas composition
half4 BL_Cell(float2 local, float2 origin, float2 size, float2 dx, float2 dy)
{
    float inside = step(0.0, local.x) * step(local.x, 1.0) * step(0.0, local.y) * step(local.y, 1.0);
    float2 uv = origin + saturate(local) * size;
    half4 c = SAMPLE_TEXTURE2D_GRAD(_FaceAtlas, sampler_FaceAtlas, uv, dx * size, dy * size);
    c.a *= inside;
    return c;
}

half3 BL_Over(half3 dst, half4 src) { return lerp(dst, src.rgb, src.a); }

// GLB overlay mode (_FaceCover.a = 1): the scanned face is the neutral state; the cells carry their own skin
// (painted from the scan) and every element is multiplied by the per-pixel skin mask so nothing reaches hair.
half4 BL_GlbGate(half4 c, float idx) { if (_FaceCover.a > 0.5 && idx < 0.5) c.a = 0; return c; }

half3 BL_ComposeFace(half3 albedo, float4 uv1, out half highlight)
{
    highlight = 0;
    float2 f = uv1.xy;
    half front = saturate(uv1.z * 3.0 - 1.0);
    front *= SAMPLE_TEXTURE2D_LOD(_FaceMask, sampler_FaceMask, f, 0).r;
    float2 fdx = ddx(f), fdy = ddy(f);

    // base details (nose, lash shadow, lip tint) and fx overlays share the 2x2 _FaceFx atlas
    float2 fl = (f - (_FaceFxRect.xy - _FaceFxRect.zw)) / (2.0 * _FaceFxRect.zw);
    float fin = step(0.0, fl.x) * step(fl.x, 1.0) * step(0.0, fl.y) * step(fl.y, 1.0) * front;
    float2 fs = 0.5;
    float2 fldx = fdx / (2.0 * _FaceFxRect.zw), fldy = fdy / (2.0 * _FaceFxRect.zw);
    half4 fxBase = SAMPLE_TEXTURE2D_GRAD(_FaceFx, sampler_FaceFx, float2(0.0, 0.5) + saturate(fl) * fs, fldx * fs, fldy * fs);
    albedo = BL_Over(albedo, half4(fxBase.rgb, fxBase.a * fin));   // GLB faces: painted nose / lip shading
    if (_FaceState1.z > 0.001)
    {
        half4 blush = SAMPLE_TEXTURE2D_GRAD(_FaceFx, sampler_FaceFx, float2(0.5, 0.5) + saturate(fl) * fs, fldx * fs, fldy * fs);
        albedo = BL_Over(albedo, half4(blush.rgb, blush.a * fin * _FaceState1.z));
    }
    if (_FaceState1.w > 0.001)
    {
        half4 gloom = SAMPLE_TEXTURE2D_GRAD(_FaceFx, sampler_FaceFx, float2(0.5, 0.0) + saturate(fl) * fs, fldx * fs, fldy * fs);
        albedo = BL_Over(albedo, half4(gloom.rgb, gloom.a * fin * _FaceState1.w));
    }
    // motion track: physical strain. Reddening (struggle, strangling) and pallor (unconscious, dying) inside the face, fading
    // toward the hairline and jaw so the neck and hair are untouched.
    if (_FaceStrain.x + _FaceStrain.y > 0.001)
    {
        half k = front * saturate(1.7 - 2.3 * length((f - 0.5) * float2(1.0, 0.85)));
        albedo = lerp(albedo, albedo * half3(1.16, 0.74, 0.76) + half3(0.05, 0.0, 0.0), _FaceStrain.x * k * 0.6);
        half lum = dot(albedo, half3(0.3, 0.59, 0.11));
        half3 pale = lerp(half3(lum, lum, lum), half3(0.8, 0.86, 0.92) * (lum * 0.9 + 0.12), 0.55);
        albedo = lerp(albedo, pale, _FaceStrain.y * k * 0.75);
    }

    // brows (image-left brow = character's right; image-right is mirrored)
    {
        float2 hs = _FaceBrow.zw;
        float2 cL = float2(0.5 - _FaceBrow.x, _FaceBrow.y);
        float2 cR = float2(0.5 + _FaceBrow.x, _FaceBrow.y);
        float2 lL = (f - (cL - hs)) / (2.0 * hs);
        float2 lR = (f - (cR - hs)) / (2.0 * hs); lR.x = 1.0 - lR.x;
        float2 dx = fdx / (2.0 * hs), dy = fdy / (2.0 * hs);
        float bl = _FaceState0.z, br = _FaceState0.w;
        float2 oL = float2(fmod(bl, 4.0) * 0.25, 0.25 - (floor(bl / 4.0) + 1.0) * 0.0625);
        float2 oR = float2(fmod(br, 4.0) * 0.25, 0.25 - (floor(br / 4.0) + 1.0) * 0.0625);
        // GLB overlay mode keeps the scanned brows (they sit under the fringe)
        half glbOff = 1.0h - step(0.5h, _FaceCover.a);
        half4 b1 = BL_Cell(lL, oL, float2(0.25, 0.0625), dx, dy); b1.a *= glbOff;
        half4 b2 = BL_Cell(lR, oR, float2(0.25, 0.0625), float2(-dx.x, dx.y), float2(-dy.x, dy.y)); b2.a *= glbOff;
        albedo = BL_Over(albedo, half4(b1.rgb, b1.a * front));
        albedo = BL_Over(albedo, half4(b2.rgb, b2.a * front));
    }
    // GLB overlay mode: feature cells ignore the skin mask (it marks the scanned eye / lips as non-skin, which let the painted
    // open eye show through a closed lid: two eyes). Only the front gate limits them.
    half featFront = lerp(front, saturate(uv1.z * 3.0 - 1.0), step(0.5h, _FaceCover.a));
    // eyes
    {
        float2 hs = _FaceEye.zw;
        float2 cL = float2(0.5 - _FaceEye.x, _FaceEye.y);
        float2 cR = float2(0.5 + _FaceEye.x, _FaceEye.y);
        float2 lL = (f - (cL - hs)) / (2.0 * hs);
        float2 lR = (f - (cR - hs)) / (2.0 * hs); lR.x = 1.0 - lR.x;
        float2 dx = fdx / (2.0 * hs), dy = fdy / (2.0 * hs);
        float el = _FaceState0.x, er = _FaceState0.y;
        float2 oL = float2(fmod(el, 4.0) * 0.25, 1.0 - (floor(el / 4.0) + 1.0) * 0.125);
        float2 oR = float2(fmod(er, 4.0) * 0.25, 1.0 - (floor(er / 4.0) + 1.0) * 0.125);
        half4 e1 = BL_GlbGate(BL_Cell(lL, oL, float2(0.25, 0.125), dx, dy), el);
        half4 e2 = BL_GlbGate(BL_Cell(lR, oR, float2(0.25, 0.125), float2(-dx.x, dx.y), float2(-dy.x, dy.y)), er);
        // pure white texels inside the eye are highlights -> keep them unlit-bright
        highlight = max(highlight, e1.a * featFront * step(0.97, min(e1.r, min(e1.g, e1.b))));
        highlight = max(highlight, e2.a * featFront * step(0.97, min(e2.r, min(e2.g, e2.b))));
        albedo = BL_Over(albedo, half4(e1.rgb, e1.a * featFront));
        albedo = BL_Over(albedo, half4(e2.rgb, e2.a * featFront));
    }
    // mouth
    {
        float2 hs = _FaceMouth.zw;
        float2 c = _FaceMouth.xy;
        float2 l = (f - (c - hs)) / (2.0 * hs);
        float2 dx = fdx / (2.0 * hs), dy = fdy / (2.0 * hs);
        float mi = _FaceState1.x;
        float2 o = float2(fmod(mi, 4.0) * 0.25, 0.5 - (floor(mi / 4.0) + 1.0) * 0.0625);
        half4 m = BL_GlbGate(BL_Cell(l, o, float2(0.25, 0.0625), dx, dy), mi);
        albedo = BL_Over(albedo, half4(m.rgb, m.a * featFront));
    }
    // tears on top
    if (_FaceState1.y > 0.001)
    {
        half4 tears = SAMPLE_TEXTURE2D_GRAD(_FaceFx, sampler_FaceFx, float2(0.0, 0.0) + saturate(fl) * fs, fldx * fs, fldy * fs);
        albedo = BL_Over(albedo, half4(tears.rgb, tears.a * fin * _FaceState1.y));
        highlight = max(highlight, tears.a * fin * _FaceState1.y * 0.5);
    }
    return albedo;
}

// ------------------------------------------------------------------ dark faces (psychological "ink shadow" trope)
// Drawn procedurally in face uv from the calibrated eye / mouth layout, masked by the facial skin (hair stays lit).
void BL_DarkFace(float4 uv1, inout half3 albedo, out half ink, out half3 emis)
{
    ink = 0; emis = 0;
    float kind = _DarkFace.z;
    if (kind < 0.5 || (_DarkFace.x < 0.001 && _DarkFace.y < 0.001)) return;
    float2 f = uv1.xy;
    half front = saturate(uv1.z * 3.0 - 1.0) * SAMPLE_TEXTURE2D_LOD(_FaceMask, sampler_FaceMask, f, 0).r;
    float t = _DarkFace.w;
    half sh = _DarkFace.x, gl = _DarkFace.y;
    float eh = _FaceEye.w;
    float2 eC0 = float2(0.5 - _FaceEye.x, _FaceEye.y), eC1 = float2(0.5 + _FaceEye.x, _FaceEye.y);
    float2 m = _FaceMouth.xy;
    float mw = _FaceMouth.z * 0.26;          // half width of the real mouth
    float brow = _FaceEye.y + eh * 0.9;
    float chin = m.y - (_FaceEye.y - m.y) * 0.8;
    float fw = 0.025;
    bool cornered = kind > 1.5 && kind < 2.5;
    float target = cornered ? lerp(_FaceEye.y, m.y, 0.5) : chin - 0.05;
    float bottom = lerp(brow, target, sh);
    half s = smoothstep(brow + fw, brow - fw * 0.6, f.y) * smoothstep(bottom - fw, bottom + fw * 0.4, f.y);
    if (kind > 2.5) s *= lerp(0.18h, 1.0h, smoothstep(-0.04, 0.06, (f.x - 0.5) + (f.y - _FaceEye.y) * 0.4));
    ink = saturate(s * saturate(front * 1.6h) * saturate(sh * 1.25h));

    // eyes: pale glowing rings, pinprick pupils (cornered: wider, tinier, trembling; veiled: half-lidded)
    float r = eh * (cornered ? 0.4 : 0.28);
    float ringW = r * (cornered ? 0.12 : 0.16);
    float pr = r * (cornered ? 0.06 : 0.1);
    float2 jit = cornered ? (float2(BL_Noise3(float3(t * 23.0, 1.7, 2.3)), BL_Noise3(float3(3.1, t * 29.0, 5.3))) - 0.5) * r * 0.16 : float2(0, 0);
    [unroll] for (int k = 0; k < 2; k++)
    {
        float2 d = f - (k == 0 ? eC0 : eC1);
        float dist = length(d);
        half lid = kind > 2.5 ? smoothstep(r * 0.3, -r * 0.05, d.y) : 1.0h;
        half ring = (1.0h - smoothstep(ringW * 0.35, ringW, abs(dist - r))) * lid;
        half disc = (1.0h - smoothstep(r * 0.9, r, dist)) * lid;
        half pup = (1.0h - smoothstep(pr * 0.55, pr, length(d - jit))) * disc;
        half g = saturate(ring + disc * (kind > 2.5 ? 0.3h : 0.06h)) * (1.0h - pup);
        emis += _DarkFaceColor.rgb * g * gl * 0.9h * front;
        albedo = lerp(albedo, half3(0.015, 0.01, 0.02), pup * gl * front);
    }

    // mouth
    float2 q = f - m;
    if (kind < 1.5)
    {
        // wide, thin crescent grin: dark red inside, a row of small teeth under the upper edge
        float W = mw * 2.1, H = mw * 0.55;
        float X = q.x / W, Y = q.y / H;
        float yU = -0.1 + 1.0 * X * X, yL = -1.0 + 1.9 * X * X;
        half inside = step(abs(X), 1.0) * step(yL, Y) * step(Y, yU);
        half edge = inside * (1.0h - smoothstep(0.0, 0.09, min(Y - yL, yU - Y)));
        float tooth = abs(frac(X * 6.5) * 2.0 - 1.0);
        half teeth = inside * step(yU - Y, 0.22 * (1.0 - X * X) * (0.45 + 0.55 * tooth));
        half3 mc = lerp(half3(0.2, 0.015, 0.035), half3(0.08, 0.0, 0.02), saturate((yU - Y) / 0.7));
        mc = lerp(mc, half3(0.86, 0.83, 0.78), teeth);
        mc = lerp(mc, half3(0.03, 0.0, 0.01), edge * (1.0h - teeth));
        half a = inside * saturate(sh * 1.4h) * front;
        albedo = lerp(albedo, mc, a);
        ink *= 1.0h - a;
        emis += (half3(0.1, 0.0, 0.02) * (1.0h - teeth) + half3(0.2, 0.18, 0.16) * teeth) * a * gl;
    }
    else if (cornered)
    {
        // rigid smile, one corner twitching
        float W = mw * 1.25;
        float X = q.x / W;
        float tw = (BL_Noise3(float3(t * 9.0, 0.5, 0.5)) - 0.5) * 0.35 * saturate(X);
        float yc = mw * (-0.12 + 0.42 * X * X + tw);
        half ln = step(abs(X), 1.0) * (1.0h - smoothstep(mw * 0.05, mw * 0.1, abs(q.y - yc)));
        albedo = lerp(albedo, half3(0.13, 0.05, 0.06), ln * sh * front);
        // cold sweat glint sliding down the temple
        float2 sp = float2(0.5 + _FaceEye.x * 1.75, _FaceEye.y + eh * 0.55 - frac(t * 0.12) * 0.1);
        float2 sd = (f - sp) / float2(0.007, 0.012);
        half drop = 1.0h - smoothstep(0.6, 1.0, length(sd));
        emis += half3(0.7, 0.8, 0.95) * drop * gl * 0.55h * front;
    }
    else
    {
        // small closed smirk, lifted on the character's right
        float W = mw * 0.95;
        float X = q.x / W;
        float yc = mw * (-0.05 + 0.3 * X * X - 0.18 * X);
        half ln = step(abs(X), 1.0) * (1.0h - smoothstep(mw * 0.04, mw * 0.085, abs(q.y - yc)));
        albedo = lerp(albedo, half3(0.16, 0.06, 0.07), ln * sh * front);
    }
}

// ------------------------------------------------------------------ lighting
half3 BL_ToonAdditional(Light l, half3 N, half3 albedo)
{
    half ndl = dot(N, l.direction);
    half band = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, ndl);
    half sh = lerp(1.0h, smoothstep(0.25h, 0.6h, l.shadowAttenuation), _ShadowReceive);
    half atten = saturate(l.distanceAttenuation);
    half3 c = l.color * atten;
    // shade side still receives a little (keeps point-lit faces readable)
    return albedo * c * lerp(_ShadeColor.rgb * 0.35h, 1.0h, band * sh);
}

half4 ToonFrag(Varyings i, half facing : VFACE) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv0);
    half3 albedo = baseTex.rgb * _BaseColor.rgb;
    half alpha = baseTex.a * _BaseColor.a;
    #if defined(_ALPHATEST_ON)
        clip(alpha - _Cutoff);
    #endif
    if (_UseVertexColor > 0.5) albedo *= i.color.rgb;
    albedo = BL_ApplyPattern(albedo, i.bindPos);

    if (_DecalOn > 0.5)
    {
        float2 dl = (i.bindPos.xy - (_DecalRect.xy - _DecalRect.zw)) / (2.0 * _DecalRect.zw);
        float din = step(0.0, dl.x) * step(dl.x, 1.0) * step(0.0, dl.y) * step(dl.y, 1.0) * step(0.0, i.bindPos.z);
        half4 dc = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, saturate(dl));
        albedo = lerp(albedo, dc.rgb, dc.a * din);
    }

    half eyeHighlight = 0;
    half darkInk = 0; half3 darkEmis = 0;
    #if defined(_FACE)
        albedo = BL_ComposeFace(albedo, i.uv1, eyeHighlight);
        BL_DarkFace(i.uv1, albedo, darkInk, darkEmis);
    #endif

    half cavity = lerp(1.0h, i.color.a, _CavityStrength * _UseVertexColor);

    // wounds / blood / bruises
    float bruiseMask = 0;
    float bloodMask = BL_BloodMask(i.bindPos);
    if (_Bruise > 0.001)
    {
        float n = BL_Fbm(i.bindPos * 25.0 + 5.0);
        float4 wb[8] = { _Wound0, _Wound1, _Wound2, _Wound3, _Wound4, _Wound5, _Wound6, _Wound7 };
        [unroll] for (int k = 0; k < 8; k++)
        {
            if (wb[k].w < -0.0001)
            {
                float r = -wb[k].w * (0.7 + 0.6 * n);
                bruiseMask = max(bruiseMask, saturate((r - distance(i.bindPos, wb[k].xyz)) / (r * 0.6)));
            }
        }
        bruiseMask *= _Bruise;
    }
    half3 bruiseCol = lerp(half3(0.42, 0.22, 0.42), half3(0.30, 0.12, 0.20), BL_Noise3(i.bindPos * 60.0));
    albedo = lerp(albedo, albedo * bruiseCol * 1.6h, bruiseMask * 0.85h);
    half bloodN = BL_Noise3(i.bindPos * 80.0);
    half3 bloodCol = _BloodColor.rgb * (0.75h + 0.35h * bloodN);
    albedo = lerp(albedo, bloodCol, bloodMask);
    half wetness = saturate(_Wet + bloodMask * 0.45);
    albedo *= lerp(1.0h, 0.62h, _Wet);

    // ---- lighting
    half3 N = normalize(i.normalWS) * (facing > 0 ? 1.0h : -1.0h);
    half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS;
    inputData.normalWS = N;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    half4 shadowMask = half4(1, 1, 1, 1);

    Light mainLight = GetMainLight(inputData.shadowCoord, i.positionWS, shadowMask);
    half ndl = dot(N, mainLight.direction);
    half sh = lerp(1.0h, smoothstep(0.3h, 0.65h, mainLight.shadowAttenuation), _ShadowReceive);
    half ndlC = ndl - (1.0h - cavity) * 0.6h;
    half lit = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, ndlC) * sh;
    half deep = smoothstep(_Ramp2Threshold - _RampSmooth, _Ramp2Threshold + _RampSmooth, ndlC);
    half3 shadeTint = lerp(_Shade2Color.rgb, _ShadeColor.rgb, deep);
    half3 mainCol = mainLight.color * mainLight.distanceAttenuation;
    // key clamp: a strong key (close-up / studio lights) must not flatten the painted colours into white
    { half kl = max(mainCol.r, max(mainCol.g, mainCol.b)); if (kl > 1.1h) mainCol *= (1.1h + (kl - 1.1h) * 0.25h) / kl; }
    half hairM = _MaskMode > 0.5h ? i.masks.x : 1.0h;
    half skinM = _MaskMode > 0.5h ? i.masks.y : 1.0h;
    // painted-shadow colour: the shade side keeps the albedo's luminance but gains saturation (a multiplied grey
    // shadow is what makes toon shading look cheap), skin shadows lean warm
    half skinK = saturate(_SkinSSS * 3.0h) * skinM;           // skin: softer ramp, warm (not violet) shade, gentler saturation
    half3 albS = pow(max(albedo, 1e-3h), 1.0h + 0.6h * _ShadeSat * (1.0h - 0.75h * skinK));
    albS *= Luminance(albedo) / max(Luminance(albS), 1e-3h);
    half3 skinTint = half3(0.9h, 0.8h, 0.78h) * lerp(0.86h, 1.0h, deep);
    half3 tintS = lerp(shadeTint, skinTint, skinK);
    half rsS = _RampSmooth + 0.09h * skinK;
    half litS = lerp(lit, smoothstep(_RampThreshold - rsS, _RampThreshold + rsS, ndlC) * sh, skinK);
    half3 color = lerp(albS * tintS, albedo, litS) * mainCol;
    // skin: warm band along the light / shadow terminator (cheap subsurface feel)
    half term = saturate(1.0h - abs(ndlC - _RampThreshold) / (_RampSmooth * 3.0h + 0.1h));
    color += albedo * _SSSColor.rgb * term * _SkinSSS * skinM * mainCol * 0.18h * sh;

    // ambient (flattened SH so the ambient term reads as a flat toon fill)
    half3 amb = (SampleSH(N) * 0.4h + SampleSH(half3(0, 1, 0)) * 0.6h) * _AmbientStrength;
    color += albedo * amb * lerp(0.75h, 1.0h, cavity);

    #if defined(_ADDITIONAL_LIGHTS)
    uint lightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
        color += BL_ToonAdditional(l, N, albedo);
    }
    #endif
    LIGHT_LOOP_BEGIN(lightCount)
        Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
        color += BL_ToonAdditional(l, N, albedo);
    LIGHT_LOOP_END
    #endif

    // gloss (hair ring / shoes / lenses / wet / blood)
    half3 H = normalize(mainLight.direction + V);
    half ndh = saturate(dot(N, H));
    // small wet highlight on blood (kept tight and tinted so soaked cloth never flashes white)
    color += half3(1.0h, 0.5h, 0.5h) * mainCol * smoothstep(0.986h, 0.996h, ndh) * bloodMask * 0.25h;
    half gloss = _GlossStrength + _Wet * 0.6h;
    if (gloss > 0.001)
    {
        half thr = lerp(_GlossThreshold, 0.93h, saturate(_Wet));
        half s = smoothstep(thr - 0.015h, thr + 0.015h, ndh) * gloss;
        color += _GlossColor.rgb * mainCol * s * max(lit, 0.25h);
        // fake view-aligned highlight so glossy things read even without a strong key light
        half vh = smoothstep(0.86h, 0.9h, saturate(dot(N, normalize(V + half3(0, 0.55, 0))))) * (_GlossStrength + _Wet * 0.6h) * 0.35h;
        color += _GlossColor.rgb * vh * (amb + mainCol * 0.3h);
    }
    if (_HairRing * hairM > 0.001)
    {
        // "angel ring": a view-dependent band with broken, strand-like edges; tinted from the hair colour
        half3 ringDir = normalize(V + half3(0, 0.9, 0));
        half r = dot(N, ringDir);
        half jitter = (BL_Noise3(i.bindPos * float3(260.0, 12.0, 260.0)) - 0.5h) * 0.045h;
        half band = smoothstep(0.83h, 0.855h, r + jitter) * (1.0h - smoothstep(0.895h, 0.925h, r + jitter));
        half streak = 0.65h + 0.35h * sin(atan2(i.bindPos.x, i.bindPos.z) * 60.0);
        half3 ringCol = saturate(albedo * 1.8h + 0.12h);
        color += ringCol * band * streak * _HairRing * hairM * (mainCol * (0.45h + 0.6h * lit) + amb * 1.2h + 0.06h);
    }

    // highlight shoulder: strong key / point lights (dialogue close-ups, trial studio) would burn skin and white cloth
    // flat (and into bloom); luminance above 0.8 rolls off smoothly, hue kept
    {
        half lumC = Luminance(color);
        half over = max(lumC - 0.72h, 0.0h);
        half target = lumC - over + over / (1.0h + over * 2.4h);
        color *= target / max(lumC, 1e-3h);
    }

    // rim light (tinted toward the surface colour so it reads as light wrapping the form, not a white halo)
    half ndv = saturate(dot(N, V));
    half rim = smoothstep(1.0h - _RimPower - 0.05h, 1.0h - _RimPower + 0.05h, 1.0h - ndv);
    half rimLightSide = saturate(0.35h + 0.65h * saturate(dot(N, mainLight.direction) + 0.3h));
    half3 rimCol = lerp(_RimColor.rgb, saturate(albedo * 1.5h + 0.22h), 0.45h);
    color += rimCol * rim * _RimStrength * rimLightSide * (1.0h - 0.5h * hairM * step(0.5h, _MaskMode)) * (0.4h + 0.6h * saturate(Luminance(mainCol + amb * 2.0h)));

    // eye highlights stay bright (anime sparkle)
    color = lerp(color, max(color, albedo * 0.95h), eyeHighlight);

    // dark face: ink shadow over the facial skin (hair keeps its light), glowing eyes / grin on top
    color = lerp(color, color * 0.08h + half3(0.012h, 0.008h, 0.016h), darkInk);
    color += darkEmis;

    // emission + BREAK
    color += _EmissionColor.rgb * _Emission;
    if (_BreakAmount > 0.001)
    {
        half fres = pow(1.0h - ndv, 2.0h);
        half scan = step(0.5h, frac(i.positionCS.y * 0.25h + _Time.y * 6.0h)) * 0.3h;
        half flick = step(0.5h, BL_Hash13(float3(floor(_Time.y * 10.0), 1.0, 2.0)));
        half3 bc = lerp(_BreakColor.rgb, half3(0.9, 0.85, 1.0), flick * 0.4h);
        half gray = Luminance(color);
        color = lerp(color, half3(gray, gray * 0.8h, gray * 1.1h), _BreakAmount * 0.45h);
        color += bc * (fres * 1.6h + scan) * _BreakAmount;
    }

    color = MixFog(color, i.fogFactor);
    return half4(color, 1.0h);
}

#endif
