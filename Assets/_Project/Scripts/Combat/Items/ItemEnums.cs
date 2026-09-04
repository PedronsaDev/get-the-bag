using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Rarity classification for all loot across the game.
    /// Dictates stat rolls, number of part attachments, and UI styling.
    /// </summary>
    public enum ItemRarity : byte
    {
        Common = 0,       // White - Standard issue
        Uncommon = 1,     // Green - Reliable workhorse
        Rare = 2,         // Blue - Enhanced performance
        Epic = 3,         // Purple - High-tier military grade
        Legendary = 4,    // Orange - Exotic with red-text unique mechanics
        Unique = 5        // Cyan/Special - Quest rewards or signature boss drops
    }

    /// <summary>
    /// Broad category of equippable items in the unified loot ecosystem.
    /// </summary>
    public enum ItemCategory : byte
    {
        Weapon = 0,
        Shield = 1,
        GrenadeMod = 2,
        ClassMod = 3,
        Relic = 4
    }

    /// <summary>
    /// Utility for rarity colors and presentation.
    /// </summary>
    public static class RarityUtility
    {
        public static Color GetColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => new Color(0.9f, 0.9f, 0.9f),
            ItemRarity.Uncommon => new Color(0.25f, 0.85f, 0.3f),
            ItemRarity.Rare => new Color(0.2f, 0.55f, 1.0f),
            ItemRarity.Epic => new Color(0.7f, 0.25f, 0.95f),
            ItemRarity.Legendary => new Color(1.0f, 0.55f, 0.1f),
            ItemRarity.Unique => new Color(0.1f, 0.9f, 0.95f),
            _ => Color.white
        };

        public static string GetHexColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => "#E0E0E0",
            ItemRarity.Uncommon => "#40DA4D",
            ItemRarity.Rare => "#338DFE",
            ItemRarity.Epic => "#B340F3",
            ItemRarity.Legendary => "#FF8C1A",
            ItemRarity.Unique => "#1AE6F3",
            _ => "#FFFFFF"
        };
    }
}
