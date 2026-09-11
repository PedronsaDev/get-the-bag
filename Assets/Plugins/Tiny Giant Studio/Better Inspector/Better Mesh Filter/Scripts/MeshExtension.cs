using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;

#endif

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    /// This script serves two purposes.
    /// 1. Gather information about mesh
    /// 2. Perform the actions in the Action Foldout in the inspector
    /// </summary>
    public static class MeshExtension
    {
        #region Information

        //CustomPatch: created new extension method
        /// <summary>
        /// Gets all triangles from a mesh and all its submeshes without repeatedly allocating mesh related memory.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="outTriangleList">A list that will be populated with the output data. It will be automatically cleared internally</param>
        public static void GetAllTriangles(this Mesh mesh, List<int> outTriangleList)
        {
            outTriangleList.Clear();
            if (mesh == null || mesh.vertexCount == 0) return;

            using var _ = ListPool<int>.Get(out List<int> submeshTriangleList);

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetSubMesh(i).topology != MeshTopology.Triangles) continue;

                mesh.GetTriangles(submeshTriangleList, submesh: i);
                outTriangleList.AddRange(submeshTriangleList);
            }
        }

        //CustomPatch: fixed memory allocation method adding to editor GC pressure
        public static void SubMeshVertexCount(this Mesh mesh, List<int> outVertexCountList)
        {
            outVertexCountList.Clear();
            if (mesh == null || mesh.vertexCount == 0) return;

            for (int i = 0; i < mesh.subMeshCount; i++)
                outVertexCountList.Add(mesh.GetSubMesh(i).vertexCount);
        }

        //CustomPatch: fixed high memory allocation method causing editor GC pressure for large meshes and it was also much slower
        /// <summary>
        /// Returns the total number of triangles in the mesh by summing up the triangle count of all submeshes.<br/>
        /// If any submesh is not using triangle topology, it returns 0.<br/>
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static int TrianglesCount(this Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0) return 0;

            int totalIndexCount = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetSubMesh(i).topology != MeshTopology.Triangles) continue;

                totalIndexCount += (int)mesh.GetIndexCount(submesh: i);
            }

            return totalIndexCount / 3;
        }

        //CustomPatch: created new extension method
        public static int GetTangentCount(this Mesh mesh)
        {
            if (mesh == null) return 0;

            return mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent) ? mesh.vertexCount : 0;
        }

        //CustomPatch: fixed repeated high memory allocations
        public static int EdgeCount(this Mesh mesh)
        {
            var _ = HashSetPool<Edge>.Get(out HashSet<Edge> uniqueEdgesSet);
            using var __ = ListPool<int>.Get(out List<int> triangleList);
            mesh.GetAllTriangles(triangleList);

            int triangleCount = triangleList.Count;
            for (int i = 0; i < triangleCount; i += 3)
            {
                Edge edge1 = new(triangleList[i], triangleList[i + 1]);
                Edge edge2 = new(triangleList[i + 1], triangleList[i + 2]);
                Edge edge3 = new(triangleList[i + 2], triangleList[i]);

                uniqueEdgesSet.Add(edge1);
                uniqueEdgesSet.Add(edge2);
                uniqueEdgesSet.Add(edge3);
            }

            return uniqueEdgesSet.Count;
        }

        readonly struct Edge : IEquatable<Edge>
        {
            readonly int _vertexIndexA;
            readonly int _vertexIndexB;

            public Edge(int vertexIndexA, int vertexIndexB)
            {
                _vertexIndexA = Mathf.Min(vertexIndexA, vertexIndexB);
                _vertexIndexB = Mathf.Max(vertexIndexA, vertexIndexB);
            }

            public override int GetHashCode() => HashCode.Combine(_vertexIndexA, _vertexIndexB); //CustomPatch: using System.HashCode for better and SAFER hash distribution and performance compared to the previous implementation

            public override bool Equals(object obj)
            {
                if (obj is not Edge other) return false;
                return _vertexIndexA == other._vertexIndexA && _vertexIndexB == other._vertexIndexB;
            }

            public bool Equals(Edge other) =>
                _vertexIndexA == other._vertexIndexA && _vertexIndexB == other._vertexIndexB;
        }

        public static int FaceCount(this Mesh mesh) => mesh.TrianglesCount() ; //CustomPatch: fixed high memory allocation => used own extension method instead because it returns the same thing

#if UNITY_EDITOR

        public static Bounds MeshSizeEditorOnly(this Mesh mesh, float unit = 1)
        {
            Bounds newBound = mesh.bounds;
            newBound.size *= unit;
            newBound.center *= unit;

            return newBound;
        }

