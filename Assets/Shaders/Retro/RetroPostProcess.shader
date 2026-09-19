Shader "Retro/RetroPostProcess"
{
    Properties
    {
        [PerRendererData] _MainTex ("Source Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.22
        _ColorDepth ("Color Depth (Levels per Channel)", Range(4, 64)) = 24
        [Toggle] _EnableColorQuantization ("Enable Color Quantization", Float) = 1
        [Toggle] _EnableDithering ("Enable Dithering", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            Name "Default"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _DitherStrength;
            float _ColorDepth;
            float _EnableColorQuantization;
            float _EnableDithering;

            // 4x4 Bayer Dithering Matrix normalized to [0, 1]
            static const float BayerMatrix4x4[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Force sharp point-sampling on low-res render texture
                float2 texelCoord = floor(IN.texcoord * _MainTex_TexelSize.zw);
                float2 pointUV = (texelCoord + 0.5) * _MainTex_TexelSize.xy;

                half4 color = tex2D(_MainTex, pointUV) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                int2 pixelCoord = int2(texelCoord);
                int ditherIndex = (pixelCoord.x % 4) + (pixelCoord.y % 4) * 4;
                float ditherValue = BayerMatrix4x4[ditherIndex] - 0.5;

                if (_EnableDithering > 0.5)
                {
                    color.rgb += ditherValue * _DitherStrength;
                }

                if (_EnableColorQuantization > 0.5 && _ColorDepth > 0)
                {
                    color.rgb = floor(color.rgb * _ColorDepth + 0.5) / _ColorDepth;
                }

                color.rgb = saturate(color.rgb);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "UI/Default"
}
