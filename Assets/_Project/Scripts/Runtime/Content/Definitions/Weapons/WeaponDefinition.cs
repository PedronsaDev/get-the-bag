using Project.Core.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Project.Combat.Data
{
[CreateAssetMenu(fileName = "WEAP_TYPE_NAME", menuName = "Weapons/Weapon Definition", order = 1)]
public class WeaponDefinition : ScriptableObject, IRegistryItem
{
    private readonly string _guid = System.Guid.NewGuid().ToString();
    
    [Header("Identity")] 
    [SerializeField] private string _weaponId;
    [SerializeField] private string _name;

    [Header("Addressables Visual References")] 
    [SerializeField] private AssetReferenceT<GameObject> _prefabReference;
    [SerializeField] private AssetReferenceT<Sprite> _iconReference;

    public string Guid => _guid;
    public string Id => _weaponId;
    public string Name => string.IsNullOrEmpty(_name) ? name : _name;

    public AssetReferenceT<GameObject> PrefabReference => _prefabReference;
    public AssetReferenceT<Sprite> IconReference => _iconReference;
}
}
