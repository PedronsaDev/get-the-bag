using System;
using Project.Core.Data;
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
    public bool HasModifier => Mathf.Abs(_add) > 0.0001f || Mathf.Abs(_percent) > 0.0001f;

    public WeaponStatModifier(float add, float percent = 0)
    {
        _add = add;
        _percent = percent;
    }

    public string ToSummary(string statName, bool isInteger = true, bool lowerIsBetter = false)
    {
        if (!HasModifier) return null;
        var parts = new System.Text.StringBuilder();
        if (Mathf.Abs(_add) > 0.0001f)
        {
            var sign = _add > 0 ? "+" : "";
            parts.Append(isInteger ? $"{sign}{Mathf.RoundToInt(_add)}" : $"{sign}{_add:F2}");
        }
        if (Mathf.Abs(_percent) > 0.0001f)
        {
            var sign = _percent > 0 ? "+" : "";
            if (parts.Length > 0) parts.Append(" / ");
            parts.Append($"{sign}{Mathf.RoundToInt(_percent * 100f)}%");
        }
        return $"{statName}: {parts}";
    }
}

[CreateAssetMenu(fileName = "MOD_SLOT_NAME", menuName = "Weapons/Attachment Definition")]
public sealed class WeaponAttachmentDefinition : ScriptableObject, IRegistryItem
{
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private WeaponAttachmentSlot _slot;
    [SerializeField] private WeaponCategoryMask _compatibleCategories = WeaponCategoryMask.All;

    [Header("Modifiers")]
    [SerializeField] private WeaponStatModifier _damage;
    [SerializeField] private WeaponStatModifier _magazineSize;
    [SerializeField] private WeaponStatModifier _reloadDuration;
    [SerializeField] private WeaponStatModifier _fireRate;
    [SerializeField] private WeaponStatModifier _range;

    public string Guid => _id;
    public string Id => _id;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
    public WeaponAttachmentSlot Slot => _slot;
    public WeaponCategoryMask CompatibleCategories => _compatibleCategories;

    public WeaponStatModifier Damage => _damage;
    public WeaponStatModifier MagazineSize => _magazineSize;
    public WeaponStatModifier ReloadDuration => _reloadDuration;
    public WeaponStatModifier FireRate => _fireRate;
    public WeaponStatModifier Range => _range;

    public bool IsCompatibleWith(WeaponCategory category)
    {
        return category.IsInMask(_compatibleCategories);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_id)) _id = name.ToLowerInvariant().Replace(" ", "_");
    }
#endif
}
}
