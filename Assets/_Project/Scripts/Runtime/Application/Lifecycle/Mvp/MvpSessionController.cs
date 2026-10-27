using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Project.Combat.Health;
using Project.Content;
using Project.Core.Match;
using Project.Core.Player;
using Project.Logging;
using Project.Loot.Data;
using Project.Network.Bootstrapping;
using Project.Networking.Scenes;
using Project.Player.Loot;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Game.Mvp
{
    public enum MvpPhase : byte { Offline, Hub, Loading, Raid, Results, Error }

    public struct MvpPlayerResult : IPackedAuto
    {
        public PlayerID Player;
        public bool Extracted;
        public int LootValue;
        public int BankedValue;
        public string Loot;
    }

    public struct MvpRunSummary : IPackedAuto
    {
        public int RunId;
        public string Outcome;
        public MvpPlayerResult[] Players;
    }

    public sealed class MvpSessionController : NetworkBehaviour
    {
        public const string HubScene = "Scene_Hub";
        public const string BankScene = "Scene_Bank";
        public static MvpSessionController Instance { get; private set; }

        private readonly SyncVar<MvpPhase> _phase = new(MvpPhase.Offline);
        private readonly SyncVar<MvpRunSummary> _summary = new();
        private readonly Dictionary<PlayerID, List<DuffleBagItemEntry>> _bank = new();
        private readonly Dictionary<PlayerID, int> _bankedValues = new();
        private readonly HashSet<PlayerID> _participants = new();
        private MvpRaidSetup _setup;
        private RaidManager _raid;
        private bool _busy;
        private bool _connecting;
        private Scene _initializedScene;
        private int _runId;
        private int _resolvedRun;

        public MvpPhase Phase => isSpawned ? _phase.value : MvpPhase.Offline;
        public MvpRunSummary Summary => _summary.value;
        public bool IsBusy => _busy;
        public string LastError { get; private set; }
        public int PartySize => NetworkManager.main && NetworkManager.main.playerModule != null
            ? NetworkManager.main.playerModule.players.Count : 0;

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.runInBackground = true;
        }

        protected override void OnSpawned()
        {
            _initializedScene = default;
            LastError = null;
            if (!isServer) return;
            _bank.Clear();
            _bankedValues.Clear();
            _participants.Clear();
            _runId = _resolvedRun = 0;
            _summary.value = default;
            _phase.value = MvpPhase.Loading;
            networkManager.onPlayerJoined += HandlePlayerJoined;
        }

        protected override void OnDespawned()
        {
            if (networkManager) networkManager.onPlayerJoined -= HandlePlayerJoined;
            if (_raid) _raid.OnResolvedServer -= HandleResolvedServer;
            _raid = null;
            _setup = null;
            _bank.Clear();
            _bankedValues.Clear();
            _participants.Clear();
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void Update()
        {
            if (!isServer || _busy || Phase == MvpPhase.Error) return;
            var scene = SceneManager.GetActiveScene();
            if (scene != _initializedScene && (scene.name == BankScene || scene.name == HubScene))
                InitializeSceneSafelyAsync().Forget();
        }

        public void HostLocal() => ConnectLocalAsync(true, "127.0.0.1").Forget();
        public void JoinLocal(string address) => ConnectLocalAsync(false, address).Forget();

        private async UniTaskVoid ConnectLocalAsync(bool host, string address)
        {
            if (_busy || (NetworkManager.main && !NetworkManager.main.isOffline)) return;
            _busy = _connecting = true;
            LastError = null;
            try
            {
                await NetworkStartupGate.WaitAsync();
                var manager = NetworkManager.main;
                if (!manager || !manager.TryGetComponent<UDPTransport>(out var transport))
                    throw new InvalidOperationException("Core services need a UDP transport.");
                manager.transport = transport;
                transport.address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
                transport.serverPort = 5000;
                if (host) manager.StartHost(); else manager.StartClient();
                float deadline = Time.realtimeSinceStartup + 20f;
                while (manager.clientState != ConnectionState.Connected || (host && !manager.isServer))
                {
                    if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Could not connect. Check the host address and UDP port 5000.");
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                }
                if (host)
                {
                    await NetworkSceneCoordinator.Instance.TransitionToSceneAsync(HubScene);
                    await InitializeSceneServerAsync();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                LastError = error.Message;
                AppLog.LogException(LogChannel.Network, error, this);
                Disconnect();
            }
            finally { _busy = _connecting = false; }
        }

        public void DeployToBank()
        {
            if (isServer && Phase == MvpPhase.Hub && !_busy) TravelAsync(BankScene).Forget();
        }

        public void ReturnToHub()
        {
            if (isServer && !_busy && Phase is MvpPhase.Results or MvpPhase.Error)
                TravelAsync(HubScene).Forget();
        }

        public void Disconnect()
        {
            var manager = NetworkManager.main;
            if (!manager) return;
            if (manager.isClient) manager.StopClient();
            if (manager.isServer) manager.StopServer();
        }

        private async UniTaskVoid TravelAsync(string sceneName)
        {
            _busy = true;
            _phase.value = MvpPhase.Loading;
            LastError = null;
            SetPlayerGameplay(false);
            try
            {
                await NetworkSceneCoordinator.Instance.TransitionToSceneAsync(sceneName);
                await InitializeSceneServerAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { FailOperation(error); }
            finally { _busy = false; }
        }

        private async UniTaskVoid InitializeSceneSafelyAsync()
        {
            _busy = true;
            try { await InitializeSceneServerAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception error) { FailOperation(error); }
            finally { _busy = false; }
        }

        private async UniTask InitializeSceneServerAsync()
        {
            var scene = SceneManager.GetActiveScene();
            _phase.value = MvpPhase.Loading;
            if (_raid) _raid.OnResolvedServer -= HandleResolvedServer;
            _raid = null;
            _setup = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!AllPawnsReady(scene))
            {
                if (!isServer) throw new OperationCanceledException("Session stopped during player initialization.");
                if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Timed out waiting for player pawns.");
                await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            }
            _initializedScene = scene;
            if (scene.name == HubScene)
            {
                SetPlayerGameplay(true);
                if (TrainTransitController.Instance) TrainTransitController.Instance.ResetAtHubServer();
                _phase.value = MvpPhase.Hub;
                return;
            }
            if (scene.name != BankScene) return;
            _setup = FindAnyObjectByType<MvpRaidSetup>();
            if (!_setup || !_setup.Raid) throw new InvalidOperationException("Scene_Bank is missing its MVP raid setup.");
            _raid = _setup.Raid;
            _raid.OnResolvedServer += HandleResolvedServer;
            _participants.Clear();
            _runId++;
            foreach (var player in PlayerIdentifier.ActivePlayers)
            {
                if (!player || player.gameObject.scene != scene || !player.PlayerID.HasValue) continue;
                _participants.Add(player.PlayerID.Value);
                player.SetGameplayEnabledServer(false);
                if (player.TryGetComponent<PlayerDuffleBagController>(out var bag)) bag.GrantEmptyBag();
                else throw new InvalidOperationException("A raid pawn has no duffle bag controller.");
            }
            await _setup.InitializeServerAsync(this.GetCancellationTokenOnDestroy());
            SetPlayerGameplay(true);
            _phase.value = MvpPhase.Raid;
            _raid.BeginRaidServer();
            AppLog.Log(LogChannel.Match, $"Bank run {_runId} started with {_participants.Count} players.", this);
        }

        private bool AllPawnsReady(Scene scene)
        {
            var players = networkManager.playerModule.players;
            if (players.Count == 0) return false;
            for (int i = 0; i < players.Count; i++)
            {
                int count = 0;
                foreach (var pawn in PlayerIdentifier.ActivePlayers)
                    if (pawn && pawn.isSpawned && pawn.gameObject.scene == scene && pawn.PlayerID == players[i]) count++;
                if (count != 1) return false;
            }
            return true;
        }

        private void SetPlayerGameplay(bool enabled)
        {
            foreach (var player in PlayerIdentifier.ActivePlayers)
            {
                if (!player) continue;
                player.SetGameplayEnabledServer(enabled);
                if (player.TryGetComponent<HealthComponent>(out var health)) health.SetInvulnerable(!enabled);
            }
        }

        private void HandleResolvedServer(RaidState state)
        {
            if (!isServer || _resolvedRun == _runId || _runId == 0) return;
            _resolvedRun = _runId;
            SetPlayerGameplay(false);
            if (_setup) _setup.StopCombatServer();
            var results = new List<MvpPlayerResult>(_participants.Count);
            foreach (var id in _participants)
            {
                bool extracted = state == RaidState.Completed && ContainsExtracted(id) && IsConnected(id);
                DuffleBagSnapshot snapshot = default;
                if (PlayerIdentifier.TryGetPlayer(id, out var pawn) && pawn.TryGetComponent<PlayerDuffleBagController>(out var bag))
                    snapshot = bag.ConsumeBagServer();
                int value = 0;
                var description = new StringBuilder();
                if (snapshot.Items != null)
                    foreach (var item in snapshot.Items)
                    {
                        if (item.Quantity <= 0 || !GameContent.LootRegistry.TryGetById(item.ItemId, out var definition)) continue;
                        value = (int)Math.Min(int.MaxValue, (long)value + (long)Math.Max(0, definition.BaseValue) * item.Quantity);
                        if (description.Length > 0) description.Append(", ");
                        description.Append(item.Quantity).Append("x ").Append(definition.Name);
                        if (extracted) BankItem(id, item);
                    }
                _bankedValues.TryGetValue(id, out int previousValue);
                int total = extracted ? (int)Math.Min(int.MaxValue, (long)previousValue + value) : previousValue;
                _bankedValues[id] = total;
                results.Add(new MvpPlayerResult { Player = id, Extracted = extracted, LootValue = value,
                    BankedValue = total, Loot = description.Length > 0 ? description.ToString() : "Empty bag" });
            }
            _summary.value = new MvpRunSummary { RunId = _runId,
                Outcome = state == RaidState.Completed ? "Extraction complete" : "Raid failed", Players = results.ToArray() };
            _phase.value = MvpPhase.Results;
            AppLog.Log(LogChannel.Match, $"Bank run {_runId} resolved once: {state}.", this);
        }

        private bool ContainsExtracted(PlayerID id)
        {
            foreach (var extracted in _raid.ExtractedPlayers) if (id == extracted) return true;
            return false;
        }

        private bool IsConnected(PlayerID id)
        {
            foreach (var connected in networkManager.playerModule.players) if (id == connected) return true;
            return false;
        }

        private void BankItem(PlayerID player, DuffleBagItemEntry item)
        {
            if (!_bank.TryGetValue(player, out var items)) _bank[player] = items = new List<DuffleBagItemEntry>();
            for (int i = 0; i < items.Count; i++)
                if (items[i].ItemId == item.ItemId)
                {
                    var entry = items[i];
                    entry.Quantity = (int)Math.Min(int.MaxValue, (long)entry.Quantity + item.Quantity);
                    items[i] = entry;
                    return;
                }
            items.Add(item);
        }

        public IReadOnlyList<DuffleBagItemEntry> GetBankedLootServer(PlayerID player) =>
            isServer && _bank.TryGetValue(player, out var items) ? items.AsReadOnly() : Array.Empty<DuffleBagItemEntry>();

        private void HandlePlayerJoined(PlayerID player, bool reconnect, bool asServer)
        {
            if (!asServer || player == networkManager.localPlayer) return;
            if (PartySize <= 4 && (Phase == MvpPhase.Hub || _connecting)) return;
            if (networkManager.playerModule.TryGetConnection(player, out var connection))
                networkManager.rawTransport.CloseConnection(connection);
        }

        private void FailOperation(Exception error)
        {
            if (_setup) _setup.StopCombatServer();
            _phase.value = MvpPhase.Error;
            RpcOperationError(error.Message);
            AppLog.LogException(LogChannel.Match, error, this);
        }

        [ObserversRpc(runLocally: true)]
        private void RpcOperationError(string error) => LastError = error;
    }
}
