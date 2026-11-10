using System.Collections.Generic;
using Project.AI.Combat;
using Project.Core.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityPhysics = UnityEngine.Physics;

namespace Project.AI.Cover
{
public static class CoverScoringEngine
{
    private const int FinalistCount = 3;
    private const int CornerBufferSize = 32;
    private const float OccluderTolerance = 0.2f;
    private const float SideSampleOffset = 0.3f;
    private const float PathSampleHeight = 1.0f;
    private const float ThreatEyeHeight = 1.6f;
    private const float MinThreatDistanceFactor = 0.5f;

    private static readonly List<CoverPoint> _candidateBuffer = new(32);
    private static readonly CoverPoint[] _finalists = new CoverPoint[FinalistCount];
    private static readonly float[] _finalistScores = new float[FinalistCount];
    private static readonly Vector3[] _corners = new Vector3[CornerBufferSize];
    private static NavMeshPath _path;

    internal static void ReserveCandidateCapacity(int count)
    {
        if (_candidateBuffer.Capacity < count)
            _candidateBuffer.Capacity = Mathf.Max(count, _candidateBuffer.Capacity * 2);
    }

    // Two stages: every point nearby is scored cheaply with straight-line distance, then only the best few
    // pay for a NavMesh path and a check of how exposed the run to them is.
    public static bool TryFindBestCover(
        Vector3 agentPosition,
        Transform threatTransform,
        EntityId agentId,
        AIMovementConfig moveConfig,
        AICoverScoringConfig scoreConfig,
        out CoverPoint bestCover)
    {
        bestCover = null;

        int count = CoverRegistry.GetCoverPointsInRangeNonAlloc(agentPosition, scoreConfig.MaxSearchRadius, _candidateBuffer);
        if (count == 0)
            return false;

        Vector3 threatPosition = threatTransform.position;
        float minThreatDistance = moveConfig.MinCombatDistance * MinThreatDistanceFactor;
        ClearFinalists();

        for (int i = 0; i < count; i++)
        {
            var candidate = _candidateBuffer[i];
            if (!candidate)
                continue;

            Vector3 toThreat = threatPosition - candidate.Position;
            toThreat.y = 0f;
            if (toThreat.sqrMagnitude < 0.01f || toThreat.magnitude < minThreatDistance)
                continue;

            // The baker stores the outward wall normal. Protection is on the opposite side.
            float facingAlignment = Vector3.Dot(-candidate.Forward, toThreat.normalized);
            if (facingAlignment < scoreConfig.MinFacingAlignment)
                continue;

            if (!IsThreatOccluded(candidate, threatPosition, scoreConfig.ObstacleMask))
                continue;

            float distance = Vector3.Distance(agentPosition, candidate.Position);
            float score = Score(candidate, distance, facingAlignment, threatTransform, moveConfig, scoreConfig);
            InsertFinalist(candidate, score);
        }

        CoverPoint selected = ChooseAmongFinalists(agentPosition, threatTransform, moveConfig, scoreConfig);
        if (selected && selected.TryReserve(agentId))
        {
            bestCover = selected;
            return true;
        }

        return false;
    }

    public static bool IsThreatOccluded(CoverPoint point, Vector3 threatPos, LayerMask mask)
    {
        Vector3 threatEyePos = threatPos + Vector3.up * ThreatEyeHeight;
        Vector3 stance = point.Position + Vector3.up * CoverDimensions.StanceProbeHeight(point.Height);
        Vector3 side = Vector3.Cross(Vector3.up, point.Forward);
        side = side.sqrMagnitude > 0.0001f ? side.normalized * SideSampleOffset : Vector3.right * SideSampleOffset;

        // One ray flickers at corners; protected means most of the body is hidden.
        int blocked = 0;
        if (IsRayBlocked(threatEyePos, stance, mask)) blocked++;
        if (IsRayBlocked(threatEyePos, stance + side, mask)) blocked++;
        if (IsRayBlocked(threatEyePos, stance - side, mask)) blocked++;
        return blocked >= 2;
    }

    public static float Utility(float travelDistance, float facingAlignment, bool hasPeek,
        float coverToThreatDistance, float idealDistance, float quality, AICoverScoringConfig config)
    {
        float proximity = 1.0f - Mathf.Clamp01(travelDistance / config.MaxSearchRadius);
        float facing = Mathf.Clamp01(facingAlignment);
        float peek = hasPeek ? 1.0f : 0.4f;
        float range = 1.0f - Mathf.Clamp01(Mathf.Abs(coverToThreatDistance - idealDistance) / idealDistance);

        float weighted = (config.ProximityWeight * proximity) +
                         (config.FacingWeight * facing) +
                         (config.PeekWeight * peek) +
                         (config.IdealRangeWeight * range);

        float influence = config.QualityInfluence;
        return weighted * Mathf.Lerp(1f - influence, 1f + influence, Mathf.Clamp01(quality));
    }

