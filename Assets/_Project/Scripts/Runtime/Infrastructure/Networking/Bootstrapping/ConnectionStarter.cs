using System.Collections;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Logging;
using Project.Networking.Scenes;
using PurrLobby;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Multiplayer.PlayMode;

namespace Project.Network.Bootstrapping
{
public static class NetworkStartupGate
{
    private static Func<UniTask> _wait;
    private static Func<bool> _canStart;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { _wait = null; _canStart = null; }
    public static void Configure(Func<UniTask> wait, Func<bool> canStart) { _wait = wait; _canStart = canStart; }
    public static async UniTask WaitAsync()
    {
        if (_wait == null) throw new InvalidOperationException("Application bootstrap has not configured networking.");
        await _wait();
        if (_canStart == null || !_canStart()) throw new OperationCanceledException("Application is not accepting connections.");
    }
}

public class ConnectionStarter : MonoBehaviour
{
    [SerializeField] private UDPTransport _udpTransport;
    [SerializeField] private PurrTransport _purrTransport;
    [SerializeField, Min(1f)] private float _lobbyConnectionTimeoutSeconds = 30f;

    private NetworkManager _networkManager;
    private LobbyDataHolder _lobbyDataHolder;
    private LobbyManager _lobbyManager;
    private CancellationTokenSource _connectionCancellation;

    private bool _isFromLobby;
    private bool _connectionPending;
    private bool _lobbyHubTransitionRequested;
    public static bool HasLobbyConnection => FindAnyObjectByType<LobbyDataHolder>();

