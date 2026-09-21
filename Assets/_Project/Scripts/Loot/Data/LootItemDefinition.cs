using UnityEngine;

namespace Project.Loot
{
    /// <summary>
    /// ScriptableObject defining an individual contraband / lootable valuable item.
    /// Used for raid economy, duffle bag cargo, and extraction value.
    /// </summary>
    [CreateAssetMenu(fileName = "Loot_", menuName = "Loot/Loot Item Definition")]
    public class LootItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private Sprite _icon;

        [Header("Economy & Weight")]
        [SerializeField] private int _baseValue = 100;
        [SerializeField] private float _weightKg = 2.5f;
        [SerializeField] private LootCategory _category = LootCategory.Valuable;
        [SerializeField] private LootRarity _rarity = LootRarity.Common;

        [Header("World Visuals")]
        [SerializeField] private GameObject _worldPrefab;

        public string Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public int BaseValue => _baseValue;
        public float WeightKg => _weightKg;
        public LootCategory Category => _category;
        public LootRarity Rarity => _rarity;
        public GameObject WorldPrefab => _worldPrefab;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_id))
            {
                _id = name.ToLowerInvariant().Replace(" ", "_");
            }
        }
#endif
    }
}
