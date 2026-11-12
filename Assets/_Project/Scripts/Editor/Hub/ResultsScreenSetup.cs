using System.Collections.Generic;
using Project.Match;
using Project.Definitions.Loot;
using Project.UI.Match;
using PurrNet;
using UnityEditor;
using UnityEngine;

namespace Project.Hub.Editor
{
public static class ResultsScreenSetup
{
    private const string PreviewMenu = "Tools/UI/Preview Raid Results/";

    // AI AlarmReason values (fixed): GuardAlerted 0, BodyFound 1, CivilianCall 2, CivilianKilled 3.
    private const byte GuardAlerted = 0;
    private const byte CivilianCall = 2;
    private const byte CivilianKilled = 3;
    private const int PreviewStealthBonusPercent = 20;
    private const int PreviewCivilianPenalty = 500;

    [MenuItem(PreviewMenu + "Extracted, Stealth")]
    private static void PreviewExtracted() => Preview(RaidOutcome.Extracted, true, 3);

    [MenuItem(PreviewMenu + "Extracted, Loud")]
    private static void PreviewExtractedLoud() => Preview(RaidOutcome.Extracted, true, 3, GuardAlerted, 0, 2);

    [MenuItem(PreviewMenu + "Extracted, Civilians Killed")]
    private static void PreviewExtractedKills() => Preview(RaidOutcome.Extracted, true, 3, CivilianKilled, 2, 1);

    [MenuItem(PreviewMenu + "Left Behind")]
    private static void PreviewLeftBehind() => Preview(RaidOutcome.Extracted, false, 2, GuardAlerted, 0, 3);

    [MenuItem(PreviewMenu + "Nobody Extracted")]
    private static void PreviewNobody() => Preview(RaidOutcome.NobodyExtracted, false, 2, CivilianCall, 0, 2);

    [MenuItem(PreviewMenu + "Wiped")]
    private static void PreviewWiped() => Preview(RaidOutcome.Wiped, false, 4, GuardAlerted, 1, 3);

    [MenuItem(PreviewMenu + "Timed Out")]
    private static void PreviewTimedOut() => Preview(RaidOutcome.TimedOut, false, 1, CivilianCall, 0, 5);

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
    [MenuItem(PreviewMenu + "Extracted, Stealth", true)]
    [MenuItem(PreviewMenu + "Extracted, Loud", true)]
    [MenuItem(PreviewMenu + "Extracted, Civilians Killed", true)]
    [MenuItem(PreviewMenu + "Left Behind", true)]
    [MenuItem(PreviewMenu + "Nobody Extracted", true)]
    [MenuItem(PreviewMenu + "Wiped", true)]
    [MenuItem(PreviewMenu + "Timed Out", true)]
    [MenuItem(PreviewMenu + "Extracted, Empty Bag", true)]
    private static bool CanPreview() => Application.isPlaying;

    private static int _previewRun = 100000;
    private const float PreviewReturnSeconds = 15f;

    /// <param name="alarmReason">255 = stealth (no alarm).</param>
    private static void Preview(RaidOutcome outcome, bool extracted, int itemCount,
        byte alarmReason = 255, int civilianKills = 0, int assaults = 0)
    {
        var channel = Resources.Load<RaidEventChannel>(RaidEventChannel.ResourcePath);
        if (!channel)
        {
            Debug.LogError("[ResultsScreenSetup] RaidEventChannel not found in Resources/EventChannels.");
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

        bool stealth = alarmReason == 255;
        bool banksLoot = extracted && itemCount > 0;
        var payout = RaidPayout.Calculate(value, banksLoot, stealth, civilianKills,
            PreviewStealthBonusPercent, PreviewCivilianPenalty);

        PlayerID local = NetworkManager.main ? NetworkManager.main.localPlayer : default;
        var me = new RaidPlayerResult
        {
            Player = local,
            Extracted = extracted,
            RunValue = value,
            StealthBonus = payout.StealthBonus,
            CivilianPenalty = payout.CivilianPenalty,
            Payout = payout.Payout,
            BankedTotal = payout.Payout + 30000,
            ItemIds = ids.ToArray(),
            ItemQuantities = quantities.ToArray()
        };

        channel.RaiseResultsReady(new RaidResults
        {
            RunId = ++_previewRun,
            Outcome = outcome,
            Players = new[] { me },
            Stealth = stealth,
            AlarmReason = alarmReason,
            CivilianKills = civilianKills,
            AssaultsReached = assaults
        });

        var ui = Object.FindAnyObjectByType<RaidResultsUI>();
        if (ui && !RaidManager.Instance) ui.StartPreviewReturnCountdown(PreviewReturnSeconds);
    }
}
}
