using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Cover.Editor
{
public readonly struct BoundaryEdge
{
    public readonly Vector3 A;
    public readonly Vector3 B;
    public readonly Vector3 InteriorPoint;

    public BoundaryEdge(Vector3 a, Vector3 b, Vector3 interiorPoint)
    {
        A = a;
        B = b;
        InteriorPoint = interiorPoint;
    }

    /// <summary>Horizontal direction pointing out of the NavMesh, toward whatever blocks it.</summary>
    public Vector3 Outward
    {
        get
        {
            Vector3 along = (B - A).normalized;
            Vector3 outward = Vector3.Cross(Vector3.up, along);
            if (Vector3.Dot(outward, InteriorPoint - A) > 0f)
                outward = -outward;
            outward.y = 0f;
            return outward.normalized;
        }
    }
}

/// <summary>Finds the edges of the baked NavMesh. Agents cannot walk past them, so walls and drops show up here.</summary>
public static class NavMeshBoundary
{
    private const float WeldPrecision = 100f;

    public static void Collect(Bounds area, List<BoundaryEdge> edges)
    {
        edges.Clear();
        var triangulation = NavMesh.CalculateTriangulation();
        var vertices = triangulation.vertices;
        var indices = triangulation.indices;
        if (vertices.Length == 0)
            return;

        var points = new List<Vector3>(vertices.Length);
        var weldedIndex = WeldVertices(vertices, points);

        var seen = new Dictionary<ulong, EdgeUse>(indices.Length);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = weldedIndex[indices[i]];
            int b = weldedIndex[indices[i + 1]];
            int c = weldedIndex[indices[i + 2]];
            if (a == b || b == c || a == c)
                continue;

            CountEdge(seen, a, b, c);
            CountEdge(seen, b, c, a);
            CountEdge(seen, c, a, b);
        }

        foreach (var pair in seen)
        {
            if (pair.Value.Count != 1)
                continue;

            int a = (int)(pair.Key >> 32);
            int b = (int)(pair.Key & 0xFFFFFFFF);
            Vector3 middle = (points[a] + points[b]) * 0.5f;
            if (area.Contains(middle))
                edges.Add(new BoundaryEdge(points[a], points[b], points[pair.Value.Third]));
        }
    }

    private struct EdgeUse
    {
        public int Count;
        public int Third;
    }

    // The triangulation repeats vertices where tiles meet; welding them makes shared edges match.
    private static int[] WeldVertices(Vector3[] vertices, List<Vector3> welded)
    {
        var lookup = new Dictionary<Vector3Int, int>(vertices.Length);
        var map = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var key = Vector3Int.RoundToInt(vertices[i] * WeldPrecision);
            if (!lookup.TryGetValue(key, out int index))
            {
                index = welded.Count;
                lookup.Add(key, index);
                welded.Add(vertices[i]);
            }
            map[i] = index;
        }
        return map;
    }

    private static void CountEdge(Dictionary<ulong, EdgeUse> seen, int a, int b, int third)
    {
        int low = a < b ? a : b;
        int high = a < b ? b : a;
        ulong key = ((ulong)low << 32) | (uint)high;
        seen.TryGetValue(key, out EdgeUse use);
        use.Count++;
        use.Third = third;
        seen[key] = use;
    }
}
}
