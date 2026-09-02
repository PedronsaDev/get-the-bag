using UnityEngine;
using TMPro;

namespace PedronsaDev.Console.Components
{
public class UIFontSetter : MonoBehaviour
{
    [SerializeField] private ConsolePreferences m_Preferences;
    [SerializeField] private TMP_InputField m_InputField;
    [SerializeField] private TextMeshProUGUI[] m_Texts;
    [SerializeField] private TextMeshProUGUI m_AutocompleteText;

    private void Start()
    {
        ApplyFontSettings();
    }

    private void OnEnable()
    {
        if (m_Preferences)
        {
            m_Preferences.OnPreferencesChanged += ApplyFontSettings;
        }
    }

    private void OnDisable()
    {
        if (m_Preferences)
        {
            m_Preferences.OnPreferencesChanged -= ApplyFontSettings;
        }
    }

    public void ApplyFontSettings()
    {
        if (!m_Preferences) return;

        if (m_InputField)
        {
            if (m_Preferences.GlobalFont)
            {
                m_InputField.fontAsset = m_Preferences.GlobalFont;
            }

            m_InputField.pointSize = m_Preferences.InputTextFontSize;
            m_AutocompleteText.fontSize = m_Preferences.InputTextFontSize;
        }

        if (m_Texts != null && m_Preferences.GlobalFont)
        {
            foreach (var textMeshPro in m_Texts)
            {
                if (textMeshPro)
                {
                    textMeshPro.font = m_Preferences.GlobalFont;
                }
            }
        }
    }

    [Command("set_font_size", "Sets the font size for console log messages (default: 24)",
        Aliases = new[] { "fontsize" })]
    public void SetFontSize(float size)
    {
        if (!m_Preferences) return;
        m_Preferences.LogTextFontSize = Mathf.Clamp(size, 10f, 60f);
        m_Preferences.NotifyPreferencesChanged();
        Console.Log($"[Theme] Log font size set to {m_Preferences.LogTextFontSize}");
    }

    [Command("set_input_font_size", "Sets the font size for the search/input bar (default: 24)",
        Aliases = new[] { "input_fontsize" })]
    public void SetInputFontSize(float size)
    {
        if (!m_Preferences) return;
        m_Preferences.InputTextFontSize = Mathf.Clamp(size, 10f, 60f);
        m_Preferences.NotifyPreferencesChanged();
        Console.Log($"[Theme] Search/Input font size set to {m_Preferences.InputTextFontSize}");
    }
}
}