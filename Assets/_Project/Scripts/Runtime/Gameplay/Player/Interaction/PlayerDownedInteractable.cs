using Project.Core.Interaction;
using Project.Player.Locomotion;
using PurrNet;
using UnityEngine;

namespace Project.Player.Interaction
{
public class PlayerDownedInteractable : NetworkBehaviour, IInteractable
{
    [Header("References")] 
    [SerializeField] private Collider _interactionCollider;
    [SerializeField] private PlayerDownedState _playerDownedState;

    [Header("Settings")] 
    [SerializeField] private float _holdDuration = 3.5f;
    [SerializeField] private Color _accentColor = new Color(1f, 0.35f, 0.35f, 1f);

    public string InteractionPrompt
    {
        get
        {
            if (_playerDownedState && _playerDownedState.IsBeingDraggedByLocalPlayer())
                return "[F] Release Drag";
            return "[F] Drag Teammate";
        }
    }

    public string HoldPrompt => "[Hold F] Revive";
    public string TargetName => "Downed Teammate";

    public string Subtext
    {
        get
        {
            if (_playerDownedState && _playerDownedState.IsDown.value)
                return $"Bleeding Out: {Mathf.CeilToInt(_playerDownedState.BleedOutTimer.value)}s";
            return "";
        }
    }

    public Color AccentColor => _accentColor;
    public InteractionMode Mode => InteractionMode.DualInstantAndHold;
    public float HoldDuration => _holdDuration;

    private void Awake()
    {
        if (!_playerDownedState)
            _playerDownedState = GetComponent<PlayerDownedState>() ?? GetComponentInParent<PlayerDownedState>() ??
                GetComponentInChildren<PlayerDownedState>();

        if (!_interactionCollider)
            _interactionCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();

        if (_interactionCollider)
        {
            _interactionCollider.isTrigger = true;

            if (_interactionCollider is SphereCollider sphere)
            {
                sphere.radius = Mathf.Max(sphere.radius, 1.25f);
                sphere.center = new Vector3(0f, 0.6f, 0f);
            }

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                gameObject.layer = interactableLayer;
                _interactionCollider.gameObject.layer = interactableLayer;
            }
        }
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!_playerDownedState || !_playerDownedState.IsDown.value)
            return false;

        if (interactor == null || !interactor.InteractorGameObject)
            return false;

        if (interactor.InteractorGameObject == gameObject ||
            interactor.InteractorGameObject.transform.root == transform.root)
            return false;

        var interactorDowned = interactor.InteractorGameObject.GetComponentInParent<PlayerDownedState>() ??
                               interactor.InteractorGameObject.GetComponentInChildren<PlayerDownedState>();
        if (interactorDowned && interactorDowned.IsDown.value)
            return false;

        if (isHold)
            return true;

        if (_playerDownedState.IsBeingDraggedByLocalPlayer())
            return true;

        return !_playerDownedState.IsBeingDragged;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!_playerDownedState || !_playerDownedState.IsDown.value)
            return "Teammate is not downed";

        if (interactor != null && interactor.InteractorGameObject)
        {
            if (interactor.InteractorGameObject == gameObject ||
                interactor.InteractorGameObject.transform.root == transform.root)
                return "";

            var interactorDowned = interactor.InteractorGameObject.GetComponentInParent<PlayerDownedState>() ??
                                   interactor.InteractorGameObject.GetComponentInChildren<PlayerDownedState>();
            if (interactorDowned && interactorDowned.IsDown.value)
                return "Cannot interact while downed";
        }

        if (!isHold && _playerDownedState.IsBeingDragged && !_playerDownedState.IsBeingDraggedByLocalPlayer())
            return "Teammate is already being dragged";

        return "";
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (!_playerDownedState) return;

        if (isHold)
        {
            _playerDownedState.RequestRevive();
        }
        else
        {
            if (_playerDownedState.IsBeingDraggedByLocalPlayer())
            {
                _playerDownedState.RequestStopDrag();
            }
            else
            {
                _playerDownedState.RequestStartDrag();
            }
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
    }

    public void Enable()
    {
        if (_interactionCollider)
            _interactionCollider.enabled = true;
    }

    public void Disable()
    {
        if (_interactionCollider)
            _interactionCollider.enabled = false;
    }
}
}