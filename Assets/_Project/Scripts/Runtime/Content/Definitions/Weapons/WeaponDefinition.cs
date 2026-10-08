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
    [SerializeField] private string _id;
    [SerializeField] private string _name;

    [Header("Weapon Data")]
    [SerializeField] private WeaponCategory _category = WeaponCategory.Pistol;
    [SerializeField] private int _baseDamage = 10;
    [SerializeField] private int _baseMagazineSize = 12;
    [SerializeField] private float _reloadDuration = 1.5f;
    [Min(1)] [SerializeField] private int _fireRate = 600;
    [Min(0)] [SerializeField] private int _startingReserveAmmo = 90;
    [Min(0.01f)] [SerializeField] private float _range = 100f;
    [SerializeField] private LayerMask _hitMask = ~0;
    [SerializeField] private WeaponFireMode _fireMode = WeaponFireMode.SemiAutomatic;
    [Min(1)] [SerializeField] private int _burstSize = 3;
    [Min(0)] [SerializeField] private float _burstCooldown = 0.25f;
    [SerializeField] private WeaponFireModeDefinition _customFireMode;

    [Header("Addressables Visual References")]
    [SerializeField] private AssetReferenceT<GameObject> _prefabReference;

    public string Guid => _guid;
    public string Id => _id;
    public string Name => string.IsNullOrEmpty(_name) ? name : _name;

    public WeaponCategory Category => _category;
    public int BaseDamage => _baseDamage;
    public int BaseMagazineSize => _baseMagazineSize;
    public float ReloadDuration => _reloadDuration;
    public int FireRate => _fireRate;
    public float FireRateSeconds => 60f / Mathf.Max(1, _fireRate);
    public int StartingReserveAmmo => Mathf.Max(0, _startingReserveAmmo);
    public float Range => _range;
    public LayerMask HitMask => _hitMask;
    public WeaponFireMode FireMode => _fireMode;
    public int BurstSize => Mathf.Max(1, _burstSize);
    public float BurstCooldown => Mathf.Max(0, _burstCooldown);
    public WeaponFireModeDefinition CustomFireMode => _customFireMode;

    public AssetReferenceT<GameObject> PrefabReference => _prefabReference;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_id))
        {
            _id = name.ToLowerInvariant().Replace(" ", "_");
        }

        _fireRate = Mathf.Max(1, _fireRate);
        _baseMagazineSize = Mathf.Max(1, _baseMagazineSize);
        _baseDamage = Mathf.Max(0, _baseDamage);
        _reloadDuration = Mathf.Max(0.01f, _reloadDuration);
        _range = Mathf.Max(0.01f, _range);
    }
#endif
}
}
