using UnityEngine;

namespace Project.AI.Combat
{
[CreateAssetMenu(fileName = "AIMovementConfig", menuName = "AI/AIMovementConfig", order = 1)]
public class AIMovementConfig : ScriptableObject
{
    public float PatrolSpeed = 2.0f;
    public float InvestigateSpeed = 3.5f;
    public float CombatRunSpeed = 5.5f;
    public float MinCombatDistance = 6.0f;
    public float IdealCombatDistance = 12.0f;
    public float MaxCombatDistance = 20.0f;
}
}