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
	}
}
