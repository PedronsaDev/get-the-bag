using System.Reflection;
using PedronsaDev.Console.Components;
using UnityEngine;

namespace PedronsaDev.Console
{
	public class LogMessage
	{
		public readonly string Text;
		public readonly string AltText;
		public readonly LogMessageType Type;
		public readonly ParameterInfo[] Parameters;
		public LogMessageSetup LogMessageSetup { get; set; }
		public LogMessageSelector LogMessageSelector { get; set; }
		public readonly Color CustomColor;
		
		public LogMessage(string sender, string text, LogMessageType type, Color customColor)
		{
			CustomColor = customColor;
			Text = $"[{sender}] {text}";
			Type = type;
			Parameters = null;
		}

		public LogMessage(string text, string altText, LogMessageType type)
		{
			Text = text;
			AltText = altText;
			Type = type;
			Parameters = null;
		}

		public LogMessage(string text,string altText, LogMessageType type, ParameterInfo[] parameters)
		{
			Text = text;
			AltText = altText;
			Type = type;
			Parameters = parameters;
		}

		public static implicit operator string(LogMessage logMessage)
		{
			return $"{logMessage.Type}:{logMessage.Text}";
		}
	}
}
