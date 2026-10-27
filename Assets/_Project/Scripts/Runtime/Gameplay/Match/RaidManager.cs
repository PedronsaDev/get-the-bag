using System;
using System.Collections.Generic;
using Project.Core.Events;
using Project.Core.Player;
using Project.Logging;
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
    [SerializeField, Min(30f)] private float _raidDurationSeconds = 480f;
    private readonly HashSet<PlayerID> _extractedPlayers = new();
    public IReadOnlyCollection<PlayerID> ExtractedPlayers => _extractedPlayers;
    public event Action<RaidState> OnResolvedServer;
    public bool IsActive => CurrentState.value is RaidState.InRaid or RaidState.ExtractionAvailable or RaidState.ExtractionHoldout;
    
    [Header("Networked State")]
    public SyncVar<RaidState> CurrentState = new();
    public SyncVar<float> TimeRemaining = new(1200f);
    public SyncVar<int> StandingPlayerCount = new();
    public SyncVar<int> DownedPlayerCount = new();
    public SyncVar<int> DeadPlayerCount = new();

    private bool IsServerAuthority =>
        isServer;

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

        if (standing == 0 && IsActive)
        {
            TriggerTeamWipe();
        }
    }

    private void TriggerTeamWipe()
    {
        AppLog.Log(LogChannel.Match, "All players eliminated or downed. Raid failed.", this);
        ChangeState(RaidState.Failed);
    }

    private void TriggerTimesUp()
    {
        AppLog.Log(LogChannel.Match, "Raid timer expired.", this);
        ChangeState(RaidState.Failed);
    }

    public void ChangeState(RaidState newState)
    {
        if (!IsServerAuthority)
            return;
        if (CurrentState.value is RaidState.Completed or RaidState.Failed) return;

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
                TimeRemaining.value = _raidDurationSeconds;
                break;
            case RaidState.InRaid:
                break;
            case RaidState.ExtractionAvailable:
                break;
            case RaidState.ExtractionHoldout:
                break;
            case RaidState.Completed:
                OnResolvedServer?.Invoke(newState);
                break;
            case RaidState.Failed:
                OnResolvedServer?.Invoke(newState);
                break;
        }
    }

    public void BeginRaidServer()
    {
        if (!isServer || CurrentState.value != RaidState.WaitingAtPlatform) return;
        _extractedPlayers.Clear();
        TimeRemaining.value = _raidDurationSeconds;
        ChangeState(RaidState.InRaid);
    }

    public bool ResolveExtractionServer(IReadOnlyList<PlayerID> extracted)
    {
        if (!isServer || !IsActive || TimeRemaining.value <= 0f) return false;
        _extractedPlayers.Clear();
        for (int i = 0; i < extracted.Count; i++) _extractedPlayers.Add(extracted[i]);
        ChangeState(_extractedPlayers.Count > 0 ? RaidState.Completed : RaidState.Failed);
        return true;
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
