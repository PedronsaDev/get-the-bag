using Project.AI.Blackboard;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Decision.States
{
public class AICombatState : IAIState
{
    public void OnEnter(AIBlackboard blackboard)
    {
        blackboard.SetCombatIntent(true);
    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        var config = blackboard.MovementConfig;
        
        if (!blackboard.HasLineOfSight)
        {
            blackboard.SetCombatIntent(false);
            if (blackboard.TargetPosition != Vector3.zero)
            {
                blackboard.SetMoveDestination(blackboard.TargetPosition, config.CombatRunSpeed);
            }
            
            return;
        }
    

        if (blackboard.DistanceToTarget > config.MaxCombatDistance)
        {
            blackboard.SetMoveDestination(blackboard.TargetPosition, config.CombatRunSpeed);
        }
        else if (blackboard.DistanceToTarget >= config.MinCombatDistance && blackboard.DistanceToTarget <= config.MaxCombatDistance)
        {
            blackboard.ClearMoveDestination();
            blackboard.SetCombatIntent(true);
        }
        else
        {
            Vector3 dirAwayFromTarget = blackboard.transform.position - blackboard.TargetPosition;
            
            dirAwayFromTarget.y = 0f;
            if (dirAwayFromTarget.sqrMagnitude > 0.001f)
            {
                dirAwayFromTarget.Normalize();
            }
            else
            {
                dirAwayFromTarget = -blackboard.transform.forward;
            }
            Vector3 desiredRetreatPoint = blackboard.transform.position + dirAwayFromTarget * 4.0f;

            if (NavMesh.SamplePosition(desiredRetreatPoint, out var hit, 2.0f, NavMesh.AllAreas))
                blackboard.SetMoveDestination(hit.position, config.CombatRunSpeed);
        }
    }

    public void OnExit(AIBlackboard blackboard)
    {
        blackboard.SetCombatIntent(false);
    }
}
}