using Project.Networking.Scenes;
using UnityEngine;

namespace Project.Core.Match
{
public static class LevelLoadingStatus
{
    public static string TargetScene { get; internal set; } = string.Empty;
    public static float SceneProgress { get; internal set; }
    public static bool IsShowing { get; internal set; }
}

internal static class LevelLoadingScreenBridge
{
    private static TransitionEventChannel _channel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        // Unsubscribe first: with domain reload disabled, statics survive between play sessions.
        NetworkSceneCoordinator.OnLoadingScreenRequested -= HandleRequested;
        NetworkSceneCoordinator.OnLoadingScreenReleased -= HandleReleased;
        NetworkSceneCoordinator.OnLoadingProgress -= HandleProgress;
        NetworkSceneCoordinator.OnLoadingScreenRequested += HandleRequested;
        NetworkSceneCoordinator.OnLoadingScreenReleased += HandleReleased;
        NetworkSceneCoordinator.OnLoadingProgress += HandleProgress;
        _channel = null;
        LevelLoadingStatus.TargetScene = string.Empty;
        LevelLoadingStatus.SceneProgress = 0f;
        LevelLoadingStatus.IsShowing = false;
    }

    private static TransitionEventChannel Channel
    {
        get
        {
            if (!_channel)
                _channel = Resources.Load<TransitionEventChannel>("Event Channels/TransitionEventChannel");
            return _channel;
        }
    }

    private static void HandleRequested(string sceneName)
    {
        LevelLoadingStatus.TargetScene = sceneName ?? string.Empty;
        LevelLoadingStatus.SceneProgress = 0f;

        if (LevelLoadingStatus.IsShowing) return;

        LevelLoadingStatus.IsShowing = true;
        if (Channel) Channel.RaiseOnTransitionStarted();
    }

    private static void HandleProgress(float progress)
    {
        LevelLoadingStatus.SceneProgress = Mathf.Clamp01(progress);
    }

    private static void HandleReleased()
    {
        var gate = LevelReadyGate.Instance;
        if (gate && !gate.CanStartPlaying)
        {
            gate.HoldLoadingScreen();
            return;
        }

        ReleaseNow();
    }

    internal static void ReleaseNow()
    {
        if (!LevelLoadingStatus.IsShowing) return;

        LevelLoadingStatus.IsShowing = false;
        if (Channel) Channel.RaiseOnTransitionCompleted();
    }
}
}
