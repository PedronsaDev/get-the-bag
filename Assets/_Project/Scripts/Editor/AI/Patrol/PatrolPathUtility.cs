using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Patrol.Editor
{
public enum PatrolIssueSeverity : byte
{
    Warning,
    Error
}

public readonly struct PatrolPathIssue
{
    public readonly PatrolIssueSeverity Severity;
    public readonly int PointIndex;
    public readonly string Message;

    public PatrolPathIssue(PatrolIssueSeverity severity, int pointIndex, string message)
    {
        Severity = severity;
        PointIndex = pointIndex;
        Message = message;
    }
}

public static class PatrolPathUtility
{
    private const float OnNavMeshTolerance = 0.5f;
    private const float DuplicatePointDistance = 0.5f;

    public static AIPatrolRoute CreateRoute(Vector3 position)
    {
        var routeObject = new GameObject("Patrol Route");
        Undo.RegisterCreatedObjectUndo(routeObject, "Create Patrol Route");
        routeObject.transform.position = TrySnap(position, 2f, out Vector3 snapped) ? snapped : position;
        return Undo.AddComponent<AIPatrolRoute>(routeObject);
    }

    public static int AddPoint(AIPatrolRoute route, Vector3 position, int index = -1)
    {
        int count = route.PointCount;
        int insertAt = index < 0 || index > count ? count : index;

        var point = new GameObject();
        Undo.RegisterCreatedObjectUndo(point, "Add Patrol Point");
        Undo.SetTransformParent(point.transform, route.transform, "Add Patrol Point");
        point.transform.position = position;
        point.transform.SetSiblingIndex(insertAt);

        FaceTravelDirection(route, insertAt);
        Renumber(route);
        return insertAt;
    }

    public static void RemovePoint(AIPatrolRoute route, int index)
    {
        if (!IsValidIndex(route, index))
            return;
        Undo.DestroyObjectImmediate(route.GetPoint(index).gameObject);
        Renumber(route);
    }

    public static void MovePoint(AIPatrolRoute route, int from, int to)
    {
        if (!IsValidIndex(route, from) || !IsValidIndex(route, to) || from == to)
            return;
        Undo.RegisterFullObjectHierarchyUndo(route.gameObject, "Reorder Patrol Point");
        route.GetPoint(from).SetSiblingIndex(to);
        Renumber(route);
    }

    public static void Reverse(AIPatrolRoute route)
    {
        int count = route.PointCount;
        if (count < 2)
            return;
        Undo.RegisterFullObjectHierarchyUndo(route.gameObject, "Reverse Patrol Route");
        for (int i = 0; i < count - 1; i++)
            route.GetPoint(count - 1).SetSiblingIndex(i);
        Renumber(route);
    }

    public static int SnapToNavMesh(AIPatrolRoute route, float radius)
    {
        int failed = 0;
        for (int i = 0; i < route.PointCount; i++)
        {
            var point = route.GetPoint(i);
            if (!TrySnap(point.position, radius, out Vector3 snapped))
            {
                failed++;
                continue;
            }
            Undo.RecordObject(point, "Snap Patrol Points");
            point.position = snapped;
        }
        return failed;
    }

    public static void FaceAlongPath(AIPatrolRoute route)
    {
        for (int i = 0; i < route.PointCount; i++)
            FaceTravelDirection(route, i);
    }

    public static bool TrySnap(Vector3 position, float radius, out Vector3 snapped)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            snapped = hit.position;
            return true;
        }
        snapped = position;
        return false;
    }

    public static int NearestSegmentStart(AIPatrolRoute route, Vector3 position)
    {
        int count = route.PointCount;
        if (count < 2)
            return count - 1;

        int segments = route.Loop ? count : count - 1;
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = route.GetPoint(i).position;
            Vector3 b = route.GetPoint((i + 1) % count).position;
            float distance = (position - ClosestPointOnSegment(a, b, position)).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    public static float PathLength(AIPatrolRoute route)
    {
        int count = route.PointCount;
        int segments = SegmentCount(route);
        float length = 0f;
        var path = new NavMeshPath();
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = route.GetPoint(i).position;
            Vector3 b = route.GetPoint((i + 1) % count).position;
            length += NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete
                ? CornerLength(path)
                : Vector3.Distance(a, b);
        }
        return length;
    }

    public static float EstimateCycleSeconds(AIPatrolRoute route, float speed)
    {
        if (route.PointCount == 0 || speed <= 0f)
            return 0f;

        float oneWay = PathLength(route);
        if (route.Loop || route.PointCount < 2)
            return oneWay / speed + route.PointCount * route.WaitSeconds;

        // Forward and back: every point is visited twice except the two ends.
        return 2f * oneWay / speed + (2 * route.PointCount - 2) * route.WaitSeconds;
    }

    public static List<PatrolPathIssue> Validate(AIPatrolRoute route)
    {
        var issues = new List<PatrolPathIssue>();
        int count = route.PointCount;
        if (count == 0)
        {
            issues.Add(new PatrolPathIssue(PatrolIssueSeverity.Error, -1, "The route has no waypoints."));
            return issues;
        }
        if (count == 1)
            issues.Add(new PatrolPathIssue(PatrolIssueSeverity.Warning, -1,
                "Only one waypoint: the guard will stand there and look around."));

        var onNavMesh = new bool[count];
        for (int i = 0; i < count; i++)
        {
            onNavMesh[i] = NavMesh.SamplePosition(route.GetPoint(i).position, out _, OnNavMeshTolerance, NavMesh.AllAreas);
            if (!onNavMesh[i])
                issues.Add(new PatrolPathIssue(PatrolIssueSeverity.Error, i,
                    $"Point {i + 1} is not on the NavMesh. Use Snap All or move it."));
        }

        var path = new NavMeshPath();
        int segments = SegmentCount(route);
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % count;
            if (next == i)
                continue;

            Vector3 a = route.GetPoint(i).position;
            Vector3 b = route.GetPoint(next).position;
            if ((a - b).sqrMagnitude < DuplicatePointDistance * DuplicatePointDistance)
                issues.Add(new PatrolPathIssue(PatrolIssueSeverity.Warning, i,
                    $"Points {i + 1} and {next + 1} are almost in the same place."));

            if (onNavMesh[i] && onNavMesh[next] &&
                (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete))
                issues.Add(new PatrolPathIssue(PatrolIssueSeverity.Error, i,
                    $"No complete path from point {i + 1} to point {next + 1}."));
        }
        return issues;
    }

    private static int SegmentCount(AIPatrolRoute route)
    {
        int count = route.PointCount;
        if (count < 2)
            return 0;
        return route.Loop ? count : count - 1;
    }

    private static bool IsValidIndex(AIPatrolRoute route, int index) => index >= 0 && index < route.PointCount;

    private static void Renumber(AIPatrolRoute route)
    {
        for (int i = 0; i < route.PointCount; i++)
        {
            var point = route.GetPoint(i).gameObject;
            string pointName = $"Point {i + 1:00}";
            if (point.name == pointName)
                continue;
            Undo.RecordObject(point, "Rename Patrol Point");
            point.name = pointName;
        }
    }

    // A waypoint faces the next one; the last point of a forward-and-back route faces back along the route.
    private static void FaceTravelDirection(AIPatrolRoute route, int index)
    {
        int count = route.PointCount;
        if (count < 2)
            return;

        int target = index + 1 < count ? index + 1 : (route.Loop ? 0 : index - 1);
        Vector3 direction = route.GetPoint(target).position - route.GetPoint(index).position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        var point = route.GetPoint(index);
        Undo.RecordObject(point, "Face Patrol Point");
        point.rotation = Quaternion.LookRotation(direction);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 segment = b - a;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr < 0.0001f)
            return a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, segment) / lengthSqr);
        return a + segment * t;
    }

    private static float CornerLength(NavMeshPath path)
    {
        var corners = path.corners;
        float length = 0f;
        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }
}
}
