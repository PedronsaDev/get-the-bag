using System.IO;
using Project.Levels.Data;
using Project.UI.Match;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Hub.Editor
{
public static class LoadingScreenSetup
{
    private const string PrefabPath = "Assets/_Project/Resources/UI/UIRoot.prefab";
    private const string LevelsFolder = "Assets/_Project/Data/Levels";
    private const string ChildName = "Loading Screen";

    [MenuItem("Tools/UI/Build Loading Screen In UIRoot")]
    private static void Build()
    {
        var bank = GetOrCreateLevel("LVL_Bank", "Scene_Bank", "The Bank", "Get in, get the bag, get out.");
        var hub = GetOrCreateLevel("LVL_Hub", "Scene_Hub", "Hideout", string.Empty);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var transition = root.GetComponentInChildren<TransitionUI>(true);
            if (!transition)
            {
                Debug.LogError("[LoadingScreenSetup] No TransitionUI found in UIRoot.prefab.");
                return;
            }

            var container = new SerializedObject(transition).FindProperty("_rootContainer").objectReferenceValue as GameObject;
            Transform parent = container ? container.transform : transition.transform;

            if (parent.Find(ChildName))
            {
                EditorUtility.DisplayDialog("Loading screen", $"'{ChildName}' already exists in UIRoot. Delete it first to rebuild.", "OK");
                return;
            }

            var existingText = root.GetComponentInChildren<TMP_Text>(true);
            TMP_FontAsset font = existingText ? existingText.font : TMP_Settings.defaultFontAsset;

            var screen = NewRect(ChildName, parent);
            Stretch(screen);

            var background = NewRect("Background", screen).gameObject.AddComponent<Image>();
            Stretch(background.rectTransform);
            background.preserveAspect = false;
            background.raycastTarget = false;
            background.enabled = false;

            var shade = NewRect("Shade", screen).gameObject.AddComponent<Image>();
            Stretch(shade.rectTransform);
            shade.color = new Color(0f, 0f, 0f, 0.55f);
            shade.raycastTarget = false;

            var levelName = NewText("Level Name", screen, font, 96f, FontStyles.Bold);
            Place(levelName.rectTransform, new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.34f));

            var tagline = NewText("Tagline", screen, font, 36f, FontStyles.Italic);
            tagline.color = new Color(1f, 1f, 1f, 0.8f);
            Place(tagline.rectTransform, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.2f));

            var status = NewText("Status", screen, font, 28f, FontStyles.Normal);
            status.color = new Color(1f, 1f, 1f, 0.75f);
            Place(status.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.13f));

            var track = NewRect("Progress Track", screen).gameObject.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.15f);
            track.raycastTarget = false;
            Place(track.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.06f));

            var fill = NewRect("Progress Fill", track.rectTransform).gameObject.AddComponent<Image>();
            fill.color = new Color(1f, 0.8f, 0.2f, 1f);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var ui = screen.gameObject.AddComponent<LoadingScreenUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("_levelName").objectReferenceValue = levelName;
            so.FindProperty("_tagline").objectReferenceValue = tagline;
            so.FindProperty("_status").objectReferenceValue = status;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_progressFill").objectReferenceValue = fill.rectTransform;
            var levels = so.FindProperty("_levels");
            levels.arraySize = 2;
            levels.GetArrayElementAtIndex(0).objectReferenceValue = bank;
            levels.GetArrayElementAtIndex(1).objectReferenceValue = hub;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[LoadingScreenSetup] Added the loading screen to UIRoot.prefab and created level definitions in " + LevelsFolder);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static LevelDefinition GetOrCreateLevel(string assetName, string scene, string displayName, string tagline)
    {
        string path = $"{LevelsFolder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
        if (existing) return existing;

        if (!AssetDatabase.IsValidFolder(LevelsFolder))
            Directory.CreateDirectory(LevelsFolder);

        var def = ScriptableObject.CreateInstance<LevelDefinition>();
        var so = new SerializedObject(def);
        so.FindProperty("_sceneName").stringValue = scene;
        so.FindProperty("_displayName").stringValue = displayName;
        so.FindProperty("_tagline").stringValue = tagline;
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        return def;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.color = Color.white;
        text.text = name;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
}
