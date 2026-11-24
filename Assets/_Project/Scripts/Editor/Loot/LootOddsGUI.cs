using System.Collections.Generic;
using Project.Definitions.Loot;
using Project.Loot.Spawning;
using UnityEditor;
using UnityEngine;

namespace Project.Loot.Editor
{
internal static class LootOddsGUI
{
    private const string QualityKey = "Loot.PreviewQuality";
    private const string QuantityKey = "Loot.PreviewQuantity";
    private static readonly List<KeyValuePair<LootDefinition, float>> Odds = new();

    /// <summary>Preview modifiers shared by all loot inspectors (editor only, not saved in the scene).</summary>
    public static LootRollModifiers PreviewModifiers => new()
    {
        QuantityMultiplier = SessionState.GetFloat(QuantityKey, 1f),
        QualityBias = SessionState.GetFloat(QualityKey, 0f)
    };

    public static void DrawPreviewSliders()
    {
        EditorGUILayout.LabelField("Preview modifiers (inspector only)", EditorStyles.miniBoldLabel);
        float quantity = EditorGUILayout.Slider("Quantity", SessionState.GetFloat(QuantityKey, 1f), 0f, 3f);
        float quality = EditorGUILayout.Slider("Quality", SessionState.GetFloat(QualityKey, 0f), -3f, 3f);
        SessionState.SetFloat(QuantityKey, quantity);
        SessionState.SetFloat(QualityKey, quality);
    }

    public static float DrawOdds(IReadOnlyList<LootTable> tables, LootRollModifiers mods)
    {
        LootTable.GetOdds(tables, mods, Odds);
        if (Odds.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing can be picked: add entries with loot and a weight above 0.", MessageType.Warning);
            return 0f;
        }

        float expected = 0f;
        foreach (var pair in Odds)
        {
            var loot = pair.Key;
            expected += pair.Value * loot.BaseValue;

            var rect = EditorGUILayout.GetControlRect();
            var swatch = new Rect(rect.x, rect.y + 3, 10, rect.height - 6);
            EditorGUI.DrawRect(swatch, LootGizmoColors.For(loot.Rarity));

            rect.xMin += 16;
            float w = rect.width;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, w * 0.45f, rect.height), loot.Name);
            EditorGUI.LabelField(new Rect(rect.x + w * 0.45f, rect.y, w * 0.25f, rect.height), loot.Rarity.ToString(), EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + w * 0.7f, rect.y, w * 0.15f, rect.height), pair.Value.ToString("P1"));
            EditorGUI.LabelField(new Rect(rect.x + w * 0.85f, rect.y, w * 0.15f, rect.height), $"${loot.BaseValue:N0}", EditorStyles.miniLabel);
        }

        EditorGUILayout.LabelField("Average value per pick", $"${expected:N0}", EditorStyles.boldLabel);
        return expected;
    }
}

[CustomEditor(typeof(LootPool))]
internal class LootPoolEditor : UnityEditor.Editor
{
    private readonly List<LootTable> _tables = new(1);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Odds", EditorStyles.boldLabel);
        LootOddsGUI.DrawPreviewSliders();

        _tables.Clear();
        _tables.Add(((LootPool)target).Table);
        LootOddsGUI.DrawOdds(_tables, LootOddsGUI.PreviewModifiers);
    }
}

[CustomEditor(typeof(LootSpawnPoint)), CanEditMultipleObjects]
internal class LootSpawnPointEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (targets.Length > 1) return;

        var point = (LootSpawnPoint)target;
        var mods = LootOddsGUI.PreviewModifiers;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Odds", EditorStyles.boldLabel);
        LootOddsGUI.DrawPreviewSliders();

        var group = point.GetComponentInParent<LootSpawnGroup>();
        if (group)
            EditorGUILayout.HelpBox($"Controlled by group '{group.name}': only spawns if the group picks it.", MessageType.Info);
        else
            EditorGUILayout.LabelField("Spawn chance", point.EffectiveChance(mods).ToString("P0"));

        float perPick = LootOddsGUI.DrawOdds(point.Tables, mods);
        if (!group)
            EditorGUILayout.LabelField("Average value of this point", $"${perPick * point.EffectiveChance(mods):N0}");
    }
}
}
