using System.Collections.Generic;
using Project.Definitions.Loot;
using UnityEngine;

namespace Project.Loot.Spawning
{
/// <summary>
/// A place where one piece of loot may appear when the level starts.
/// Picks from its pool plus its own entries. Rolled by the level's LootSpawnDirector on the server.
/// </summary>
[AddComponentMenu("Loot/Loot Spawn Point")]
public class LootSpawnPoint : MonoBehaviour
{
    [Tooltip("Shared pool to pick from. Optional if Extra Entries has loot.")]
    [SerializeField] private LootPool _pool;

    [Tooltip("Loot picked from in addition to the pool. Use for one-off spots.")]
    [SerializeField] private LootTable _extraEntries = new();

    [Tooltip("Chance this point spawns anything at all.")]
    [SerializeField, Range(0f, 1f)] private float _spawnChance = 1f;

    [Tooltip("Ignore the quantity modifier (for example objective or quest loot that must always appear).")]
    [SerializeField] private bool _ignoreQuantityModifier;

    private readonly List<LootTable> _tables = new(2);

    public LootPool Pool => _pool;
    public float SpawnChance => _spawnChance;

    public float EffectiveChance(in LootRollModifiers mods) =>
        _ignoreQuantityModifier ? _spawnChance : mods.ApplyToChance(_spawnChance);

    public IReadOnlyList<LootTable> Tables
    {
        get
        {
            _tables.Clear();
            if (_pool) _tables.Add(_pool.Table);
            _tables.Add(_extraEntries);
            return _tables;
        }
    }

    /// <summary>Rolls the chance, then picks loot. With <paramref name="skipChance"/> the point always picks (used by groups).</summary>
    public bool TryRoll(System.Random rng, in LootRollModifiers mods, bool skipChance, out LootDefinition loot)
    {
        loot = null;
        if (!skipChance && rng.NextDouble() >= EffectiveChance(mods))
            return false;

        loot = LootTable.Roll(Tables, rng, mods);
        return loot;
    }

#if UNITY_EDITOR
    private static readonly List<KeyValuePair<LootDefinition, float>> GizmoOdds = new();

    private void OnDrawGizmos()
    {
        LootTable.GetOdds(Tables, LootRollModifiers.Neutral, GizmoOdds);
        var color = GizmoOdds.Count == 0 ? Color.red : LootGizmoColors.For(BestRarity());
        bool grouped = GetComponentInParent<LootSpawnGroup>();

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(color.r, color.g, color.b, 0.15f + 0.6f * _spawnChance);
        Gizmos.DrawCube(Vector3.up * 0.15f, Vector3.one * 0.3f);
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.up * 0.15f, Vector3.one * 0.3f);
        if (grouped) Gizmos.DrawWireCube(Vector3.up * 0.15f, Vector3.one * 0.36f);
        Gizmos.DrawLine(Vector3.up * 0.15f, Vector3.up * 0.15f + Vector3.forward * 0.3f);
    }

    private void OnDrawGizmosSelected()
    {
        LootTable.GetOdds(Tables, LootRollModifiers.Neutral, GizmoOdds);
        string label = GizmoOdds.Count == 0
            ? "NO LOOT"
            : $"{(_pool ? _pool.name : "Inline")}  {_spawnChance:P0}";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, label);
    }

    private LootRarity BestRarity()
    {
        var best = LootRarity.Common;
        foreach (var pair in GizmoOdds)
            if (pair.Key.Rarity > best) best = pair.Key.Rarity;
        return best;
    }
#endif
}

public static class LootGizmoColors
{
    public static Color For(LootRarity rarity) => rarity switch
    {
        LootRarity.Common => new Color(0.75f, 0.75f, 0.75f),
        LootRarity.Uncommon => new Color(0.3f, 0.85f, 0.35f),
        LootRarity.Rare => new Color(0.25f, 0.55f, 1f),
        LootRarity.VeryRare => new Color(0.7f, 0.35f, 1f),
        LootRarity.Legendary => new Color(1f, 0.6f, 0.1f),
        LootRarity.Quest => new Color(1f, 0.9f, 0.2f),
        _ => Color.white
    };
}
}
