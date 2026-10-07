using UnityEngine;

namespace Project.Core.Interaction
{
public interface IInteractable
{
    string InteractionPrompt { get; }
    string HoldPrompt { get; }
    string TargetName { get; }
    string Subtext { get; }

    Color AccentColor { get; }

    InteractionMode Mode { get; }
    float HoldDuration { get; }

    bool CanInteract(IPlayerInteractor interactor, bool isHold = false);
    string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false);
    void OnInteract(IPlayerInteractor interactor, bool isHold);
    void OnHoverEnter(IPlayerInteractor interactor);
    void OnHoverExit(IPlayerInteractor interactor);
}
}