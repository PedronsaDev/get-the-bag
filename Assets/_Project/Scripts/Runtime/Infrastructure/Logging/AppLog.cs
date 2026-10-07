using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Logging
{
    /// <summary>
    /// High-performance static facade for subsystem-filtered logging across the project.
    /// Supports channel bitmask filtering, zero-allocation overloads, rich text badging, and callstack hiding.
    /// </summary>
    public static class AppLog
    {
        [HideInCallstack]
        public static bool IsChannelEnabled(LogChannel channel, LogLevel level = LogLevel.Info)
        {
            return LogSettings.Instance.IsChannelEnabled(channel, level);
        }

        #region Info Logging

        [HideInCallstack]
        public static void Log(LogChannel channel, string message, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Info)) return;
            Debug.Log(FormatMessage(channel, message), context);
        }

        [HideInCallstack]
        public static void Log(LogChannel channel, string format, object arg0, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Info)) return;
            Debug.Log(FormatMessage(channel, string.Format(format, arg0)), context);
        }

        [HideInCallstack]
        public static void Log(LogChannel channel, string format, object arg0, object arg1, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Info)) return;
            Debug.Log(FormatMessage(channel, string.Format(format, arg0, arg1)), context);
        }

        [HideInCallstack]
        public static void Log(LogChannel channel, string format, object arg0, object arg1, object arg2, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Info)) return;
            Debug.Log(FormatMessage(channel, string.Format(format, arg0, arg1, arg2)), context);
        }

        [HideInCallstack]
        public static void Log(LogChannel channel, string format, params object[] args)
        {
            if (!IsChannelEnabled(channel, LogLevel.Info)) return;
            Debug.Log(FormatMessage(channel, string.Format(format, args)));
        }

        #endregion

        #region Verbose Logging

        [HideInCallstack]
        public static void LogVerbose(LogChannel channel, string message, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Verbose)) return;
            Debug.Log(FormatMessage(channel, message), context);
        }

        [HideInCallstack]
        public static void LogVerbose(LogChannel channel, string format, object arg0, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Verbose)) return;
            Debug.Log(FormatMessage(channel, string.Format(format, arg0)), context);
        }

        #endregion

        #region Warning Logging

        [HideInCallstack]
        public static void LogWarning(LogChannel channel, string message, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Warning)) return;
            Debug.LogWarning(FormatMessage(channel, message), context);
        }

        [HideInCallstack]
        public static void LogWarning(LogChannel channel, string format, object arg0, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Warning)) return;
            Debug.LogWarning(FormatMessage(channel, string.Format(format, arg0)), context);
        }

        [HideInCallstack]
        public static void LogWarning(LogChannel channel, string format, params object[] args)
        {
            if (!IsChannelEnabled(channel, LogLevel.Warning)) return;
            Debug.LogWarning(FormatMessage(channel, string.Format(format, args)));
        }

        #endregion

        #region Error Logging

        [HideInCallstack]
        public static void LogError(LogChannel channel, string message, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Error)) return;
            Debug.LogError(FormatMessage(channel, message), context);
        }

        [HideInCallstack]
        public static void LogError(LogChannel channel, string format, object arg0, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Error)) return;
            Debug.LogError(FormatMessage(channel, string.Format(format, arg0)), context);
        }

        [HideInCallstack]
        public static void LogError(LogChannel channel, string format, params object[] args)
        {
            if (!IsChannelEnabled(channel, LogLevel.Error)) return;
            Debug.LogError(FormatMessage(channel, string.Format(format, args)));
        }

        [HideInCallstack]
        public static void LogException(LogChannel channel, Exception exception, Object context = null)
        {
            if (!IsChannelEnabled(channel, LogLevel.Error)) return;
            Debug.LogError(FormatMessage(channel, $"Exception: {exception.Message}\n{exception.StackTrace}"), context);
        }

        #endregion

        #region Formatting Helpers

        [HideInCallstack]
        private static string FormatMessage(LogChannel channel, string message)
        {
            var settings = LogSettings.Instance;

            string framePrefix = settings.IncludeFrameCount ? $"[F:{Time.frameCount}] " : "";

            if (!settings.EnableRichTextColor)
            {
                return $"{framePrefix}[{channel}] {message}";
            }

            string colorHex = settings.GetChannelColorHex(channel);
            return $"{framePrefix}<b><color={colorHex}>[{channel}]</color></b> {message}";
        }

        #endregion
    }
}
