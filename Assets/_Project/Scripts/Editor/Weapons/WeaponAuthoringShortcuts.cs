using System.IO;
using Project.Combat.Data;
using Project.Logging;
using UnityEditor;
using UnityEngine;

namespace Project.Weapons.Editor
{
public static class WeaponAuthoringShortcuts
{
    [MenuItem("Tools/Weapons/Sync All Weapon Compatibilities", priority = 10)]
    public static void SyncAllWeaponCompatibilities()
    {
        var attachments = WeaponEditorUtilities.FindAllProjectAttachments();
        var weapons = WeaponEditorUtilities.FindAllProjectWeapons();

        for (var i = 0; i < weapons.Count; i++)
        {
            var w = weapons[i];
            if (!w) continue;
            w.SyncCompatibleAttachments(attachments);
        }

        AssetDatabase.SaveAssets();
        AppLog.Log(LogChannel.Combat, $"[Weapon Tooling] Successfully synchronized {weapons.Count} weapons against {attachments.Count} attachments.");
    }

    [MenuItem("Tools/Weapons/Validate All Weapons and Attachments", priority = 20)]
    public static void ValidateAllWeaponsAndAttachments()
    {
        var attachments = WeaponEditorUtilities.FindAllProjectAttachments();
        var weapons = WeaponEditorUtilities.FindAllProjectWeapons();

        var issueCount = 0;

        // Validate Attachments
        var attachmentIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < attachments.Count; i++)
        {
            var a = attachments[i];
            if (!a) continue;

            if (string.IsNullOrWhiteSpace(a.Id))
            {
                AppLog.LogError(LogChannel.Combat, $"Attachment '{a.name}' has an empty or null ID.", a);
                issueCount++;
            }
            else if (!attachmentIds.Add(a.Id))
            {
                AppLog.LogError(LogChannel.Combat, $"Duplicate attachment ID '{a.Id}' found on '{a.name}'.", a);
                issueCount++;
            }

            if (a.CompatibleCategories == WeaponCategoryMask.None)
            {
                AppLog.LogWarning(LogChannel.Combat, $"Attachment '{a.name}' has CompatibleCategories set to None (cannot be equipped on any weapon).", a);
                issueCount++;
            }
        }

        // Validate Weapons
        var weaponIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < weapons.Count; i++)
        {
            var w = weapons[i];
            if (!w) continue;

            if (string.IsNullOrWhiteSpace(w.Id))
            {
                AppLog.LogError(LogChannel.Combat, $"Weapon '{w.name}' has an empty or null ID.", w);
                issueCount++;
            }
            else if (!weaponIds.Add(w.Id))
            {
                AppLog.LogError(LogChannel.Combat, $"Duplicate weapon ID '{w.Id}' found on '{w.name}'.", w);
                issueCount++;
            }

            if (w.BaseDamage <= 0)
            {
                AppLog.LogWarning(LogChannel.Combat, $"Weapon '{w.name}' has base damage <= 0.", w);
                issueCount++;
            }

            if (w.BaseMagazineSize <= 0)
            {
                AppLog.LogError(LogChannel.Combat, $"Weapon '{w.name}' has base magazine size <= 0.", w);
                issueCount++;
            }

            if (w.BaseFireRate <= 0)
            {
                AppLog.LogError(LogChannel.Combat, $"Weapon '{w.name}' has base fire rate <= 0.", w);
                issueCount++;
            }

            if (!w.PrefabReference.RuntimeKeyIsValid())
            {
                AppLog.LogWarning(LogChannel.Combat, $"Weapon '{w.name}' has an unassigned or invalid Addressables PrefabReference.", w);
                issueCount++;
            }
        }

