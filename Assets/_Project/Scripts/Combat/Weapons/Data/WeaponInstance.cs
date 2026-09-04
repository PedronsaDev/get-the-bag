using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Live runtime instance of an assembled weapon.
    /// Manages compiled stats, ammo state, procedural name generation, and red-text perks.
    /// </summary>
    [Serializable]
    public class WeaponInstance
    {
        [SerializeField] private WeaponCoreDefinition _core;
        [SerializeField] private List<WeaponPartDefinition> _parts = new();
        [SerializeField] private uint _seed;
        [SerializeField] private byte _level = 1;
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;
        [SerializeField] private CompiledWeaponStats _compiledStats;
        [SerializeField] private int _currentAmmoInMag;
        [SerializeField] private string _displayName;
        [SerializeField] private string _redText;
        [SerializeField] private List<string> _uniqueBehaviorIds = new();

        public WeaponCoreDefinition Core => _core;
        public IReadOnlyList<WeaponPartDefinition> Parts => _parts;
        public uint Seed => _seed;
        public byte Level => _level;
        public ItemRarity Rarity => _rarity;
        public CompiledWeaponStats CompiledStats => _compiledStats;
        public int CurrentAmmoInMag { get => _currentAmmoInMag; set => _currentAmmoInMag = Mathf.Clamp(value, 0, _compiledStats.MagazineSize); }
        public string DisplayName => _displayName;
        public string RedText => _redText;
        public IReadOnlyList<string> UniqueBehaviorIds => _uniqueBehaviorIds;

        public WeaponInstance(WeaponCoreDefinition core, IEnumerable<WeaponPartDefinition> parts, uint seed, byte level = 1, ItemRarity rarity = ItemRarity.Common, string customDisplayName = null)
        {
            _core = core;
            _seed = seed;
            _level = level;
            _rarity = rarity;

            _parts.Clear();
            if (parts != null)
            {
                _parts.AddRange(parts);
            }

            CompileStats();
            _currentAmmoInMag = _compiledStats.MagazineSize;
            GenerateDisplayNameAndPerks(customDisplayName);
        }

        /// <summary>
        /// Compiles all base stats and part modifiers into a single final runtime stat sheet.
        /// </summary>
        public void CompileStats()
        {
            if (!_core) return;

            // Start with base unmodded stats
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

            // Level scaling determined by Core archetype configuration (Exponential, Linear, or Custom Curve)
            damage *= _core.CalculateLevelMultiplier(_level);

            // Apply Part Modifiers
            for (int p = 0; p < _parts.Count; p++)
            {
                var part = _parts[p];
                if (!part) continue;

                if (part.OverridesElement)
                    element = part.ElementOverride;

                if (part.OverridesFiringMode)
                    firingMode = part.FiringModeOverride;

                var mods = part.Modifiers;
                for (int m = 0; m < mods.Count; m++)
                {
                    var mod = mods[m];
                    switch (mod.TargetStat)
                    {
                        case StatType.Damage:
                            damage = mod.Apply(_core.BaseDamage, damage);
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
                var part = _parts[i];
                if (!part) continue;

                // Dynamic rarity promotion: If any part is Unique or higher rarity, upgrade weapon
                if (part.IsUnique)
                {
                    if (_rarity < ItemRarity.Legendary)
                        _rarity = ItemRarity.Legendary;

                    if (!string.IsNullOrEmpty(part.RedText))
                        _redText = part.RedText;

                    if (!string.IsNullOrEmpty(part.UniqueBehaviorId))
                        _uniqueBehaviorIds.Add(part.UniqueBehaviorId);
                }
                else if (part.Rarity > _rarity)
                {
                    _rarity = part.Rarity;
                }

                // Accessories and Barrels provide weapon title prefixes (e.g. "Double Penetrating")
                if (part.PartType == WeaponPartType.Accessory && !string.IsNullOrEmpty(part.DisplayName))
                {
                    prefix = part.DisplayName;
                }
                else if (string.IsNullOrEmpty(prefix) && part.PartType == WeaponPartType.Barrel && !string.IsNullOrEmpty(part.DisplayName))
                {
                    prefix = part.DisplayName;
                }
            }

            string baseTitle = !string.IsNullOrWhiteSpace(customDisplayName) ? customDisplayName : _core.BaseWeaponName;
            if (!string.IsNullOrEmpty(prefix))
            {
                _displayName = $"{prefix} {baseTitle}";
            }
            else
            {
                _displayName = baseTitle;
            }
        }
    }
}
