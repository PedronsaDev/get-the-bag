using System;
using System.Collections.Generic;
using ImprovedTimers;
using Project.Core.Player;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match.Zones
{
[RequireComponent(typeof(AreaPlayerDetector))]
public class ExtractionZone : NetworkBehaviour
{
    [Header("References")] 
    [SerializeField] private AreaPlayerDetector _detector;
    
    [Header("Settings")]
    [SerializeField] private float _countdownDuration = 30f;
    
    public SyncVar<float> HoldoutProgress = new();
    public SyncVar<bool> IsActivated = new();
    public SyncVar<bool> IsCountdownActive = new();
    public SyncVar<float> CountdownRemaining = new();
    
    public event Action OnExtractionActivated;
    public event Action<PlayerID, bool> OnPlayerExtractionResolved; // player, extracted or not
    
    private CountdownTimer _exfilTimer;
    
    private bool IsServerAuthority =>
        isServer;

    private void Awake()
    {
        if (!_detector)
        {
            _detector = GetComponent<AreaPlayerDetector>() ?? GetComponentInChildren<AreaPlayerDetector>();
        }

        _exfilTimer = new CountdownTimer(_countdownDuration);
        _exfilTimer.OnTimerStop += HandleExfilTimerStop;
    }

    private void OnDisable()
    {
        if (_exfilTimer != null)
        {
            _exfilTimer.OnTimerStop -= HandleExfilTimerStop;
            _exfilTimer.Dispose();
            _exfilTimer = null;
        }
    }
    
    private void Update()
    {
        if (!IsServerAuthority)
            return;

        if (!IsActivated.value || !RaidManager.Instance || !RaidManager.Instance.IsActive)
            return;

        if (_exfilTimer is { IsRunning: true })
        {
            CountdownRemaining.value = Mathf.Max(0f, _exfilTimer.CurrentTime);
            if (!IsCountdownActive.value)
                IsCountdownActive.value = true;
        }
        else if (IsCountdownActive.value && _exfilTimer is not { IsRunning: true })
        {
            IsCountdownActive.value = false;
            CountdownRemaining.value = 0f;
        }
    }
    
    public void ActivateExtraction()
    {
        if (!isServer || IsActivated.value || !RaidManager.Instance || !RaidManager.Instance.IsActive ||
            RaidManager.Instance.TimeRemaining.value <= 0f)
            return;

        _detector.Activate();
        IsActivated.value = true;

        if (_exfilTimer == null)
        {
            _exfilTimer = new CountdownTimer(_countdownDuration);
            _exfilTimer.OnTimerStop += HandleExfilTimerStop;
        }
        else
        {
            _exfilTimer.Reset(_countdownDuration);
        }

        IsCountdownActive.value = true;
        CountdownRemaining.value = _countdownDuration;
        _exfilTimer.Start();

        if (RaidManager.Instance)
        {
            RaidManager.Instance.ChangeState(RaidState.ExtractionHoldout);
        }
        
        OnExtractionActivated?.Invoke();
    }

    private void HandleExfilTimerStop()
    {
        if (!IsServerAuthority || _exfilTimer == null || !RaidManager.Instance || !RaidManager.Instance.IsActive)
            return;

        if (!_exfilTimer.IsFinished)
            return;

        IsCountdownActive.value = false;
        CountdownRemaining.value = 0f;

        var activePlayers = PlayerIdentifier.ActivePlayers;
        var extractedPlayers = new List<PlayerID>();
        var leftBehindPlayers = new List<PlayerID>();

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var p = activePlayers[i];
            if (!p || !p.PlayerID.HasValue)
                continue;

            PlayerID playerID = p.PlayerID.Value;

            if (p.IsDead)
            {
                leftBehindPlayers.Add(playerID);
                continue;
            }

            if (_detector && _detector.IsPlayerInside(playerID))
            {
                extractedPlayers.Add(playerID);
            }
            else
            {
                leftBehindPlayers.Add(playerID);
            }
        }

        if (!RaidManager.Instance.ResolveExtractionServer(extractedPlayers)) return;
        RpcBroadcastExtractionResults(extractedPlayers.ToArray(), leftBehindPlayers.ToArray());

        AppLog.Log(LogChannel.Match, $"[ExfilZone] Exfil countdown complete. {extractedPlayers.Count} extracted, {leftBehindPlayers.Count} left behind.");
    }

    [ObserversRpc(runLocally: true)]
    private void RpcBroadcastExtractionResults(PlayerID[] extracted, PlayerID[] leftBehind)
    {
        PlayerID? localID = NetworkManager.main ? NetworkManager.main.localPlayer : null;

        foreach (var id in extracted)
        {
            bool isLocal = localID.HasValue && localID.Value == id;
            OnPlayerExtractionResolved?.Invoke(id, true);

            if (isLocal)
            {
                HandleLocalPlayerExtracted();
            }
        }

        foreach (var id in leftBehind)
        {
            bool isLocal = localID.HasValue && localID.Value == id;
            OnPlayerExtractionResolved?.Invoke(id, false);

            if (isLocal)
            {
                HandleLocalPlayerLeftBehind();
            }
        }
    }

    private void HandleLocalPlayerLeftBehind()
    {
        AppLog.Log(LogChannel.Match, "[Extraction] <color=red>You were left behind and failed to extract.</color>");
    }

    private void HandleLocalPlayerExtracted()
    {
        AppLog.Log(LogChannel.Match, "[Extraction] <color=green>You successfully extracted.</color>");
    }
}
}
