using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using PurrNet;
using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Logging;

namespace Project.Networking.Scenes
{
    /// <summary>
    /// Host-driven synchronized scene changes.
    /// Online: waits until every player who was connected when the load began has loaded the
    /// requested scene (not just any scene), drops players who disconnect, and gives up after a timeout.
    /// <see cref="TransitionToSceneAsync"/> returns true only when everyone made it.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkSceneCoordinator : NetworkBehaviour
    {
        public static NetworkSceneCoordinator Instance { get; private set; }

        public static event Action<Scene> OnAllClientsLoadedScene;

        /// <summary>Raised on every peer when a level load begins (host immediately, clients via RPC).</summary>
        public static event Action OnLoadingScreenRequested;
        /// <summary>Raised on every peer once its own scene load has finished (or failed).</summary>
        public static event Action OnLoadingScreenReleased;

        public event Action<string> OnSceneTransitionStarted;
        public event Action<float> OnLocalLoadProgress;
        public event Action OnAllClientsReady;

        [Tooltip("Host gives up waiting for the other players to load the scene after this long.")]
        [SerializeField, Min(5f)] private float _loadTimeoutSeconds = 60f;

        // The roster is frozen when the load starts, so a mid-load join cannot stall or satisfy it.
        private readonly HashSet<PlayerID> _expectedClients = new();
        private readonly HashSet<PlayerID> _readyClients = new();
        private UniTaskCompletionSource<bool> _pending;
        private string _targetSceneName;
        private bool _isTransitioning;
        private bool _completing;
        public bool IsTransitioning => _isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake() => RegisterInstance();

        protected override void OnSpawned()
        {
            base.OnSpawned();
            RegisterInstance();
        }

        private void RegisterInstance()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeServerEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void TransitionToScene(string sceneName)
        {
            TransitionToSceneAsync(sceneName).Forget();
        }

        /// <summary>
        /// Loads the scene for everyone. Returns true when all expected players loaded it,
        /// false when the request was rejected, the load could not start, or the wait timed out.
        /// </summary>
        public async UniTask<bool> TransitionToSceneAsync(string sceneName)
        {
            bool isOffline = !networkManager || networkManager.isOffline;
            if (!isServer && !isOffline)
            {
                AppLog.LogWarning(LogChannel.Network, "Only the Host can initiate scene transitions!", this);
                return false;
            }

            if (_isTransitioning)
            {
                AppLog.LogWarning(LogChannel.Network, "Scene transition already in progress.", this);
                return false;
            }

            _targetSceneName = Path.GetFileNameWithoutExtension(sceneName);
            var pending = new UniTaskCompletionSource<bool>();
            _pending = pending;

            if (isOffline)
            {
                _isTransitioning = true;
                OnSceneTransitionStarted?.Invoke(sceneName);
                OnLoadingScreenRequested?.Invoke();
                StartCoroutine(LoadSceneAsyncRoutineOffline(sceneName, pending));
            }
            else if (!BeginOnlineTransition(sceneName, pending))
            {
                _pending = null;
                return false;
            }

            var (timedOut, success) = await pending.Task.TimeoutWithoutException(TimeSpan.FromSeconds(_loadTimeoutSeconds));
            if (timedOut)
            {
                AppLog.LogError(LogChannel.Network,
                    $"Timed out loading '{_targetSceneName}'. Still waiting for {DescribeMissing()}.", this);
                AbortTransition();
                return false;
            }

            _pending = null;
            return success;
        }

        private bool BeginOnlineTransition(string sceneName, UniTaskCompletionSource<bool> pending)
        {
            var nm = networkManager ? networkManager : NetworkManager.main;

            _isTransitioning = true;
            _completing = false;
            _readyClients.Clear();
            _expectedClients.Clear();
            if (nm && nm.playerModule != null)
            {
                foreach (var p in nm.playerModule.players)
                    _expectedClients.Add(p);
            }

            AppLog.Log(LogChannel.Network,
                $"Host initiating PurrNet transition to '{sceneName}' for {_expectedClients.Count} player(s).", this);
            OnSceneTransitionStarted?.Invoke(sceneName);
            OnLoadingScreenRequested?.Invoke();

            RpcInstructTransitionStarted(sceneName);
            SubscribeServerEvents(nm);

            var asyncOp = nm.sceneModule.LoadSceneAsync(sceneName, new PurrSceneSettings
            {
                mode = LoadSceneMode.Single,
                isPublic = true,
                physicsMode = LocalPhysicsMode.None
            });

            if (asyncOp == null)
            {
                AppLog.LogError(LogChannel.Network, $"Failed to initiate PurrNet scene load for {sceneName}!", this);
                UnsubscribeServerEvents();
                _isTransitioning = false;
                OnLoadingScreenReleased?.Invoke();
                pending.TrySetResult(false);
                return false;
            }

            StartCoroutine(TrackLocalLoadProgress(asyncOp));
            return true;
        }

        private void AbortTransition()
        {
            UnsubscribeServerEvents();
            _isTransitioning = false;
            _completing = false;
            _pending = null;
            OnLoadingScreenReleased?.Invoke();
        }

        private string DescribeMissing()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in _expectedClients)
            {
                if (_readyClients.Contains(p)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(p);
            }
            return sb.Length == 0 ? "nobody" : sb.ToString();
        }

