using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        private readonly HashSet<PlayerID> _readyClients = new();
        private bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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
            UnsubscribeScenePlayerEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Fires and forgets an asynchronous scene transition.
        /// </summary>
        public void TransitionToScene(string sceneName)
        {
            TransitionToSceneAsync(sceneName).Forget();
        }

        /// <summary>
        /// Host-only entry point to asynchronously transition all connected players to a new scene.
        /// Awaits until all clients have loaded and avatars are spawned.
        /// </summary>
        public async UniTask TransitionToSceneAsync(string sceneName)
        {
            bool isOffline = !networkManager || networkManager.isOffline;
            if (!isServer && !isOffline)
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
                if (isOffline)
                {
                    _isTransitioning = true;
                    OnSceneTransitionStarted?.Invoke(sceneName);
                    StartCoroutine(LoadSceneAsyncRoutineOffline(sceneName));
                }
                else
                {
                    TransitionToSceneOnline(sceneName, allReadyUcs);
                }

                await allReadyUcs.Task;
            }
            finally
            {
                OnAllClientsReady -= onReady;
            }
        }

        private void TransitionToSceneOnline(string sceneName, UniTaskCompletionSource ucs)
        {
            _isTransitioning = true;
            _readyClients.Clear();

            Debug.Log($"[{nameof(NetworkSceneCoordinator)}] Host initiating PurrNet transition to scene: {sceneName}");
            OnSceneTransitionStarted?.Invoke(sceneName);

            // Notify all clients to display transition screens/VFX
            RpcInstructTransitionStarted(sceneName);

            var nm = networkManager ? networkManager : NetworkManager.main;
            SubscribeScenePlayerEvents(nm);

            var asyncOp = nm.sceneModule.LoadSceneAsync(sceneName, new PurrSceneSettings
            {
                mode = LoadSceneMode.Single,
                isPublic = true,
                physicsMode = LocalPhysicsMode.None
            });

            if (asyncOp != null)
            {
                StartCoroutine(TrackLocalLoadProgress(asyncOp));
            }
            else
            {
                Debug.LogError($"[{nameof(NetworkSceneCoordinator)}] Failed to initiate PurrNet scene load for {sceneName}!");
                _isTransitioning = false;
                ucs.TrySetResult();
            }
        }

        [ObserversRpc(runLocally: false)]
        private void RpcInstructTransitionStarted(string sceneName)
        {
            Debug.Log($"[{nameof(NetworkSceneCoordinator)}] Received scene transition notice from Host: {sceneName}");
            _isTransitioning = true;
            OnSceneTransitionStarted?.Invoke(sceneName);
            StartCoroutine(TrackClientLoadProgress(sceneName));
        }

        private IEnumerator TrackLocalLoadProgress(AsyncOperation asyncOp)
        {
            while (!asyncOp.isDone)
            {
                OnLocalLoadProgress?.Invoke(asyncOp.progress);
                yield return null;
            }

            OnLocalLoadProgress?.Invoke(1f);
            Debug.Log($"[{nameof(NetworkSceneCoordinator)}] Local scene load completed via PurrNet.");
        }

        private IEnumerator TrackClientLoadProgress(string targetSceneName)
        {
            var nm = networkManager ? networkManager : NetworkManager.main;
            while (SceneManager.GetActiveScene().name != targetSceneName)
            {
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
            _isTransitioning = false;
        }

        private void SubscribeScenePlayerEvents(NetworkManager nm)
        {
            if (!nm) return;
            if (nm.TryGetModule(out ScenePlayersModule scenePlayers, true))
            {
                scenePlayers.onPlayerLoadedScene -= HandlePlayerLoadedScene;
                scenePlayers.onPlayerLoadedScene += HandlePlayerLoadedScene;
            }
        }

        private void UnsubscribeScenePlayerEvents()
        {
            var nm = networkManager ? networkManager : NetworkManager.main;
            if (nm && nm.TryGetModule(out ScenePlayersModule scenePlayers, true))
            {
                scenePlayers.onPlayerLoadedScene -= HandlePlayerLoadedScene;
            }
        }

        private void HandlePlayerLoadedScene(PlayerID player, SceneID scene, bool asServer)
        {
            if (!isServer) return;

            _readyClients.Add(player);

            var nm = networkManager ? networkManager : NetworkManager.main;
            int totalPlayers = (nm && nm.playerModule != null) ? nm.playerModule.players.Count : 1;

            Debug.Log($"[{nameof(NetworkSceneCoordinator)}] Player {player} loaded target scene. ({_readyClients.Count}/{totalPlayers})");

            if (_readyClients.Count >= totalPlayers)
            {
                UnsubscribeScenePlayerEvents();
                StartCoroutine(CompleteTransitionRoutine());
            }
        }

        private IEnumerator CompleteTransitionRoutine()
        {
            // Allow 1 frame for newly loaded scene objects to execute Awake/Start
            yield return null;

            _isTransitioning = false;
            Scene activeScene = SceneManager.GetActiveScene();
            Debug.Log($"[{nameof(NetworkSceneCoordinator)}] All players loaded scene '{activeScene.name}'. Transition complete.");

            OnAllClientsLoadedScene?.Invoke(activeScene);
            OnAllClientsReady?.Invoke();
        }

        private IEnumerator LoadSceneAsyncRoutineOffline(string sceneName)
        {
            var asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                Debug.LogError($"[{nameof(NetworkSceneCoordinator)}] Failed to load scene offline: {sceneName}!");
                _isTransitioning = false;
                OnAllClientsReady?.Invoke();
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
            Scene activeScene = SceneManager.GetActiveScene();
            OnAllClientsLoadedScene?.Invoke(activeScene);
            OnAllClientsReady?.Invoke();
        }
    }
}