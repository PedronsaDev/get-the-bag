using Project.Core.Events;
using Project.Persistence;
using PurrNet;
using UnityEngine;

namespace Project.Player.Data
{

/// <summary>
/// Controller que cuida da progressao do jogador durante a run.
/// Mantem o estado de XP, Scrip e Level do jogador, e replica para os outros players na rede.
/// </summary>
public class PlayerProgressionController : NetworkBehaviour
{
    [Header("Replicated Badges")]
    public SyncVar<string> Name = new("PLAYER_NAME");
    public SyncVar<int> Level = new(1);
    
    [Header("In-Run Currency")]
    public SyncVar<int> InRunScrip = new(0);

    private int _inRunXp;
    public int InRunXp => _inRunXp;

    [Header("References")]
    [SerializeField] private ProgressionSettings _progressionSettings;
    [SerializeField] private RaidEventChannel _raidEvents;

    protected override void OnSpawned()
    {
        if (!isOwner)
            return;
        
        var profile = PlayerProfileManager.Instance;
        
        if (profile)
        {
            Name.value = profile.Name;
            Level.value = profile.Level;
        }
    }

    private void OnEnable()
    {
        if (_raidEvents)
        {
            _raidEvents.OnRaidCompleted += HandleRaidCompleted;
            _raidEvents.OnRaidFailed += HandleRaidFailed;
        }
    }

    private void OnDisable()
    {
        if (_raidEvents)
        {
            _raidEvents.OnRaidCompleted -= HandleRaidCompleted;
            _raidEvents.OnRaidFailed -= HandleRaidFailed;
        }
    }

    public void AddInRunScrip(int amount)
    {
        if (!isOwner || amount <= 0) return;
        InRunScrip.value += amount;
    }

    public void AddInRunXp(int amount)
    {
        if (!isOwner || amount <= 0) return;
        _inRunXp += amount;
    }

    private void HandleRaidCompleted()
    {
        if (!isOwner) return;
        
        PushRaidProgress();
        
        InRunScrip.value = 0;
        _inRunXp = 0;
    }

    private void HandleRaidFailed()
    {
        if (!isOwner) return;
        
        InRunScrip.value = 0;
        _inRunXp /= 2;
        
        PushRaidProgress();

        _inRunXp = 0;
    }
    
    private void PushRaidProgress()
    {
        var profile = PlayerProfileManager.Instance;
        if (profile)
        {
            profile.AddScrip(InRunScrip.value);
            profile.AddExperience(_inRunXp);
            DataPersistenceManager.Instance?.SaveGame();
        }
    }
}
}