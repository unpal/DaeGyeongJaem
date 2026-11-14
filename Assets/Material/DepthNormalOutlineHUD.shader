Shader "Dotori/DepthNormalOutlineHUD"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Thickness ("Thickness", Float) = 1.5
        _DepthThreshold ("Depth Threshold", Float) = 0.5
        _NormalThreshold ("Normal Threshold", Float) = 0.4
        _FogStart ("Fog Start", Float) = 10
        _FogEnd ("Fog End", Float) = 25
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _BgColor ("Background Color", Color) = (0, 0, 0, 1)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "DepthNormalOutlineHUD"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS   : SV_POSITION;
                half4  color         : COLOR;
                float2 uv            : TEXCOORD0;
                float2 positionLocal : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Thickness;
                float _DepthThreshold;
                float _NormalThreshold;
                float _FogStart;
                float _FogEnd;
                half4 _LineColor;
                half4 _BgColor;
                float4 _ClipRect;
                float _UIMaskSoftnessX;
                float _UIMaskSoftnessY;
            CBUFFER_END

            float SampleEyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                return (unity_OrthoParams.w == 0.0)
                    ? LinearEyeDepth(raw, _ZBufferParams)
                    : LinearDepthToEyeDepth(raw);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionLocal = IN.positionOS.xy;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                float2 texel = _Thickness / GetScaledScreenParams().xy;

                float2 uvTL = saturate(screenUV + float2(-texel.x,  texel.y));
                float2 uvBR = saturate(screenUV + float2( texel.x, -texel.y));
                float2 uvTR = saturate(screenUV + float2( texel.x,  texel.y));
                float2 uvBL = saturate(screenUV + float2(-texel.x, -texel.y));

                float dTL = SampleEyeDepth(uvTL);
                float dBR = SampleEyeDepth(uvBR);
                float dTR = SampleEyeDepth(uvTR);
                float dBL = SampleEyeDepth(uvBL);
                float depthDiff = abs(dTL - dBR) + abs(dTR - dBL);
                float depthEdge = step(_DepthThreshold, depthDiff);

                float3 nTL = SampleSceneNormals(uvTL);
                float3 nBR = SampleSceneNormals(uvBR);
                float3 nTR = SampleSceneNormals(uvTR);
                float3 nBL = SampleSceneNormals(uvBL);
                float normalDiff = distance(nTL, nBR) + distance(nTR, nBL);
                float normalEdge = step(_NormalThreshold, normalDiff);

                float minDepth = min(min(dTL, dBR), min(dTR, dBL));
                half fogFactor = (half)(1.0 - smoothstep(_FogStart, max(_FogEnd, _FogStart + 0.001), minDepth));
                half edge = (half)max(depthEdge, normalEdge) * fogFactor;

                half4 spriteSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 color = lerp(_BgColor, _LineColor, edge);
                color.rgb *= spriteSample.rgb * IN.color.rgb;
                color.a *= spriteSample.a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                    float2 clipDist = min(IN.positionLocal - _ClipRect.xy, _ClipRect.zw - IN.positionLocal);
                    half clipX = (_UIMaskSoftnessX > 0.0) ? saturate(clipDist.x / _UIMaskSoftnessX) : step(0.0, clipDist.x);
                    half clipY = (_UIMaskSoftnessY > 0.0) ? saturate(clipDist.y / _UIMaskSoftnessY) : step(0.0, clipDist.y);
                    color.a *= clipX * clipY;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001h);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}

