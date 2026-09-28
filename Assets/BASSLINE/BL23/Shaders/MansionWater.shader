// Water surfaces: pool, mirror room, fountains, aquarium. Refraction via the opaque texture, depth absorption,
// planar reflection (when the reflection plane matches) else environment reflection, lit highlights (Forward+).
// _CAUSTICS variant draws additive animated caustics on pool floors / walls.
Shader "BL23/MansionWater"
{
    Properties
    {
        _Shallow("Shallow color", Color) = (0.2, 0.75, 0.8, 1)
        _Deep("Deep color", Color) = (0.02, 0.12, 0.2, 1)
        _Absorb("Absorption per m", Float) = 1.2
        _Waves("Wave strength", Range(0, 1)) = 0.35
        _WaveScale("Wave scale", Float) = 1.3
        _Speed("Wave speed", Float) = 0.4
        _Refract("Refraction", Range(0, 0.2)) = 0.04
        _Reflect("Reflection", Range(0, 2)) = 1
        _Mirror("Mirror bias (shallow mirror room)", Range(0, 1)) = 0
        _Smooth("Highlight smoothness", Range(0, 1)) = 0.9
        _EnvRough("Environment blur", Range(0, 1)) = 0.08
        [HDR] _Caustic("Caustic color", Color) = (0.6, 1.2, 1.3, 1)
        [Toggle(_CAUSTICS)] _UseCaustics("Caustics (additive)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Back
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _CAUSTICS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "MansionCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep; half _Absorb, _Waves, _WaveScale, _Speed, _Refract, _Reflect, _Mirror, _Smooth, _EnvRough; half4 _Caustic;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float4 screenPos : TEXCOORD2; half fog : TEXCOORD3; half4 color : TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V Vert(A v)
            {
                V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.color = v.color;
                return o;
            }

            float H(float2 p)
            {
                float t = _Time.y * _Speed;
                return BL_ValueNoise(p * _WaveScale + float2(t, t * 0.7)) * 0.6 + BL_ValueNoise(p * _WaveScale * 2.3 - float2(t * 1.3, -t * 0.4)) * 0.4;
            }

            float Caustic(float2 p)
            {
                float t = _Time.y * 0.6;
                float c = 0;
                for (int k = 0; k < 2; k++)
                {
                    float2 q = p * (1.7 + k * 1.3) + float2(t * (0.4 + k * 0.3), -t * 0.35);
                    float2 ip = floor(q), fp = frac(q);
                    float f1 = 8, f2 = 8;
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = float2(BL_Hash21(ip + g), BL_Hash21(ip + g + 19.1));
                        o = 0.5 + 0.45 * sin(t * 1.3 + 6.28 * o);
                        float d = length(g + o - fp);
                        if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                    }
                    c += pow(saturate(1.0 - (f2 - f1) * 3.0), 6.0);
                }
                return c;
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(_CAUSTICS)
                    half cc = Caustic(i.positionWS.xz + i.positionWS.y * 0.3);
                    return half4(_Caustic.rgb * cc * i.color.a * lerp(1, 0.2, _BL_Globals.x), 0);
                #else
                    float e = 0.05;
                    float2 p = i.positionWS.xz;
                    float h0 = H(p), hx = H(p + float2(e, 0)), hz = H(p + float2(0, e));
                    float3 n = normalize(float3(-(hx - h0) / e * _Waves * 0.2, 1, -(hz - h0) / e * _Waves * 0.2));
                    float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                    float2 suv = i.screenPos.xy / i.screenPos.w;
                    float2 ruv = suv + n.xz * _Refract;
                    float sceneZ = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                    float myZ = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                    if (sceneZ < myZ) { ruv = suv; sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams); }
                    float thick = max(sceneZ - myZ, 0);
                    half3 under = SampleSceneColor(ruv);
                    half trans = exp(-thick * _Absorb);
                    half3 body = lerp(_Deep.rgb, _Shallow.rgb, trans);
                    half3 refr = lerp(body * 0.35, under * _Shallow.rgb * 1.1, trans);

                    half nv = saturate(dot(n, v));
                    half fres = lerp(0.03, 1.0, pow(1 - nv, 5));
                    fres = lerp(fres, 0.85, _Mirror);

                    // reflection: planar if this surface is the current plane, else environment probe
                    half3 refl;
                    float planeOk = _BL_PlanarParams.y * step(abs(i.positionWS.y - _BL_PlanarParams.x), 0.2);
                    if (planeOk > 0.5)
                        refl = SAMPLE_TEXTURE2D_LOD(_BL_PlanarTex, sampler_BL_PlanarTex, float2(1.0 - suv.x, suv.y) + n.xz * _BL_PlanarParams.w * 2.0, 0).rgb;
                    else
                        refl = GlossyEnvironmentReflection(reflect(-v, n), i.positionWS, _EnvRough, 1.0, suv);

                    // light highlights
                    InputData inp = (InputData)0;
                    inp.positionWS = i.positionWS; inp.positionCS = i.positionCS; inp.normalWS = n; inp.viewDirectionWS = v;
                    inp.normalizedScreenSpaceUV = suv; inp.shadowMask = 1; inp.bakedGI = 0;
                    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                        inp.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                    #endif
                    SurfaceData s = (SurfaceData)0;
                    s.albedo = 0; s.specular = 0.02; s.metallic = 0; s.smoothness = _Smooth; s.occlusion = 1; s.alpha = 1; s.normalTS = half3(0, 0, 1);
                    half3 spec = UniversalFragmentPBR(inp, s).rgb;
                    spec = BL_SoftClamp(spec, 0.8);   // lamp glints on water / glass stay soft: no sparkle, no bloom spikes
                    // a whisper of the cloudy daylight sky on open water by day
                    refl += half3(0.05, 0.055, 0.06) * _BL_Day.x * (1 - _Mirror);
                    half3 col = lerp(refr, refl * _Reflect, fres) + spec;
                    col = BL_SoftClamp(col, 2.0);
                    col = MixFog(col, i.fog);
                    return half4(col, 1);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
