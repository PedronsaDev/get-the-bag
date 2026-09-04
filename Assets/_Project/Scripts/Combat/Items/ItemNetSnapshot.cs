using System;
using PurrNet.Packing;

namespace Project.Combat
{
    /// <summary>
    /// Network snapshot representing a generated weapon or item.
    /// Uses immutable GUIDs for Core and Part definitions to guarantee 100% deterministic
    /// reconstruction without manual numerical ID assignments or list index fragility.
    /// Includes CustomName to preserve named presets and title prefixes.
    /// </summary>
    [Serializable]
    public struct ItemNetSnapshot : IPackedAuto
    {
        public string CoreGuid;       // Unique GUID of the WeaponCoreDefinition
        public uint Seed;             // Procedural seed for rolls
        public byte Level;            // Item level (1-100)
        public byte Rarity;           // ItemRarity byte cast
        public string[] PartGuids;    // Explicit GUIDs of attached parts
        public string CustomName;     // Preserves named titles (e.g. "Boss Combat Pistol", "Unkempt Harold")

        public ItemNetSnapshot(string coreGuid, uint seed, byte level, ItemRarity rarity, string[] partGuids = null, string customName = null)
        {
            CoreGuid = coreGuid ?? string.Empty;
            Seed = seed;
            Level = level;
            Rarity = (byte)rarity;
            PartGuids = partGuids ?? Array.Empty<string>();
            CustomName = customName ?? string.Empty;
        }
    }
}
