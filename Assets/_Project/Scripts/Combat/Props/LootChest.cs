using Project.Core.Interaction;
using PurrNet;
using UnityEngine;

namespace Project.Combat.Props
{
public class LootChest : NetworkBehaviour, IInteractable
{
    [SerializeField] private string _interactionPrompt = "Press [F] to open";
    [SerializeField] private InteractionMode _interactionMode = InteractionMode.Instant;

    public string InteractionPrompt => _interactionPrompt;
    public string HoldPrompt => null;
    public string TargetName { get; }
    public string Subtext { get; }
    public Color AccentColor { get; }
    public InteractionMode Mode => _interactionMode;
    public float HoldDuration { get; }

    public bool CanInteract(IPlayerInteractor interactor)
    {
        return true;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
    }
}
}