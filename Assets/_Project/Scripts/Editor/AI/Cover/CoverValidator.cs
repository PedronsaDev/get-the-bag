using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityPhysics = UnityEngine.Physics;

namespace Project.AI.Cover.Editor
{
public enum CoverIssueSeverity : byte
{
    Warning,
    Error
}

public readonly struct CoverIssue
{
    public readonly CoverIssueSeverity Severity;
    public readonly CoverPoint Point;
    public readonly string Message;

    public CoverIssue(CoverIssueSeverity severity, CoverPoint point, string message)
    {
        Severity = severity;
        Point = point;
        Message = message;
    }
}

/// <summary>Finds cover points that look fine in the Scene view but would fail or mislead the AI at runtime.</summary>
public static class CoverValidator
{
    private const float NavMeshTolerance = 0.3f;
    private const float DuplicateDistance = 0.3f;
    private const float MaxPeekHeightDifference = 0.1f;
    private const float BodyShrink = 0.8f;

    public static List<CoverIssue> Validate(GameObject root, CoverBakeSettings settings)
    {
        var points = new List<CoverPoint>(root.GetComponentsInChildren<CoverPoint>(true));
        var issues = new List<CoverIssue>();
        var path = new NavMeshPath();

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            CheckPlacement(point, settings, issues);
            CheckProtection(point, settings, issues);
            CheckPeeks(point, settings, issues);
            CheckDuplicates(point, points, i, issues);
            CheckReachable(point, points[0], path, issues);
        }

        issues.Sort((a, b) => b.Severity.CompareTo(a.Severity));
        return issues;
    }

    private static void CheckPlacement(CoverPoint point, CoverBakeSettings settings, List<CoverIssue> issues)
    {
        Vector3 position = point.Position;
        if (!NavMesh.SamplePosition(position, out _, NavMeshTolerance, NavMesh.AllAreas))
            issues.Add(new CoverIssue(CoverIssueSeverity.Error, point, "Not on the NavMesh, so agents cannot stand here."));

        float radius = settings.AgentRadius;
        Vector3 bottom = position + Vector3.up * radius;
        Vector3 top = position + Vector3.up * (CoverDimensions.StandBodyHeight - radius);
        if (UnityPhysics.CheckCapsule(bottom, top, radius * BodyShrink, settings.ObstacleMask, QueryTriggerInteraction.Ignore))
            issues.Add(new CoverIssue(CoverIssueSeverity.Error, point, "A standing body does not fit here: the point is inside geometry."));
    }

    private static void CheckProtection(CoverPoint point, CoverBakeSettings settings, List<CoverIssue> issues)
    {
        Vector3 origin = point.Position + Vector3.up * settings.LowCoverHeight;
        if (!UnityPhysics.Raycast(origin, -point.Forward, settings.WallSearchDistance, settings.ObstacleMask,
                QueryTriggerInteraction.Ignore))
            issues.Add(new CoverIssue(CoverIssueSeverity.Error, point, "Nothing blocks the line behind this point, so it gives no protection."));
    }

    private static void CheckPeeks(CoverPoint point, CoverBakeSettings settings, List<CoverIssue> issues)
    {
        var flags = (CoverPeekDirection)new SerializedObject(point).FindProperty("_availablePeeks").intValue;
        foreach (var side in new[] { CoverPeekDirection.Left, CoverPeekDirection.Right })
        {
            if ((flags & side) == 0)
                continue;

            if (!point.TryGetPeekDestination(side, out Vector3 destination))
            {
                issues.Add(new CoverIssue(CoverIssueSeverity.Error, point, $"{side} peek is enabled but has no usable offset."));
                continue;
            }

            float offset = side == CoverPeekDirection.Left ? point.LeftPeekOffset : point.RightPeekOffset;
            if (offset > settings.MaxPeekDistance)
                issues.Add(new CoverIssue(CoverIssueSeverity.Error, point, $"{side} peek offset {offset:0.00} m is beyond the {settings.MaxPeekDistance:0.00} m the AI accepts."));

            bool onMesh = NavMesh.SamplePosition(destination, out NavMeshHit hit, settings.MaxNavigationSnap, NavMesh.AllAreas);
            if (!onMesh || Mathf.Abs(hit.position.y - point.Position.y) > MaxPeekHeightDifference)
                issues.Add(new CoverIssue(CoverIssueSeverity.Error, point, $"{side} peek position is not on the NavMesh."));
        }
    }

    private static void CheckDuplicates(CoverPoint point, List<CoverPoint> points, int index, List<CoverIssue> issues)
    {
        float limit = DuplicateDistance * DuplicateDistance;
        for (int other = index + 1; other < points.Count; other++)
        {
            if ((points[other].Position - point.Position).sqrMagnitude < limit)
            {
                issues.Add(new CoverIssue(CoverIssueSeverity.Warning, point, $"Almost the same place as {points[other].name}."));
                return;
            }
        }
    }

    private static void CheckReachable(CoverPoint point, CoverPoint reference, NavMeshPath path, List<CoverIssue> issues)
    {
        if (point == reference)
            return;

        bool reachable = NavMesh.CalculatePath(point.Position, reference.Position, NavMesh.AllAreas, path) &&
                         path.status == NavMeshPathStatus.PathComplete;
        if (!reachable)
            issues.Add(new CoverIssue(CoverIssueSeverity.Warning, point, $"No walkable route to {reference.name}: it may be on a separate NavMesh island."));
    }
}
}
