using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PedronsaDev.Console.Components
{
	public class LogMessageSetup : MonoBehaviour
	{
		[SerializeField] private ConsolePreferences m_Preferences;
		[SerializeField] private TextMeshProUGUI m_Text;
		[SerializeField] private TextMeshProUGUI m_AltText;
		[SerializeField] private Image m_Background;

		public GameObject AltTextGameObject => m_AltText.gameObject;
		public TextMeshProUGUI TextComponent => m_Text;
		public TextMeshProUGUI AltTextComponent => m_AltText;
		public LogMessage LogMessage { get; set; }

		private Color m_TextColor;
		private LogMessageSelector _selector;

		private void Awake()
		{
			_selector = GetComponent<LogMessageSelector>();
		}

		private void Start()
		{
			m_TextColor = m_Text.color;
		}

		public void UpdateFont(TMP_FontAsset font, float fontSize)
		{
			if (font == null) return;
			if (m_Text)
			{
				m_Text.font = font;
				m_Text.fontSize = fontSize;
			}
			if (m_AltText)
			{
				m_AltText.font = font;
				m_AltText.fontSize = Mathf.Max(8f, fontSize - 2f);
			}
		}

		public string GetCleanText() => LogMessage != null ? LogMessage.GetCleanText() : m_Text.text;
		public string GetCleanAltText() => LogMessage != null ? LogMessage.GetCleanAltText() : m_AltText.text;
		public string GetFullFormattedLog() => LogMessage != null ? LogMessage.GetFullFormattedLog() : m_Text.text;

		public void SetText(string text, string altText, Color textColor, TMP_FontAsset font, float fontSize)
		{
			m_Text.text = $"{text}";
			m_AltText.text = $"{altText}";
			
			m_Text.color = textColor;
			m_Text.font = font;
			
			m_Text.fontSize = fontSize;
			m_AltText.fontSize = fontSize - 2;
		}

		public void EnableAltText()
		{
			if(m_AltText.text.Length > 0)
				AltTextGameObject.SetActive(true);
		}
		
		public void DisableAltText()
		{
			if(m_AltText.text.Length > 0)
				AltTextGameObject.SetActive(false);
		}

		public void HighlightText()
		{
			m_Text.color = m_Preferences.HighlightTextColor;
		}
		
		public void UnhighlightText()
		{
			m_Text.color = m_TextColor;
		}
		
		public void SetTextParameter(ParameterInfo[] parameters)
		{
			string colorHex = ColorUtility.ToHtmlStringRGBA(m_Preferences.AutocompleteParameterTextColor);

			for (int i = 0; i < parameters.Length; i++)
			{
				m_Text.text += $"<i><color=#{colorHex}> {parameters[i].Name}</color></i>";

				if (parameters[i].HasDefaultValue)
				{
					m_Text.text += $"<i><color=#{colorHex}> = {parameters[i].DefaultValue.ToString()}</color></i>";
				}
			}
		}

		public void SetBackgroundColor(Color color)
		{
			if (m_Background != null)
			{
				m_Background.color = color;
			}

			if (_selector == null)
			{
				_selector = GetComponent<LogMessageSelector>();
			}

			if (_selector != null)
			{
				_selector.SetBaseColor(color);
			}
		}

		public void DisableSelection()
		{
			LogMessageSelector selector = gameObject.GetComponent<LogMessageSelector>();

			if (selector)
			{
				selector.enabled = false;
			}
		}
	}
}