        if (issueCount == 0)
        {
            AppLog.Log(LogChannel.Combat, $"[Weapon Tooling] Validation passed with zero issues! Checked {weapons.Count} weapons and {attachments.Count} attachments.");
        }
        else
        {
            AppLog.LogWarning(LogChannel.Combat, $"[Weapon Tooling] Validation completed with {issueCount} potential issues detected. See console logs above.");
        }
    }

    [MenuItem("Assets/Create/Weapons/Templates/Assault Rifle", priority = 100)]
    public static void CreateAssaultRifleTemplate()
    {
        CreateWeaponAsset("WEAP_Rifle_Template", WeaponCategory.Rifle, 14, 30, 90, 600, 1.8f, 120f, WeaponFireMode.FullAutomatic);
    }

    [MenuItem("Assets/Create/Weapons/Templates/Pistol", priority = 101)]
    public static void CreatePistolTemplate()
    {
        CreateWeaponAsset("WEAP_Pistol_Template", WeaponCategory.Pistol, 20, 15, 60, 450, 1.2f, 50f, WeaponFireMode.SemiAutomatic);
    }

    [MenuItem("Assets/Create/Weapons/Templates/Shotgun", priority = 102)]
    public static void CreateShotgunTemplate()
    {
        CreateWeaponAsset("WEAP_Shotgun_Template", WeaponCategory.Shotgun, 60, 8, 32, 100, 2.5f, 30f, WeaponFireMode.SemiAutomatic);
    }

    [MenuItem("Assets/Create/Weapons/Templates/Sniper", priority = 103)]
    public static void CreateSniperTemplate()
    {
        CreateWeaponAsset("WEAP_Sniper_Template", WeaponCategory.Sniper, 95, 5, 20, 50, 3.0f, 300f, WeaponFireMode.SemiAutomatic);
    }

    [MenuItem("Assets/Create/Weapons/Templates/Extended Magazine Attachment", priority = 120)]
    public static void CreateExtendedMagTemplate()
    {
        CreateAttachmentAsset("MOD_Magazine_Extended_Template", "Extended Magazine", WeaponAttachmentSlot.Magazine, WeaponCategoryMask.Rifle | WeaponCategoryMask.Pistol, new WeaponStatModifier(10f, 0f), default, new WeaponStatModifier(0f, 0.10f), default, default);
    }

    [MenuItem("Assets/Create/Weapons/Templates/Muzzle Brake Attachment", priority = 121)]
    public static void CreateMuzzleBrakeTemplate()
    {
        CreateAttachmentAsset("MOD_Muzzle_Brake_Template", "Muzzle Brake", WeaponAttachmentSlot.Muzzle, WeaponCategoryMask.All, default, default, default, default, new WeaponStatModifier(15f, 0f));
    }

    private static void CreateWeaponAsset(string defaultName, WeaponCategory category, int damage, int magSize, int reserve, int fireRate, float reload, float range, WeaponFireMode fireMode)
    {
        var path = GetActiveFolderPath();
        var fullPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(path, $"{defaultName}.asset"));

        var asset = ScriptableObject.CreateInstance<WeaponDefinition>();

        var so = new SerializedObject(asset);
        so.FindProperty("_id").stringValue = defaultName.ToLowerInvariant();
        so.FindProperty("_name").stringValue = defaultName;
        so.FindProperty("_category").enumValueIndex = (int)category;
        so.FindProperty("_baseDamage").intValue = damage;
        so.FindProperty("_baseMagazineSize").intValue = magSize;
        so.FindProperty("_startingReserveAmmo").intValue = reserve;
        so.FindProperty("_baseFireRate").intValue = fireRate;
        so.FindProperty("_baseReloadDuration").floatValue = reload;
        so.FindProperty("_baseRange").floatValue = range;
        so.FindProperty("_fireMode").enumValueIndex = (int)fireMode;
        so.FindProperty("_autoMatchByCategory").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(asset, fullPath);
        asset.SyncCompatibleAttachments(WeaponEditorUtilities.FindAllProjectAttachments());
        AssetDatabase.SaveAssets();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static void CreateAttachmentAsset(string defaultName, string displayName, WeaponAttachmentSlot slot, WeaponCategoryMask mask, WeaponStatModifier magMod, WeaponStatModifier dmgMod, WeaponStatModifier reloadMod, WeaponStatModifier fireRateMod, WeaponStatModifier rangeMod)
    {
        var path = GetActiveFolderPath();
        var fullPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(path, $"{defaultName}.asset"));

        var asset = ScriptableObject.CreateInstance<WeaponAttachmentDefinition>();

        var so = new SerializedObject(asset);
        so.FindProperty("_id").stringValue = defaultName.ToLowerInvariant();
        so.FindProperty("_displayName").stringValue = displayName;
        so.FindProperty("_slot").enumValueIndex = (int)slot;
        so.FindProperty("_compatibleCategories").intValue = (int)mask;

        SetModifierProperty(so.FindProperty("_magazineSize"), magMod);
        SetModifierProperty(so.FindProperty("_damage"), dmgMod);
        SetModifierProperty(so.FindProperty("_reloadDuration"), reloadMod);
        SetModifierProperty(so.FindProperty("_fireRate"), fireRateMod);
        SetModifierProperty(so.FindProperty("_range"), rangeMod);

        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(asset, fullPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static void SetModifierProperty(SerializedProperty prop, WeaponStatModifier mod)
    {
        prop.FindPropertyRelative("_add").floatValue = mod.Add;
        prop.FindPropertyRelative("_percent").floatValue = mod.Percent;
    }

    private static string GetActiveFolderPath()
    {
        var path = "Assets";
        foreach (var obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
                break;
            }
        }
        return path;
    }
}
}
