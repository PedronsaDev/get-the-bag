using System;
using UnityEngine;

namespace PedronsaDev.Console.Components
{
	[RequireComponent(typeof(Autocompletor))]
	public class Highlighter : MonoBehaviour
	{
		private Autocompletor m_AutocompletionManager;
		private LogMessage m_HighlightAutocomplete;

		private void Awake()
		{
			m_AutocompletionManager = gameObject.GetComponent<Autocompletor>();
		}

		private void OnEnable()
		{
			m_AutocompletionManager.OnCopyAutocompletion += HighlightAutocomplete;
			m_AutocompletionManager.OnBreakAutocompletion += UnhighlightAutocomplete;
		}

		private void OnDisable()
		{
			m_AutocompletionManager.OnCopyAutocompletion -= HighlightAutocomplete;
			m_AutocompletionManager.OnBreakAutocompletion -= UnhighlightAutocomplete;
		}

		/// <summary>
		/// Highlight autocomplete log message, unhighlight highlighted autcomplete if exists
		/// </summary>
		/// <param name="logMessage">the log message to highlight</param>
		private void HighlightAutocomplete(LogMessage logMessage)
		{
			if (m_HighlightAutocomplete != null)
			{
				if (m_HighlightAutocomplete.LogMessageSetup == null)
				{
					// autcomplete has been cleared
					m_HighlightAutocomplete = null;
				}
				else
				{
					m_HighlightAutocomplete.LogMessageSetup.UnhighlightText();
				}
			}

			if (logMessage.LogMessageSetup == null)
			{
				throw new NullReferenceException("LogMessageSetup");
			}

			logMessage.LogMessageSetup.HighlightText();
			m_HighlightAutocomplete = logMessage;
		}

		/// <summary>
		/// Unhighlight highlighted autcomplete if exists
		/// </summary>
		private void UnhighlightAutocomplete()
		{
			if (m_HighlightAutocomplete == null)
			{
				return;
			}

			if (m_HighlightAutocomplete.LogMessageSetup == null)
			{
				// autcomplete has been cleared
				m_HighlightAutocomplete = null;
				return;
			}

			m_HighlightAutocomplete.LogMessageSetup.UnhighlightText();
		}
	}
}
