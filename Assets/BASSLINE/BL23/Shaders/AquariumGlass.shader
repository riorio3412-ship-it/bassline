Shader "BL23/AquariumGlass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.75,0.95,1,0.08)
        _EdgeColor ("Edge Color", Color) = (0.85,1,1,1)
        _Fresnel ("Fresnel", Range(0,2)) = 0.9
        _Streak ("Reflection Streak", Range(0,2)) = 0.6
        _BreakAmount ("BREAK", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half4 _EdgeColor;
            half _Fresnel;
            half _Streak;
            half _BreakAmount;
        CBUFFER_END

        struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half3 normalVS : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

        V vert(A v)
        {
            V o = (V)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = p.positionCS;
            o.positionWS = p.positionWS;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.normalVS = TransformWorldToViewDir(o.normalWS);
            o.uv = v.uv;
            return o;
        }

        half4 frag(V i, half facing : VFACE) : SV_Target
        {
            half3 N = normalize(i.normalWS) * (facing > 0 ? 1.0h : -1.0h);
            half3 Vd = GetWorldSpaceNormalizeViewDir(i.positionWS);
            half ndv = saturate(dot(N, Vd));
            half fres = pow(1.0h - ndv, 3.0h) * _Fresnel;
            // edge frame on each glass panel (uv 0..1 per face)
            float2 e = min(i.uv, 1.0 - i.uv);
            half edge = 1.0h - smoothstep(0.0h, 0.035h, min(e.x, e.y));
            // diagonal reflection streaks
            half s = frac((i.uv.x + i.uv.y * 0.6) * 1.7);
            half streak = (smoothstep(0.12h, 0.16h, s) - smoothstep(0.2h, 0.24h, s)) * 0.6h + (smoothstep(0.32h, 0.34h, s) - smoothstep(0.36h, 0.38h, s)) * 0.35h;
            streak *= _Streak * (facing > 0 ? 1.0h : 0.35h);
            Light ml = GetMainLight();
            half3 amb = SampleSH(half3(0, 1, 0));
            half3 lightCol = ml.color * 0.35h + amb * 1.2h + 0.08h;
            half3 col = _Tint.rgb * lightCol;
            half a = _Tint.a + fres * 0.45h + streak * 0.5h + edge * 0.55h;
            col += (_EdgeColor.rgb * (edge + streak) + fres * 0.6h) * lightCol;
            if (_BreakAmount > 0.001) col += half3(1, 0.1, 0.7) * _BreakAmount * step(0.5, frac(_Time.y * 5.0 + i.uv.y * 3.0)) * 0.4h;
            return half4(col, saturate(a));
        }
        ENDHLSL

        Pass
        {
            Name "GlassBack"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "GlassFront"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
