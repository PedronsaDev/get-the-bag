using Project.Networking.Scenes;
using UnityEngine;

namespace Project.Core.Match
{
/// <summary>
/// Forwards the scene coordinator's loading signals to <see cref="TransitionEventChannel"/>,
/// which the existing TransitionUI overlay listens to. Runs on every peer, so clients get the
/// overlay too. Needs no scene wiring.
/// When the freshly loaded scene has a <see cref="LevelReadyGate"/> that is not live yet, the overlay
/// stays up until the gate resolves (live or failed) instead of dropping as soon as the scene exists.
/// </summary>
internal static class LevelLoadingScreenBridge
{
    private static TransitionEventChannel _channel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        // Unsubscribe first so a disabled domain reload cannot subscribe twice.
        NetworkSceneCoordinator.OnLoadingScreenRequested -= HandleRequested;
        NetworkSceneCoordinator.OnLoadingScreenReleased -= HandleReleased;
        NetworkSceneCoordinator.OnLoadingScreenRequested += HandleRequested;
        NetworkSceneCoordinator.OnLoadingScreenReleased += HandleReleased;
        _channel = null;
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

    private static void HandleRequested()
    {
        if (Channel) Channel.RaiseOnTransitionStarted();
    }

    private static void HandleReleased()
    {
        var gate = LevelReadyGate.Instance;
        if (gate && !gate.IsResolved)
        {
            // The scene is here but the players are not all in yet. The gate calls ReleaseNow() later.
            gate.HoldLoadingScreen();
            return;
        }

        ReleaseNow();
    }

    internal static void ReleaseNow()
    {
        if (Channel) Channel.RaiseOnTransitionCompleted();
    }
}
}
