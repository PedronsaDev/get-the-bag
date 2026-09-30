using UnityEngine;
using UnityEngine.SceneManagement;
using PurrNet;
using Project.Logging;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project.Core.Bootstrap
{
public static class SceneBootstrapper
{
    private const string BootstrapSceneName = "Scene_Bootstrap";
    private const string CoreServicesPrefabPath = "Core/[CORE_SERVICES]";

#if UNITY_EDITOR
    public static bool AutoBootstrapEnabled => UnityEditor.EditorPrefs.GetBool("AutoBootstrap_Enabled", true);
#else
        public static bool AutoBootstrapEnabled => false;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        if (!AutoBootstrapEnabled) return;

        if (NetworkManager.main) return;

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (currentSceneName == BootstrapSceneName) return;

        if (AppLog.IsChannelEnabled(LogChannel.General))
        {
            AppLog.Log(LogChannel.General,
                "SceneBootstrapper: Direct play detected in scene '{0}'. Initializing Core Services...",
                currentSceneName);
        }

        var corePrefab = Resources.Load<GameObject>(CoreServicesPrefabPath);
        if (corePrefab)
        {
            var coreInstance = Object.Instantiate(corePrefab);
            coreInstance.name = "[CORE_SERVICES_RUNTIME]";
            Object.DontDestroyOnLoad(coreInstance);
        }
        else
        {
            AppLog.LogWarning(LogChannel.General,
                "SceneBootstrapper: Could not find Resources/{0}. Loading {1} additively.", CoreServicesPrefabPath,
                BootstrapSceneName);
            SceneManager.LoadScene(BootstrapSceneName, LoadSceneMode.Additive);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        if (!AutoBootstrapEnabled) return;

        if (NetworkManager.main && !NetworkManager.main.isServer && !NetworkManager.main.isClient)
        {
            if (!NetworkManager.main.GetComponent<Project.Network.Bootstrapping.ConnectionStarter>())
            {
                if (AppLog.IsChannelEnabled(LogChannel.Network))
                {
                    AppLog.Log(LogChannel.Network, "SceneBootstrapper: Fallback auto-host triggered.");
                }

                NetworkManager.main.StartHost();
            }
        }
    }
}
}