using Project.AI.Blackboard;
using PurrNet;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Locomotion
{
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class AINavMeshLocomotion : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private AIBlackboard _blackboard;
    [SerializeField] private NavMeshAgent _agent;
    
    [Header("Settings")]
    [SerializeField] private float _repathInterval = 0.2f; // 5hz
    [SerializeField] private float _destinationThresholdSqr = 0.25f; // 0.5m

    private float _repathTimer;
    private Vector3 _lastSetDestination;
    
    private void Awake()
    {
        if (!_blackboard) 
            _blackboard = GetComponentInChildren<AIBlackboard>();

        if (!_agent) 
            _agent = GetComponent<NavMeshAgent>();
    }

    protected override void OnSpawned()
    {
        if (!isServer)
        {
            _agent.enabled = false;
            enabled = false;
            return;
        }

        _repathTimer = 0f;
        _lastSetDestination = Vector3.positiveInfinity;
    }

    protected override void OnDespawned()
    {
        if (isServer && _agent && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
        }
    }

    private void Update()
    {
        if (!_agent.isOnNavMesh) return;
        UpdateNavigation();
        CheckArrival();
    }

    private void UpdateNavigation()
    {
        if (!_blackboard.HasMoveDestination)
        {
            if (!_agent.isStopped && _agent.hasPath)
            {
                _agent.ResetPath();
                _lastSetDestination = Vector3.positiveInfinity;
            }
            return;
        }

        if (!Mathf.Approximately(_agent.speed, _blackboard.DesiredSpeed)) 
            _agent.speed = _blackboard.DesiredSpeed;
        
        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f) 
        {
            _repathTimer = _repathInterval;
            
            Vector3 targetDestination = _blackboard.MoveDestination;
            float distanceMovedSqr = (targetDestination - _lastSetDestination).sqrMagnitude;

            if (distanceMovedSqr >= _destinationThresholdSqr)
            {
                _agent.SetDestination(targetDestination);
                _lastSetDestination = targetDestination;
            }
        }
    }

    private void CheckArrival()
    {
        if (!_blackboard.HasMoveDestination)
            return;

        if (_agent.pathPending) 
            return;
        
        if (_agent.remainingDistance <= _agent.stoppingDistance)
        {
            if (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f)
            {
                _blackboard.SetAtDestination(true);
            } 
        }
        else
        {
            _blackboard.SetAtDestination(false);
        }
    }
}
}