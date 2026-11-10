using System;
using Project.AI.Alarm;
using Project.AI.Blackboard;
using Project.AI.Configuration;
using Project.Combat.Core;
using Project.Combat.Health;
using Project.Core.Player;
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
    [SerializeField, Range(0f, 0.99f), Tooltip("Suspicion after finding a body. Below 1 so it never alerts by itself.")]
    private float _bodySuspicion = 0.95f;

    [Header("References")]
    [SerializeField] private AIBlackboard _blackboard;

    public AlertState CurrentState { get; private set; } = AlertState.Unaware;
    public TargetRecord CurrentTarget => _primaryTarget;
    public bool HasTarget => _primaryTarget.Target;

    public event Action<AlertState> OnStateChanged;

    private TargetRecord _primaryTarget;
    private float _searchTimer;
    private bool _huntPending;

    private HealthComponent _health;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>(true);
        if (!_blackboard)
            _blackboard = GetComponent<AIBlackboard>();
    }

    protected override void OnSpawned()
    {
        if (!isServer || !_blackboard || !_blackboard.TryInitializeArchetype())
        {
            enabled = false;
            return;
        }

        CurrentState = AlertState.Unaware;
        _searchTimer = 0f;
        enabled = true;
        ClearTarget();
        _huntPending = true;
        if (_health)
            _health.OnDamaged += HandleDamaged;
    }

    private void HandleDamaged(DamageResult damage)
    {
        if (!damage.Applied || !damage.AttackerId.HasValue || (_health && _health.IsDead))
            return;
        if (PlayerIdentifier.TryGetPlayer(damage.AttackerId.Value, out var attacker) && !attacker.IsDead)
            ReportDamage(attacker.gameObject, attacker.BodyTransform.position);
    }

    protected override void OnDespawned()
    {
        if (_health)
            _health.OnDamaged -= HandleDamaged;
        _primaryTarget = default;
        CurrentState = AlertState.Unaware;
        _searchTimer = 0f;
    }

    private void Update()
    {
        if (!isServer || !_blackboard || !_blackboard.IsArchetypeReady)
            return;
        UpdateMemory(Time.deltaTime);
    }

    private void UpdateMemory(float deltaTime)
    {
        if (_huntPending)
        {
            _huntPending = false;
            if (ShouldHunt())
                HuntNearestPlayer();
        }

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
                    if (ShouldHunt() && HuntNearestPlayer())
                        return;

                    if (_primaryTarget.SuspicionLevel > 0f)
                    {
                        ChangeState(AlertState.Suspicious);
                    }
                    else
                    {
                        ClearTarget();
                        ChangeState(AlertState.Unaware);
                    }
                }
            }
            else if (CurrentState == AlertState.Alerted)
            {
                // Alerted without sight (loud noise, unseen attacker): search instead of staying alerted forever.
                _searchTimer = _searchDuration;
                ChangeState(AlertState.Searching);
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
        if (!CanReceiveReports || !target)
            return;
        if (_primaryTarget.Target && _primaryTarget.Target != target)
            _primaryTarget.SuspicionLevel = 0f;
        _primaryTarget.Target = target;
        _primaryTarget.HasLineOfSight = true;
        _primaryTarget.LastKnownPosition = worldPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;

        float proximity = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, maxDistance));
        float coneModifier = isPeripheral ? 0.35f : 1.0f;
        float buildRate = _baseDetectionRate * (proximity * proximity) * coneModifier;

        _primaryTarget.SuspicionLevel = Mathf.Clamp01(_primaryTarget.SuspicionLevel + (buildRate * deltaTime));

        // Publish to the blackboard before announcing a state change: listeners read it synchronously.
        SyncToBlackboard();

        if (CurrentState == AlertState.Searching)
        {
            ChangeState(AlertState.Alerted);
        }
    }

    public void ReportLostSight()
    {
        if (!CanReceiveReports)
            return;
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
        if (!CanReceiveReports)
            return;
        // Unrelated noise must not replace a visible or remembered combat target's position.
        if (_primaryTarget.HasLineOfSight ||
            (_primaryTarget.Target && CurrentState is AlertState.Alerted or AlertState.Searching))
            return;
        _primaryTarget.LastKnownPosition = soundPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;

        float bump = intensity >= 20f ? 1.0f : 0.4f;
        _primaryTarget.SuspicionLevel = Mathf.Clamp01(_primaryTarget.SuspicionLevel + bump);

        SyncToBlackboard();

        if (_primaryTarget.SuspicionLevel >= 1.0f)
            ChangeState(AlertState.Alerted);
        else if (CurrentState == AlertState.Unaware)
            ChangeState(AlertState.Suspicious);
    }

    public void ReportBodyFound(Vector3 bodyPosition)
    {
        if (!CanReceiveReports)
            return;
        if (_primaryTarget.HasLineOfSight ||
            (_primaryTarget.Target && CurrentState is AlertState.Alerted or AlertState.Searching))
            return;
        _primaryTarget.LastKnownPosition = bodyPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;
        _primaryTarget.SuspicionLevel = Mathf.Max(_primaryTarget.SuspicionLevel, _bodySuspicion);

        SyncToBlackboard();

        if (CurrentState == AlertState.Unaware)
            ChangeState(AlertState.Suspicious);
    }

    private bool ShouldHunt()
    {
        var alarm = AlarmDirector.Instance;
        return alarm && alarm.State == AlarmState.Alarmed && _blackboard && _blackboard.IsArchetypeReady &&
               _blackboard.ArchetypeConfig.Role.Fights();
    }

    private bool HuntNearestPlayer()
    {
        Vector3 here = transform.position;
        Vector3 nearest = default;
        float nearestDistance = float.MaxValue;
        foreach (var player in PlayerIdentifier.ActivePlayers)
        {
            if (!player || player.IsDead)
                continue;
            Vector3 position = player.BodyTransform.position;
            float distance = (position - here).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = position;
            }
        }

        if (nearestDistance == float.MaxValue)
            return false;

        ReportAlarm(nearest);
        return true;
    }

    public void ReportAlarm(Vector3 alarmPosition)
    {
        if (!CanReceiveReports || !_blackboard.ArchetypeConfig.Role.Fights() || _primaryTarget.HasLineOfSight)
            return;
        _primaryTarget.LastKnownPosition = alarmPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;
        _primaryTarget.SuspicionLevel = 1.0f;

        SyncToBlackboard();
        ChangeState(AlertState.Alerted);
    }

    public void ReportDamage(GameObject attacker, Vector3 attackerPosition)
    {
        if (!CanReceiveReports)
            return;
        _primaryTarget.Target = attacker;
        _primaryTarget.LastKnownPosition = attackerPosition;
        _primaryTarget.LastSeenTimestamp = Time.time;
        _primaryTarget.SuspicionLevel = 1.0f;

        SyncToBlackboard();
        ChangeState(AlertState.Alerted);
    }

    public void ClearTarget()
    {
        if (!CanReceiveReports)
            return;
        _primaryTarget = default;
        SyncToBlackboard();
    }

    // States
    private bool CanReceiveReports => isServer && isActiveAndEnabled && _blackboard && _blackboard.IsArchetypeReady;

    private void ChangeState(AlertState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);

        _blackboard?.SetAlertState(newState);

        if (newState == AlertState.Alerted && _blackboard && _blackboard.ArchetypeConfig.Role.Fights() &&
            AlarmDirector.Instance)
            AlarmDirector.Instance.Raise(AlarmReason.GuardAlerted, _primaryTarget.LastKnownPosition);
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
        else if (_primaryTarget.LastKnownPosition != Vector3.zero &&
                 (CurrentState != AlertState.Unaware || _primaryTarget.SuspicionLevel > 0f))
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
