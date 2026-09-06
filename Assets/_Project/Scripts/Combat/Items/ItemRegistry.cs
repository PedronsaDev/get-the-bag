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
                if (_instance != null) return _instance;

                _instance = Resources.Load<ItemRegistry>("ItemRegistry");

#if UNITY_EDITOR
                if (_instance == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemRegistry");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemRegistry>(path);
                    }
                    else
                    {
                        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                        {
                            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project"))
                                UnityEditor.AssetDatabase.CreateFolder("Assets", "_Project");
                            UnityEditor.AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                        }
                        _instance = CreateInstance<ItemRegistry>();
                        UnityEditor.AssetDatabase.CreateAsset(_instance, "Assets/_Project/Resources/ItemRegistry.asset");
                        UnityEditor.AssetDatabase.SaveAssets();
                    }
                }
#endif

                if (_instance == null)
                {
                    _instance = CreateInstance<ItemRegistry>();
                    _instance.name = "ItemRegistry_RuntimeFallback";
                }

                return _instance;
            }
        }

        /// <summary>
        /// Fired whenever the item registry is rescanned or catalog data is updated.
        /// </summary>
        public static event Action OnRegistryUpdated;

        [Header("Registered Item Archetypes")]
        [SerializeField] private List<WeaponCoreDefinition> _weaponCores = new();
        [SerializeField] private List<WeaponPartDefinition> _weaponParts = new();
        [SerializeField] private List<WeaponPresetDefinition> _weaponPresets = new();
        [SerializeField] private List<ManufacturerDefinition> _manufacturers = new();
        [SerializeField] private List<ProjectileBehaviorDefinition> _projectileBehaviors = new();
        [SerializeField] private List<WeaponPerkDefinition> _weaponPerks = new();

        private readonly Dictionary<string, WeaponCoreDefinition> _coresByGuid = new();
        private readonly Dictionary<string, WeaponCoreDefinition> _coresByStringId = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, WeaponPartDefinition> _partsByGuid = new();
        private readonly Dictionary<string, WeaponPartDefinition> _partsByStringId = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, WeaponPresetDefinition> _presetsByGuid = new();
        private readonly Dictionary<string, WeaponPresetDefinition> _presetsByStringId = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ManufacturerDefinition> _manufacturersById = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ProjectileBehaviorDefinition> _behaviorsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WeaponPerkDefinition> _perksById = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<WeaponCoreDefinition> WeaponCores => _weaponCores;
        public IReadOnlyList<WeaponPartDefinition> WeaponParts => _weaponParts;
        public IReadOnlyList<WeaponPresetDefinition> WeaponPresets => _weaponPresets;
        public IReadOnlyList<ManufacturerDefinition> Manufacturers => _manufacturers;
        public IReadOnlyList<ProjectileBehaviorDefinition> ProjectileBehaviors => _projectileBehaviors;
        public IReadOnlyList<WeaponPerkDefinition> WeaponPerks => _weaponPerks;

        private void OnEnable()
        {
            InitializeLookup();
#if UNITY_EDITOR
            if (_weaponCores.Count == 0 && _weaponParts.Count == 0)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null) ScanProjectAssets(silent: true);
                };
            }
