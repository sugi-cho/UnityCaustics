using UnityEngine;

namespace CausticsReflective
{
    public interface IWaterSurfaceProvider
    {
        Texture NormalTex { get; }
        Texture HeightTex { get; }
        Vector2 WaterSizeMeters { get; }
        Matrix4x4 WaterToWorld { get; }
        float NormalTexelSize { get; }
    }
}
