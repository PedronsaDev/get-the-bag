using Project.AI.Blackboard;

namespace Project.AI.Decision.States
{
public class AIIdleState : IAIState
{
    public void OnEnter(AIBlackboard blackboard)
    {
        blackboard.SetCombatIntent(false);
    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        // do nothing for now
    }

    public void OnExit(AIBlackboard blackboard)
    {
        
    }
}
}