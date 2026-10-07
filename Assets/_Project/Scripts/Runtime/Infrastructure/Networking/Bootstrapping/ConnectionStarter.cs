using System.Collections;
using System;
using Cysharp.Threading.Tasks;
using Project.Logging;
using Project.Networking.Scenes;
using PurrLobby;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;
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

    private NetworkManager _networkManager;
    private LobbyDataHolder _lobbyDataHolder;

    private bool _isFromLobby;
    private bool _connectionPending;
    private Coroutine _clientRoutine;
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

        _lobbyDataHolder = FindAnyObjectByType<LobbyDataHolder>();

        if (_lobbyDataHolder)
        {
            _isFromLobby = true;
        }
    }

    public void StartRequestedConnection()
    {
        if (_connectionPending || _clientRoutine != null) return;
        _connectionPending = true;
        StartRequestedConnectionAsync().Forget();
    }

    private async UniTask StartRequestedConnectionAsync()
    {
        try
        {
            await NetworkStartupGate.WaitAsync();
            if (!this || !isActiveAndEnabled) return;
            _lobbyDataHolder = FindAnyObjectByType<LobbyDataHolder>();
            _isFromLobby = _lobbyDataHolder;
            StartNetworkStandard();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { AppLog.LogException(LogChannel.Network, exception, this); }
        finally { if (this) _connectionPending = false; }
    }

    public void StopPendingConnection()
    {
        StopAllCoroutines();
        _clientRoutine = null;
        _connectionPending = false;
    }

    private void StartNetworkStandard()
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

        if (!_purrTransport)
        {
            AppLog.LogError(LogChannel.Network, $"Failed to start connection. {nameof(PurrTransport)} is null!", this);
            return;
        }

        if (_isFromLobby)
        {
            StartFromLobby();
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

    private void StartFromLobby()
    {
        _networkManager.transport = _purrTransport;
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

        if (_networkManager.transport is PurrTransport transport)
        {
            transport.roomName = _lobbyDataHolder.CurrentLobby.LobbyId;
        }

        if (_lobbyDataHolder.CurrentLobby.IsOwner)
            _networkManager.StartServer();

        _clientRoutine = StartCoroutine(StartClient());
    }



    private IEnumerator StartClient()
    {
        yield return new WaitForSeconds(1f);
        if (_networkManager && !_networkManager.isClient)
            _networkManager.StartClient();
        _clientRoutine = null;
    }
}
}
