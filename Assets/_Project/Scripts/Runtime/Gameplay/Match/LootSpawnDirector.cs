using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Content;
using Project.Logging;
using Project.Loot;
using Project.Loot.Data;
using Project.Loot.Spawning;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
/// <summary>
/// Rolls every <see cref="LootSpawnPoint"/> in the level once, on the server, and spawns the results.
/// One per level scene (the Level Handler is a good home).
/// </summary>
public class LootSpawnDirector : MonoBehaviour
{
    public enum SpawnTrigger
    {
        /// <summary>When the LevelReadyGate goes live (everyone loaded).</summary>
        LevelLive,
        /// <summary>As soon as the server starts the scene.</summary>
        ServerStart,
        /// <summary>Only when SpawnAll() is called.</summary>
        Manual
    }

    /// <summary>
    /// Server-side modifiers for the next level, on top of the level's own. Set this before loading a level
    /// (difficulty, contracts, heat...). Stays until changed.
    /// </summary>
    public static LootRollModifiers SessionModifiers = LootRollModifiers.Neutral;

    public static LootSpawnDirector Instance { get; private set; }

    [Tooltip("LevelLive: when everyone has loaded. ServerStart: as soon as the scene starts. Manual: only via SpawnAll().")]
    [SerializeField] private SpawnTrigger _spawnWhen = SpawnTrigger.LevelLive;

    [Tooltip("This level's own loot tuning. Combined with SessionModifiers.")]
    [SerializeField] private LootRollModifiers _levelModifiers = LootRollModifiers.Neutral;

    [Tooltip("0 = different loot every run. Any other value = the same layout every run (handy for testing).")]
    [SerializeField] private int _seed;

    [SerializeField] private bool _logSummary = true;

    private bool _spawned;
    private LevelReadyGate _gate;

    public LootRollModifiers EffectiveModifiers => _levelModifiers.Combine(SessionModifiers);
    public int Seed => _seed;
    public event Action<IReadOnlyList<PlannedLoot>> OnLootSpawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => SessionModifiers = LootRollModifiers.Neutral;

    private void Awake()
    {
        if (!Instance) Instance = this;
    }

    private void OnDestroy()
    {
        if (_gate) _gate.OnPhaseChanged -= HandleGatePhaseChanged;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (!IsServer) return;

        switch (_spawnWhen)
        {
            case SpawnTrigger.ServerStart:
                SpawnAll();
                break;

            case SpawnTrigger.LevelLive:
                _gate = LevelReadyGate.Instance;
                if (!_gate)
                {
                    AppLog.LogWarning(LogChannel.Match, "[LootSpawnDirector] No LevelReadyGate; spawning loot now.", this);
                    SpawnAll();
                }
                else if (_gate.IsLive) SpawnAll();
                else _gate.OnPhaseChanged += HandleGatePhaseChanged;
                break;
        }
    }

    private void HandleGatePhaseChanged(LevelPhase phase)
    {
        if (phase == LevelPhase.Live) SpawnAll();
    }

    /// <summary>Server only. Rolls and spawns the level's loot. Does nothing the second time.</summary>
    public void SpawnAll()
    {
        if (!IsServer || _spawned) return;
        _spawned = true;
        SpawnAllAsync(destroyCancellationToken).Forget();
    }

    public System.Random CreateRandom() => new(_seed != 0 ? _seed : Environment.TickCount);

    private async UniTaskVoid SpawnAllAsync(CancellationToken ct)
    {
        if (!GameContent.IsLoaded &&
            await UniTask.WaitUntil(() => GameContent.IsLoaded, cancellationToken: ct).SuppressCancellationThrow())
            return;

        var points = new List<LootSpawnPoint>();
        var groups = new List<LootSpawnGroup>();
        var plan = new List<PlannedLoot>();
        var mods = EffectiveModifiers;

        LootSpawnPlanner.FindInScene(points, groups);
        LootSpawnPlanner.Plan(points, groups, CreateRandom(), mods, plan);

        int spawned = 0;
        int value = 0;
        foreach (var item in plan)
        {
            try
            {
                await LootSpawnService.SpawnAsync(item.Loot.Id, item.Position, item.Rotation, ct);
                spawned++;
                value += item.Loot.BaseValue;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                AppLog.LogException(LogChannel.Match, exception, item.Point);
            }
        }

        if (_logSummary)
            AppLog.Log(LogChannel.Match,
                $"[LootSpawnDirector] Spawned {spawned} loot from {points.Count} points and {groups.Count} groups " +
                $"(${value:N0} total, {mods}).", this);

        OnLootSpawned?.Invoke(plan);
    }

    private static bool IsServer
    {
        get
        {
            var manager = NetworkManager.main;
            return manager && manager.isServer;
        }
    }
}
}
