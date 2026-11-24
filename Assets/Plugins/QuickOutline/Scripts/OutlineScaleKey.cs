using UnityEngine;

namespace Plugins.Outline
{
    // Smoothed outline directions depend on the renderer's scale ratio (non-uniform
    // scale changes how adjacent face normals combine), so owned meshes are built per
    // quantized ratio. Uniform scale does not change directions: every uniformly scaled
    // renderer shares the Uniform key and therefore the same owned mesh.
    internal static class OutlineScaleKey
    {
        private const int Steps = 100;
        private const float NegligibleScale = 1e-6f;

        public static readonly Vector3Int Uniform = new Vector3Int(Steps, Steps, Steps);

        // Column lengths of the matrix are the world scale per local axis. Exact for
        // rotation and scale; an approximation under parent shear.
        public static Vector3Int From(Matrix4x4 localToWorld)
        {
            float x = new Vector3(localToWorld.m00, localToWorld.m10, localToWorld.m20).magnitude;
            float y = new Vector3(localToWorld.m01, localToWorld.m11, localToWorld.m21).magnitude;
            float z = new Vector3(localToWorld.m02, localToWorld.m12, localToWorld.m22).magnitude;
            float largest = Mathf.Max(x, Mathf.Max(y, z));
            if (largest < NegligibleScale) return Uniform;
            return new Vector3Int(Quantize(x / largest), Quantize(y / largest), Quantize(z / largest));
        }

        public static Vector3 ToRatio(Vector3Int key) => new Vector3(key.x, key.y, key.z) / Steps;

        private static int Quantize(float ratio) => Mathf.Clamp(Mathf.RoundToInt(ratio * Steps), 1, Steps);
    }
}
