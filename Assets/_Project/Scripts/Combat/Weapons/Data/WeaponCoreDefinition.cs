using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Base archetype definition for a weapon category/model.
    /// Dictates baseline unmodded stats, the physical body/receiver prefab (with sockets),
    /// and the cascading pools of compatible parts available for procedural rolling.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponCore_New", menuName = "Combat/Weapons/Weapon Core")]
    public class WeaponCoreDefinition : ScriptableObject
    {
        [BoxGroup("Registry & Identity")]
        [SerializeField, ReadOnly] private string _guid;
        [BoxGroup("Registry & Identity")]
        [Tooltip("Human-readable string identifier used for debug logs, console commands, and spawn cheats.")]
        [SerializeField] private string _coreId = "core_pistol_01";
        [BoxGroup("Registry & Identity")]
        [SerializeField] private string _baseWeaponName = "Combat Pistol";
        [BoxGroup("Registry & Identity")]
        [SerializeField] private WeaponType _weaponType = WeaponType.Pistol;
        [BoxGroup("Registry & Identity")]
        [SerializeField] private ManufacturerDefinition _manufacturer;
        [BoxGroup("Registry & Identity")]
        [SerializeField] private ItemRarity _defaultRarity = ItemRarity.Common;

        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private float _baseDamage = 25f;
        [BoxGroup("Base Archetype Stats")]
        [Tooltip("Fire rate in rounds per minute (RPM). E.g., 600 RPM = 10 rounds/sec, 300 RPM = 5 rounds/sec, 210 RPM = 3.5 rounds/sec.")]
        [SerializeField] private int _baseFireRate = 300;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private int _baseMagazineSize = 12;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private float _baseReloadSpeed = 2.0f;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private float _baseSpread = 1.2f;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private float _maxSpread = 4.5f;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private float _baseRecoilKick = 2.0f;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private float _baseProjectileSpeed = 0f; // 0 = hitscan
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private int _basePelletsPerShot = 1;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private float _baseCriticalMultiplier = 2.0f;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private DamageType _baseElement = DamageType.Physical;
        [BoxGroup("Base Archetype Stats")]
        [SerializeField] private FiringMode _baseFiringMode = FiringMode.SemiAuto;
        [BoxGroup("Base Archetype Stats")]
        [ShowIf(nameof(IsBurst))]
        [SerializeField] private int _baseBurstCount = 3;
        [BoxGroup("Base Archetype Stats")]
        [ShowIf(nameof(IsBurst))]
        [SerializeField] private float _baseBurstInterval = 0.08f;
        [BoxGroup("Base Archetype Stats")]
        [Tooltip("Optional deterministic pellet firing pattern (e.g. 3x3 matrix, horizontal line). Overrides random cone spread.")]
        [SerializeField] private PelletPatternDefinition _basePelletPattern;

        [BoxGroup("Level Scaling Override")]
        [Tooltip("If true, this specific core uses its own custom scaling instead of the global ItemScalingSettings.")]
        [SerializeField] private bool _overrideGlobalScaling = false;
        [BoxGroup("Level Scaling Override")]
        [ShowIf(nameof(_overrideGlobalScaling))]
        [SerializeField] private LevelScalingMode _levelScalingMode = LevelScalingMode.Exponential;
        [BoxGroup("Level Scaling Override")]
        [ShowIf(nameof(ShowScaleRate))]
        [Tooltip("Rate of damage scaling per level (e.g. 0.08 = 8% per level, 0.13 = 13% per level).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _damageScalePerLevel = 0.08f;
        [BoxGroup("Level Scaling Override")]
        [ShowIf(nameof(_overrideGlobalScaling))]
        [SerializeField] private bool _useCustomLevelCurve = false;
        [BoxGroup("Level Scaling Override")]
        [ShowIf(nameof(ShowCurveField))]
        [SerializeField] private AnimationCurve _customLevelCurve = AnimationCurve.Linear(1, 1, 50, 10);

        [BoxGroup("Visual Body Prefab")]
        [Tooltip("Prefab containing the receiver mesh and child transform sockets (e.g. socket_barrel, socket_grip)")]
        [SerializeField] private GameObject _bodyPrefab;

        [BoxGroup("Part Slot Support")]
        [Tooltip("Configure which part slots are physically supported on this weapon body.")]
        [SerializeField] private bool _supportsBarrel = true;
        [BoxGroup("Part Slot Support")]
        [SerializeField] private bool _supportsGrip = true;
        [BoxGroup("Part Slot Support")]
        [SerializeField] private bool _supportsStock = true;
        [BoxGroup("Part Slot Support")]
        [SerializeField] private bool _supportsSight = true;
        [BoxGroup("Part Slot Support")]
        [SerializeField] private bool _supportsMagazine = true;
        [BoxGroup("Part Slot Support")]
        [SerializeField] private bool _supportsAccessory = true;

        [Foldout("Compatible Part Pools")]
        [ReorderableList]
        [SerializeField] private List<WeaponPartDefinition> _compatibleBarrels = new();
        [Foldout("Compatible Part Pools")]
        [ReorderableList]
        [SerializeField] private List<WeaponPartDefinition> _compatibleGrips = new();
        [Foldout("Compatible Part Pools")]
        [ReorderableList]
        [SerializeField] private List<WeaponPartDefinition> _compatibleStocks = new();
        [Foldout("Compatible Part Pools")]
        [ReorderableList]
        [SerializeField] private List<WeaponPartDefinition> _compatibleSights = new();
        [Foldout("Compatible Part Pools")]
        [ReorderableList]
        [SerializeField] private List<WeaponPartDefinition> _compatibleMagazines = new();
        [Foldout("Compatible Part Pools")]
        [ReorderableList]
        [SerializeField] private List<WeaponPartDefinition> _compatibleAccessories = new();

        private bool IsBurst => _baseFiringMode == FiringMode.Burst;
        private bool ShowScaleRate => _overrideGlobalScaling && !_useCustomLevelCurve;
        private bool ShowCurveField => _overrideGlobalScaling && _useCustomLevelCurve;

        public bool SupportsPartSlot(WeaponPartType partType) => partType switch
        {
            WeaponPartType.Barrel => _supportsBarrel,
            WeaponPartType.Grip => _supportsGrip,
            WeaponPartType.Stock => _supportsStock,
            WeaponPartType.Sight => _supportsSight,
            WeaponPartType.Magazine => _supportsMagazine,
            WeaponPartType.Accessory => _supportsAccessory,
            _ => false
        };

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

            // Intelligent defaults based on WeaponType
            if (_weaponType == WeaponType.Pistol)
            {
                // Pistols don't normally have shoulder stocks
                if (_supportsStock && _compatibleStocks.Count == 0)
                {
                    _supportsStock = false;
                }
            }
        }
#endif

        public string Guid => _guid;
        public string CoreId => _coreId;
        public string BaseWeaponName => _baseWeaponName;
        public WeaponType WeaponType => _weaponType;
        public ManufacturerDefinition Manufacturer => _manufacturer;
        public ItemRarity DefaultRarity => _defaultRarity;
        public GameObject BodyPrefab => _bodyPrefab;

        public float BaseDamage => _baseDamage;
        public int BaseFireRate => _baseFireRate;
        public int BaseMagazineSize => _baseMagazineSize;
        public float BaseReloadSpeed => _baseReloadSpeed;
        public float BaseSpread => _baseSpread;
        public float MaxSpread => _maxSpread;
        public float BaseRecoilKick => _baseRecoilKick;
        public float BaseProjectileSpeed => _baseProjectileSpeed;
        public int BasePelletsPerShot => _basePelletsPerShot;
        public float BaseCriticalMultiplier => _baseCriticalMultiplier;
        public DamageType BaseElement => _baseElement;
        public FiringMode BaseFiringMode => _baseFiringMode;
        public int BaseBurstCount => _baseBurstCount;
        public float BaseBurstInterval => _baseBurstInterval;
        public PelletPatternDefinition BasePelletPattern => _basePelletPattern;

        public IReadOnlyList<WeaponPartDefinition> CompatibleBarrels => _compatibleBarrels;
        public IReadOnlyList<WeaponPartDefinition> CompatibleGrips => _compatibleGrips;
        public IReadOnlyList<WeaponPartDefinition> CompatibleStocks => _compatibleStocks;
        public IReadOnlyList<WeaponPartDefinition> CompatibleSights => _compatibleSights;
        public IReadOnlyList<WeaponPartDefinition> CompatibleMagazines => _compatibleMagazines;
        public IReadOnlyList<WeaponPartDefinition> CompatibleAccessories => _compatibleAccessories;

        public bool OverrideGlobalScaling => _overrideGlobalScaling;
        public LevelScalingMode LevelScalingMode => _levelScalingMode;
        public float DamageScalePerLevel => _damageScalePerLevel;
        public bool UseCustomLevelCurve => _useCustomLevelCurve;
        public AnimationCurve CustomLevelCurve => _customLevelCurve;

        /// <summary>
        /// Computes the damage multiplier for a given item level based on global or local scaling configuration.
        /// </summary>
        public float CalculateLevelMultiplier(int level)
        {
            if (!_overrideGlobalScaling)
            {
                return ItemScalingSettings.Instance.CalculateDamageMultiplier(level);
            }

            if (level <= 1) return 1f;

            if (_useCustomLevelCurve && _customLevelCurve != null)
            {
                return Mathf.Max(0.01f, _customLevelCurve.Evaluate(level));
            }

            return _levelScalingMode switch
            {
                LevelScalingMode.Exponential => Mathf.Pow(1f + _damageScalePerLevel, level - 1),
                LevelScalingMode.Linear => 1f + ((level - 1) * _damageScalePerLevel),
                _ => 1f
            };
        }

        /// <summary>
        /// Retrieves the list of compatible parts for a specific part socket type.
        /// </summary>
        public IReadOnlyList<WeaponPartDefinition> GetPoolForPartType(WeaponPartType partType) => partType switch
        {
            WeaponPartType.Barrel => _compatibleBarrels,
            WeaponPartType.Grip => _compatibleGrips,
            WeaponPartType.Stock => _compatibleStocks,
            WeaponPartType.Sight => _compatibleSights,
            WeaponPartType.Magazine => _compatibleMagazines,
            WeaponPartType.Accessory => _compatibleAccessories,
            _ => Array.Empty<WeaponPartDefinition>()
        };
    }
}
