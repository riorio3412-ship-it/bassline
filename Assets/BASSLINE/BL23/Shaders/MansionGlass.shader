// Backlit window glass: moonlit leaded panes or stained glass (voronoi cells + lead came + moon/eye/rose motif).
// UV 0..1 over the window rectangle. Vertex color = glass color A, TEXCOORD2.rgb = color B, TEXCOORD2.w = motif.
Shader "BL23/MansionGlass"
{
    Properties
    {
        [HDR] _MoonColor("Moon light", Color) = (0.55, 0.7, 1.0, 1)
        _Intensity("Intensity", Float) = 2.5
        _ColorC("Third glass color", Color) = (0.62, 0.4, 0.14, 1)
        _ColorD("Fourth glass color", Color) = (0.14, 0.3, 0.32, 1)
        _Cells("Cell density", Float) = 7
        _Panes("Pane grid (x,y)", Vector) = (2, 4, 0, 0)
        _Aspect("Window aspect (w/h)", Float) = 0.45
        [KeywordEnum(Window, Stained)] _Kind("Kind", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Glass"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _KIND_WINDOW _KIND_STAINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MansionCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MoonColor; half _Intensity; half4 _ColorC; half4 _ColorD; float _Cells; float4 _Panes; float _Aspect;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 custom : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; float4 custom : TEXCOORD2; float3 positionWS : TEXCOORD3; half fog : TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V Vert(A v)
            {
                V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv; o.color = v.color; o.custom = v.custom;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float2 Hash22(float2 p)
            {
                float3 a = frac(float3(p.xyx) * float3(123.34, 234.34, 345.65));
                a += dot(a, a + 34.45);
                return frac(float2(a.x * a.y, a.y * a.z));
            }

            // returns (F1, F2-F1, cell id hash)
            float3 Voronoi(float2 p)
            {
                float2 ip = floor(p), fp = frac(p);
                float f1 = 8, f2 = 8; float id = 0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = Hash22(ip + g);
                    float2 r = g + o - fp;
                    float d = dot(r, r);
                    if (d < f1) { f2 = f1; f1 = d; id = BL_Hash21(ip + g); }
                    else if (d < f2) f2 = d;
                }
                return float3(sqrt(f1), sqrt(f2) - sqrt(f1), id);
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 uv = i.uv;
                float2 auv = float2(uv.x * _Aspect, uv.y);
                half dark = lerp(1.0, 0.3, _BL_Globals.x);
                half3 c;
                #if defined(_KIND_WINDOW)
                    // night sky gradient + blurred moon glow + leaded panes
                    half day = _BL_Day.x, dusk = _BL_Day.y;
                    half3 sky = lerp(half3(0.02, 0.03, 0.08), half3(0.10, 0.16, 0.32), uv.y);
                    float2 mc = float2(0.62 * _Aspect, 0.78);
                    float md = length(auv - mc);
                    sky += _MoonColor.rgb * (exp(-md * 9.0) * 0.9 + exp(-md * 2.5) * 0.25) * (1 - day);
                    // by day: a pale overcast sky, amber low down at dusk
                    half3 skyDay = lerp(half3(0.36, 0.39, 0.4), half3(0.27, 0.32, 0.39), uv.y);
                    skyDay = lerp(skyDay, half3(0.85, 0.55, 0.36) * (1.1 - uv.y * 0.5), dusk * 0.75);
                    sky = lerp(sky, skyDay, day);
                    // the garden outside: dark foliage and branches against the lower glass
                    float2 wp = float2(i.positionWS.x + i.positionWS.z, i.positionWS.y);
                    half fol = smoothstep(0.42, 0.62, BL_Fbm(wp * 0.55 + 7.3)) * smoothstep(0.62, 0.08, uv.y);
                    fol = max(fol, smoothstep(0.62, 0.8, BL_Fbm(wp * 1.7 + 2.1)) * 0.6 * smoothstep(0.95, 0.4, uv.y));
                    sky = lerp(sky, sky * half3(0.22, 0.27, 0.22), fol * 0.85);
                    // each old pane its own faint tint; rain runs; condensation fogging the lower glass by day
                    half ph = BL_Hash21(floor(wp * 0.8));
                    sky *= lerp(half3(1, 1, 1), half3(0.9, 1.0, 0.92), ph * 0.8);
                    half run = smoothstep(0.9, 1.0, BL_ValueNoise(float2(uv.x * 55.0, uv.y * 1.5 + ph * 7.0)));
                    sky *= 1.0 - run * 0.35;
                    sky = lerp(sky, half3(0.5, 0.52, 0.52) * (0.4 + 0.6 * day), smoothstep(0.3, 0.0, uv.y) * 0.35 * day);
                    // old crown glass: faint ripples and a film of grime toward the sash
                    float streak = BL_ValueNoise(float2(uv.x * 40.0, uv.y * 3.0 - _Time.y * 0.15));
                    sky *= 0.85 + 0.3 * streak;
                    float2 pg = frac(uv * _Panes.xy);
                    float2 pe = abs(pg - 0.5);
                    half grime = saturate(BL_Fbm(auv * 11.0) * 0.6 + smoothstep(0.3, 0.5, max(pe.x, pe.y)) * 0.5);
                    sky *= lerp(1.0, 0.62, grime);
                    float lead = step(0.47, pe.x) + step(0.47, pe.y);
                    // diamond quarries on the lower part
                    float2 dq = frac(float2(auv.x * 7.0 + auv.y * 7.0, auv.x * 7.0 - auv.y * 7.0));
                    lead += step(0.94, max(dq.x, dq.y)) * step(uv.y, 0.72) * 0.6;
                    c = sky * lerp(_Intensity, 1.1, day) * (1.0 - saturate(lead) * 0.92) * i.color.rgb;
                #else
                    float3 vr = Voronoi(auv * _Cells);
                    half3 ca = i.color.rgb, cb = i.custom.rgb;
                    half3 cells[4] = { ca, cb, _ColorC.rgb, _ColorD.rgb };
                    int k = (int)floor(vr.z * 3.999);
                    half3 glass = cells[k] * (0.55 + 0.6 * frac(vr.z * 17.0));
                    // motif: moon disc, crescent eye or rose in the upper centre
                    float motif = i.custom.w;
                    float2 mc = float2(0.5 * _Aspect, 0.66);
                    float2 d = auv - mc;
                    float r = length(d);
                    float mr = 0.19 * _Aspect * 2.0;
                    half lead = smoothstep(0.035, 0.015, vr.y);
                    if (motif < 0.5)
                    {
                        // full moon with craters
                        half inMoon = smoothstep(mr, mr - 0.01, r);
                        half crater = BL_Fbm(d * 18.0);
                        glass = lerp(glass, half3(0.82, 0.72, 0.5) * (0.7 + 0.3 * crater), inMoon);   // a honey-amber moon, not a white disc
                        lead = lerp(lead, 0, inMoon);
                        lead = max(lead, smoothstep(0.012, 0.0, abs(r - mr)) );
                        lead = max(lead, smoothstep(0.01, 0.0, abs(r - mr * 1.45)) );
                    }
                    else if (motif < 1.5)
                    {
                        // eye
                        float ex = abs(d.x) / (mr * 1.6);
                        float lid = mr * 0.62 * (1 - ex * ex);
                        half inEye = step(ex, 1) * step(abs(d.y), lid);
                        half iris = smoothstep(mr * 0.42, mr * 0.38, r);
                        half pupil = smoothstep(mr * 0.18, mr * 0.14, r);
                        half3 eyeCol = lerp(half3(0.62, 0.56, 0.44), cb * 1.1, iris);   // an ivory, smoky sclera: no bright white hoop
                        eyeCol = lerp(eyeCol, half3(0.02, 0.0, 0.02), pupil);
                        glass = lerp(glass, eyeCol, inEye);
                        lead = lerp(lead, 0, inEye);
                        lead = max(lead, step(ex, 1) * smoothstep(0.012, 0.0, abs(abs(d.y) - lid)));
                    }
                    else
                    {
                        // rose window: radial petals
                        float ang = atan2(d.y, d.x);
                        float petals = abs(cos(ang * 6.0));
                        half inRose = smoothstep(mr * 1.4, mr * 1.38, r);
                        half3 roseCol = lerp(ca, cb, petals) * (0.7 + 0.5 * petals);
                        glass = lerp(glass, roseCol, inRose);
                        half rl = smoothstep(0.02, 0.0, abs(frac(ang / 6.28318 * 12.0) - 0.5) * r * 2.0);
                        lead = lerp(lead, max(rl * step(mr * 0.3, r), smoothstep(0.012, 0.0, abs(r - mr * 0.3))), inRose);
                        lead = max(lead, smoothstep(0.012, 0.0, abs(r - mr * 1.39)));
                    }
                    // border band
                    float border = step(uv.x, 0.04) + step(0.96, uv.x) + step(uv.y, 0.025);
                    glass = lerp(glass, cb * 0.8, saturate(border));
                    // antique pot-metal glass: muted, uneven (seedy streaks and thicker/thinner pours per cell),
                    // darkened by a century of soot along the leads and the bottom of the light
                    glass = lerp(dot(glass, half3(0.3, 0.55, 0.15)).xxx, glass, 0.82) * 0.8;   // deep jewel pot-metal, darkened
                    glass *= 0.72 + 0.4 * BL_ValueNoise(float2(auv.x * 70.0, auv.y * 9.0) + vr.z * 31.0);
                    half soot = saturate(smoothstep(0.09, 0.0, vr.y) * 0.55 + BL_Fbm(auv * 7.0 + 3.1) * 0.45 + (1.0 - uv.y) * 0.25);
                    glass *= lerp(1.0, 0.45, soot);
                    // lead cames: wider, slightly irregular, a dull grey rather than a black line
                    half leadW = lead;
                    leadW = max(leadW, smoothstep(0.05, 0.03, vr.y + (BL_ValueNoise(auv * 40.0) - 0.5) * 0.012));
                    half3 leadCol = half3(0.035, 0.034, 0.032);
                    half light = (0.75 + 0.35 * uv.y) * lerp(1.0, 1.2, _BL_Day.x);
                    c = lerp(glass * _Intensity * light, leadCol, leadW * 0.96);
                #endif
                c = BL_SoftClamp(c, 1.6);
                c *= dark;
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
