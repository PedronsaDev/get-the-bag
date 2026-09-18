using System;
using System.Text;
using PedronsaDev.Console;

namespace Project.Logging.Commands
{
    /// <summary>
    /// In-game Smart Console commands for live inspection and filtering of logging channels.
    /// </summary>
    [CommandGroup("Logging")]
    public static class LoggingCommands
    {
        [Command("log_channels", "Lists all logging channels and their current active status.")]
        public static void ListChannels()
        {
            var settings = LogSettings.Instance;
            var sb = new StringBuilder();

            sb.AppendLine($"<color=#38BDF8>--- Logging Configuration ---</color>");
            sb.AppendLine($"Minimum Log Level: <b>{settings.MinimumLogLevel}</b>");
            sb.AppendLine($"Always Pass Warnings/Errors: <b>{settings.AlwaysPassWarningsAndErrors}</b>");
            sb.AppendLine("Channels:");

            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                if (channel is LogChannel.None or LogChannel.All) continue;

                bool isEnabled = settings.IsChannelEnabled(channel);
                string statusTag = isEnabled
                    ? "<color=#4ADE80>[ON]</color>"
                    : "<color=#94A3B8>[OFF]</color>";

                string colorHex = settings.GetChannelColorHex(channel);
                sb.AppendLine($"  {statusTag} <color={colorHex}>{channel}</color>");
            }

            PedronsaDev.Console.Console.Log(sb.ToString());
        }

        [Command("log_toggle", "Toggles a specific logging channel on or off. Usage: log_toggle [channel]")]
        public static void ToggleChannel(LogChannel channel)
        {
            if (channel is LogChannel.None or LogChannel.All)
            {
                PedronsaDev.Console.Console.LogWarning("Cannot toggle 'None' or 'All'. Use 'log_all' instead.");
                return;
            }

            var settings = LogSettings.Instance;
            bool newState = settings.ToggleChannel(channel);
            string stateStr = newState ? "<color=#4ADE80>ENABLED</color>" : "<color=#F43F5E>DISABLED</color>";
            string colorHex = settings.GetChannelColorHex(channel);

            PedronsaDev.Console.Console.Log($"Channel <color={colorHex}>[{channel}]</color> is now {stateStr}.");
        }

        [Command("log_enable", "Enables a specific logging channel. Usage: log_enable [channel]")]
        public static void EnableChannel(LogChannel channel)
        {
            if (channel == LogChannel.All)
            {
                SetAllChannels(true);
                return;
            }

            var settings = LogSettings.Instance;
            settings.SetChannelEnabled(channel, true);
            string colorHex = settings.GetChannelColorHex(channel);
            PedronsaDev.Console.Console.Log($"Channel <color={colorHex}>[{channel}]</color> <color=#4ADE80>ENABLED</color>.");
        }

        [Command("log_disable", "Disables a specific logging channel. Usage: log_disable [channel]")]
        public static void DisableChannel(LogChannel channel)
        {
            if (channel == LogChannel.All)
            {
                SetAllChannels(false);
                return;
            }

            var settings = LogSettings.Instance;
            settings.SetChannelEnabled(channel, false);
            string colorHex = settings.GetChannelColorHex(channel);
            PedronsaDev.Console.Console.Log($"Channel <color={colorHex}>[{channel}]</color> <color=#F43F5E>DISABLED</color>.");
        }

        [Command("log_all", "Enables or disables all logging channels at once. Usage: log_all [true/false]")]
        public static void SetAllChannels(bool enabled)
        {
            LogSettings.Instance.SetAllChannels(enabled);
            string stateStr = enabled ? "<color=#4ADE80>ENABLED</color>" : "<color=#F43F5E>DISABLED</color>";
            PedronsaDev.Console.Console.Log($"All logging channels are now {stateStr}.");
        }

        [Command("log_level", "Sets minimum severity threshold (Verbose, Info, Warning, Error). Usage: log_level [level]")]
        public static void SetLogLevel(LogLevel level)
        {
            LogSettings.Instance.MinimumLogLevel = level;
            PedronsaDev.Console.Console.Log($"Minimum log level updated to <b>{level}</b>.");
        }

        [Command("log_reset", "Resets all logging channels and thresholds to defaults.")]
        public static void ResetToDefaults()
        {
            LogSettings.Instance.ResetToDefaults();
            PedronsaDev.Console.Console.Log("Logging configuration reset to defaults.");
        }
    }
}
