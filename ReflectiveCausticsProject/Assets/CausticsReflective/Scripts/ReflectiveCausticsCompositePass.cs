using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CausticsReflective
{
    public class ReflectiveCausticsCompositePass : ScriptableRenderPass
    {
        private const int MaxReceivers = 4;

        private static readonly int ReceiverCountId = Shader.PropertyToID("_RC_ReceiverCount");
        private static readonly int WorldToPlaneId = Shader.PropertyToID("_RC_WorldToPlane");
        private static readonly int PlaneInfoId = Shader.PropertyToID("_RC_PlaneInfo");
        private static readonly int TintIntensityId = Shader.PropertyToID("_RC_TintIntensity");
        private static readonly int MultiplyBlendId = Shader.PropertyToID("_RC_MultiplyBlend");
        private static readonly int TempColorId = Shader.PropertyToID("_RC_TempColorTexture");
        private static readonly int[] CausticsTextureIds =
        {
            Shader.PropertyToID("_RC_CausticsTex0"),
            Shader.PropertyToID("_RC_CausticsTex1"),
            Shader.PropertyToID("_RC_CausticsTex2"),
            Shader.PropertyToID("_RC_CausticsTex3"),
        };

        private static readonly Matrix4x4[] WorldToPlaneBuffer = new Matrix4x4[MaxReceivers];
        private static readonly Vector4[] PlaneInfoBuffer = new Vector4[MaxReceivers];

        private readonly ProfilingSampler _profilingSampler = new("Reflective Caustics Composite");
        private readonly Material _material;

        private RenderTargetIdentifier _colorTarget;
        private RenderTargetIdentifier _depthTarget;
        private bool _multiplyBlend;
        private bool _tempColorAllocated;

        public ReflectiveCausticsCompositePass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(RenderTargetIdentifier colorTarget, RenderTargetIdentifier depthTarget, bool multiplyBlend)
        {
            _colorTarget = colorTarget;
            _depthTarget = depthTarget;
            _multiplyBlend = multiplyBlend;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(_colorTarget, _depthTarget);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                return;
            }

            var manager = ReflectiveCausticsManager.Instance;
            var receivers = ReflectiveCausticsManager.Receivers;

            if (manager == null || receivers == null || receivers.Count == 0)
            {
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                PopulateShaderData(cmd, manager, receivers);

                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.enableRandomWrite = false;
                cmd.GetTemporaryRT(TempColorId, descriptor, FilterMode.Bilinear);
                _tempColorAllocated = true;

                Blit(cmd, _colorTarget, TempColorId);

                cmd.SetGlobalFloat(MultiplyBlendId, _multiplyBlend ? 1f : 0f);
                Blit(cmd, TempColorId, _colorTarget, _material);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (_tempColorAllocated)
            {
                cmd.ReleaseTemporaryRT(TempColorId);
                _tempColorAllocated = false;
            }
        }

        private static void PopulateShaderData(CommandBuffer cmd, ReflectiveCausticsManager manager, IReadOnlyList<CausticsReceiverPlane> receivers)
        {
            var tintLinear = manager.Tint.linear;
            cmd.SetGlobalVector(TintIntensityId, new Vector4(tintLinear.r, tintLinear.g, tintLinear.b, manager.Intensity));

            var count = Mathf.Min(receivers.Count, MaxReceivers);
            for (var i = 0; i < MaxReceivers; i++)
            {
                if (i < count)
                {
                    var receiver = receivers[i];
                    WorldToPlaneBuffer[i] = receiver.WorldToPlane;
                    PlaneInfoBuffer[i] = new Vector4(receiver.sizeMeters.x, receiver.sizeMeters.y, receiver.planeDistanceTolerance, receiver.twoSided ? 1f : 0f);
                    var texture = receiver.CausticsRT != null ? receiver.CausticsRT : Texture2D.blackTexture;
                    cmd.SetGlobalTexture(CausticsTextureIds[i], texture);
                }
                else
                {
                    WorldToPlaneBuffer[i] = Matrix4x4.identity;
                    PlaneInfoBuffer[i] = Vector4.zero;
                    cmd.SetGlobalTexture(CausticsTextureIds[i], Texture2D.blackTexture);
                }
            }

            cmd.SetGlobalInt(ReceiverCountId, count);
            cmd.SetGlobalMatrixArray(WorldToPlaneId, WorldToPlaneBuffer);
            cmd.SetGlobalVectorArray(PlaneInfoId, PlaneInfoBuffer);
        }
    }
}
