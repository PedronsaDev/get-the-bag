using Project.Core.Events;
using Project.Core.Player;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{

public enum RaidState
{
    WaitingAtPlatform,
    InRaid,
    ExtractionAvailable,
    ExtractionHoldout,
    Completed,
    Failed
}

public class RaidManager : NetworkBehaviour
{
    public static RaidManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RaidEventChannel _raidEvents;
    
    [Header("Networked State")]
    public SyncVar<RaidState> CurrentState = new();
    public SyncVar<float> TimeRemaining = new(1200f);
    public SyncVar<int> StandingPlayerCount = new();
    public SyncVar<int> DownedPlayerCount = new();
    public SyncVar<int> DeadPlayerCount = new();

    private bool IsServerAuthority =>
        isServer || (NetworkManager.main && NetworkManager.main.isServer) || (!NetworkManager.main || NetworkManager.main.isOffline);

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }

        if (!_raidEvents)
        {
            _raidEvents = Resources.Load<RaidEventChannel>("Event Channels/RaidEventChannel");
        }

        CurrentState.onChanged += HandleNetworkStateChanged;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CurrentState.onChanged -= HandleNetworkStateChanged;
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (_raidEvents)
            _raidEvents.OnInfilCompleted += HandleInfilCompleted;
    }

    private void OnDisable()
    {
        if (_raidEvents)
            _raidEvents.OnInfilCompleted -= HandleInfilCompleted;
    }

    private void Start()
    {
        if (_raidEvents)
            _raidEvents.RaiseRaidManagerUpdated(this);

        if (IsServerAuthority)
        {
            ChangeState(RaidState.WaitingAtPlatform);
        }
    }

    private void Update()
    {
        if (!IsServerAuthority)
        {
            return;
        }

        if (CurrentState.value is RaidState.InRaid or RaidState.ExtractionAvailable or RaidState.ExtractionHoldout)
        {
            TimeRemaining.value -= Time.deltaTime;
            EvaluatePlayerStatuses();
            if (TimeRemaining.value <= 0f)
            {
                TriggerTimesUp();
            }
        }
    }
    
    private void HandleInfilCompleted()
    {
        if (!IsServerAuthority)
            return;

        ChangeState(RaidState.InRaid);
    }

    private void EvaluatePlayerStatuses()
    {
        var activePlayers = PlayerIdentifier.ActivePlayers;
        int standing = 0;
        int downed = 0;
        int dead = 0;
        
        for (int i = 0; i < activePlayers.Count; i++)
        {
            var p = activePlayers[i];
            if (!p) continue;
            if (p.IsDead)
                dead++;
            else if (p.IsDowned)
                downed++;
            else
                standing++;
        }
        
        StandingPlayerCount.value = standing;
        DownedPlayerCount.value = downed;
        DeadPlayerCount.value = dead;

        if (activePlayers.Count > 0 && standing == 0 && CurrentState.value is RaidState.InRaid or RaidState.ExtractionAvailable or RaidState.ExtractionHoldout)
        {
            TriggerTeamWipe();
        }
    }

    private void TriggerTeamWipe()
    {
        Debug.Log("[RaidManager] All players eliminated or downed! Raid failed.");
        ChangeState(RaidState.Failed);
    }

    private void TriggerTimesUp()
    {
        Debug.Log("[RaidManager] Raid timer expired!");
        ChangeState(RaidState.Failed);
    }

    public void ChangeState(RaidState newState)
    {
        if (!IsServerAuthority)
            return;

        if (CurrentState.value != newState)
        {
            CurrentState.value = newState;
            ExecuteServerStateEffects(newState);
        }
    }

    private void ExecuteServerStateEffects(RaidState newState)
    {
        switch (newState)
        {
            case RaidState.WaitingAtPlatform:
                TimeRemaining.value = 1200f;
                break;
            case RaidState.InRaid:
                break;
            case RaidState.ExtractionAvailable:
                break;
            case RaidState.ExtractionHoldout:
                break;
            case RaidState.Completed:
                break;
            case RaidState.Failed:
                break;
        }
    }

    private void HandleNetworkStateChanged(RaidState newState)
    {
        if (!_raidEvents)
            return;

        switch (newState)
        {
            case RaidState.Completed:
                _raidEvents.RaiseRaidCompleted();
                break;
            case RaidState.Failed:
                _raidEvents.RaiseRaidFailed();
                break;
        }
        
        _raidEvents.RaiseStateChanged(newState);
    }
}
}