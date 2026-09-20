Shader "Retro/RetroPixelLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)

        [Header(Directional Light Steps)]
        _ShadowThreshold ("Shadow Threshold", Range(0.01, 1.0)) = 0.2
        _MidThreshold ("Midtone Threshold", Range(0.01, 1.0)) = 0.55
        _ShadowIntensity ("Shadow Ambient Brightness", Range(0.0, 0.5)) = 0.2
        _MidIntensity ("Midtone Brightness", Range(0.2, 0.9)) = 0.6

        [Header(Point Light Steps)]
        _PointLightThresholdLow ("Point Light Outer Threshold", Range(0.01, 0.5)) = 0.05
        _PointLightThresholdHigh ("Point Light Inner Threshold", Range(0.1, 1.0)) = 0.35
        _PointLightMidIntensity ("Point Light Mid Brightness", Range(0.2, 0.9)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ShadowThreshold;
                float _MidThreshold;
                float _ShadowIntensity;
                float _MidIntensity;
                float _PointLightThresholdLow;
                float _PointLightThresholdHigh;
                float _PointLightMidIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            // Directional Light: 3-step banded shading (Shadow, Midtone, Highlight)
            float StepDirectionalLight(float rawLight)
            {
                if (rawLight < _ShadowThreshold)
                {
                    return _ShadowIntensity;
                }
                else if (rawLight < _MidThreshold)
                {
                    return _MidIntensity;
                }
                else
                {
                    return 1.0;
                }
            }

            // Point / Additional Light: 3-step banded shading (0 = out of range, Midtone, Full)
            float StepAdditionalLight(float rawLight)
            {
                if (rawLight < _PointLightThresholdLow)
                {
                    return 0.0;
                }
                else if (rawLight < _PointLightThresholdHigh)
                {
                    return _PointLightMidIntensity;
                }
                else
                {
                    return 1.0;
                }
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (any(input.color.rgb))
                {
                    texColor *= input.color;
                }

                float3 normalWS = normalize(input.normalWS);

                // 1. Directional Main Light (Sun / Moon) with stepped shadow/light bands
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float lightAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float mainIntensity = StepDirectionalLight(NdotL * lightAtten);

                float3 lighting = mainLight.color * mainIntensity;

                // 2. Additional Lights (Point Lights / Torches) with stepped bands
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint clIndex = 0; clIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); clIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light addLight = GetAdditionalLight(clIndex, inputData.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    float addAtten = addLight.distanceAttenuation;
                    float addIntensity = StepAdditionalLight(addNdotL * addAtten);
                    lighting += addLight.color * addIntensity;
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    float addAtten = addLight.distanceAttenuation;
                    float addIntensity = StepAdditionalLight(addNdotL * addAtten);
                    lighting += addLight.color * addIntensity;
                LIGHT_LOOP_END
                #endif

                // 3. Ambient lighting
                float3 ambient = SampleSH(normalWS);
                float3 finalColor = texColor.rgb * (lighting + ambient * _ShadowIntensity);

                return float4(finalColor, texColor.a);
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
