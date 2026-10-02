using System;
using Project.Core;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Player.Profile
{
public class RuntimePlayerProfile : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerProfileEventChannel _events;

    [Header("Networked State")]
    [SerializeField] private SyncVar<string> _name = new("PLAYER_NAME", ownerAuth: true);
    [SerializeField] private SyncVar<int> _currentLevel = new(1, ownerAuth: true);
    [SerializeField] private SyncVar<int> _totalExperience = new(0, ownerAuth: true);

    public string Name => _name.value;
    public int CurrentLevel => _currentLevel.value;
    public int TotalExperience => _totalExperience.value;
    
    public event Action<string> OnNameChanged;
    public event Action<int> OnLevelChanged;
    public event Action<int, int> OnExperienceChanged;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        _name.onChanged += HandleReplicatedNameChanged;
        _currentLevel.onChanged += HandleReplicatedLevelChanged;
        _totalExperience.onChanged += HandleReplicatedExperienceChanged;

        if (isOwner)
        {
            if (_events)
            {
                _events.OnExperienceChanged += HandleLocalExperienceChanged;
                _events.OnLevelChanged += HandleLocalLevelChanged;
                _events.OnNameChanged += HandleLocalNameChanged;
            }

            var instance = PlayerProfileManager.Instance;
            if (instance)
            {
                _name.value = instance.Name;
                _totalExperience.value = instance.TotalExperience;
                _currentLevel.value = instance.CurrentLevel;
            }
        }
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();

        _name.onChanged -= HandleReplicatedNameChanged;
        _currentLevel.onChanged -= HandleReplicatedLevelChanged;
        _totalExperience.onChanged -= HandleReplicatedExperienceChanged;

        if (isOwner && _events)
        {
            _events.OnExperienceChanged -= HandleLocalExperienceChanged;
            _events.OnLevelChanged -= HandleLocalLevelChanged;
            _events.OnNameChanged -= HandleLocalNameChanged;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _name.onChanged -= HandleReplicatedNameChanged;
        _currentLevel.onChanged -= HandleReplicatedLevelChanged;
        _totalExperience.onChanged -= HandleReplicatedExperienceChanged;

        if (isOwner && _events)
        {
            _events.OnExperienceChanged -= HandleLocalExperienceChanged;
            _events.OnLevelChanged -= HandleLocalLevelChanged;
            _events.OnNameChanged -= HandleLocalNameChanged;
        }
    }

    private void HandleLocalNameChanged(string newName)
    {
        if (isOwner)
        {
            AppLog.Log(LogChannel.Player, $"[RuntimePlayerProfile] Setting replicated name: {newName}", this);
            _name.value = newName;
        }
    }

    private void HandleLocalLevelChanged(int newLevel)
    {
        if (isOwner)
        {
            _currentLevel.value = newLevel;
        }
    }

    private void HandleLocalExperienceChanged(int newExperience, int delta, int level)
    {
        if (isOwner)
        {
            _totalExperience.value = newExperience;
        }
    }

    private void HandleReplicatedNameChanged(string newName)
    {
        OnNameChanged?.Invoke(newName);
    }

    private void HandleReplicatedLevelChanged(int newLevel)
    {
        OnLevelChanged?.Invoke(newLevel);
    }

    private void HandleReplicatedExperienceChanged(int newExperience)
    {
        OnExperienceChanged?.Invoke(newExperience, 0);
    }
}
}