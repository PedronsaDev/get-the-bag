using Project.Core.Environment;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{

[DisallowMultipleComponent]
public class TrainDoorController : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private BasicDoor _door;

    private bool _isSubscribed;

    private bool IsServerOrOffline => !NetworkManager.main || NetworkManager.main.isOffline || NetworkManager.main.isServer;

    private void Awake()
    {
        if (!_door)
            _door = GetComponent<BasicDoor>() ?? GetComponentInChildren<BasicDoor>();
    }

    private void OnEnable()
    {
        TrySubscribeAndSync();
    }

    private void Start()
    {
        TrySubscribeAndSync();
    }

    private void OnDisable()
    {
        UnsubscribeFromTransitEvents();
    }

    private void TrySubscribeAndSync()
    {
        if (!TrainTransitController.Instance) return;

        SubscribeToTransitEvents();
        SyncWithCurrentTransitState();
    }

    private void SubscribeToTransitEvents()
    {
        if (_isSubscribed || !TrainTransitController.Instance) return;

        TrainTransitController.Instance.OnDoorsOpened += HandleDoorsOpened;
        TrainTransitController.Instance.OnDoorsClosed += HandleDoorsClosed;
        TrainTransitController.Instance.OnTransitStateChanged += HandleTransitStateChanged;
        _isSubscribed = true;
    }

    private void UnsubscribeFromTransitEvents()
    {
        if (!_isSubscribed || !TrainTransitController.Instance) return;

        TrainTransitController.Instance.OnDoorsOpened -= HandleDoorsOpened;
        TrainTransitController.Instance.OnDoorsClosed -= HandleDoorsClosed;
        TrainTransitController.Instance.OnTransitStateChanged -= HandleTransitStateChanged;
        _isSubscribed = false;
    }

    private void HandleTransitStateChanged(TransitState state)
    {
        if (!IsServerOrOffline || !_door) return;

        bool shouldBeOpen = state is TransitState.DockedAtHub or TransitState.Arrived;
        _door.SetDoorState(shouldBeOpen);
    }

    private void HandleDoorsOpened()
    {
        if (!IsServerOrOffline || !_door) return;
        _door.OpenDoor();
    }

    private void HandleDoorsClosed()
    {
        if (!IsServerOrOffline || !_door) return;
        _door.CloseDoor();
    }

    private void SyncWithCurrentTransitState()
    {
        if (!IsServerOrOffline || !_door || !TrainTransitController.Instance) return;

        TransitState state = TrainTransitController.Instance.CurrentState.value;
        bool shouldBeOpen = state is TransitState.DockedAtHub or TransitState.Arrived;
        _door.SetDoorState(shouldBeOpen);
    }
}
}
