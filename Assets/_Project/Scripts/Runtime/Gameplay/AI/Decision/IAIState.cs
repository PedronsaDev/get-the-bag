using Project.AI.Blackboard;

namespace Project.AI.Decision
{
public interface IAIState
{
    void OnEnter(AIBlackboard blackboard);
    void OnUpdate(AIBlackboard blackboard, float deltaTime);
    void OnExit(AIBlackboard blackboard);
}
}
