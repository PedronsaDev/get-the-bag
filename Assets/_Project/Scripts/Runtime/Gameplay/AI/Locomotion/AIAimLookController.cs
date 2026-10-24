using Project.AI.Blackboard;
using Project.AI.Perception;
using PurrNet;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Locomotion
{
public class AIAimLookController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private AIBlackboard _blackboard;
    [SerializeField] private NavMeshAgent _agent;

    [Header("Aim Settings")]
    [SerializeField] private float _turnSpeedDegrees = 360.0f;
    [SerializeField] private float _aimHeightOffset = 1.4f;
    [SerializeField] private float _aimAlignmentThresholdDot = 0.96f;

    public bool IsAimAligned { get; private set; }
    public Vector3 AimTargetPoint { get; private set; }

    public Vector3 GetAimPoint(Vector3 rememberedPosition) => rememberedPosition + Vector3.up * _aimHeightOffset;

    private void Awake()
    {
        if (!_blackboard)
            _blackboard = GetComponent<AIBlackboard>();

        if (!_agent)
            _agent = GetComponent<NavMeshAgent>();
    }

    protected override void OnSpawned()
    {
        if (!isServer || !_blackboard || !_blackboard.TryInitializeArchetype() || !_agent)
        {
            enabled = false;
            return;
        }
        enabled = true;
        IsAimAligned = false;
    }

    protected override void OnDespawned()
    {
        if (_agent)
            _agent.updateRotation = true;
        IsAimAligned = false;
    }

    private void Update()
    {
        if (!isServer || !_blackboard || !_blackboard.IsArchetypeReady || !_agent ||
            !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
        {
            IsAimAligned = false;
            return;
        }
        UpdateAimAndRotation(Time.deltaTime);
    }

    private void UpdateAimAndRotation(float deltaTime)
    {
        IsAimAligned = false;
        bool shouldAimAtTarget = _blackboard.CurrentAlertState is AlertState.Alerted or AlertState.Suspicious or AlertState.Searching;

        if (shouldAimAtTarget && (_blackboard.Target || _blackboard.CurrentAlertState == AlertState.Suspicious))
        {
            _agent.updateRotation = false;

            Vector3 targetPoint = GetAimPoint(_blackboard.TargetPosition);
            AimTargetPoint = targetPoint;
            Vector3 planarDirection = targetPoint - transform.position;
            planarDirection.y = 0f;
            if (planarDirection.sqrMagnitude > 0.001f)
            {
                planarDirection.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(planarDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeedDegrees * deltaTime);
                float alignment = Vector3.Dot(transform.forward, planarDirection);
                IsAimAligned = alignment >= _aimAlignmentThresholdDot;
            }
        }
        else
        {
            _agent.updateRotation = true;
            IsAimAligned = false;
            AimTargetPoint = transform.position + (transform.forward * 5.0f);
        }
    }
}
}
