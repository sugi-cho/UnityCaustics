Shader "Hidden/CausticsReflective/Composite"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Composite"
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define RC_MAX_RECEIVERS 4

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_RC_CausticsTex0);
            SAMPLER(sampler_RC_CausticsTex0);
            TEXTURE2D(_RC_CausticsTex1);
            SAMPLER(sampler_RC_CausticsTex1);
            TEXTURE2D(_RC_CausticsTex2);
            SAMPLER(sampler_RC_CausticsTex2);
            TEXTURE2D(_RC_CausticsTex3);
            SAMPLER(sampler_RC_CausticsTex3);

            CBUFFER_START(RCCompositeParams)
                float4 _RC_TintIntensity; // rgb tint, w intensity
                float _RC_MultiplyBlend;
                float3 _RC_Padding0;
                float4 _RC_PlaneInfo[RC_MAX_RECEIVERS]; // xy size, z tolerance, w two sided flag
                float4x4 _RC_WorldToPlane[RC_MAX_RECEIVERS];
                int _RC_ReceiverCount;
                float3 _RC_Padding1;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            float SampleCausticsTexture(int index, float2 uv)
            {
                float sampleValue = 0.0;

                if (index == 0)
                {
                    sampleValue = SAMPLE_TEXTURE2D(_RC_CausticsTex0, sampler_RC_CausticsTex0, uv).r;
                }
                else if (index == 1)
                {
                    sampleValue = SAMPLE_TEXTURE2D(_RC_CausticsTex1, sampler_RC_CausticsTex1, uv).r;
                }
                else if (index == 2)
                {
                    sampleValue = SAMPLE_TEXTURE2D(_RC_CausticsTex2, sampler_RC_CausticsTex2, uv).r;
                }
                else if (index == 3)
                {
                    sampleValue = SAMPLE_TEXTURE2D(_RC_CausticsTex3, sampler_RC_CausticsTex3, uv).r;
                }

                return sampleValue;
            }

            float3 ComputeWorldPosition(float2 uv, float deviceDepth)
            {
                return ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                const float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv);
                if (rawDepth >= 1.0 - 1e-5)
                {
                    return baseColor;
                }

                const float3 worldPos = ComputeWorldPosition(input.uv, rawDepth);

                float bestDistance = 1e20;
                float causticsIntensity = 0.0;
                bool foundReceiver = false;

                [unroll]
                for (int i = 0; i < RC_MAX_RECEIVERS; ++i)
                {
                    if (i >= _RC_ReceiverCount)
                    {
                        break;
                    }

                    const float4x4 worldToPlane = _RC_WorldToPlane[i];
                    const float3 planePos = mul(worldToPlane, float4(worldPos, 1.0)).xyz;

                    const float signedDistance = planePos.z;
                    const float distance = abs(signedDistance);
                    const float tolerance = _RC_PlaneInfo[i].z;
                    if (distance > tolerance)
                    {
                        continue;
                    }

                    const bool twoSided = _RC_PlaneInfo[i].w > 0.5;
                    if (!twoSided && signedDistance < 0.0)
                    {
                        continue;
                    }

                    const float halfWidth = _RC_PlaneInfo[i].x * 0.5;
                    const float halfHeight = _RC_PlaneInfo[i].y * 0.5;
                    if (abs(planePos.x) > halfWidth || abs(planePos.y) > halfHeight)
                    {
                        continue;
                    }

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        float2 uvPlane = planePos.xy / float2(_RC_PlaneInfo[i].x, _RC_PlaneInfo[i].y) + 0.5;
                        uvPlane = saturate(uvPlane);
                        causticsIntensity = SampleCausticsTexture(i, uvPlane);
                        foundReceiver = true;
                    }
                }

                if (!foundReceiver)
                {
                    return baseColor;
                }

                float shadowAtten = 1.0;
            #if defined(MAIN_LIGHT_SHADOWS) || defined(MAIN_LIGHT_SHADOWS_CASCADE)
                const float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                shadowAtten = MainLightRealtimeShadow(shadowCoord);
            #endif

                const float3 tint = _RC_TintIntensity.rgb * _RC_TintIntensity.w;
                const float3 caustics = causticsIntensity.xxx * tint * shadowAtten;

                float3 color = baseColor.rgb;
                if (_RC_MultiplyBlend > 0.5)
                {
                    color *= (1.0 + caustics);
                }
                else
                {
                    color += caustics;
                }

                return half4(color, baseColor.a);
            }
            ENDHLSL
        }
    }
}

