using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Master registry and deterministic generator for procedural weapons and items.
    /// Uses immutable GUIDs for fast O(1) dictionary lookups and network sync,
    /// string IDs for console commands and cheats, and supports both rarity-gated
    /// procedural rolling and dedicated boss drop presets (WeaponPresetDefinition).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Combat/Items/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        private static ItemRegistry _instance;
        public static ItemRegistry Instance
        {
            get
            {
                if (!_instance)
                    _instance = Resources.Load<ItemRegistry>("ItemRegistry");
                return _instance;
            }
        }

        [Header("Registered Item Archetypes")]
        [SerializeField] private List<WeaponCoreDefinition> _weaponCores = new();
        [SerializeField] private List<WeaponPartDefinition> _weaponParts = new();
        [SerializeField] private List<WeaponPresetDefinition> _weaponPresets = new();
        [SerializeField] private List<ManufacturerDefinition> _manufacturers = new();

        private readonly Dictionary<string, WeaponCoreDefinition> _coresByGuid = new();
        private readonly Dictionary<string, WeaponCoreDefinition> _coresByStringId = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, WeaponPartDefinition> _partsByGuid = new();
        private readonly Dictionary<string, WeaponPartDefinition> _partsByStringId = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, WeaponPresetDefinition> _presetsByGuid = new();
        private readonly Dictionary<string, WeaponPresetDefinition> _presetsByStringId = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<WeaponCoreDefinition> WeaponCores => _weaponCores;
        public IReadOnlyList<WeaponPartDefinition> WeaponParts => _weaponParts;
        public IReadOnlyList<WeaponPresetDefinition> WeaponPresets => _weaponPresets;

        private void OnEnable()
        {
            InitializeLookup();
        }

        public void InitializeLookup()
        {
            _coresByGuid.Clear();
            _coresByStringId.Clear();
            _partsByGuid.Clear();
            _partsByStringId.Clear();
            _presetsByGuid.Clear();
            _presetsByStringId.Clear();

            for (int i = 0; i < _weaponCores.Count; i++)
            {
                var core = _weaponCores[i];
                if (!core) continue;

                if (!string.IsNullOrEmpty(core.Guid))
                    _coresByGuid[core.Guid] = core;

                if (!string.IsNullOrEmpty(core.CoreId))
                    _coresByStringId[core.CoreId] = core;
            }

            for (int i = 0; i < _weaponParts.Count; i++)
            {
                var part = _weaponParts[i];
                if (!part) continue;

                if (!string.IsNullOrEmpty(part.Guid))
                    _partsByGuid[part.Guid] = part;

                if (!string.IsNullOrEmpty(part.PartId))
                    _partsByStringId[part.PartId] = part;
            }

            for (int i = 0; i < _weaponPresets.Count; i++)
            {
                var preset = _weaponPresets[i];
                if (!preset) continue;

                if (!string.IsNullOrEmpty(preset.Guid))
                    _presetsByGuid[preset.Guid] = preset;

                if (!string.IsNullOrEmpty(preset.PresetId))
                    _presetsByStringId[preset.PresetId] = preset;
            }
        }

        // ================= FAST O(1) LOOKUPS =================

        public bool TryGetCoreByGuid(string guid, out WeaponCoreDefinition core)
        {
            if (_coresByGuid.Count == 0) InitializeLookup();
            return _coresByGuid.TryGetValue(guid, out core);
        }

        public bool TryGetCoreById(string coreId, out WeaponCoreDefinition core)
        {
            if (_coresByStringId.Count == 0) InitializeLookup();
            return _coresByStringId.TryGetValue(coreId, out core);
        }

        public bool TryGetPartByGuid(string guid, out WeaponPartDefinition part)
        {
            if (_partsByGuid.Count == 0) InitializeLookup();
            return _partsByGuid.TryGetValue(guid, out part);
        }

        public bool TryGetPartById(string partId, out WeaponPartDefinition part)
        {
            if (_partsByStringId.Count == 0) InitializeLookup();
            return _partsByStringId.TryGetValue(partId, out part);
        }

        public bool TryGetPresetByGuid(string guid, out WeaponPresetDefinition preset)
        {
            if (_presetsByGuid.Count == 0) InitializeLookup();
            return _presetsByGuid.TryGetValue(guid, out preset);
        }

        public bool TryGetPresetById(string presetId, out WeaponPresetDefinition preset)
        {
            if (_presetsByStringId.Count == 0) InitializeLookup();
            return _presetsByStringId.TryGetValue(presetId, out preset);
        }

        // ================= PROCEDURAL GENERATION & DEDICATED PRESETS =================

        /// <summary>
        /// Deterministically rolls and compiles a procedural weapon instance with rarity-gated part filtering.
        /// </summary>
        public WeaponInstance GenerateWeapon(WeaponCoreDefinition core, uint seed, byte level = 1, ItemRarity rarity = ItemRarity.Common)
        {
            if (!core) return null;

            System.Random prng = new System.Random((int)seed);
            List<WeaponPartDefinition> selectedParts = new();

            if (core.SupportsPartSlot(WeaponPartType.Barrel))
                RollAndAddPart(core.CompatibleBarrels, prng, selectedParts, rarity);

            if (core.SupportsPartSlot(WeaponPartType.Grip))
                RollAndAddPart(core.CompatibleGrips, prng, selectedParts, rarity);

            if (core.SupportsPartSlot(WeaponPartType.Stock))
                RollAndAddPart(core.CompatibleStocks, prng, selectedParts, rarity);

            if (core.SupportsPartSlot(WeaponPartType.Sight))
                RollAndAddPart(core.CompatibleSights, prng, selectedParts, rarity);

            if (core.SupportsPartSlot(WeaponPartType.Magazine))
                RollAndAddPart(core.CompatibleMagazines, prng, selectedParts, rarity);

            // Accessories only roll on Rare (Blue) or higher, or guaranteed on Legendary
            if (core.SupportsPartSlot(WeaponPartType.Accessory) && rarity >= ItemRarity.Rare && core.CompatibleAccessories.Count > 0)
            {
                RollAndAddPart(core.CompatibleAccessories, prng, selectedParts, rarity);
            }

            return new WeaponInstance(core, selectedParts, seed, level, rarity);
        }

        /// <summary>
        /// Generates a weapon from a dedicated preset (e.g. boss drop, quest reward, iconic legendary).
        /// Locked slots use their assigned parts; unassigned slots roll procedurally for farmable god-rolls.
        /// </summary>
        public WeaponInstance GenerateWeaponFromPreset(WeaponPresetDefinition preset, uint seed, byte level = 1)
        {
            if (!preset || !preset.Core) return null;

            var core = preset.Core;
            System.Random prng = new System.Random((int)seed);
            List<WeaponPartDefinition> selectedParts = new();

            // Barrel
            if (core.SupportsPartSlot(WeaponPartType.Barrel))
            {
                if (preset.IsSlotLocked(WeaponPartType.Barrel))
                    selectedParts.Add(preset.LockedBarrel);
                else
                    RollAndAddPart(core.CompatibleBarrels, prng, selectedParts, preset.Rarity);
            }

            // Grip
            if (core.SupportsPartSlot(WeaponPartType.Grip))
            {
                if (preset.IsSlotLocked(WeaponPartType.Grip))
                    selectedParts.Add(preset.LockedGrip);
                else
                    RollAndAddPart(core.CompatibleGrips, prng, selectedParts, preset.Rarity);
            }

            // Stock
            if (core.SupportsPartSlot(WeaponPartType.Stock))
            {
                if (preset.IsSlotLocked(WeaponPartType.Stock))
                    selectedParts.Add(preset.LockedStock);
                else
                    RollAndAddPart(core.CompatibleStocks, prng, selectedParts, preset.Rarity);
            }

            // Sight
            if (core.SupportsPartSlot(WeaponPartType.Sight))
            {
                if (preset.IsSlotLocked(WeaponPartType.Sight))
                    selectedParts.Add(preset.LockedSight);
                else
                    RollAndAddPart(core.CompatibleSights, prng, selectedParts, preset.Rarity);
            }

            // Magazine
            if (core.SupportsPartSlot(WeaponPartType.Magazine))
            {
                if (preset.IsSlotLocked(WeaponPartType.Magazine))
                    selectedParts.Add(preset.LockedMagazine);
                else
                    RollAndAddPart(core.CompatibleMagazines, prng, selectedParts, preset.Rarity);
            }

            // Accessory
            if (core.SupportsPartSlot(WeaponPartType.Accessory))
            {
                if (preset.IsSlotLocked(WeaponPartType.Accessory))
                    selectedParts.Add(preset.LockedAccessory);
                else if (preset.Rarity >= ItemRarity.Rare && core.CompatibleAccessories.Count > 0)
                    RollAndAddPart(core.CompatibleAccessories, prng, selectedParts, preset.Rarity);
            }

            return new WeaponInstance(core, selectedParts, seed, level, preset.Rarity, preset.PresetDisplayName);
        }

        /// <summary>
        /// Rarity-gated part rolling: filters the pool so that only parts of equal or lower rarity
        /// can roll, and unique/legendary parts are excluded from non-legendary weapons.
        /// </summary>
        private static void RollAndAddPart(IReadOnlyList<WeaponPartDefinition> pool, System.Random prng, List<WeaponPartDefinition> resultList, ItemRarity targetRarity)
        {
            if (pool == null || pool.Count == 0) return;

            List<WeaponPartDefinition> candidates = new(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                var part = pool[i];
                if (!part) continue;

                // Unique parts with special perks only roll on Legendary weapons or dedicated presets
                if (part.IsUnique && targetRarity < ItemRarity.Legendary)
                    continue;

                if (part.Rarity <= targetRarity)
                {
                    candidates.Add(part);
                }
            }

            // Fallback: if no parts matched the strict rarity filter, pick from non-unique candidates
            if (candidates.Count == 0)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    var part = pool[i];
                    if (part && !part.IsUnique) candidates.Add(part);
                }
                if (candidates.Count == 0 && pool.Count > 0)
                    candidates.Add(pool[0]);
            }

            if (candidates.Count > 0)
            {
                int index = prng.Next(0, candidates.Count);
                resultList.Add(candidates[index]);
            }
        }

        // ================= NETWORK SERIALIZATION (GUID-BASED) =================

        public ItemNetSnapshot SerializeWeapon(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.Core)
                return default;

            var core = weapon.Core;
            var parts = weapon.Parts;
            string[] partGuids = new string[parts.Count];

            for (int i = 0; i < parts.Count; i++)
            {
                partGuids[i] = parts[i] ? parts[i].Guid : string.Empty;
            }

            return new ItemNetSnapshot(core.Guid, weapon.Seed, weapon.Level, weapon.Rarity, partGuids, weapon.DisplayName);
        }

        public WeaponInstance ReconstructWeapon(ItemNetSnapshot snapshot)
        {
            if (_coresByGuid.Count == 0)
                InitializeLookup();

            if (!TryGetCoreByGuid(snapshot.CoreGuid, out var core) || !core)
            {
                Debug.LogError($"[ItemRegistry] Failed to find WeaponCore with GUID {snapshot.CoreGuid}");
                return null;
            }

            if (snapshot.PartGuids != null && snapshot.PartGuids.Length > 0)
            {
                List<WeaponPartDefinition> parts = new(snapshot.PartGuids.Length);
                for (int i = 0; i < snapshot.PartGuids.Length; i++)
                {
                    if (TryGetPartByGuid(snapshot.PartGuids[i], out var part) && part)
                    {
                        parts.Add(part);
                    }
                }
                return new WeaponInstance(core, parts, snapshot.Seed, snapshot.Level, (ItemRarity)snapshot.Rarity, snapshot.CustomName);
            }

            return GenerateWeapon(core, snapshot.Seed, snapshot.Level, (ItemRarity)snapshot.Rarity);
        }

        // ================= EDITOR HELPERS =================

