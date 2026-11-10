using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Logging;
using Project.Networking.Scenes;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
public enum LaunchState : byte
{
    Idle,
    Countdown,
    Loading
}

/// <summary>
/// Hub-side authority for starting a level. Lives in the hub scene only (no DontDestroyOnLoad),
/// so every return to the hub starts again from <see cref="LaunchState.Idle"/>.
/// Flow: Idle -> Countdown (cancelable) -> Loading (NetworkSceneCoordinator) -> scene unloads.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[DisallowMultipleComponent]
public class LevelLaunchController : NetworkBehaviour
{
    public static LevelLaunchController Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Seconds players can see 'Deploying' before the scene load starts. 0 = load immediately.")]
    [SerializeField] private float _countdownSeconds = 5f;

    [Tooltip("When on, the host cannot launch until every connected player is ready on the hub ready pad.")]
    [SerializeField] private bool _requireAllReady;

    [Tooltip("Every connected player must stand inside this zone to launch. Leaving it cancels the countdown. Leave empty for no location rule.")]
    [SerializeField] private AreaPlayerDetector _deployZone;

    [Header("Networked State")]
    public SyncVar<LaunchState> State = new(LaunchState.Idle);
    public SyncVar<float> CountdownRemaining = new();
    public SyncVar<string> PendingLevel = new(string.Empty);

    // Mirrors used when the object is not network-spawned (offline hub).
    private LaunchState _localState = LaunchState.Idle;
    private float _localCountdown;
    private string _localPending = string.Empty;
    private bool _cancelRequested;

    public event Action<LaunchState> OnLaunchStateChanged;

    public LaunchState CurrentState => isSpawned ? State.value : _localState;
    public float Countdown => isSpawned ? CountdownRemaining.value : _localCountdown;
    public string Pending => isSpawned ? PendingLevel.value : _localPending;
    public bool IsIdle => CurrentState == LaunchState.Idle;

    private bool IsServerAuthority =>
        isServer || (NetworkManager.main && NetworkManager.main.isServer) ||
        !NetworkManager.main || NetworkManager.main.isOffline;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        State.onChanged += HandleStateChanged;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        State.onChanged -= HandleStateChanged;

        if (Instance == this)
            Instance = null;
    }

    /// <summary>Why a launch cannot start right now, or false with an empty reason when it can.</summary>
    public bool IsLaunchBlocked(out string reason)
    {
        reason = string.Empty;

        if (!IsIdle)
        {
            reason = "Deployment in progress";
            return true;
        }

        if (IsZoneBlocked(out reason))
            return true;

        var ready = HubReadyState.Instance;
        if (_requireAllReady && ready && !ready.AllReady)
        {
            reason = $"Waiting for players ({ready.ReadyCount}/{ready.TotalCount} ready)";
            return true;
        }

        return false;
    }

    public AreaPlayerDetector DeployZone => _deployZone;

    /// <summary>True when a deploy zone is set and not every player is standing in it.</summary>
    private bool IsZoneBlocked(out string reason)
    {
        reason = string.Empty;
        if (!_deployZone) return false;

        if (!_deployZone.AreAllPlayersInside.value)
        {
            reason = $"Everyone must be in the deploy zone ({_deployZone.PlayersInsideCount.value}/{_deployZone.TotalRequiredCount.value})";
            return true;
        }

        return false;
    }

    /// <summary>Server/offline only. Returns false when the request was rejected.</summary>
    public bool TryLaunch(string sceneName)
    {
        if (!IsServerAuthority)
        {
            AppLog.LogWarning(LogChannel.Match, "Only the host can launch a level.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            AppLog.LogError(LogChannel.Match, $"Level '{sceneName}' is not in the build settings.", this);
            return false;
        }

        if (IsLaunchBlocked(out var blockedReason))
        {
            AppLog.LogWarning(LogChannel.Match, $"Launch ignored: {blockedReason}.", this);
            return false;
        }

        _cancelRequested = false;
        SetPending(sceneName);
        RunLaunchAsync(sceneName, destroyCancellationToken).Forget();
        return true;
    }

    /// <summary>Server/offline only. Only valid while the countdown is running.</summary>
    public void CancelLaunch()
    {
        if (!IsServerAuthority || CurrentState != LaunchState.Countdown)
            return;

        _cancelRequested = true;
    }

    private async UniTaskVoid RunLaunchAsync(string sceneName, CancellationToken token)
    {
        try
        {
            if (_countdownSeconds > 0f)
            {
                SetState(LaunchState.Countdown);

                float remaining = _countdownSeconds;
                while (remaining > 0f)
                {
                    if (_cancelRequested)
                    {
                        AppLog.Log(LogChannel.Match, "Launch cancelled during countdown.", this);
                        return;
                    }

                    if (IsZoneBlocked(out var zoneReason))
                    {
                        AppLog.Log(LogChannel.Match, $"Launch cancelled: {zoneReason}.", this);
                        return;
                    }

                    SetCountdown(remaining);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    remaining -= Time.deltaTime;
                }
            }

            SetState(LaunchState.Loading);
            SetCountdown(0f);

            var coordinator = NetworkSceneCoordinator.Instance;
            if (!coordinator)
            {
                AppLog.LogError(LogChannel.Match, "NetworkSceneCoordinator not found. Cannot load level.", this);
                return;
            }

            AppLog.Log(LogChannel.Match, $"Loading level '{sceneName}'.", this);
            await coordinator.TransitionToSceneAsync(sceneName);
        }
        catch (OperationCanceledException)
        {
            // The hub scene was unloaded under us. Nothing to clean up.
        }
        finally
        {
            // After a successful load this object is already destroyed, hence the check.
            if (this)
            {
                _cancelRequested = false;
                SetCountdown(0f);
                SetPending(string.Empty);
                SetState(LaunchState.Idle);
            }
        }
    }

    private void SetState(LaunchState newState)
    {
        if (isSpawned)
            State.value = newState;
        else
        {
            if (_localState == newState) return;
            _localState = newState;
            HandleStateChanged(newState);
        }
    }

    private void SetCountdown(float value)
    {
        if (isSpawned) CountdownRemaining.value = value;
        else _localCountdown = value;
    }

    private void SetPending(string sceneName)
    {
        if (isSpawned) PendingLevel.value = sceneName;
        else _localPending = sceneName;
    }

    private void HandleStateChanged(LaunchState newState)
    {
        AppLog.Log(LogChannel.Match, $"Launch state: {newState}", this);
        OnLaunchStateChanged?.Invoke(newState);
    }
}
}
