using System;
using Project.AI.Blackboard;
using Project.AI.Decision.States;
using Project.AI.Perception;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.AI.Decision
{
public class AIBrain : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private AIBlackboard _blackboard;

    private readonly AIIdleState _idleState = new();
    private readonly AIInvestigateState _investigationState = new();
    private readonly AICombatState _combatState = new();

    public IAIState CurrentState { get; private set; }
    private bool _running;

    private void Awake()
    {
        if (!_blackboard)
            _blackboard = GetComponent<AIBlackboard>();
    }

    protected override void OnSpawned()
    {
        StopBrain();
        if (!isServer)
        {
            enabled = false;
            return;
        }
        if (!_blackboard)
        {
            AppLog.LogError(LogChannel.AI, "AI brain requires a blackboard.", this);
            enabled = false;
            return;
        }

        if (!_blackboard.TryInitializeArchetype())
        {
            enabled = false;
            return;
        }

        _blackboard.ResetRuntimeState();
        enabled = true;
        _running = true;
        _blackboard.OnAlertStateChanged += HandleStateChanged;
        HandleStateChanged(_blackboard.CurrentAlertState);
    }
    protected override void OnDespawned()
    {
        StopBrain();
    }

    private void OnDisable() => StopBrain();

    private void StopBrain()
    {
        _running = false;
        if (_blackboard)
        {
            _blackboard.OnAlertStateChanged -= HandleStateChanged;
            CurrentState?.OnExit(_blackboard);
        }
        CurrentState = null;
    }

    private void Update()
    {
        if (!isServer || !_running || !_blackboard || !_blackboard.IsArchetypeReady)
            return;
        CurrentState?.OnUpdate(_blackboard, Time.deltaTime);
    }

    private void HandleStateChanged(AlertState newState)
    {
        if (!_running || !isServer || !_blackboard.IsArchetypeReady)
            return;
        switch (newState)
        {
            case AlertState.Unaware:
                TransitionTo(_idleState);
                break;
            case AlertState.Suspicious:
                TransitionTo(_investigationState);
                break;
            case AlertState.Alerted:
                TransitionTo(_combatState);
                break;
            case AlertState.Searching:
                if (CurrentState == _combatState && _combatState.IsUsingCover && _blackboard.Target)
                    break;
                TransitionTo(_investigationState);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }

    public void TransitionTo(IAIState newState)
    {
        if (!_running || !isServer || !_blackboard || !_blackboard.IsArchetypeReady)
            return;
        if (CurrentState == newState)
            return;

        CurrentState?.OnExit(_blackboard);
        CurrentState = newState;
        CurrentState?.OnEnter(_blackboard);
    }
}
}
