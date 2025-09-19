using UnityEngine;

namespace CausticsReflective
{
    [ExecuteAlways]
    public class CausticsReceiverPlane : MonoBehaviour
    {
        public Vector2 sizeMeters = new(6f, 3f);
        public Vector2Int resolution = new(1024, 512);
        public bool twoSided = false;
        public float planeDistanceTolerance = 0.02f;

        private RenderTexture _rt;
        public RenderTexture CausticsRT => _rt;

        public Matrix4x4 PlaneToWorld => Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        public Matrix4x4 WorldToPlane => PlaneToWorld.inverse;

        private void OnEnable()
        {
            Allocate();
            ReflectiveCausticsManager.Register(this);
        }

        private void OnDisable()
        {
            ReflectiveCausticsManager.Unregister(this);
            Release();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ReallocateIfNeeded();
            }
        }

        private void Allocate()
        {
            Release();

            _rt = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.R16)
            {
                name = $"{name}_CausticsRT",
                enableRandomWrite = true,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp
            };

            _rt.Create();
        }

        private void ReallocateIfNeeded()
        {
            if (_rt == null || _rt.width != resolution.x || _rt.height != resolution.y)
            {
                Allocate();
            }
        }

        private void Release()
        {
            if (_rt == null)
            {
                return;
            }

            _rt.Release();
#if UNITY_EDITOR
            DestroyImmediate(_rt);
#else
            Destroy(_rt);
#endif
            _rt = null;
        }
    }
}
