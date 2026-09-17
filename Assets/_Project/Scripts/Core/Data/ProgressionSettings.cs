using UnityEngine;

[CreateAssetMenu(fileName = "ProgressionSettings", menuName = "Progression/XP Settings", order = 1)]
public class ProgressionSettings : ScriptableObject
{
    public int MaxLevel = 50;
    public int MaxLevelXPRequirement = 1000000;
    
    public AnimationCurve XPCurve;
    
    public int GetXPRequirementForLevel(int targetLevel)
    {
        if (targetLevel <= 1) return 0;
        if (targetLevel > MaxLevel) return -1;

        float levelProgress = (float)targetLevel / MaxLevel;
        float curveMultiplier = XPCurve.Evaluate(levelProgress);
        return Mathf.RoundToInt(MaxLevelXPRequirement * curveMultiplier);
    }
}