Shader "Hidden/Coop/OutlineComposite"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineThickness ("Outline Thickness", Float) = 2.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "OutlineComposite"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_MaskTex);
            SAMPLER(sampler_LinearClamp);

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float centerMask = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv).r;

                // 물체 내부 픽셀은 외곽선 표시 제외 (물체 원래 색상 보존)
                if (centerMask > 0.5)
                {
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                // 화면 해상도 기반 텍셀 오프셋 계산
                float2 texelSize = _ScreenParams.zw - 1.0; // zw is (1 + 1/width, 1 + 1/height)
                float2 offset = _OutlineThickness * texelSize;

                // 8방향 이웃 픽셀 샘플링으로 매끄러운 외곽선 검출
                float n0 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv + float2(0.0, offset.y)).r;
                float n1 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv - float2(0.0, offset.y)).r;
                float n2 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv + float2(offset.x, 0.0)).r;
                float n3 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv - float2(offset.x, 0.0)).r;

                float d0 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv + float2(offset.x, offset.y) * 0.7071).r;
                float d1 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv + float2(-offset.x, offset.y) * 0.7071).r;
                float d2 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv + float2(offset.x, -offset.y) * 0.7071).r;
                float d3 = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv + float2(-offset.x, -offset.y) * 0.7071).r;

                float maxNeighbor = max(max(max(n0, n1), max(n2, n3)), max(max(d0, d1), max(d2, d3)));

                if (maxNeighbor > 0.5)
                {
                    return _OutlineColor;
                }

                return half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
