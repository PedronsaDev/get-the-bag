using System;
using UnityEngine;

namespace Project.AI.Combat
{
[Serializable]
public class AIMovementConfig
{
    [Header("Movement Speeds")]
    public float PatrolSpeed = 2.0f;
    public float InvestigateSpeed = 3.5f;
    public float CombatRunSpeed = 5.5f;
    
    [Header("Combat Distances")]
    public float MinCombatDistance = 6.0f;
    public float IdealCombatDistance = 12.0f;
    public float MaxCombatDistance = 20.0f;
}
}