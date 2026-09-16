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

    [Command("font", "Switches the active console font by name (e.g. 'font jetbrains', 'font inconsolata')",
        Aliases = new[] { "set_font" })]
    public void SetFont(string fontName)
    {
        if (!m_Preferences) return;

        if (m_Preferences.TrySetFont(fontName))
        {
            Console.Log($"[Theme] Active console font switched to: <b>{m_Preferences.GlobalFont.name}</b>");
        }
        else
        {
            Console.LogWarning($"[Theme] Font '{fontName}' not found in registered fonts. Run 'fonts' to view available options.");
        }
    }

    [Command("fonts", "Lists all registered console fonts and indicates the active font",
        Aliases = new[] { "font_list" })]
    public void ListFonts()
    {
        if (!m_Preferences || m_Preferences.AvailableFonts == null || m_Preferences.AvailableFonts.Count == 0)
        {
            Console.Log("[Theme] No fonts registered in Console Preferences. Use the Inspector to scan or add fonts.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>Registered Console Fonts:</b>");
        for (int i = 0; i < m_Preferences.AvailableFonts.Count; i++)
        {
            var entry = m_Preferences.AvailableFonts[i];
            if (!entry.FontAsset) continue;

            bool isActive = m_Preferences.GlobalFont == entry.FontAsset;
            string marker = isActive ? "<color=#4ADE80>[ACTIVE]</color>" : "        ";
            sb.AppendLine($"{marker} <b>{entry.Name}</b> (<i>{entry.FontAsset.name}</i>)");
        }
        sb.Append("Type <i>font &lt;name&gt;</i> to switch.");
        Console.Log(sb.ToString());
    }
}
}