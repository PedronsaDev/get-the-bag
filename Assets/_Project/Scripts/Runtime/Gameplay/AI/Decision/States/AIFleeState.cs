using Project.AI.Alarm;
using Project.AI.Blackboard;
using Project.AI.Configuration;
using Project.AI.Locomotion;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Decision.States
{
public class AIFleeState : IAIState
{
    private enum Phase : byte
    {
        Running,
        Calling,
        Cowering
    }

    private const float AwaySampleRadius = 5f;

    private Phase _phase = Phase.Cowering;
    private AIPhonePoint _phone;
    private EntityId _id;
    private float _callTimer;

    public bool IsBusy => _phase != Phase.Cowering;

    public void OnEnter(AIBlackboard blackboard)
    {
        _id = blackboard.gameObject.GetEntityId();
        _phone = null;

        var role = blackboard.ArchetypeConfig.Role;
        if (role.CallsPolice() && AIPhonePoint.TryReserveNearest(blackboard.AgentPosition, _id, out _phone))
        {
            RunTo(blackboard, _phone.Position);
            return;
        }

        if (TryFindEscapePoint(blackboard, out Vector3 escape))
            RunTo(blackboard, escape);
        else
            Cower(blackboard);
    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        switch (_phase)
        {
            case Phase.Running:
                UpdateRunning(blackboard);
                break;
            case Phase.Calling:
                UpdateCalling(blackboard, deltaTime);
                break;
        }
    }

    public void OnExit(AIBlackboard blackboard)
    {
        ReleasePhone();
        blackboard.ClearMoveDestination();
        blackboard.SetLieDown(false);
        _phase = Phase.Cowering;
    }

    private void UpdateRunning(AIBlackboard blackboard)
    {
        if (blackboard.MoveStatus == MovementStatus.Failed)
        {
            ReleasePhone();
            blackboard.ClearMoveDestination();
            if (TryFindEscapePoint(blackboard, out Vector3 escape))
                RunTo(blackboard, escape);
            else
                Cower(blackboard);
        }
        else if (blackboard.IsAtDestination)
        {
            blackboard.ClearMoveDestination();
            BeginCallOrCower(blackboard);
        }
    }

    private void UpdateCalling(AIBlackboard blackboard, float deltaTime)
    {
        _callTimer -= deltaTime;
        if (_callTimer > 0f)
            return;

        if (AlarmDirector.Instance)
            AlarmDirector.Instance.Raise(AlarmReason.CivilianCall, blackboard.AgentPosition);
        ReleasePhone();
        Cower(blackboard);
    }

    private void BeginCallOrCower(AIBlackboard blackboard)
    {
        if (_phone)
        {
            _phase = Phase.Calling;
            _callTimer = _phone.CallSeconds;
        }
        else
        {
            Cower(blackboard);
        }
    }

    private void Cower(AIBlackboard blackboard)
    {
        _phase = Phase.Cowering;
        blackboard.SetLieDown(true);
    }

    private void RunTo(AIBlackboard blackboard, Vector3 destination)
    {
        _phase = Phase.Running;
        blackboard.SetMoveDestination(destination, blackboard.ArchetypeConfig.MovementConfig.FleeSpeed);
    }

    private static bool TryFindEscapePoint(AIBlackboard blackboard, out Vector3 escape)
    {
        Vector3 away = blackboard.AgentPosition - blackboard.TargetPosition;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : -blackboard.transform.forward;

        Vector3 wanted = blackboard.AgentPosition + away * blackboard.ArchetypeConfig.MovementConfig.FleeDistance;
        if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, AwaySampleRadius, NavMesh.AllAreas))
        {
            escape = hit.position;
            return true;
        }
        escape = default;
        return false;
    }

    private void ReleasePhone()
    {
        if (_phone)
            _phone.Release(_id);
        _phone = null;
    }
}
}
