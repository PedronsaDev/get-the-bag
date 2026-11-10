using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Project.Core.Player;
using Project.Logging;
using Project.Networking.Scenes;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
public enum LevelPhase : byte
{
    /// <summary>Scene is loaded, but not every player has a working pawn yet. Gameplay must not run.</summary>
    Loading,
    /// <summary>Every expected player is in and spawned. Gameplay may start.</summary>
    Live,
    /// <summary>Someone never showed up in time. Everyone is being sent back to the hub.</summary>
    Failed
}

/// <summary>
/// Place one in every level scene. It answers "is it safe to start the game yet?".
/// The server freezes the roster when the level spawns and waits until each of those players has
/// (a) a spawned pawn on the server and (b) a message from that player's own client saying its
/// local pawn exists. If that takes longer than the timeout, everyone goes back to the hub.
/// Other systems read <see cref="IsLive"/> or listen to <see cref="OnPhaseChanged"/>.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[DisallowMultipleComponent]
public class LevelReadyGate : NetworkBehaviour
{
    public static LevelReadyGate Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Seconds the server waits for every player's pawn before sending everyone back.")]
    [SerializeField, Min(5f)] private float _timeoutSeconds = 30f;
    [Tooltip("Scene to return to when the level cannot start.")]
    [SerializeField] private string _hubSceneName = "Scene_Hub";
    [Tooltip("Frames a client waits after its pawn appears before reporting ready.")]
    [SerializeField, Min(0)] private int _settleFrames = 2;

    [Header("Networked State")]
    public SyncVar<LevelPhase> Phase = new(LevelPhase.Loading);
    public SyncVar<int> ExpectedCount = new();
    public SyncList<PlayerID> ReadyPlayers = new();
    public SyncVar<string> FailureReason = new(string.Empty);

    /// <summary>Fires on every peer when the phase changes (and once for the offline shortcut).</summary>
    public event Action<LevelPhase> OnPhaseChanged;

    private LevelPhase _localPhase = LevelPhase.Loading;
    private readonly HashSet<PlayerID> _expected = new();
    private readonly HashSet<PlayerID> _reported = new();
    private float _serverStartTime;
    private bool _clientReported;
    private int _clientSettle;
    private float _overlayDeadline;
    private bool _holdingOverlay;

    private bool IsOffline => !NetworkManager.main || NetworkManager.main.isOffline;

    public LevelPhase CurrentPhase => isSpawned && !IsOffline ? Phase.value : _localPhase;
    public bool IsLive => CurrentPhase == LevelPhase.Live;
    public bool IsResolved => CurrentPhase != LevelPhase.Loading;
    public int ReadyCount => isSpawned && !IsOffline ? ReadyPlayers.Count : (IsLive ? 1 : 0);
    public int Expected => isSpawned && !IsOffline ? ExpectedCount.value : 1;

    private void Awake()
    {
        if (!Instance) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        Phase.onChanged += HandlePhaseChanged;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Phase.onChanged -= HandlePhaseChanged;
        var nm = NetworkManager.main;
        if (nm && nm.playerModule != null)
            nm.playerModule.onPlayerLeft -= HandlePlayerLeft;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Running a level scene directly without a network session (editor testing): nothing to wait for.
        if (IsOffline && !isSpawned)
            SetPhase(LevelPhase.Live);
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        if (!isServer) return;

        _serverStartTime = Time.time;
        _expected.Clear();
        _reported.Clear();
        if (networkManager.playerModule != null)
        {
            foreach (var p in networkManager.playerModule.players)
                _expected.Add(p);
            networkManager.playerModule.onPlayerLeft += HandlePlayerLeft;
        }

        ExpectedCount.value = _expected.Count;
        AppLog.Log(LogChannel.Match, $"Level gate waiting for {_expected.Count} player(s).", this);
    }

    private void Update()
    {
        if (!isSpawned || IsOffline)
        {
            ReleaseOverlayIfResolved();
            return;
        }

        if (isServer) ServerTick();
        ClientTick();
        ReleaseOverlayIfResolved();
    }

    // ---- Server ----

