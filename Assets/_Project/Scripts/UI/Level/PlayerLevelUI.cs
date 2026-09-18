using Project.Core;
using TMPro;
using UnityEngine;

namespace Project.UI.Level
{
public class PlayerLevelUI : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private ProgressionSettings _settings;
    [SerializeField] private PlayerProfileEventChannel _events;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private ProgressBarUI _experienceBar;
    
    private void OnEnable()
    {
        if (_events)
        {
            _events.OnExperienceChanged += HandleExperienceChanged;
            _events.OnLevelChanged += HandleLevelChanged;
        }
    }

    private void OnDisable()
    {
        if (_events)
        {
            _events.OnExperienceChanged -= HandleExperienceChanged;
            _events.OnLevelChanged -= HandleLevelChanged;
        }
    }

    private void HandleExperienceChanged(int newExperience, int delta, int level)
    {
        if (_levelText) 
            _levelText.text = $"Lv. {level}";
        
        if (level >= _settings.MaxLevel)
        {
            _experienceBar.SetFillAmount(1f);
            return;
        }
        
        int currentLevelFloor = _settings.GetXPRequirementForLevel(level);
        int nextLevelFloor = _settings.GetXPRequirementForLevel(level + 1);

        int xpInLevel = newExperience - currentLevelFloor;
        int xpRequiredForLevel = nextLevelFloor - currentLevelFloor;

        float progress = Mathf.Clamp01((float)xpInLevel / xpRequiredForLevel);
        _experienceBar.SetFillAmount(progress);
    }
    
    private void HandleLevelChanged(int newLevel)
    {
        
    }
}
}