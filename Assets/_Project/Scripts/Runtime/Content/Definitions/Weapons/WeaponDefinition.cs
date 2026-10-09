using System;
using System.Collections.Generic;
using NaughtyAttributes;
using Project.Core.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("_reloadDuration")]
    [SerializeField] private float _baseReloadDuration = 1.5f;
    
    [SerializeField] private int _baseDamage = 10;
    
    [SerializeField] private WeaponFireMode _fireMode = WeaponFireMode.SemiAutomatic;
    [FormerlySerializedAs("_fireRate")]
    [Min(1)] [SerializeField] private int _baseFireRate = 600;
    [FormerlySerializedAs("_range")]
    [Min(0.01f)] [SerializeField] private float _baseRange = 100f;
    
    [ShowIf("_fireMode", WeaponFireMode.Burst)]
    [Min(1)] [SerializeField] private int _burstSize = 3;
    [ShowIf("_fireMode", WeaponFireMode.Burst)]
    [Min(0)] [SerializeField] private float _burstCooldown = 0.25f;
    
    [Header("Custom Fire Mode (Optional)")]
    [SerializeField] private WeaponFireModeDefinition _customFireMode;
    
    [SerializeField] private LayerMask _hitMask = ~0;

    [Header("Attachment Compatibility Settings")]
    [Tooltip("If true, all attachments whose CompatibleCategories include this weapon's category are automatically allowed.")]
    [SerializeField] private bool _autoMatchByCategory = true;
    [Tooltip("Explicit attachments allowed outside category rules.")]
    [SerializeField] private WeaponAttachmentDefinition[] _additionalAttachments = Array.Empty<WeaponAttachmentDefinition>();
    [Tooltip("Explicit attachments excluded even if matching category.")]
    [SerializeField] private WeaponAttachmentDefinition[] _excludedAttachments = Array.Empty<WeaponAttachmentDefinition>();

    [Header("Compatible Attachments (Cached / Serialized)")]
    [SerializeField] private WeaponAttachmentDefinition[] _compatibleAttachments = Array.Empty<WeaponAttachmentDefinition>();

    [Header("Addressables Visual References")]
    [SerializeField] private AssetReferenceT<GameObject> _prefabReference;

    public static Func<string, WeaponAttachmentDefinition> AttachmentLookup { get; set; }

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

    public bool AutoMatchByCategory => _autoMatchByCategory;
    public IReadOnlyList<WeaponAttachmentDefinition> AdditionalAttachments => _additionalAttachments ?? Array.Empty<WeaponAttachmentDefinition>();
    public IReadOnlyList<WeaponAttachmentDefinition> ExcludedAttachments => _excludedAttachments ?? Array.Empty<WeaponAttachmentDefinition>();
    public IReadOnlyList<WeaponAttachmentDefinition> CompatibleAttachments => _compatibleAttachments ?? Array.Empty<WeaponAttachmentDefinition>();

    public AssetReferenceT<GameObject> PrefabReference => _prefabReference;

    public bool IsAttachmentCompatible(WeaponAttachmentDefinition attachment)
    {
        if (!attachment) return false;
        if (_excludedAttachments != null)
        {
            foreach (var t in _excludedAttachments)
            {
                if (t && string.Equals(t.Id, attachment.Id, StringComparison.OrdinalIgnoreCase)) return false;
            }
        }

        if (_autoMatchByCategory && attachment.IsCompatibleWith(_category))
            return true;

        if (_additionalAttachments != null)
        {
            foreach (var t in _additionalAttachments)
            {
                if (t && string.Equals(t.Id, attachment.Id, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        // The serialized cache is for discovery, never a substitute for current rules.
        return false;
    }

#if UNITY_EDITOR
    public void SyncCompatibleAttachments(IEnumerable<WeaponAttachmentDefinition> allProjectAttachments)
    {
        var resolved = new List<WeaponAttachmentDefinition>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var excludedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_excludedAttachments != null)
        {
            foreach (var exc in _excludedAttachments)
            {
                if (exc && !string.IsNullOrEmpty(exc.Id)) excludedIds.Add(exc.Id);
            }
        }

        if (_autoMatchByCategory && allProjectAttachments != null)
        {
            foreach (var att in allProjectAttachments)
            {
                if (!att || string.IsNullOrEmpty(att.Id) || excludedIds.Contains(att.Id)) continue;
                if (att.IsCompatibleWith(_category) && seenIds.Add(att.Id))
                {
                    resolved.Add(att);
                }
            }
        }

        if (_additionalAttachments != null)
        {
            foreach (var add in _additionalAttachments)
            {
                if (!add || string.IsNullOrEmpty(add.Id) || excludedIds.Contains(add.Id)) continue;
                if (seenIds.Add(add.Id))
                {
                    resolved.Add(add);
                }
            }
        }

        resolved.Sort((a, b) =>
        {
            var slotCmp = a.Slot.CompareTo(b.Slot);
            return slotCmp != 0 ? slotCmp : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        _compatibleAttachments = resolved.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
    }

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
