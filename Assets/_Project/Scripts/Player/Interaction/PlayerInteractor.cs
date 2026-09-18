using System;
using Project.Core.Interaction;
using Project.Core.UI;
using Project.Player.Input;
using PurrNet;
using UnityEngine;

namespace Project.Player.Interaction
{
public class PlayerInteractor : NetworkBehaviour, IPlayerInteractor
{
    [Header("Detection Settings")] 
    [SerializeField] private float _detectionRange = 3.5f;
    [SerializeField] private float _assistRadius = 0.5f;
    [SerializeField] private float _assistRange = 3.5f;
    [SerializeField] private LayerMask _interactableLayers;

    [Header("References")] 
    [SerializeField] private LocalPlayerInputProvider _inputProvider;
    [SerializeField] private PlayerFacade _playerFacade;
    [SerializeField] private FPSCameraController _cameraController;

    private IInteractable _currentTarget;
    private float _currentHoldTimer;
    private bool _isHolding;

    public GameObject InteractorGameObject => this.gameObject;
    public Transform AimTransform => _cameraController ? _cameraController.PitchPivot : transform;
    public IInteractable CurrentTarget => _currentTarget;

    public float CurrentHoldProgress => _currentTarget is { HoldDuration: > 0f }
        ? Mathf.Clamp01(_currentHoldTimer / _currentTarget.HoldDuration)
        : 0f;

    public event Action<IInteractable> OnTargetChanged;
    public event Action<float> OnHoldProgressChanged;
    public event Action<IInteractable, bool> OnInteractionExecuted;
    public event Action<IInteractable, string, bool> OnInteractionDenied;

    private void Awake()
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            _interactableLayers |= (1 << interactableLayer);
        }

        if (!_inputProvider)
            _inputProvider = GetComponent<LocalPlayerInputProvider>() ?? GetComponentInParent<LocalPlayerInputProvider>() ?? GetComponentInChildren<LocalPlayerInputProvider>();

        if (!_playerFacade)
            _playerFacade = GetComponentInParent<PlayerFacade>();

        if (!_cameraController)
            _cameraController = GetComponentInChildren<FPSCameraController>();
    }

    private void Update()
    {
        if (!isOwner)
            return;

        if (UIBlockerSystem.IsActionsBlocked)
        {
            ResetHold();
            if (_currentTarget != null)
            {
                _currentTarget.OnHoverExit(this);
                _currentTarget = null;
                OnTargetChanged?.Invoke(null);
            }
            return;
        }

        UpdateTargetDetection();
        UpdateInteractionInput();
    }

    private void UpdateTargetDetection()
    {
        var aim = AimTransform;
        var origin = aim.position;
        var forward = aim.forward;

        IInteractable foundInteractable = null;

        if (Physics.Raycast(origin, forward, out RaycastHit hit, _detectionRange, _interactableLayers,
                QueryTriggerInteraction.Collide))
        {
            foundInteractable = hit.collider.GetComponentInParent<IInteractable>();
        }

        if (foundInteractable == null && Physics.SphereCast(origin, _assistRadius, forward, out RaycastHit sphereHit,
                _assistRange, _interactableLayers, QueryTriggerInteraction.Collide))
        {
            foundInteractable = sphereHit.collider.GetComponentInParent<IInteractable>();
        }

        if (foundInteractable != _currentTarget)
        {
            ResetHold();
            _currentTarget?.OnHoverExit(this);
            _currentTarget = foundInteractable;
            _currentTarget?.OnHoverEnter(this);
            OnTargetChanged?.Invoke(_currentTarget);
        }
    }

    private void UpdateInteractionInput()
    {
        if (_currentTarget == null)
        {
            ResetHold();
            return;
        }

        var input = _inputProvider ? _inputProvider.GetCurrentInput() : (_playerFacade ? _playerFacade.CurrentInput : default);

        switch (_currentTarget.Mode)
        {
            case InteractionMode.Instant:
                if (input.InteractPressed)
                {
                    if (_currentTarget.CanInteract(this, isHold: false))
                    {
                        ExecuteInteraction(isHold: false);
                    }
                    else
                    {
                        string reason = _currentTarget.GetDisabledReason(this, isHold: false);
                        OnInteractionDenied?.Invoke(_currentTarget, reason, false);
                    }
                }

                break;
            case InteractionMode.Hold:
                bool canHoldAction = _currentTarget.CanInteract(this, isHold: true);
                if (input.InteractPressed && !canHoldAction)
                {
                    string reason = _currentTarget.GetDisabledReason(this, isHold: true);
                    OnInteractionDenied?.Invoke(_currentTarget, reason, true);
                }

                if (canHoldAction && input.InteractHeld)
                {
                    _currentHoldTimer += Time.deltaTime;
                    OnHoldProgressChanged?.Invoke(CurrentHoldProgress);
                    if (_currentHoldTimer >= _currentTarget.HoldDuration)
                    {
                        ExecuteInteraction(isHold: true);
                        ResetHold();
                    }
                }
                else if (_currentHoldTimer > 0f)
                {
                    ResetHold();
                }

                break;
            case InteractionMode.DualInstantAndHold:
                if (input.InteractPressed)
                {
                    bool canHold = _currentTarget.CanInteract(this, isHold: true);
                    bool canTap = _currentTarget.CanInteract(this, isHold: false);

                    if (!canHold && !canTap)
                    {
                        string reason = _currentTarget.GetDisabledReason(this, isHold: false) 
                                     ?? _currentTarget.GetDisabledReason(this, isHold: true);
                        OnInteractionDenied?.Invoke(_currentTarget, reason, false);
                    }
                    else
                    {
                        _isHolding = true;
                        _currentHoldTimer = 0f;
                    }
                }

                if (_isHolding && input.InteractHeld)
                {
                    bool canHold = _currentTarget.CanInteract(this, isHold: true);
                    if (canHold)
                    {
                        _currentHoldTimer += Time.deltaTime;
                        OnHoldProgressChanged?.Invoke(CurrentHoldProgress);
                        if (_currentHoldTimer >= _currentTarget.HoldDuration)
                        {
                            ExecuteInteraction(isHold: true);
                            _isHolding = false;
                            ResetHold();
                        }
                    }
                }
                else if (_isHolding && !input.InteractHeld)
                {
                    // Released before hold duration reached: player intended a Tap
                    if (_currentTarget.CanInteract(this, isHold: false))
                    {
                        ExecuteInteraction(isHold: false);
                    }
                    else
                    {
                        string reason = _currentTarget.GetDisabledReason(this, isHold: false);
                        OnInteractionDenied?.Invoke(_currentTarget, reason, false);
                    }

                    _isHolding = false;
                    ResetHold();
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ExecuteInteraction(bool isHold)
    {
        if (_currentTarget == null) return;
        var target = _currentTarget;

        if (!target.CanInteract(this, isHold))
        {
            string reason = target.GetDisabledReason(this, isHold);
            OnInteractionDenied?.Invoke(target, reason, isHold);
            return;
        }

        target.OnInteract(this, isHold);
        OnInteractionExecuted?.Invoke(target, isHold);
    }

    private void ResetHold()
    {
        _isHolding = false;
        if (_currentHoldTimer > 0f)
        {
            _currentHoldTimer = 0f;
            OnHoldProgressChanged?.Invoke(0f);
        }
    }
}
}