    private void ServerTick()
    {
        if (Phase.value != LevelPhase.Loading) return;

        foreach (var p in _expected)
        {
            if (ReadyPlayers.Contains(p)) continue;
            // Ready needs both: the client said so, and the server really has that player's pawn.
            if (_reported.Contains(p) && PlayerIdentifier.TryGetPlayer(p, out var pawn) && pawn.isSpawned)
                ReadyPlayers.Add(p);
        }

        if (_expected.Count > 0 && ReadyPlayers.Count >= _expected.Count)
        {
            AppLog.Log(LogChannel.Match, "Level gate: all players ready. Going live.", this);
            Phase.value = LevelPhase.Live;
            return;
        }

        if (Time.time - _serverStartTime > _timeoutSeconds)
            Fail();
    }

    private void HandlePlayerLeft(PlayerID player, bool asServer)
    {
        if (!isServer || !asServer || Phase.value != LevelPhase.Loading) return;

        _expected.Remove(player);
        _reported.Remove(player);
        if (ReadyPlayers.Contains(player)) ReadyPlayers.Remove(player);
        ExpectedCount.value = _expected.Count;
        AppLog.LogWarning(LogChannel.Match, $"Level gate: player {player} left while loading.", this);
    }

    private void Fail()
    {
        var sb = new StringBuilder("Players did not load in time: ");
        bool any = false;
        foreach (var p in _expected)
        {
            if (ReadyPlayers.Contains(p)) continue;
            if (any) sb.Append(", ");
            sb.Append(p);
            any = true;
        }

        FailureReason.value = sb.ToString();
        AppLog.LogError(LogChannel.Match, $"Level gate failed. {FailureReason.value}", this);
        Phase.value = LevelPhase.Failed;
        ReturnToHubAsync().Forget();
    }

    private async UniTaskVoid ReturnToHubAsync()
    {
        // Let every peer see the failure state before the scene changes under them.
        await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: destroyCancellationToken);

        var coordinator = NetworkSceneCoordinator.Instance;
        if (!coordinator)
        {
            AppLog.LogError(LogChannel.Match, "Cannot return to hub: NetworkSceneCoordinator missing.", this);
            return;
        }

        await coordinator.TransitionToSceneAsync(_hubSceneName);
    }

    // ---- Client (the host is a client too) ----

    private void ClientTick()
    {
        var nm = NetworkManager.main;
        if (_clientReported || !nm || !nm.isClient || Phase.value != LevelPhase.Loading) return;

        if (!PlayerIdentifier.TryGetPlayer(nm.localPlayer, out var pawn) || !pawn.isSpawned)
        {
            _clientSettle = 0;
            return;
        }

        // Give the pawn a couple of frames to run its own setup before claiming "ready".
        if (_clientSettle++ < _settleFrames) return;

        _clientReported = true;
        if (isServer) _reported.Add(nm.localPlayer);
        else RpcReportReady();
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcReportReady(RPCInfo info = default)
    {
        _reported.Add(info.sender);
    }

    // ---- Shared ----

    private void SetPhase(LevelPhase phase)
    {
        if (_localPhase == phase) return;
        _localPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }

    private void HandlePhaseChanged(LevelPhase phase)
    {
        _localPhase = phase;
        AppLog.Log(LogChannel.Match, $"Level phase: {phase}", this);
        OnPhaseChanged?.Invoke(phase);
    }

    // ---- Loading overlay hand-off ----

    /// <summary>Called by the loading bridge when this peer's scene finished loading but the level is not live yet.</summary>
    internal void HoldLoadingScreen()
    {
        _holdingOverlay = true;
        _overlayDeadline = Time.unscaledTime + _timeoutSeconds + 10f;
    }

    private void ReleaseOverlayIfResolved()
    {
        if (!_holdingOverlay) return;

        // The deadline protects a peer that never receives the resolution (for example a dropped host).
        if (IsResolved || Time.unscaledTime > _overlayDeadline)
        {
            _holdingOverlay = false;
            LevelLoadingScreenBridge.ReleaseNow();
        }
    }
}
}
