using Plugins.Outline;
using Project.Core.Events;
using Project.Core.Interaction;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
[RequireComponent(typeof(NetworkIdentity))]
public class LevelSelectorConsole : NetworkBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private RouteSelectionEventChannel _routeSelectionEventChannel;
    [SerializeField] private LevelLaunchController _launchController;
    [SerializeField] private MeshOutline _outline;

    [Header("Settings")]
    [SerializeField, Min(0.2f)] private float _clearHoldSeconds = 1f;

    private bool HasSelection => Launcher && Launcher.HasSelection;
    private string SelectedName => Launcher ? LevelLaunchController.DisplayName(Launcher.Pending) : string.Empty;

    public string InteractionPrompt => HasSelection ? "[F] Change Level" : "[F] Select Level";
    public string HoldPrompt => HasSelection ? "[Hold F] Cancel Job" : string.Empty;
    public InteractionMode Mode => HasSelection ? InteractionMode.DualInstantAndHold : InteractionMode.Instant;
    public float HoldDuration => _clearHoldSeconds;
    public string TargetName => "Level Selector";
    public string Subtext => HasSelection ? $"Job: {SelectedName}" : "Choose the next job";
    public Color AccentColor => new Color(0.2f, 0.8f, 1.0f);

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
        return string.IsNullOrEmpty(GetDisabledReason(interactor, isHold));
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!IsHostOrOffline)
            return HasSelection ? $"Host only. Job: {SelectedName}" : "Host only";

        if (Launcher && Launcher.IsLoading)
            return "Deployment in progress";

        if (isHold && !HasSelection)
            return "No job selected";

        return string.Empty;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (isHold)
        {
            if (Launcher) Launcher.ClearSelection();
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

    public void SelectLevel(string sceneName)
    {
        if (IsHostOrOffline)
            SelectLevelInternal(sceneName);
        else
            RpcRequestSelectLevel(sceneName);
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcRequestSelectLevel(string sceneName, RPCInfo info = default)
    {
        if (networkManager && !networkManager.isOffline && info.sender != networkManager.localPlayer)
        {
            AppLog.LogWarning(LogChannel.Match,
                $"{nameof(LevelSelectorConsole)}: Non-host player {info.sender} attempted to select a level.", this);
            return;
        }

        SelectLevelInternal(sceneName);
    }

    private void SelectLevelInternal(string sceneName)
    {
        if (!Launcher)
        {
            AppLog.LogError(LogChannel.Match, "LevelLaunchController not found in scene. Cannot select level.", this);
            return;
        }

        Launcher.TrySelectLevel(sceneName);
    }
}
}
