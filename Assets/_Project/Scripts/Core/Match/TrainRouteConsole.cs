using Project.Core.Events;
using Project.Core.Interaction;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
[RequireComponent(typeof(NetworkIdentity))]
public class TrainRouteConsole : NetworkBehaviour, IInteractable
{
    [Header("References")] [SerializeField]
    private RouteSelectionEventChannel _routeSelectionEventChannel;

    [SerializeField] private TrainTransitController _transitController;

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

        // Only interactable if the train is currently docked at the hub
        if (_transitController && _transitController.CurrentState.value != TransitState.DockedAtHub)
        {
            return false;
        }

        // Only the server/host can operate the console in networked play
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
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
    }

    /// <summary>
    /// Request to launch a route from the client/UI.
    /// </summary>
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
                Debug.LogWarning(
                    $"[{nameof(TrainRouteConsole)}] Non-host player {info.sender} attempted to launch route.");
                return;
            }
        }

        LaunchRouteInternal(routeSceneName);
    }

    private void LaunchRouteInternal(string routeSceneName)
    {
        Debug.Log($"[{nameof(TrainRouteConsole)}] Launching route to scene: '{routeSceneName}'");

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
            Debug.LogError(
                $"[{nameof(TrainRouteConsole)}] TrainTransitController not found in scene! Cannot launch route.", this);
        }
    }
}
}