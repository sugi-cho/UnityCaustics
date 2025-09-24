using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CausticsReflective
{
    public class ReflectiveCausticsGenPass : ScriptableRenderPass
    {
        private static readonly int WaterNormalTexId = Shader.PropertyToID("_WaterNormalTex");
        private static readonly int WaterHeightTexId = Shader.PropertyToID("_WaterHeightTex");
        private static readonly int WaterToWorldId = Shader.PropertyToID("_WaterToWorld");
        private static readonly int WorldToWaterId = Shader.PropertyToID("_WorldToWater");
        private static readonly int WaterSizeId = Shader.PropertyToID("_WaterSize");
        private static readonly int WaterGridParamsId = Shader.PropertyToID("_WaterGridParams");
        private static readonly int WaterNormalTexelSizeId = Shader.PropertyToID("_WaterNormalTexelSize");
        private static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int FresnelF0Id = Shader.PropertyToID("_F0");
        private static readonly int JacobianGainId = Shader.PropertyToID("_JacobianGain");
        private static readonly int PlaneToWorldId = Shader.PropertyToID("_PlaneToWorld");
        private static readonly int WorldToPlaneId = Shader.PropertyToID("_WorldToPlane");
        private static readonly int PlaneSizeId = Shader.PropertyToID("_PlaneSize");
        private static readonly int ReceiverResolutionId = Shader.PropertyToID("_ReceiverResolution");
        private static readonly int PlaneDistanceToleranceId = Shader.PropertyToID("_PlaneDistanceTolerance");
        private static readonly int PlaneTwoSidedId = Shader.PropertyToID("_PlaneTwoSided");

        private readonly ProfilingSampler _profilingSampler = new("ReflectiveCausticsGen");

        private Material _material;

        public void Setup(Material material)
        {
            _material = material;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);
            bool rendered = TryRender(cmd);

            if (rendered)
            {
                context.ExecuteCommandBuffer(cmd);
            }

            CommandBufferPool.Release(cmd);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
            {
                return;
            }

            using var builder = renderGraph.AddUnsafePass<PassData>(_profilingSampler.name, out var passData);
            passData.Pass = this;

            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                data.Pass.TryRender(cmd);
            });
        }

        private class PassData
        {
            internal ReflectiveCausticsGenPass Pass;
        }

        private bool TryRender(CommandBuffer cmd)
        {
            if (_material == null)
            {
                return false;
            }

            var manager = ReflectiveCausticsManager.Instance;
            if (manager == null)
            {
                return false;
            }

            IWaterSurfaceProvider waterProvider = manager.Water;
            IReadOnlyList<CausticsReceiverPlane> receivers = ReflectiveCausticsManager.Receivers;
            if (waterProvider == null || receivers == null || receivers.Count == 0)
            {
                return false;
            }

            Texture normalTex = waterProvider.NormalTex != null ? waterProvider.NormalTex : Texture2D.normalTexture;
            Texture heightTex = waterProvider.HeightTex != null ? waterProvider.HeightTex : Texture2D.blackTexture;
            Vector2 waterSize = waterProvider.WaterSizeMeters;
            Matrix4x4 waterToWorld = waterProvider.WaterToWorld;
            Matrix4x4 worldToWater = waterToWorld.inverse;
            float normalTexelSize = Mathf.Max(1e-4f, waterProvider.NormalTexelSize);

            int gridX = Mathf.Clamp(Mathf.RoundToInt(waterSize.x / normalTexelSize), 1, 8192);
            int gridY = Mathf.Clamp(Mathf.RoundToInt(waterSize.y / normalTexelSize), 1, 8192);
            int instanceCount = gridX * gridY;
            if (instanceCount <= 0)
            {
                return false;
            }

            Vector3 sunDir = Vector3.down;
            if (manager.Sun != null)
            {
                sunDir = -manager.Sun.transform.forward;
            }

            if (sunDir.sqrMagnitude < 1e-6f)
            {
                sunDir = Vector3.down;
            }

            sunDir.Normalize();

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                _material.SetTexture(WaterNormalTexId, normalTex);
                _material.SetTexture(WaterHeightTexId, heightTex);
                _material.SetMatrix(WaterToWorldId, waterToWorld);
                _material.SetMatrix(WorldToWaterId, worldToWater);
                _material.SetVector(WaterSizeId, new Vector4(waterSize.x, waterSize.y, 1.0f / Mathf.Max(1e-4f, waterSize.x), 1.0f / Mathf.Max(1e-4f, waterSize.y)));
                _material.SetVector(WaterGridParamsId, new Vector4(gridX, gridY, 1.0f / Mathf.Max(1, gridX), 1.0f / Mathf.Max(1, gridY)));
                _material.SetFloat(WaterNormalTexelSizeId, normalTexelSize);
                _material.SetVector(SunDirId, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0.0f));
                _material.SetColor(TintId, manager.Tint);
                _material.SetFloat(IntensityId, manager.Intensity);
                _material.SetFloat(FresnelF0Id, manager.F0);
                _material.SetFloat(JacobianGainId, manager.JacobianGain);

                foreach (CausticsReceiverPlane receiver in receivers)
                {
                    if (receiver == null)
                    {
                        continue;
                    }

                    RenderTexture target = receiver.CausticsRT;
                    if (target == null)
                    {
                        continue;
                    }

                    if (!target.IsCreated())
                    {
                        target.Create();
                    }

                    cmd.SetRenderTarget(target);
                    cmd.ClearRenderTarget(false, true, Color.black);
                    cmd.SetViewport(new Rect(0, 0, target.width, target.height));

                    _material.SetMatrix(PlaneToWorldId, receiver.PlaneToWorld);
                    _material.SetMatrix(WorldToPlaneId, receiver.WorldToPlane);
                    _material.SetVector(PlaneSizeId, new Vector4(receiver.sizeMeters.x, receiver.sizeMeters.y, 1.0f / Mathf.Max(1e-4f, receiver.sizeMeters.x), 1.0f / Mathf.Max(1e-4f, receiver.sizeMeters.y)));
                    _material.SetVector(ReceiverResolutionId, new Vector4(target.width, target.height, 1.0f / Mathf.Max(1, target.width), 1.0f / Mathf.Max(1, target.height)));
                    _material.SetFloat(PlaneDistanceToleranceId, receiver.planeDistanceTolerance);
                    _material.SetFloat(PlaneTwoSidedId, receiver.twoSided ? 1.0f : 0.0f);

                    cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Points, instanceCount, 1);
                }
            }

            return true;
        }
    }
}
