Shader "CausticsReflective/ReflectiveCausticsGen"
{
    Properties
    {
        [HideInInspector]_Unused("Unused", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ReflectiveCausticsGen"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4x4 _RC_WaterToWorld;
                float4x4 _RC_WorldToWater;
                float4x4 _RC_PlaneToWorld;
                float4x4 _RC_WorldToPlane;
                float4 _RC_WaterSizeMeters; // xy: size meters, z: normal texel size
                float4 _RC_SunDirection;    // xyz: direction (towards surface)
                float4 _RC_ReceiverParams;  // xy: size meters, z: tolerance, w: two-sided flag
                float4 _RC_ReceiverResolution;
                float4 _RC_PlaneOrigin;     // xyz: plane origin
                float4 _RC_PlaneNormal;     // xyz: plane normal
                float _RC_Intensity;
                float _RC_F0;
                float _RC_JacobianGain;
            CBUFFER_END

            TEXTURE2D(_RC_WaterNormalTex);
            SAMPLER(sampler_RC_WaterNormalTex);
            TEXTURE2D(_RC_WaterHeightTex);
            SAMPLER(sampler_RC_WaterHeightTex);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.uv = uv;
                output.positionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                return output;
            }

            float3 SampleWaterNormal(float2 uv)
            {
                float3 n = SAMPLE_TEXTURE2D(_RC_WaterNormalTex, sampler_RC_WaterNormalTex, uv).xyz * 2.0f - 1.0f;
                float3x3 tbn = (float3x3)_RC_WaterToWorld;
                return normalize(mul(tbn, n));
            }

            float SampleHeight(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_RC_WaterHeightTex, sampler_RC_WaterHeightTex, uv).r;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 receiverSize = _RC_ReceiverParams.xy;
                float2 planeLocal = (input.uv - 0.5f) * receiverSize;
                float3 localPoint = float3(planeLocal, 0.0f);
                float3 worldPoint = mul(_RC_PlaneToWorld, float4(localPoint, 1.0f)).xyz;

                float3 planeNormal = normalize(_RC_PlaneNormal.xyz);
                float3 sunDir = normalize(_RC_SunDirection.xyz);
                float2 waterSize = max(_RC_WaterSizeMeters.xy, float2(1e-4f, 1e-4f));

                float3 waterLocal = mul(_RC_WorldToWater, float4(worldPoint, 1.0f)).xyz;
                float2 waterUv = waterLocal.xz / waterSize + 0.5f;
                waterUv = saturate(waterUv);

                float3 waterNormal = SampleWaterNormal(waterUv);
                float3 reflected = normalize(reflect(-sunDir, waterNormal));

                float denom = dot(reflected, planeNormal);
                bool twoSided = _RC_ReceiverParams.w > 0.5f;

                if ((!twoSided && denom >= -1e-4f) || (twoSided && abs(denom) <= 1e-4f))
                {
                    return 0;
                }

                float3 planeOrigin = _RC_PlaneOrigin.xyz;
                float t = dot(planeOrigin - worldPoint, planeNormal) / denom;
                if (t <= 0.0f)
                {
                    return 0;
                }

                float3 hitWorld = worldPoint + reflected * t;
                float3 hitLocal = mul(_RC_WorldToPlane, float4(hitWorld, 1.0f)).xyz;
                float2 halfSize = receiverSize * 0.5f;
                if (abs(hitLocal.x) > halfSize.x + 1e-4f || abs(hitLocal.y) > halfSize.y + 1e-4f)
                {
                    return 0;
                }

                float distance = length(hitWorld - worldPoint);
                float tolerance = max(1e-4f, _RC_ReceiverParams.z);
                float distanceWeight = saturate(1.0f - distance / tolerance);

                float cosTheta = saturate(dot(waterNormal, -sunDir));
                float fresnel = _RC_F0 + (1.0f - _RC_F0) * pow(1.0f - cosTheta, 5.0f);
                float focus = saturate(abs(denom));
                float jacobian = pow(focus, 2.0f) * _RC_JacobianGain;

                float intensity = fresnel * jacobian * distanceWeight * _RC_Intensity;
                return float4(intensity, 0.0f, 0.0f, 1.0f);
            }
            ENDHLSL
        }
    }
}
