using UnityEngine;

namespace CausticsReflective
{
    public class DummyWaterProvider : MonoBehaviour, IWaterSurfaceProvider
    {
        public Texture normalTex;
        public Texture heightTex;
        public Vector2 waterSizeMeters = new(8f, 8f);
        public Transform waterTransform;

        public Texture NormalTex => normalTex;
        public Texture HeightTex => heightTex;
        public Vector2 WaterSizeMeters => waterSizeMeters;
        public Matrix4x4 WaterToWorld => waterTransform ? waterTransform.localToWorldMatrix : Matrix4x4.identity;
        public float NormalTexelSize => normalTex ? 1.0f / Mathf.Max(1, normalTex.width) : 1.0f / 512f;
    }
}
