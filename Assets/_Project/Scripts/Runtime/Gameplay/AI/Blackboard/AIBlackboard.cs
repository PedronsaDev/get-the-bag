using System;
using NaughtyAttributes;
using Project.AI.Configuration;
using Project.AI.Cover;
using Project.AI.Locomotion;
using Project.AI.Perception;
using Project.Core.Player;
using Project.Logging;
using UnityEngine;

namespace Project.AI.Blackboard
{
[DisallowMultipleComponent]
public class AIBlackboard : MonoBehaviour
{
    [Header("Archetype")]
    [SerializeField] private AIArchetypeConfig _archetypeConfig;

    private bool _archetypeInitializationAttempted;
    private bool _archetypeReady;
    private Transform _targetTransform;

    [Header("Perception State")]
    [SerializeField, ReadOnly] private GameObject _target;
    [SerializeField, ReadOnly] private Vector3 _targetPosition;
    [SerializeField, ReadOnly] private bool _hasLineOfSight;
    [SerializeField, ReadOnly] private float _distanceToTarget;
    [SerializeField, ReadOnly] private AlertState _alertState = AlertState.Unaware;

    [Header("Movement State")]
    [SerializeField, ReadOnly] private Vector3 _moveDestination;
    [SerializeField, ReadOnly] private bool _hasMoveDestination;
    [SerializeField, ReadOnly] private bool _isAtDestination;
    [SerializeField, ReadOnly] private float _desiredSpeed;
    [SerializeField, ReadOnly] private int _moveRequestVersion;
    [SerializeField, ReadOnly] private MovementStatus _moveStatus = MovementStatus.None;

    [Header("Combat State")]
    [SerializeField, ReadOnly] private bool _wantsToShoot;
    [SerializeField, ReadOnly] private bool _inCover;
    [SerializeField, ReadOnly] private Vector3 _coverPosition;

    [Header("Cover Stance State")]
    [SerializeField, ReadOnly] private bool _isPeeking;
    [SerializeField, ReadOnly] private CoverHeight _coverHeight;
    [SerializeField, ReadOnly] private CoverPeekDirection _activePeekDirection;

    public AIArchetypeConfig ArchetypeConfig => _archetypeConfig;
    public bool IsArchetypeReady => _archetypeReady && _archetypeConfig;
    public Vector3 AgentPosition => transform.position;
    public GameObject Target => _target;
    public Transform TargetTransform => _targetTransform;
    public Vector3 TargetPosition => _targetPosition;
    public bool HasLineOfSight => _hasLineOfSight;
    public float DistanceToTarget => _distanceToTarget;
    public AlertState CurrentAlertState => _alertState;

    public Vector3 MoveDestination => _moveDestination;
    public bool HasMoveDestination => _hasMoveDestination;
    public bool IsAtDestination => _isAtDestination;
    public float DesiredSpeed => _desiredSpeed;
    public int MoveRequestVersion => _moveRequestVersion;
    public MovementStatus MoveStatus => _moveStatus;

    public bool WantsToShoot => _wantsToShoot;
    public bool InCover => _inCover;
    public Vector3 CoverPosition => _coverPosition;

    public bool IsPeeking => _isPeeking;
    public CoverHeight CoverHeight => _coverHeight;
    public CoverPeekDirection ActivePeekDirection => _activePeekDirection;

    public event Action<GameObject> OnTargetChanged;
    public event Action<AlertState> OnAlertStateChanged;

    public bool TryInitializeArchetype()
    {
        if (_archetypeInitializationAttempted)
            return IsArchetypeReady;

        _archetypeInitializationAttempted = true;
        if (!_archetypeConfig)
        {
            AppLog.LogError(LogChannel.AI, "AI simulation disabled: assign an archetype to the blackboard.", this);
            return false;
        }

        if (!_archetypeConfig.TryValidate(out string error))
        {
            AppLog.LogError(LogChannel.AI, error, this);
            return false;
        }

        _archetypeReady = true;
        return true;
    }

    public void ResetRuntimeState()
    {
        ClearTarget();
        ClearMoveDestination();
        SetCombatIntent(false);
        SetCoverStance(false, CoverHeight.Low);
        SetAlertState(AlertState.Unaware);
    }

    public void SetTarget(GameObject newTarget, Vector3 lastKnownPos, bool hasLOS, float distance)
    {
        bool targetChanged = _target != newTarget;

        _target = newTarget;
        _targetPosition = lastKnownPos;
        _hasLineOfSight = hasLOS;
        _distanceToTarget = distance;

        if (targetChanged)
        {
            _targetTransform = newTarget
                ? newTarget.TryGetComponent(out PlayerIdentifier player) ? player.BodyTransform : newTarget.transform
                : null;
            OnTargetChanged?.Invoke(newTarget);
        }
    }

    public void SetCoverStance(bool isPeeking, CoverHeight coverHeight,
        CoverPeekDirection direction = CoverPeekDirection.None)
    {
        _isPeeking = isPeeking;
        _coverHeight = coverHeight;
        _activePeekDirection = direction;
    }

    public void SetMoveDestination(Vector3 destination, float speed)
    {
        if (!float.IsFinite(destination.x) || !float.IsFinite(destination.y) || !float.IsFinite(destination.z) ||
            !float.IsFinite(speed) || speed < 0f)
        {
            ClearMoveDestination();
            return;
        }

        if (!_hasMoveDestination || !destination.Equals(_moveDestination))
        {
            _moveRequestVersion = unchecked(_moveRequestVersion + 1);
            _moveDestination = destination;
            _moveStatus = MovementStatus.Pending;
            _hasMoveDestination = true;
            _isAtDestination = false;
        }

        _desiredSpeed = speed;
    }

    public void ReportMoveResult(int requestVersion, MovementStatus status)
    {
        if (!_hasMoveDestination || requestVersion != _moveRequestVersion)
            return;

        if (status is not (MovementStatus.Pending or MovementStatus.Moving or MovementStatus.Arrived or MovementStatus.Failed))
            return;

        _moveStatus = status;
        _isAtDestination = status == MovementStatus.Arrived;
    }

    public void SetAlertState(AlertState state)
    {
        if (_alertState == state)
            return;

        _alertState = state;
        OnAlertStateChanged?.Invoke(_alertState);
    }

    public void SetAtDestination(bool isAtDestination)
    {
        ReportMoveResult(_moveRequestVersion, isAtDestination ? MovementStatus.Arrived : MovementStatus.Moving);
    }

    public void SetCombatIntent(bool shoot, bool inCover = false, Vector3 coverPos = default)
    {
        _wantsToShoot = shoot;
        _inCover = inCover;
        _coverPosition = coverPos;
    }

    public void ClearTarget()
    {
        bool targetChanged = !ReferenceEquals(_target, null);
        _target = null;
        _targetTransform = null;
        _hasLineOfSight = false;
        _targetPosition = Vector3.zero;
        _distanceToTarget  = 0;

        if (targetChanged)
            OnTargetChanged?.Invoke(null);
    }

    public void ClearMoveDestination()
    {
        if (_hasMoveDestination)
            _moveRequestVersion = unchecked(_moveRequestVersion + 1);

        _moveDestination = Vector3.zero;
        _desiredSpeed = 0f;
        _hasMoveDestination = false;
        _isAtDestination = false;
        _moveStatus = MovementStatus.None;
    }
}
}
