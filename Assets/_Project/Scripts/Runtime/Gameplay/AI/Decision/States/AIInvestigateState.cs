using Project.AI.Blackboard;

namespace Project.AI.Decision.States
{
public class AIInvestigateState : IAIState
{
    public void OnEnter(AIBlackboard blackboard)
    {
        blackboard.SetMoveDestination(blackboard.TargetPosition, blackboard.ArchetypeConfig.MovementConfig.InvestigateSpeed);
    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        if (!blackboard.IsAtDestination)
            return;
        
        blackboard.ClearMoveDestination();
    }

    public void OnExit(AIBlackboard blackboard)
    {
        
    }
}
}