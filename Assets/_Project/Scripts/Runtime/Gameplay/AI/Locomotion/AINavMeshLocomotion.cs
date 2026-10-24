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
    private const float MinimumArrivalTolerance = 0.01f;

    [Header("References")]
    [SerializeField] private AIBlackboard _blackboard;
    [SerializeField] private NavMeshAgent _agent;

    [Header("Settings")]
    [SerializeField] private float _repathInterval = 0.2f; // 5hz

    private float _repathTimer;
    private Vector3 _lastSetDestination;

    private int _submittedRequestVersion;
    private int _submittedFrame;
    private bool _hasRequestedPath;

    private void Awake()
    {
        if (!_blackboard)
            _blackboard = GetComponentInChildren<AIBlackboard>();

        if (!_agent)
            _agent = GetComponent<NavMeshAgent>();
    }

    protected override void OnSpawned()
    {
        if (!isServer || !_blackboard || !_blackboard.TryInitializeArchetype() || !_agent)
        {
            if (_agent)
                _agent.enabled = false;
            enabled = false;
            return;
        }

        _agent.enabled = true;
        enabled = true;
        ResetNavigation();
    }

    protected override void OnDespawned()
    {
        StopNavigation();
    }

    private void OnDisable() => StopNavigation();

    private void StopNavigation()
    {
        if (isServer && _blackboard && _blackboard.HasMoveDestination)
            _blackboard.ReportMoveResult(_blackboard.MoveRequestVersion, MovementStatus.Failed);

        ResetNavigation();
    }

    private void ResetNavigation()
    {
        if (isServer && _agent && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            if (_agent.hasPath || _agent.pathPending)
                _agent.ResetPath();
        }
        _hasRequestedPath = false;
        _lastSetDestination = Vector3.positiveInfinity;
        _submittedRequestVersion = 0;
        _submittedFrame = -1;
        _repathTimer = 0f;
    }

    private void Update()
    {
        if (!isServer || !_blackboard || !_blackboard.IsArchetypeReady)
            return;

        if (!_agent || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
        {
            StopNavigation();
            return;
        }

        UpdateNavigation();
        CheckArrival();
    }

    private void UpdateNavigation()
    {
        if (!_blackboard.HasMoveDestination || _blackboard.MoveStatus == MovementStatus.Failed)
        {
            ResetNavigation();
            return;
        }

        if (!Mathf.Approximately(_agent.speed, _blackboard.DesiredSpeed))
            _agent.speed = _blackboard.DesiredSpeed;

        if (_repathTimer > 0f)
            _repathTimer -= Time.deltaTime;

        bool needsSubmission = !_hasRequestedPath || _submittedRequestVersion != _blackboard.MoveRequestVersion;
        if (!needsSubmission || _repathTimer > 0f || _agent.pathPending)
            return;

        Vector3 requestedDestination = _blackboard.MoveDestination;
        int requestedVersion = _blackboard.MoveRequestVersion;
        _repathTimer = float.IsFinite(_repathInterval) && _repathInterval > 0f ? _repathInterval : 0.2f;

        if (!_agent.SetDestination(requestedDestination))
        {
            FailRequest(requestedVersion);
            return;
        }

        _agent.isStopped = false;
        _lastSetDestination = requestedDestination;
        _submittedRequestVersion = requestedVersion;
        _submittedFrame = Time.frameCount;
        _hasRequestedPath = true;
        _blackboard.ReportMoveResult(_submittedRequestVersion, MovementStatus.Moving);
    }

    private void CheckArrival()
    {
        if (!_blackboard.HasMoveDestination || !_hasRequestedPath ||
            _submittedRequestVersion != _blackboard.MoveRequestVersion ||
            _blackboard.MoveStatus == MovementStatus.Failed ||
            _agent.pathPending || _submittedFrame == Time.frameCount)
            return;

        if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            FailRequest(_submittedRequestVersion);
            return;
        }

        // Unity can consume a complete path millimetres short of its endpoint, even with a zero stopping distance.
        float stoppingDistance = Mathf.Max(_agent.stoppingDistance, MinimumArrivalTolerance);
        float remainingDistance = _agent.hasPath ? _agent.remainingDistance :
            Vector3.Distance(transform.position, _lastSetDestination);
        if (float.IsNaN(remainingDistance) || remainingDistance < 0f ||
            (!_agent.hasPath && remainingDistance > stoppingDistance))
        {
            FailRequest(_submittedRequestVersion);
            return;
        }

        // Positive infinity means Unity has not established the remaining distance yet.
        bool arrived = remainingDistance <= stoppingDistance && _agent.velocity.sqrMagnitude < 0.01f;
        _blackboard.ReportMoveResult(_submittedRequestVersion, arrived ? MovementStatus.Arrived : MovementStatus.Moving);
    }

    private void FailRequest(int requestVersion)
    {
        _blackboard.ReportMoveResult(requestVersion, MovementStatus.Failed);
        ResetNavigation();
    }
}
}
