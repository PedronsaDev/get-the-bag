using System;
using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Bootstrap
{
    public static class SceneBootstrapper
    {
        private const string CoreServicesPrefabPath = "Core/[CORE_SERVICES]";
#if UNITY_EDITOR
        public static bool AutoBootstrapEnabled => UnityEditor.EditorPrefs.GetBool("AutoBootstrap_Enabled", true);
#else
        public static bool AutoBootstrapEnabled => false;
#endif
        public static bool IsDirectPlayScene => Application.isEditor &&
            SceneManager.GetActiveScene().name != "Scene_Lobby";

        public static GameObject EnsureCoreServices()
        {
            var existing = NetworkManager.main ? NetworkManager.main : UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
            if (existing)
            {
                UnityEngine.Object.DontDestroyOnLoad(existing.transform.root.gameObject);
                return existing.transform.root.gameObject;
            }

            var prefab = Resources.Load<GameObject>(CoreServicesPrefabPath);
            if (!prefab)
                throw new InvalidOperationException($"Missing Resources/{CoreServicesPrefabPath}.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "[CORE_SERVICES_RUNTIME]";
            UnityEngine.Object.DontDestroyOnLoad(instance);
            return instance;
        }
    }
}
