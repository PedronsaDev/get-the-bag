using Project.Core.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Project.Loot.Data
{
[CreateAssetMenu(fileName = "LOOT_Name", menuName = "Loot/Loot Definition")]
public class LootDefinition : ScriptableObject, IRegistryItem
{
    
    [Header("Identity")]
    [SerializeField] private string _id;
    [SerializeField] private string _name;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;

    [Header("Economy & Weight")] 
    [SerializeField] private int _baseValue = 100;
    [SerializeField] private float _weightKg = 2.5f;
    [SerializeField] private LootCategory _category = LootCategory.Valuable;
    [SerializeField] private LootRarity _rarity = LootRarity.Common;

    [Header("Addressables Visual References")] 
    [SerializeField] private AssetReferenceT<GameObject> _prefabReference;

    public string Guid => _id;
    public string Id => _id;
    
    public string Name => string.IsNullOrEmpty(_name) ? name : _name;
    public string Description => _description;
    public Sprite Icon => _icon;
    public int BaseValue => _baseValue;
    public float WeightKg => _weightKg;
    public LootCategory Category => _category;
    public LootRarity Rarity => _rarity;
    
    public AssetReferenceT<GameObject> PrefabReference => _prefabReference;

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
