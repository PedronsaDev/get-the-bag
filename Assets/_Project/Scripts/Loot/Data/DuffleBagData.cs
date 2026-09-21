using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Loot
{
[Serializable]
public struct DuffleBagItemEntry
{
    public string ItemId;
    public int Quantity;

    public DuffleBagItemEntry(string itemId, int quantity = 1)
    {
        ItemId = itemId;
        Quantity = Mathf.Max(1, quantity);
    }
}

[Serializable]
public struct DuffleBagSnapshot
{
    public float TotalWeight;
    public int TotalValue;
    public List<DuffleBagItemEntry> Items;

    public static DuffleBagSnapshot Empty => new()
    {
        TotalWeight = 0f,
        TotalValue = 0,
        Items = new List<DuffleBagItemEntry>()
    };

    public readonly bool IsEmpty => Items == null || Items.Count == 0;

    public DuffleBagSnapshot(List<DuffleBagItemEntry> items, float totalWeight, int totalValue)
    {
        Items = items != null ? new List<DuffleBagItemEntry>(items) : new List<DuffleBagItemEntry>();
        TotalWeight = totalWeight;
        TotalValue = totalValue;
    }
}
}