using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildSmokePlayer
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Stop Play Mode before building the test player.");
        string output = Path.GetFullPath(".codex/v1/build/AnimationV1.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        bool background = PlayerSettings.runInBackground;
        try
        {

            PlayerSettings.runInBackground = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/_Project/Scenes/Dev/Scene_Gym_Combat.unity", "Assets/_Project/Scenes/Scene_Hub.unity", "Assets/_Project/Scenes/Scene_DeepShafts.unity" },
                locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            string result = JsonConvert.SerializeObject(new { output, result = report.summary.result.ToString(),
                report.summary.totalErrors, report.summary.totalWarnings, durationSeconds = report.summary.totalTime.TotalSeconds });
            File.WriteAllText(".codex/v1/build-report.json", result);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception(result);
            return result;
        }
        finally { PlayerSettings.runInBackground = background; }
    }
}
