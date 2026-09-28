// Procedural faces: TV static, watching eye, colour bars, nonsense departure board, clock faces (hands animated in shader).
// UV 0..1 over the face. TEXCOORD2: x = mode (0 static, 1 eye, 2 bars, 3 board, 4 clock, 5 on-air sign),
// y = seed / clock hour offset, z = speed (clock: negative = backwards), w = power group + 10 (0 = always on).
Shader "BL23/MansionScreen"
{
    Properties
    {
        [HDR] _Color("Tint", Color) = (1,1,1,1)
        _Intensity("Intensity", Float) = 1.5
        _FaceColor("Clock face", Color) = (0.92, 0.88, 0.78, 1)
        _InkColor("Clock ink", Color) = (0.08, 0.05, 0.05, 1)
        _Aspect("Aspect w/h", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Screen"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "MansionCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; half _Intensity; half4 _FaceColor; half4 _InkColor; float _Aspect;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 custom : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 custom : TEXCOORD1; half4 color : TEXCOORD2; float3 positionWS : TEXCOORD3; half3 normalWS : TEXCOORD4; half fog : TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V Vert(A v)
            {
                V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv; o.custom = v.custom; o.color = v.color; o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Seg(float2 p, float2 a, float2 b, float w)
            {
                float2 pa = p - a, ba = b - a; float h = saturate(dot(pa, ba) / dot(ba, ba));
                return smoothstep(w, w * 0.6, length(pa - ba * h));
            }
            // 7-segment digit in [0,1]^2 cell
            float Digit(float2 p, int d)
            {
                // segment bits: a b c d e f g
                int bits[10] = { 0x7E, 0x30, 0x6D, 0x79, 0x33, 0x5B, 0x5F, 0x70, 0x7F, 0x7B };
                int b = bits[clamp(d, 0, 9)];
                float w = 0.09; float r = 0;
                float2 A0 = float2(0.2, 0.9), A1 = float2(0.8, 0.9), M0 = float2(0.2, 0.5), M1 = float2(0.8, 0.5), B0 = float2(0.2, 0.1), B1 = float2(0.8, 0.1);
                if (b & 0x40) r = max(r, Seg(p, A0, A1, w));
                if (b & 0x20) r = max(r, Seg(p, A1, M1, w));
                if (b & 0x10) r = max(r, Seg(p, M1, B1, w));
                if (b & 0x08) r = max(r, Seg(p, B0, B1, w));
                if (b & 0x04) r = max(r, Seg(p, M0, B0, w));
                if (b & 0x02) r = max(r, Seg(p, A0, M0, w));
                if (b & 0x01) r = max(r, Seg(p, M0, M1, w));
                return r;
            }
            // pseudo-hangul glyph: random strokes inside a block
            float Glyph(float2 p, float seed)
            {
                float r = 0;
                for (int k = 0; k < 4; k++)
                {
                    float h1 = BL_Hash21(float2(seed, k * 3.1)), h2 = BL_Hash21(float2(seed * 1.7, k + 11.0)), h3 = BL_Hash21(float2(k, seed * 2.3));
                    float2 a = float2(0.15 + 0.7 * h1, 0.15 + 0.7 * h2);
                    float2 b = h3 > 0.5 ? float2(a.x, 0.15 + 0.7 * h3) : float2(0.15 + 0.7 * h3, a.y);
                    r = max(r, Seg(p, a, b, 0.07));
                }
                if (BL_Hash21(float2(seed, 99)) > 0.6) r = max(r, smoothstep(0.16, 0.1, abs(length(p - 0.5) - 0.2)));
                return r;
            }

            half3 ClockFace(float2 uv, float4 c)
            {
                float2 p = uv * 2 - 1;
                float r = length(p);
                if (r > 1) discard;
                half3 col = _FaceColor.rgb * (0.85 + 0.15 * (1 - r));
                col = lerp(col, _InkColor.rgb, smoothstep(0.035, 0.0, abs(r - 0.93)));
                float ang = atan2(p.x, p.y); // 0 at 12 o'clock, clockwise
                float tick = abs(frac(ang / 6.28318 * 12 + 0.5) - 0.5);
                col = lerp(col, _InkColor.rgb, step(tick, 0.035) * step(0.74, r) * step(r, 0.88));
                float mt = abs(frac(ang / 6.28318 * 60 + 0.5) - 0.5);
                col = lerp(col, _InkColor.rgb, step(mt, 0.06) * step(0.84, r) * step(r, 0.88) * 0.7);
                // time: seed = hour offset (0..12), speed (1 = real time, <0 backwards, 0 stopped)
                float t = c.y * 3600.0 + _Time.y * c.z;
                float sec = floor(frac(t / 60.0) * 60.0 + 1e-3);
                float secA = sec / 60.0 * 6.28318;
                float minA = frac(t / 3600.0) * 6.28318;
                float hrA = frac(t / 43200.0) * 6.28318;
                float h = Seg(p, 0, float2(sin(hrA), cos(hrA)) * 0.45, 0.07);
                float m = Seg(p, 0, float2(sin(minA), cos(minA)) * 0.72, 0.045);
                float s = Seg(p, -float2(sin(secA), cos(secA)) * 0.15, float2(sin(secA), cos(secA)) * 0.8, 0.02);
                col = lerp(col, _InkColor.rgb, max(h, m));
                col = lerp(col, half3(0.7, 0.05, 0.1), s);
                col = lerp(col, _InkColor.rgb, smoothstep(0.07, 0.05, r));
                return col;
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float mode = i.custom.x;
                float2 uv = i.uv;
                float group = i.custom.w > 0.5 ? i.custom.w - 10.0 : -1.0;
                half power = BL_CircuitPower(group);
                half3 col = 0;
                bool lit = true;
                float t = _Time.y;
                if (mode < 0.5)
                {
                    // static with a rolling bar and a faint figure that is not there
                    float n = BL_Hash21(floor(uv * float2(160, 120)) + floor(t * 24.0) * 1.37);
                    float bar = smoothstep(0.1, 0.0, abs(frac(uv.y - t * 0.15) - 0.5) - 0.35);
                    float2 fp = uv - float2(0.5, 0.35);
                    float fig = smoothstep(0.23, 0.2, length(fp * float2(1.6, 1.0))) + smoothstep(0.1, 0.08, length(uv - float2(0.5, 0.68)));
                    col = (n * 0.8 + 0.2) * (0.7 + 0.3 * bar) * half3(0.75, 0.85, 1.0) - fig * 0.25 * (0.5 + 0.5 * sin(t * 0.7));
                    col *= 0.9 + 0.1 * sin(uv.y * 400.0);
                }
                else if (mode < 1.5)
                {
                    float2 p = (uv - 0.5) * float2(_Aspect, 1);
                    float ex = abs(p.x) / 0.42;
                    float lid = 0.22 * (1 - ex * ex) * (0.15 + 0.85 * smoothstep(0.0, 0.1, abs(frac(t * 0.13) - 0.5) - 0.02));
                    float2 look = float2(sin(t * 0.5) * 0.12, sin(t * 0.31) * 0.04);
                    float r = length(p - look);
                    col = half3(0.02, 0.0, 0.03);
                    if (ex < 1 && abs(p.y) < lid)
                    {
                        col = half3(0.9, 0.85, 0.8);
                        col = lerp(col, half3(0.9, 0.1, 0.5), smoothstep(0.13, 0.12, r));
                        col = lerp(col, 0.01, smoothstep(0.06, 0.05, r));
                    }
                    col *= 0.85 + 0.15 * sin(uv.y * 300.0 + t * 20.0);
                }
                else if (mode < 2.5)
                {
                    half3 bars[7] = { half3(0.75,0.75,0.75), half3(0.75,0.75,0), half3(0,0.75,0.75), half3(0,0.75,0), half3(0.75,0,0.75), half3(0.75,0,0), half3(0,0,0.75) };
                    int k = (int)floor(uv.x * 7);
                    col = bars[clamp(k, 0, 6)];
                    if (uv.y < 0.25) col = uv.x < 0.5 ? half3(0.05, 0.05, 0.08) : half3(0.9, 0.9, 0.9) * step(0.5, frac(uv.x * 12));
                    // a black rectangle where a face should be
                    col *= 1 - step(abs(uv.x - 0.5), 0.12) * step(abs(uv.y - 0.6), 0.18) * (0.5 + 0.5 * step(0.5, frac(t * 0.5)));
                }
                else if (mode < 3.5)
                {
                    // departure board: 8 rows | time | destination glyphs | status
                    float rows = 8;
                    float row = floor(uv.y * rows);
                    float2 cell = float2(uv.x, frac(uv.y * rows));
                    float flip = floor(t * (0.25 + BL_Hash21(float2(row, i.custom.y)) * 0.2) + row * 0.37);
                    float seed = BL_Hash21(float2(row + flip * 7.0, i.custom.y + 3.0)) * 100.0;
                    col = half3(0.02, 0.02, 0.025);
                    float gridLine = step(frac(uv.x * 26.0), 0.08) + step(cell.y, 0.07);
                    half3 amber = half3(1.0, 0.62, 0.12);
                    if (uv.x < 0.24)
                    {
                        // nonsense times like 25:71
                        float cx = uv.x / 0.24 * 5.0; int ci = (int)floor(cx);
                        float2 dp = float2(frac(cx), cell.y * 1.25 - 0.12);
                        int dig = (int)floor(BL_Hash21(float2(seed, ci)) * 10.0);
                        if (ci == 0) dig = 1 + (int)(BL_Hash21(float2(seed, 5)) * 2.99);
                        float on = ci == 2 ? step(abs(dp.x - 0.5), 0.1) * step(abs(abs(dp.y - 0.5) - 0.2), 0.08) : Digit(dp, dig);
                        col = lerp(col, amber, on);
                    }
                    else if (uv.x < 0.82)
                    {
                        float gx = (uv.x - 0.26) / 0.56 * 9.0; int gi = (int)floor(gx);
                        float2 gp = float2(frac(gx), cell.y * 1.2 - 0.1);
                        float g = BL_Hash21(float2(seed, gi + 20)) > 0.12 ? Glyph(gp, floor(seed * 13.0) + gi) : 0;
                        col = lerp(col, half3(0.95, 0.93, 0.85), g);
                    }
                    else
                    {
                        float blink = step(0.5, frac(t * 1.3 + row * 0.2));
                        half3 st = BL_Hash21(float2(seed, 77)) > 0.5 ? half3(1.0, 0.1, 0.2) : half3(0.2, 1.0, 0.5);
                        float2 gp = float2(frac((uv.x - 0.83) / 0.16 * 2.0), cell.y * 1.2 - 0.1);
                        col = lerp(col, st, Glyph(gp, floor(seed * 7.0) + floor((uv.x - 0.83) / 0.08)) * blink);
                    }
                    col *= 1 - saturate(gridLine) * 0.6;
                }
                else if (mode < 4.5)
                {
                    col = ClockFace(uv, i.custom);
                    lit = false;
                }
                else
                {
                    // ON AIR sign glow
                    float2 p = uv * 2 - 1;
                    col = half3(1.0, 0.05, 0.12) * (0.6 + 0.4 * step(0.3, frac(t * 0.8))) * (1 - smoothstep(0.85, 1.0, max(abs(p.x), abs(p.y))));
                }
                if (!lit)
                {
                    // clock faces take scene lighting (ambient + lights) instead of glowing
                    InputData inp = (InputData)0;
                    inp.positionWS = i.positionWS; inp.positionCS = i.positionCS; inp.normalWS = normalize(i.normalWS);
                    inp.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                    inp.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    inp.bakedGI = BL_SampleAmbient(i.positionWS, inp.normalWS); inp.shadowMask = 1;
                    SurfaceData s = (SurfaceData)0; s.albedo = col * i.color.rgb; s.smoothness = 0.55; s.occlusion = 1; s.alpha = 1; s.normalTS = half3(0,0,1);
                    half3 c = UniversalFragmentPBR(inp, s).rgb;
                    return half4(MixFog(c, i.fog), 1);
                }
                col *= _Color.rgb * i.color.rgb * _Intensity * power;
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
