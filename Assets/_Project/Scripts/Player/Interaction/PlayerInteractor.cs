using System;
using Project.Core.Interaction;
using Project.Core.UI;
using PurrNet;
using UnityEngine;

namespace Project.Player.Interaction
{
public class PlayerInteractor : NetworkBehaviour, IPlayerInteractor
{
    [Header("Detection Settings")] [SerializeField]
    private float _detectionRange = 3.0f;

    [SerializeField] private float _assistRadius = 0.4f;
    [SerializeField] private float _assistRange = 3.0f;
    [SerializeField] private LayerMask _interactableLayers;

    [Header("References")] [SerializeField]
    private PlayerFacade _playerFacade;

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

    private void Awake()
    {
        if (_interactableLayers.value == 0)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                _interactableLayers = 1 << interactableLayer;
            }
        }

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
        if (_currentTarget == null || !_currentTarget.CanInteract(this))
        {
            ResetHold();
            return;
        }

        var input = _playerFacade ? _playerFacade.CurrentInput : default;

        switch (_currentTarget.Mode)
        {
            case InteractionMode.Instant:
                if (input.InteractPressed)
                {
                    ExecuteInteraction(isHold: false);
                }

                break;
            case InteractionMode.Hold:
                if (input.InteractHeld)
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
                    _isHolding = true;
                    _currentHoldTimer = 0f;
                }

                if (_isHolding && input.InteractHeld)
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
                else if (_isHolding && !input.InteractHeld)
                {
                    ExecuteInteraction(isHold: false);
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