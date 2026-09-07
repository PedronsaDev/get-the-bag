using System;
using NaughtyAttributes;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Defines a dedicated named weapon archetype (e.g. boss drops, quest rewards, iconic legendaries).
    /// Allows pinning specific iconic parts (like a unique legendary barrel or accessory),
    /// while leaving unassigned slots open to procedural rolling for farmable god-rolls.
    /// </summary>
    [CreateAssetMenu(fileName = "PRESET_Pistol_Legendary_New", menuName = "Combat/Weapons/Weapon Preset (Dedicated Drop)")]
    public class WeaponPresetDefinition : ScriptableObject
    {
        [BoxGroup("Preset Identity")]
        [SerializeField, ReadOnly] private string _guid;
        [BoxGroup("Preset Identity")]
        [Tooltip("Human-friendly string identifier used for debug commands, drop tables, and cheats (e.g. 'preset_unkempt_harold').")]
        [SerializeField] private string _presetId = "preset_pistol_legendary_new";
        [BoxGroup("Preset Identity")]
        [Tooltip("Official display name of this named weapon (e.g. 'Unkempt Harold', 'Conference Call').")]
        [SerializeField] private string _presetDisplayName = "Custom Named Weapon";
        [BoxGroup("Preset Identity")]
        [Required]
        [SerializeField] private WeaponCoreDefinition _core;
        [BoxGroup("Preset Identity")]
        [SerializeField] private ItemRarity _rarity = ItemRarity.Legendary;

        [BoxGroup("Pinned / Locked Slots (Leave empty to roll procedurally)")]
        [Tooltip("If assigned, this slot is locked to this exact part on every drop. If empty, the slot rolls procedurally from the core pool.")]
        [SerializeField] private WeaponPartDefinition _lockedBarrel;
        [BoxGroup("Pinned / Locked Slots (Leave empty to roll procedurally)")]
        [SerializeField] private WeaponPartDefinition _lockedGrip;
        [BoxGroup("Pinned / Locked Slots (Leave empty to roll procedurally)")]
        [SerializeField] private WeaponPartDefinition _lockedStock;
        [BoxGroup("Pinned / Locked Slots (Leave empty to roll procedurally)")]
        [SerializeField] private WeaponPartDefinition _lockedSight;
        [BoxGroup("Pinned / Locked Slots (Leave empty to roll procedurally)")]
        [SerializeField] private WeaponPartDefinition _lockedMagazine;
        [BoxGroup("Pinned / Locked Slots (Leave empty to roll procedurally)")]
        [SerializeField] private WeaponPartDefinition _lockedAccessory;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_guid))
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(this);
                if (!string.IsNullOrEmpty(path))
                {
                    _guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                }
                if (string.IsNullOrEmpty(_guid))
                {
                    _guid = System.Guid.NewGuid().ToString("N");
                }
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        public string Guid => _guid;
        public string PresetId => _presetId;
        public string PresetDisplayName => _presetDisplayName;
        public WeaponCoreDefinition Core => _core;
        public ItemRarity Rarity => _rarity;

        public WeaponPartDefinition LockedBarrel => _lockedBarrel;
        public WeaponPartDefinition LockedGrip => _lockedGrip;
        public WeaponPartDefinition LockedStock => _lockedStock;
        public WeaponPartDefinition LockedSight => _lockedSight;
        public WeaponPartDefinition LockedMagazine => _lockedMagazine;
        public WeaponPartDefinition LockedAccessory => _lockedAccessory;

        /// <summary>
        /// Retrieves the pinned part for the given slot, or null if the slot should roll procedurally.
        /// </summary>
        public WeaponPartDefinition GetLockedPart(WeaponPartType slot) => slot switch
        {
            WeaponPartType.Barrel => _lockedBarrel,
            WeaponPartType.Grip => _lockedGrip,
            WeaponPartType.Stock => _lockedStock,
            WeaponPartType.Sight => _lockedSight,
            WeaponPartType.Magazine => _lockedMagazine,
            WeaponPartType.Accessory => _lockedAccessory,
            _ => null
        };

        /// <summary>
        /// Returns true if this slot is locked to a specific part definition.
        /// </summary>
        public bool IsSlotLocked(WeaponPartType slot) => GetLockedPart(slot) != null;
    }
}
