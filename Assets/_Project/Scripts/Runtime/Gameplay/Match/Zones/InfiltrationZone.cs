using System;
using ImprovedTimers;
using Project.Core.Events;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match.Zones
{
[RequireComponent(typeof(AreaPlayerDetector))]
public class InfiltrationZone : NetworkBehaviour
{
    [Header("References")] 
    [SerializeField] private RaidEventChannel _raidEvents;
    [SerializeField] private AreaPlayerDetector _detector;

    [Header("Countdown Settings")] 
    [SerializeField] private float _countdownDuration = 5f;

    [Header("Networked State")] 
    public SyncVar<float> CountdownRemaining = new(0f);
    public SyncVar<bool> IsCountdownActive = new(false);

    private CountdownTimer _infilTimer;

    public event Action<float> OnCountdownStarted;
    public event Action OnCountdownCancelled;
    public event Action OnInfilCompleted;

    private bool IsServerAuthority =>
        isServer || (NetworkManager.main && NetworkManager.main.isServer) ||
        (!NetworkManager.main || NetworkManager.main.isOffline);

    private void Awake()
    {
        if (!_raidEvents)
        {
            _raidEvents = Resources.Load<RaidEventChannel>("Event Channels/RaidEventChannel");
        }

        if (!_detector)
        {
            _detector = GetComponent<AreaPlayerDetector>() ?? GetComponentInChildren<AreaPlayerDetector>();
        }

        _infilTimer = new CountdownTimer(_countdownDuration);
        _infilTimer.OnTimerStop += HandleInfilTimerStop;
    }

    private void OnEnable()
    {
        if (_detector)
        {
            _detector.OnAllPlayersEntered += HandleAllPlayersEntered;
            _detector.OnPlayerLeft += HandlePlayerLeft;
        }
    }

    private void OnDisable()
    {
        if (_detector)
        {
            _detector.OnAllPlayersEntered -= HandleAllPlayersEntered;
            _detector.OnPlayerLeft -= HandlePlayerLeft;
        }

        if (_infilTimer != null)
        {
            _infilTimer.OnTimerStop -= HandleInfilTimerStop;
            _infilTimer.Dispose();
            _infilTimer = null;
        }
    }

    private void Update()
    {
        if (!IsServerAuthority)
            return;

        if (_infilTimer is { IsRunning: true })
        {
            CountdownRemaining.value = Mathf.Max(0f, _infilTimer.CurrentTime);
            if (!IsCountdownActive.value)
                IsCountdownActive.value = true;
        }
        else if (IsCountdownActive.value && _infilTimer is not { IsRunning: true })
        {
            IsCountdownActive.value = false;
            CountdownRemaining.value = 0f;
        }
    }

    private void HandleAllPlayersEntered()
    {
        if (!IsServerAuthority || _infilTimer == null)
            return;

        _infilTimer.Reset(_countdownDuration);
        _infilTimer.Start();

        IsCountdownActive.value = true;
        CountdownRemaining.value = _countdownDuration;

        RpcNotifyCountdownStarted(_countdownDuration);
    }

    private void HandlePlayerLeft()
    {
        if (!IsServerAuthority || _infilTimer == null)
            return;

        if (_infilTimer.IsRunning)
        {
            _infilTimer.Stop();
            IsCountdownActive.value = false;
            CountdownRemaining.value = 0f;

            RpcNotifyCountdownCancelled();
        }
    }

    private void HandleInfilTimerStop()
    {
        if (!IsServerAuthority || _infilTimer == null)
            return;

        if (!_infilTimer.IsFinished)
            return;

        IsCountdownActive.value = false;
        CountdownRemaining.value = 0f;

        RpcNotifyInfilCompleted();
    }

    [ObserversRpc(runLocally: true)]
    private void RpcNotifyCountdownStarted(float duration)
    {
        OnCountdownStarted?.Invoke(duration);
    }

    [ObserversRpc(runLocally: true)]
    private void RpcNotifyCountdownCancelled()
    {
        OnCountdownCancelled?.Invoke();
    }

    [ObserversRpc(runLocally: true)]
    private void RpcNotifyInfilCompleted()
    {
        OnInfilCompleted?.Invoke();
        if (_raidEvents)
            _raidEvents.RaiseInfilCompleted();

        AppLog.Log(LogChannel.Match, "[InfilStationPlatform] Infil countdown complete! Initiating raid infil sequence.", this);
    }
}
}
