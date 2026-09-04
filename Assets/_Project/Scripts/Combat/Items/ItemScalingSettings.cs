using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Combat
{
    /// <summary>
    /// Master global scaling configuration shared across all weapons, items,
    /// shields, and enemy balance tiers.
    /// Loaded automatically from Resources or created on demand with intelligent defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemScalingSettings", menuName = "Combat/Items/Global Scaling Settings")]
    public class ItemScalingSettings : ScriptableObject
    {
        public const string ResourcePath = "ItemScalingSettings";

        [Header("Weapon & Item Damage Scaling")]
        [Tooltip("How damage scales as item level increases above Level 1.")]
        [SerializeField] private LevelScalingMode _damageScalingMode = LevelScalingMode.Exponential;

        [Tooltip("Rate of damage scaling per level (e.g. 0.08 = 8% per level, 0.13 = 13% per level like Borderlands 2).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _damageScalePerLevel = 0.08f;

        [Tooltip("Optional custom animation curve for damage scaling across levels 1-100.")]
        [SerializeField] private bool _useCustomDamageCurve = false;
        [SerializeField] private AnimationCurve _customDamageCurve = AnimationCurve.Linear(1, 1, 50, 10);

        [Header("Shield & Armor Scaling")]
        [Tooltip("Rate of shield capacity scaling per level for shields and defensive gear.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _shieldScalePerLevel = 0.08f;

        [Header("Health & Enemy Scaling")]
        [Tooltip("Rate of health scaling per level for destructibles and enemies.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _healthScalePerLevel = 0.08f;

        public LevelScalingMode DamageScalingMode => _damageScalingMode;
        public float DamageScalePerLevel => _damageScalePerLevel;
        public bool UseCustomDamageCurve => _useCustomDamageCurve;
        public AnimationCurve CustomDamageCurve => _customDamageCurve;
        public float ShieldScalePerLevel => _shieldScalePerLevel;
        public float HealthScalePerLevel => _healthScalePerLevel;

        private static ItemScalingSettings _cachedInstance;

        public static ItemScalingSettings Instance
        {
            get
            {
                if (_cachedInstance != null) return _cachedInstance;

                _cachedInstance = Resources.Load<ItemScalingSettings>(ResourcePath);

#if UNITY_EDITOR
                if (_cachedInstance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:ItemScalingSettings");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _cachedInstance = AssetDatabase.LoadAssetAtPath<ItemScalingSettings>(path);
                    }
                    else
                    {
                        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                        {
                            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                                AssetDatabase.CreateFolder("Assets", "_Project");
                            AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                        }
                        _cachedInstance = CreateInstance<ItemScalingSettings>();
                        AssetDatabase.CreateAsset(_cachedInstance, "Assets/_Project/Resources/ItemScalingSettings.asset");
                        AssetDatabase.SaveAssets();
                    }
                }
#endif

                if (_cachedInstance == null)
                {
                    _cachedInstance = CreateInstance<ItemScalingSettings>();
                    _cachedInstance.name = "ItemScalingSettings_RuntimeFallback";
                }

                return _cachedInstance;
            }
        }

        /// <summary>
        /// Calculates the universal damage multiplier for a given item level.
        /// </summary>
        public float CalculateDamageMultiplier(int level)
        {
            if (level <= 1) return 1f;

            if (_useCustomDamageCurve && _customDamageCurve != null)
            {
                return Mathf.Max(0.01f, _customDamageCurve.Evaluate(level));
            }

            return _damageScalingMode switch
            {
                LevelScalingMode.Exponential => Mathf.Pow(1f + _damageScalePerLevel, level - 1),
                LevelScalingMode.Linear => 1f + ((level - 1) * _damageScalePerLevel),
                _ => 1f
            };
        }

        /// <summary>
        /// Calculates the universal shield capacity multiplier for a given item level.
        /// </summary>
        public float CalculateShieldMultiplier(int level)
        {
            if (level <= 1) return 1f;
            return Mathf.Pow(1f + _shieldScalePerLevel, level - 1);
        }

        /// <summary>
        /// Calculates the universal health multiplier for a given entity level.
        /// </summary>
        public float CalculateHealthMultiplier(int level)
        {
            if (level <= 1) return 1f;
            return Mathf.Pow(1f + _healthScalePerLevel, level - 1);
        }
    }
}
