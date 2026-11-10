using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Loot.Data
{
[Serializable]
public class LootTableEntry
{
    public LootDefinition Loot;

    [Tooltip("Relative chance against the other entries. 2 is twice as likely as 1.")]
    [Min(0f)] public float Weight = 1f;
}

/// <summary>
/// Knobs that bend every loot roll. Neutral values change nothing.
/// Combine several sources (level, difficulty, heat) with <see cref="Combine"/>.
/// </summary>
[Serializable]
public struct LootRollModifiers
{
    [Tooltip("Multiplies spawn chances and group counts. 1 = normal, 0.5 = half as much loot, 2 = twice as much.")]
    [Min(0f)] public float QuantityMultiplier;

    [Tooltip("Shifts picks toward rarer loot. 0 = normal. +1 doubles each rarity step's weight " +
             "(Rare x4 vs Common), -1 halves it.")]
    [Range(-3f, 3f)] public float QualityBias;

    public static LootRollModifiers Neutral => new() { QuantityMultiplier = 1f, QualityBias = 0f };

    public LootRollModifiers Combine(LootRollModifiers other) => new()
    {
        QuantityMultiplier = QuantityMultiplier * other.QuantityMultiplier,
        QualityBias = QualityBias + other.QualityBias
    };

    public float ApplyToChance(float chance) => Mathf.Clamp01(chance * QuantityMultiplier);

    public float RarityFactor(LootRarity rarity) =>
        rarity == LootRarity.Quest ? 1f : Mathf.Pow(2f, QualityBias * (int)rarity);

    public override string ToString() => $"quantity x{QuantityMultiplier:0.##}, quality {QualityBias:+0.##;-0.##;0}";
}

/// <summary>A weighted list of loot. Used by <see cref="LootPool"/> assets and inline on spawn points.</summary>
[Serializable]
public class LootTable
{
    [SerializeField] private List<LootTableEntry> _entries = new();

    public IReadOnlyList<LootTableEntry> Entries => _entries;

    public bool HasEntries
    {
        get
        {
            foreach (var e in _entries)
                if (e != null && e.Loot && e.Weight > 0f) return true;
            return false;
        }
    }

    public static float EffectiveWeight(LootTableEntry entry, in LootRollModifiers mods) =>
        entry == null || !entry.Loot || entry.Weight <= 0f ? 0f : entry.Weight * mods.RarityFactor(entry.Loot.Rarity);

    /// <summary>Picks one entry across all given tables, weighted. Null if nothing can be picked.</summary>
    public static LootDefinition Roll(IReadOnlyList<LootTable> tables, System.Random rng, in LootRollModifiers mods)
    {
        float total = 0f;
        foreach (var table in tables)
            if (table != null)
                foreach (var e in table._entries)
                    total += EffectiveWeight(e, mods);

        if (total <= 0f) return null;

        double pick = rng.NextDouble() * total;
        LootDefinition last = null;
        foreach (var table in tables)
        {
            if (table == null) continue;
            foreach (var e in table._entries)
            {
                float w = EffectiveWeight(e, mods);
                if (w <= 0f) continue;
                last = e.Loot;
                pick -= w;
                if (pick <= 0) return e.Loot;
            }
        }

        return last;
    }

    /// <summary>Fills <paramref name="results"/> with each loot's chance (0-1) of being picked. For inspectors and tuning.</summary>
    public static void GetOdds(IReadOnlyList<LootTable> tables, in LootRollModifiers mods, List<KeyValuePair<LootDefinition, float>> results)
    {
        results.Clear();
        float total = 0f;
        var weights = new Dictionary<LootDefinition, float>();

        foreach (var table in tables)
        {
            if (table == null) continue;
            foreach (var e in table._entries)
            {
                float w = EffectiveWeight(e, mods);
                if (w <= 0f) continue;
                weights.TryGetValue(e.Loot, out float existing);
                weights[e.Loot] = existing + w;
                total += w;
            }
        }

        if (total <= 0f) return;
        foreach (var pair in weights)
            results.Add(new KeyValuePair<LootDefinition, float>(pair.Key, pair.Value / total));
        results.Sort((a, b) => b.Value.CompareTo(a.Value));
    }
}
}
