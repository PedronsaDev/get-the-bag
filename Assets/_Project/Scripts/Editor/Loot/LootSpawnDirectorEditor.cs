using System.Collections.Generic;
using System.Linq;
using Project.Core.Match;
using Project.Loot.Data;
using Project.Loot.Spawning;
using UnityEditor;
using UnityEngine;

namespace Project.Loot.Editor
{
[CustomEditor(typeof(LootSpawnDirector))]
internal class LootSpawnDirectorEditor : UnityEditor.Editor
{
    private int _runs = 500;
    private string _report;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var director = (LootSpawnDirector)target;
        var points = new List<LootSpawnPoint>();
        var groups = new List<LootSpawnGroup>();
        LootSpawnPlanner.FindInScene(points, groups);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level loot", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Spawn points / groups", $"{points.Count} / {groups.Count}");
        EditorGUILayout.LabelField("Modifiers now", director.EffectiveModifiers.ToString());

        if (GUILayout.Button("Open Loot Placer"))
            EditorApplication.ExecuteMenuItem("Tools/Loot/Loot Placer");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select All Spawn Points"))
                Selection.objects = points.Select(p => (Object)p.gameObject).ToArray();
            if (GUILayout.Button("Points Without Loot"))
                Selection.objects = points.Where(p => !p.Tables.Any(t => t.HasEntries)).Select(p => (Object)p.gameObject).ToArray();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Simulate (random seed each run, uses the modifiers above)", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _runs = Mathf.Clamp(EditorGUILayout.IntField("Runs", _runs), 1, 100000);
            if (GUILayout.Button("Simulate", GUILayout.Width(90)))
                _report = Simulate(points, groups, director.EffectiveModifiers, _runs);
        }

        if (!string.IsNullOrEmpty(_report))
            EditorGUILayout.HelpBox(_report, MessageType.None);
    }

    private static string Simulate(List<LootSpawnPoint> points, List<LootSpawnGroup> groups, LootRollModifiers mods, int runs)
    {
        var plan = new List<PlannedLoot>();
        var rng = new System.Random();
        var perRarity = new Dictionary<LootRarity, int>();
        long totalItems = 0, totalValue = 0;
        int minValue = int.MaxValue, maxValue = 0;

        for (int run = 0; run < runs; run++)
        {
            LootSpawnPlanner.Plan(points, groups, new System.Random(rng.Next()), mods, plan);
            int value = 0;
            foreach (var item in plan)
            {
                value += item.Loot.BaseValue;
                perRarity.TryGetValue(item.Loot.Rarity, out int c);
                perRarity[item.Loot.Rarity] = c + 1;
            }

            totalItems += plan.Count;
            totalValue += value;
            minValue = Mathf.Min(minValue, value);
            maxValue = Mathf.Max(maxValue, value);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{runs} runs with {mods}");
        sb.AppendLine($"Items per run: {(double)totalItems / runs:0.0}");
        sb.AppendLine($"Value per run: avg ${(double)totalValue / runs:N0}, min ${minValue:N0}, max ${maxValue:N0}");
        foreach (var pair in perRarity.OrderBy(p => p.Key))
            sb.AppendLine($"  {pair.Key}: {(double)pair.Value / runs:0.00} per run");
        return sb.ToString().TrimEnd();
    }
}
}
