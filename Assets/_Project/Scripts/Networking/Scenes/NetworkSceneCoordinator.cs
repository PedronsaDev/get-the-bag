using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Networking.Scenes
{
public class NetworkSceneCoordinator : NetworkBehaviour
{
    public static NetworkSceneCoordinator Instance { get; private set; }

    public event Action<string> OnSceneTransitionStarted;
    public event Action<float> OnLocalLoadProgress;
    public event Action OnAllClientsReady;

    private readonly HashSet<PlayerID> _readyClients = new();
    private bool _isTransitioning;

    protected override void OnSpawned()
    {
        base.OnSpawned();
        if (!Instance)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async UniTask TransitionToSceneAsync(string sceneName)
    {
        if (!isServer)
        {
            Debug.LogWarning($"[{nameof(NetworkSceneCoordinator)}] Only the Host can initiate scene transitions!", this);
            return;
        }

        if (_isTransitioning)
        {
            Debug.LogWarning($"[{nameof(NetworkSceneCoordinator)}] Scene transition already in progress.", this);
            return;
        }

        var allReadyUcs = new UniTaskCompletionSource();
        Action onReady = () => allReadyUcs.TrySetResult();
        OnAllClientsReady += onReady;

        try
        {
            TransitionToScene(sceneName);
            await allReadyUcs.Task;
        }
        finally
        {
            OnAllClientsReady -= onReady;
        }
    }
    
    /// <summary>
    /// Host-only entry point to transition all connected players to a new scene.
    /// </summary>
    public void TransitionToScene(string sceneName)
    {
        if (!isServer)
        {
            Debug.LogWarning($"[{nameof(NetworkSceneCoordinator)}] Only the Host can initiate scene transitions!",
                this);
            return;
        }

        if (_isTransitioning)
        {
            Debug.LogWarning($"[{nameof(NetworkSceneCoordinator)}] Scene transition already in progress.", this);
            return;
        }

        _isTransitioning = true;
        _readyClients.Clear();

        Debug.Log($"[{nameof(NetworkSceneCoordinator)}] Host initiating transition to scene: {sceneName}");
        OnSceneTransitionStarted?.Invoke(sceneName);

        // Notify all clients to load the target scene
        RpcInstructLoadScene(sceneName);

        // Host loads scene locally
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    [ObserversRpc(runLocally: false)]
    private void RpcInstructLoadScene(string sceneName)
    {
        Debug.Log($"[{nameof(NetworkSceneCoordinator)}] Received scene transition request from Host: {sceneName}");
        OnSceneTransitionStarted?.Invoke(sceneName);
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        if (asyncOp == null)
        {
            Debug.LogError($"[{nameof(NetworkSceneCoordinator)}] Failed to load scene: {sceneName}!");
            yield break;
        }

        while (!asyncOp.isDone)
        {
            OnLocalLoadProgress?.Invoke(asyncOp.progress);
            yield return null;
        }

        OnLocalLoadProgress?.Invoke(1f);
        Debug.Log($"[{nameof(NetworkSceneCoordinator)}] Local scene load completed: {sceneName}");

        // Notify Host that local client has completed loading
        if (isServer)
        {
            HandleClientReady(localPlayer ?? default);
        }
        else
        {
            RpcNotifyHostSceneReady();
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcNotifyHostSceneReady(RPCInfo info = default)
    {
        HandleClientReady(info.sender);
    }

    private void HandleClientReady(PlayerID playerId)
    {
        if (!isServer) return;

        _readyClients.Add(playerId);
        int totalPlayers = networkManager.playerModule != null ? networkManager.playerModule.players.Count : 1;
        Debug.Log(
            $"[{nameof(NetworkSceneCoordinator)}] Player {playerId} reported ready. ({_readyClients.Count}/{totalPlayers})");

        if (_readyClients.Count >= totalPlayers)
        {
            _isTransitioning = false;
            Debug.Log($"[{nameof(NetworkSceneCoordinator)}] All connected players ready! Triggering spawn flow.");
            OnAllClientsReady?.Invoke();
        }
    }
}
}