        [ObserversRpc(runLocally: false)]
        private void RpcInstructTransitionStarted(string sceneName)
        {
            AppLog.Log(LogChannel.Network, $"Received scene transition notice from Host: {sceneName}", this);
            _isTransitioning = true;
            OnSceneTransitionStarted?.Invoke(sceneName);
            OnLoadingScreenRequested?.Invoke();
            StartCoroutine(TrackClientLoadProgress(Path.GetFileNameWithoutExtension(sceneName)));
        }

        private IEnumerator TrackLocalLoadProgress(AsyncOperation asyncOp)
        {
            while (!asyncOp.isDone)
            {
                OnLocalLoadProgress?.Invoke(asyncOp.progress);
                yield return null;
            }

            OnLocalLoadProgress?.Invoke(1f);
            OnLoadingScreenReleased?.Invoke();
            AppLog.Log(LogChannel.Network, "Local scene load completed via PurrNet.", this);
        }

        private IEnumerator TrackClientLoadProgress(string targetSceneName)
        {
            var nm = networkManager ? networkManager : NetworkManager.main;
            float deadline = Time.unscaledTime + _loadTimeoutSeconds + 5f;

            while (SceneManager.GetActiveScene().name != targetSceneName)
            {
                if (Time.unscaledTime > deadline)
                {
                    // The host will abort too; do not leave this peer stuck behind the overlay.
                    AppLog.LogError(LogChannel.Network, $"Gave up waiting to load '{targetSceneName}'.", this);
                    _isTransitioning = false;
                    OnLoadingScreenReleased?.Invoke();
                    yield break;
                }

                if (nm && nm.sceneModule != null)
                {
                    var ops = nm.sceneModule.GetPendingOperations();
                    if (ops.Count > 0 && ops[0].operation != null)
                    {
                        OnLocalLoadProgress?.Invoke(ops[0].operation.progress);
                    }
                }
                yield return null;
            }

            OnLocalLoadProgress?.Invoke(1f);
            OnLoadingScreenReleased?.Invoke();
            _isTransitioning = false;
        }

        private void SubscribeServerEvents(NetworkManager nm)
        {
            if (!nm) return;
            if (nm.TryGetModule(out ScenePlayersModule scenePlayers, true))
            {
                scenePlayers.onPlayerLoadedScene -= HandlePlayerLoadedScene;
                scenePlayers.onPlayerLoadedScene += HandlePlayerLoadedScene;
            }

            if (nm.playerModule != null)
            {
                nm.playerModule.onPlayerLeft -= HandlePlayerLeft;
                nm.playerModule.onPlayerLeft += HandlePlayerLeft;
            }
        }

        private void UnsubscribeServerEvents()
        {
            var nm = networkManager ? networkManager : NetworkManager.main;
            if (!nm) return;

            if (nm.TryGetModule(out ScenePlayersModule scenePlayers, true))
                scenePlayers.onPlayerLoadedScene -= HandlePlayerLoadedScene;

            if (nm.playerModule != null)
                nm.playerModule.onPlayerLeft -= HandlePlayerLeft;
        }

        private void HandlePlayerLoadedScene(PlayerID player, SceneID scene, bool asServer)
        {
            if (!isServer || !asServer || !_isTransitioning) return;

            // A callback for the old scene (or any other scene) must not count as "loaded the new one".
            var nm = networkManager ? networkManager : NetworkManager.main;
            if (!nm || !nm.sceneModule.TryGetSceneState(scene, out var state) || state.scene.name != _targetSceneName)
                return;

            _readyClients.Add(player);
            AppLog.Log(LogChannel.Network,
                $"Player {player} loaded '{_targetSceneName}'. ({_readyClients.Count}/{_expectedClients.Count})", this);

            TryComplete();
        }

        private void HandlePlayerLeft(PlayerID player, bool asServer)
        {
            if (!asServer || !_isTransitioning) return;

            _expectedClients.Remove(player);
            _readyClients.Remove(player);
            AppLog.LogWarning(LogChannel.Network, $"Player {player} left during the scene load; no longer waiting for them.", this);

            TryComplete();
        }

        private void TryComplete()
        {
            if (_completing || _expectedClients.Count == 0) return;

            foreach (var p in _expectedClients)
                if (!_readyClients.Contains(p)) return;

            _completing = true;
            UnsubscribeServerEvents();
            StartCoroutine(CompleteTransitionRoutine());
        }

        private IEnumerator CompleteTransitionRoutine()
        {
            yield return null;

            _isTransitioning = false;
            Scene activeScene = SceneManager.GetActiveScene();
            AppLog.Log(LogChannel.Network, $"All players loaded scene '{activeScene.name}'. Transition complete.", this);

            OnAllClientsLoadedScene?.Invoke(activeScene);
            OnAllClientsReady?.Invoke();
            _pending?.TrySetResult(true);
        }

        private IEnumerator LoadSceneAsyncRoutineOffline(string sceneName, UniTaskCompletionSource<bool> pending)
        {
            var asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                AppLog.LogError(LogChannel.Network, $"Failed to load scene offline: {sceneName}!", this);
                _isTransitioning = false;
                OnLoadingScreenReleased?.Invoke();
                pending.TrySetResult(false);
                yield break;
            }

            while (!asyncOp.isDone)
            {
                OnLocalLoadProgress?.Invoke(asyncOp.progress);
                yield return null;
            }

            OnLocalLoadProgress?.Invoke(1f);
            yield return null;

            _isTransitioning = false;
            OnLoadingScreenReleased?.Invoke();
            Scene activeScene = SceneManager.GetActiveScene();
            OnAllClientsLoadedScene?.Invoke(activeScene);
            OnAllClientsReady?.Invoke();
            pending.TrySetResult(true);
        }
    }
}
