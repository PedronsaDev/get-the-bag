using System;
using Project.AI.Configuration;
using Project.AI.Perception;

namespace Project.AI.Decision
{
public static class AIStateSelection
{
    public static AIStateKind Select(AIRole role, AlertState alert, bool hasPatrolRoute = false)
    {
        switch (alert)
        {
            case AlertState.Unaware:
                return role.Patrols() && hasPatrolRoute ? AIStateKind.Patrol : AIStateKind.Idle;
            case AlertState.Suspicious:
                return role.Investigates() ? AIStateKind.Investigate : AIStateKind.Idle;
            case AlertState.Searching:
                // Fleeing continues when the danger is out of sight.
                if (role.Flees())
                    return AIStateKind.Flee;
                return role.Investigates() ? AIStateKind.Investigate : AIStateKind.Idle;
            case AlertState.Alerted:
                if (role.Fights())
                    return AIStateKind.Combat;
                return role.Flees() ? AIStateKind.Flee : AIStateKind.Idle;
            default:
                throw new ArgumentOutOfRangeException(nameof(alert), alert, null);
        }
    }
}
}
