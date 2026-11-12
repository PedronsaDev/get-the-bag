using System;
using Project.UI.Match;
using Project.UI.Match.Reveal;
using TMPEffects.Components;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Hub.Editor
{
/// <summary>Adds the threat HUD banner and the results threat summary to UIRoot.prefab.</summary>
public static class ThreatUiSetup
{
    private const string PrefabPath = "Assets/_Project/Resources/UI/UIRoot.prefab";
    private const string HudName = "Threat HUD";
    private const string SummaryTextName = "Summary Text";
    private const string BonusTextName = "Bonus Text";
    private const string FirstSummaryStepName = "Summary - Stealth";

    private const RevealOutcome Played = RevealOutcome.Extracted | RevealOutcome.Lost;

    [MenuItem("Tools/UI/Build Threat HUD In UIRoot")]
    private static void BuildHud()
    {
        EditPrefab(root =>
        {
            if (root.GetComponentInChildren<ThreatHudUI>(true))
                return Fail("UIRoot already has a ThreatHudUI. Delete it first to rebuild.");

            var canvas = FindTransform(root.transform, "Canvas_HUD");
            if (!canvas)
                return Fail("Canvas_HUD not found in UIRoot.");

            var hud = NewRect(HudName, canvas);
            hud.anchorMin = hud.anchorMax = new Vector2(0.5f, 1f);
            hud.pivot = new Vector2(0.5f, 1f);
            hud.anchoredPosition = new Vector2(0f, -110f);
            hud.sizeDelta = new Vector2(900f, 70f);
            var group = hud.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var label = NewRect("Label", hud).gameObject.AddComponent<TextMeshProUGUI>();
            Stretch(label.rectTransform);
            var existing = root.GetComponentInChildren<TMP_Text>(true);
            if (existing) label.font = existing.font;
            label.fontSize = 40f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = "POLICE ARRIVING 0:45";
            label.gameObject.AddComponent<TMPAnimator>();

            var ui = hud.gameObject.AddComponent<ThreatHudUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[ThreatUiSetup] Added '{HudName}' to Canvas_HUD.");
            return true;
        });
    }

    [MenuItem("Tools/UI/Add Threat Summary To Raid Results")]
    private static void AddSummary()
    {
        EditPrefab(root =>
        {
            var results = root.GetComponentInChildren<RaidResultsUI>(true);
            if (!results)
                return Fail("No RaidResultsUI in UIRoot.");

            var steps = FindTransform(results.transform, "Reveal Steps");
            var bankedStep = steps ? FindTransform(steps, "Banked - Count") : null;
            var items = FindText(results.transform, "Items Text");
            var banked = FindText(results.transform, "Banked Total");
            if (!steps || !bankedStep || !items || !banked)
                return Fail("Expected 'Reveal Steps' with a 'Banked - Count' step, plus 'Items Text' and 'Banked Total'.");

            if (FindTransform(steps, FirstSummaryStepName))
                return Fail("The threat summary steps already exist. Delete them first to rebuild.");

            var summary = FindText(results.transform, SummaryTextName) ?? AddTextRowBefore(items, banked, SummaryTextName, 0.8f);
            var bonus = FindText(results.transform, BonusTextName) ?? AddTextRowBefore(items, banked, BonusTextName, 0.8f);

            int index = bankedStep.GetSiblingIndex();

            index = AddType(steps, index, "Summary - Stealth", summary, RevealOutcome.Extracted, RevealCondition.StealthOnly,
                "<!delay=0.04><+grow><color=#7CFC7C>STEALTH</color></+grow>  <size=70%><color=#AAAAAA>{alarmReason}</color></size>");
            index = AddType(steps, index, "Summary - Loud", summary, Played, RevealCondition.LoudOnly,
                "<!delay=0.04><+shake><color=#E04040>LOUD</color></+shake>  <size=70%><color=#AAAAAA>{alarmReason}</color></size>\n" +
                "<!delay=0.015><size=70%><+fade>Assaults survived  {assaults}     Civilians killed  {civilianKills}</+fade></size>");
            index = AddType(steps, index, "Bonus - Stealth", bonus, RevealOutcome.Extracted, RevealCondition.StealthOnly,
                "<!delay=0.02><+move>Stealth bonus  <color=#7CFC7C>+${stealthBonus}</color></+move>");
            index = AddType(steps, index, "Bonus - Civilian Penalty", bonus, RevealOutcome.Extracted, RevealCondition.CiviliansKilled,
                "<!delay=0.02><+move>Civilian penalty  <color=#E04040>-${civilianPenalty}</color></+move>");
            AddWait(steps, index, 0.3f);

            Debug.Log("[ThreatUiSetup] Added the threat summary steps before 'Banked - Count'.");
            return true;
        });
    }

    private static void EditPrefab(Func<GameObject, bool> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (edit(root))
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool Fail(string message)
    {
        EditorUtility.DisplayDialog("Threat UI", message, "OK");
        return false;
    }

    private static int AddType(Transform steps, int index, string name, TMP_Text target, RevealOutcome when,
        RevealCondition condition, string text)
    {
        var step = NewRect(name, steps).gameObject.AddComponent<TypeTextStep>();
        step.transform.SetSiblingIndex(index);
        var so = new SerializedObject(step);
        so.FindProperty("_when").intValue = (int)when;
        so.FindProperty("_condition").enumValueIndex = (int)condition;
        so.FindProperty("_target").objectReferenceValue = target;
        so.FindProperty("_text").stringValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
        return index + 1;
    }

    private static void AddWait(Transform steps, int index, float seconds)
    {
        var step = NewRect("Wait", steps).gameObject.AddComponent<WaitStep>();
        step.transform.SetSiblingIndex(index);
        var so = new SerializedObject(step);
        so.FindProperty("_when").intValue = (int)Played;
        so.FindProperty("_seconds").floatValue = seconds;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Copies the layout row that holds <paramref name="template"/> and puts it above the row holding
    /// <paramref name="before"/>. A row is the text itself when it sits directly in the layout group,
    /// otherwise the container around it.
    /// </summary>
    private static TMP_Text AddTextRowBefore(TMP_Text template, TMP_Text before, string name, float sizeScale)
    {
        var templateRow = RowOf(template.transform);
        var beforeRow = RowOf(before.transform);

        var row = Object.Instantiate(templateRow.gameObject, beforeRow.parent).transform;
        row.SetSiblingIndex(beforeRow.GetSiblingIndex());

        var text = row.GetComponentInChildren<TMP_Text>(true);
        text.name = name;
        if (row != text.transform) row.name = name.Replace(" Text", " Container");
        text.fontSize = Mathf.Max(12f, template.fontSize * sizeScale);
        text.text = name;
        return text;
    }

    private static Transform RowOf(Transform text)
    {
        var parent = text.parent;
        bool parentIsLayout = parent && parent.GetComponent<UnityEngine.UI.LayoutGroup>();
        return parentIsLayout ? text : parent;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Transform FindTransform(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static TMP_Text FindText(Transform root, string name)
    {
        var t = FindTransform(root, name);
        return t ? t.GetComponent<TMP_Text>() : null;
    }
}
}
