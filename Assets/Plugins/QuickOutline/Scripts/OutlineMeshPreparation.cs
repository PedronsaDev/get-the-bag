using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.Outline
{
    // Only owned copies are modified. The last submesh covers the entire object.
    public static class OutlineMeshPreparation
    {
        public static Mesh Create(Mesh source, int materialSlots, bool skinned, bool preparedNormals = false)
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
                if (skinned) copy.SetUVs(3, new Vector3[source.vertexCount]);
                else if (!preparedNormals) copy.SetUVs(3, SmoothNormals(source));
                return copy;
            }
            catch
            {
                Destroy(copy);
                throw;
            }
        }

#if UNITY_EDITOR
        public static Mesh Bake(Mesh source)
        {
            if (!source || source.subMeshCount == 0) throw new InvalidOperationException("Select a mesh with triangles.");
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                copy.name = source.name + " (Prepared Outline)";
                copy.SetUVs(3, SmoothNormals(source));
                return copy;
            }
            catch { Destroy(copy); throw; }
        }
#endif

        private static Vector3[] SmoothNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals.Length != vertices.Length)
                return new Vector3[vertices.Length]; // Shader falls back to native normals.

            var groups = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                groups.TryGetValue(vertices[i], out var sum);
                groups[vertices[i]] = sum + normals[i];
            }
            var result = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                result[i] = groups[vertices[i]].normalized;
            return result;
        }

        internal static void Destroy(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
