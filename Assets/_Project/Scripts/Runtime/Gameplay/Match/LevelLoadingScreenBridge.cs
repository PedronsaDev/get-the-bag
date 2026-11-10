using System;
using Cysharp.Threading.Tasks;
using Project.Networking.Scenes;
using UnityEngine;
using Project.Core;

namespace Project.Match
{
public static class LevelLoadingStatus
{
    public const float DefaultMinimumSeconds = 3f;

    public static string TargetScene { get; internal set; } = string.Empty;
    public static float SceneProgress { get; internal set; }
    public static bool IsShowing { get; internal set; }

    public static float ShownSince { get; internal set; }

    /// <summary>
    /// The loading screen stays up at least this long, and a level does not go live sooner.
    /// Set by the loading screen UI from its inspector.
    /// </summary>
    public static float MinimumSeconds { get; set; } = DefaultMinimumSeconds;

    public static float ElapsedSeconds => IsShowing ? Time.unscaledTime - ShownSince : 0f;

    /// <summary>0 to 1 over <see cref="MinimumSeconds"/>. Use it to cap a progress bar so it fills smoothly.</summary>
    public static float MinimumProgress =>
        !IsShowing || MinimumSeconds <= 0f ? 1f : Mathf.Clamp01(ElapsedSeconds / MinimumSeconds);

    public static bool MinimumTimePassed => MinimumProgress >= 1f;
}

internal static class LevelLoadingScreenBridge
{
    private static TransitionEventChannel _channel;
    private static int _showId;

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
        _showId = 0;
        LevelLoadingStatus.TargetScene = string.Empty;
        LevelLoadingStatus.SceneProgress = 0f;
        LevelLoadingStatus.IsShowing = false;
        LevelLoadingStatus.ShownSince = 0f;
        LevelLoadingStatus.MinimumSeconds = LevelLoadingStatus.DefaultMinimumSeconds;
    }

    private static TransitionEventChannel Channel
    {
        get
        {
            if (!_channel)
                _channel = Resources.Load<TransitionEventChannel>(TransitionEventChannel.ResourcePath);
            return _channel;
        }
    }

    private static void HandleRequested(string sceneName)
    {
        LevelLoadingStatus.TargetScene = sceneName ?? string.Empty;
        LevelLoadingStatus.SceneProgress = 0f;

        // The coordinator reads this right after raising the request and waits before loading the scene,
        // so the new level is never visible under a half-faded screen.
        NetworkSceneCoordinator.LoadingScreenFadeInSeconds =
            LevelLoadingStatus.IsShowing || !Channel ? 0f : Channel.FadeInSeconds;

        if (LevelLoadingStatus.IsShowing) return;

        _showId++;
        LevelLoadingStatus.IsShowing = true;
        LevelLoadingStatus.ShownSince = Time.unscaledTime;
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

    /// <summary>Closes the loading screen, waiting out the minimum time first if needed.</summary>
    internal static void ReleaseNow()
    {
        if (!LevelLoadingStatus.IsShowing) return;

        float remaining = LevelLoadingStatus.MinimumSeconds - LevelLoadingStatus.ElapsedSeconds;
        if (remaining > 0f)
        {
            ReleaseLaterAsync(_showId, remaining).Forget();
            return;
        }

        LevelLoadingStatus.IsShowing = false;
        if (Channel) Channel.RaiseOnTransitionCompleted();
    }

    private static async UniTaskVoid ReleaseLaterAsync(int showId, float delay)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true);

        // A newer loading screen started meanwhile; it releases itself.
        if (showId != _showId) return;
        ReleaseNow();
    }
}
}
