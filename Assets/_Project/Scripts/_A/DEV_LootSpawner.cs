using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Interaction;
using Project.Game;
using Project.Logging;
using Project.Loot;
using Project.Loot.Data;
using PurrNet;
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
    private bool _isSpawning;

    public string InteractionPrompt => "[F] Spawn Loot";
    public string HoldPrompt => null;
    public string TargetName => "Loot Spawner";
    public string Subtext => null;
    public Color AccentColor => Color.white;
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration => 0f;

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false) =>
        isActiveAndEnabled && !_isSpawning && GameContent.IsLoaded &&
        NetworkManager.main && NetworkManager.main.isServer;

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!GameContent.IsLoaded) return "Game content is not ready.";
        if (!NetworkManager.main || !NetworkManager.main.isServer)
            return "Only the host can spawn test loot.";
        return _isSpawning ? "Loot is spawning." : null;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        CreateLootAsync().Forget(exception =>
            AppLog.LogException(LogChannel.Network, exception, this));
    }

    private async UniTask CreateLootAsync()
    {
        _isSpawning = true;
        var manager = NetworkManager.main;
        try
        {
            foreach (var spawnDef in _spawnDefs)
            {
                if (!this || !isActiveAndEnabled || !GameContent.IsLoaded || !manager || !manager.isServer)
                    return;
                if (spawnDef == null || !spawnDef.IsValid || spawnDef.IsSpawned)
                    continue;

                if (!GameContent.LootRegistry.TryGetById(spawnDef.ItemDefinition.Id, out var definition))
                {
                    AppLog.LogWarning(LogChannel.Network, $"Loot ID '{spawnDef.ItemDefinition.Id}' is not registered.", this);
                    continue;
                }
                if (definition.PrefabReference == null || !definition.PrefabReference.RuntimeKeyIsValid())
                {
                    AppLog.LogWarning(LogChannel.Network, $"Loot '{definition.Id}' has no valid prefab reference.", this);
                    continue;
                }

                GameObject instance;
                try
                {
                    instance = await manager.SpawnAddressableByGuidAsync(definition.PrefabReference.AssetGUID,
                        spawnDef.SpawnTransform.position, spawnDef.SpawnTransform.rotation);
                }
                catch (Exception exception)
                {
                    AppLog.LogException(LogChannel.Network, exception, this);
                    continue;
                }
                if (!instance)
                {
                    AppLog.LogWarning(LogChannel.Network, $"PurrNet could not spawn loot '{definition.Id}'. Check its network Addressables registration.", this);
                    continue;
                }

                if (!this || !isActiveAndEnabled || !GameContent.IsLoaded || !manager || !manager.isServer)
                {
                    ReleaseSpawn(manager, instance);
                    return;
                }

                if (!instance.TryGetComponent<LootInteractable>(out var loot) || !loot.ItemDefinition ||
                    !string.Equals(loot.ItemDefinition.Id, definition.Id, StringComparison.OrdinalIgnoreCase))
                {
                    AppLog.LogError(LogChannel.Network, $"Loot prefab for '{definition.Id}' must have a matching LootInteractable definition.", this);
                    ReleaseSpawn(manager, instance);
                    continue;
                }
                loot.Initialize(definition);
                spawnDef.SpawnedObject = instance;
            }
        }
        finally
        {
            _isSpawning = false;
        }
    }

    private static void ReleaseSpawn(NetworkManager manager, GameObject instance)
    {
        if (manager && manager.isServer) manager.DespawnAddressable(instance);
        else if (instance) Destroy(instance);
    }

    public void OnHoverEnter(IPlayerInteractor interactor) { }
    public void OnHoverExit(IPlayerInteractor interactor) { }
}
