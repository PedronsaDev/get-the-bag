using System;
using Project.Core.Interaction;
using PurrNet;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Combat.Props
{
[RequireComponent(typeof(Collider))]
public class SimpleInteractable : NetworkBehaviour, IInteractable
{
    [Header("Identity & Prompts")] 
    [SerializeField] private string _interactionPrompt = "[F] Activate";
    [SerializeField] private string _holdPrompt = "";
    [SerializeField] private string _targetName = "Switch";
    [SerializeField] private string _subtext = "";
    [SerializeField] private Color _accentColor = Color.white;

    [Header("Interaction Mode")] 
    [SerializeField] private InteractionMode _mode = InteractionMode.Instant;
    [SerializeField] private float _holdDuration = 0.5f;

    [Header("Availability & Disabled State")] 
    [SerializeField] private bool _isInteractable = true;
    [SerializeField] private string _disabledReason = "No Power";

    [Header("Events")] 
    [SerializeField] private UnityEvent<IPlayerInteractor> _onInteracted;
    [SerializeField] private UnityEvent<IPlayerInteractor, string> _onInteractionDenied;

    public event Action<IPlayerInteractor> OnInteracted;
    public event Action<IPlayerInteractor, string> OnInteractionDenied;

    public string InteractionPrompt => _interactionPrompt;
    public string HoldPrompt => _holdPrompt;
    public string TargetName => _targetName;
    public string Subtext => _subtext;
    public Color AccentColor => _accentColor;
    public InteractionMode Mode => _mode;
    public float HoldDuration => _holdDuration;

    public bool IsInteractable => _isInteractable;
    public string DisabledReason => _disabledReason;

    private void Awake()
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            gameObject.layer = interactableLayer;
        }
    }

    public void SetInteractable(bool isInteractable, string disabledReason = null)
    {
        _isInteractable = isInteractable;
        if (disabledReason != null)
            _disabledReason = disabledReason;
    }

    public void SetDisabledReason(string disabledReason)
    {
        _disabledReason = disabledReason;
    }

    public void SetPrompts(string interactionPrompt, string holdPrompt = null)
    {
        _interactionPrompt = interactionPrompt;
        if (holdPrompt != null)
            _holdPrompt = holdPrompt;
    }

    public void SetIdentity(string targetName, string subtext, Color? accentColor = null)
    {
        _targetName = targetName;
        _subtext = subtext;
        if (accentColor.HasValue)
            _accentColor = accentColor.Value;
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        return _isInteractable && interactor != null;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!_isInteractable)
            return _disabledReason;

        return null;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (!_isInteractable)
        {
            _onInteractionDenied?.Invoke(interactor, _disabledReason);
            OnInteractionDenied?.Invoke(interactor, _disabledReason);
            return;
        }

        _onInteracted?.Invoke(interactor);
        OnInteracted?.Invoke(interactor);
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
    }
}
}