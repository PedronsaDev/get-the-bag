using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Content;
using PurrNet;
using UnityEngine;

namespace Project.Loot
{
public static class LootSpawnService
{
    public static async UniTask<LootInteractable> SpawnAsync(string id, Vector3 position, Quaternion rotation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = NetworkManager.main;
        
        if (!GameContent.IsLoaded || !manager || !manager.isServer)
            throw new InvalidOperationException("Loot spawning requires loaded content and an active server.");
        
        if (!GameContent.LootRegistry.TryGetById(id, out var definition))
            throw new ArgumentException($"Loot ID '{id}' is not registered.", nameof(id));
        
        if (definition.PrefabReference == null || !definition.PrefabReference.RuntimeKeyIsValid())
            throw new InvalidOperationException($"Loot '{id}' has no valid prefab reference.");

        var instance =
            await manager.SpawnAddressableByGuidAsync(definition.PrefabReference.AssetGUID, position, rotation);
        
        if (!instance)
            throw new InvalidOperationException($"PurrNet could not spawn '{id}'. Check its Addressable network registration.");
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GameContent.IsLoaded || !manager || !manager.isServer)
                throw new OperationCanceledException("The spawning session ended.");
            
            if (!instance.TryGetComponent<LootInteractable>(out var loot))
                throw new InvalidOperationException($"Prefab for '{id}' needs a matching LootInteractable definition on every peer.");
            
            loot.Initialize(definition);

            if (!loot.Definition || !string.Equals(loot.Definition.Id, definition.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"LootInteractable for '{id}' failed to initialize with the correct definition.");
            
            return loot;
        }
        catch
        {
            if (manager && manager.isServer) manager.DespawnAddressable(instance);
            else if (instance) UnityEngine.Object.Destroy(instance);
            throw;
        }
    }
}
}