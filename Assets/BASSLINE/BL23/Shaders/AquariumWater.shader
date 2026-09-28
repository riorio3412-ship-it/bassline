Shader "BL23/AquariumWater"
{
    Properties
    {
        _Shallow ("Shallow Color", Color) = (0.45,0.95,0.95,0.35)
        _Deep ("Deep Color", Color) = (0.05,0.45,0.55,0.6)
        _Glow ("Glow", Range(0,3)) = 0.9
        _WaveAmp ("Wave Amplitude", Range(0,0.02)) = 0.006
        _Calm ("Calm (0 moving .. 1 frozen)", Range(0,1)) = 0
        _BreakAmount ("BREAK", Range(0,1)) = 0
        _Height ("Water Local Height", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Shallow;
            half4 _Deep;
            half _Glow;
            half _WaveAmp;
            half _Calm;
            half _BreakAmount;
            float _Height;
        CBUFFER_END

        struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

        float Waves(float2 p, float t)
        {
            return sin(p.x * 55.0 + t * 2.3) * 0.5 + sin(p.y * 47.0 - t * 1.9) * 0.35 + sin((p.x + p.y) * 83.0 + t * 3.1) * 0.15;
        }

        V vert(A v)
        {
            V o = (V)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 p = v.positionOS.xyz;
            float t = _Time.y * (1.0 - _Calm);
            // only the top surface vertices move
            float top = step(_Height * 0.5 - 0.0005, p.y);
            p.y += top * Waves(p.xz, t) * _WaveAmp;
            if (_BreakAmount > 0.001) p.y += top * sin(_Time.y * 31.0 + p.x * 90.0) * 0.01 * _BreakAmount;
            VertexPositionInputs vp = GetVertexPositionInputs(p);
            o.positionCS = vp.positionCS;
            o.positionWS = vp.positionWS;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionOS = p;
            return o;
        }

        half4 frag(V i, half facing : VFACE) : SV_Target
        {
            half3 N = normalize(i.normalWS) * (facing > 0 ? 1.0h : -1.0h);
            half3 Vd = GetWorldSpaceNormalizeViewDir(i.positionWS);
            half ndv = saturate(dot(N, Vd));
            float t = _Time.y * (1.0 - _Calm);
            float depth01 = saturate((_Height * 0.5 - i.positionOS.y) / max(_Height, 1e-3));
            half3 col = lerp(_Shallow.rgb, _Deep.rgb, depth01);
            half a = lerp(_Shallow.a, _Deep.a, depth01);
            // caustic web
            float2 q = i.positionOS.xz * 40.0 + i.positionOS.y * 12.0;
            float c1 = abs(sin(q.x + t * 1.3 + sin(q.y * 0.7 + t)) * sin(q.y + t * 1.1 + sin(q.x * 0.8 - t * 0.7)));
            half caustic = smoothstep(0.75h, 0.95h, 1.0h - c1);
            // surface line at the top
            half surf = smoothstep(_Height * 0.5 - 0.012, _Height * 0.5 - 0.002, i.positionOS.y);
            Light ml = GetMainLight();
            half3 amb = SampleSH(half3(0, 1, 0));
            half3 lightCol = ml.color * 0.3h + amb + 0.25h;
            col = col * lightCol + (caustic * 0.35h + surf * 0.6h) * half3(0.8, 1, 1) * lightCol;
            col += _Shallow.rgb * _Glow * (0.35h + 0.65h * (1.0h - depth01)) * 0.5h;
            half fres = pow(1.0h - ndv, 2.0h);
            a = saturate(a + fres * 0.25h + surf * 0.3h);
            if (facing < 0) { col *= 0.7h; a *= 0.8h; }
            if (_BreakAmount > 0.001) col = lerp(col, half3(0.9, 0.05, 0.25), _BreakAmount * 0.6h * (0.5h + 0.5h * sin(_Time.y * 9.0)));
            return half4(col, a);
        }
        ENDHLSL

        Pass
        {
            Name "WaterBack"
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
            Name "WaterFront"
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
