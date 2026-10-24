using Project.AI.Blackboard;
using Action = System.Action;
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
    private NavMeshAgent _cachedAgent;
    private readonly CoverPeekValidator _peekValidator = new();
    private bool _hasToken;
    private float _tokenCooldownTimer;
    private float _reflankTimer;
    private EntityId _entityId;
    private AIBlackboard _blackboard;
    private CombatTokenType _tokenType;
    private readonly Action _burstCompletedHandler;

    private float _coverCheckTimer;
    private CoverPeekDirection _activePeekDirection;
    private float _peekCheckTimer;
    private float _peekAttemptTimer;
    private float _returnTimer;
    private bool _atPeekPosition;
    private bool _returningToAnchor;

    public bool IsUsingCover => _activeCover && _activeCover.isActiveAndEnabled &&
        _stance is CombatStance.MovingToCover or CombatStance.InCover;

    public AICombatState()
    {
        _burstCompletedHandler = HandleBurstCompleted;
    }

    public void OnEnter(AIBlackboard blackboard)
    {
        _blackboard = blackboard;
        _tokenType = blackboard.ArchetypeConfig.PreferredTokenType;
        _entityId = blackboard.gameObject.GetEntityId();

        if (!_cachedWeaponExecutor)
            _cachedWeaponExecutor = blackboard.GetComponent<AIWeaponExecutor>();
        if (!_cachedAgent)
            _cachedAgent = blackboard.GetComponent<NavMeshAgent>();
        if (_cachedWeaponExecutor)
            _cachedWeaponExecutor.OnBurstCompleted += _burstCompletedHandler;

        _hasToken = false;
        _tokenCooldownTimer = 0f;
        _reflankTimer = 0f;
        _coverCheckTimer = 0f;
        _activePeekDirection = CoverPeekDirection.None;
        _peekCheckTimer = 0f;
        _peekAttemptTimer = 0f;
        _returnTimer = 0f;
        _atPeekPosition = false;
        _returningToAnchor = false;

        DecideOpenFieldTactics(blackboard, blackboard.ArchetypeConfig.MovementConfig);
    }

    private Vector3 _cachedPeekDestination;

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        var config = blackboard.ArchetypeConfig.MovementConfig;

        UpdateTimers(deltaTime);

        if (_hasToken && (!CombatTokenDirector.Instance ||
            !CombatTokenDirector.Instance.TryRenewToken(_entityId, _tokenType)))
        {
            ReleaseTokenIfHeld();
            blackboard.SetCombatIntent(false, blackboard.InCover, blackboard.CoverPosition);
            if (_activeCover && _activeCover.Height == CoverHeight.High && _activePeekDirection != CoverPeekDirection.None)
                BeginReturnToAnchor(blackboard, config);
            else if (_stance == CombatStance.Firing)
                _stance = CombatStance.FlakingInOpen;
        }

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
        if (_peekCheckTimer > 0f)
            _peekCheckTimer -= deltaTime;
        if (_hasToken && (!_atPeekPosition || !_blackboard.HasLineOfSight))
            _peekAttemptTimer -= deltaTime;
        if (_returningToAnchor)
            _returnTimer -= deltaTime;
    }

    private bool ValidateLineOfSightAndTarget(AIBlackboard blackboard, AIMovementConfig config)
    {
        if (!blackboard.Target)
        {
            ReleaseTokenIfHeld();
            ReleaseCoverIfReserved();
            blackboard.SetCoverStance(false, CoverHeight.Low);
            blackboard.SetCombatIntent(false);
            _stance = CombatStance.FlakingInOpen;
        }

        bool isUsingCover = _stance is CombatStance.InCover or CombatStance.MovingToCover;
        if ((!blackboard.Target || !blackboard.HasLineOfSight) && !isUsingCover)
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
            blackboard.Target && CombatTokenDirector.Instance.TryAcquireToken(_entityId, _tokenType))
        {
            _hasToken = true;
            _stance = CombatStance.Firing;
            return;
        }

        var archetypeConfig = blackboard.ArchetypeConfig;

        if (archetypeConfig.CanUseCover && blackboard.Target && CoverScoringEngine.TryFindBestCover(
                blackboard.AgentPosition,
                blackboard.TargetTransform,
                _entityId,
                archetypeConfig.MovementConfig,
                archetypeConfig.CoverConfig,
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


        if (_stance is not (CombatStance.InCover or CombatStance.MovingToCover))
            return;

        var archetypeConfig = blackboard.ArchetypeConfig;

        if (!archetypeConfig.CanUseCover || !_activeCover || !_activeCover.isActiveAndEnabled ||
            !_activeCover.TryReserve(_entityId))
        {
            AbandonCover(blackboard);
            return;
        }

        if (CoverScoringEngine.IsThreatOccluded(_activeCover, blackboard.TargetPosition,
                archetypeConfig.CoverConfig.ObstacleMask)) return;

        AbandonCover(blackboard);
    }

    private void AbandonCover(AIBlackboard blackboard)
    {
        ReleaseTokenIfHeld();
        ReleaseCoverIfReserved();
        blackboard.ClearMoveDestination();
        blackboard.SetCoverStance(false, CoverHeight.Low);
        blackboard.SetCombatIntent(false);
        _activePeekDirection = CoverPeekDirection.None;
        _atPeekPosition = false;
        _returningToAnchor = false;
        _peekCheckTimer = 0f;
        _reflankTimer = 0f;
        _stance = CombatStance.FlakingInOpen;
    }

    private void ExecuteMovingToCoverStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        if (_stance != CombatStance.MovingToCover || !_activeCover)
            return;

        if (blackboard.MoveStatus == Project.AI.Locomotion.MovementStatus.Failed)
        {
            AbandonCover(blackboard);
            return;
        }
        if (blackboard.IsAtDestination)
        {
            blackboard.ClearMoveDestination();
            blackboard.SetCombatIntent(false, true, _activeCover.Position);
            _stance = CombatStance.InCover;
        }
    }

    private void ExecuteCoverStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        if (!_activeCover)
            return;
        if (_activeCover.Height == CoverHeight.High)
        {
            ExecuteHighCoverStance(blackboard, config);
            return;
        }
        if (!_hasToken && _tokenCooldownTimer <= 0f && CombatTokenDirector.Instance)
            _hasToken = CombatTokenDirector.Instance.TryAcquireToken(_entityId, _tokenType);
        blackboard.ClearMoveDestination();
        blackboard.SetCoverStance(_hasToken, CoverHeight.Low,
            _hasToken ? CoverPeekDirection.Over : CoverPeekDirection.None);
        blackboard.SetCombatIntent(_hasToken, true, _activeCover.Position);
    }
    private void ExecuteHighCoverStance(AIBlackboard blackboard, AIMovementConfig movement)
    {
        var settings = blackboard.ArchetypeConfig.CoverExecutionConfig;
        if (_returningToAnchor)
        {
            blackboard.SetCombatIntent(false, true, _activeCover.Position);
            if (blackboard.MoveStatus == Project.AI.Locomotion.MovementStatus.Failed || _returnTimer <= 0f)
            {
                AbandonCover(blackboard);
                return;
            }
            if (blackboard.IsAtDestination)
            {
                blackboard.ClearMoveDestination();
                _returningToAnchor = false;
            }
            return;
        }

        if (_hasToken)
        {
            if (blackboard.MoveStatus == Project.AI.Locomotion.MovementStatus.Failed || _peekAttemptTimer <= 0f)
            {
                BeginReturnToAnchor(blackboard, movement);
                return;
            }
            if (_peekCheckTimer <= 0f)
            {
                _peekCheckTimer = settings.ValidationInterval;
                if (!_peekValidator.TryValidate(_activeCover, _activePeekDirection, blackboard, _cachedAgent,
                        _cachedWeaponExecutor, out var refreshed, out _) ||
                    (refreshed.Destination - _cachedPeekDestination).sqrMagnitude > settings.ArrivalTolerance * settings.ArrivalTolerance)
                {
                    BeginReturnToAnchor(blackboard, movement);
                    return;
                }
            }
            if (!_atPeekPosition && blackboard.IsAtDestination)
            {
                blackboard.ClearMoveDestination();
                _atPeekPosition = true;
                _peekAttemptTimer = settings.ApproachTimeout;
            }
            if (_atPeekPosition &&
                (blackboard.AgentPosition - _cachedPeekDestination).sqrMagnitude > settings.ArrivalTolerance * settings.ArrivalTolerance)
            {
                BeginReturnToAnchor(blackboard, movement);
                return;
            }
            blackboard.SetCoverStance(_atPeekPosition, CoverHeight.High, _activePeekDirection);
            blackboard.SetCombatIntent(_atPeekPosition, true, _activeCover.Position);
            return;
        }

        blackboard.SetCoverStance(false, CoverHeight.High);
        blackboard.SetCombatIntent(false, true, _activeCover.Position);
        if ((blackboard.AgentPosition - _activeCover.Position).sqrMagnitude > settings.ArrivalTolerance * settings.ArrivalTolerance)
        {
            BeginReturnToAnchor(blackboard, movement);
            return;
        }
        if (_tokenCooldownTimer > 0f || _peekCheckTimer > 0f)
            return;

        _peekCheckTimer = settings.ValidationInterval;
        bool rightValid = _peekValidator.TryValidate(_activeCover, CoverPeekDirection.Right, blackboard, _cachedAgent,
            _cachedWeaponExecutor, out var right, out _);
        bool leftValid = _peekValidator.TryValidate(_activeCover, CoverPeekDirection.Left, blackboard, _cachedAgent,
            _cachedWeaponExecutor, out var left, out _);
        if (!rightValid && !leftValid)
        {
            _tokenCooldownTimer = settings.RetryDelay;
            return;
        }
        var selected = rightValid ? right : left;
        if (rightValid && leftValid &&
            (left.Destination - _activeCover.Position).sqrMagnitude < (right.Destination - _activeCover.Position).sqrMagnitude)
            selected = left;
        if (!CombatTokenDirector.Instance || !CombatTokenDirector.Instance.TryAcquireToken(_entityId, _tokenType))
            return;

        _hasToken = true;
        _activePeekDirection = selected.Direction;
        _cachedPeekDestination = selected.Destination;
        _atPeekPosition = false;
        _peekAttemptTimer = settings.ApproachTimeout;
        blackboard.SetMoveDestination(_cachedPeekDestination, movement.InvestigateSpeed);
    }

    private void BeginReturnToAnchor(AIBlackboard blackboard, AIMovementConfig movement)
    {
        ReleaseTokenIfHeld();
        _activePeekDirection = CoverPeekDirection.None;
        _atPeekPosition = false;
        blackboard.SetCoverStance(false, CoverHeight.High);
        blackboard.SetCombatIntent(false, true, _activeCover.Position);
        blackboard.ClearMoveDestination();
        blackboard.SetMoveDestination(_activeCover.Position, movement.InvestigateSpeed);
        _returningToAnchor = true;
        _returnTimer = blackboard.ArchetypeConfig.CoverExecutionConfig.ReturnTimeout;
        _tokenCooldownTimer = Mathf.Max(_tokenCooldownTimer, blackboard.ArchetypeConfig.CoverExecutionConfig.RetryDelay);
    }

    public void OnExit(AIBlackboard blackboard)
    {
        if (_cachedWeaponExecutor)
            _cachedWeaponExecutor.OnBurstCompleted -= _burstCompletedHandler;

        ReleaseTokenIfHeld();
        ReleaseCoverIfReserved();

        blackboard.SetCoverStance(isPeeking: false, CoverHeight.Low, CoverPeekDirection.None);
        blackboard.SetCombatIntent(false);
        blackboard.ClearMoveDestination();
        _activePeekDirection = CoverPeekDirection.None;
        _atPeekPosition = false;
        _returningToAnchor = false;
        _blackboard = null;
    }

    private void ReleaseCoverIfReserved()
    {
        if (_activeCover)
            _activeCover.Release(_entityId);
        _activeCover = null;
    }

    private void HandleBurstCompleted()
    {
        ReleaseTokenIfHeld();
        var weaponConfig = _blackboard.ArchetypeConfig.WeaponConfig;
        _tokenCooldownTimer = Random.Range(weaponConfig.MinBurstPause, weaponConfig.MaxBurstPause);
        _blackboard.SetCombatIntent(false, _blackboard.InCover, _blackboard.CoverPosition);
        _activePeekDirection = CoverPeekDirection.None;

        if (_activeCover && _activeCover.Height == CoverHeight.High && _stance == CombatStance.InCover)
            BeginReturnToAnchor(_blackboard, _blackboard.ArchetypeConfig.MovementConfig);

        if (_stance == CombatStance.Firing)
        {
            _stance = CombatStance.FlakingInOpen;
        }
    }

    private void ReleaseTokenIfHeld()
    {
        if (_hasToken && CombatTokenDirector.Instance)
        {
            CombatTokenDirector.Instance.ReleaseToken(_entityId, _tokenType);
        }
        _hasToken = false;
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

        var archetypeConfig = blackboard.ArchetypeConfig;
        if (_reflankTimer <= 0f)
        {
            DecideOpenFieldTactics(blackboard, archetypeConfig.MovementConfig);
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
