using System;
using Project.UI.Match;
using Project.UI.Match.Reveal;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Hub.Editor
{
public static class RevealStepsBuilder
{
    private const string PrefabPath = "Assets/_Project/Resources/UI/UIRoot.prefab";
    private const string RootName = "Reveal Steps";

    private const RevealOutcome Everything = (RevealOutcome)~0;
    private const RevealOutcome Played = RevealOutcome.Extracted | RevealOutcome.Lost;

    [MenuItem("Tools/UI/Build Raid Results Reveal Steps")]
    private static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var ui = root.GetComponentInChildren<RaidResultsUI>(true);
            if (!ui)
            {
                Debug.LogError("[RevealStepsBuilder] No RaidResultsUI found in UIRoot.prefab.");
                return;
            }

            if (ui.transform.Find(RootName))
            {
                EditorUtility.DisplayDialog("Reveal steps", $"'{RootName}' already exists. Delete it first to rebuild.", "OK");
                return;
            }

            var outcome = Find(ui.transform, "Outcome Text");
            var items = Find(ui.transform, "Items Text");
            var take = Find(ui.transform, "Run Value Text");
            var banked = Find(ui.transform, "Banked Total");
            var returnText = Find(ui.transform, "Return Text");
            if (!outcome || !items || !take || !banked || !returnText)
            {
                Debug.LogError("[RevealStepsBuilder] Expected 'Outcome Text', 'Items Text', 'Run Value Text', 'Banked Total' and 'Return Text' under the Raid Results UI.");
                return;
            }

            var stepsRoot = NewChild(RootName, ui.transform);
            var sequence = stepsRoot.gameObject.AddComponent<RevealSequence>();

            Header(stepsRoot, "Header - Extracted", outcome, RevealOutcome.Extracted,
                "<!delay=0.06><+grow><wave>JOB COMPLETE</wave></+grow>\n<!wait=0.3><!delay=0.02><size=55%><+fade>You made it out with the bag.</+fade></size>");
            Header(stepsRoot, "Header - Left Behind", outcome, RevealOutcome.LeftBehind,
                "<!delay=0.08><+fade><dangle>LEFT BEHIND</dangle></+fade>\n<!wait=0.3><!delay=0.02><size=55%><+fade>The crew got out without you.</+fade></size>");
            Header(stepsRoot, "Header - Nobody Out", outcome, RevealOutcome.NobodyExtracted,
                "<!delay=0.08><+move><color=#E0A040>NOBODY GOT OUT</color></+move>\n<!wait=0.3><!delay=0.02><size=55%><+fade>The van left empty.</+fade></size>");
            Header(stepsRoot, "Header - Wiped", outcome, RevealOutcome.Wiped,
                "<!delay=0.1><+shake><color=#E04040><shake>CREW DOWN</shake></color></+shake>\n<!wait=0.4><!delay=0.02><size=55%><+fade>Everything in the bags is gone.</+fade></size>");
            Header(stepsRoot, "Header - Timed Out", outcome, RevealOutcome.TimedOut,
                "<!delay=0.08><+fade><color=#E0A040><swing>OUT OF TIME</swing></color></+fade>\n<!wait=0.3><!delay=0.02><size=55%><+fade>The police locked the place down.</+fade></size>");
            Header(stepsRoot, "Header - No Data", outcome, RevealOutcome.NoData, "<+fade>RAID OVER</+fade>");

            Wait(stepsRoot, 0.4f);

            Add<HaulListStep>(stepsRoot, "Haul", Played, so =>
            {
                so.FindProperty("_target").objectReferenceValue = items;
                so.FindProperty("_title").stringValue = "<!delay=0.03><size=70%><color=#AAAAAA>HAUL</color></size>\n";
                so.FindProperty("_itemLine").stringValue = "<!delay=0.015><+move>{name}  <color=#FFD060>x{qty}</color>  <size=70%><color=#AAAAAA>${value}</color></size></+move><!wait=0.12>\n";
                so.FindProperty("_lostItemLine").stringValue = "<!delay=0.015><+fade><color=#777777><s>{name}  x{qty}</s></color></+fade><!wait=0.08>\n";
                so.FindProperty("_emptyLine").stringValue = "<+fade><i><color=#AAAAAA>Empty bag.</color></i></+fade>";
            });

            Wait(stepsRoot, 0.4f);

            Add<CountUpStep>(stepsRoot, "Take - Count", RevealOutcome.Extracted, so =>
            {
                so.FindProperty("_target").objectReferenceValue = take;
                so.FindProperty("_toToken").stringValue = "take";
                so.FindProperty("_fromToken").stringValue = "";
                so.FindProperty("_countingText").stringValue = "TAKE  <color=#FFD060>${value}</color>";
                so.FindProperty("_finalText").stringValue = "TAKE  <color=#FFD060><jump>${value}</jump></color>";
                so.FindProperty("_duration").floatValue = 1.2f;
            });
            Add<TypeTextStep>(stepsRoot, "Take - Lost", RevealOutcome.Lost, so =>
            {
                so.FindProperty("_target").objectReferenceValue = take;
                so.FindProperty("_text").stringValue = "TAKE  <color=#777777><s>${take}</s></color>  <color=#E04040><shake>LOST</shake></color>";
                so.FindProperty("_instant").boolValue = true;
            });
            Punch(stepsRoot, "Take - Punch", take.transform);

            Wait(stepsRoot, 0.4f);

            Add<CountUpStep>(stepsRoot, "Banked - Count", RevealOutcome.Extracted, so =>
            {
                so.FindProperty("_target").objectReferenceValue = banked;
                so.FindProperty("_toToken").stringValue = "banked";
                so.FindProperty("_fromToken").stringValue = "bankedBefore";
                so.FindProperty("_countingText").stringValue = "BANKED  ${value}";
                so.FindProperty("_finalText").stringValue = "BANKED  <wave>${value}</wave>";
                so.FindProperty("_duration").floatValue = 1.2f;
            });
            Add<TypeTextStep>(stepsRoot, "Banked - Unchanged", RevealOutcome.Lost, so =>
            {
                so.FindProperty("_target").objectReferenceValue = banked;
                so.FindProperty("_text").stringValue = "BANKED  ${banked}";
                so.FindProperty("_instant").boolValue = true;
            });
            Punch(stepsRoot, "Banked - Punch", banked.transform, RevealOutcome.Extracted);

            Add<ReturnCountdownStep>(stepsRoot, "Return Countdown", Everything, so =>
            {
                so.FindProperty("_target").objectReferenceValue = returnText;
                so.FindProperty("_text").stringValue = "<size=60%><color=#AAAAAA>Back to the hideout in {seconds}</color></size>";
            });

            if (!ui.TryGetComponent(out CanvasGroup group))
                group = ui.gameObject.AddComponent<CanvasGroup>();

            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("_sequence").objectReferenceValue = sequence;
            uiSo.FindProperty("_canvasGroup").objectReferenceValue = group;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[RevealStepsBuilder] Built '{RootName}' with {stepsRoot.childCount} steps under the Raid Results UI.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Header(Transform parent, string name, TMP_Text target, RevealOutcome when, string text) =>
        Add<TypeTextStep>(parent, name, when, so =>
        {
            so.FindProperty("_target").objectReferenceValue = target;
            so.FindProperty("_text").stringValue = text;
        });

    private static void Wait(Transform parent, float seconds) =>
        Add<WaitStep>(parent, "Wait", Played, so => so.FindProperty("_seconds").floatValue = seconds);

    private static void Punch(Transform parent, string name, Transform target, RevealOutcome when = Played) =>
        Add<PunchStep>(parent, name, when, so =>
        {
            so.FindProperty("_target").objectReferenceValue = target;
            so.FindProperty("_strength").floatValue = 0.2f;
            so.FindProperty("_duration").floatValue = 0.35f;
            so.FindProperty("_dontWait").boolValue = true;
        });

    private static void Add<T>(Transform parent, string name, RevealOutcome when, Action<SerializedObject> configure)
        where T : RevealStep
    {
        var step = NewChild(name, parent).gameObject.AddComponent<T>();
        var so = new SerializedObject(step);
        so.FindProperty("_when").intValue = (int)when;
        configure(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static TMP_Text Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name && t.TryGetComponent(out TMP_Text text))
                return text;
        return null;
    }
}
}
