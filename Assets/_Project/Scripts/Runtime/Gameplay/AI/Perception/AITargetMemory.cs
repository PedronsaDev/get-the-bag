using System;
using Project.AI.Blackboard;
using PurrNet;
using UnityEngine;

namespace Project.AI.Perception
{
public class AITargetMemory : NetworkBehaviour
{
    [Header("Detection Tuning")] 
    [SerializeField] private float _baseDetectionRate = 3.5f;
    [SerializeField] private float _decayRate = 0.25f;
    [SerializeField] private float _searchDuration = 5.0f;
    
    [Header("References")]
    [SerializeField] private AIBlackboard _blackboard;

    public AlertState CurrentState { get; private set; } = AlertState.Unaware;
    public TargetRecord CurrentTarget => _primaryTarget;
    public bool HasTarget => _primaryTarget.Target;

    public event Action<AlertState> OnStateChanged;

    private TargetRecord _primaryTarget;
    private float _searchTimer;

    private void Awake()
    {
        if (!_blackboard) 
            _blackboard = GetComponent<AIBlackboard>();
    }

    protected override void OnSpawned()
    {
        if (!isServer)
        {
            enabled = false;
            return;
        }
        
        ClearTarget();
        CurrentState = AlertState.Unaware;
        _searchTimer = 0f;
    }

    private void Update()
    {
        UpdateMemory(Time.deltaTime);
    }

    private void UpdateMemory(float deltaTime)
    {
        if (_primaryTarget.HasLineOfSight)
        {
            if (_primaryTarget.SuspicionLevel >= 1.0f)
            {
                ChangeState(AlertState.Alerted);
            }
            else if (_primaryTarget.SuspicionLevel > 0f)
            {
                ChangeState(AlertState.Suspicious);
            }
        }
        else
        {
            _primaryTarget.SuspicionLevel = Mathf.Clamp01(_primaryTarget.SuspicionLevel - (_decayRate * deltaTime));

            if (CurrentState == AlertState.Searching)
            {
                _searchTimer -= deltaTime;
                if (_searchTimer <= 0f)
                {
                    ChangeState(_primaryTarget.SuspicionLevel > 0f ? AlertState.Suspicious : AlertState.Unaware);
                }
            }
            else if (CurrentState == AlertState.Suspicious && _primaryTarget.SuspicionLevel <= 0f)
            {
                ClearTarget();
                ChangeState(AlertState.Unaware);
            }
        }
    }

    // Reports
    public void ReportVisualContact(GameObject target, Vector3 worldPosition, float distance, float maxDistance,
        bool isPeripheral, float deltaTime)
    {
        _primaryTarget.Target = target;
        _primaryTarget.HasLineOfSight = true;
        _primaryTarget.LastKnownPosition = worldPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;

        float proximity = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, maxDistance));
        float coneModifier = isPeripheral ? 0.35f : 1.0f;
        float buildRate = _baseDetectionRate * (proximity * proximity) * coneModifier;

        _primaryTarget.SuspicionLevel = Mathf.Clamp01(_primaryTarget.SuspicionLevel + (buildRate * deltaTime));

        if (CurrentState == AlertState.Searching)
        {
            ChangeState(AlertState.Alerted);
        }
        
        SyncToBlackboard();
    }

    public void ReportLostSight()
    {
        if (!_primaryTarget.HasLineOfSight) return;

        _primaryTarget.HasLineOfSight = false;

        if (CurrentState == AlertState.Alerted)
        {
            _searchTimer = _searchDuration;
            ChangeState(AlertState.Searching);
        }
        
        SyncToBlackboard();
    }

    public void ReportNoise(Vector3 soundPosition, float intensity)
    {
        _primaryTarget.LastKnownPosition = soundPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;

        float bump = intensity >= 20f ? 1.0f : 0.4f;
        _primaryTarget.SuspicionLevel = Mathf.Clamp01(_primaryTarget.SuspicionLevel + bump);

        if (_primaryTarget.SuspicionLevel >= 1.0f)
            ChangeState(AlertState.Alerted);
        else if (CurrentState == AlertState.Unaware)
            ChangeState(AlertState.Suspicious);
        
        SyncToBlackboard();
    }

    public void ReportDamage(GameObject attacker, Vector3 attackerPosition)
    {
        _primaryTarget.Target = attacker;
        _primaryTarget.LastKnownPosition = attackerPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;
        _primaryTarget.SuspicionLevel = 1.0f;
        ChangeState(AlertState.Alerted);
        
        SyncToBlackboard();
    }

    public void ClearTarget()
    {
        _primaryTarget = default;
        SyncToBlackboard();
    }

    // States
    private void ChangeState(AlertState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);
        
        _blackboard?.SetAlertState(newState);
    }
    
    // Blackboard Helper
    private void SyncToBlackboard()
    {
        if (!_blackboard) 
            return;
        
        if (_primaryTarget.Target)
        {
            float distance = Vector3.Distance(transform.position, _primaryTarget.LastKnownPosition);
            _blackboard.SetTarget(_primaryTarget.Target, _primaryTarget.LastKnownPosition, _primaryTarget.HasLineOfSight, distance);
        } 
        else if (_primaryTarget.LastKnownPosition != Vector3.zero && CurrentState != AlertState.Unaware)
        {
            float distance = Vector3.Distance(transform.position, _primaryTarget.LastKnownPosition);
            _blackboard.SetTarget(null, _primaryTarget.LastKnownPosition, false, distance);
        }
        else
        {
            _blackboard.ClearTarget();
        }
    }
}
}