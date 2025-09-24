using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CausticsReflective
{
    public class ReflectiveCausticsGenPass : ScriptableRenderPass
    {
        private const int MaxReceivers = 4;

        private static readonly int WaterNormalTexId = Shader.PropertyToID("_RC_WaterNormalTex");
        private static readonly int WaterHeightTexId = Shader.PropertyToID("_RC_WaterHeightTex");
        private static readonly int WaterToWorldId = Shader.PropertyToID("_RC_WaterToWorld");
        private static readonly int WorldToWaterId = Shader.PropertyToID("_RC_WorldToWater");
        private static readonly int WaterSizeMetersId = Shader.PropertyToID("_RC_WaterSizeMeters");
        private static readonly int SunDirectionId = Shader.PropertyToID("_RC_SunDirection");
        private static readonly int IntensityId = Shader.PropertyToID("_RC_Intensity");
        private static readonly int FresnelF0Id = Shader.PropertyToID("_RC_F0");
        private static readonly int JacobianGainId = Shader.PropertyToID("_RC_JacobianGain");
        private static readonly int PlaneToWorldId = Shader.PropertyToID("_RC_PlaneToWorld");
        private static readonly int WorldToPlaneId = Shader.PropertyToID("_RC_WorldToPlane");
        private static readonly int PlaneOriginId = Shader.PropertyToID("_RC_PlaneOrigin");
        private static readonly int PlaneNormalId = Shader.PropertyToID("_RC_PlaneNormal");
        private static readonly int ReceiverParamsId = Shader.PropertyToID("_RC_ReceiverParams");
        private static readonly int ReceiverResolutionId = Shader.PropertyToID("_RC_ReceiverResolution");

        private readonly ProfilingSampler _profilingSampler = new("Reflective Caustics Generation");
        private readonly Material _material;

        private readonly Matrix4x4[] _planeToWorldCache = new Matrix4x4[MaxReceivers];

        public ReflectiveCausticsGenPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                return;
            }

            var manager = ReflectiveCausticsManager.Instance;
            var receivers = ReflectiveCausticsManager.Receivers;

            if (manager == null || manager.Water == null || receivers == null || receivers.Count == 0)
            {
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                PopulateSharedShaderData(cmd, manager);
                RenderReceivers(cmd, receivers);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void PopulateSharedShaderData(CommandBuffer cmd, ReflectiveCausticsManager manager)
        {
            var water = manager.Water;

            var normalTex = water.NormalTex != null ? water.NormalTex : Texture2D.normalTexture;
            var heightTex = water.HeightTex != null ? water.HeightTex : Texture2D.blackTexture;
            var waterToWorld = water.WaterToWorld;
            var worldToWater = waterToWorld.inverse;

            cmd.SetGlobalTexture(WaterNormalTexId, normalTex);
            cmd.SetGlobalTexture(WaterHeightTexId, heightTex);
            cmd.SetGlobalMatrix(WaterToWorldId, waterToWorld);
            cmd.SetGlobalMatrix(WorldToWaterId, worldToWater);
            cmd.SetGlobalVector(WaterSizeMetersId, new Vector4(water.WaterSizeMeters.x, water.WaterSizeMeters.y, Mathf.Max(1e-5f, water.NormalTexelSize), 0f));

            var sunDirection = manager.Sun != null ? -manager.Sun.transform.forward : Vector3.down;
            cmd.SetGlobalVector(SunDirectionId, new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
            cmd.SetGlobalFloat(IntensityId, Mathf.Max(0f, manager.Intensity));
            cmd.SetGlobalFloat(FresnelF0Id, Mathf.Clamp01(manager.F0));
            cmd.SetGlobalFloat(JacobianGainId, Mathf.Max(0f, manager.JacobianGain));
        }

        private void RenderReceivers(CommandBuffer cmd, IReadOnlyList<CausticsReceiverPlane> receivers)
        {
            var count = Mathf.Min(receivers.Count, MaxReceivers);
            for (var i = 0; i < count; i++)
            {
                var receiver = receivers[i];
                if (receiver == null || receiver.CausticsRT == null)
                {
                    continue;
                }

                _planeToWorldCache[i] = receiver.PlaneToWorld;

                ConfigureReceiver(cmd, receiver);

                cmd.SetRenderTarget(receiver.CausticsRT);
                cmd.ClearRenderTarget(false, true, Color.black);
                cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3, 1);
            }
        }

        private static void ConfigureReceiver(CommandBuffer cmd, CausticsReceiverPlane receiver)
        {
            var planeToWorld = receiver.PlaneToWorld;
            var worldToPlane = receiver.WorldToPlane;
            var planeOrigin = planeToWorld.MultiplyPoint(Vector3.zero);
            var planeNormal = planeToWorld.MultiplyVector(Vector3.forward).normalized;

            cmd.SetGlobalMatrix(PlaneToWorldId, planeToWorld);
            cmd.SetGlobalMatrix(WorldToPlaneId, worldToPlane);
            cmd.SetGlobalVector(PlaneOriginId, new Vector4(planeOrigin.x, planeOrigin.y, planeOrigin.z, 0f));
            cmd.SetGlobalVector(PlaneNormalId, new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, 0f));
            cmd.SetGlobalVector(ReceiverParamsId, new Vector4(receiver.sizeMeters.x, receiver.sizeMeters.y, receiver.planeDistanceTolerance, receiver.twoSided ? 1f : 0f));
            cmd.SetGlobalVector(ReceiverResolutionId, new Vector4(receiver.resolution.x, receiver.resolution.y, 1f / Mathf.Max(1, receiver.resolution.x), 1f / Mathf.Max(1, receiver.resolution.y)));
        }
    }
}