    private void Awake()
    {
        if (NetworkManager.main && NetworkManager.main != GetComponent<NetworkManager>() && !NetworkManager.main.isOffline)
        {
            Destroy(gameObject);
            return;
        }

        if (!TryGetComponent(out _networkManager))
        {
            AppLog.LogError(LogChannel.Network, $"Failed to get {nameof(NetworkManager)} component.", this);
            return;
        }

        if (!_udpTransport)
        {
            AppLog.LogError(LogChannel.Network, $"Failed to get {nameof(UDPTransport)} component.", this);
            return;
        }

        if (!_purrTransport)
        {
            AppLog.LogError(LogChannel.Network, $"Failed to get {nameof(PurrTransport)} component.", this);
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySubscribeToLobbyReady();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (_lobbyManager)
            _lobbyManager.OnAllReady.RemoveListener(HandleLobbyAllReady);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Scene_Lobby")
        {
            TrySubscribeToLobbyReady();
            return;
        }

        if (_lobbyManager)
        {
            _lobbyManager.OnAllReady.RemoveListener(HandleLobbyAllReady);
            _lobbyManager = null;
        }
    }

    private void TrySubscribeToLobbyReady()
    {
        if (_lobbyManager) return;

        _lobbyManager = FindAnyObjectByType<LobbyManager>();
        if (_lobbyManager)
            _lobbyManager.OnAllReady.AddListener(HandleLobbyAllReady);
    }

    private void HandleLobbyAllReady()
    {
        _lobbyDataHolder = FindAnyObjectByType<LobbyDataHolder>();
        _isFromLobby = _lobbyDataHolder;
        if (!_isFromLobby)
        {
            AppLog.LogError(LogChannel.Network,
                $"Cannot start the lobby session: {nameof(LobbyDataHolder)} is missing.", this);
            return;
        }

        StartRequestedConnection();
    }

    public void StartRequestedConnection()
    {
        if (_connectionPending) return;
        _connectionPending = true;
        _connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        StartRequestedConnectionAsync(_connectionCancellation).Forget();
    }

    private async UniTask StartRequestedConnectionAsync(CancellationTokenSource cancellationSource)
    {
        CancellationToken cancellationToken = cancellationSource.Token;
        try
        {
            await NetworkStartupGate.WaitAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (!this || !isActiveAndEnabled) return;
            _lobbyDataHolder = FindAnyObjectByType<LobbyDataHolder>();
            _isFromLobby = _lobbyDataHolder;
            await StartNetworkStandardAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { AppLog.LogException(LogChannel.Network, exception, this); }
        finally
        {
            cancellationSource.Dispose();
            if (this)
            {
                if (_connectionCancellation == cancellationSource)
                    _connectionCancellation = null;
                _connectionPending = false;
            }
        }
    }

    public void StopPendingConnection()
    {
        _connectionCancellation?.Cancel();
        _connectionPending = false;
    }

    private async UniTask StartNetworkStandardAsync(CancellationToken cancellationToken)
    {
        if (!_networkManager)
        {
            AppLog.LogError(LogChannel.Network, $"Failed to start connection. {nameof(NetworkManager)} is null!", this);
            return;
        }

        if (_networkManager.isServer || _networkManager.isClient)
        {
            return;
        }

        if (_isFromLobby)
        {
            await StartFromLobbyAsync(cancellationToken);
        }
        else
        {
            StartNormal();
        }
    }


    private void StartNormal()
    {
        var host = CurrentPlayer.IsMainEditor;
        _networkManager.transport = _udpTransport;

        if (host)
            _networkManager.StartHost();
        else
            _networkManager.StartClient();
    }

    public void ReturnToHub()
    {
        NetworkSceneCoordinator.Instance.TransitionToScene("Scene_Hub");
    }

    private async UniTask StartFromLobbyAsync(CancellationToken cancellationToken)
    {
        if (!_lobbyDataHolder)
        {
            AppLog.LogError(LogChannel.Network, $"Failed to start connection. {nameof(LobbyDataHolder)} is null!", this);
            return;
        }

        if (!_lobbyDataHolder.CurrentLobby.IsValid)
        {
            AppLog.LogError(LogChannel.Network, $"Failed to start connection. Lobby is invalid!", this);
            return;
        }

        if (!_purrTransport)
        {
            AppLog.LogError(LogChannel.Network, $"Failed to start connection. {nameof(PurrTransport)} is null!", this);
            return;
        }

        _networkManager.transport = _purrTransport;
        if (_networkManager.transport is PurrTransport transport)
        {
            transport.roomName = _lobbyDataHolder.CurrentLobby.LobbyId;
        }

        bool isLobbyHost = _lobbyDataHolder.CurrentLobby.IsOwner;
        if (isLobbyHost)
        {
            _networkManager.StartServer();

            float serverDeadline = Time.realtimeSinceStartup + _lobbyConnectionTimeoutSeconds;
            while (_networkManager &&
                   _networkManager.serverState != ConnectionState.Connected &&
                   Time.realtimeSinceStartup < serverDeadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (!_networkManager || _networkManager.serverState != ConnectionState.Connected)
            {
                AppLog.LogError(LogChannel.Network,
                    $"Lobby host server did not become ready within {_lobbyConnectionTimeoutSeconds:0.#} seconds.", this);
                return;
            }

            // PurrTransport needs its hosted relay session ready before the host joins it as a client.
            _networkManager.StartClient();
        }
        else
            _networkManager.StartClient();

        float deadline = Time.realtimeSinceStartup + _lobbyConnectionTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_networkManager) return;

            bool clientConnected = _networkManager.clientState == ConnectionState.Connected;
            bool hostConnected = !isLobbyHost || _networkManager.serverState == ConnectionState.Connected;
            if (clientConnected && hostConnected)
                break;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        bool connected = _networkManager &&
                         _networkManager.clientState == ConnectionState.Connected &&
                         (!isLobbyHost || _networkManager.serverState == ConnectionState.Connected);
        if (!connected)
        {
            AppLog.LogError(LogChannel.Network,
                $"Lobby network connection did not become ready within {_lobbyConnectionTimeoutSeconds:0.#} seconds.", this);
            return;
        }

        if (!isLobbyHost || _lobbyHubTransitionRequested)
            return;

        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        var sceneCoordinator = NetworkSceneCoordinator.Instance;
        if (!sceneCoordinator)
        {
            AppLog.LogError(LogChannel.Network,
                $"Cannot enter the hub: {nameof(NetworkSceneCoordinator)} is missing.", this);
            return;
        }

        _lobbyHubTransitionRequested = true;
        AppLog.Log(LogChannel.Network, "Lobby session connected. Host is loading Scene_Hub through PurrNet.", this);
        sceneCoordinator.TransitionToScene("Scene_Hub");
    }
}
}
