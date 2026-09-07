using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// ScriptableObject defining an interchangeable modular weapon part.
    /// Can represent standard procedural components (barrels, grips, stocks)
    /// or unique/legendary components granting red-text perks and custom projectiles.
    /// </summary>
    [CreateAssetMenu(fileName = "PART_Pistol_Barrel_New", menuName = "Combat/Weapons/Weapon Part")]
    public class WeaponPartDefinition : ScriptableObject
    {
        [BoxGroup("Part Identity")]
        [SerializeField, ReadOnly] private string _guid;
        [BoxGroup("Part Identity")]
        [Tooltip("Human-readable string identifier used for debug logs, console commands, and spawn cheats.")]
        [SerializeField] private string _partId = "part_pistol_barrel_new";
        [BoxGroup("Part Identity")]
        [SerializeField] private string _displayName = "Standard Part";
        [BoxGroup("Part Identity")]
        [SerializeField] private WeaponPartType _partType = WeaponPartType.Barrel;
        [BoxGroup("Part Identity")]
        [HideIf(nameof(_isUniversal))]
        [SerializeField] private WeaponType _weaponType = WeaponType.Pistol;
        [BoxGroup("Part Identity")]
        [Tooltip("If true, this part can be mounted on any weapon category (e.g. universal sights or accessories).")]
        [SerializeField] private bool _isUniversal = false;
        [BoxGroup("Part Identity")]
        [SerializeField] private ManufacturerDefinition _manufacturer;
        [BoxGroup("Part Identity")]
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;

        [BoxGroup("Visual Mounting")]
        [SerializeField] private GameObject _meshPrefab;
        [BoxGroup("Visual Mounting")]
        [ShowIf(nameof(IsAccessory))]
        [Tooltip("Custom socket override (e.g. socket_muzzle, socket_underbarrel_attachment). Only used for Accessories; standard parts automatically route by PartType.")]
        [SerializeField] private string _customSocketName;
        [BoxGroup("Visual Mounting")]
        [ShowIf(nameof(IsAccessory))]
        [Tooltip("If true, when this accessory is mounted, its fire_point or muzzle socket will override the weapon's fire point (e.g. Silencers, Compensators, Extended Muzzles).")]
        [SerializeField] private bool _overridesFirePoint;

        [BoxGroup("Unique & Legendary Flavors")]
        [SerializeField] private bool _isUnique;
        [BoxGroup("Unique & Legendary Flavors")]
        [ShowIf(nameof(_isUnique))]
        [ResizableTextArea]
        [SerializeField] private string _redText;
        //[BoxGroup("Unique & Legendary Flavors")]
        //[ShowIf(nameof(_isUnique))]
        //[SerializeField] private string _uniqueBehaviorId;

        [BoxGroup("Modular Behaviors & Perks")]
        [SerializeField] private List<ProjectileBehaviorDefinition> _projectileBehaviors = new();
        [BoxGroup("Modular Behaviors & Perks")]
        [SerializeField] private List<WeaponPerkDefinition> _weaponPerks = new();
        [BoxGroup("Modular Behaviors & Perks")]
        [SerializeField] private PelletPatternDefinition _pelletPattern;

        [BoxGroup("Behavior Overrides")]
        [SerializeField] private bool _overridesElement;
        [BoxGroup("Behavior Overrides")]
        [ShowIf(nameof(_overridesElement))]
        [SerializeField] private DamageType _elementOverride = DamageType.Physical;
        [BoxGroup("Behavior Overrides")]
        [SerializeField] private bool _overridesFiringMode;
        [BoxGroup("Behavior Overrides")]
        [ShowIf(nameof(_overridesFiringMode))]
        [SerializeField] private FiringMode _firingModeOverride = FiringMode.FullAuto;

        [BoxGroup("Stat Modifiers")]
        [ReorderableList]
        [SerializeField] private List<StatModifier> _modifiers = new();

        private bool IsAccessory => _partType == WeaponPartType.Accessory;

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

            // Standard parts automatically route by PartType; clear custom socket if not an Accessory
            if (_partType != WeaponPartType.Accessory && !string.IsNullOrEmpty(_customSocketName))
            {
                _customSocketName = null;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        public string Guid => _guid;
        public string PartId => _partId;
        public string DisplayName => _displayName;
        public WeaponPartType PartType => _partType;
        public WeaponType WeaponType => _weaponType;
        public bool IsUniversal => _isUniversal;
        public bool IsCompatibleWith(WeaponType targetType) => _isUniversal || _weaponType == targetType;
        public ManufacturerDefinition Manufacturer => _manufacturer;
        public ItemRarity Rarity => _rarity;
        public bool IsUnique => _isUnique;
        public string RedText => _redText;
        //public string UniqueBehaviorId => _uniqueBehaviorId;
        public IReadOnlyList<ProjectileBehaviorDefinition> ProjectileBehaviors => _projectileBehaviors;
        public IReadOnlyList<WeaponPerkDefinition> WeaponPerks => _weaponPerks;
        public GameObject MeshPrefab => _meshPrefab;
        public string CustomSocketName => _customSocketName;
        public bool OverridesFirePoint => _overridesFirePoint;
        public bool IsMuzzleAttachment => _partType == WeaponPartType.Accessory && (_overridesFirePoint || IsMuzzleSocketName(TargetSocketName));

        public static bool IsMuzzleSocketName(string socketName)
        {
            if (string.IsNullOrEmpty(socketName)) return false;
            return socketName.Equals(WeaponSocketNames.Muzzle, StringComparison.OrdinalIgnoreCase) ||
                   socketName.Equals(WeaponSocketNames.MuzzleAttachment, StringComparison.OrdinalIgnoreCase) ||
                   socketName.Equals(WeaponSocketNames.Silencer, StringComparison.OrdinalIgnoreCase) ||
                   socketName.Equals(WeaponSocketNames.FirePoint, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Automatically resolves the target socket transform name.
        /// Standard parts (Barrel, Grip, Stock, Sight, Mag) automatically use their dedicated
        /// socket based on PartType, completely preventing manual typo errors.
        /// Accessories can optionally specify a custom socket (e.g. socket_muzzle, socket_underbarrel_attachment).
        /// </summary>
        public string TargetSocketName
        {
            get
            {
                if (_partType == WeaponPartType.Accessory && !string.IsNullOrWhiteSpace(_customSocketName))
                {
                    return _customSocketName.Trim();
                }

                return _partType switch
                {
                    WeaponPartType.Barrel => WeaponSocketNames.Barrel,
                    WeaponPartType.Grip => WeaponSocketNames.Grip,
                    WeaponPartType.Stock => WeaponSocketNames.Stock,
                    WeaponPartType.Sight => WeaponSocketNames.Sight,
                    WeaponPartType.Magazine => WeaponSocketNames.Magazine,
                    WeaponPartType.Accessory => WeaponSocketNames.Accessory,
                    _ => WeaponSocketNames.Barrel
                };
            }
        }
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;
        public bool OverridesElement => _overridesElement;
        public DamageType ElementOverride => _elementOverride;
        public bool OverridesFiringMode => _overridesFiringMode;
        public FiringMode FiringModeOverride => _firingModeOverride;
        public PelletPatternDefinition PelletPattern => _pelletPattern;
    }
}
