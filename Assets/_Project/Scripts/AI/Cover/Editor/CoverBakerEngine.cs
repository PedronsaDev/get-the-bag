using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Cover.Editor
{

public static class CoverBakerEngine
{
    public struct BakeSettings
    {
        public LayerMask ObstacleMask;
        public float StepInterval; 
        public float StandoffDistance; 
        public float LowCoverHeight; 
        public float HighCoverHeight; 
        public float MinSpacingBetweenPointsSqr; 
    }
    
    private struct FaceSegment
    {
        public Vector3 Start;
        public Vector3 End;
        public Vector3 InwardDir;
    }

    public static int BakeForHierarchy(GameObject root, BakeSettings settings)
    {
        var colliders = root.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0) return 0;

        Transform container = GetOrCreateContainer(root.transform);

        List<Vector3> createdPositions = new();
        int totalCreated = 0;

        foreach (var col in colliders)
        {
            if ((settings.ObstacleMask.value & (1 << col.gameObject.layer)) == 0)
                continue;

            totalCreated += BakeCollider(col, container, settings, createdPositions);
        }

        return totalCreated;
    }

    private static int BakeCollider(Collider col, Transform container, BakeSettings settings, List<Vector3> existingPoints)
{
    Bounds b = col.bounds;
    float y = b.min.y + 0.4f;
    float probeOffset = 0.5f;
    int count = 0;

    var faces = new FaceSegment[]
    {
        new() { Start = new Vector3(b.min.x, y, b.max.z + probeOffset), End = new Vector3(b.max.x, y, b.max.z + probeOffset), InwardDir = Vector3.back },
        new() { Start = new Vector3(b.min.x, y, b.min.z - probeOffset), End = new Vector3(b.max.x, y, b.min.z - probeOffset), InwardDir = Vector3.forward },
        new() { Start = new Vector3(b.max.x + probeOffset, y, b.min.z), End = new Vector3(b.max.x + probeOffset, y, b.max.z), InwardDir = Vector3.left },
        new() { Start = new Vector3(b.min.x - probeOffset, y, b.min.z), End = new Vector3(b.min.x - probeOffset, y, b.max.z), InwardDir = Vector3.right }
    };

    foreach (var face in faces)
    {
        float length = Vector3.Distance(face.Start, face.End);
        Vector3 stepDir = (face.End - face.Start).normalized;
        
        float currentDist = settings.StepInterval * 0.5f;

        while (currentDist < length)
        {
            Vector3 origin = face.Start + stepDir * currentDist;
            currentDist += settings.StepInterval;

            if (!Physics.Raycast(origin, face.InwardDir, out RaycastHit hit, probeOffset * 2.5f, settings.ObstacleMask))
                continue;

            if (hit.collider != col)
                continue;

            Vector3 pStandoff = hit.point + hit.normal * settings.StandoffDistance;
            if (!NavMesh.SamplePosition(pStandoff, out NavMeshHit navHit, 0.6f, NavMesh.AllAreas))
                continue;

            Vector3 pCover = navHit.position;

            if (IsTooCloseToExisting(pCover, existingPoints, settings.MinSpacingBetweenPointsSqr))
                continue;

            Vector3 lowProbe = pCover + Vector3.up * settings.LowCoverHeight;
            Vector3 highProbe = pCover + Vector3.up * settings.HighCoverHeight;

            bool lowHits = Physics.Raycast(lowProbe, -hit.normal, 1.0f, settings.ObstacleMask);
            if (!lowHits)
                continue;

            bool highHits = Physics.Raycast(highProbe, -hit.normal, 1.0f, settings.ObstacleMask);
            CoverHeight height = highHits ? CoverHeight.High : CoverHeight.Low;
            CoverPeekDirection peeks = CoverPeekDirection.None;

            if (height == CoverHeight.Low)
                peeks |= CoverPeekDirection.Over;

            Vector3 rightDir = Vector3.Cross(Vector3.up, hit.normal).normalized;
            Vector3 leftDir = -rightDir;

            Vector3 rightProbe = pCover + (rightDir * 0.65f) + (Vector3.up * 1.0f);
            if (!Physics.Raycast(rightProbe, -hit.normal, 1.0f, settings.ObstacleMask))
                peeks |= CoverPeekDirection.Right;

            Vector3 leftProbe = pCover + (leftDir * 0.65f) + (Vector3.up * 1.0f);
            if (!Physics.Raycast(leftProbe, -hit.normal, 1.0f, settings.ObstacleMask))
                peeks |= CoverPeekDirection.Left;

            CreateCoverPointObject(container, pCover, hit.normal, height, peeks);
            existingPoints.Add(pCover);
            count++;
        }
    }

    return count;
}

    private static bool IsTooCloseToExisting(Vector3 pos, List<Vector3> existing, float minSqrDist)
    {
        for (int i = 0; i < existing.Count; i++)
        {
            if ((existing[i] - pos).sqrMagnitude < minSqrDist)
                return true;
        }
        return false;
    }

    private static void CreateCoverPointObject(Transform parent, Vector3 position, Vector3 forward, CoverHeight height, CoverPeekDirection peeks)
    {
        var go = new GameObject($"CoverPoint_{height}");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        go.transform.SetParent(parent, true);

        var cp = go.AddComponent<CoverPoint>();

        var so = new SerializedObject(cp);
        so.FindProperty("_height").enumValueIndex = (int)height;
        so.FindProperty("_availablePeeks").intValue = (int)peeks;
        so.FindProperty("_forward").vector3Value = Vector3.forward;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(go, "Bake Cover Point");
    }

    private static Transform GetOrCreateContainer(Transform root)
    {
        Transform container = root.Find("[CoverPoints]");
        if (!container)
        {
            var go = new GameObject("[CoverPoints]");
            go.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(go, "Create Cover Container");
            container = go.transform;
        }

        return container;
    }

    public static void ClearCoverPoints(GameObject root)
    {
        Transform container = root.transform.Find("[CoverPoints]");
        if (container)
        {
            Undo.DestroyObjectImmediate(container.gameObject);
        }
    }
    
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Pickable)]
    private static void DrawUnselectedCoverPoint(CoverPoint point, GizmoType gizmoType)
    {
        Gizmos.color = point.Height == CoverHeight.High 
            ? new Color(0.2f, 0.8f, 1f, 0.5f) 
            : new Color(0.3f, 1f, 0.5f, 0.5f);

        Gizmos.DrawWireSphere(point.Position, 0.25f);
        Gizmos.DrawRay(point.Position, point.Forward * 0.6f);
    }
}
}
