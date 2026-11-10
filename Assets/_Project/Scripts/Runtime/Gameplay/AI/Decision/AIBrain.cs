using System;
using Project.AI.Alarm;
using Project.AI.Blackboard;
using Project.AI.Configuration;
using Project.AI.Decision.States;
using Project.AI.Perception;
using Project.Combat.Health;
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
    private readonly AIPatrolState _patrolState = new();
    private readonly AIFleeState _fleeState = new();
    private readonly AIInvestigateState _investigationState = new();
    private readonly AICombatState _combatState = new();

    public IAIState CurrentState { get; private set; }
    [SerializeField, Min(0f)] private float _corpseSeconds = 15f;

    private bool _running;
    private HealthComponent _health;
    private float _despawnAt;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>(true);
        if (!_blackboard)
            _blackboard = GetComponent<AIBlackboard>();
    }

    protected override void OnSpawned()
    {
        StopBrain();
        _despawnAt = 0f;
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
        if (isServer && _despawnAt > 0f && Time.time >= _despawnAt)
        {
            Despawn();
            return;
        }
        if (!isServer || !_running || !_blackboard || !_blackboard.IsArchetypeReady)
            return;
        if (_health && _health.IsDead)
        {
            if (_blackboard.ArchetypeConfig.Role.Flees() && AlarmDirector.Instance)
                AlarmDirector.Instance.ReportCivilianKilled(transform.position);
            StopBrain();
            _despawnAt = Time.time + _corpseSeconds;
            return;
        }
        CurrentState?.OnUpdate(_blackboard, Time.deltaTime);
    }

    private void HandleStateChanged(AlertState newState)
    {
        if (!_running || !isServer || !_blackboard.IsArchetypeReady)
            return;
        // A civilian who is running to a phone or making the call finishes first.
        if (CurrentState == _fleeState && _fleeState.IsBusy)
            return;

        // Taking cover breaks line of sight on purpose, so a fight in cover survives Searching.
        if (newState == AlertState.Searching && CurrentState == _combatState && _combatState.IsUsingCover &&
            _blackboard.Target)
            return;

        var kind = AIStateSelection.Select(_blackboard.ArchetypeConfig.Role, newState, _blackboard.HasPatrolRoute);
        TransitionTo(StateFor(kind));
    }

    private IAIState StateFor(AIStateKind kind)
    {
        switch (kind)
        {
            case AIStateKind.Idle:
                return _idleState;
            case AIStateKind.Patrol:
                return _patrolState;
            case AIStateKind.Flee:
                return _fleeState;
            case AIStateKind.Investigate:
                return _investigationState;
            case AIStateKind.Combat:
                return _combatState;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
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
