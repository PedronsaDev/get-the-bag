using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedronsaDev.Console.Components
{
	public class UIGenerator : MonoBehaviour
	{
		[SerializeField] private ConsolePreferences m_Preferences;
		[SerializeField] private Transform m_Parent;
		[SerializeField] private GameObject m_LogMessagePrefab;
		[SerializeField, Tooltip("Maximum number of active log UI elements before recycling oldest items.")]
		private int m_MaxLogMessages = 200;

		private readonly Queue<LogMessageSetup> _logMessagePool = new();
		private bool m_IsLastMessageEven;

		private void OnEnable()
		{
			Console.LogMessageReceived += GenerateLogMessage;
			Console.AutocompletionLogMessageReceived += GenerateAutocompletionLogMessage;
			Console.LogMessageReceived += EnsureAutocompleteSiblings;
			Console.OnClear += HandleClear;
			Console.OnClearAutocompletions += HandleClearAutocompletions;
			Console.OnFilterChanged += ApplyFilter;
		}

		private void OnDisable()
		{
			Console.LogMessageReceived -= GenerateLogMessage;
			Console.AutocompletionLogMessageReceived -= GenerateAutocompletionLogMessage;
			Console.LogMessageReceived -= EnsureAutocompleteSiblings;
			Console.OnClear -= HandleClear;
			Console.OnClearAutocompletions -= HandleClearAutocompletions;
			Console.OnFilterChanged -= ApplyFilter;
		}

		private void ApplyFilter(Console.LogFilterMode filter)
		{
			for (int i = 0; i < Console.LogMessages.Count; i++)
			{
				var msg = Console.LogMessages[i];
				if (msg.LogMessageSetup == null || msg.LogMessageSetup.gameObject == null) continue;

				bool visible = IsLogVisibleUnderFilter(msg, filter);
				msg.LogMessageSetup.gameObject.SetActive(visible);
			}
		}

		private static bool IsLogVisibleUnderFilter(LogMessage msg, Console.LogFilterMode filter)
		{
			return filter switch
			{
				Console.LogFilterMode.All => true,
				Console.LogFilterMode.Log => msg.Type == LogMessageType.Log || msg.Type == LogMessageType.Command,
				Console.LogFilterMode.Warning => msg.Type == LogMessageType.Warning,
				Console.LogFilterMode.Error => msg.Type == LogMessageType.Error,
				Console.LogFilterMode.Custom => msg.Type == LogMessageType.Custom,
				_ => true
			};
		}

		private void HandleClear()
		{
			for (int i = 0; i < Console.LogMessages.Count; i++)
			{
				var setup = Console.LogMessages[i].LogMessageSetup;
				if (setup != null && setup.gameObject != null)
				{
					setup.gameObject.SetActive(false);
					_logMessagePool.Enqueue(setup);
				}
			}
		}

		private void HandleClearAutocompletions()
		{
			for (int i = 0; i < Console.AutocompletionLogMessages.Count; i++)
			{
				var setup = Console.AutocompletionLogMessages[i].LogMessageSetup;
				if (setup != null && setup.gameObject != null)
				{
					setup.gameObject.SetActive(false);
					_logMessagePool.Enqueue(setup);
				}
			}
		}

		/// <summary>
		/// Retrieves a pooled log message setup instance or instantiates a new one if the pool is empty.
		/// </summary>
		private LogMessageSetup GetOrCreateLogMessageInstance(LogMessage logMessage)
		{
			LogMessageSetup setup = null;

			while (_logMessagePool.Count > 0)
			{
				var candidate = _logMessagePool.Dequeue();
				if (candidate != null && candidate.gameObject != null)
				{
					setup = candidate;
					setup.gameObject.SetActive(true);
					setup.transform.SetAsLastSibling();
					break;
				}
			}

			if (setup == null)
			{
				GameObject logInstance = Instantiate(m_LogMessagePrefab, m_Parent);
				if (!logInstance.TryGetComponent(out setup))
				{
					throw new NullReferenceException("LogMessageSetup missing from instantiated prefab.");
				}
			}

			string logMessageName = logMessage.GetType().Name;
			setup.name = $"{logMessageName}({logMessage.Type})";

			return setup;
		}

		/// <summary>
		/// Generates or recycles a log message UI item.
		/// </summary>
		private void GenerateLogMessage(LogMessage logMessage)
		{
			LogMessageSetup logInstanceSetup;

			// Circular buffer: recycle oldest item if max capacity reached
			if (Console.LogMessages.Count >= m_MaxLogMessages && Console.LogMessages.Count > 0)
			{
				var oldest = Console.LogMessages[0];
				Console.LogMessages.RemoveAt(0);

				if (oldest.LogMessageSetup != null && oldest.LogMessageSetup.gameObject != null)
				{
					logInstanceSetup = oldest.LogMessageSetup;
					logInstanceSetup.transform.SetAsLastSibling();
				}
				else
				{
					logInstanceSetup = GetOrCreateLogMessageInstance(logMessage);
				}
			}
			else
			{
				logInstanceSetup = GetOrCreateLogMessageInstance(logMessage);
			}

			string logMessageText = logMessage.Text;
			float fontSize = m_Preferences.LogTextFontSize;

			if (logMessage.Type == LogMessageType.Command)
			{
				logMessageText = logMessageText.Insert(0, "[Console] ");
			}

			var color = logMessage.Type == LogMessageType.Custom ? logMessage.CustomColor : GetLogMessageTextColor(logMessage.Type);
			logInstanceSetup.SetText(logMessageText, logMessage.AltText, color, m_Preferences.GlobalFont, fontSize);

			if (m_IsLastMessageEven)
			{
				logInstanceSetup.SetBackgroundColor(m_Preferences.BackgroundEvenColor);
			}

			m_IsLastMessageEven = !m_IsLastMessageEven;

			logMessage.LogMessageSetup = logInstanceSetup;
			logMessage.LogMessageSelector = logInstanceSetup.GetComponent<LogMessageSelector>();

			logInstanceSetup.gameObject.SetActive(IsLogVisibleUnderFilter(logMessage, Console.ActiveFilter));

			Console.LogMessages.Add(logMessage);
		}

		/// <summary>
		/// Generates or recycles an autocompletion log message UI item.
		/// </summary>
		private void GenerateAutocompletionLogMessage(LogMessage logMessage)
		{
			LogMessageSetup logInstanceSetup = GetOrCreateLogMessageInstance(logMessage);

			var color = logMessage.Type == LogMessageType.Custom ? logMessage.CustomColor : GetLogMessageTextColor(logMessage.Type);
			logInstanceSetup.SetText($"<i>{logMessage.Text}</i>", logMessage.AltText, color, m_Preferences.GlobalFont, m_Preferences.AutocompleteTextFontSize);

			if (logMessage.Parameters != null)
			{
				logInstanceSetup.SetTextParameter(logMessage.Parameters);
			}

			logInstanceSetup.SetBackgroundColor(m_Preferences.BackgroundAutocompleteColor);

			logMessage.LogMessageSetup = logInstanceSetup;
			logMessage.LogMessageSelector = logInstanceSetup.GetComponent<LogMessageSelector>();

			Console.AutocompletionLogMessages.Add(logMessage);
		}

		private Color GetLogMessageTextColor(LogMessageType logMessageType)
		{
			return logMessageType switch
			{
				LogMessageType.Log => m_Preferences.LogTextColor,
				LogMessageType.Command => m_Preferences.CommandTextColor,
				LogMessageType.Warning => m_Preferences.WarningTextColor,
				LogMessageType.Error => m_Preferences.ErrorTextColor,
				LogMessageType.Autocompletion => m_Preferences.AutocompleteTextColor,
				_ => throw new ArgumentOutOfRangeException(nameof(logMessageType), logMessageType, null)
			};
		}

		private void EnsureAutocompleteSiblings(LogMessage logMessage)
		{
			if (Console.AutocompletionLogMessages.Count == 0)
			{
				return;
			}

			for (int i = 0; i < Console.AutocompletionLogMessages.Count; i++)
			{
				int siblingIndex = Console.LogMessages.Count + Console.AutocompletionLogMessages.Count - 1 + i;
				if (Console.AutocompletionLogMessages[i].LogMessageSetup != null)
				{
					Console.AutocompletionLogMessages[i].LogMessageSetup.transform.SetSiblingIndex(siblingIndex);
				}
			}
		}
	}
}
