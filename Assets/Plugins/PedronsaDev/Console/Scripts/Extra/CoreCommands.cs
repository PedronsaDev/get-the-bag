using System;
using System.Linq;
using System.Text;
using UnityEngine;
using PedronsaDev.Console.Components;

namespace PedronsaDev.Console.Extra
{
	[CommandGroup("Core")]
	public static class CoreCommands
	{
		[Command("help", "Displays console help or details for a specific command. Usage: help [command_name]", Aliases = new[] { "desc", "describe", "man", "?" })]
		public static void PrintHelp(string command = "")
		{
			if (string.IsNullOrWhiteSpace(command))
			{
				Console.Log("Type 'get_commands' for a list of available commands.\r\nType 'help <command>' or 'desc <command>' for command details, parameters, and aliases.\r\nFor any problems with the console message .pedronsa in Discord");
				return;
			}

			var cache = ConsoleSystem.Cache;
			if (!cache)
			{
				Console.LogWarning("Console cache is not available yet.");
				return;
			}

			Command targetCmd = cache.AvailableCommands?.FirstOrDefault(c =>
				c.Name.Equals(command, StringComparison.OrdinalIgnoreCase) ||
				(c.Aliases != null && c.Aliases.Any(a => a.Equals(command, StringComparison.OrdinalIgnoreCase))));

			if (targetCmd == null && cache.CommandRegistry != null)
			{
				targetCmd = cache.CommandRegistry.FirstOrDefault(c =>
					c.Name.Equals(command, StringComparison.OrdinalIgnoreCase) ||
					(c.Aliases != null && c.Aliases.Any(a => a.Equals(command, StringComparison.OrdinalIgnoreCase))));
			}

			if (targetCmd == null)
			{
				Console.LogWarning($"Command '{command}' not found. Type 'get_commands' to see all available commands.");
				return;
			}

			var sb = new StringBuilder();
			sb.AppendLine($"<b><color=#60A5FA>{targetCmd.Name}</color></b> (Group: <i>{targetCmd.Group}</i>)");

			if (!string.IsNullOrEmpty(targetCmd.Description))
			{
				sb.AppendLine($"<color=#E2E8F0>Description:</color> {targetCmd.Description}");
			}
			else
			{
				sb.AppendLine("<color=#94A3B8>Description: None provided.</color>");
			}

			if (targetCmd.Aliases != null && targetCmd.Aliases.Length > 0)
			{
				sb.AppendLine($"<color=#CBD5E1>Aliases:</color> {string.Join(", ", targetCmd.Aliases)}");
			}

			if (targetCmd.Method != null)
			{
				var parameters = targetCmd.Method.GetParameters();
				if (parameters.Length > 0)
				{
					sb.AppendLine("<color=#CBD5E1>Parameters:</color>");
					foreach (var p in parameters)
					{
						string defaultVal = p.HasDefaultValue ? $" = {p.DefaultValue ?? "null"}" : " <color=#F59E0B>(required)</color>";
						sb.AppendLine($"  - <b>{p.ParameterType.Name}</b> {p.Name}{defaultVal}");
					}
				}
				else
				{
					sb.AppendLine("<color=#94A3B8>Parameters: None</color>");
				}
			}

			Console.Log(sb.ToString());
		}

		[Command("version", "Displays the Smart Console version")]
		public static void PrintVersion()
		{
			Console.Log($"{Console.Version}\n");
		}

		[Command("quit", "Quits the application")]
		public static void Quit()
		{
			Application.Quit();
		}

		[Command("filter", "Filter visible logs: all, log, warning, error, custom", Aliases = new[] { "f" })]
		public static void SetLogFilter(string mode = "all")
		{
			switch (mode.ToLowerInvariant())
			{
				case "log":
				case "logs":
					Console.SetFilter(Console.LogFilterMode.Log);
					Console.Log("[Filter] Displaying standard logs only.");
					break;
				case "warn":
				case "warning":
				case "warnings":
					Console.SetFilter(Console.LogFilterMode.Warning);
					Console.Log("[Filter] Displaying warning logs only.");
					break;
				case "error":
				case "errors":
					Console.SetFilter(Console.LogFilterMode.Error);
					Console.Log("[Filter] Displaying error logs only.");
					break;
				case "custom":
				case "net":
					Console.SetFilter(Console.LogFilterMode.Custom);
					Console.Log("[Filter] Displaying custom/network logs only.");
					break;
				default:
					Console.SetFilter(Console.LogFilterMode.All);
					Console.Log("[Filter] Displaying all logs.");
					break;
			}
		}

		[Command("search", "Searches log history for a keyword", Aliases = new[] { "find" })]
		public static void SearchLogs(string keyword)
		{
			if (string.IsNullOrWhiteSpace(keyword))
			{
				Console.LogWarning("Usage: search <keyword>");
				return;
			}

			int matches = 0;
			for (int i = 0; i < Console.LogMessages.Count; i++)
			{
				var msg = Console.LogMessages[i];
				if (msg.Text != null && msg.Text.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					matches++;
					Console.Log($"  [{msg.Type}] {msg.Text}");
				}
			}

			Console.Log($"Found {matches} matching log(s) for '{keyword}'.");
		}

		[Command("copy", "Copies all current logs to the system clipboard", Aliases = new[] { "copy_logs" })]
		public static void CopyLogsToClipboard()
		{
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < Console.LogMessages.Count; i++)
			{
				var msg = Console.LogMessages[i];
				sb.AppendLine($"[{msg.Type}] {msg.Text}");
			}
			GUIUtility.systemCopyBuffer = sb.ToString();
			Console.Log($"Copied {Console.LogMessages.Count} logs to clipboard!");
		}
	}
}
