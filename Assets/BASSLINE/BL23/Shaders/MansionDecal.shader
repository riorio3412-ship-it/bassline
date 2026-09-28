// Lit alpha-blended decal quads (blood, drips, footprints, scratches, cracks, soil, ash, paint drips).
// Atlas cell selected by the mesh UVs. Vertex color = tint (alpha = opacity), TEXCOORD2: x = wet/gloss, y = emissive,
// z = bump strength. Polygon offset + builder normal offset avoid z-fighting.
Shader "BL23/MansionDecal"
{
    Properties
    {
        _MainTex("Atlas (A = shape, R = height)", 2D) = "white" {}
        _Gloss("Gloss", Range(0,1)) = 0.5
        _Bump("Bump", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -2, -2
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "MansionCommon.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half _Gloss, _Bump;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float4 custom : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; half4 tangentWS : TEXCOORD3; half4 color : TEXCOORD4; float4 custom : TEXCOORD5; half fog : TEXCOORD6; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V Vert(A v)
            {
                V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = n.normalWS; o.tangentWS = half4(n.tangentWS, v.tangentOS.w);
                o.uv = v.uv; o.color = v.color; o.custom = v.custom; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half a = t.a * i.color.a;
                clip(a - 0.01);
                // fake bump from the height channel
                float2 du = float2(_MainTex_TexelSize.x * 2, 0), dv = float2(0, _MainTex_TexelSize.y * 2);
                half hx = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + du).r - SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - du).r;
                half hy = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + dv).r - SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - dv).r;
                half bump = _Bump * (i.custom.z > 0 ? i.custom.z : 1);
                half3 nTS = normalize(half3(-hx * bump, -hy * bump, 1));
                float3 N = normalize(i.normalWS), T = normalize(i.tangentWS.xyz), B = cross(N, T) * (i.tangentWS.w > 0 ? 1 : -1);
                float3 nWS = normalize(nTS.x * T + nTS.y * B + nTS.z * N);
                InputData inp = (InputData)0;
                inp.positionWS = i.positionWS; inp.positionCS = i.positionCS; inp.normalWS = nWS;
                inp.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inp.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inp.bakedGI = BL_SampleAmbient(i.positionWS, nWS);
                inp.shadowMask = 1;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inp.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                inp.fogCoord = i.fog;
                SurfaceData s = (SurfaceData)0;
                half shade = lerp(0.55, 1.0, t.r);
                s.albedo = i.color.rgb * shade;
                s.smoothness = saturate(_Gloss * 0.3 + i.custom.x);
                s.metallic = 0; s.occlusion = 1; s.alpha = a; s.normalTS = nTS;
                s.emission = i.color.rgb * i.custom.y;
                half4 c = UniversalFragmentPBR(inp, s);
                c.rgb = MixFog(c.rgb, i.fog);
                return half4(c.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
