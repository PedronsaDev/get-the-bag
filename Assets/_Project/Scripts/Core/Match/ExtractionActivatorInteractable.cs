using Project.Core.Interaction;
using Project.Core.Match.Zones;
using PurrNet;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Core.Match
{
public class ExtractionActivatorInteractable : NetworkBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private ExtractionZone _extractionZone;
    
    public SyncVar<bool> ExtractionActivated = new();
    
    public string InteractionPrompt => "[F] Activate Extraction";
    public string HoldPrompt { get; }
    public string TargetName { get; }
    public string Subtext { get; }
    public Color AccentColor { get; }
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration { get; }
    
    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        return !ExtractionActivated.value;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        return "Extraction is already active.";
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        _extractionZone.ActivateExtraction();
        ExtractionActivated.value = true;
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        
    }
}
}