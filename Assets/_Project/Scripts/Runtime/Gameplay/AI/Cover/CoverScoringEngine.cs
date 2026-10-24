using System.Collections.Generic;
using Project.AI.Combat;
using Project.Core.Player;
using UnityEngine;
using UnityPhysics = UnityEngine.Physics; // Discipline Rule 2: Namespace Shadowing


namespace Project.AI.Cover
{
public static class CoverScoringEngine
{
    // Buffer reutilizavel para nao ter alocacao de GC durante a buscar
    private static readonly List<CoverPoint> _candidateBuffer = new(32);

    // Registration is a setup path; grow here so combat queries never resize this buffer.
    internal static void ReserveCandidateCapacity(int count)
    {
        if (_candidateBuffer.Capacity < count)
            _candidateBuffer.Capacity = Mathf.Max(count, _candidateBuffer.Capacity * 2);
    }

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

        float highestScore = float.NegativeInfinity;
        CoverPoint selectedCandidate = null;

        for (int i = 0; i < count; i++)
        {
            var candidate = _candidateBuffer[i];
            if (!candidate)
                continue;

            var threatPosition = threatTransform.position;

            Vector3 toThreat = threatPosition - candidate.Position;
            toThreat.y = 0f;
            if (toThreat.sqrMagnitude < 0.01f)
                continue;

            Vector3 toThreatDir = toThreat.normalized;
            // The baker stores the outward wall normal. Protection is on the opposite side.
            float facingAlignment = Vector3.Dot(-candidate.Forward, toThreatDir);

            if (facingAlignment < scoreConfig.MinFacingAlignment)
                continue;

            if (!IsThreatOccluded(candidate, threatPosition, scoreConfig.ObstacleMask))
                continue;
            float score = CalculateUtilityScore(
                candidate,
                agentPosition,
                threatPosition,
                facingAlignment,
                moveConfig.IdealCombatDistance,
                scoreConfig);

            score *= CalculateCrossfireSafetyMultiplier(candidate, threatTransform, scoreConfig.ObstacleMask);

            if (score > highestScore)
            {
                highestScore = score;
                selectedCandidate = candidate;
            }
        }

        if (selectedCandidate && selectedCandidate.TryReserve(agentId))
        {
            bestCover = selectedCandidate;
            return true;
        }

        return false;
    }

    private static float CalculateCrossfireSafetyMultiplier(CoverPoint candidate, Transform primaryTarget, LayerMask mask)
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

    public static bool IsThreatOccluded(CoverPoint point, Vector3 threatPos, LayerMask mask)
    {
        Vector3 threatEyePos = threatPos + Vector3.up * 1.6f;
        float stanceY = point.Height == CoverHeight.Low ? 0.75f : 1.5f;
        Vector3 coverStancePos = point.Position + Vector3.up * stanceY;

        Vector3 rayDir = coverStancePos - threatEyePos;
        float distance = rayDir.magnitude;
        if (distance < 0.01f)
            return false;

        if (UnityPhysics.Raycast(threatEyePos, rayDir.normalized, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.distance < distance - 0.2f;
        }

        return false;
    }

    private static float CalculateUtilityScore(
        CoverPoint candidate,
        Vector3 agentPosition,
        Vector3 threatPos,
        float facingAlignment,
        float idealDistance,
        AICoverScoringConfig config)
    {
        float distToCover = Vector3.Distance(agentPosition, candidate.Position);
        float sDist = 1.0f - Mathf.Clamp01(distToCover / config.MaxSearchRadius);

        float sFacing = Mathf.Clamp01(facingAlignment);

        float sPeek = candidate.PeekDirections != CoverPeekDirection.None ? 1.0f : 0.4f;

        float distCoverToThreat = Vector3.Distance(candidate.Position, threatPos);
        float sRange = 1.0f - Mathf.Clamp01(Mathf.Abs(distCoverToThreat - idealDistance)/ idealDistance);

        return (config.ProximityWeight * sDist) +
               (config.FacingWeight * sFacing) +
               (config.PeekWeight * sPeek) +
               (config.IdealRangeWeight * sRange);

    }
}
}
