using PurrNet;
using PurrNet.Transports;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

namespace Project.Networking.Bootstrapping
{
    public enum QuickPlayMode
    {
        AutoDetect, // Main editor hosts; clones/virtual players connect as client
        AlwaysHost,
        AlwaysClient,
        ManualOnly  // Only connect via UI buttons or script calls
    }

    /// <summary>
    /// Development bootstrapper allowing instant play in any gameplay or sandbox scene.
    /// Eliminates the need to navigate through menus/lobbies during day-to-day iteration.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class QuickPlayBootstrapper : MonoBehaviour
    {
        [Header("Quick Play Settings")]
        [Tooltip("How this instance should automatically connect when entering play mode.")]
        [SerializeField] private QuickPlayMode _mode = QuickPlayMode.AutoDetect;

        [Tooltip("Target loopback IP address for local UDP connection.")]
        [SerializeField] private string _serverAddress = "127.0.0.1";

        [Tooltip("Target UDP port.")]
        [SerializeField] private ushort _serverPort = 5000;

        [Header("Debug Overlay")]
        [SerializeField] private bool _showDebugGui = true;
        
        public bool ShowDebugGui
        {
            get => _showDebugGui;
            set => _showDebugGui = value;
        }

        private NetworkManager _networkManager;

        private void Awake()
        {
            _networkManager = NetworkManager.main ? NetworkManager.main : FindAnyObjectByType<NetworkManager>();
        }

        private void Start()
        {
            if (!_networkManager)
            {
                Debug.LogWarning($"[{nameof(QuickPlayBootstrapper)}] No NetworkManager found in scene! Quick-Play is standing by.", this);
                return;
            }

            // If already connected (e.g. entered scene via LobbyManager), do nothing
            if (!_networkManager.isOffline)
            {
                return;
            }

            ConfigureUdpTransport();
            ExecuteAutoStart();
        }

        private void ConfigureUdpTransport()
        {
            if (_networkManager.transport is UDPTransport udp)
            {
                udp.address = _serverAddress;
                udp.serverPort = _serverPort;
            }
        }

        private void ExecuteAutoStart()
        {
            switch (_mode)
            {
                case QuickPlayMode.AlwaysHost:
                    StartHost();
                    break;

                case QuickPlayMode.AlwaysClient:
                    StartClient();
                    break;

                case QuickPlayMode.AutoDetect:
                    if (IsCloneOrVirtualInstance())
                    {
                        Debug.Log($"[{nameof(QuickPlayBootstrapper)}] Detected Virtual/Clone Editor instance -> Starting as Client.");
                        StartClient();
                    }
                    else
                    {
                        Debug.Log($"[{nameof(QuickPlayBootstrapper)}] Detected Main Editor instance -> Starting as Host (Server + Client).");
                        StartHost();
                    }
                    break;

                case QuickPlayMode.ManualOnly:
                    break;
            }
        }

        public void StartHost()
        {
            if (!_networkManager || !_networkManager.isOffline) return;
            ConfigureUdpTransport();
            _networkManager.StartHost();
        }

        public void StartClient()
        {
            if (!_networkManager || !_networkManager.isOffline) return;
            ConfigureUdpTransport();
            _networkManager.StartClient();
        }

        public void Disconnect()
        {
            if (!_networkManager || _networkManager.isOffline) return;
            if (_networkManager.isServer) _networkManager.StopServer();
            if (_networkManager.isClient) _networkManager.StopClient();
        }

        private bool IsCloneOrVirtualInstance()
        {
            var host = CurrentPlayer.IsMainEditor;
            return !host;   
        }

        private void OnGUI()
        {
            if (!_showDebugGui || !_networkManager) return;

            const int width = 230;
            const int height = 140;
            Rect rect = new Rect(Screen.width - width - 10, 10, width, height);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("<b>[Quick-Play Network]</b>");

            string statusText = "Status: " + (_networkManager.isOffline ? "<color=yellow>Offline</color>" :
                _networkManager.isServer && _networkManager.isClient ? "<color=green>Host (Server+Client)</color>" :
                _networkManager.isServer ? "<color=green>Dedicated Server</color>" :
                "<color=cyan>Client Connected</color>");

            GUILayout.Label(statusText, new GUIStyle(GUI.skin.label) { richText = true });

            if (_networkManager.isOffline)
            {
                if (GUILayout.Button("Start Host (UDP)"))
                {
                    StartHost();
                }

                if (GUILayout.Button($"Join ({_serverAddress}:{_serverPort})"))
                {
                    StartClient();
                }
            }
            else
            {
                if (GUILayout.Button("Disconnect"))
                {
                    Disconnect();
                }
            }

            GUILayout.EndArea();
        }
    }
}
