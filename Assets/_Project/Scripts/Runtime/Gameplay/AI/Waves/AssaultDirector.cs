using System.Collections.Generic;
using Project.AI.Alarm;
using Project.AI.Decision;
using Project.Combat.Health;
using Project.Core.Player;
using Project.Core.Raid;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.AI.Waves
{
/// <summary>
/// Server: runs the police assault through <see cref="AssaultCoordinator"/> and spawns its enemies.
/// Every peer: publishes the level's threat as <see cref="IThreatStatus"/>, read from SyncVars, for the HUD
/// and the results.
/// </summary>
[DisallowMultipleComponent]
public class AssaultDirector : NetworkBehaviour, IThreatStatus
{
    [SerializeField] private AIWaveConfig _config;

    private readonly SyncVar<byte> _syncedPhase = new((byte)ThreatPhase.Stealth);
    private readonly SyncVar<int> _syncedSeconds = new(0);
    private readonly SyncVar<int> _syncedAssault = new(0);
    private readonly SyncVar<int> _syncedEnemiesAlive = new(0);
    private readonly List<HealthComponent> _spawned = new(32);
    private readonly List<Vector3> _playerPositions = new(4);
    private readonly System.Random _random = new();

    private AssaultCoordinator _coordinator;
    private float _staggerTimer;
    private bool _configChecked;
    private bool _configValid;

    public ThreatPhase Phase => (ThreatPhase)_syncedPhase.value;
    public float SecondsInPhase => _syncedSeconds.value;
    public int AssaultNumber => _syncedAssault.value;
    public int EnemiesAlive => _syncedEnemiesAlive.value;
    public int CivilianKills => AlarmDirector.Instance ? AlarmDirector.Instance.CivilianKills : 0;
    byte IThreatStatus.AlarmReason => AlarmDirector.Instance ? AlarmDirector.Instance.ReasonByte : ThreatStatus.NoAlarmReason;

    protected override void OnSpawned() => ThreatStatus.Current = this;

    protected override void OnDespawned() => ClearCurrentStatus();

    protected override void OnDestroy()
    {
        ClearCurrentStatus();
        base.OnDestroy();
    }

    private void Update()
    {
        if (!isServer || !ConfigIsValid())
            return;

        _coordinator ??= new AssaultCoordinator(_config, _random);

        var alarm = AlarmDirector.Instance;
        bool alarmed = alarm && alarm.State == AlarmState.Alarmed;
        int players = Mathf.Max(1, PlayerIdentifier.ActivePlayers.Count);
        _coordinator.Advance(Time.deltaTime, RaidLifecycle.Phase, alarmed, players);

        if (!_coordinator.HasEnded)
            UpdateSpawning(Time.deltaTime, AIWavePlanner.MaxAlive(_config, players));

        PublishStatus();
    }

    private void UpdateSpawning(float deltaTime, int maxAlive)
    {
        var queue = _coordinator.SpawnQueue;
        if (queue.Count == 0)
            return;

        _staggerTimer -= deltaTime;
        if (_staggerTimer > 0f || CountSpawnedAlive() >= maxAlive)
            return;

        _staggerTimer = _config.SpawnStagger;
        if (!TrySpawn(queue.Peek()))
            return;
        queue.Dequeue();
    }

    private bool TrySpawn(GameObject prefab)
    {
        CollectPlayerPositions();
        if (!AIWaveSpawnPoint.TryPick(_playerPositions, _config.MinSpawnDistance, out var point))
        {
            AppLog.LogWarning(LogChannel.AI, "No wave spawn point is far enough from the players. Add more AIWaveSpawnPoints.", this);
            return false;
        }

        var instance = Instantiate(prefab, point.Position, point.transform.rotation);
        if (NetworkManager.main)
            NetworkManager.main.Spawn(instance);

        var health = instance.GetComponentInChildren<HealthComponent>(true);
        if (health)
            _spawned.Add(health);
        return true;
    }

    private void CollectPlayerPositions()
    {
        _playerPositions.Clear();
        foreach (var player in PlayerIdentifier.ActivePlayers)
        {
            if (player && !player.IsDead)
                _playerPositions.Add(player.BodyTransform.position);
        }
    }

    private int CountSpawnedAlive()
    {
        int alive = 0;
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (!_spawned[i])
                _spawned.RemoveAt(i);
            else if (!_spawned[i].IsDead)
                alive++;
        }
        return alive;
    }

    private static int CountLivingFighters()
    {
        int alive = 0;
        var brains = AIBrain.All;
        for (int i = 0; i < brains.Count; i++)
        {
            if (brains[i].IsLivingFighter)
                alive++;
        }
        return alive;
    }

    // SyncVars ignore a write of the value they already hold, so this can run every frame.
    private void PublishStatus()
    {
        _syncedPhase.value = (byte)_coordinator.CurrentThreatPhase;
        _syncedSeconds.value = SecondsToShow();
        _syncedAssault.value = _coordinator.Scheduler.AssaultNumber;
        _syncedEnemiesAlive.value = CountLivingFighters();
    }

    private int SecondsToShow()
    {
        if (_coordinator.HasEnded)
            return 0;
        return Mathf.CeilToInt(_coordinator.Scheduler.SecondsInPhase);
    }

    private void ClearCurrentStatus()
    {
        if (ReferenceEquals(ThreatStatus.Current, this))
            ThreatStatus.Current = null;
    }

    private bool ConfigIsValid()
    {
        if (_configChecked)
            return _configValid;

        _configChecked = true;
        if (!_config)
        {
            AppLog.LogError(LogChannel.AI, "AssaultDirector has no wave config.", this);
            return false;
        }
        _configValid = _config.TryValidate(out string error);
        if (!_configValid)
            AppLog.LogError(LogChannel.AI, error, this);
        return _configValid;
    }
}
}
