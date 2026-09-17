using System;
using Project.Core.Persistence;
using Project.Persistence;
using UnityEngine;

namespace Project.Player.Data
{

/// <summary>
/// Singleton que cuida do perfil do jogador.
/// Eh atualizado em momentos de save/load e outros eventos de persistencia.
/// </summary>
public class PlayerProfileManager : MonoBehaviour, IDataPersistence
{
    [Header("References")]
    [SerializeField] private ProgressionSettings _progressionSettings;
    
    public static PlayerProfileManager Instance { get; private set; }
    
    public string Name { get; private set; } = "PLAYER_NAME";
    public int Level { get; private set; } = 1;
    public int Experience { get; private set; } = 0;
    public int Scrip { get; private set; } = 0;

    public event Action<int, int> OnScripChanged;         // (newValue, delta)
    public event Action<int, int> OnScripChangedWithOld;  // (oldValue, newValue)
    public event Action<int> OnLevelChanged;              // (newLevel)
    public event Action<int, int> OnXpGained;             // (newXp, delta)

    private void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        if (DataPersistenceManager.Instance != null)
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
        Level = data.Player.Level;
        Experience = data.Player.Experience;
        Scrip = data.Player.Scrip;
    }
    
    public void SaveData(GameData data)
    {
        data.Player.Name = Name;
        data.Player.Level = Level;
        data.Player.Experience = Experience;
        data.Player.Scrip = Scrip;
    }
    
    public bool TrySpendScrip(int amount)
    {
        if (amount <= 0 || Scrip < amount) return false;
        
        var old = Scrip;
        Scrip -= amount;
            
        OnScripChangedWithOld?.Invoke(old, Scrip);
        OnScripChanged?.Invoke(Scrip, -amount);
            
        return true;
    }
    
    public void AddScrip(int amount)
    {
        if (amount <= 0) return;
        
        var old = Scrip;
        Scrip += amount;
        
        OnScripChangedWithOld?.Invoke(old, Scrip);
        OnScripChanged?.Invoke(Scrip, amount);
    }
    
    public void AddExperience(int amount)
    {
        if (amount <= 0) return;
        
        Experience += amount;

        var xpRequirement = _progressionSettings.GetXPRequirementForLevel(Level + 1);
        
        while (Experience >= xpRequirement)
        {
            Level++;
            Experience -= xpRequirement;
            OnLevelChanged?.Invoke(Level);
            xpRequirement = _progressionSettings.GetXPRequirementForLevel(Level + 1);
        }
        
        OnXpGained?.Invoke(Experience, amount);
    }
}
}