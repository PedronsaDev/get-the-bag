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
        var typographyBox = CreateSectionBox("Typography & Font Library");
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("GlobalFont"), "Active Font"));

        // Quick Font Switcher Row
        if (_preferences.AvailableFonts != null && _preferences.AvailableFonts.Count > 0)
        {
            var quickFontLabel = new Label("Quick Switch Registered Fonts:")
            {
                style = { fontSize = 11, color = new Color(0.7f, 0.7f, 0.75f), marginTop = 4, marginBottom = 2 }
            };
            typographyBox.Add(quickFontLabel);

            var fontButtonsContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 6 }
            };

            for (int i = 0; i < _preferences.AvailableFonts.Count; i++)
            {
                var entry = _preferences.AvailableFonts[i];
                if (!entry.FontAsset) continue;

                bool isActive = _preferences.GlobalFont == entry.FontAsset;
                var fontBtn = new Button(() =>
                {
                    Undo.RecordObject(_preferences, "Switch Console Font");
                    _preferences.GlobalFont = entry.FontAsset;
                    EditorUtility.SetDirty(_preferences);
                    _preferences.NotifyPreferencesChanged();
                })
                {
                    text = string.IsNullOrEmpty(entry.Name) ? entry.FontAsset.name : entry.Name
                };

                fontBtn.style.height = 24;
                fontBtn.style.marginRight = 4;
                fontBtn.style.marginBottom = 4;
                fontBtn.style.paddingLeft = 8;
                fontBtn.style.paddingRight = 8;
                fontBtn.style.backgroundColor = isActive ? new Color(0.1f, 0.45f, 0.8f) : new Color(0.25f, 0.25f, 0.28f);
                fontBtn.style.color = isActive ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                fontBtn.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;

                fontButtonsContainer.Add(fontBtn);
            }

            typographyBox.Add(fontButtonsContainer);
        }

        // Action Row: Scan Project for Fonts
        var fontActionRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginTop = 4, marginBottom = 6 }
        };

        var scanFontsButton = new Button(ScanProjectForFonts)
        {
            text = "Scan Project for TMP Fonts"
        };
        scanFontsButton.style.flexGrow = 1;
        scanFontsButton.style.height = 26;
        scanFontsButton.style.backgroundColor = new Color(0.2f, 0.35f, 0.25f);
        scanFontsButton.style.color = new Color(0.8f, 1.0f, 0.8f);
        scanFontsButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        fontActionRow.Add(scanFontsButton);
        typographyBox.Add(fontActionRow);

        typographyBox.Add(new PropertyField(serializedObject.FindProperty("AvailableFonts"), "Registered Fonts"));
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("LogTextFontSize"), "Log Text Font Size"));
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("InputTextFontSize"),
            "Search / Input Font Size"));
        typographyBox.Add(new PropertyField(serializedObject.FindProperty("AutocompleteTextFontSize"),
            "Autocomplete Font Size"));
        root.Add(typographyBox);

        // Window & Opacity Section
        var windowBox = CreateSectionBox("Window Frame & Transparency");
        windowBox.Add(new PropertyField(serializedObject.FindProperty("Opacity")));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundMainColor"), "Console Window Background"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundEvenColor"), "Log Even Row Color"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundOddColor"), "Log Odd Row Color"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundHoverTextColor"), "Log Hover Highlight"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundSelectionColor"), "Log Selection Highlight"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundHeaderColor"), "Header Bar Color"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundBottomColor"), "Bottom Bar Color"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("BackgroundAutocompleteColor"), "Autocomplete Background Color"));
        windowBox.Add(new PropertyField(serializedObject.FindProperty("SeparatorColor"), "Separator Line Color"));
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

    private void ScanProjectForFonts()
    {
        Undo.RecordObject(_preferences, "Scan Project for TMP Fonts");
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int addedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
            if (!fontAsset) continue;

            string cleanName = fontAsset.name.Replace(" SDF", "").Replace("-Regular", "");
            bool alreadyExists = false;

            for (int i = 0; i < _preferences.AvailableFonts.Count; i++)
            {
                if (_preferences.AvailableFonts[i].FontAsset == fontAsset)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                _preferences.RegisterFont(cleanName, fontAsset);
                addedCount++;
            }
        }

        EditorUtility.SetDirty(_preferences);
        _preferences.NotifyPreferencesChanged();
        Debug.Log($"[ConsolePreferences] Scanned project for fonts. Added {addedCount} new TMP Font Assets.");
    }
}
}