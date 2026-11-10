using Project.AI.Blackboard;

namespace Project.AI.Decision.States
{
public class AIIdleState : IAIState
{
    public void OnEnter(AIBlackboard blackboard)
    {
        blackboard.SetCombatIntent(false);
        blackboard.ClearMoveDestination();
    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
    }

    public void OnExit(AIBlackboard blackboard)
    {
        
    }
}
}