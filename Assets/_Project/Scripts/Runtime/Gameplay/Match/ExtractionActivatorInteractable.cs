using System;
using Project.Core.Interaction;
using Project.Core.Match.Zones;
using PurrNet;
using UnityEngine;

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
    
    private bool IsServerAuthority =>
        isServer || (NetworkManager.main && NetworkManager.main.isServer) ||
        (!NetworkManager.main || NetworkManager.main.isOffline);

    private void Awake()
    {
        if (!_extractionZone)
        {
            _extractionZone = FindAnyObjectByType<ExtractionZone>();
        }
    }

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
        if (ExtractionActivated.value)
            return;

        if (IsServerAuthority)
        {
            TriggerActivationServer();
        }
        else
        {
            RpcRequestActivateExtraction();
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcRequestActivateExtraction(RPCInfo info = default)
    {
        TriggerActivationServer();
    }

    private void TriggerActivationServer()
    {
        if (ExtractionActivated.value)
            return;

        ExtractionActivated.value = true;

        if (!_extractionZone)
        {
            _extractionZone = FindAnyObjectByType<ExtractionZone>();
        }

        if (_extractionZone)
        {
            _extractionZone.ActivateExtraction();
        }
        else
        {
            Debug.LogError($"[{nameof(ExtractionActivatorInteractable)}] ExtractionZone not found in scene!", this);
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_extractionZone)
        {
            Gizmos.color = Color.yellowNice;
            Gizmos.DrawSphere(transform.position, 2f);
            return;
        }
        
        Gizmos.color = Color.red;
        
        if (_extractionZone.IsActivated) 
            Gizmos.color = Color.green;
        
        Gizmos.DrawLine(transform.position, _extractionZone.transform.position);
    }
#endif
}
}
