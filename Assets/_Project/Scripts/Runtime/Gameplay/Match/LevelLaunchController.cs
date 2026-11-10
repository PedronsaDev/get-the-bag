using System;
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
    Selected,
    Countdown,
    Loading
}

[RequireComponent(typeof(NetworkIdentity))]
[DisallowMultipleComponent]
public class LevelLaunchController : NetworkBehaviour
{
    public static LevelLaunchController Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Seconds of countdown once everyone is in position. 0 = load immediately.")]
    [SerializeField] private float _countdownSeconds = 5f;

    [Tooltip("When on, the countdown also waits until every connected player is ready on the hub ready pad.")]
    [SerializeField] private bool _requireAllReady;

    [Tooltip("Every connected player must stand inside this zone for the countdown to run. Leave empty for no location rule.")]
    [SerializeField] private AreaPlayerDetector _deployZone;

    [Header("Networked State")]
    public SyncVar<LaunchState> State = new(LaunchState.Idle);
    public SyncVar<float> CountdownRemaining = new();
    public SyncVar<string> PendingLevel = new(string.Empty);

    private LaunchState _localState = LaunchState.Idle;
    private float _localCountdown;
    private string _localPending = string.Empty;

    private float _remaining;

    public event Action<LaunchState> OnLaunchStateChanged;

    public LaunchState CurrentState => isSpawned ? State.value : _localState;
    public float Countdown => isSpawned ? CountdownRemaining.value : _localCountdown;
    public string Pending => isSpawned ? PendingLevel.value : _localPending;
    public bool IsIdle => CurrentState == LaunchState.Idle;
    public bool HasSelection => CurrentState != LaunchState.Idle;
    public bool IsLoading => CurrentState == LaunchState.Loading;
    public AreaPlayerDetector DeployZone => _deployZone;

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

    public static string DisplayName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return string.Empty;
        if (sceneName.StartsWith("Scene_")) sceneName = sceneName.Substring(6);
        return sceneName.Replace('_', ' ').ToUpperInvariant();
    }

    public bool TrySelectLevel(string sceneName)
    {
        if (!IsServerAuthority)
        {
            AppLog.LogWarning(LogChannel.Match, "Only the host can select a level.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            AppLog.LogError(LogChannel.Match, $"Level '{sceneName}' is not in the build settings.", this);
            return false;
        }

        if (IsLoading)
        {
            AppLog.LogWarning(LogChannel.Match, "Cannot change the level while it is loading.", this);
            return false;
        }

        SetPending(sceneName);
        SetCountdown(0f);
        SetState(LaunchState.Selected);
        AppLog.Log(LogChannel.Match, $"Level selected: '{sceneName}'.", this);
        return true;
    }

    public void ClearSelection()
    {
        if (!IsServerAuthority || IsIdle || IsLoading)
            return;

        SetCountdown(0f);
        SetPending(string.Empty);
        SetState(LaunchState.Idle);
        AppLog.Log(LogChannel.Match, "Level selection cleared by host.", this);
    }

    public bool ArePlayersInPosition(out string reason)
    {
        reason = string.Empty;

        if (_deployZone && !_deployZone.AreAllPlayersInside.value)
        {
            reason = $"Waiting for players in the deploy zone ({_deployZone.PlayersInsideCount.value}/{_deployZone.TotalRequiredCount.value})";
            return false;
        }

        var ready = HubReadyState.Instance;
        if (_requireAllReady && ready && !ready.AllReady)
        {
            reason = $"Waiting for players to ready up ({ready.ReadyCount}/{ready.TotalCount})";
            return false;
        }

        return true;
    }

    private void Update()
    {
        if (!IsServerAuthority) return;

        switch (CurrentState)
        {
            case LaunchState.Selected:
                if (ArePlayersInPosition(out _))
                    StartCountdown();
                break;

            case LaunchState.Countdown:
                if (!ArePlayersInPosition(out var reason))
                {
                    AppLog.Log(LogChannel.Match, $"Countdown reset: {reason}.", this);
                    SetCountdown(0f);
                    SetState(LaunchState.Selected);
                    break;
                }

                _remaining -= Time.deltaTime;
                PublishCountdown();
                if (_remaining <= 0f)
                    BeginLoadAsync(Pending).Forget();
                break;
        }
    }

    private void StartCountdown()
    {
        _remaining = Mathf.Max(0f, _countdownSeconds);
        SetState(LaunchState.Countdown);
        PublishCountdown();

        if (_remaining <= 0f)
            BeginLoadAsync(Pending).Forget();
    }

    private void PublishCountdown()
    {
        float shown = Mathf.Ceil(Mathf.Max(0f, _remaining));
        if (!Mathf.Approximately(Countdown, shown))
            SetCountdown(shown);
    }

    private async UniTaskVoid BeginLoadAsync(string sceneName)
    {
        SetState(LaunchState.Loading);
        SetCountdown(0f);

        var coordinator = NetworkSceneCoordinator.Instance;
        if (!coordinator)
        {
            AppLog.LogError(LogChannel.Match, "NetworkSceneCoordinator not found. Cannot load level.", this);
            SetState(LaunchState.Selected);
            return;
        }

        AppLog.Log(LogChannel.Match, $"Loading level '{sceneName}'.", this);
        bool ok = await coordinator.TransitionToSceneAsync(sceneName);

        if (this && !ok)
        {
            AppLog.LogWarning(LogChannel.Match, $"Loading '{sceneName}' failed. Selection kept.", this);
            SetState(LaunchState.Selected);
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
