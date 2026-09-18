using UnityEngine;

[CreateAssetMenu(fileName = "ProgressionSettings", menuName = "Settings/XP Settings", order = 1)]
public class ProgressionSettings : ScriptableObject
{
    [Header("Leveling Settings")]
    public int MaxLevel = 50;
    public int MaxLevelXPRequirement = 1000000;
    public AnimationCurve XPCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Profile Settings")]
    public int StartingScrip = 1000;
    public int MaxScrip = 999999;

    /// <summary>
    /// Returns the total cumulative XP required to reach targetLevel.
    /// </summary>
    public int GetXPRequirementForLevel(int targetLevel)
    {
        if (targetLevel <= 1) return 0;
        if (targetLevel >= MaxLevel) return MaxLevelXPRequirement;

        // Correctly maps Level 1 -> 0.0 and MaxLevel -> 1.0
        float levelProgress = (float)(targetLevel - 1) / (MaxLevel - 1);
        float curveMultiplier = Mathf.Clamp01(XPCurve.Evaluate(levelProgress));
        
        return Mathf.RoundToInt(MaxLevelXPRequirement * curveMultiplier);
    }

    /// <summary>
    /// Converts a total cumulative XP value into the corresponding level.
    /// </summary>
    public int GetLevelForXP(int totalXP)
    {
        if (totalXP <= 0) return 1;
        if (totalXP >= MaxLevelXPRequirement) return MaxLevel;

        for (int level = MaxLevel; level >= 1; level--)
        {
            if (totalXP >= GetXPRequirementForLevel(level))
            {
                return level;
            }
        }

        return 1;
    }
}