#if UNITY_EDITOR
        [ContextMenu("Scan Project For All Weapons & Parts")]
        public void ScanProjectAssets()
        {
            _weaponCores.Clear();
            _weaponParts.Clear();
            _weaponPresets.Clear();

            string[] coreGuids = UnityEditor.AssetDatabase.FindAssets("t:WeaponCoreDefinition");
            foreach (var g in coreGuids)
            {
                var core = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponCoreDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (core && !_weaponCores.Contains(core)) _weaponCores.Add(core);
            }

            string[] partGuids = UnityEditor.AssetDatabase.FindAssets("t:WeaponPartDefinition");
            foreach (var g in partGuids)
            {
                var part = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponPartDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (part && !_weaponParts.Contains(part)) _weaponParts.Add(part);
            }

            string[] presetGuids = UnityEditor.AssetDatabase.FindAssets("t:WeaponPresetDefinition");
            foreach (var g in presetGuids)
            {
                var preset = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponPresetDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (preset && !_weaponPresets.Contains(preset)) _weaponPresets.Add(preset);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            InitializeLookup();
        }

        public void RegisterCore(WeaponCoreDefinition core)
        {
            if (core && !_weaponCores.Contains(core))
            {
                _weaponCores.Add(core);
                UnityEditor.EditorUtility.SetDirty(this);
                InitializeLookup();
            }
        }

        public void RegisterPart(WeaponPartDefinition part)
        {
            if (part && !_weaponParts.Contains(part))
            {
                _weaponParts.Add(part);
                UnityEditor.EditorUtility.SetDirty(this);
                InitializeLookup();
            }
        }

        public void RegisterPreset(WeaponPresetDefinition preset)
        {
            if (preset && !_weaponPresets.Contains(preset))
            {
                _weaponPresets.Add(preset);
                UnityEditor.EditorUtility.SetDirty(this);
                InitializeLookup();
            }
        }
#endif
    }
}
