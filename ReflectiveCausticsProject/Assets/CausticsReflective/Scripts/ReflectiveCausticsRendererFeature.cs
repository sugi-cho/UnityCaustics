using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CausticsReflective
{
    public class ReflectiveCausticsRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Shader used to generate reflective caustics.")]
            public Shader genShader;

            [Tooltip("Render pass event for the caustics generation pass.")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public Settings settings = new();

        private ReflectiveCausticsGenPass _genPass;
        private Material _genMaterial;

        public override void Create()
        {
            if (settings.genShader == null)
            {
                settings.genShader = Shader.Find("CausticsReflective/ReflectiveCausticsGen");
            }

            if (settings.genShader != null)
            {
                _genMaterial = CoreUtils.CreateEngineMaterial(settings.genShader);
            }

            _genPass = new ReflectiveCausticsGenPass
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_genPass == null)
            {
                return;
            }

            if (_genMaterial == null && settings.genShader != null)
            {
                _genMaterial = CoreUtils.CreateEngineMaterial(settings.genShader);
            }

            if (_genMaterial == null)
            {
                return;
            }

            _genPass.Setup(_genMaterial);
            renderer.EnqueuePass(_genPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_genMaterial != null)
            {
                CoreUtils.Destroy(_genMaterial);
                _genMaterial = null;
            }
        }
    }
}
