using Project.AI.Blackboard;
using Project.AI.Combat;
using Project.AI.Cover;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Decision.States
{
public class AICombatState : IAIState
{
    private enum CombatStance : byte
    {
        Firing,
        MovingToCover,
        InCover,
        FlakingInOpen
    }

    private CombatStance _stance;
    private CoverPoint _activeCover;
    private AIWeaponExecutor _cachedWeaponExecutor;
    private bool _hasToken;
    private float _tokenCooldownTimer;
    private float _reflankTimer;
    private EntityId _entityId;

    private float _coverCheckTimer;
    private CoverPeekDirection _activePeekDirection;

    public void OnEnter(AIBlackboard blackboard)
    {
        _entityId = blackboard.gameObject.GetEntityId();

        if (!_cachedWeaponExecutor)
            _cachedWeaponExecutor = blackboard.GetComponent<AIWeaponExecutor>();
        if (_cachedWeaponExecutor)
            _cachedWeaponExecutor.OnBurstCompleted += HandleBurstCompleted;

        _hasToken = false;
        _tokenCooldownTimer = 0f;
        _reflankTimer = 0f;
        
        DecideOpenFieldTactics(blackboard, blackboard.MovementConfig);
    }

    private Vector3 _cachedPeekDestination;

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        var config = blackboard.MovementConfig;

        UpdateTimers(deltaTime);

        if (!ValidateLineOfSightAndTarget(blackboard, config))
            return;

        ValidateCoverIntegrity(blackboard);

        switch (_stance)
        {
            case CombatStance.InCover:
                ExecuteCoverStance(blackboard, config);
                break;

            case CombatStance.MovingToCover:
                ExecuteMovingToCoverStance(blackboard, config);
                break;

            case CombatStance.Firing:
                ExecuteFiringStance(blackboard, config);
                break;

            case CombatStance.FlakingInOpen:
                ExecuteFlankingStance(blackboard, config);
                break;
        }
    }
    
    private void UpdateTimers(float deltaTime)
    {
        if (_tokenCooldownTimer > 0f) 
            _tokenCooldownTimer -= deltaTime;
        if (_reflankTimer > 0f) 
            _reflankTimer -= deltaTime;
        if (_coverCheckTimer > 0f) 
            _coverCheckTimer -= deltaTime;
    }

    private bool ValidateLineOfSightAndTarget(AIBlackboard blackboard, AIMovementConfig config)
    {
        bool isUsingCover = _stance is CombatStance.InCover or CombatStance.MovingToCover;
        if (!blackboard.HasLineOfSight && !isUsingCover)
        {
            ReleaseTokenIfHeld();
            blackboard.SetCombatIntent(false);
            if (blackboard.TargetPosition != Vector3.zero)
            {
                blackboard.SetMoveDestination(blackboard.TargetPosition, config.CombatRunSpeed);
            }
            return false;
        }

        return true;
    }
    
    private void DecideOpenFieldTactics(AIBlackboard blackboard, AIMovementConfig config)
    {
        if (_tokenCooldownTimer <= 0f && CombatTokenDirector.Instance &&
            CombatTokenDirector.Instance.TryAcquireToken(_entityId, CombatTokenType.RangedBurst))
        {
            _hasToken = true;
            _stance = CombatStance.Firing;
            return;
        }

        if (blackboard.Target && CoverScoringEngine.TryFindBestCover(
                blackboard.AgentPosition,
                blackboard.Target.transform,
                _entityId,
                blackboard.MovementConfig,
                blackboard.AICoverScoringConfig,
                out var cover))
        {
            _activeCover = cover;
            blackboard.SetMoveDestination(_activeCover.Position, config.CombatRunSpeed);
            _stance = CombatStance.MovingToCover;
            return;
        }

        _stance = CombatStance.FlakingInOpen;
    }

    private void ValidateCoverIntegrity(AIBlackboard blackboard)
    {
        if (!(_coverCheckTimer <= 0f)) 
            return;
        
        _coverCheckTimer = 0.5f;

        if (!_activeCover || _stance is not (CombatStance.InCover or CombatStance.MovingToCover)) 
            return;

        if (CoverScoringEngine.IsThreatOccluded(_activeCover, blackboard.TargetPosition,
                blackboard.AICoverScoringConfig.ObstacleMask)) return;
        
        _activeCover.Release(_entityId);
        _activeCover = null;

        if (CombatTokenDirector.Instance && CombatTokenDirector.Instance.TryAcquireToken(_entityId, CombatTokenType.RangedBurst))
        {
            _hasToken = true;
            _stance = CombatStance.Firing;
        }
        else
        {
            _hasToken = false;
            _stance = CombatStance.FlakingInOpen;
        }
    }
    
    private void ExecuteMovingToCoverStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        if (_stance != CombatStance.MovingToCover || !_activeCover) 
            return;
        
        float distToCoverSqr = (blackboard.AgentPosition - _activeCover.Position).sqrMagnitude;
        if (blackboard.IsAtDestination || distToCoverSqr <= 0.36f)
        {
            blackboard.ClearMoveDestination();
            blackboard.SetCombatIntent(false, true, _activeCover.Position);
            _stance = CombatStance.InCover;
        }
    }

    private void ExecuteCoverStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        if (!_activeCover) return;
        if (!_hasToken && _tokenCooldownTimer <= 0f && CombatTokenDirector.Instance)
        {
            _hasToken = CombatTokenDirector.Instance.TryAcquireToken(_entityId, CombatTokenType.RangedBurst);
            if (_hasToken && _activeCover.Height == CoverHeight.High)
            {
                _cachedPeekDestination = GetBestPeekPosition(_activeCover, blackboard.TargetPosition,
                    blackboard.AICoverScoringConfig.ObstacleMask);

                if (_activePeekDirection == CoverPeekDirection.None)
                {
                    ReleaseTokenIfHeld();
                    _tokenCooldownTimer = 1.0f;
                }
            }
        }

        if (_hasToken)
        {
            if (_activeCover.Height == CoverHeight.Low)
            {
                blackboard.ClearMoveDestination();
                blackboard.SetCoverStance(isPeeking: true, CoverHeight.Low, CoverPeekDirection.Over);
                blackboard.SetCombatIntent(shoot: true, inCover: true, coverPos: _activeCover.Position);
            }
            else
            {
                if (_activePeekDirection != CoverPeekDirection.None)
                {
                    float distToPeekSqr = (blackboard.AgentPosition - _cachedPeekDestination).sqrMagnitude;
                    if (distToPeekSqr > 0.09f)
                    {
                        blackboard.SetMoveDestination(_cachedPeekDestination, config.InvestigateSpeed);
                        blackboard.SetCombatIntent(shoot: false, inCover: true, coverPos: _activeCover.Position);
                    }
                    else
                    {
                        blackboard.ClearMoveDestination();
                        blackboard.SetCoverStance(isPeeking: true, CoverHeight.High, _activePeekDirection);
                        blackboard.SetCombatIntent(shoot: true, inCover: true, coverPos: _activeCover.Position);
                    }
                }
            }
        }
        else
        {
            if (_activeCover.Height == CoverHeight.Low)
            {
                blackboard.ClearMoveDestination();
                blackboard.SetCoverStance(isPeeking: false, CoverHeight.Low);
                blackboard.SetCombatIntent(shoot: false, inCover: true, coverPos: _activeCover.Position);
            }
            else
            {
                float distToAnchorSqr = (blackboard.AgentPosition - _activeCover.Position).sqrMagnitude;
                if (distToAnchorSqr > 0.09f)
                {
                    blackboard.SetMoveDestination(_activeCover.Position, config.InvestigateSpeed);
                }
                else
                {
                    blackboard.ClearMoveDestination();
                }

                blackboard.SetCoverStance(isPeeking: false, CoverHeight.High);
                blackboard.SetCombatIntent(shoot: false, inCover: true, coverPos: _activeCover.Position);
            }
        }
    }

    private Vector3 GetBestPeekPosition(CoverPoint cover, Vector3 threatPos, LayerMask obstacleMask)
    {
        Vector3 right = Vector3.Cross(Vector3.up, cover.Forward).normalized;
        float stepDist = 0.65f;

        if ((cover.PeekDirections & CoverPeekDirection.Right) != 0)
        {
            Vector3 rightPos = cover.Position + (right * stepDist);
            if (NavMesh.SamplePosition(rightPos, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
            {
                if (HasClearLineOfFire(hit.position, threatPos, obstacleMask))
                {
                    _activePeekDirection = CoverPeekDirection.Right;
                    return hit.position;
                }
            }
        }

        if ((cover.PeekDirections & CoverPeekDirection.Left) != 0)
        {
            Vector3 leftPos = cover.Position - (right * stepDist);
            if (NavMesh.SamplePosition(leftPos, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
            {
                if (HasClearLineOfFire(hit.position, threatPos, obstacleMask))
                {
                    _activePeekDirection = CoverPeekDirection.Left;
                    return hit.position;
                }
            }
        }

        _activePeekDirection = CoverPeekDirection.None;
        return cover.Position;
    }

    private bool HasClearLineOfFire(Vector3 peekPos, Vector3 threatPos, LayerMask mask)
    {
        Vector3 muzzlePos = peekPos + Vector3.up * 1.4f;
        Vector3 threatAimPos = threatPos + Vector3.up * 1.2f;
        Vector3 dir = threatAimPos - muzzlePos;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;
        return !Physics.Raycast(muzzlePos, dir.normalized, dist, mask, QueryTriggerInteraction.Ignore);
    }
    
    public void OnExit(AIBlackboard blackboard)
    {
        if (_cachedWeaponExecutor)
            _cachedWeaponExecutor.OnBurstCompleted -= HandleBurstCompleted;

        ReleaseTokenIfHeld();
        ReleaseCoverIfReserved();

        blackboard.SetCoverStance(isPeeking: false, CoverHeight.Low, CoverPeekDirection.None);
        blackboard.SetCombatIntent(false);
    }

    private void ReleaseCoverIfReserved()
    {
        _activeCover?.Release(_entityId);
        _activeCover = null;
    }

    private void HandleBurstCompleted()
    {
        ReleaseTokenIfHeld();
        _tokenCooldownTimer = Random.Range(1.0f, 1.6f);
        _activePeekDirection = CoverPeekDirection.None;

        if (_stance == CombatStance.Firing)
        {
            _stance = CombatStance.FlakingInOpen;
        }
    }

    private void ReleaseTokenIfHeld()
    {
        if (_hasToken && CombatTokenDirector.Instance)
        {
            CombatTokenDirector.Instance.ReleaseToken(_entityId, CombatTokenType.RangedBurst);
            _hasToken = false;
        }
    }

    private void ExecuteFiringStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        blackboard.SetCombatIntent(true);

        if (blackboard.DistanceToTarget > config.MaxCombatDistance)
            blackboard.SetMoveDestination(blackboard.TargetPosition, config.CombatRunSpeed);
        else if (blackboard.DistanceToTarget < config.MinCombatDistance)
            ExecuteRetreat(blackboard, config);
        else
            blackboard.ClearMoveDestination();
    }

    private void ExecuteFlankingStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        blackboard.SetCombatIntent(false);

        if (_reflankTimer <= 0f)
        {
            DecideOpenFieldTactics(blackboard, blackboard.MovementConfig);
            if (_stance != CombatStance.FlakingInOpen)
                return;

            _reflankTimer = Random.Range(1.5f, 2.5f);
            Vector3 flankDestination = CalculateFlankPosition(blackboard, config);
            blackboard.SetMoveDestination(flankDestination, config.CombatRunSpeed);
        }
    }

    private Vector3 CalculateFlankPosition(AIBlackboard blackboard, AIMovementConfig config)
    {
        Vector3 toSelf = blackboard.transform.position - blackboard.TargetPosition;
        toSelf.y = 0f;

        if (toSelf.sqrMagnitude < 0.01f)
            toSelf = -blackboard.transform.forward;
        else
            toSelf.Normalize();

        float flankAngle = ((blackboard.gameObject.GetEntityId().GetHashCode() & 1) == 0 ? 1f : -1f) * 60f;
        Vector3 flankDir = Quaternion.Euler(0f, flankAngle, 0f) * toSelf;

        Vector3 desiredPosition = blackboard.TargetPosition + flankDir * config.IdealCombatDistance;

        if (NavMesh.SamplePosition(desiredPosition, out var hit, 4.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return blackboard.transform.position;
    }

    private void ExecuteRetreat(AIBlackboard blackboard, AIMovementConfig config)
    {
        Vector3 dirAwayFromTarget = blackboard.transform.position - blackboard.TargetPosition;

        dirAwayFromTarget.y = 0f;
        if (dirAwayFromTarget.sqrMagnitude > 0.001f)
        {
            dirAwayFromTarget.Normalize();
        }
        else
        {
            dirAwayFromTarget = -blackboard.transform.forward;
        }

        Vector3 desiredRetreatPoint = blackboard.transform.position + dirAwayFromTarget * 4.0f;

        if (NavMesh.SamplePosition(desiredRetreatPoint, out var hit, 2.0f, NavMesh.AllAreas))
            blackboard.SetMoveDestination(hit.position, config.CombatRunSpeed);
    }
}
}