using System;
using UnityEngine;

namespace Project.Core
{
[CreateAssetMenu(fileName = "PlayerProfileEventChannel", menuName = "Events/PlayerProfileEventChannel")]
public class PlayerProfileEventChannel : ScriptableObject
{
    public event Action<string> OnNameChanged; // newName
    public event Action<int> OnLevelChanged; // newLevel
    public event Action<int, int> OnExperienceChanged; // totalExperience, delta
    public event Action<int, int> OnScripChanged; // scrip, delta

    public void RaiseNameChanged(string newName)
    {
        OnNameChanged?.Invoke(newName);
    }
    
    public void RaiseLevelChanged(int newLevel)
    {
        OnLevelChanged?.Invoke(newLevel);
    }

    public void RaiseExperienceChanged(int totalExperience, int delta)
    {
        OnExperienceChanged?.Invoke(totalExperience, delta);
    }

    public void RaiseScripChanged(int scrip, int delta)
    {
        OnScripChanged?.Invoke(scrip, delta);
    }
}
}