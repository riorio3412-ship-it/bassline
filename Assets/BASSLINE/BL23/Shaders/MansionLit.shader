// BL23 mansion surface shader (hand written URP HLSL, Forward+ ready).
// Vertex color = tint A, TEXCOORD2 = tint B / custom. Keywords select checker marble, wallpaper (with watching eyes),
// planar reflection, recolor for scanned fabrics, wetness.
Shader "BL23/MansionLit"
{
    Properties
    {
        _BaseMap("Base", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _Tiling("Tiling xy, offset zw", Vector) = (1,1,0,0)
        [Normal] _NormalMap("Normal", 2D) = "bump" {}
        _NormalScale("Normal Scale", Float) = 1
        _MaskMap("ARM (AO, Rough, Metal)", 2D) = "white" {}
        _Roughness("Roughness (mult)", Range(0,2)) = 0.6
        _RoughnessBias("Roughness bias", Range(-1,1)) = 0
        _Metallic("Metallic (mult)", Range(0,1)) = 0
        _MetallicBias("Metallic bias", Range(-1,1)) = 0
        _Occlusion("Occlusion strength", Range(0,1)) = 1
        _Recolor("Recolor toward tint", Range(0,1)) = 0
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)
        _EmissionMap("Emission map", 2D) = "white" {}
        _EmissionCircuit("Emission circuit (-1 none)", Float) = -1
        _CheckerSize("Checker cell (m)", Float) = 0.8
        _CheckerPhase("Checker phase (1 = all dark when huge)", Float) = 0
        _PatternMap("Pattern mask (R motif, G gilt, A grime)", 2D) = "black" {}
        _PatternTiling("Pattern tiling", Float) = 1
        _EyeDensity("Eye density", Range(0,1)) = 0
        _EyeScale("Eye cell (1/m)", Float) = 1.6
        _Grime("Grime", Range(0,1)) = 0.25
        _Wet("Wetness", Range(0,1)) = 0
        _PlanarStrength("Planar reflection", Range(0,1)) = 0
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle(_NORMALMAP)] _UseNormal("Normal map", Float) = 0
        [Toggle(_MASKMAP)] _UseMask("Mask map", Float) = 0
        [Toggle(_CHECKER)] _UseChecker("Checker", Float) = 0
        [Toggle(_WALLPAPER)] _UseWallpaper("Wallpaper", Float) = 0
        [Toggle(_PLANAR)] _UsePlanar("Planar", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha test", Float) = 0
        [Toggle(_EMISSIONMAP)] _UseEmissionMap("Emission map", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_PatternMap); SAMPLER(sampler_PatternMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _Tiling;
            half _NormalScale;
            half _Roughness, _RoughnessBias, _Metallic, _MetallicBias, _Occlusion, _Recolor;
            half4 _EmissionColor;
            half _EmissionCircuit;
            float _CheckerSize; half _CheckerPhase;
            float _PatternTiling;
            half _EyeDensity;
            float _EyeScale;
            half _Grime, _Wet, _PlanarStrength, _Cutoff;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _MASKMAP
            #pragma shader_feature_local_fragment _CHECKER
            #pragma shader_feature_local _WALLPAPER
            #pragma shader_feature_local_fragment _PLANAR
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSIONMAP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "MansionCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 custom : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : TEXCOORD4;
                float4 custom : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.tangentWS = half4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv * _Tiling.xy + _Tiling.zw;
                o.color = v.color;
                o.custom = v.custom;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // Almond eye with an iris that follows the viewer. Returns rgb + mask.
            half4 WatchingEye(float2 uvw, half3 viewTS, float3 baseCol, float3 inkCol)
            {
                float2 cell = floor(uvw);
                float h = BL_Hash21(cell + 7.13);
                if (h > _EyeDensity) return 0;
                float2 q = frac(uvw) - 0.5;
                q.y *= 1.35;
                float ex = abs(q.x) / 0.36;
                if (ex >= 1) return 0;
                float lid = 0.2 * (1 - ex * ex);
                // blink: each eye closes briefly at its own rhythm
                float t = _Time.y * (0.25 + h * 0.35) + h * 40.0;
                float blink = smoothstep(0.0, 0.06, abs(frac(t) - 0.5) - 0.44);
                lid *= lerp(0.08, 1.0, blink);
                float inside = step(abs(q.y), lid);
                if (inside < 0.5)
                {
                    float rim = step(abs(q.y), lid + 0.025);
                    return half4(inkCol * 0.25, rim * 0.9);
                }
                float2 look = clamp(viewTS.xy * 0.16, -0.12, 0.12);
                float2 d = q - look;
                float r = length(d);
                float3 sclera = float3(0.86, 0.80, 0.72) * (0.75 + 0.25 * (1 - abs(q.y) / max(lid, 1e-3)));
                float3 iris = lerp(inkCol * 1.8, float3(1.0, 0.25, 0.55), 0.35) * (0.6 + 0.4 * sin(atan2(d.y, d.x) * 14.0) * 0.5 + 0.2);
                float3 col = sclera;
                col = lerp(col, iris, smoothstep(0.105, 0.095, r));
                col = lerp(col, float3(0.02, 0.01, 0.02), smoothstep(0.045, 0.035, r));
                col += smoothstep(0.025, 0.0, length(d - float2(-0.03, 0.03))) * 0.8;
                return half4(col, 1);
            }

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 uv = i.uv;
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                #if defined(_ALPHATEST_ON)
                    clip(tex.a * _BaseColor.a - _Cutoff);
                #endif
                half3 tintA = i.color.rgb;
                half3 albedo = tex.rgb * _BaseColor.rgb;
                albedo = max(albedo, 0.035);   // nothing is blacker than old black lacquer: a floor keeps every surface readable

                // recolor scanned textures toward the palette tint (fabric / upholstery)
                half lum = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                albedo = lerp(albedo * tintA, (lum * 1.55 + 0.06) * tintA * _BaseColor.rgb, _Recolor);

                half3 normalTS = half3(0, 0, 1);
                #if defined(_NORMALMAP)
                    normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
                #endif

                half ao = 1, rough = _Roughness + _RoughnessBias, metal = _Metallic + _MetallicBias;
                #if defined(_MASKMAP)
                    half3 arm = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv).rgb;
                    ao = lerp(1, arm.r, _Occlusion);
                    rough = arm.g * _Roughness + _RoughnessBias;
                    metal = arm.b * _Metallic + _MetallicBias;
                #endif

                // robust frame: scanned meshes can carry zero / degenerate normals or tangents; normalising a zero vector is
                // NaN, and a NaN pixel either blooms white or (with stopNaN) turns the whole piece black
                float3 nRaw = i.normalWS; float nl = dot(nRaw, nRaw);
                float3 nWS = nl > 1e-10 ? nRaw * rsqrt(nl) : float3(0, 1, 0);
                if (!isFront) nWS = -nWS;
                float3 tRaw = i.tangentWS.xyz - nWS * dot(nWS, i.tangentWS.xyz);
                float tl = dot(tRaw, tRaw);
                float3 tWS = tl > 1e-10 ? tRaw * rsqrt(tl) : normalize(abs(nWS.y) < 0.99 ? cross(float3(0, 1, 0), nWS) : cross(float3(1, 0, 0), nWS));
                float3 bWS = cross(nWS, tWS) * (i.tangentWS.w >= 0 ? 1 : -1);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

                #if defined(_CHECKER)
                {
                    // world-aligned checker; dark cells invert the marble so veins glow
                    float2 c = floor(i.positionWS.xz / _CheckerSize + 1e-3);
                    float odd = fmod(abs(c.x + c.y) + _CheckerPhase, 2.0);
                    half3 light = tex.rgb * tintA * 1.05;
                    half veins = saturate(1.15 - lum);
                    half3 darkTint = dot(i.custom.rgb, 1) > 0.001 ? i.custom.rgb : half3(0.16, 0.15, 0.18);
                    half3 dark = darkTint * (0.12 + 1.2 * veins * veins) + tex.rgb * 0.02;
                    albedo = lerp(light, dark, odd);
                    rough = lerp(rough, rough * 0.7, odd);
                    // thin grout lines
                    float2 g = abs(frac(i.positionWS.xz / _CheckerSize + 1e-3) - 0.5);
                    half grout = smoothstep(0.494, 0.4995, max(g.x, g.y));
                    albedo = lerp(albedo, albedo * 0.35, grout);
                    rough = lerp(rough, 0.9, grout);
                }
                #endif

                #if defined(_WALLPAPER)
                {
                    float2 puv = uv * _PatternTiling;
                    half4 pat = SAMPLE_TEXTURE2D(_PatternMap, sampler_PatternMap, puv);
                    half3 ink = i.custom.rgb;
                    albedo = lerp(albedo, ink * (0.8 + 0.4 * lum), pat.r * 0.78);
                    half3 gilt = ink * 1.7 + half3(0.12, 0.09, 0.03);
                    albedo = lerp(albedo, gilt, pat.g * 0.75);
                    metal = lerp(metal, 0.5, pat.g);      // old leaf gilding: a soft sheen, not a mirror
                    rough = lerp(rough, 0.46, pat.g);
                    // watching eyes hidden in the motif
                    half3 viewTS = half3(dot(viewWS, tWS), dot(viewWS, bWS), dot(viewWS, nWS));
                    float2 meters = uv / max(abs(_Tiling.xy), 1e-3);
                    half4 eye = WatchingEye(meters * _EyeScale, viewTS, albedo, ink);
                    albedo = lerp(albedo, eye.rgb, eye.a);
                    rough = lerp(rough, 0.15, eye.a * step(0.99, eye.a));
                    metal = lerp(metal, 0, eye.a);
                    // grime / water stains
                    half grime = pat.a * _Grime;
                    albedo *= 1 - grime * 0.55;
                }
                #endif

                // wetness
                half wet = _Wet;
                albedo *= lerp(1, 0.55, wet);
                rough = lerp(rough, 0.06, wet);
                normalTS = normalize(lerp(normalTS, half3(0, 0, 1), wet * 0.6));

                rough = saturate(rough);
                metal = saturate(metal);

                float3x3 tbn = float3x3(tWS, bWS, nWS);
                float3 nm = mul(normalTS, tbn); float nml = dot(nm, nm);
                float3 normalWS = nml > 1e-10 ? nm * rsqrt(nml) : nWS;

                // geometric specular anti-aliasing: where the normal changes faster than a pixel (bevels, carved
                // mouldings, normal-mapped metal at a distance) the lobe widens instead of shimmering; and a floor on
                // roughness so no dry surface throws pin-point sparkles off candle flames
                half roughDirect;
                {
                    float3 dndx = ddx(normalWS), dndy = ddy(normalWS);
                    float variance = 0.25 * (dot(dndx, dndx) + dot(dndy, dndy));
                    float kr = min(2.0 * variance, 0.2);
                    float a = rough * rough;
                    roughDirect = (half)sqrt(sqrt(saturate(a * a + kr)));
                    roughDirect = max(roughDirect, lerp(0.24, 0.1, wet));
                }

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = viewWS;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                input.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.bakedGI = BL_SampleAmbient(i.positionWS, normalWS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = metal;
                s.specular = 0;
                s.smoothness = 1 - roughDirect;
                s.occlusion = ao;
                s.normalTS = normalTS;
                s.alpha = 1;
                half3 emi = _EmissionColor.rgb;
                #if defined(_EMISSIONMAP)
                    emi *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                #endif
                s.emission = emi * BL_CircuitPower(_EmissionCircuit);

                half4 col = UniversalFragmentPBR(input, s);

                #if defined(_PLANAR)
                {
                    float planeOk = _BL_PlanarParams.y * step(0.85, normalWS.y) * step(abs(i.positionWS.y - _BL_PlanarParams.x), 0.12);
                    if (planeOk > 0.5)
                    {
                        float2 suv = float2(1.0 - input.normalizedScreenSpaceUV.x, input.normalizedScreenSpaceUV.y) + normalTS.xy * _BL_PlanarParams.w;   // reflection RT is mirrored left/right
                        half4 reflA = SAMPLE_TEXTURE2D_LOD(_BL_PlanarTex, sampler_BL_PlanarTex, suv, 0.75 + rough * 6.0);
                        // alpha 0 where the mirror camera saw nothing: fade out instead of reflecting the clear colour
                        half3 refl = reflA.rgb * saturate(reflA.a * 1.25);
                        half nv = saturate(dot(normalWS, viewWS));
                        half fres = lerp(0.06, 1.0, pow(1 - nv, 4));
                        half gloss = (1 - rough) * (1 - rough);
                        half3 specTint = lerp(half3(1, 1, 1), albedo, metal);
                        col.rgb += refl * specTint * fres * gloss * _PlanarStrength * _BL_PlanarParams.z;
                    }
                }
                #endif

                // a soft sheen along silhouettes from the room's fill light, so dark pieces keep their shape against dark walls
                {
                    half rimNV = saturate(dot(normalWS, viewWS));
                    col.rgb += input.bakedGI * (pow(1.0 - rimNV, 4.0) * 0.45) * ao;
                }
                // highlights roll off softly above ~2.4: bloom picks up real light sources, not every glint
                col.rgb = BL_SoftClamp(col.rgb, 2.4);
                col.rgb = MixFog(col.rgb, input.fogCoord);
                return half4(col.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V ShadowVert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 L = normalize(_LightPosition - p);
                #else
                    float3 L = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, L)));
                o.uv = v.uv * _Tiling.xy + _Tiling.zw;
                return o;
            }
            half4 ShadowFrag(V i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DVert
            #pragma fragment DFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V DVert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv * _Tiling.xy + _Tiling.zw; return o; }
            half DFrag(V i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NVert
            #pragma fragment NFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V NVert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv * _Tiling.xy + _Tiling.zw; o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 NFrag(V i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                half3 n = normalize(i.normalWS); if (!isFront) n = -n;
                return half4(n, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
