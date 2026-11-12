using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.Outline
{
    // Only owned copies are modified. The last submesh covers the entire object.
    public static class OutlineMeshPreparation
    {
        // Longest corner extrusion relative to the configured width. A cube corner needs
        // sqrt(3) (about 1.73); the cap keeps thin wedges from producing long spikes.
        public const float MaxMiterLength = 2f;

        // UV channel 3 (UV4 in 1-based naming) carries the extrusion data read by the
        // Fill shader as TEXCOORD3.
        private const int ExtrusionChannel = 3;
        private const float SameNormalDot = 0.999f;
        private const float CancelledSumSqr = 1e-8f;

        public static Mesh Create(Mesh source, int materialSlots, bool skinned, Vector3 scaleRatio)
        {
            if (!source || !source.isReadable || source.subMeshCount == 0)
                throw new InvalidOperationException("Outline preparation requires a readable mesh or a baked outline mesh.");

            for (int i = 0; i < source.subMeshCount; i++)
                if (source.GetTopology(i) != MeshTopology.Triangles)
                    throw new InvalidOperationException("MeshOutline supports triangle meshes.");

            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                copy.name = source.name + " (Outline)";
                copy.hideFlags = HideFlags.DontSave;
                int baseSlots = Mathf.Max(source.subMeshCount, materialSlots);
                var triangles = new List<int>();
                for (int i = 0; i < source.subMeshCount; i++)
                    triangles.AddRange(source.GetTriangles(i));
                copy.subMeshCount = baseSlots + 1;
                for (int i = source.subMeshCount; i < baseSlots; i++)
                    copy.SetTriangles(source.GetTriangles(source.subMeshCount - 1), i, false);
                copy.SetTriangles(triangles, baseSlots, false);
                // Skinning does not deform UV channels, so skinned copies leave the channel
                // zeroed and the shader falls back to the deformed native normals.
                var extrusion = skinned ? new Vector4[source.vertexCount] : ExtrusionData(source, scaleRatio);
                copy.SetUVs(ExtrusionChannel, extrusion);
                return copy;
            }
            catch
            {
                Destroy(copy);
                throw;
            }
        }

#if UNITY_EDITOR
        // A readable author-time copy. Extrusion data is generated at runtime per scale
        // ratio, so the copy only has to make the mesh data CPU-readable.
        public static Mesh Bake(Mesh source)
        {
            if (!source || source.subMeshCount == 0) throw new InvalidOperationException("Select a mesh with triangles.");
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + " (Prepared Outline)";
            return copy;
        }
#endif

        // Per vertex: xyz is the smoothed outward direction and w the miter length that
        // keeps every adjacent face's outline at the configured width. Directions are
        // combined in the renderer's scaled space, so non-uniform scale does not skew
        // them. A zero vector tells the shader to use the native normal instead.
        private static Vector4[] ExtrusionData(Mesh mesh, Vector3 scaleRatio)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var result = new Vector4[vertices.Length];
            if (normals.Length != vertices.Length) return result;

            var normalsByPosition = GroupDistinctScaledNormals(vertices, normals, scaleRatio);
            var extrusionByPosition = new Dictionary<Vector3, Vector4>(normalsByPosition.Count);
            foreach (var group in normalsByPosition)
                extrusionByPosition.Add(group.Key, Extrusion(group.Value, scaleRatio));

            for (int i = 0; i < vertices.Length; i++)
                result[i] = extrusionByPosition[vertices[i]];
            return result;
        }

        // Hard edges split one corner into several vertices with different normals. Each
        // distinct face normal counts once, so UV seams (duplicate vertices with the same
        // normal) do not pull the averaged direction toward one face.
        private static Dictionary<Vector3, List<Vector3>> GroupDistinctScaledNormals(Vector3[] vertices, Vector3[] normals, Vector3 scaleRatio)
        {
            var groups = new Dictionary<Vector3, List<Vector3>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!groups.TryGetValue(vertices[i], out var group))
                {
                    group = new List<Vector3>(4);
                    groups.Add(vertices[i], group);
                }
                var scaledNormal = ToScaledSpaceNormal(normals[i], scaleRatio);
                if (scaledNormal != Vector3.zero && !ContainsSimilar(group, scaledNormal))
                    group.Add(scaledNormal);
            }
            return groups;
        }

        // Normals transform by the inverse scale: n becomes normalize(n / scale).
        private static Vector3 ToScaledSpaceNormal(Vector3 normal, Vector3 scaleRatio)
        {
            return new Vector3(normal.x / scaleRatio.x, normal.y / scaleRatio.y, normal.z / scaleRatio.z).normalized;
        }

        private static bool ContainsSimilar(List<Vector3> normals, Vector3 candidate)
        {
            foreach (var normal in normals)
                if (Vector3.Dot(normal, candidate) > SameNormalDot) return true;
            return false;
        }

        private static Vector4 Extrusion(List<Vector3> faceNormals, Vector3 scaleRatio)
        {
            var sum = Vector3.zero;
            foreach (var normal in faceNormals) sum += normal;
            if (sum.sqrMagnitude < CancelledSumSqr) return Vector4.zero; // Opposing faces (thin walls) cancel out.

            var direction = sum.normalized;
            float miterLength = MiterLength(direction, faceNormals);

            // The shader transforms xyz as a normal, by the inverse transpose of
            // (rotation * scale). Applied to (scale * direction) that yields
            // (rotation * direction): the scaled-space direction, rotated into the world.
            var encoded = Vector3.Scale(direction, scaleRatio);
            return new Vector4(encoded.x, encoded.y, encoded.z, miterLength);
        }

        // Moving a vertex by L along the direction moves each adjacent face plane by
        // L * dot(direction, faceNormal). L = 1 / smallest dot gives every face at least
        // the full width; smooth surfaces (a single normal) keep L = 1.
        private static float MiterLength(Vector3 direction, List<Vector3> faceNormals)
        {
            float smallestDot = 1f;
            foreach (var normal in faceNormals)
                smallestDot = Mathf.Min(smallestDot, Vector3.Dot(direction, normal));
            return 1f / Mathf.Max(smallestDot, 1f / MaxMiterLength);
        }

        internal static void Destroy(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
