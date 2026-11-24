using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Cover.Editor
{
public static class CoverBakerEngine
{
    public const string ContainerName = "[CoverPoints]";

    public static bool DrawPoints { get; private set; } = true;

    public static void SetDrawPoints(bool draw) => DrawPoints = draw;

    public static void Collect(GameObject root, CoverBakeSettings settings, List<CoverCandidate> results)
    {
        results.Clear();
        if (!TryGetObstacleBounds(root, settings, out Bounds area))
            return;

        area.Expand(settings.WallSearchDistance * 2f + 1f);
        var edges = new List<BoundaryEdge>();
        NavMeshBoundary.Collect(area, edges);

        var all = new List<CoverCandidate>();
        foreach (var edge in edges)
            SampleEdge(edge, settings, all);

        SelectSpaced(all, settings.MinSpacing, results);
    }

    public static int Bake(GameObject root, CoverBakeSettings settings)
    {
        var candidates = new List<CoverCandidate>();
        Collect(root, settings, candidates);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Bake Cover Points");
        int group = Undo.GetCurrentGroup();

        ClearCoverPoints(root);
        if (candidates.Count > 0)
        {
            Transform container = CreateContainer(root.transform);
            for (int i = 0; i < candidates.Count; i++)
                CreateCoverPointObject(container, candidates[i], i);
        }

        Undo.CollapseUndoOperations(group);
        return candidates.Count;
    }

    public static void ClearCoverPoints(GameObject root)
    {
        Transform container = root.transform.Find(ContainerName);
        if (container)
            Undo.DestroyObjectImmediate(container.gameObject);
    }

    private static bool TryGetObstacleBounds(GameObject root, CoverBakeSettings settings, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            if (collider.isTrigger || (settings.ObstacleMask.value & (1 << collider.gameObject.layer)) == 0)
                continue;

            if (found)
            {
                bounds.Encapsulate(collider.bounds);
            }
            else
            {
                bounds = collider.bounds;
                found = true;
            }
        }
        return found;
    }

    private static void SampleEdge(BoundaryEdge edge, CoverBakeSettings settings, List<CoverCandidate> results)
    {
        Vector3 vector = edge.B - edge.A;
        float length = vector.magnitude;
        if (length < 0.05f)
            return;

        Vector3 along = vector / length;
        Vector3 outward = edge.Outward;
        int samples = Mathf.Max(1, Mathf.FloorToInt(length / settings.StepInterval));
        for (int i = 0; i < samples; i++)
        {
            float distance = (i + 0.5f) / samples * length;
            if (CoverCandidateEvaluator.TryEvaluate(edge.A + along * distance, outward, settings, out var candidate))
                results.Add(candidate);
        }
    }

    // Best candidates first, so a corner with a peek wins over a plain stretch of wall next to it.
    private static void SelectSpaced(List<CoverCandidate> all, float minSpacing, List<CoverCandidate> results)
    {
        all.Sort(CompareByQuality);
        float minSqr = minSpacing * minSpacing;
        foreach (var candidate in all)
        {
            bool tooClose = false;
            foreach (var kept in results)
            {
                if ((kept.Position - candidate.Position).sqrMagnitude < minSqr)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose)
                results.Add(candidate);
        }
    }

    private static int CompareByQuality(CoverCandidate a, CoverCandidate b)
    {
        int byQuality = b.Quality.CompareTo(a.Quality);
        if (byQuality != 0)
            return byQuality;
        int byX = a.Position.x.CompareTo(b.Position.x);
        return byX != 0 ? byX : a.Position.z.CompareTo(b.Position.z);
    }

    private static Transform CreateContainer(Transform root)
    {
        var go = new GameObject(ContainerName);
        Undo.RegisterCreatedObjectUndo(go, "Create Cover Container");
        go.transform.SetParent(root, false);
        return go.transform;
    }

    private static void CreateCoverPointObject(Transform parent, CoverCandidate candidate, int index)
    {
        var go = new GameObject($"Cover_{candidate.Height}_{index:000}");
        Undo.RegisterCreatedObjectUndo(go, "Bake Cover Point");
        go.transform.SetParent(parent, true);
        go.transform.position = candidate.Position;
        go.transform.rotation = Quaternion.LookRotation(candidate.Normal, Vector3.up);

        var point = go.AddComponent<CoverPoint>();
        var serialized = new SerializedObject(point);
        serialized.FindProperty("_height").enumValueIndex = (int)candidate.Height;
        serialized.FindProperty("_availablePeeks").intValue = (int)candidate.Peeks;
        serialized.FindProperty("_leftLocalOffset").floatValue = candidate.LeftOffset;
        serialized.FindProperty("_rightLocalOffset").floatValue = candidate.RightOffset;
        serialized.FindProperty("_quality").floatValue = candidate.Quality;
        serialized.FindProperty("_forward").vector3Value = Vector3.forward;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Pickable)]
    private static void DrawUnselectedCoverPoint(CoverPoint point, GizmoType gizmoType)
    {
        if (!DrawPoints)
            return;

        Gizmos.color = point.Height == CoverHeight.High
            ? new Color(0.2f, 0.8f, 1f, 0.5f)
            : new Color(0.3f, 1f, 0.5f, 0.5f);

        Gizmos.DrawWireSphere(point.Position, 0.25f);
        Gizmos.DrawRay(point.Position, point.Forward * 0.6f);
    }
}
}
