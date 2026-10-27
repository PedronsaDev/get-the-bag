using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Logging;
using PurrNet;
using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Networking.Scenes
{
    [DisallowMultipleComponent]
    public class NetworkSceneCoordinator : NetworkBehaviour
    {
        public static NetworkSceneCoordinator Instance { get; private set; }
        public static event Action<Scene> OnAllClientsLoadedScene;
        public event Action<string> OnSceneTransitionStarted;
        public event Action<float> OnLocalLoadProgress;
        public event Action OnAllClientsReady;
        public event Action<string> OnTransitionFailed;

        [SerializeField, Min(5f)] private float _loadTimeoutSeconds = 45f;
        private readonly HashSet<PlayerID> _expectedClients = new();
        private readonly HashSet<PlayerID> _readyClients = new();
        private string _targetScene;
        private int _generation;
        private bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            OnAllClientsLoadedScene = null;
        }

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public void TransitionToScene(string sceneName) => TransitionSafelyAsync(sceneName).Forget();

        private async UniTaskVoid TransitionSafelyAsync(string sceneName)
        {
            try { await TransitionToSceneAsync(sceneName); }
            catch (OperationCanceledException) { }
            catch (Exception error) { AppLog.LogException(LogChannel.Network, error, this); }
        }

        public async UniTask TransitionToSceneAsync(string sceneName)
        {
            var manager = networkManager ? networkManager : NetworkManager.main;
            bool online = manager && !manager.isOffline;
            if (online && !manager.isServer) throw new InvalidOperationException("Only the host can change scenes.");
            if (_isTransitioning) throw new InvalidOperationException("A scene transition is already running.");
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                throw new ArgumentException($"Scene '{sceneName}' is not enabled in Build Settings.", nameof(sceneName));

            var cancellation = this.GetCancellationTokenOnDestroy();
            int generation = ++_generation;
            _targetScene = sceneName;
            _expectedClients.Clear();
            _readyClients.Clear();
            _isTransitioning = true;
            OnSceneTransitionStarted?.Invoke(sceneName);
            try
            {
                AsyncOperation operation;
                if (online)
                {
                    foreach (var player in manager.playerModule.players) _expectedClients.Add(player);
                    manager.onPlayerLoadedScene += HandlePlayerLoadedScene;
                    manager.onPlayerLeft += HandlePlayerLeft;
                    RpcTransitionStarted(sceneName, generation);
                    operation = manager.sceneModule.LoadSceneAsync(sceneName, new PurrSceneSettings
                    {
                        mode = LoadSceneMode.Single, isPublic = true, physicsMode = LocalPhysicsMode.None
                    });
                }
                else operation = SceneManager.LoadSceneAsync(sceneName);
                if (operation == null) throw new InvalidOperationException($"Could not start loading '{sceneName}'.");

                float deadline = Time.realtimeSinceStartup + _loadTimeoutSeconds;
                while (!operation.isDone || (online && !_expectedClients.IsSubsetOf(_readyClients)))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (online && (!manager || !manager.isServer)) throw new OperationCanceledException("The host stopped.");
                    if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException($"Timed out loading '{sceneName}' for the party.");
                    OnLocalLoadProgress?.Invoke(Mathf.Clamp01(operation.progress / 0.9f));
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                }
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellation);
                if (online) RpcTransitionFinished(generation, null);
                OnLocalLoadProgress?.Invoke(1f);
                OnAllClientsLoadedScene?.Invoke(SceneManager.GetActiveScene());
                OnAllClientsReady?.Invoke();
                AppLog.Log(LogChannel.Network, $"Party loaded '{sceneName}' ({_readyClients.Count} peers).", this);
            }
            catch (Exception error)
            {
                if (online && manager && manager.isServer && isSpawned) RpcTransitionFinished(generation, error.Message);
                OnTransitionFailed?.Invoke(error.Message);
                throw;
            }
            finally
            {
                Unsubscribe();
                _isTransitioning = false;
            }
        }

        private void HandlePlayerLoadedScene(PlayerID player, SceneID sceneId, bool asServer)
        {
            if (!asServer || !_isTransitioning || !_expectedClients.Contains(player)) return;
            var manager = networkManager ? networkManager : NetworkManager.main;
            if (!manager || !manager.TryGetScene(sceneId, out var scene) || scene.name != _targetScene) return;
            _readyClients.Add(player);
        }

        private void HandlePlayerLeft(PlayerID player, bool asServer)
        {
            if (asServer) _expectedClients.Remove(player);
        }

        private void Unsubscribe()
        {
            var manager = networkManager ? networkManager : NetworkManager.main;
            if (!manager) return;
            manager.onPlayerLoadedScene -= HandlePlayerLoadedScene;
            manager.onPlayerLeft -= HandlePlayerLeft;
        }

        [ObserversRpc(runLocally: false)]
        private void RpcTransitionStarted(string sceneName, int generation)
        {
            _generation = generation;
            _isTransitioning = true;
            _targetScene = sceneName;
            OnSceneTransitionStarted?.Invoke(sceneName);
        }

        [ObserversRpc(runLocally: false)]
        private void RpcTransitionFinished(int generation, string error)
        {
            if (generation != _generation) return;
            _isTransitioning = false;
            if (error != null) OnTransitionFailed?.Invoke(error);
            else { OnLocalLoadProgress?.Invoke(1f); OnAllClientsReady?.Invoke(); }
        }
    }
}
