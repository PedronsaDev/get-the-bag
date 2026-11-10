using UnityEngine;
using UnityEngine.AI;
using UnityPhysics = UnityEngine.Physics;

namespace Project.AI.Cover.Editor
{
public struct CoverCandidate
{
    public Vector3 Position;
    public Vector3 Normal;
    public CoverHeight Height;
    public CoverPeekDirection Peeks;
    public float LeftOffset;
    public float RightOffset;
    public float Quality;
}

/// <summary>Decides whether a spot on the NavMesh edge is real cover, and measures what it offers.</summary>
public static class CoverCandidateEvaluator
{
    private const float WallProbeHeight = 0.6f;
    private const float MaxNormalSlope = 0.3f;
    private const float MinWallAlignment = 0.5f;
    private const float ScanStep = 0.1f;
    private const float MaxScanDistance = 3f;
    private const float MaxPeekHeightDifference = 0.1f;
    private const float WidthForFullQuality = 4f;

    public static bool TryEvaluate(Vector3 spot, Vector3 outward, CoverBakeSettings settings, out CoverCandidate candidate)
    {
        candidate = default;
        var mask = settings.ObstacleMask;
        float reach = settings.WallSearchDistance;

        if (!UnityPhysics.Raycast(spot + Vector3.up * WallProbeHeight, outward, out RaycastHit wall, reach, mask,
                QueryTriggerInteraction.Ignore))
            return false;
        if (Mathf.Abs(wall.normal.y) > MaxNormalSlope)
            return false;

        Vector3 normal = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
        if (Vector3.Dot(-normal, outward) < MinWallAlignment)
            return false;

        if (!HitsWall(spot, settings.LowCoverHeight, normal, reach, mask))
            return false;

        bool high = HitsWall(spot, settings.HighCoverHeight, normal, reach, mask);
        Vector3 right = Vector3.Cross(Vector3.up, normal);
        MeasureSide(spot, right, normal, settings, out float rightExtent, out float rightOffset);
        MeasureSide(spot, -right, normal, settings, out float leftExtent, out float leftOffset);

        var peeks = CoverPeekDirection.None;
        if (rightOffset > 0f) peeks |= CoverPeekDirection.Right;
        if (leftOffset > 0f) peeks |= CoverPeekDirection.Left;
        if (!high) peeks |= CoverPeekDirection.Over;

        candidate = new CoverCandidate
        {
            Position = spot,
            Normal = normal,
            Height = high ? CoverHeight.High : CoverHeight.Low,
            Peeks = peeks,
            LeftOffset = leftOffset,
            RightOffset = rightOffset,
            Quality = Quality(high, peeks, leftExtent + rightExtent)
        };
        return true;
    }

    public static float Quality(bool high, CoverPeekDirection peeks, float wallWidth)
    {
        float width = Mathf.Clamp01(wallWidth / WidthForFullQuality);
        float peek = peeks != CoverPeekDirection.None ? 1f : 0f;
        float height = high ? 1f : 0.6f;
        return 0.4f * width + 0.35f * peek + 0.25f * height;
    }

    private static bool HitsWall(Vector3 spot, float height, Vector3 normal, float reach, LayerMask mask) =>
        UnityPhysics.Raycast(spot + Vector3.up * height, -normal, reach, mask, QueryTriggerInteraction.Ignore);

    // Walks sideways along the wall. Where it ends, a body that steps out by its own radius can see past the edge.
    private static void MeasureSide(Vector3 spot, Vector3 side, Vector3 normal, CoverBakeSettings settings,
        out float extent, out float peekOffset)
    {
        extent = MaxScanDistance;
        peekOffset = 0f;

        bool ended = false;
        for (float distance = ScanStep; distance <= MaxScanDistance; distance += ScanStep)
        {
            Vector3 probe = spot + side * distance + Vector3.up * CoverDimensions.PeekProbeHeight;
            if (UnityPhysics.Raycast(probe, -normal, settings.WallSearchDistance, settings.ObstacleMask,
                    QueryTriggerInteraction.Ignore))
                continue;

            extent = distance;
            ended = true;
            break;
        }
        if (!ended)
            return;

        float offset = extent + settings.AgentRadius + CoverDimensions.PeekMargin;
        if (offset > settings.MaxPeekDistance)
            return;

        // The runtime peek check only accepts a destination that is already on the NavMesh.
        Vector3 destination = spot + side * offset;
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, settings.MaxNavigationSnap, NavMesh.AllAreas) &&
            Mathf.Abs(hit.position.y - spot.y) <= MaxPeekHeightDifference)
            peekOffset = offset;
    }
}
}
