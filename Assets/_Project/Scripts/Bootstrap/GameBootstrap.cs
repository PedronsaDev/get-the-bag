using System;
using Cysharp.Threading.Tasks;
using Project.Game;
using Project.Logging;
using Project.Network.Bootstrapping;
using Project.Networking.Bootstrapping;
using UnityEngine;

namespace Project.Bootstrap
{
public static class GameBootstrap
{
    private static UniTaskCompletionSource _ready;
    public static bool IsReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        _ready = new UniTaskCompletionSource();
        IsReady = false;
    }

    public static UniTask WaitUntilReadyAsync() => _ready.Task;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static async UniTaskVoid OnStartBootstrap()
    {
        NetworkStartupGate.Configure(WaitUntilReadyAsync,
            () => IsReady && GameLifetime.Instance && !GameLifetime.Instance.IsStopping && GameContent.IsLoaded);
        if (!GameLifetime.Instance)
            new GameObject("GameLifetime").AddComponent<GameLifetime>();
        try
        {
            if (!GameLifetime.Instance || GameLifetime.Instance.IsStopping)
                throw new OperationCanceledException("Startup interrupted by shutdown.");
            bool directPlay = SceneBootstrapper.IsDirectPlayScene;
            if (!directPlay || SceneBootstrapper.AutoBootstrapEnabled)
                SceneBootstrapper.EnsureCoreServices();

            GameLifetime.Instance.ConfigureSessionShutdown(StopSessionAsync);
            await UniTask.Yield();
            await GameContent.InitializeRegistriesAsync();
            if (!GameLifetime.Instance || GameLifetime.Instance.IsStopping)
                throw new OperationCanceledException("Startup interrupted by shutdown.");
            IsReady = true;
            _ready.TrySetResult();

            if (directPlay && SceneBootstrapper.AutoBootstrapEnabled)
            {
                var quickPlay = UnityEngine.Object.FindAnyObjectByType<QuickPlayBootstrapper>();
                if (quickPlay)
                    quickPlay.StartConfiguredMode();
                else if (PurrNet.NetworkManager.main &&
                         PurrNet.NetworkManager.main.TryGetComponent<ConnectionStarter>(out var starter))
                    starter.StartRequestedConnection();
            }
            else if (ConnectionStarter.HasLobbyConnection &&
                     UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Scene_Lobby" &&
                     PurrNet.NetworkManager.main &&
                     PurrNet.NetworkManager.main.TryGetComponent<ConnectionStarter>(out var starter))
                starter.StartRequestedConnection();

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        catch (OperationCanceledException)
        {
            _ready.TrySetCanceled();
        }
        catch (Exception exception)
        {
            IsReady = false;
            _ready.TrySetException(exception);
            AppLog.LogException(LogChannel.General, exception);
        }
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!IsReady || scene.name == "Scene_Lobby" || !ConnectionStarter.HasLobbyConnection)
            return;
        if (PurrNet.NetworkManager.main &&
            PurrNet.NetworkManager.main.TryGetComponent<ConnectionStarter>(out var starter))
            starter.StartRequestedConnection();
    }

    private static async UniTask StopSessionAsync()
    {
        IsReady = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        var manager = PurrNet.NetworkManager.main;
        if (!manager) return;
        if (manager.TryGetComponent<ConnectionStarter>(out var starter)) starter.StopPendingConnection();
        if (manager.isClient) manager.StopClient();
        if (manager.isServer) manager.StopServer();
        UnityEngine.Object.Destroy(manager.transform.root.gameObject);
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
    }
}
}