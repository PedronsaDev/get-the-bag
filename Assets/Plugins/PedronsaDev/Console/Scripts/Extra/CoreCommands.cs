using UnityEngine;

namespace PedronsaDev.Console.Extra
{
	[CommandGroup("Core")]
	public static class CoreCommands
	{
		[Command("help", "Displays information about the Console")]
		public static void PrintHelp()
		{
			Console.Log("Type 'get_commands' for a list of available commands. \r\nFor any problems with the console message .pedronsa in Discord");
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
		
		[Command("test_logs", "Tests all kinds of logs available")]
		public static void TestLogs()
		{
			Console.Log("This is a Console Log");
			Console.LogWarning("This is a Console Warning Log");
			Console.LogError("This is a Console Error Log");
			Debug.Log("This is a Unity Engine Log");
			Debug.LogWarning("This is a Unity Engine Warning Log");
			Debug.LogError("This is a Unity Engine Error Log");
			
			Console.LogCustom("Client", "This is a Console Custom Log", new Color(0.47f,0.92f,0.3f));
			Console.LogCustom("Server", "This is another Console Custom Log", new Color(0.859f,0.431f,0.675f));
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
