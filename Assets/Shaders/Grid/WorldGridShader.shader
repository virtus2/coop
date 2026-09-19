Shader "Custom/WorldGridShader"
{
    Properties
    {
        _GridOrigin ("Grid Origin", Vector) = (0, 0, 0, 0)
        _CellSize ("Cell Size", Float) = 1.0
        _LineWidth ("Line Width (Meters)", Float) = 0.035
        _LineColor ("Line Color", Color) = (0.8, 0.92, 1.0, 0.6)
        _FillColor ("Fill Color", Color) = (0.2, 0.6, 1.0, 0.03)
        _FocusPosition ("Focus Position (World XZ)", Vector) = (0, 0, 0, 0)
        _FadeRadius ("Fade Radius", Float) = 16.0
        _FadeFalloff ("Fade Falloff", Float) = 4.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GridOrigin;
                float4 _FocusPosition;
                float4 _LineColor;
                float4 _FillColor;
                float _CellSize;
                float _LineWidth;
                float _FadeRadius;
                float _FadeFalloff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 월드 좌표 기준 그리드 오프셋 계산
                float2 worldXZ = input.positionWS.xz - _GridOrigin.xz;
                float cellSize = max(0.01, _CellSize);

                // 2. 각 축별 셀 내 거리 및 안티에일리어싱(fwidth) 계산
                float2 cellPos = worldXZ / cellSize;
                float2 distToCellCenter = abs(frac(cellPos) - 0.5);
                float2 distToLine = (0.5 - distToCellCenter) * cellSize; // 선까지의 월드 거리

                // 스크린 미분 기반 부드러운 안티에일리어싱
                float2 ddist = max(fwidth(worldXZ), float2(0.001, 0.001));
                float halfWidth = _LineWidth * 0.5;
                float2 lineAA = smoothstep(halfWidth + ddist, halfWidth - ddist, distToLine);
                float lineAlpha = max(lineAA.x, lineAA.y);

                // 3. 포커스(플레이어/시선) 위치 기준 원형 페이드아웃
                float distToFocus = length(input.positionWS.xz - _FocusPosition.xz);
                float fade = 1.0 - smoothstep(_FadeRadius - _FadeFalloff, _FadeRadius, distToFocus);

                // 4. 최종 색상 조합
                half4 col = lerp(_FillColor, _LineColor, lineAlpha);
                col.a *= fade;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