    private static float Score(CoverPoint candidate, float travelDistance, float facingAlignment,
        Transform threat, AIMovementConfig moveConfig, AICoverScoringConfig config)
    {
        float coverToThreat = Vector3.Distance(candidate.Position, threat.position);
        float score = Utility(travelDistance, facingAlignment, candidate.PeekDirections != CoverPeekDirection.None,
            coverToThreat, moveConfig.IdealCombatDistance, candidate.Quality, config);
        return score * CrossfireSafetyMultiplier(candidate, threat, config.ObstacleMask);
    }

    private static CoverPoint ChooseAmongFinalists(Vector3 agentPosition, Transform threat,
        AIMovementConfig moveConfig, AICoverScoringConfig config)
    {
        Vector3 threatEye = threat.position + Vector3.up * ThreatEyeHeight;
        CoverPoint best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < FinalistCount; i++)
        {
            var candidate = _finalists[i];
            if (!candidate)
                continue;

            if (!TryMeasureApproach(agentPosition, candidate.Position, threatEye, config.ObstacleMask,
                    out float pathLength, out float exposedFraction))
                continue;

            Vector3 toThreat = threat.position - candidate.Position;
            toThreat.y = 0f;
            float facing = Vector3.Dot(-candidate.Forward, toThreat.normalized);

            float score = Score(candidate, pathLength, facing, threat, moveConfig, config);
            score *= 1f - config.ApproachExposurePenalty * exposedFraction;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    // Walks the real NavMesh path. Fails when the cover cannot be reached.
    private static bool TryMeasureApproach(Vector3 from, Vector3 to, Vector3 threatEye, LayerMask mask,
        out float length, out float exposedFraction)
    {
        length = 0f;
        exposedFraction = 0f;

        _path ??= new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete)
            return false;

        int cornerCount = _path.GetCornersNonAlloc(_corners);
        if (cornerCount < 2)
        {
            length = Vector3.Distance(from, to);
            return true;
        }

        int exposed = 0;
        for (int i = 1; i < cornerCount; i++)
        {
            Vector3 segment = _corners[i] - _corners[i - 1];
            length += segment.magnitude;
            Vector3 middle = _corners[i - 1] + segment * 0.5f + Vector3.up * PathSampleHeight;
            if (!UnityPhysics.Linecast(threatEye, middle, mask, QueryTriggerInteraction.Ignore))
                exposed++;
        }

        exposedFraction = exposed / (float)(cornerCount - 1);
        return true;
    }

    private static float CrossfireSafetyMultiplier(CoverPoint candidate, Transform primaryTarget, LayerMask mask)
    {
        var players = PlayerIdentifier.ActivePlayers;
        int secondaryCount = 0;
        int occludedCount = 0;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player || player.BodyTransform == primaryTarget || player.IsDead || player.IsDowned)
                continue;
            secondaryCount++;
            if (IsThreatOccluded(candidate, player.BodyTransform.position, mask))
                occludedCount++;
        }

        if (secondaryCount == 0)
            return 1.0f;
        return 0.35f + (0.65f * (occludedCount / (float)secondaryCount));
    }

    private static bool IsRayBlocked(Vector3 from, Vector3 to, LayerMask mask)
    {
        Vector3 ray = to - from;
        float distance = ray.magnitude;
        if (distance < 0.01f)
            return false;

        if (UnityPhysics.Raycast(from, ray / distance, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
            return hit.distance < distance - OccluderTolerance;
        return false;
    }

    private static void ClearFinalists()
    {
        for (int i = 0; i < FinalistCount; i++)
        {
            _finalists[i] = null;
            _finalistScores[i] = float.NegativeInfinity;
        }
    }

    private static void InsertFinalist(CoverPoint candidate, float score)
    {
        int slot = -1;
        float lowest = float.PositiveInfinity;
        for (int i = 0; i < FinalistCount; i++)
        {
            if (_finalistScores[i] < lowest)
            {
                lowest = _finalistScores[i];
                slot = i;
            }
        }

        if (score > lowest)
        {
            _finalists[slot] = candidate;
            _finalistScores[slot] = score;
        }
    }
}
}
