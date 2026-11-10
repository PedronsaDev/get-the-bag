using System.Collections.Generic;
using Project.Core.Events;
using Project.Core.Match;
using Project.Loot.Data;
using Project.UI.Match;
using PurrNet;
using UnityEditor;
using UnityEngine;

namespace Project.Hub.Editor
{
public static class ResultsScreenSetup
{
    private const string PreviewMenu = "Tools/UI/Preview Raid Results/";

    [MenuItem(PreviewMenu + "Extracted")]
    private static void PreviewExtracted() => Preview(RaidOutcome.Extracted, true, 3);

    [MenuItem(PreviewMenu + "Left Behind")]
    private static void PreviewLeftBehind() => Preview(RaidOutcome.Extracted, false, 2);

    [MenuItem(PreviewMenu + "Nobody Extracted")]
    private static void PreviewNobody() => Preview(RaidOutcome.NobodyExtracted, false, 2);

    [MenuItem(PreviewMenu + "Wiped")]
    private static void PreviewWiped() => Preview(RaidOutcome.Wiped, false, 4);

    [MenuItem(PreviewMenu + "Timed Out")]
    private static void PreviewTimedOut() => Preview(RaidOutcome.TimedOut, false, 1);

    [MenuItem(PreviewMenu + "Extracted, Empty Bag")]
    private static void PreviewEmpty() => Preview(RaidOutcome.Extracted, true, 0);

    [MenuItem(PreviewMenu + "Skip", false, 100)]
    private static void PreviewSkip()
    {
        var ui = Object.FindAnyObjectByType<RaidResultsUI>();
        if (ui) ui.Skip();
    }

    [MenuItem(PreviewMenu + "Hide", false, 101)]
    private static void PreviewHide()
    {
        var ui = Object.FindAnyObjectByType<RaidResultsUI>();
        if (ui) ui.Hide();
    }

    [MenuItem(PreviewMenu + "Skip", true)]
    [MenuItem(PreviewMenu + "Hide", true)]
    [MenuItem(PreviewMenu + "Extracted", true)]
    [MenuItem(PreviewMenu + "Left Behind", true)]
    [MenuItem(PreviewMenu + "Nobody Extracted", true)]
    [MenuItem(PreviewMenu + "Wiped", true)]
    [MenuItem(PreviewMenu + "Timed Out", true)]
    [MenuItem(PreviewMenu + "Extracted, Empty Bag", true)]
    private static bool CanPreview() => Application.isPlaying;

    private static int _previewRun = 100000;
    private const float PreviewReturnSeconds = 15f;

    private static void Preview(RaidOutcome outcome, bool extracted, int itemCount)
    {
        var channel = Resources.Load<RaidEventChannel>("Event Channels/RaidEventChannel");
        if (!channel)
        {
            Debug.LogError("[ResultsScreenSetup] RaidEventChannel not found in Resources/Event Channels.");
            return;
        }

        var ids = new List<string>();
        var quantities = new List<int>();
        int value = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:LootDefinition"))
        {
            if (ids.Count >= itemCount) break;
            var def = AssetDatabase.LoadAssetAtPath<LootDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (!def || string.IsNullOrEmpty(def.Id)) continue;

            int quantity = ids.Count + 1;
            ids.Add(def.Id);
            quantities.Add(quantity);
            value += def.BaseValue * quantity;
        }

        if (value == 0 && itemCount > 0) value = 12450;

        PlayerID local = NetworkManager.main ? NetworkManager.main.localPlayer : default;
        var me = new RaidPlayerResult
        {
            Player = local,
            Extracted = extracted,
            RunValue = value,
            BankedTotal = (extracted ? value : 0) + 30000,
            ItemIds = ids.ToArray(),
            ItemQuantities = quantities.ToArray()
        };

        channel.RaiseResultsReady(new RaidResults
        {
            RunId = ++_previewRun,
            Outcome = outcome,
            Players = new[] { me }
        });

        var ui = Object.FindAnyObjectByType<RaidResultsUI>();
        if (ui && !RaidManager.Instance) ui.StartPreviewReturnCountdown(PreviewReturnSeconds);
    }
}
}
