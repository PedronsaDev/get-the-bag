using Project.AI.Blackboard;
using Project.AI.Combat;
using Project.AI.Configuration;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Cover
{
/// <summary>Main-thread spatial queries. Each enemy owns one instance; buffers never grow during queries.</summary>
public sealed class CoverPeekValidator
{
    private readonly Collider[] _overlaps = new Collider[64];
    private readonly RaycastHit[] _sweepHits = new RaycastHit[64];

    public bool TryResolveCandidate(CoverPoint candidate, CoverPeekDirection direction,
        AICoverExecutionConfig config, out Vector3 worldPosition, out CoverPeekFailure failure)
    {
        worldPosition = default;
        if (!candidate || !candidate.isActiveAndEnabled)
            return Fail(CoverPeekFailure.InvalidCoverPoint, out failure);
        if (direction is not (CoverPeekDirection.Left or CoverPeekDirection.Right))
            return Fail(CoverPeekFailure.InvalidPeekDirection, out failure);
        if (config == null || !Positive(config.MaxLateralPeekDistance) || !Positive(config.MaxVerticalDeviation))
            return Fail(CoverPeekFailure.InvalidConfiguration, out failure);
        if (!candidate.TryGetPeekDestination(direction, out Vector3 destination))
            return Fail(CoverPeekFailure.UnavailablePeekDirection, out failure);
        if (!WithinLimits(candidate.Position, destination, config, out failure))
            return false;
        worldPosition = destination;
        failure = CoverPeekFailure.None;
        return true;
    }

    public bool TryValidate(CoverPoint candidate, CoverPeekDirection direction, AIBlackboard blackboard,
        NavMeshAgent agent, AIWeaponExecutor weapon, out CoverPeekResult result, out CoverPeekFailure failure)
    {
        result = default;
        if (!blackboard || !blackboard.IsArchetypeReady || !blackboard.ArchetypeConfig.CanUseCover)
            return Fail(CoverPeekFailure.InvalidConfiguration, out failure);
        var config = blackboard.ArchetypeConfig.CoverExecutionConfig;
        if (!TryResolveCandidate(candidate, direction, config, out Vector3 authored, out failure))
            return false;
        if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh || agent.isOnOffMeshLink ||
            !Positive(agent.radius) || !Positive(agent.height) || agent.height < 2f * agent.radius ||
            !float.IsFinite(agent.baseOffset))
            return Fail(CoverPeekFailure.InvalidAgent, out failure);
        // The standing envelope supports upright, unit-scale actor roots; scaled cover transforms are supported.
        Vector3 scale = blackboard.transform.lossyScale;
        if (!Finite(scale) || (scale - Vector3.one).sqrMagnitude > 0.000001f ||
            Vector3.Dot(blackboard.transform.up, Vector3.up) < 0.999f)
            return Fail(CoverPeekFailure.InvalidAgent, out failure);
        if (!Positive(config.MaxNavigationSnapDistance) || !Positive(config.ClearanceSkin) ||
            config.ClearanceSkin >= agent.radius || config.BodyClearanceMask.value == 0)
            return Fail(CoverPeekFailure.InvalidConfiguration, out failure);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        float snapDistance = config.MaxNavigationSnapDistance;
        if (!NavMesh.SamplePosition(candidate.Position, out NavMeshHit anchorHit, snapDistance, filter) ||
            !NavMesh.SamplePosition(authored, out NavMeshHit peekHit, snapDistance, filter))
            return Fail(CoverPeekFailure.NavigationSampleFailed, out failure);
        if (!Finite(anchorHit.position) || !Finite(peekHit.position) ||
            (anchorHit.position - candidate.Position).sqrMagnitude > snapDistance * snapDistance ||
            (peekHit.position - authored).sqrMagnitude > snapDistance * snapDistance)
            return Fail(CoverPeekFailure.NavigationSnapExceeded, out failure);
        if (!WithinLimits(candidate.Position, anchorHit.position, config, out failure) ||
            !WithinLimits(candidate.Position, peekHit.position, config, out failure))
            return false;
        if (NavMesh.Raycast(anchorHit.position, peekHit.position, out _, filter))
            return Fail(CoverPeekFailure.NavigationRouteBlocked, out failure);
        Vector3 travel = peekHit.position - anchorHit.position;
        float length = travel.magnitude;
        if (!Positive(length) || length < 0.001f || Vector3.Dot(travel, authored - candidate.Position) <= 0f)
            return Fail(CoverPeekFailure.InvalidPosition, out failure);
        Transform self = blackboard.transform;
        float radius = agent.radius;
        Vector3 bottomOffset = Vector3.up * (agent.baseOffset + config.ClearanceSkin + radius);
        Vector3 topOffset = Vector3.up * (agent.baseOffset + config.ClearanceSkin + agent.height - radius);
        LayerMask mask = config.BodyClearanceMask;
        if (!BodyFits(anchorHit.position + bottomOffset, anchorHit.position + topOffset, radius, mask, self, out failure) ||
            !BodyFits(peekHit.position + bottomOffset, peekHit.position + topOffset, radius, mask, self, out failure))
            return false;
        int count = Physics.CapsuleCastNonAlloc(anchorHit.position + bottomOffset, anchorHit.position + topOffset,
            radius, travel / length, _sweepHits, length, mask, QueryTriggerInteraction.Ignore);
        bool saturated = count == _sweepHits.Length;
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _sweepHits[i].collider;
            if (hit && !hit.transform.IsChildOf(self)) blocked = true;
            _sweepHits[i] = default;
        }
        if (saturated) return Fail(CoverPeekFailure.QueryBufferFull, out failure);
        if (blocked) return Fail(CoverPeekFailure.MovementBlocked, out failure);
        if (!weapon || !weapon.isActiveAndEnabled || !weapon.TryGetFiringGeometry(out Vector3 muzzle, out Vector3 aim))
            return Fail(CoverPeekFailure.InvalidFiringGeometry, out failure);
        Vector3 projectedRoot = peekHit.position + Vector3.up * agent.baseOffset;
        Vector3 projectedMuzzle = muzzle + projectedRoot - self.position;
        if (!weapon.HasClearFiringLine(projectedMuzzle, aim))
            return Fail(CoverPeekFailure.FiringLineBlocked, out failure);
        result = new CoverPeekResult(direction, peekHit.position);
        failure = CoverPeekFailure.None;
        return true;
    }

    private bool BodyFits(Vector3 bottom, Vector3 top, float radius, LayerMask mask, Transform self,
        out CoverPeekFailure failure)
    {
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlaps, mask, QueryTriggerInteraction.Ignore);
        bool saturated = count == _overlaps.Length;
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlaps[i];
            if (hit && !hit.transform.IsChildOf(self)) blocked = true;
            _overlaps[i] = null;
        }
        if (saturated) return Fail(CoverPeekFailure.QueryBufferFull, out failure);
        if (blocked) return Fail(CoverPeekFailure.BodyBlocked, out failure);
        failure = CoverPeekFailure.None;
        return true;
    }

    private static bool WithinLimits(Vector3 anchor, Vector3 position, AICoverExecutionConfig config,
        out CoverPeekFailure failure)
    {
        Vector3 displacement = position - anchor;
        if (!Finite(anchor) || !Finite(position) || !Finite(displacement))
            return Fail(CoverPeekFailure.InvalidPosition, out failure);
        float horizontalSqr = displacement.x * displacement.x + displacement.z * displacement.z;
        if (!float.IsFinite(horizontalSqr) || horizontalSqr > config.MaxLateralPeekDistance * config.MaxLateralPeekDistance)
            return Fail(CoverPeekFailure.ExceededMaxLateralPeekDistance, out failure);
        if (Mathf.Abs(displacement.y) > config.MaxVerticalDeviation)
            return Fail(CoverPeekFailure.ExceededMaxVerticalPeekDistance, out failure);
        failure = CoverPeekFailure.None;
        return true;
    }

    private static bool Positive(float value) => float.IsFinite(value) && value > 0f;
    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    private static bool Fail(CoverPeekFailure value, out CoverPeekFailure failure) { failure = value; return false; }
}
}
