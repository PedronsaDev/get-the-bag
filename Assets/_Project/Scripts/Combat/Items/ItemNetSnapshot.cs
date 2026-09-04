using System;
using PurrNet.Packing;

namespace Project.Combat
{
    /// <summary>
    /// Network snapshot representing a generated weapon or item.
    /// Uses immutable GUIDs for Core and Part definitions to guarantee 100% deterministic
    /// reconstruction without manual numerical ID assignments or list index fragility.
    /// Includes PartLevels and PartLockedFlags to sync the Gunsmith Circuit system.
    /// </summary>
    [Serializable]
    public struct ItemNetSnapshot : IPackedAuto
    {
        public string CoreGuid;            // Unique GUID of the WeaponCoreDefinition
        public uint Seed;                  // Procedural seed for rolls
        public byte Level;                 // Weapon core level (1-100)
        public byte Rarity;                // ItemRarity byte cast
        public string[] PartGuids;         // Explicit GUIDs of attached parts
        public byte[] PartLevels;          // Individual level for each attached part
        public bool[] PartLockedFlags;      // Signature lock flags for each attached part
        public string CustomName;          // Preserves named titles (e.g. "Unkempt Harold")

        public ItemNetSnapshot(
            string coreGuid,
            uint seed,
            byte level,
            ItemRarity rarity,
            string[] partGuids = null,
            byte[] partLevels = null,
            bool[] partLockedFlags = null,
            string customName = null)
        {
            CoreGuid = coreGuid ?? string.Empty;
            Seed = seed;
            Level = level;
            Rarity = (byte)rarity;
            PartGuids = partGuids ?? Array.Empty<string>();
            PartLevels = partLevels ?? Array.Empty<byte>();
            PartLockedFlags = partLockedFlags ?? Array.Empty<bool>();
            CustomName = customName ?? string.Empty;
        }

        // Backward-compatible constructor overload
        public ItemNetSnapshot(string coreGuid, uint seed, byte level, ItemRarity rarity, string[] partGuids, string customName)
            : this(coreGuid, seed, level, rarity, partGuids, null, null, customName)
        {
        }
    }
}
