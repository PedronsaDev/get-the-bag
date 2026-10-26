using Plugins.Outline;
using Project.Core.Events;
using Project.Core.Interaction;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
[RequireComponent(typeof(NetworkIdentity))]
public class TrainRouteConsole : NetworkBehaviour, IInteractable
{
    [Header("References")] 
    [SerializeField] private RouteSelectionEventChannel _routeSelectionEventChannel;
    [SerializeField] private TrainTransitController _transitController;
    [SerializeField] private MeshOutline _outline;

    public string InteractionPrompt => "[F] Access Route Console";
    public InteractionMode Mode => InteractionMode.Instant;
    public string HoldPrompt => string.Empty;
    public string TargetName => "Navigation Console";
    public string Subtext => "Select destination route";
    public Color AccentColor => new Color(0.2f, 0.8f, 1.0f);
    public float HoldDuration => 0f;

    private void Awake()
    {
        if (!_routeSelectionEventChannel)
        {
            _routeSelectionEventChannel =
                Resources.Load<RouteSelectionEventChannel>("Event Channels/RouteSelectionChannel");
        }

        if (!_transitController)
        {
            _transitController = FindAnyObjectByType<TrainTransitController>();
        }
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!_transitController)
        {
            _transitController = FindAnyObjectByType<TrainTransitController>();
        }

        if (_transitController && _transitController.CurrentState.value != TransitState.DockedAtHub)
        {
            return false;
        }

        bool isOffline = !NetworkManager.main || NetworkManager.main.isOffline;
        bool isHost = isServer || (NetworkManager.main && NetworkManager.main.isServer);
        if (!isOffline && !isHost)
        {
            return false;
        }

        return true;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!_transitController)
        {
            _transitController = FindAnyObjectByType<TrainTransitController>();
        }

        if (_transitController && _transitController.CurrentState.value != TransitState.DockedAtHub)
        {
            return "Train in transit";
        }

        bool isOffline = !NetworkManager.main || NetworkManager.main.isOffline;
        bool isHost = isServer || (NetworkManager.main && NetworkManager.main.isServer);
        if (!isOffline && !isHost)
        {
            return "Host only";
        }

        return string.Empty;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (_routeSelectionEventChannel)
        {
            _routeSelectionEventChannel.RaiseOnConsoleInteracted(this);
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        _outline.enabled = true;
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        _outline.enabled = false;
    }

    public void LaunchRoute(string routeSceneName)
    {
        bool isOffline = !NetworkManager.main || NetworkManager.main.isOffline;
        bool isHost = isServer || (NetworkManager.main && NetworkManager.main.isServer);
        if (isOffline || isHost)
        {
            LaunchRouteInternal(routeSceneName);
        }
        else
        {
            RpcRequestLaunchRoute(routeSceneName);
        }
    }

    [ServerRpc(requireOwnership: false)]
    public void RpcRequestLaunchRoute(string routeSceneName, RPCInfo info = default)
    {
        if (networkManager && !networkManager.isOffline)
        {
            if (info.sender != networkManager.localPlayer)
            {
                AppLog.LogWarning(LogChannel.Match, $"{nameof(TrainRouteConsole)}: Non-host player {info.sender} attempted to launch route.", this);
                return;
            }
        }

        LaunchRouteInternal(routeSceneName);
    }

    private void LaunchRouteInternal(string routeSceneName)
    {
        if (!_transitController)
        {
            _transitController = FindAnyObjectByType<TrainTransitController>();
        }

        if (_transitController)
        {
            _transitController.TriggerTransit(routeSceneName);
        }
        else
        {
            AppLog.LogError(LogChannel.Match, "TrainTransitController not found in scene! Cannot launch route.", this);
        }
    }
}
}
