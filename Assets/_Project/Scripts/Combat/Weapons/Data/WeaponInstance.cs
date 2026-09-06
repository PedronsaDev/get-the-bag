using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Live runtime instance of an assembled weapon in the Gunsmith Circuit system.
    /// Tracks individual part levels, signature locking on legendary components,
    /// physical slot scaling (Barrel + Receiver), and real-time part swapping.
    /// </summary>
    [Serializable]
    public class WeaponInstance
    {
        [SerializeField] private WeaponCoreDefinition _core;
        [SerializeField] private List<WeaponPartInstance> _parts = new();
        [SerializeField] private uint _seed;
        [SerializeField] private byte _level = 1;
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;
        [SerializeField] private CompiledWeaponStats _compiledStats;
        [SerializeField] private int _currentAmmoInMag;
        [SerializeField] private string _displayName;
        [SerializeField] private string _customDisplayName;
        [SerializeField] private string _redText;
        [SerializeField] private List<string> _uniqueBehaviorIds = new();

        public WeaponCoreDefinition Core => _core;
        public IReadOnlyList<WeaponPartInstance> PartInstances => _parts;
        public uint Seed => _seed;
        public byte Level => _level;
        public ItemRarity Rarity => _rarity;
        public CompiledWeaponStats CompiledStats => _compiledStats;
        public int CurrentAmmoInMag { get => _currentAmmoInMag; set => _currentAmmoInMag = Mathf.Clamp(value, 0, _compiledStats.MagazineSize); }
        public string DisplayName => _displayName;
        public string CustomDisplayName => _customDisplayName;
        public string RedText => _redText;
        public IReadOnlyList<string> UniqueBehaviorIds => _uniqueBehaviorIds;

        public event Action<WeaponInstance> OnWeaponAssembled;

        /// <summary>
        /// Backward-compatible list of part definitions.
        /// </summary>
        public IReadOnlyList<WeaponPartDefinition> Parts
        {
            get
            {
                var list = new List<WeaponPartDefinition>(_parts.Count);
                for (int i = 0; i < _parts.Count; i++)
                {
                    if (_parts[i].IsValid) list.Add(_parts[i].Definition);
                }
                return list;
            }
        }

        public WeaponInstance(WeaponCoreDefinition core, uint seed = 1, byte level = 1, ItemRarity rarity = ItemRarity.Common, string customDisplayName = null)
            : this(core, (IEnumerable<WeaponPartInstance>)null, seed, level, rarity, customDisplayName)
        {
        }

        public WeaponInstance(WeaponCoreDefinition core, IEnumerable<WeaponPartInstance> partInstances, uint seed, byte level = 1, ItemRarity rarity = ItemRarity.Common, string customDisplayName = null)
        {
            _core = core;
            _seed = seed;
            _level = (byte)Mathf.Clamp(level, 1, 100);
            _rarity = rarity;
            _customDisplayName = customDisplayName;

            _parts.Clear();
            if (partInstances != null)
            {
                foreach (var p in partInstances)
                {
                    if (p.IsValid) _parts.Add(p);
                }
            }

            CompileStats();
            _currentAmmoInMag = _compiledStats.MagazineSize;
            GenerateDisplayNameAndPerks(customDisplayName);
        }

        /// <summary>
        /// Backward-compatible constructor for part definitions (e.g. from legacy generators or inspectors).
        /// Signature/unique parts automatically initialize with IsLocked = true.
        /// </summary>
        public WeaponInstance(WeaponCoreDefinition core, IEnumerable<WeaponPartDefinition> parts, uint seed, byte level = 1, ItemRarity rarity = ItemRarity.Common, string customDisplayName = null)
        {
            _core = core;
            _seed = seed;
            _level = (byte)Mathf.Clamp(level, 1, 100);
            _rarity = rarity;
            _customDisplayName = customDisplayName;

            _parts.Clear();
            if (parts != null)
            {
                foreach (var def in parts)
                {
                    if (def != null)
                    {
                        _parts.Add(new WeaponPartInstance(def, _level, isLocked: def.IsUnique));
                    }
                }
            }

            CompileStats();
            _currentAmmoInMag = _compiledStats.MagazineSize;
            GenerateDisplayNameAndPerks(customDisplayName);
        }

        // ================= PART SLOT ACCESSORS =================

        public WeaponPartInstance GetPart(WeaponPartType slot)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].IsValid && _parts[i].PartType == slot)
                    return _parts[i];
            }
            return default;
        }

        public bool TryGetPart(WeaponPartType slot, out WeaponPartInstance part)
        {
            part = GetPart(slot);
            return part.IsValid;
        }

        public byte GetPartLevel(WeaponPartType slot)
        {
            if (TryGetPart(slot, out var part))
                return part.Level;
            return _level; // Fallback to core level if slot is empty
        }

        public bool IsPartLocked(WeaponPartType slot)
        {
            return TryGetPart(slot, out var part) && part.IsLocked;
        }

        public bool CanDetachPart(WeaponPartType slot)
        {
            if (!TryGetPart(slot, out var part))
                return false;

            return !part.IsLocked;
        }

        public bool CanAttachPart(WeaponPartType slot, WeaponPartInstance part)
        {
            if (!part.IsValid)
                return false;

            if (part.PartType != slot)
                return false;

            if (!_core || !_core.SupportsPartSlot(slot))
                return false;

            return part.IsCompatibleWith(_core.WeaponType);
        }

        // ================= GUNSMITH SWAPPING & MUTATION =================

        /// <summary>
        /// Attempts to swap or insert a part into the designated slot.
        /// Validates archetype compatibility and signature locks.
        /// </summary>
        public bool TrySwapPart(WeaponPartType slot, WeaponPartInstance newPart, out WeaponPartInstance oldPart)
        {
            oldPart = default;

            if (!CanAttachPart(slot, newPart))
                return false;

            int existingIndex = -1;
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].IsValid && _parts[i].PartType == slot)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                if (_parts[existingIndex].IsLocked)
                    return false; // Signature legendary parts cannot be detached

                oldPart = _parts[existingIndex];
                _parts[existingIndex] = newPart;
            }
            else
            {
                _parts.Add(newPart);
            }

            CompileStats();
            GenerateDisplayNameAndPerks();
            OnWeaponAssembled?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Attempts to detach an unlocked part from the weapon, leaving the slot empty.
        /// </summary>
        public bool TryDetachPart(WeaponPartType slot, out WeaponPartInstance detachedPart)
        {
            detachedPart = default;

            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].IsValid && _parts[i].PartType == slot)
                {
                    if (_parts[i].IsLocked)
                        return false;

                    detachedPart = _parts[i];
                    _parts.RemoveAt(i);

                    CompileStats();
                    GenerateDisplayNameAndPerks();
                    OnWeaponAssembled?.Invoke(this);
                    return true;
                }
            }

            return false;
        }

        // ================= COMPILE STATS & LEVEL SCALING =================

        /// <summary>
        /// Compiles all base stats and part modifiers into a single final runtime stat sheet.
        /// Physical Slot Scaling: Damage scales from Barrel Level and Receiver Level.
        /// </summary>
        public void CompileStats()
        {
            if (!_core) return;

            // 1. Base Unmodded Stats
            float damage = _core.BaseDamage;
            float fireRate = _core.BaseFireRate;
            float magSize = _core.BaseMagazineSize;
            float reloadSpeed = _core.BaseReloadSpeed;
            float baseSpread = _core.BaseSpread;
            float maxSpread = _core.MaxSpread;
            float recoilKick = _core.BaseRecoilKick;
            float projectileSpeed = _core.BaseProjectileSpeed;
            float pellets = _core.BasePelletsPerShot;
            float critMult = _core.BaseCriticalMultiplier;
            DamageType element = _core.BaseElement;
            FiringMode firingMode = _core.BaseFiringMode;
            int burstCount = _core.BaseBurstCount;
            float burstInterval = _core.BaseBurstInterval;

            // 2. Physical Slot Level Scaling (Receiver Level + Barrel Level)
            byte barrelLevel = GetPartLevel(WeaponPartType.Barrel);
            float receiverMultiplier = _core.CalculateLevelMultiplier(_level);
            float barrelMultiplier = _core.CalculateLevelMultiplier(barrelLevel);
            float effectiveLevelMultiplier = (receiverMultiplier + barrelMultiplier) * 0.5f;

            damage *= effectiveLevelMultiplier;
            float scaledBaseDamage = _core.BaseDamage * effectiveLevelMultiplier;

            // 3. Apply Part Modifiers
            for (int p = 0; p < _parts.Count; p++)
            {
                var partInst = _parts[p];
                if (!partInst.IsValid) continue;
                var def = partInst.Definition;

                if (def.OverridesElement)
                    element = def.ElementOverride;

                if (def.OverridesFiringMode)
                    firingMode = def.FiringModeOverride;

                var mods = def.Modifiers;
                for (int m = 0; m < mods.Count; m++)
                {
                    var mod = mods[m];
                    switch (mod.TargetStat)
                    {
                        case StatType.Damage:
                            damage = mod.Apply(scaledBaseDamage, damage);
                            break;
                        case StatType.FireRate:
                            fireRate = mod.Apply(_core.BaseFireRate, fireRate);
                            break;
                        case StatType.MagazineSize:
                            magSize = mod.Apply(_core.BaseMagazineSize, magSize);
                            break;
                        case StatType.ReloadSpeed:
                            reloadSpeed = mod.Apply(_core.BaseReloadSpeed, reloadSpeed);
                            break;
                        case StatType.BaseSpread:
                            baseSpread = mod.Apply(_core.BaseSpread, baseSpread);
                            break;
                        case StatType.MaxSpread:
                            maxSpread = mod.Apply(_core.MaxSpread, maxSpread);
                            break;
                        case StatType.RecoilKick:
                            recoilKick = mod.Apply(_core.BaseRecoilKick, recoilKick);
                            break;
                        case StatType.ProjectileSpeed:
                            projectileSpeed = mod.Apply(_core.BaseProjectileSpeed, projectileSpeed);
                            break;
                        case StatType.PelletsPerShot:
                            pellets = mod.Apply(_core.BasePelletsPerShot, pellets);
                            break;
                        case StatType.CriticalMultiplier:
                            critMult = mod.Apply(_core.BaseCriticalMultiplier, critMult);
                            break;
                    }
                }
            }

            _compiledStats = new CompiledWeaponStats
            {
                Damage = Mathf.Max(1f, damage),
                FireRate = Mathf.Max(0.2f, fireRate),
                MagazineSize = Mathf.Max(1, Mathf.RoundToInt(magSize)),
                ReloadSpeed = Mathf.Max(0.5f, reloadSpeed),
                BaseSpread = Mathf.Max(0.1f, baseSpread),
                MaxSpread = Mathf.Max(baseSpread, maxSpread),
                RecoilKick = Mathf.Max(0.1f, recoilKick),
                ProjectileSpeed = Mathf.Max(0f, projectileSpeed),
                PelletsPerShot = Mathf.Max(1, Mathf.RoundToInt(pellets)),
                CriticalMultiplier = Mathf.Max(1.0f, critMult),
                Element = element,
                FiringMode = firingMode,
                BurstCount = burstCount,
                BurstInterval = burstInterval
            };
        }

        private void GenerateDisplayNameAndPerks(string customDisplayName = null)
        {
            if (!_core) return;

            _uniqueBehaviorIds.Clear();
            _redText = string.Empty;
            string prefix = string.Empty;

            for (int i = 0; i < _parts.Count; i++)
            {
                var partInst = _parts[i];
                if (!partInst.IsValid) continue;
                var def = partInst.Definition;

                // Dynamic rarity promotion: If any part is Unique or higher rarity, upgrade weapon
                if (def.IsUnique)
                {
                    if (_rarity < ItemRarity.Legendary)
                        _rarity = ItemRarity.Legendary;

                    if (!string.IsNullOrEmpty(def.RedText))
                        _redText = def.RedText;

                    if (!string.IsNullOrEmpty(def.UniqueBehaviorId))
                        _uniqueBehaviorIds.Add(def.UniqueBehaviorId);
                }
                else if (def.Rarity > _rarity)
                {
                    _rarity = def.Rarity;
                }

                // Accessories and Barrels provide weapon title prefixes (e.g. "Double Penetrating")
                if (def.PartType == WeaponPartType.Accessory && !string.IsNullOrEmpty(def.DisplayName))
                {
                    prefix = def.DisplayName;
                }
                else if (string.IsNullOrEmpty(prefix) && def.PartType == WeaponPartType.Barrel && !string.IsNullOrEmpty(def.DisplayName))
                {
                    prefix = def.DisplayName;
                }
            }

            if (!string.IsNullOrWhiteSpace(customDisplayName))
            {
                _customDisplayName = customDisplayName;
            }

            string baseTitle = !string.IsNullOrWhiteSpace(_customDisplayName) ? _customDisplayName : _core.BaseWeaponName;
            _displayName = !string.IsNullOrEmpty(prefix) ? $"{prefix} {baseTitle}" : baseTitle;
        }
    }
}
