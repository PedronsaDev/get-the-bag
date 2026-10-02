using System;
using Project.Core.Interaction;
using Project.Loot;
using UnityEngine;

public class DEV_DufflebagSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject _dufflebagPrefab;
    [SerializeField] private Transform _spawnPoint;


    public string InteractionPrompt => "[F] Spawn Duffle Bag";
    public string HoldPrompt { get; }
    public string TargetName { get; }
    public string Subtext { get; }
    public Color AccentColor { get; }
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration { get; }
    
    private void Start()
    {
        CreateBag();
    }
    
    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        return true;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        return null;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        CreateBag();
    }

    private void CreateBag()
    {
        if (Instantiate(_dufflebagPrefab, _spawnPoint.position, _spawnPoint.rotation).TryGetComponent(out WorldDuffleBag bag))
        {
            bag.Initialize(DuffleBagSnapshot.Empty);
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        
    }
}