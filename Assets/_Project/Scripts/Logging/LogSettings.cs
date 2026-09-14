using System;
using UnityEngine;

namespace Project.Logging
{
    /// <summary>
    /// Configuration asset for filtering and styling runtime logging channels.
    /// Supports runtime mutation, hot reloading, and fast bitmask checks.
    /// </summary>
    [CreateAssetMenu(fileName = "LogSettings", menuName = "Project/Logging/Log Settings")]
    public class LogSettings : ScriptableObject
    {
        private const string ResourcePath = "Logging/LogSettings";

        private static LogSettings _instance;

        public static LogSettings Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = Resources.Load<LogSettings>(ResourcePath);
#if UNITY_EDITOR
                    if (!_instance)
                    {
                        string dir = "Assets/_Project/Resources/Logging";
                        string assetPath = $"{dir}/LogSettings.asset";
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<LogSettings>(assetPath);
                        if (!_instance)
                        {
                            if (!System.IO.Directory.Exists(dir))
                            {
                                System.IO.Directory.CreateDirectory(dir);
                            }
                            _instance = CreateInstance<LogSettings>();
                            UnityEditor.AssetDatabase.CreateAsset(_instance, assetPath);
                            UnityEditor.AssetDatabase.SaveAssets();
                        }
                    }
#endif
                    if (!_instance)
                    {
                        _instance = CreateInstance<LogSettings>();
                        _instance.name = "LogSettings (Runtime Default)";
                    }
                }
                return _instance;
            }
        }

        [Header("Channel Filtering")]
        [SerializeField] private LogChannel _enabledChannels = LogChannel.All;
        [SerializeField] private LogLevel _minimumLogLevel = LogLevel.Info;
        [SerializeField] private bool _alwaysPassWarningsAndErrors = true;

        [Header("Formatting")]
        [SerializeField] private bool _enableRichTextColor = true;
        [SerializeField] private bool _includeFrameCount = false;

        [Header("Channel Colors (Hex)")]
        [SerializeField] private string _colorGeneral = "#94A3B8";
        [SerializeField] private string _colorNetwork = "#38BDF8";
        [SerializeField] private string _colorMatch = "#FBBF24";
        [SerializeField] private string _colorCombat = "#F43F5E";
        [SerializeField] private string _colorPlayer = "#4ADE80";
        [SerializeField] private string _colorAI = "#A855F7";
        [SerializeField] private string _colorUI = "#818CF8";
        [SerializeField] private string _colorPersistence = "#FB923C";
        [SerializeField] private string _colorAudio = "#E879F9";

        public event Action OnSettingsChanged;

        public LogChannel EnabledChannels
        {
            get => _enabledChannels;
            set
            {
                if (_enabledChannels != value)
                {
                    _enabledChannels = value;
                    NotifyChanged();
                }
            }
        }

        public LogLevel MinimumLogLevel
        {
            get => _minimumLogLevel;
            set
            {
                if (_minimumLogLevel != value)
                {
                    _minimumLogLevel = value;
                    NotifyChanged();
                }
            }
        }

        public bool AlwaysPassWarningsAndErrors
        {
            get => _alwaysPassWarningsAndErrors;
            set
            {
                if (_alwaysPassWarningsAndErrors != value)
                {
                    _alwaysPassWarningsAndErrors = value;
                    NotifyChanged();
                }
            }
        }

        public bool EnableRichTextColor
        {
            get => _enableRichTextColor;
            set
            {
                if (_enableRichTextColor != value)
                {
                    _enableRichTextColor = value;
                    NotifyChanged();
                }
            }
        }

        public bool IncludeFrameCount => _includeFrameCount;

        public bool IsChannelEnabled(LogChannel channel)
        {
            return (_enabledChannels & channel) != 0;
        }

        public bool IsChannelEnabled(LogChannel channel, LogLevel level)
        {
            if (_alwaysPassWarningsAndErrors && level >= LogLevel.Warning)
                return true;

            if (level < _minimumLogLevel)
                return false;

            return (_enabledChannels & channel) != 0;
        }

        public void SetChannelEnabled(LogChannel channel, bool enabled)
        {
            if (enabled)
                _enabledChannels |= channel;
            else
                _enabledChannels &= ~channel;

            NotifyChanged();
        }

        public bool ToggleChannel(LogChannel channel)
        {
            bool newState = !IsChannelEnabled(channel);
            SetChannelEnabled(channel, newState);
            return newState;
        }

        public void SetAllChannels(bool enabled)
        {
            _enabledChannels = enabled ? LogChannel.All : LogChannel.None;
            NotifyChanged();
        }

        public string GetChannelColorHex(LogChannel channel)
        {
            return channel switch
            {
                LogChannel.General     => _colorGeneral,
                LogChannel.Network     => _colorNetwork,
                LogChannel.Match       => _colorMatch,
                LogChannel.Combat      => _colorCombat,
                LogChannel.Player      => _colorPlayer,
                LogChannel.AI          => _colorAI,
                LogChannel.UI          => _colorUI,
                LogChannel.Persistence => _colorPersistence,
                LogChannel.Audio       => _colorAudio,
                _                      => "#FFFFFF"
            };
        }

        public void ResetToDefaults()
        {
            _enabledChannels = LogChannel.All;
            _minimumLogLevel = LogLevel.Info;
            _alwaysPassWarningsAndErrors = true;
            _enableRichTextColor = true;
            _includeFrameCount = false;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            OnSettingsChanged?.Invoke();
        }
    }
}
