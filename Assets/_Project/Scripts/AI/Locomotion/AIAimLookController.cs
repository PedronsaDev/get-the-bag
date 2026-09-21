using System;
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

    private void Awake()
    {
        if (!_blackboard) 
            _blackboard = GetComponent<AIBlackboard>();

        if (!_agent) 
            _agent = GetComponent<NavMeshAgent>();
    }

    protected override void OnSpawned()
    {
        if (!isServer)
        {
            enabled = false;
            return;
        }
    }

    protected override void OnDespawned()
    {
        if (_agent) 
            _agent.updateRotation = true;
        IsAimAligned = false;
    }

    private void Update()
    {
        UpdateAimAndRotation(Time.deltaTime);
    }

    private void UpdateAimAndRotation(float deltaTime)
    {
        bool shouldAimAtTarget = _blackboard.CurrentAlertState is AlertState.Alerted or AlertState.Suspicious or AlertState.Searching;

        if (shouldAimAtTarget && _blackboard.TargetPosition != Vector3.zero)
        {
            _agent.updateRotation = false;

            Vector3 targetPoint = _blackboard.TargetPosition + (Vector3.up * _aimHeightOffset);
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