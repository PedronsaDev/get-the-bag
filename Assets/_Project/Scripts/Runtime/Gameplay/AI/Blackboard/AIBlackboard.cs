using System;
using NaughtyAttributes;
using Project.AI.Combat;
using Project.AI.Cover;
using Project.AI.Perception;
using UnityEngine;

namespace Project.AI.Blackboard
{
[DisallowMultipleComponent]
public class AIBlackboard : MonoBehaviour
{
    [Header("References")]
    [Expandable]
    [SerializeField] private AIMovementConfig _movementConfig;
    [Expandable]
    [SerializeField] private AICoverScoringConfig _aiCoverScoringConfig;
    
    [Header("Perception State")] 
    [SerializeField] private GameObject _target;
    [SerializeField] private Vector3 _targetPosition;
    [SerializeField] private bool _hasLineOfSight;
    [SerializeField] private float _distanceToTarget;
    [SerializeField] private AlertState _alertState = AlertState.Unaware;

    [Header("Movement State")] 
    [SerializeField] private Vector3 _moveDestination;
    [SerializeField] private bool _hasMoveDestination;
    [SerializeField] private bool _isAtDestination;
    [SerializeField] private float _desiredSpeed;

    [Header("Combat State")] 
    [SerializeField] private bool _wantsToShoot;
    [SerializeField] private bool _inCover;
    [SerializeField] private Vector3 _coverPosition;

    [Header("Cover Stance State")] 
    [SerializeField] private bool _isPeeking;
    [SerializeField] private CoverHeight _coverHeight;
    [SerializeField] private CoverPeekDirection _activePeekDirection;

    // Public Accessors (Properties)
    public AIMovementConfig MovementConfig => _movementConfig;
    public AICoverScoringConfig AICoverScoringConfig => _aiCoverScoringConfig;
    public Vector3 AgentPosition => transform.position;
    public GameObject Target => _target;
    public Vector3 TargetPosition => _targetPosition;
    public bool HasLineOfSight => _hasLineOfSight;
    public float DistanceToTarget => _distanceToTarget;
    public AlertState CurrentAlertState => _alertState;

    public Vector3 MoveDestination => _moveDestination;
    public bool HasMoveDestination => _hasMoveDestination;
    public bool IsAtDestination => _isAtDestination;
    public float DesiredSpeed => _desiredSpeed;

    public bool WantsToShoot => _wantsToShoot;
    public bool InCover => _inCover;
    public Vector3 CoverPosition => _coverPosition;
    
    public bool IsPeeking => _isPeeking;
    public CoverHeight CoverHeight => _coverHeight;
    public CoverPeekDirection ActivePeekDirection => _activePeekDirection;

    public event Action<GameObject> OnTargetChanged;
    public event Action<AlertState> OnAlertStateChanged;

    public void SetTarget(GameObject newTarget, Vector3 lastKnownPos, bool hasLOS, float distance)
    {
        bool targetChanged = _target != newTarget;
        
        _target = newTarget;
        _targetPosition = lastKnownPos;
        _hasLineOfSight = hasLOS;
        _distanceToTarget = distance;

        if (targetChanged) 
            OnTargetChanged?.Invoke(newTarget);
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
        _moveDestination = destination;
        _desiredSpeed = speed;
        _hasMoveDestination = true;
        _isAtDestination = false;
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
        _isAtDestination = isAtDestination;
    }

    public void SetCombatIntent(bool shoot, bool inCover = false, Vector3 coverPos = default)
    {
        _wantsToShoot = shoot;
        _inCover = inCover;
        _coverPosition = coverPos;
    }

    public void ClearTarget()
    {
        if (!_target) 
            return;
        
        _target = null;
        _hasLineOfSight = false;
        _targetPosition = Vector3.zero;
        _distanceToTarget  = 0;
        
        OnTargetChanged?.Invoke(null);
    }
    
    public void ClearMoveDestination()
    {
        _moveDestination = Vector3.zero;
        _hasMoveDestination = false;
        _isAtDestination = false;
    }
}
}