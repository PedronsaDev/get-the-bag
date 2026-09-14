using System;
using Project.Core.Interaction;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Environment
{
[DisallowMultipleComponent]
public class BasicDoor : NetworkBehaviour, IInteractable
{
    private static readonly int OnOpenHash = Animator.StringToHash("OnOpen");
    private static readonly int IsOpenHash = Animator.StringToHash("IsOpen");
    private static readonly int OnCloseHash = Animator.StringToHash("OnClose");

    [Header("References")] 
    [SerializeField] private Animator _doorAnimator;
    [SerializeField] private Collider _doorCollider;
    [SerializeField] private bool _toggleCollider = false;

    [Header("State")] public SyncVar<bool> IsOpen = new(false);

    [Header("Interaction (Optional)")] 
    [SerializeField] private bool _isInteractable = false;
    [SerializeField] private string _openPrompt = "[F] Open";
    [SerializeField] private string _closePrompt = "[F] Close";
    [SerializeField] private string _targetName = "Door";
    [SerializeField] private string _subtext = "";
    [SerializeField] private string _lockedReason = "Locked";
    [SerializeField] private Color _accentColor = Color.white;

    public event Action<bool> OnDoorStateChanged;
    public event Action OnDoorOpened;
    public event Action OnDoorClosed;

    #region IInteractable Implementation

    public string InteractionPrompt => IsOpen.value ? _closePrompt : _openPrompt;
    public string HoldPrompt => "";
    public string TargetName => _targetName;
    public string Subtext => _subtext;
    public Color AccentColor => _accentColor;
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration => 0f;

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false) => _isInteractable;

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false) =>
        _isInteractable ? null : _lockedReason;

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (!_isInteractable) return;
        ToggleDoor();
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
    }

    #endregion

    private void Awake()
    {
        if (!_doorAnimator)
            _doorAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

        if (!_doorCollider)
            _doorCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();

        if (_isInteractable)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
                gameObject.layer = interactableLayer;
        }
    }

    private void OnEnable()
    {
        IsOpen.onChanged += HandleDoorStateChanged;
    }

    private void Start()
    {
        if (!isSpawned)
        {
            UpdateVisuals(IsOpen.value, immediate: true);
        }
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        UpdateVisuals(IsOpen.value, immediate: true);
    }

    private void OnDisable()
    {
        IsOpen.onChanged -= HandleDoorStateChanged;
    }

    private void HandleDoorStateChanged(bool isOpen)
    {
        UpdateVisuals(isOpen, immediate: false);

        OnDoorStateChanged?.Invoke(isOpen);
        if (isOpen)
            OnDoorOpened?.Invoke();
        else
            OnDoorClosed?.Invoke();
    }

    public void UpdateVisuals(bool isOpen, bool immediate)
    {
        if (_toggleCollider && _doorCollider)
        {
            _doorCollider.enabled = !isOpen;
        }

        if (!_doorAnimator) return;

        _doorAnimator.SetBool(IsOpenHash, isOpen);

        if (immediate)
        {
            string stateName = isOpen ? "BasicDoor_IdleOpen" : "BasicDoor_IdleClosed";
            _doorAnimator.Play(stateName, 0, 1f);
        }
        else
        {
            _doorAnimator.SetTrigger(isOpen ? OnOpenHash : OnCloseHash);
        }
    }

    private bool IsServerOrOffline =>
        !NetworkManager.main || NetworkManager.main.isOffline ||
        (NetworkManager.main != null && NetworkManager.main.isServer) || isServer;

    public void OpenDoor() => SetDoorState(true);
    public void CloseDoor() => SetDoorState(false);
    public void ToggleDoor() => SetDoorState(!IsOpen.value);

    public void SetDoorState(bool isOpen)
    {
        if (IsServerOrOffline)
        {
            if (IsOpen.value != isOpen)
            {
                IsOpen.value = isOpen;
                if (!isSpawned)
                {
                    HandleDoorStateChanged(isOpen);
                }
            }
        }
        else
        {
            if (!isSpawned)
            {
                AppLog.LogWarning(LogChannel.Match, "Cannot request door state change via RPC before object is spawned over network.", this);
                return;
            }

            RequestSetDoorStateServerRpc(isOpen);
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void RequestSetDoorStateServerRpc(bool isOpen, RPCInfo info = default)
    {
        if (IsOpen.value != isOpen)
        {
            IsOpen.value = isOpen;
        }
    }
}
}