using Project.Logging;
using UnityEngine;

namespace Project.Game
{
    internal static class LowSpecStartupSettings
    {
        internal const string QualityName = "Low Spec";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (Application.isEditor || QualitySettings.names[QualitySettings.GetQualityLevel()] != QualityName) return;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            AppLog.Log(LogChannel.General, "Low Spec active: 1280x720 windowed, 30 FPS target.");
        }
    }
}
