using UnityEngine;

namespace PedronsaDev.Console.Extra
{
	[CommandGroup("Time")]
	public static class TimeCommands
	{
		[Command("get_time_scale", "Gets the scale at which time passes")]
		public static void GetTimeScale()
		{
			Console.Log($"Time scale is {Time.timeScale}.");
		}

		[Command("set_time_scale", "Sets the scale at which time passes")]
		public static void SetTimeScale(float newTimeScale)
		{
			Time.timeScale = newTimeScale;
			Console.Log($"Updated time scale to {Time.timeScale}.");
		}
	}
}
