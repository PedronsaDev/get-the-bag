using PedronsaDev.Console;
using Project.Networking.Scenes;
using PurrNet;
using UnityEngine;

namespace Project.Networking.Commands
{
    [CommandGroup("Network")]
    public static class NetworkCommands
    {
        // -------------------------------------------------------------
        // PUBLIC DIAGNOSTIC COMMANDS (Safe for release, all players)
        // -------------------------------------------------------------

        [Command("net_status", "Displays current network session status, latency, and role")]
        public static void PrintNetworkStatus()
        {
            var net = NetworkManager.main;
            if (!net || net.isOffline)
            {
                Console.LogWarning("[Network] Currently Offline.");
                return;
            }

            string role = net.isServer && net.isClient ? "Host (Server + Client)" :
                          net.isServer ? "Dedicated Server" : "Client";

            string transport = net.transport ? net.transport.GetType().Name : "None";
            int playerCount = net.playerModule != null ? net.playerModule.players.Count : 0;

            Console.Log($"[Network Status]\nRole: {role}\nTransport: {transport}\nPlayers: {playerCount}\nTick Rate: {net.tickRate} Hz");
        }

        [Command("net_players", "Lists all connected player IDs in the current session")]
        public static void ListPlayers()
        {
            var net = NetworkManager.main;
            if (!net || net.isOffline)
            {
                Console.LogWarning("[Network] Not connected to any session.");
                return;
            }

            var players = net.playerModule?.players;
            int count = players?.Count ?? 0;
            Console.Log($"--- Connected Players ({count}) ---");

            if (players != null)
            {
                foreach (var player in players)
                {
                    bool isLocal = player == net.localPlayer;
                    Console.Log($" - Player ID: {player} {(isLocal ? "(You)" : "")}");
                }
            }
        }

        // -------------------------------------------------------------
        // HOST-AUTHORITATIVE / DEV COMMANDS (Restricted to Host)
        // -------------------------------------------------------------

        [Command("host_scene", "Loads a new scene across all connected players (Host only)")]
        public static void ChangeScene(string sceneName)
        {
            if (!ValidateHostAuthority("host_scene")) return;

            if (!NetworkSceneCoordinator.Instance)
            {
                Console.LogError("[host_scene] NetworkSceneCoordinator not found in scene!");
                return;
            }

            Console.Log($"[host_scene] Transitioning party to: {sceneName}");
            NetworkSceneCoordinator.Instance.TransitionToScene(sceneName);
        }

        [Command("host_kick", "Kicks a player from the session (Host only)")]
        public static void KickPlayer(int playerId)
        {
            if (!ValidateHostAuthority("host_kick")) return;

            var net = NetworkManager.main;
            var players = net.playerModule?.players;
            if (players == null)
            {
                PedronsaDev.Console.Console.LogWarning("[host_kick] Player module unavailable.");
                return;
            }

            PlayerID? target = null;
            foreach (var p in players)
            {
                if (p.id.value == (ulong)playerId)
                {
                    target = p;
                    break;
                }
            }

            if (!target.HasValue)
            {
                Console.LogWarning($"[host_kick] Player {playerId} not found.");
                return;
            }

            if (net.playerModule.TryGetConnection(target.Value, out var conn))
            {
                net.rawTransport.CloseConnection(conn);
                Console.Log($"[host_kick] Disconnected Player {playerId}.");
            }
            else
            {
                Console.LogWarning($"[host_kick] Could not find active transport connection for player {playerId}.");
            }
        }

        private static bool ValidateHostAuthority(string commandName)
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                Console.LogError($"[{commandName}] Command disabled in production builds.");
                return false;
            }

            var net = NetworkManager.main;
            if (!net || net.isOffline)
            {
                Console.LogWarning($"[{commandName}] Not connected to a session.");
                return false;
            }

            if (!net.isServer)
            {
                Console.LogError($"[{commandName}] Access Denied: Requires Host authority!");
                return false;
            }

            return true;
        }
    }
}