#endif
        }

        public void InitializeLookup()
        {
            _coresByGuid.Clear();
            _coresByStringId.Clear();
            _partsByGuid.Clear();
            _partsByStringId.Clear();
            _presetsByGuid.Clear();
            _presetsByStringId.Clear();
            _manufacturersById.Clear();

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

            for (int i = 0; i < _manufacturers.Count; i++)
            {
                var mfg = _manufacturers[i];
                if (!mfg) continue;

                if (!string.IsNullOrEmpty(mfg.ManufacturerId))
                    _manufacturersById[mfg.ManufacturerId] = mfg;
            }

            _behaviorsById.Clear();
            for (int i = 0; i < _projectileBehaviors.Count; i++)
            {
                var beh = _projectileBehaviors[i];
                if (!beh) continue;
                if (!string.IsNullOrEmpty(beh.BehaviorId))
                    _behaviorsById[beh.BehaviorId] = beh;
            }

            _perksById.Clear();
            for (int i = 0; i < _weaponPerks.Count; i++)
            {
                var perk = _weaponPerks[i];
                if (!perk) continue;
                if (!string.IsNullOrEmpty(perk.PerkId))
                    _perksById[perk.PerkId] = perk;
            }
        }

        public bool TryGetBehaviorById(string behaviorId, out ProjectileBehaviorDefinition behavior)
        {
            if (_behaviorsById.Count == 0 && _projectileBehaviors.Count > 0) InitializeLookup();
            return _behaviorsById.TryGetValue(behaviorId, out behavior);
        }

        public bool TryGetPerkById(string perkId, out WeaponPerkDefinition perk)
        {
            if (_perksById.Count == 0 && _weaponPerks.Count > 0) InitializeLookup();
            return _perksById.TryGetValue(perkId, out perk);
        }

        public bool TryGetManufacturer(string manufacturerId, out ManufacturerDefinition manufacturer)
        {
            if (_manufacturersById.Count == 0 && _manufacturers.Count > 0) InitializeLookup();
            return _manufacturersById.TryGetValue(manufacturerId, out manufacturer);
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

            List<WeaponPartInstance> partInstances = new(selectedParts.Count);
            for (int i = 0; i < selectedParts.Count; i++)
            {
                var p = selectedParts[i];
                if (p) partInstances.Add(new WeaponPartInstance(p, level, isLocked: p.IsUnique));
            }

            return new WeaponInstance(core, partInstances, seed, level, rarity);
        }

        /// <summary>
        /// Generates a weapon from a dedicated preset (e.g. boss drop, quest reward, iconic legendary).
        /// Locked slots use their assigned parts with IsLocked = true; unassigned slots roll procedurally.
        /// </summary>
        public WeaponInstance GenerateWeaponFromPreset(WeaponPresetDefinition preset, uint seed, byte level = 1)
        {
            if (!preset || !preset.Core) return null;

            var core = preset.Core;
            System.Random prng = new System.Random((int)seed);
            List<WeaponPartInstance> partInstances = new();

            // Barrel
            if (core.SupportsPartSlot(WeaponPartType.Barrel))
            {
                if (preset.IsSlotLocked(WeaponPartType.Barrel))
                    partInstances.Add(new WeaponPartInstance(preset.LockedBarrel, level, isLocked: true));
                else
                    RollAndAddPartInstance(core.CompatibleBarrels, prng, partInstances, preset.Rarity, level);
            }

            // Grip
            if (core.SupportsPartSlot(WeaponPartType.Grip))
            {
                if (preset.IsSlotLocked(WeaponPartType.Grip))
                    partInstances.Add(new WeaponPartInstance(preset.LockedGrip, level, isLocked: true));
                else
                    RollAndAddPartInstance(core.CompatibleGrips, prng, partInstances, preset.Rarity, level);
            }

            // Stock
            if (core.SupportsPartSlot(WeaponPartType.Stock))
            {
                if (preset.IsSlotLocked(WeaponPartType.Stock))
                    partInstances.Add(new WeaponPartInstance(preset.LockedStock, level, isLocked: true));
                else
                    RollAndAddPartInstance(core.CompatibleStocks, prng, partInstances, preset.Rarity, level);
            }

            // Sight
            if (core.SupportsPartSlot(WeaponPartType.Sight))
            {
                if (preset.IsSlotLocked(WeaponPartType.Sight))
                    partInstances.Add(new WeaponPartInstance(preset.LockedSight, level, isLocked: true));
                else
                    RollAndAddPartInstance(core.CompatibleSights, prng, partInstances, preset.Rarity, level);
            }

            // Magazine
            if (core.SupportsPartSlot(WeaponPartType.Magazine))
            {
                if (preset.IsSlotLocked(WeaponPartType.Magazine))
                    partInstances.Add(new WeaponPartInstance(preset.LockedMagazine, level, isLocked: true));
                else
                    RollAndAddPartInstance(core.CompatibleMagazines, prng, partInstances, preset.Rarity, level);
            }

            // Accessory
            if (core.SupportsPartSlot(WeaponPartType.Accessory))
            {
                if (preset.IsSlotLocked(WeaponPartType.Accessory))
                    partInstances.Add(new WeaponPartInstance(preset.LockedAccessory, level, isLocked: true));
                else if (preset.Rarity >= ItemRarity.Rare && core.CompatibleAccessories.Count > 0)
                    RollAndAddPartInstance(core.CompatibleAccessories, prng, partInstances, preset.Rarity, level);
            }

            return new WeaponInstance(core, partInstances, seed, level, preset.Rarity, preset.PresetDisplayName);
        }

        private static void RollAndAddPartInstance(IReadOnlyList<WeaponPartDefinition> pool, System.Random prng, List<WeaponPartInstance> resultList, ItemRarity targetRarity, byte level)
        {
            List<WeaponPartDefinition> temp = new(1);
            RollAndAddPart(pool, prng, temp, targetRarity);
            if (temp.Count > 0 && temp[0])
            {
                resultList.Add(new WeaponPartInstance(temp[0], level, isLocked: temp[0].IsUnique));
            }
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
            var parts = weapon.PartInstances;
            string[] partGuids = new string[parts.Count];
            byte[] partLevels = new byte[parts.Count];
            bool[] partLockedFlags = new bool[parts.Count];

            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                partGuids[i] = p.IsValid ? p.Definition.Guid : string.Empty;
                partLevels[i] = p.Level;
                partLockedFlags[i] = p.IsLocked;
            }

            return new ItemNetSnapshot(core.Guid, weapon.Seed, weapon.Level, weapon.Rarity, partGuids, partLevels, partLockedFlags, weapon.DisplayName);
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
                List<WeaponPartInstance> parts = new(snapshot.PartGuids.Length);
                for (int i = 0; i < snapshot.PartGuids.Length; i++)
                {
                    if (TryGetPartByGuid(snapshot.PartGuids[i], out var part) && part)
                    {
                        byte lvl = (snapshot.PartLevels != null && i < snapshot.PartLevels.Length && snapshot.PartLevels[i] > 0)
                            ? snapshot.PartLevels[i]
                            : snapshot.Level;

                        bool locked = (snapshot.PartLockedFlags != null && i < snapshot.PartLockedFlags.Length)
                            ? snapshot.PartLockedFlags[i]
                            : part.IsUnique;

                        parts.Add(new WeaponPartInstance(part, lvl, locked));
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
            ScanProjectAssetsInternal(silent: false);
        }

        public bool ScanProjectAssets(bool silent)
        {
            return ScanProjectAssetsInternal(silent);
        }

        private bool ScanProjectAssetsInternal(bool silent)
        {
            var foundCores = new List<WeaponCoreDefinition>();
            var foundParts = new List<WeaponPartDefinition>();
            var foundPresets = new List<WeaponPresetDefinition>();
            var foundManufacturers = new List<ManufacturerDefinition>();
            var foundBehaviors = new List<ProjectileBehaviorDefinition>();
            var foundPerks = new List<WeaponPerkDefinition>();

            string[] coreGuids = UnityEditor.AssetDatabase.FindAssets("t:WeaponCoreDefinition");
            foreach (var g in coreGuids)
            {
                var core = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponCoreDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (core && !foundCores.Contains(core)) foundCores.Add(core);
            }

            string[] partGuids = UnityEditor.AssetDatabase.FindAssets("t:WeaponPartDefinition");
            foreach (var g in partGuids)
            {
                var part = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponPartDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (part && !foundParts.Contains(part)) foundParts.Add(part);
            }

            string[] presetGuids = UnityEditor.AssetDatabase.FindAssets("t:WeaponPresetDefinition");
            foreach (var g in presetGuids)
            {
                var preset = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponPresetDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (preset && !foundPresets.Contains(preset)) foundPresets.Add(preset);
            }

            string[] mfgGuids = UnityEditor.AssetDatabase.FindAssets("t:ManufacturerDefinition");
            foreach (var g in mfgGuids)
            {
                var mfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ManufacturerDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (mfg && !foundManufacturers.Contains(mfg)) foundManufacturers.Add(mfg);
            }

            string[] behGuids = UnityEditor.AssetDatabase.FindAssets("t:ProjectileBehaviorDefinition");
            foreach (var g in behGuids)
            {
                var beh = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectileBehaviorDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (beh && !foundBehaviors.Contains(beh)) foundBehaviors.Add(beh);
            }

            string[] perkGuids = UnityEditor.AssetDatabase.FindAssets("t:WeaponPerkDefinition");
            foreach (var g in perkGuids)
            {
                var perk = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponPerkDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (perk && !foundPerks.Contains(perk)) foundPerks.Add(perk);
            }

            foundCores.Sort((a, b) => string.Compare(a.CoreId, b.CoreId, StringComparison.OrdinalIgnoreCase));
            foundParts.Sort((a, b) => string.Compare(a.PartId, b.PartId, StringComparison.OrdinalIgnoreCase));
            foundPresets.Sort((a, b) => string.Compare(a.PresetId, b.PresetId, StringComparison.OrdinalIgnoreCase));
            foundManufacturers.Sort((a, b) => string.Compare(a.ManufacturerId, b.ManufacturerId, StringComparison.OrdinalIgnoreCase));
            foundBehaviors.Sort((a, b) => string.Compare(a.BehaviorId, b.BehaviorId, StringComparison.OrdinalIgnoreCase));
            foundPerks.Sort((a, b) => string.Compare(a.PerkId, b.PerkId, StringComparison.OrdinalIgnoreCase));

            bool changed = !AreAssetListsEqual(_weaponCores, foundCores) ||
                           !AreAssetListsEqual(_weaponParts, foundParts) ||
                           !AreAssetListsEqual(_weaponPresets, foundPresets) ||
                           !AreAssetListsEqual(_manufacturers, foundManufacturers) ||
                           !AreAssetListsEqual(_projectileBehaviors, foundBehaviors) ||
                           !AreAssetListsEqual(_weaponPerks, foundPerks);

            if (changed)
            {
                _weaponCores = foundCores;
                _weaponParts = foundParts;
                _weaponPresets = foundPresets;
                _manufacturers = foundManufacturers;
                _projectileBehaviors = foundBehaviors;
                _weaponPerks = foundPerks;

                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
                InitializeLookup();
                OnRegistryUpdated?.Invoke();

                if (!silent)
                {
                    Debug.Log($"[ItemRegistry] Auto-scanned project: {_weaponCores.Count} Cores, {_weaponParts.Count} Parts, {_weaponPresets.Count} Presets, {_manufacturers.Count} Manufacturers, {_projectileBehaviors.Count} Behaviors, {_weaponPerks.Count} Perks.");
                }
                return true;
            }
            else
            {
                InitializeLookup();
                return false;
            }
        }

        private static bool AreAssetListsEqual<T>(List<T> a, List<T> b) where T : UnityEngine.Object
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        public void RegisterCore(WeaponCoreDefinition core)
        {
            if (core && !_weaponCores.Contains(core))
            {
                _weaponCores.Add(core);
                _weaponCores.Sort((a, b) => string.Compare(a.CoreId, b.CoreId, StringComparison.OrdinalIgnoreCase));
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
                InitializeLookup();
                OnRegistryUpdated?.Invoke();
            }
        }

        public void RegisterPart(WeaponPartDefinition part)
        {
            if (part && !_weaponParts.Contains(part))
            {
                _weaponParts.Add(part);
                _weaponParts.Sort((a, b) => string.Compare(a.PartId, b.PartId, StringComparison.OrdinalIgnoreCase));
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
                InitializeLookup();
                OnRegistryUpdated?.Invoke();
            }
        }

        public void RegisterPreset(WeaponPresetDefinition preset)
        {
            if (preset && !_weaponPresets.Contains(preset))
            {
                _weaponPresets.Add(preset);
                _weaponPresets.Sort((a, b) => string.Compare(a.PresetId, b.PresetId, StringComparison.OrdinalIgnoreCase));
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
                InitializeLookup();
                OnRegistryUpdated?.Invoke();
            }
        }

        public void RegisterManufacturer(ManufacturerDefinition manufacturer)
        {
            if (manufacturer && !_manufacturers.Contains(manufacturer))
            {
                _manufacturers.Add(manufacturer);
                _manufacturers.Sort((a, b) => string.Compare(a.ManufacturerId, b.ManufacturerId, StringComparison.OrdinalIgnoreCase));
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
                InitializeLookup();
                OnRegistryUpdated?.Invoke();
            }
        }
#endif
    }
}
