using System;
using Project.Core;
using Project.Core.Persistence;
using Project.Logging;
using Project.Persistence;
using UnityEngine;

namespace Project.Player.Profile
{
public class PlayerProfileManager : MonoBehaviour, IDataPersistence
{
    public static PlayerProfileManager Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private PlayerProfileEventChannel _events;
    [SerializeField] private ProgressionSettings _settings;

    public string Name {get; private set;} = "PLAYER_NAME";
    public int CurrentLevel {get; private set;} = 1;
    public int TotalExperience {get; private set;} = 0;
    public int Scrip {get; private set;} = 0;

    private void Awake()
    {
        if (Instance)
        {
            Destroy(this.gameObject);
            AppLog.LogWarning(LogChannel.Player, "[Player Profile Manager] Another PlayerProfileManager instance already exists. Destroying duplicate.");
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    private void OnEnable()
    {
        if (DataPersistenceManager.Instance)
        {
            DataPersistenceManager.Instance.Register(this);
        }
    }
    
    private void OnDisable()
    {
        if (DataPersistenceManager.HasInstance)
        {
            DataPersistenceManager.Instance.Unregister(this);
        }
    }
    public void LoadData(GameData data)
    {
        Name = data.Player.Name;
        TotalExperience = data.Player.TotalExperience;
        Scrip = data.Player.Scrip;
        
        CurrentLevel = _settings.GetLevelForXP(TotalExperience);
        
        _events.RaiseNameChanged(Name);
        _events.RaiseExperienceChanged(TotalExperience, 0, CurrentLevel);
        _events.RaiseLevelChanged(CurrentLevel);
        _events.RaiseScripChanged(Scrip, 0);
    }

    public void SaveData(GameData data)
    {
        data.Player.Name = Name;
        data.Player.TotalExperience = TotalExperience;
        data.Player.Scrip = Scrip;
    }
    
    public void ChangePlayerName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            AppLog.LogWarning(LogChannel.Player, "[Player Profile Manager] Attempted to set player name to an empty or whitespace string.");
            return;
        }

        Name = newName;
        _events.RaiseNameChanged(Name);
    }
    
    public void AddExperience(int amount)
    {
        if (amount <= 0 || CurrentLevel >= _settings.MaxLevel)
        {
            CurrentLevel = _settings.MaxLevel;
            return;
        }

        TotalExperience = Mathf.Clamp(TotalExperience + amount, 0, _settings.MaxLevelXPRequirement);
        int newLevel = _settings.GetLevelForXP(TotalExperience);

        if (newLevel > CurrentLevel)
        {
            int oldLevel = CurrentLevel;
            CurrentLevel = newLevel;
            for (int lvl = oldLevel; lvl <= newLevel; lvl++)
            {
                _events.RaiseLevelChanged(lvl);
            }
            
        }
        _events.RaiseExperienceChanged(TotalExperience,amount,CurrentLevel);
    }

    public void AddScrip(int amount)
    {
        if (amount <= 0)
            return;
        
        var targetAmount = Mathf.Min(Scrip + amount, _settings.MaxScrip);
        var delta = targetAmount - Scrip;
        Scrip = targetAmount;
        _events.RaiseScripChanged(Scrip, delta);
    }
    
    // Debug methods
    public void SetPlayerLevel(int level)
    {
        if (level < 1 || level > _settings.MaxLevel)
        {
            AppLog.LogWarning(LogChannel.Player, $"[Player Profile Manager] Attempted to set player level to {level}, which is outside the valid range of 1 to {_settings.MaxLevel}.");
            return;
        }

        CurrentLevel = level;
        TotalExperience = _settings.GetXPRequirementForLevel(level);
        _events.RaiseExperienceChanged(TotalExperience, 0, level);
        _events.RaiseLevelChanged(level);
    }
}
}