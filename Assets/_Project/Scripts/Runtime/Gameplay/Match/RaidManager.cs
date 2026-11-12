using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Player;
using Project.Core.Raid;
using Project.Logging;
using Project.Loot;
using Project.Definitions.Loot;
using Project.Networking.Scenes;
using PurrNet;
using UnityEngine;

namespace Project.Match
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

    [Header("Return")]
    [SerializeField] private string _hubSceneName = "Scene_Hub";
    [Tooltip("Seconds after the raid ends before everyone is sent back to the hub. 0 = only when the host calls ReturnToHub().")]
    [SerializeField, Min(0f)] private float _autoReturnSeconds = 15f;

    [Header("Networked State")]
    public SyncVar<RaidState> CurrentState = new();
    public SyncVar<float> TimeRemaining = new(1200f);
    public SyncVar<int> StandingPlayerCount = new();
    public SyncVar<int> DownedPlayerCount = new();
    public SyncVar<int> DeadPlayerCount = new();
    public SyncVar<RaidResults> Results = new();
    public SyncVar<float> ReturnCountdown = new();

    private static int _runCounter;

    private LevelReadyGate _gate;
    private bool _resolved;
    private bool _returning;
    private float _returnTimer;
    private int _lastAnnouncedRun;

    public bool IsRaidActive =>
        CurrentState.value is RaidState.InRaid or RaidState.ExtractionAvailable or RaidState.ExtractionHoldout;
    public bool IsResolved => CurrentState.value is RaidState.Completed or RaidState.Failed;

    private bool IsServerAuthority =>
        isServer || (NetworkManager.main && NetworkManager.main.isServer) || (!NetworkManager.main || NetworkManager.main.isOffline);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _runCounter = 0;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }

        if (!_raidEvents)
        {
            _raidEvents = Resources.Load<RaidEventChannel>(RaidEventChannel.ResourcePath);
        }

        CurrentState.onChanged += HandleNetworkStateChanged;
        Results.onChanged += HandleResultsChanged;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CurrentState.onChanged -= HandleNetworkStateChanged;
        Results.onChanged -= HandleResultsChanged;
        if (_gate) _gate.OnPhaseChanged -= HandleGatePhaseChanged;
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
            SetState(RaidState.WaitingAtPlatform);

            _gate = LevelReadyGate.Instance;
            if (_gate)
            {
                _gate.OnPhaseChanged += HandleGatePhaseChanged;
                if (_gate.IsLive) HandleGatePhaseChanged(LevelPhase.Live);
            }
        }
    }

    private void HandleGatePhaseChanged(LevelPhase phase)
    {
        if (phase == LevelPhase.Live && CurrentState.value == RaidState.WaitingAtPlatform)
            SetState(RaidState.InRaid);
    }

    private void HandleInfilCompleted()
    {
        if (IsServerAuthority && CurrentState.value == RaidState.WaitingAtPlatform)
            SetState(RaidState.InRaid);
    }

    private void Update()
    {
        if (!IsServerAuthority)
            return;

        if (IsRaidActive)
        {
            TimeRemaining.value -= Time.deltaTime;
            EvaluatePlayerStatuses();
            if (TimeRemaining.value <= 0f)
            {
                AppLog.Log(LogChannel.Match, "[RaidManager] Raid timer expired.", this);
                Resolve(RaidOutcome.TimedOut, null);
            }
        }
        else if (_resolved && !_returning && _autoReturnSeconds > 0f)
        {
            _returnTimer -= Time.deltaTime;
            float shown = Mathf.Ceil(Mathf.Max(0f, _returnTimer));
            if (!Mathf.Approximately(ReturnCountdown.value, shown))
                ReturnCountdown.value = shown;

            if (_returnTimer <= 0f)
                ReturnToHub();
        }
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

        if (activePlayers.Count > 0 && standing == 0)
        {
            AppLog.Log(LogChannel.Match, "[RaidManager] All players eliminated or downed. Raid failed.", this);
            Resolve(RaidOutcome.Wiped, null);
        }
    }

    public void Resolve(RaidOutcome outcome, IReadOnlyCollection<PlayerID> extracted)
    {
        if (!IsServerAuthority || _resolved || !IsRaidActive)
            return;

        _resolved = true;

        var extractedSet = extracted != null ? new HashSet<PlayerID>(extracted) : new HashSet<PlayerID>();
        var results = BankAndBuildResults(outcome, extractedSet);

        Results.value = results;
        if (!isSpawned) HandleResultsChanged(results);

        SetState(outcome == RaidOutcome.Extracted ? RaidState.Completed : RaidState.Failed);

        _returnTimer = _autoReturnSeconds;
        ReturnCountdown.value = Mathf.Ceil(_autoReturnSeconds);

        AppLog.Log(LogChannel.Match,
            $"[RaidManager] Run {results.RunId} resolved: {outcome}, {extractedSet.Count} extracted.", this);
    }

    private RaidResults BankAndBuildResults(RaidOutcome outcome, HashSet<PlayerID> extracted)
    {
        var players = new List<RaidPlayerResult>();
        var activePlayers = PlayerIdentifier.ActivePlayers;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var pawn = activePlayers[i];
            if (!pawn || !pawn.PlayerID.HasValue) continue;

            PlayerID id = pawn.PlayerID.Value;
            bool didExtract = outcome == RaidOutcome.Extracted && extracted.Contains(id) && !pawn.IsDead;

            var carrier = pawn.transform.root.GetComponentInChildren<IDuffleBagCarrier>(true);
            var items = new List<DuffleBagItemEntry>();
            int value = 0;
            if (carrier != null && carrier.HasBag)
            {
                items.AddRange(carrier.StowedItems);
                value = carrier.CurrentValue;
            }

            int banked = didExtract && items.Count > 0
                ? SessionLootLedger.Bank(id, items, value)
                : SessionLootLedger.GetTotalValue(id);

            carrier?.ClearBagContents();

            var ids = new string[items.Count];
            var quantities = new int[items.Count];
            for (int j = 0; j < items.Count; j++)
            {
                ids[j] = items[j].ItemId;
                quantities[j] = items[j].Quantity;
            }

            players.Add(new RaidPlayerResult
            {
                Player = id,
                Extracted = didExtract,
                RunValue = value,
                BankedTotal = banked,
                ItemIds = ids,
                ItemQuantities = quantities
            });
        }

        return new RaidResults
        {
            RunId = ++_runCounter,
            Outcome = outcome,
            Players = players.ToArray()
        };
    }

    public void ReturnToHub()
    {
        if (!IsServerAuthority || !_resolved || _returning)
            return;

        ReturnToHubAsync().Forget();
    }

    private async UniTaskVoid ReturnToHubAsync()
    {
        var coordinator = NetworkSceneCoordinator.Instance;
        if (!coordinator)
        {
            AppLog.LogError(LogChannel.Match, "[RaidManager] Cannot return to hub: NetworkSceneCoordinator missing.", this);
            return;
        }

        _returning = true;
        bool ok = await coordinator.TransitionToSceneAsync(_hubSceneName);
        if (this && !ok)
        {
            AppLog.LogWarning(LogChannel.Match, "[RaidManager] Return to hub failed; host can try again.", this);
            _returning = false;
        }
    }

    public void ChangeState(RaidState newState)
    {
        if (newState is RaidState.Completed or RaidState.Failed)
        {
            AppLog.LogWarning(LogChannel.Match, $"[RaidManager] Use Resolve() to end a raid, not ChangeState({newState}).", this);
            return;
        }

        if (_resolved) return;
        SetState(newState);
    }

    private void SetState(RaidState newState)
    {
        if (!IsServerAuthority)
            return;

        if (CurrentState.value != newState)
        {
            CurrentState.value = newState;
            if (newState == RaidState.WaitingAtPlatform)
                TimeRemaining.value = 1200f;
        }
    }

    private void HandleNetworkStateChanged(RaidState newState)
    {
        PublishToLifecycle(newState);

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

    // Runs on every peer (SyncVar change), so the lifecycle is correct on clients too.
    private static void PublishToLifecycle(RaidState state)
    {
        switch (state)
        {
            case RaidState.ExtractionHoldout:
                RaidLifecycle.MarkExtraction();
                break;
            case RaidState.Completed:
            case RaidState.Failed:
                RaidLifecycle.MarkEnded();
                break;
        }
    }

    private void HandleResultsChanged(RaidResults results)
    {
        if (!results.IsValid || results.RunId == _lastAnnouncedRun) return;

        _lastAnnouncedRun = results.RunId;
        if (_raidEvents) _raidEvents.RaiseResultsReady(results);
    }
}
}
