// Emissive helper shader: solid glowing parts (bulbs, neon, lit windows), additive light shafts / halos,
// and GPU-billboarded candle flames. TEXCOORD2: x = intensity, y = flicker, z = size/extra, w = power group
// (-1 always, -2 fire, 0..7 electric circuit).
Shader "BL23/MansionGlow"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (1,1,1,1)
        _MainTex("Mask", 2D) = "white" {}
        _Intensity("Intensity", Float) = 1
        _Soft("Soft edge", Range(0.01, 4)) = 1
        _DepthFade("Depth fade (m)", Float) = 0.5
        _Group("Power group (-1 always, -2 fire, 0..7 circuit)", Float) = -1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [KeywordEnum(Solid, Add, Flame, Ray, Halo)] _Mode("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _MODE_SOLID _MODE_ADD _MODE_FLAME _MODE_RAY _MODE_HALO
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "MansionCommon.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float4 _MainTex_ST; half _Intensity, _Soft, _DepthFade, _Group;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 custom : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; float4 custom : TEXCOORD2;
                float3 positionWS : TEXCOORD3; half3 normalWS : TEXCOORD4; float4 screenPos : TEXCOORD5; half fog : TEXCOORD6; float seed : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            V Vert(A v)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                o.seed = BL_Hash31(floor(pw * 7.1));
                #if defined(_MODE_FLAME)
                    // all four verts sit at the wick; uv holds the corner (-1..1, 0..1), custom.z = height
                    float h = max(v.custom.z, 0.02);
                    float3 camR = normalize(float3(UNITY_MATRIX_V[0].x, 0, UNITY_MATRIX_V[0].z));
                    float t = _Time.y * (7.0 + o.seed * 5.0);
                    float flick = 1.0 + (sin(t) * 0.5 + sin(t * 2.3 + 1.7) * 0.3) * 0.12 * (0.5 + v.custom.y);
                    float sway = sin(t * 0.37 + o.seed * 6.0) * 0.18;
                    float3 up = normalize(float3(sway * 0.3, 1, sway * 0.2));
                    // quad is larger than the flame: the halo glow lives around the body
                    pw += camR * (v.uv.x * h * 0.75) + up * ((v.uv.y * 1.7 - 0.35) * h * flick);
                #endif
                #if defined(_MODE_HALO)
                    // camera-facing billboard: all four verts sit at the centre, uv carries the corner, custom.z the size
                    float hs = max(v.custom.z, 0.05);
                    float3 bR = UNITY_MATRIX_V[0].xyz, bU = UNITY_MATRIX_V[1].xyz;
                    pw += (bR * (v.uv.x - 0.5) + bU * (v.uv.y - 0.5)) * hs;
                    pw += normalize(_WorldSpaceCameraPos - pw) * hs * 0.35;
                #endif
                o.positionWS = pw;
                o.positionCS = TransformWorldToHClip(pw);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                o.custom = v.custom;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // custom.w stores (group + 10); 0 = unset -> material group
                float group = i.custom.w > 0.5 ? i.custom.w - 10.0 : _Group;
                half power = BL_CircuitPower(group);
                half inten = _Intensity * (i.custom.x > 0 ? i.custom.x : 1.0) * power;
                half3 col = _Color.rgb * i.color.rgb;

                // flicker (electric buzz or candle)
                half fl = i.custom.y;
                if (fl > 0)
                {
                    float t = _Time.y;
                    half n = BL_ValueNoise(float2(t * 9.0 + i.seed * 91.0, i.seed * 13.0));
                    half buzz = step(0.93, BL_ValueNoise(float2(t * 3.1, i.seed * 7.0))) * step(0.5, BL_Hash21(float2(floor(t * 20.0), i.seed)));
                    inten *= lerp(1.0, 0.7 + 0.5 * n, saturate(fl)) * (1.0 - buzz * saturate(fl - 0.5) * 1.6);
                }
                // noise chapter: electric light stutters
                if (group > -0.5) inten *= 1.0 - _BL_Globals.y * 0.6 * step(0.7, BL_Hash21(float2(floor(_Time.y * 24.0), i.seed * 3.0)));

                #if defined(_MODE_SOLID)
                    half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw).rgb;
                    half3 c = col * tex * inten;
                    // lamp glass, bulbs, lit dials: a touch less saturated, and a soft shoulder so bloom stays a halo
                    c = lerp(dot(c, half3(0.2126, 0.7152, 0.0722)).xxx, c, 0.8);
                    c = BL_SoftClamp(c, 2.2);
                    c = MixFog(c, i.fog);
                    return half4(c, 1);
                #elif defined(_MODE_FLAME)
                    // flame space in units of the flame height: x across, y up from the wick
                    float px = i.uv.x * 0.75, py = i.uv.y * 1.7 - 0.35;
                    float w = 0.34 * (1.0 - saturate(py)) * sqrt(saturate(py * 3.0));
                    float d = abs(px) / max(w, 1e-3);
                    half inside = step(0.0, py) * step(py, 1.0);
                    half core = saturate(1.0 - d) * saturate(1.0 - py * 0.9) * inside;
                    half3 hot = lerp(half3(1.0, 0.32, 0.04), half3(1.0, 0.86, 0.6), smoothstep(0.15, 0.85, core));
                    hot = lerp(hot, half3(0.25, 0.35, 1.0), smoothstep(0.22, 0.0, py) * 0.6 * inside); // blue root
                    half a = smoothstep(0.0, 0.3, core);
                    // soft warm halo around the flame
                    float gl = exp(-length(float2(px, (py - 0.4) * 0.8)) * 5.0);
                    half3 c = (hot * a * 2.4 + half3(1.0, 0.45, 0.12) * gl * 0.55) * col * inten;
                    return half4(c, 0);
                #else
                    // additive shafts / halos with soft intersection
                    float2 suv = i.screenPos.xy / i.screenPos.w;
                    float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float myZ = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                    half soft = saturate((sceneZ - myZ) / max(_DepthFade, 1e-3));
                    half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                    half3 n = normalize(i.normalWS);
                    half a = 1;
                    #if defined(_MODE_RAY)
                        // moon shafts by night, pale daylight shafts by day
                        col = lerp(col, dot(col, half3(0.33, 0.33, 0.34)) * half3(1.3, 1.24, 1.1) * 1.25, _BL_Day.x);
                        half facing = abs(dot(n, v));
                        a = pow(facing, _Soft) * saturate(1.0 - i.uv.y) * smoothstep(0.0, 0.08, i.uv.y);
                        // drifting dust in the beam
                        a *= 0.65 + 0.35 * BL_ValueNoise(i.positionWS.xz * 3.0 + i.positionWS.y * 2.0 + _Time.y * 0.3);
                        half camNear = saturate(myZ / 1.5);
                        a *= camNear;
                    #elif defined(_MODE_HALO)
                        float2 q = i.uv * 2 - 1;
                        a = saturate(1 - length(q));
                        a = pow(a, _Soft * 2.0);
                    #else
                        half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw).rgb;
                        col *= tex;
                    #endif
                    half3 c = col * inten * a * soft * i.color.a;
                    c *= 1 - saturate(i.fog * 0);
                    return half4(c, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
