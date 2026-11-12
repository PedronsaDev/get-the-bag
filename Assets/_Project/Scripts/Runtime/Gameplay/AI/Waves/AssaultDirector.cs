using System.Collections.Generic;
using Project.AI.Alarm;
using Project.Combat.Health;
using Project.Core.Player;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.AI.Waves
{
[DisallowMultipleComponent]
public class AssaultDirector : NetworkBehaviour
{
    [SerializeField] private AIWaveConfig _config;

    private readonly SyncVar<int> _syncedAssault = new(0);
    private readonly AIWaveScheduler _scheduler = new();
    private readonly List<HealthComponent> _spawned = new(32);
    private readonly Queue<GameObject> _spawnQueue = new(16);
    private readonly List<int> _plan = new(16);
    private readonly List<Vector3> _playerPositions = new(4);
    private readonly System.Random _random = new();

    private float _staggerTimer;
    private bool _configChecked;
    private bool _configValid;

    public int AssaultNumber => _syncedAssault.value;
    public AIWaveScheduler.Phase CurrentPhase => _scheduler.CurrentPhase;

    private void Update()
    {
        if (!isServer || !ConfigIsValid())
            return;

        UpdateScheduleFromAlarm();
        if (_scheduler.CurrentPhase == AIWaveScheduler.Phase.Inactive)
            return;

        int players = Mathf.Max(1, PlayerIdentifier.ActivePlayers.Count);
        HandleChange(_scheduler.Tick(Time.deltaTime), players);
        UpdateSpawning(Time.deltaTime, AIWavePlanner.MaxAlive(_config, players));
    }

    private void UpdateScheduleFromAlarm()
    {
        var alarm = AlarmDirector.Instance;
        bool alarmed = alarm && alarm.State == AlarmState.Alarmed;

        if (alarmed && _scheduler.CurrentPhase == AIWaveScheduler.Phase.Inactive)
        {
            _scheduler.Start(_config.FirstWaveDelay, _config.AssaultSeconds, _config.BreakSeconds);
            AppLog.Log(LogChannel.AI, "Assault starts in {0} seconds.", _config.FirstWaveDelay, this);
        }
        else if (!alarmed && _scheduler.CurrentPhase != AIWaveScheduler.Phase.Inactive)
        {
            _scheduler.Stop();
            _spawnQueue.Clear();
            _syncedAssault.value = 0;
        }
    }

    private void HandleChange(AIWaveScheduler.Change change, int players)
    {
        switch (change)
        {
            case AIWaveScheduler.Change.AssaultStarted:
                QueueAssault(AIWavePlanner.Budget(_config, _scheduler.AssaultNumber, players));
                break;
            case AIWaveScheduler.Change.FinalPushStarted:
                QueueAssault(AIWavePlanner.FinalPushBudget(_config, _scheduler.AssaultNumber, players));
                break;
            case AIWaveScheduler.Change.BreakStarted:
                _spawnQueue.Clear();
                AppLog.Log(LogChannel.AI, "Break for {0} seconds.", _config.BreakSeconds, this);
                break;
        }
    }

    private void QueueAssault(int budget)
    {
        int assault = _scheduler.AssaultNumber;
        _spawnQueue.Clear();
        AIWavePlanner.Plan(_config.Entries, assault, budget, _random, _plan);
        foreach (int index in _plan)
            _spawnQueue.Enqueue(_config.Entries[index].Prefab);

        _syncedAssault.value = assault;
        AppLog.Log(LogChannel.AI, "Assault {0}: {1} enemies from a budget of {2}.", assault, _plan.Count, budget, this);
    }

    private void UpdateSpawning(float deltaTime, int maxAlive)
    {
        if (_spawnQueue.Count == 0)
            return;

        _staggerTimer -= deltaTime;
        if (_staggerTimer > 0f || CountAlive() >= maxAlive)
            return;

        _staggerTimer = _config.SpawnStagger;
        if (!TrySpawn(_spawnQueue.Peek()))
            return;
        _spawnQueue.Dequeue();
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

    private int CountAlive()
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
