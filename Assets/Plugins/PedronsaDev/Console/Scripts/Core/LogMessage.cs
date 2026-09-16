using System;
using System.Reflection;
using PedronsaDev.Console.Components;
using PedronsaDev.Console.Utils;
using UnityEngine;

namespace PedronsaDev.Console
{
	public class LogMessage
	{
		public readonly string Text;
		public readonly string AltText;
		public readonly LogMessageType Type;
		public readonly ParameterInfo[] Parameters;
		public readonly DateTime Timestamp;
		public LogMessageSetup LogMessageSetup { get; set; }
		public LogMessageSelector LogMessageSelector { get; set; }
		public readonly Color CustomColor;
		
		public LogMessage(string sender, string text, LogMessageType type, Color customColor)
		{
			Timestamp = DateTime.Now;
			CustomColor = customColor;
			Text = $"[{sender}] {text}";
			Type = type;
			Parameters = null;
		}

		public LogMessage(string text, string altText, LogMessageType type)
		{
			Timestamp = DateTime.Now;
			Text = text;
			AltText = altText;
			Type = type;
			Parameters = null;
		}

		public LogMessage(string text, string altText, LogMessageType type, ParameterInfo[] parameters)
		{
			Timestamp = DateTime.Now;
			Text = text;
			AltText = altText;
			Type = type;
			Parameters = parameters;
		}

		/// <summary>
		/// Returns the log text stripped of any rich-text markup tags (e.g. <color>, <b>).
		/// </summary>
		public string GetCleanText() => StringUtil.StripRichText(Text);

		/// <summary>
		/// Returns the alternative / callstack text stripped of any rich-text markup tags.
		/// </summary>
		public string GetCleanAltText() => StringUtil.StripRichText(AltText);

		/// <summary>
		/// Formats a complete log record with timestamp, level/type, clean message, and callstack.
		/// </summary>
		public string GetFullFormattedLog()
		{
			string cleanText = GetCleanText();
			string timestampStr = Timestamp.ToString("HH:mm:ss.fff");
			string header = $"[{timestampStr}] [{Type}] {cleanText}";

			if (!string.IsNullOrWhiteSpace(AltText))
			{
				return $"{header}\nStack Trace:\n{GetCleanAltText()}";
			}

			return header;
		}

		public static implicit operator string(LogMessage logMessage)
		{
			return $"{logMessage.Type}:{logMessage.Text}";
		}
	}
}
