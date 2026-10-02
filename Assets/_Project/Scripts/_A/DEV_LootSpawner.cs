using System;
using System.Collections.Generic;
using Project.Core.Interaction;
using Project.Loot;
using UnityEngine;

[Serializable]
class SpawnDef 
{
    public LootItemDefinition ItemDefinition;
    public Transform SpawnTransform;
    
    public GameObject SpawnedObject { get; set; }
    public bool IsValid => ItemDefinition && SpawnTransform;
    public bool IsSpawned => SpawnedObject;
}

public class DEV_LootSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private List<SpawnDef> _spawnDefs = new List<SpawnDef>();
    
    public string InteractionPrompt => "[F] Spawn Loot";
    public string HoldPrompt { get; }
    public string TargetName { get; }
    public string Subtext { get; }
    public Color AccentColor { get; }
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration { get; }
    
    private void Start()
    {
        CreateLoot();
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
        CreateLoot();
    }

    private void CreateLoot()
    {
        foreach (var spawnDef in _spawnDefs)
        {
            if (spawnDef.IsValid && !spawnDef.IsSpawned)
            {
                spawnDef.SpawnedObject = Instantiate(spawnDef.ItemDefinition.WorldPrefab, spawnDef.SpawnTransform.position, spawnDef.SpawnTransform.rotation);
            }
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        
    }
}