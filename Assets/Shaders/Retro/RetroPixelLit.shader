Shader "Retro/RetroPixelLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0.0, 1.0)) = 0.25
        _MidThreshold ("Midtone Threshold", Range(0.0, 1.0)) = 0.6
        _ShadowIntensity ("Directional Shadow Fill", Range(0.05, 0.5)) = 0.25
        _MidIntensity ("Midtone Brightness", Range(0.3, 1.0)) = 0.65
        [Toggle] _EnableDithering ("Enable Bayer Dithering", Float) = 1
        _DitherStrength ("Dither Strength", Range(0.0, 0.5)) = 0.15
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
                float _EnableDithering;
                float _DitherStrength;
            CBUFFER_END

            // Standard 4x4 Bayer Dithering Matrix
            static const float BayerMatrix4x4[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };

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

            float QuantizeDirectionalLight(float rawLight, float2 screenPixel)
            {
                float dither = 0.0;
                if (_EnableDithering > 0.5)
                {
                    int2 pixelCoord = int2(screenPixel) % 4;
                    int ditherIndex = pixelCoord.x + pixelCoord.y * 4;
                    dither = (BayerMatrix4x4[ditherIndex] - 0.5) * _DitherStrength;
                }

                float adjustedLight = rawLight + dither;
                if (adjustedLight < _ShadowThreshold)
                {
                    return _ShadowIntensity;
                }
                else if (adjustedLight < _MidThreshold)
                {
                    return _MidIntensity;
                }
                else
                {
                    return 1.0;
                }
            }

            float QuantizeAdditionalLight(float rawLight, float2 screenPixel)
            {
                if (rawLight <= 0.001)
                {
                    return 0.0;
                }

                float dither = 0.0;
                if (_EnableDithering > 0.5)
                {
                    int2 pixelCoord = int2(screenPixel) % 4;
                    int ditherIndex = pixelCoord.x + pixelCoord.y * 4;
                    dither = (BayerMatrix4x4[ditherIndex] - 0.5) * _DitherStrength;
                }

                float adjustedLight = rawLight + dither;
                if (adjustedLight <= 0.04)
                {
                    return 0.0;
                }
                else if (adjustedLight < 0.25)
                {
                    return 0.35;
                }
                else if (adjustedLight < 0.6)
                {
                    return 0.7;
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

                // 1. Main directional light
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float lightAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float mainIntensity = QuantizeDirectionalLight(NdotL * lightAtten, input.positionCS.xy);

                float3 lighting = mainLight.color * mainIntensity;

                // 2. Additional point lights (Forward+ / Clustered and Forward compatible)
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
                    float addIntensity = QuantizeAdditionalLight(addNdotL * addAtten, input.positionCS.xy);
                    lighting += addLight.color * addIntensity;
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    float addAtten = addLight.distanceAttenuation;
                    float addIntensity = QuantizeAdditionalLight(addNdotL * addAtten, input.positionCS.xy);
                    lighting += addLight.color * addIntensity;
                LIGHT_LOOP_END
                #endif

                // 3. Ambient light
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
