using System;
using Project.AI.Blackboard;
using Project.AI.Decision.States;
using Project.AI.Perception;
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
        if (_blackboard)
        {
            _blackboard.OnAlertStateChanged += HandleStateChanged;
            HandleStateChanged(_blackboard.CurrentAlertState);
        }
    }
    protected override void OnDespawned()
    {
        if (isServer && _blackboard)
        {
            _blackboard.OnAlertStateChanged -= HandleStateChanged;
        }
        
        CurrentState?.OnExit(_blackboard);
        CurrentState = null;
    }

    private void Update()
    {
        CurrentState?.OnUpdate(_blackboard, Time.deltaTime);
    }
    
    private void HandleStateChanged(AlertState newState)
    {
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
                TransitionTo(_investigationState);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }
    
    public void TransitionTo(IAIState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState?.OnExit(_blackboard);
        CurrentState = newState;
        CurrentState?.OnEnter(_blackboard);
    }
}
}