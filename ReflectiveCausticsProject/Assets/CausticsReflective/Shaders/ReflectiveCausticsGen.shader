Shader "CausticsReflective/ReflectiveCausticsGen"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" }
        Pass
        {
            Name "Gen"
            Blend One One
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float cosTheta : TEXCOORD0;
                float weight : TEXCOORD1;
            };

            TEXTURE2D(_WaterNormalTex);
            SAMPLER(sampler_WaterNormalTex);
            TEXTURE2D(_WaterHeightTex);
            SAMPLER(sampler_WaterHeightTex);

            CBUFFER_START(UnityPerMaterial)
            float4x4 _WaterToWorld;
            float4x4 _WorldToWater;
            float4x4 _PlaneToWorld;
            float4x4 _WorldToPlane;
            float4 _WaterSize;          // xy = size, zw = inv size
            float4 _WaterGridParams;    // xy = grid counts, zw = inv counts
            float4 _PlaneSize;          // xy = size, zw = inv size
            float4 _ReceiverResolution; // xy = size, zw = inv size
            float4 _SunDir;             // xyz = direction
            float4 _Tint;
            float _Intensity;
            float _F0;
            float _JacobianGain;
            float _WaterNormalTexelSize;
            float _PlaneDistanceTolerance;
            float _PlaneTwoSided;
            float _Padding0;
            float _Padding1;
            CBUFFER_END

            float3 SampleWaterNormal(float2 uv)
            {
                float3 encoded = SAMPLE_TEXTURE2D_LOD(_WaterNormalTex, sampler_WaterNormalTex, uv, 0).xyz;
                float3 normalTS = normalize(encoded * 2.0 - 1.0);
                return normalTS;
            }

            float FresnelPlaceholder(float cosTheta)
            {
                float F0 = saturate(_F0);
                float oneMinus = 1.0 - saturate(cosTheta);
                return F0 + (1.0 - F0) * pow(oneMinus, 5.0);
            }

            float JacobianPlaceholder(float baseWeight)
            {
                return max(0.0, _JacobianGain) * baseWeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(-1.0, -1.0, 0.0, 1.0);
                output.weight = 0.0;
                output.cosTheta = 1.0;

                uint gridCountX = (uint)max(1.0, round(_WaterGridParams.x));
                uint gridCountY = (uint)max(1.0, round(_WaterGridParams.y));
                float2 invGrid = float2(1.0 / gridCountX, 1.0 / gridCountY);

                uint sampleIndex = input.vertexID;
                uint sampleX = sampleIndex % gridCountX;
                uint sampleY = sampleIndex / gridCountX;

                if (sampleY >= gridCountY)
                {
                    return output;
                }

                float2 waterUV = (float2(sampleX, sampleY) + 0.5) * invGrid;

                float2 waterLocalXZ = (waterUV - 0.5) * _WaterSize.xy;
                float4 waterLocal = float4(waterLocalXZ.x, 0.0, waterLocalXZ.y, 1.0);
                float3 waterWorld = mul(_WaterToWorld, waterLocal).xyz;

                float3 normalLocal = float3(0.0, 1.0, 0.0);
                if (_WaterNormalTexelSize > 0.0)
                {
                    float3 sampled = SampleWaterNormal(waterUV);
                    normalLocal = normalize(float3(sampled.x, sampled.z, sampled.y));
                }

                float3x3 waterToWorld3x3 = (float3x3)_WaterToWorld;
                float3 normalWS = normalize(mul(waterToWorld3x3, normalLocal));

                float3 sunDir = _SunDir.xyz;
                float sunLen = max(1e-4, length(sunDir));
                float3 lightDir = -sunDir / sunLen;

                float3 reflectDir = reflect(-lightDir, normalWS);

                float3 originPlane = mul(_WorldToPlane, float4(waterWorld, 1.0)).xyz;
                float3 dirPlane = mul((float3x3)_WorldToPlane, reflectDir);

                float denom = dirPlane.y;
                if (abs(denom) < max(1e-4, _PlaneDistanceTolerance))
                {
                    return output;
                }

                float t = -originPlane.y / denom;
                if (t <= 0.0)
                {
                    return output;
                }

                if (_PlaneTwoSided < 0.5)
                {
                    if (originPlane.y <= 0.0 || denom >= 0.0)
                    {
                        return output;
                    }
                }

                float3 hitPlane = originPlane + dirPlane * t;
                float2 planeUV = hitPlane.xz * _PlaneSize.zw + 0.5;
                float2 planeUVClamped = saturate(planeUV);

                output.positionCS = float4(planeUVClamped * 2.0 - 1.0, 0.0, 1.0);

                if (planeUV.x < 0.0 || planeUV.x > 1.0 || planeUV.y < 0.0 || planeUV.y > 1.0)
                {
                    return output;
                }

                float cosTheta = saturate(dot(normalWS, -lightDir));
                output.cosTheta = cosTheta;

                float cellArea = (_WaterSize.x * _WaterSize.y) * invGrid.x * invGrid.y;
                output.weight = JacobianPlaceholder(cellArea);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float weight = input.weight;
                if (weight <= 0.0)
                {
                    discard;
                }

                float fresnel = FresnelPlaceholder(input.cosTheta);
                float energy = weight * fresnel * _Intensity;
                float3 rgb = _Tint.rgb * energy;
                return half4(rgb, energy);
            }
            ENDHLSL
        }
    }
}

