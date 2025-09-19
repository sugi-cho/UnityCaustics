using System.Collections.Generic;
using UnityEngine;

namespace CausticsReflective
{
    [DefaultExecutionOrder(-1000)]
    public class ReflectiveCausticsManager : MonoBehaviour
    {
        public static ReflectiveCausticsManager Instance { get; private set; }

        private static readonly List<CausticsReceiverPlane> ReceiversInternal = new();
        public static IReadOnlyList<CausticsReceiverPlane> Receivers => ReceiversInternal;

        [Tooltip("Implementation providing water surface data to the caustic passes.")]
        public MonoBehaviour waterProviderAdapter;

        public IWaterSurfaceProvider Water => waterProviderAdapter as IWaterSurfaceProvider;
        public Light Sun;
        [Range(0f, 1f)] public float F0 = 0.02f;
        public float Intensity = 1.0f;
        public float JacobianGain = 1.0f;
        public Color Tint = Color.white;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void Register(CausticsReceiverPlane receiver)
        {
            if (receiver == null || ReceiversInternal.Contains(receiver))
            {
                return;
            }

            ReceiversInternal.Add(receiver);
        }

        public static void Unregister(CausticsReceiverPlane receiver)
        {
            if (receiver == null)
            {
                return;
            }

            ReceiversInternal.Remove(receiver);
        }
    }
}
