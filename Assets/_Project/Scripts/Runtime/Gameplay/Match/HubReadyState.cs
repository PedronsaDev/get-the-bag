using Project.Core.Interaction;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Collider))]
public class HubReadyState : NetworkBehaviour, IInteractable
{
    public static HubReadyState Instance { get; private set; }

    public SyncList<PlayerID> ReadyPlayers = new();
    public SyncVar<int> PlayerCount = new(1);

    private bool _offlineReady;

    private bool IsOffline => !NetworkManager.main || NetworkManager.main.isOffline;
    private bool IsServerAuthority => IsOffline || isServer || NetworkManager.main.isServer;

    public int TotalCount => isSpawned && !IsOffline ? Mathf.Max(1, PlayerCount.value) : 1;
    public int ReadyCount => isSpawned && !IsOffline ? ReadyPlayers.Count : (_offlineReady ? 1 : 0);
    public bool AllReady => ReadyCount >= TotalCount;

    public bool IsLocalReady
    {
        get
        {
            if (IsOffline || !isSpawned) return _offlineReady;
            var local = NetworkManager.main.localPlayer;
            return ReadyPlayers.Contains(local);
        }
    }

    public string InteractionPrompt => IsLocalReady ? "[F] Cancel Ready" : "[F] Ready Up";
    public string HoldPrompt => string.Empty;
    public string TargetName => "Ready Pad";
    public string Subtext => $"{ReadyCount}/{TotalCount} ready";
    public Color AccentColor => IsLocalReady ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.8f, 0.2f);
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration => 0f;

    private void Awake()
    {
        if (!Instance) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) gameObject.layer = layer;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (IsOffline || !isSpawned || !isServer) return;

        var module = networkManager.playerModule;
        if (module == null) return;

        int count = module.players.Count;
        if (PlayerCount.value != count) PlayerCount.value = count;

        for (int i = ReadyPlayers.Count - 1; i >= 0; i--)
        {
            if (!IsConnected(module, ReadyPlayers[i]))
                ReadyPlayers.RemoveAt(i);
        }
    }

    private static bool IsConnected(PurrNet.Modules.PlayersManager module, PlayerID id)
    {
        foreach (var p in module.players)
            if (p == id) return true;
        return false;
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false) => true;
    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false) => string.Empty;

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        bool want = !IsLocalReady;

        if (IsOffline || !isSpawned)
        {
            _offlineReady = want;
            return;
        }

        if (isServer)
            SetReadyServer(NetworkManager.main.localPlayer, want);
        else
            RpcSetReady(want);
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcSetReady(bool ready, RPCInfo info = default)
    {
        SetReadyServer(info.sender, ready);
    }

    private void SetReadyServer(PlayerID player, bool ready)
    {
        bool has = ReadyPlayers.Contains(player);
        if (ready && !has) ReadyPlayers.Add(player);
        else if (!ready && has) ReadyPlayers.Remove(player);

        AppLog.Log(LogChannel.Match, $"Player {player} ready={ready} ({ReadyPlayers.Count}/{PlayerCount.value})", this);
    }

    public void OnHoverEnter(IPlayerInteractor interactor) { }
    public void OnHoverExit(IPlayerInteractor interactor) { }
}
}
