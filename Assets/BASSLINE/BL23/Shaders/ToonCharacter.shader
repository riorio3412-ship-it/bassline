Shader "BL23/ToonCharacter"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.62,0.52,0.66,1)
        _Shade2Color ("Deep Shade Tint", Color) = (0.42,0.33,0.48,1)
        _RampThreshold ("Ramp Threshold", Range(-1,1)) = 0.05
        _RampSmooth ("Ramp Smooth", Range(0.001,0.3)) = 0.035
        _Ramp2Threshold ("Deep Ramp Threshold", Range(-1,1)) = -0.45
        _ShadowReceive ("Shadow Receive", Range(0,1)) = 1
        _RimColor ("Rim Color", Color) = (0.85,0.8,1,1)
        _RimPower ("Rim Width", Range(0.01,0.6)) = 0.18
        _RimStrength ("Rim Strength", Range(0,2)) = 0.35
        _GlossThreshold ("Gloss Threshold", Range(0.5,1)) = 0.96
        _GlossStrength ("Gloss Strength", Range(0,2)) = 0
        _GlossColor ("Gloss Color", Color) = (1,1,1,1)
        _HairRing ("Hair Ring", Range(0,1)) = 0
        _ShadeSat ("Shade Saturation", Range(0,1)) = 0.55
        _SkinSSS ("Skin Terminator Warmth", Range(0,1)) = 0
        _SSSColor ("Skin Terminator Color", Color) = (1,0.6,0.48,1)
        _MaskMode ("Hair/Skin Masks (0 material, 1 per-vertex uv3)", Float) = 0
        _OutlineTint ("Outline Albedo Tint", Range(0,1)) = 0.45
        _UseVertexColor ("Use Vertex Color", Float) = 0
        _CavityStrength ("Cavity (vertex alpha AO)", Range(0,1)) = 0.8
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1
        _OutlineColor ("Outline Color", Color) = (0.08,0.05,0.09,1)
        _OutlineWidth ("Outline Width (px @1080p)", Range(0,6)) = 1.6
        _OutlineZ ("Outline Z Push", Range(0,50)) = 4
        _Emission ("Emission", Range(0,8)) = 0
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _BloodAmount ("Blood Spatter", Range(0,1)) = 0
        _BloodColor ("Blood Color", Color) = (0.36,0.015,0.03,1)
        _Wet ("Wet", Range(0,1)) = 0
        _BreakAmount ("BREAK", Range(0,1)) = 0
        _BreakColor ("BREAK Color", Color) = (0.62,0.06,0.28,1)
        _WoundStrength ("Wound Blood Strength", Range(0,1)) = 1
        _Bruise ("Bruise Strength", Range(0,1)) = 1
        _Wound0 ("Wound0", Vector) = (0,0,0,0)
        _Wound1 ("Wound1", Vector) = (0,0,0,0)
        _Wound2 ("Wound2", Vector) = (0,0,0,0)
        _Wound3 ("Wound3", Vector) = (0,0,0,0)
        _Wound4 ("Wound4", Vector) = (0,0,0,0)
        _Wound5 ("Wound5", Vector) = (0,0,0,0)
        _Wound6 ("Wound6", Vector) = (0,0,0,0)
        _Wound7 ("Wound7", Vector) = (0,0,0,0)
        _PatternType ("Pattern Type", Float) = 0
        _PatternColor ("Pattern Color", Color) = (0,0,0,1)
        _PatternParams ("Pattern Params (scale,width,a,b)", Vector) = (0.02,0.1,0,0)
        _DecalTex ("Decal", 2D) = "black" {}
        _DecalOn ("Decal On", Float) = 0
        _DecalRect ("Decal Rect (bind xy center, half size)", Vector) = (0,1.3,0.06,0.06)
        _Pad0 ("pad", Float) = 0
        [NoScaleOffset] _FaceAtlas ("Face Atlas", 2D) = "black" {}
        [NoScaleOffset] _FaceFx ("Face Fx", 2D) = "black" {}
        [NoScaleOffset] _FaceMask ("Face Skin Mask (GLB)", 2D) = "white" {}
        _FaceEye ("Face Eye (dx, y, hw, hh)", Vector) = (0.16,0.47,0.11,0.1)
        _FaceBrow ("Face Brow (dx, y, hw, hh)", Vector) = (0.16,0.62,0.11,0.035)
        _FaceMouth ("Face Mouth (x, y, hw, hh)", Vector) = (0.5,0.22,0.1,0.05)
        _FaceFxRect ("Face Fx Rect", Vector) = (0.5,0.5,0.5,0.5)
        _FaceState0 ("Face State0 (eyeL, eyeR, browL, browR)", Vector) = (0,0,0,0)
        _FaceState1 ("Face State1 (mouth, tears, blush, gloom)", Vector) = (0,0,0,0)
        _FaceCover ("Face Cover (rgb skin, a GLB mode)", Color) = (1,0.85,0.8,0)
        _DarkFace ("Dark Face (shadow, glow, kind, time)", Vector) = (0,0,0,0)
        _DarkFaceColor ("Dark Face Glow", Color) = (0.86,0.9,1,1)
        _FaceStrain ("Face Strain (flush, pale, gasp)", Vector) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle(_FACE)] _FaceOn ("Face Mode", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Unlit" }
        LOD 300

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ToonVert
            #pragma fragment ToonFrag
            #pragma shader_feature_local _FACE
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "ToonForward.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #include "ToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #include "ToonPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
