using Plugins.Outline;
using Project.Core.Events;
using Project.Core.Interaction;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
/// <summary>
/// Interactable hub terminal that opens the level selection UI. Only the host (or an offline player)
/// can launch; other players can still walk up and see why it is unavailable.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class LevelSelectorConsole : NetworkBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private RouteSelectionEventChannel _routeSelectionEventChannel;
    [SerializeField] private LevelLaunchController _launchController;
    [SerializeField] private MeshOutline _outline;

    private bool IsCountingDown => Launcher && Launcher.CurrentState == LaunchState.Countdown;

    public string InteractionPrompt => IsCountingDown ? "[F] Cancel Deployment" : "[F] Select Level";
    public InteractionMode Mode => InteractionMode.Instant;
    public string HoldPrompt => string.Empty;
    public string TargetName => "Level Selector";
    public string Subtext => "Choose the next job";
    public Color AccentColor => new Color(0.2f, 0.8f, 1.0f);
    public float HoldDuration => 0f;

    private bool IsHostOrOffline
    {
        get
        {
            bool isOffline = !NetworkManager.main || NetworkManager.main.isOffline;
            bool isHost = isServer || (NetworkManager.main && NetworkManager.main.isServer);
            return isOffline || isHost;
        }
    }

    private void Awake()
    {
        if (!_outline) _outline = GetComponent<MeshOutline>();
        if (!_routeSelectionEventChannel)
        {
            _routeSelectionEventChannel =
                Resources.Load<RouteSelectionEventChannel>("Event Channels/RouteSelectionChannel");
        }
    }

    private LevelLaunchController Launcher
    {
        get
        {
            if (!_launchController)
                _launchController = LevelLaunchController.Instance
                    ? LevelLaunchController.Instance
                    : FindAnyObjectByType<LevelLaunchController>();
            return _launchController;
        }
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        return GetDisabledReason(interactor, isHold).Length == 0;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!IsHostOrOffline)
            return "Host only";

        if (IsCountingDown)
            return string.Empty; // host can cancel

        if (Launcher && Launcher.IsLaunchBlocked(out var reason))
            return reason;

        return string.Empty;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (IsCountingDown)
        {
            Launcher.CancelLaunch();
            return;
        }

        if (_routeSelectionEventChannel)
            _routeSelectionEventChannel.RaiseOnConsoleInteracted(this);
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        if (_outline) _outline.SetHighlighted(true);
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        if (_outline) _outline.SetHighlighted(false);
    }

    public void LaunchLevel(string sceneName)
    {
        if (IsHostOrOffline)
            LaunchLevelInternal(sceneName);
        else
            RpcRequestLaunchLevel(sceneName);
    }

    [ServerRpc(requireOwnership: false)]
    public void RpcRequestLaunchLevel(string sceneName, RPCInfo info = default)
    {
        if (networkManager && !networkManager.isOffline && info.sender != networkManager.localPlayer)
        {
            AppLog.LogWarning(LogChannel.Match,
                $"{nameof(LevelSelectorConsole)}: Non-host player {info.sender} attempted to launch a level.", this);
            return;
        }

        LaunchLevelInternal(sceneName);
    }

    private void LaunchLevelInternal(string sceneName)
    {
        if (!Launcher)
        {
            AppLog.LogError(LogChannel.Match, "LevelLaunchController not found in scene. Cannot launch level.", this);
            return;
        }

        Launcher.TryLaunch(sceneName);
    }
}
}
