using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Waves
{
[CreateAssetMenu(fileName = "New AI Wave Config", menuName = "AI/Wave Config", order = 2)]
public class AIWaveConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Enemy prefab. It must be registered as a network prefab.")]
        public GameObject Prefab;
        [Min(1), Tooltip("Points taken from the wave budget.")]
        public int Cost = 1;
        [Min(1), Tooltip("The first wave this enemy can appear in.")]
        public int FirstWave = 1;
        [Min(1), Tooltip("Relative chance of being picked among the enemies that fit.")]
        public int Weight = 1;
    }

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _firstWaveDelay = 45f;
    [SerializeField, Min(1f)] private float _waveInterval = 60f;
    [SerializeField, Min(0f), Tooltip("Seconds between two enemies of the same wave appearing.")]
    private float _spawnStagger = 0.5f;

    [Header("Size")]
    [SerializeField, Min(1)] private int _baseBudget = 6;
    [SerializeField, Min(0)] private int _budgetGrowthPerWave = 2;
    [SerializeField, Min(0)] private int _budgetPerExtraPlayer = 3;
    [SerializeField, Min(1), Tooltip("No new wave starts while this many are alive.")]
    private int _maxAlive = 12;
    [SerializeField, Min(0)] private int _maxAlivePerExtraPlayer = 4;

    [Header("Placement")]
    [SerializeField, Min(0f), Tooltip("Enemies never appear closer than this to a player.")]
    private float _minSpawnDistance = 18f;

    [Header("Enemies")]
    [SerializeField] private List<Entry> _entries = new();

    public float FirstWaveDelay => _firstWaveDelay;
    public float WaveInterval => _waveInterval;
    public float SpawnStagger => _spawnStagger;
    public int BaseBudget => _baseBudget;
    public int BudgetGrowthPerWave => _budgetGrowthPerWave;
    public int BudgetPerExtraPlayer => _budgetPerExtraPlayer;
    public int MaxAlive => _maxAlive;
    public int MaxAlivePerExtraPlayer => _maxAlivePerExtraPlayer;
    public float MinSpawnDistance => _minSpawnDistance;
    public IReadOnlyList<Entry> Entries => _entries;

    public bool TryValidate(out string error)
    {
        if (_entries == null || _entries.Count == 0)
        {
            error = "A wave config needs at least one enemy entry.";
            return false;
        }

        foreach (var entry in _entries)
        {
            if (entry == null || !entry.Prefab || entry.Cost < 1 || entry.Weight < 1 || entry.FirstWave < 1)
            {
                error = "Every wave entry needs a prefab, a cost of at least 1, a weight of at least 1 and a first wave of at least 1.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
}
