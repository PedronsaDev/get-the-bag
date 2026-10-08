using System;
using System.Collections.Generic;
using NaughtyAttributes;
using Project.Core.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Project.Combat.Data
{
[CreateAssetMenu(fileName = "WEAP_TYPE_NAME", menuName = "Weapons/Weapon Definition", order = 1)]
public class WeaponDefinition : ScriptableObject, IRegistryItem
{
    [Header("Identity")]
    [SerializeField] private string _id;
    [SerializeField] private string _name;

    [Header("Weapon Data")]
    [SerializeField] private WeaponCategory _category = WeaponCategory.Pistol;
    
    [SerializeField] private int _baseMagazineSize = 12;
    [Min(0)] [SerializeField] private int _startingReserveAmmo = 90;
    [SerializeField] private float _baseReloadDuration = 1.5f;
    
    [SerializeField] private int _baseDamage = 10;
    
    [SerializeField] private WeaponFireMode _fireMode = WeaponFireMode.SemiAutomatic;
    [Min(1)] [SerializeField] private int _baseFireRate = 600;
    [Min(0.01f)] [SerializeField] private float _baseRange = 100f;
    
    [ShowIf("_fireMode", WeaponFireMode.Burst)]
    [Min(1)] [SerializeField] private int _burstSize = 3;
    [ShowIf("_fireMode", WeaponFireMode.Burst)]
    [Min(0)] [SerializeField] private float _burstCooldown = 0.25f;
    
    [Header("Custom Fire Mode (Optional)")]
    [SerializeField] private WeaponFireModeDefinition _customFireMode;
    
    [SerializeField] private LayerMask _hitMask = ~0;

    [Header("Compatible Attachments")]
    [SerializeField] private WeaponAttachmentDefinition[] _compatibleAttachments = Array.Empty<WeaponAttachmentDefinition>();

    [Header("Addressables Visual References")]
    [SerializeField] private AssetReferenceT<GameObject> _prefabReference;

    public string Guid => _id;
    public string Id => _id;
    public string Name => string.IsNullOrEmpty(_name) ? name : _name;

    public WeaponCategory Category => _category;
    public int BaseDamage => _baseDamage;
    public int BaseMagazineSize => _baseMagazineSize;
    public float BaseReloadDuration => _baseReloadDuration;
    public int BaseFireRate => _baseFireRate;
    public int StartingReserveAmmo => Mathf.Max(0, _startingReserveAmmo);
    public float BaseRange => _baseRange;
    public LayerMask HitMask => _hitMask;
    public WeaponFireMode FireMode => _fireMode;
    public int BurstSize => Mathf.Max(1, _burstSize);
    public float BurstCooldown => Mathf.Max(0, _burstCooldown);
    public WeaponFireModeDefinition CustomFireMode => _customFireMode;
    public IReadOnlyList<WeaponAttachmentDefinition> CompatibleAttachments => _compatibleAttachments ?? Array.Empty<WeaponAttachmentDefinition>();

    public AssetReferenceT<GameObject> PrefabReference => _prefabReference;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_id))
        {
            _id = name.ToLowerInvariant().Replace(" ", "_");
        }

        _baseFireRate = Mathf.Max(1, _baseFireRate);
        _baseMagazineSize = Mathf.Max(1, _baseMagazineSize);
        _baseDamage = Mathf.Max(0, _baseDamage);
        _baseReloadDuration = Mathf.Max(0.01f, _baseReloadDuration);
        _baseRange = Mathf.Max(0.01f, _baseRange);
    }
#endif
}
}
