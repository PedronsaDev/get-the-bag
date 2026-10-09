using System;
using System.Collections.Generic;
using Project.Combat.Data;
using UnityEditor;
using UnityEngine;

namespace Project.Weapons.Editor
{
public static class WeaponEditorUtilities
{
    public static List<WeaponAttachmentDefinition> FindAllProjectAttachments()
    {
        var list = new List<WeaponAttachmentDefinition>();
        var guids = AssetDatabase.FindAssets("t:WeaponAttachmentDefinition");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<WeaponAttachmentDefinition>(path);
            if (asset && !list.Contains(asset))
                list.Add(asset);
        }
        list.Sort((a, b) =>
        {
            var slotCmp = a.Slot.CompareTo(b.Slot);
            return slotCmp != 0 ? slotCmp : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    public static List<WeaponDefinition> FindAllProjectWeapons()
    {
        var list = new List<WeaponDefinition>();
        var guids = AssetDatabase.FindAssets("t:WeaponDefinition");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (asset && !list.Contains(asset))
                list.Add(asset);
        }
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static void SyncAllWeaponsInProject()
    {
        var attachments = FindAllProjectAttachments();
        var weapons = FindAllProjectWeapons();

        for (var i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];
            if (!weapon) continue;
            weapon.SyncCompatibleAttachments(attachments);
        }

        AssetDatabase.SaveAssets();
    }
}
}
