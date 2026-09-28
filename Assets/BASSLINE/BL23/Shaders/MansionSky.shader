// Dream night sky painted by view direction (courtyard ceiling, greenhouse glass, oculus). Looks infinitely far.
Shader "BL23/MansionSky"
{
    Properties
    {
        [HDR] _Top("Zenith", Color) = (0.03, 0.03, 0.10, 1)
        [HDR] _Horizon("Horizon", Color) = (0.18, 0.08, 0.28, 1)
        [HDR] _MoonColor("Moon", Color) = (1.4, 1.35, 1.2, 1)
        _MoonDir("Moon direction", Vector) = (0.3, 0.8, 0.5, 0)
        _MoonSize("Moon size", Range(0.01, 0.3)) = 0.09
        [HDR] _Aurora("Aurora tint", Color) = (1.0, 0.2, 0.7, 1)
        [HDR] _Aurora2("Aurora tint 2", Color) = (0.1, 0.9, 1.0, 1)
        _Stars("Star density", Range(0, 1)) = 0.6
        _Clouds("Clouds", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MansionCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon, _MoonColor, _Aurora, _Aurora2; float4 _MoonDir; half _MoonSize, _Stars, _Clouds;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 color : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V Vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.color = v.color; return o; }
            half4 Frag(V i) : SV_Target
            {
                float3 dir = normalize(i.positionWS - _WorldSpaceCameraPos);
                float up = saturate(dir.y);
                half3 col = lerp(_Horizon.rgb, _Top.rgb, pow(up, 0.6));
                // aurora ribbons (dreamy magenta / cyan)
                float2 p = dir.xz / max(dir.y + 0.35, 0.05);
                float band = BL_Fbm(p * 0.6 + float2(_Time.y * 0.01, 0));
                float rib = smoothstep(0.55, 0.75, band) * smoothstep(0.95, 0.6, band);
                col += lerp(_Aurora.rgb, _Aurora2.rgb, BL_ValueNoise(p * 0.3)) * rib * 0.35 * (1 - up * 0.5);
                // stars
                float3 sd = dir * 140.0;
                float3 cell = floor(sd);
                float s = BL_Hash31(cell);
                float3 off = float3(BL_Hash31(cell + 11.3), BL_Hash31(cell + 27.1), BL_Hash31(cell + 43.7)) - 0.5;
                float sdist = length(frac(sd) - 0.5 - off * 0.6);
                float tw = 0.6 + 0.4 * sin(_Time.y * (2.0 + s * 5.0) + s * 40.0);
                col += step(1.0 - 0.03 * _Stars, s) * smoothstep(0.16, 0.0, sdist) * tw * 2.2 * up;
                // moon
                float3 md = normalize(_MoonDir.xyz);
                float mdot = dot(dir, md);
                float ang = acos(saturate(mdot));
                half disc = smoothstep(_MoonSize, _MoonSize * 0.93, ang);
                float2 mp = float2(dot(dir - md, float3(1, 0, 0)), dot(dir - md, float3(0, 1, 0)));
                half craters = 0.75 + 0.35 * BL_Fbm(mp * 60.0);
                col = lerp(col, _MoonColor.rgb * craters, disc);
                col += _MoonColor.rgb * 0.25 * exp(-ang * 7.0);
                // drifting clouds lit from the moon
                float cl = BL_Fbm(p * 0.9 + float2(_Time.y * 0.015, _Time.y * 0.004));
                half cloud = smoothstep(0.5, 0.85, cl) * _Clouds;
                half3 cloudCol = lerp(_Horizon.rgb * 0.6, _MoonColor.rgb * 0.35, exp(-ang * 2.0));
                col = lerp(col, cloudCol, cloud * (1 - disc * 0.7));
                // daytime: a low, pale overcast (no stars, no moon, no aurora), warmer toward the horizon at dusk / dawn
                half day = _BL_Day.x, dusk = _BL_Day.y;
                if (day > 0.001)
                {
                    half3 dayCol = lerp(half3(0.58, 0.58, 0.57), half3(0.34, 0.4, 0.5), pow(up, 0.7));
                    dayCol = lerp(dayCol, half3(1.0, 0.64, 0.4) * (1.1 - up * 0.6), dusk * (1 - up) * 0.85);
                    float dc = BL_Fbm(p * 0.7 + float2(_Time.y * 0.01, _Time.y * 0.003));
                    dayCol *= 0.86 + 0.22 * smoothstep(0.35, 0.8, dc);   // heavy cloud banks
                    col = lerp(col, dayCol, day);
                }
                // a glass roof (vertex alpha 0.5): old panes filmed with grime, streaked by rain, with fallen leaves and the
                // silhouettes of branches lying on the glass
                if (i.color.a < 0.75)
                {
                    float2 wp = i.positionWS.xz;
                    half grime = BL_Fbm(wp * 0.9 + 4.7) * 0.55 + BL_ValueNoise(float2(wp.x * 7.0, wp.y * 0.6)) * 0.25;
                    col *= lerp(1.0, 0.55, saturate(grime)) * half3(0.92, 0.97, 0.9);
                    half leaves = smoothstep(0.62, 0.7, BL_Fbm(wp * 2.3 + 1.3));
                    half branch = smoothstep(0.035, 0.0, abs(BL_Fbm(wp * 0.35 + 9.1) - 0.5)) * smoothstep(0.35, 0.6, BL_Fbm(wp * 0.8 + 2.0));
                    col = lerp(col, half3(0.045, 0.05, 0.035), saturate(leaves * 0.9 + branch * 0.85));
                }
                col *= lerp(1.0, 0.35, _BL_Globals.x);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
