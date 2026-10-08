using System;
using UnityEngine;

namespace Project.Combat.Data
{
public enum WeaponAttachmentSlot
{
    Magazine,
    Muzzle,
    Barrel,
    Stock,
    Grip,
    Sight
}

[Serializable]
public struct WeaponStatModifier
{
    [SerializeField] private float _add;
    [SerializeField] private float _percent;

    public float Add => _add;
    public float Percent => _percent;

    public WeaponStatModifier(float add, float percent = 0)
    {
        _add = add;
        _percent = percent;
    }
}

[CreateAssetMenu(fileName = "MOD_SLOT_NAME", menuName = "Weapons/Attachment Definition")]
public sealed class WeaponAttachmentDefinition : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private WeaponAttachmentSlot _slot;
    [SerializeField] private WeaponStatModifier _damage;
    [SerializeField] private WeaponStatModifier _magazineSize;
    [SerializeField] private WeaponStatModifier _reloadDuration;
    [SerializeField] private WeaponStatModifier _fireRate;
    [SerializeField] private WeaponStatModifier _range;

    public string Id => _id;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
    public WeaponAttachmentSlot Slot => _slot;
    public WeaponStatModifier Damage => _damage;
    public WeaponStatModifier MagazineSize => _magazineSize;
    public WeaponStatModifier ReloadDuration => _reloadDuration;
    public WeaponStatModifier FireRate => _fireRate;
    public WeaponStatModifier Range => _range;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_id)) _id = name.ToLowerInvariant().Replace(" ", "_");
    }
#endif
}
}
