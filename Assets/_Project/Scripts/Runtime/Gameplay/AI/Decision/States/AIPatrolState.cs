using Project.AI.Blackboard;
using Project.AI.Locomotion;
using Project.AI.Patrol;
using UnityEngine;

namespace Project.AI.Decision.States
{
public class AIPatrolState : IAIState
{
    private enum Phase : byte
    {
        Walking,
        Waiting
    }

    private Phase _phase;
    private AIPatrolRoute _route;
    private int _index;
    private int _direction = 1;
    private int _consecutiveFailures;
    private float _waitTimer;
    private float _waitElapsed;

    private bool HasRoute => _route && _route.PointCount > 0;

    public void OnEnter(AIBlackboard blackboard)
    {
        _route = blackboard.PatrolRoute;
        _direction = 1;
        _consecutiveFailures = 0;
        if (!HasRoute)
            return;

        _index = _route.NearestPointIndex(blackboard.AgentPosition);
        WalkToCurrentPoint(blackboard);
    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        if (!HasRoute)
            return;

        if (_phase == Phase.Walking)
            UpdateWalking(blackboard);
        else
            UpdateWaiting(blackboard, deltaTime);
    }

    public void OnExit(AIBlackboard blackboard)
    {
        blackboard.ClearMoveDestination();
    }

    private void UpdateWalking(AIBlackboard blackboard)
    {
        if (blackboard.MoveStatus == MovementStatus.Failed)
        {
            _consecutiveFailures++;
            blackboard.ClearMoveDestination();
            if (_consecutiveFailures >= _route.PointCount)
            {
                _consecutiveFailures = 0;
                BeginWaiting();
                return;
            }
            AdvanceAndWalk(blackboard);
        }
        else if (blackboard.IsAtDestination)
        {
            _consecutiveFailures = 0;
            blackboard.ClearMoveDestination();
            BeginWaiting();
        }
    }

    private void UpdateWaiting(AIBlackboard blackboard, float deltaTime)
    {
        _waitTimer -= deltaTime;
        _waitElapsed += deltaTime;
        LookAround(blackboard, deltaTime);

        if (_waitTimer <= 0f)
            AdvanceAndWalk(blackboard);
    }

    private void BeginWaiting()
    {
        _phase = Phase.Waiting;
        _waitTimer = _route.WaitSeconds;
        _waitElapsed = 0f;
    }

    private void AdvanceAndWalk(AIBlackboard blackboard)
    {
        _index = AIPatrolRoute.NextIndex(_index, _route.PointCount, _route.Loop, ref _direction);
        WalkToCurrentPoint(blackboard);
    }

    private void WalkToCurrentPoint(AIBlackboard blackboard)
    {
        _phase = Phase.Walking;
        blackboard.SetMoveDestination(_route.GetPoint(_index).position,
            blackboard.ArchetypeConfig.MovementConfig.PatrolSpeed);
    }

    private void LookAround(AIBlackboard blackboard, float deltaTime)
    {
        float baseYaw = _route.GetPoint(_index).eulerAngles.y;
        float sweep = _route.LookSweepDegrees *
                      Mathf.Sin(2f * Mathf.PI * _waitElapsed / _route.LookSweepPeriod);
        var target = Quaternion.Euler(0f, baseYaw + sweep, 0f);
        var body = blackboard.transform;
        body.rotation = Quaternion.RotateTowards(body.rotation, target, _route.TurnSpeedDegrees * deltaTime);
    }
}
}