#endif

        #endregion Information

        #region Functions

        //CustomPatch: fixed high memory allocation method causing editor GC pressure for large meshes also causing slower performance
        /// <summary>
        /// Flips the direction of the normals
        /// </summary>
        public static Mesh FlipNormals(this Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0) return mesh;

            using var _ = ListPool<Vector3>.Get(out List<Vector3> meshNormalsList);
            int normalsCount = meshNormalsList.Count;
            for (int i = 0; i < normalsCount; i++)
                meshNormalsList[i] = -meshNormalsList[i];

            mesh.SetNormals(meshNormalsList);

            using var __ = ListPool<int>.Get(out List<int> subMeshTrianglesList);
            int subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
            {
                subMeshTrianglesList.Clear();
                mesh.GetTriangles(subMeshTrianglesList, submesh: i);

                int trianglesCount = subMeshTrianglesList.Count;
                for (int j = 0; j < trianglesCount; j += 3)
                    (subMeshTrianglesList[j], subMeshTrianglesList[j + 2]) = (subMeshTrianglesList[j + 2], subMeshTrianglesList[j]);

                mesh.SetTriangles(subMeshTrianglesList, submesh: i);
            }

            return mesh;
        }

#if UNITY_EDITOR

        /// <summary>
        /// Editor only
        /// </summary>
        public static Mesh ExportMesh(this Mesh mesh)
        {
            string path = EditorUtility.SaveFilePanel("Save mesh", "Assets/", mesh.name, "asset");
            if (string.IsNullOrEmpty(path)) return mesh;

            path = FileUtil.GetProjectRelativePath(path);

            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
                mesh = Object.Instantiate(mesh);

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();

            return mesh;
        }

#endif

        //This is working. Commented out because it was unused.
        // public static Mesh SubDivide(this Mesh mesh)
        // {
        //     Vector3[] vertices = mesh.vertices;
        //     int[] triangles = mesh.triangles;
        //     Vector3[] normals = mesh.normals;
        //     Vector2[] uv = mesh.uv;
        //
        //     int newTriangleCount = triangles.Length * 4;
        //     int[] newTriangles = new int[newTriangleCount];
        //     Vector3[] newVertices = new Vector3[vertices.Length * 4];
        //     Vector3[] newNormals = new Vector3[normals.Length * 4];
        //     Vector2[] newUV = new Vector2[uv.Length * 4];
        //
        //     int triangleIndex = 0;
        //     int vertexIndex = 0;
        //
        //     for (int i = 0; i < triangles.Length; i += 3)
        //     {
        //         int a = triangles[i];
        //         int b = triangles[i + 1];
        //         int c = triangles[i + 2];
        //
        //         Vector3 ab = (vertices[a] + vertices[b]) * 0.5f;
        //         Vector3 bc = (vertices[b] + vertices[c]) * 0.5f;
        //         Vector3 ca = (vertices[c] + vertices[a]) * 0.5f;
        //
        //         newVertices[vertexIndex] = vertices[a];
        //         newVertices[vertexIndex + 1] = vertices[b];
        //         newVertices[vertexIndex + 2] = vertices[c];
        //         newVertices[vertexIndex + 3] = ab;
        //         newVertices[vertexIndex + 4] = bc;
        //         newVertices[vertexIndex + 5] = ca;
        //
        //         newTriangles[triangleIndex] = vertexIndex;
        //         newTriangles[triangleIndex + 1] = vertexIndex + 3;
        //         newTriangles[triangleIndex + 2] = vertexIndex + 5;
        //
        //         newTriangles[triangleIndex + 3] = vertexIndex + 1;
        //         newTriangles[triangleIndex + 4] = vertexIndex + 4;
        //         newTriangles[triangleIndex + 5] = vertexIndex + 3;
        //
        //         newTriangles[triangleIndex + 6] = vertexIndex + 2;
        //         newTriangles[triangleIndex + 7] = vertexIndex + 5;
        //         newTriangles[triangleIndex + 8] = vertexIndex + 4;
        //
        //         newTriangles[triangleIndex + 9] = vertexIndex + 3;
        //         newTriangles[triangleIndex + 10] = vertexIndex + 4;
        //         newTriangles[triangleIndex + 11] = vertexIndex + 5;
        //
        //         triangleIndex += 12;
        //         vertexIndex += 6;
        //     }
        //
        //     mesh.vertices = newVertices;
        //     mesh.triangles = newTriangles;
        //     mesh.normals = newNormals;
        //     mesh.uv = newUV;
        //
        //     mesh.RecalculateNormals();
        //     mesh.RecalculateBounds();
        //
        //     return mesh;
        // }

        #endregion Functions
    }
}