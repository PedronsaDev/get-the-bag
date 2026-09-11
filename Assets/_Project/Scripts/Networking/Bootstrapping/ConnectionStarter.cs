using System.Collections;
using Project.Networking.Scenes;
using PurrLobby;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;
using Unity.Multiplayer.PlayMode;

namespace Project.Network.Bootstrapping
{

public class ConnectionStarter : MonoBehaviour
{
    [SerializeField] private UDPTransport _udpTransport;
    [SerializeField] private PurrTransport _purrTransport;
    
    private NetworkManager _networkManager;
    private LobbyDataHolder _lobbyDataHolder;

    private bool _isFromLobby;

    private void Awake()
    {
        if (!TryGetComponent(out _networkManager))
        {
            PurrLogger.LogError($"Failed to get {nameof(NetworkManager)} component.", this);
            return;
        }

        if (!_udpTransport)
        {
            PurrLogger.LogError($"Failed to get {nameof(UDPTransport)} component.", this);
            return;
        }

        if (!_purrTransport)
        {
            PurrLogger.LogError($"Failed to get {nameof(PurrTransport)} component.", this);
            return;
        }

        _lobbyDataHolder = FindAnyObjectByType<LobbyDataHolder>();
        
        if (_lobbyDataHolder)
        {
            _isFromLobby = true;
        }
    }
    
    private void Start()
    {
        StartNetworkStandard();
    }

    private void StartNetworkStandard()
    {
        if (!_networkManager)
        {
            PurrLogger.LogError($"Failed to start connection. {nameof(NetworkManager)} is null!", this);
            return;
        }

        if (!_purrTransport)
        {
            PurrLogger.LogError($"Failed to start connection. {nameof(PurrTransport)} is null!", this);
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
        NetworkSceneCoordinator.Instance.TransitionToScene("HubScene");
    }
    
    private void StartFromLobby()
    {
        _networkManager.transport = _purrTransport;
        if (!_lobbyDataHolder)
        {
            PurrLogger.LogError($"Failed to start connection. {nameof(LobbyDataHolder)} is null!", this);
            return;
        }

        if (!_lobbyDataHolder.CurrentLobby.IsValid)
        {
            PurrLogger.LogError($"Failed to start connection. Lobby is invalid!", this);
            return;
        }

        if (_networkManager.transport is PurrTransport transport)
        {
            transport.roomName = _lobbyDataHolder.CurrentLobby.LobbyId;
        }
        
        if (_lobbyDataHolder.CurrentLobby.IsOwner)
            _networkManager.StartServer();
        
        StartCoroutine(StartClient());
    }
    
    

    private IEnumerator StartClient()
    {
        yield return new WaitForSeconds(1f);
        _networkManager.StartClient();
    }
}
}