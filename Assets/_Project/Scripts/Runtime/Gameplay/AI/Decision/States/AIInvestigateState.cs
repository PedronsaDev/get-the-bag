using Project.AI.Blackboard;
using Project.AI.Locomotion;
using UnityEngine;

namespace Project.AI.Decision.States
{
public class AIInvestigateState : IAIState
{
    private Vector3 _goal;
    private bool _finished;

    public void OnEnter(AIBlackboard blackboard)
    {
        _goal = Vector3.zero;
        _finished = true;
        BeginInvestigation(blackboard);
    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        if (blackboard.TargetPosition != _goal)
            BeginInvestigation(blackboard);

        if (_finished)
            return;

        if (blackboard.MoveStatus == MovementStatus.Failed || blackboard.IsAtDestination)
        {
            blackboard.ClearMoveDestination();
            _finished = true;
        }
    }

    public void OnExit(AIBlackboard blackboard)
    {
    }

    private void BeginInvestigation(AIBlackboard blackboard)
    {
        _goal = blackboard.TargetPosition;
        // Zero means "no remembered position"; there is nothing to walk to.
        if (_goal == Vector3.zero)
            return;

        _finished = false;
        blackboard.SetMoveDestination(_goal, blackboard.ArchetypeConfig.MovementConfig.InvestigateSpeed);
    }
}
}
