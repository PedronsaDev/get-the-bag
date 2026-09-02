using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PedronsaDev.Console
{
[CustomEditor(typeof(ConsolePreferences))]
public class ConsolePreferencesEditor : Editor
{
    private ConsolePreferences _preferences;

    private void OnEnable()
    {
        _preferences = (ConsolePreferences)target;
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;
        root.style.paddingLeft = 6;
        root.style.paddingRight = 6;

        // Header Title Card
        var headerCard = new VisualElement
        {
            style =
            {
                backgroundColor = new Color(0.15f, 0.15f, 0.18f),
                paddingTop = 10,
                paddingBottom = 10,
                paddingLeft = 12,
                paddingRight = 12,
                marginBottom = 12,
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6
            }
        };

        var titleLabel = new Label("Console Visual Customization")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 15,
                color = new Color(0.95f, 0.95f, 0.95f)
            }
        };
        headerCard.Add(titleLabel);

        var subtitle = new Label("Switch theme presets with one click or fine-tune individual colors and fonts.")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.7f, 0.7f, 0.75f),
                    marginTop = 3
                }
            };
        headerCard.Add(subtitle);
        root.Add(headerCard);

        // Theme Presets Section
        var presetsBox = CreateSectionBox("Quick Theme Presets");
        var presetRow1 = new VisualElement();
        presetRow1.style.flexDirection = FlexDirection.Row;
        presetRow1.style.marginBottom = 4;

        presetRow1.Add(CreateThemeButton("Default Dark", new Color(0.15f, 0.15f, 0.15f),
            () => ApplyTheme(ThemePreset.DefaultDark)));
        presetRow1.Add(CreateThemeButton("Matrix Green", new Color(0.05f, 0.15f, 0.05f),
            () => ApplyTheme(ThemePreset.Matrix)));
        presetsBox.Add(presetRow1);

        var presetRow2 = new VisualElement();
        presetRow2.style.flexDirection = FlexDirection.Row;
        presetRow2.style.marginBottom = 4;

        presetRow2.Add(CreateThemeButton("Cyberpunk", new Color(0.18f, 0.05f, 0.25f),
            () => ApplyTheme(ThemePreset.Cyberpunk)));
        presetRow2.Add(CreateThemeButton("Dracula", new Color(0.18f, 0.18f, 0.24f),
            () => ApplyTheme(ThemePreset.Dracula)));
        presetsBox.Add(presetRow2);

        var presetRow3 = new VisualElement();
        presetRow3.style.flexDirection = FlexDirection.Row;
        presetRow3.Add(CreateThemeButton("Retro Amber", new Color(0.2f, 0.12f, 0.03f),
            () => ApplyTheme(ThemePreset.Amber)));
        presetsBox.Add(presetRow3);
        root.Add(presetsBox);

        // Typography Section
        var typographyBox = CreateSectionBox("Typography & Font");
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("GlobalFont")));
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("LogTextFontSize"), "Log Text Font Size"));
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("InputTextFontSize"),
            "Search / Input Font Size"));
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("AutocompleteTextFontSize"),
            "Autocomplete Font Size"));
        root.Add(typographyBox);

        // Window & Opacity Section
        var windowBox = CreateSectionBox("Window Frame & Transparency");
        windowBox.Add(new PropertyField(serializedObject.FindProperty("Opacity")));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundMainColor")));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundEvenColor")));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundHeaderColor")));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundBottomColor")));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("SeparatorColor")));
        root.Add(windowBox);

        // Text Colors Section
        var textColorsBox = CreateSectionBox("Log & Text Colors");
        textColorsBox.Add(new PropertyField(serializedObject.FindProperty("LogTextColor")));
        textColorsBox.Add(new PropertyField(serializedObject.FindProperty("CommandTextColor")));
        textColorsBox.Add(new PropertyField(serializedObject.FindProperty("WarningTextColor")));
        textColorsBox.Add(new PropertyField(serializedObject.FindProperty("ErrorTextColor")));
        textColorsBox.Add(new PropertyField(serializedObject.FindProperty("HighlightTextColor")));
        textColorsBox.Add(new PropertyField(serializedObject.FindProperty("AutocompleteTextColor")));
        textColorsBox.Add(new PropertyField(serializedObject.FindProperty("AutocompleteParameterTextColor")));
        root.Add(textColorsBox);

        // Behavior Settings Section
        var behaviorBox = CreateSectionBox("Console Behaviors");
        behaviorBox.Add(new PropertyField(serializedObject.FindProperty("PersistConsole")));
        behaviorBox.Add(new PropertyField(serializedObject.FindProperty("PauseGameOnConsole")));
        behaviorBox.Add(new PropertyField(serializedObject.FindProperty("CacheReloadOnPlayMode")));
        behaviorBox.Add(new PropertyField(serializedObject.FindProperty("SaveConsoleOutput")));
        root.Add(behaviorBox);

        // Reset Button
        var resetButton = new Button(() =>
        {
            Undo.RecordObject(_preferences, "Reset Console Preferences");
            _preferences.Reset();
            EditorUtility.SetDirty(_preferences);
        })
        {
            text = "↺ Reset All to Defaults"
        };
        resetButton.style.marginTop = 12;
        resetButton.style.height = 28;
        root.Add(resetButton);

        return root;
    }

    private VisualElement CreateSectionBox(string title)
    {
        var box = new VisualElement();
        box.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        box.style.paddingTop = 8;
        box.style.paddingBottom = 8;
        box.style.paddingLeft = 8;
        box.style.paddingRight = 8;
        box.style.marginBottom = 10;
        box.style.borderTopLeftRadius = 4;
        box.style.borderTopRightRadius = 4;
        box.style.borderBottomLeftRadius = 4;
        box.style.borderBottomRightRadius = 4;
        box.style.borderLeftWidth = 2;
        box.style.borderLeftColor = new Color(0.3f, 0.6f, 0.9f);

        var header = new Label(title);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.fontSize = 12;
        header.style.color = new Color(0.85f, 0.85f, 0.85f);
        header.style.marginBottom = 6;
        box.Add(header);

        return box;
    }

    private Button CreateThemeButton(string text, Color bgColor, System.Action onClick)
    {
        var btn = new Button(onClick)
        {
            text = text
        };
        btn.style.flexGrow = 1;
        btn.style.height = 26;
        btn.style.backgroundColor = bgColor;
        btn.style.color = Color.white;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.marginRight = 4;
        return btn;
    }

    private void ApplyTheme(ThemePreset preset)
    {
        Undo.RecordObject(_preferences, "Apply Console Theme");
        _preferences.ApplyPreset(preset);
        EditorUtility.SetDirty(_preferences);
    }
}
}