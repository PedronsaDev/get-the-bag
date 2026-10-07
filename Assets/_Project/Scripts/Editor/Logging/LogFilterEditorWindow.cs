using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Logging.Editor
{
    /// <summary>
    /// UI Toolkit Editor Window for managing active subsystem logging channels in real time.
    /// </summary>
    public class LogFilterEditorWindow : EditorWindow
    {
        private const string WINDOW_MENU_PATH = "Tools/Logging/Channel Filter";
        private const string ANALYSIS_MENU_PATH = "Window/Analysis/Log Channel Filter";

        private const string UXML_PATH = "Assets/_Project/Scripts/Logging/Editor/LogFilterEditorWindow.uxml";
        private const string USS_PATH = "Assets/_Project/Scripts/Logging/Editor/LogFilterEditorWindow.uss";

        private readonly Dictionary<LogChannel, Toggle> _channelToggles = new();
        private DropdownField _logLevelDropdown;
        private Toggle _passErrorsToggle;
        private Toggle _richTextToggle;
        private ScrollView _channelsScrollView;

        [MenuItem(WINDOW_MENU_PATH, priority = 200)]
        [MenuItem(ANALYSIS_MENU_PATH, priority = 200)]
        public static void Open()
        {
            var window = GetWindow<LogFilterEditorWindow>();
            window.titleContent = new GUIContent("Log Filter");
            window.minSize = new Vector2(320, 420);
            window.Show();
        }

        private void OnEnable()
        {
            var settings = LogSettings.Instance;
            if (settings != null)
            {
                settings.OnSettingsChanged += RefreshUI;
            }
        }

        private void OnDisable()
        {
            var settings = LogSettings.Instance;
            if (settings != null)
            {
                settings.OnSettingsChanged -= RefreshUI;
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (visualTree != null)
            {
                visualTree.CloneTree(root);
            }
            else
            {
                // Fallback basic container if UXML path shifted
                var fallbackContainer = new VisualElement { name = "fallback-root" };
                root.Add(fallbackContainer);
            }

            // Load USS
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(USS_PATH);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // Bind Toolbar Buttons
            var btnEnableAll = root.Q<Button>("btn-enable-all");
            if (btnEnableAll != null)
            {
                btnEnableAll.clicked += () => LogSettings.Instance.SetAllChannels(true);
            }

            var btnDisableAll = root.Q<Button>("btn-disable-all");
            if (btnDisableAll != null)
            {
                btnDisableAll.clicked += () => LogSettings.Instance.SetAllChannels(false);
            }

            var btnReset = root.Q<Button>("btn-reset");
            if (btnReset != null)
            {
                btnReset.clicked += () => LogSettings.Instance.ResetToDefaults();
            }

            // Bind Threshold Dropdown
            _logLevelDropdown = root.Q<DropdownField>("dropdown-log-level");
            if (_logLevelDropdown != null)
            {
                var levelNames = new List<string>(Enum.GetNames(typeof(LogLevel)));
                _logLevelDropdown.choices = levelNames;
                _logLevelDropdown.value = LogSettings.Instance.MinimumLogLevel.ToString();
                _logLevelDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (Enum.TryParse<LogLevel>(evt.newValue, out var parsedLevel))
                    {
                        LogSettings.Instance.MinimumLogLevel = parsedLevel;
                    }
                });
            }

            // Bind Pass Errors Toggle
            _passErrorsToggle = root.Q<Toggle>("toggle-pass-errors");
            if (_passErrorsToggle != null)
            {
                _passErrorsToggle.value = LogSettings.Instance.AlwaysPassWarningsAndErrors;
                _passErrorsToggle.RegisterValueChangedCallback(evt =>
                {
                    LogSettings.Instance.AlwaysPassWarningsAndErrors = evt.newValue;
                });
            }

            // Bind Colored Console Badges Toggle
            _richTextToggle = root.Q<Toggle>("toggle-rich-text");
            if (_richTextToggle != null)
            {
                _richTextToggle.value = LogSettings.Instance.EnableRichTextColor;
                _richTextToggle.RegisterValueChangedCallback(evt =>
                {
                    LogSettings.Instance.EnableRichTextColor = evt.newValue;
                });
            }

            // Populate Channels ScrollView
            _channelsScrollView = root.Q<ScrollView>("channels-scroll-view");
            PopulateChannelRows();
        }

        private void PopulateChannelRows()
        {
            if (_channelsScrollView == null) return;

            _channelsScrollView.Clear();
            _channelToggles.Clear();

            var settings = LogSettings.Instance;

            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                if (channel is LogChannel.None or LogChannel.All) continue;

                var row = new VisualElement();
                row.AddToClassList("channel-row");

                // Color Swatch Badge
                var badge = new VisualElement();
                badge.AddToClassList("channel-color-badge");
                if (ColorUtility.TryParseHtmlString(settings.GetChannelColorHex(channel), out var swatchColor))
                {
                    badge.style.backgroundColor = swatchColor;
                }
                row.Add(badge);

                // Channel Label
                var label = new Label(channel.ToString());
                label.AddToClassList("channel-label");
                row.Add(label);

                // Channel Toggle
                var toggle = new Toggle();
                toggle.AddToClassList("channel-toggle");
                toggle.value = settings.IsChannelEnabled(channel);

                LogChannel capturedChannel = channel;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    settings.SetChannelEnabled(capturedChannel, evt.newValue);
                });

                // Support clicking anywhere on the row to toggle
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is Toggle || (evt.target as VisualElement)?.GetFirstAncestorOfType<Toggle>() != null)
                        return;

                    toggle.value = !toggle.value;
                });

                row.Add(toggle);
                _channelsScrollView.Add(row);
                _channelToggles[channel] = toggle;
            }
        }

        private void RefreshUI()
        {
            var settings = LogSettings.Instance;
            if (settings == null) return;

            if (_logLevelDropdown != null)
            {
                _logLevelDropdown.SetValueWithoutNotify(settings.MinimumLogLevel.ToString());
            }

            if (_passErrorsToggle != null)
            {
                _passErrorsToggle.SetValueWithoutNotify(settings.AlwaysPassWarningsAndErrors);
            }

            if (_richTextToggle != null)
            {
                _richTextToggle.SetValueWithoutNotify(settings.EnableRichTextColor);
            }

            foreach (var kvp in _channelToggles)
            {
                bool isEnabled = settings.IsChannelEnabled(kvp.Key);
                kvp.Value.SetValueWithoutNotify(isEnabled);
            }
        }
    }
}
