using UnityEngine;

namespace PedronsaDev.Console.Extra
{
	[CommandGroup("Graphics")]
	public static class GraphicsCommands
	{
		[Command("get_max_fps", "Gets the frame rate at which Unity tries to render on the application")]
		public static void GetMaxFPS()
		{
			Console.Log($"Target frame rate is {Application.targetFrameRate}.");
		}

		[Command("set_max_fps", "Sets the frame rate at which Unity tries to render on the application. Set to -1 for unlimited")]
		public static void SetMaxFPS(int newTargetFrameRate)
		{
			Application.targetFrameRate = newTargetFrameRate;
			Console.Log($"Updated target frame rate to {Application.targetFrameRate}.");
		}

		[Command("set_vsync", "Enables or disables vsync for the application")]
		public static void SetVSync(bool enable)
		{
			QualitySettings.vSyncCount = enable ? 1 : 0;
			string enableMessage = enable ? "Enabled" : "Disabled";
			Console.Log($"{enableMessage} vsync.");
		}
	}
}
