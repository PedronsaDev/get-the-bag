using System;
using System.Collections.Generic;
using Project.Definitions.Loot;
using PurrNet;
using UnityEngine;

namespace Project.Match
{
public static class SessionLootLedger
{
    private sealed class Account
    {
        public readonly List<DuffleBagItemEntry> Items = new();
        public int TotalValue;
    }

    private static readonly Dictionary<PlayerID, Account> _accounts = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _accounts.Clear();

    public static int GetTotalValue(PlayerID player) =>
        _accounts.TryGetValue(player, out var account) ? account.TotalValue : 0;

    public static IReadOnlyList<DuffleBagItemEntry> GetItems(PlayerID player) =>
        _accounts.TryGetValue(player, out var account) ? account.Items : Array.Empty<DuffleBagItemEntry>();

    public static int Bank(PlayerID player, IReadOnlyList<DuffleBagItemEntry> items, int value)
    {
        if (!_accounts.TryGetValue(player, out var account))
        {
            account = new Account();
            _accounts[player] = account;
        }

        for (int i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            int index = account.Items.FindIndex(e => string.Equals(e.ItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                var existing = account.Items[index];
                existing.Quantity += entry.Quantity;
                account.Items[index] = existing;
            }
            else
            {
                account.Items.Add(entry);
            }
        }

        account.TotalValue += Mathf.Max(0, value);
        return account.TotalValue;
    }

    public static void Clear() => _accounts.Clear();
}
}
