using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Content;
using PurrNet;
using UnityEngine;

namespace Project.Combat.Weapons
{
public class WeaponSpawnService
{
    // Existing prototype retained. Reserve network spawning for WORLD weapons.
    // TODO: Add overload accepting WeaponItemState and returning WorldWeapon.
    // Preserve ammo and publish initial state to all peers, including later observers.
    // Equipped models instead use local loading in PlayerWeaponPresenter.
    public static async UniTask<WeaponInstance> SpawnAsync(string id, Vector3 position, Quaternion rotation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = NetworkManager.main;
        
        if (!GameContent.IsLoaded || !manager || !manager.isServer)
            throw new InvalidOperationException("Weapon spawning requires loaded content and an active server.");
        
        if (!GameContent.WeaponRegistry.TryGetById(id, out var definition))
            throw new ArgumentException($"Weapon ID '{id}' is not registered.", nameof(id));
        
        if (definition.PrefabReference == null || !definition.PrefabReference.RuntimeKeyIsValid())
            throw new InvalidOperationException($"Weapon '{id}' has no valid prefab reference.");

        var instance =
            await manager.SpawnAddressableByGuidAsync(definition.PrefabReference.AssetGUID, position, rotation);
        
        if (!instance)
            throw new InvalidOperationException($"PurrNet could not spawn '{id}'. Check its Addressable network registration.");
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GameContent.IsLoaded || !manager || !manager.isServer)
                throw new OperationCanceledException("The spawning session ended.");
            
            if (!instance.TryGetComponent<WeaponInstance>(out var weapon))
                throw new InvalidOperationException($"Prefab for '{id}' needs a WeaponInstance script.");
            
            weapon.Initialize(definition);

            if (!weapon.Definition || !string.Equals(weapon.Definition.Id, definition.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"WeaponInstance for '{id}' failed local definition initialization.");
            
            return weapon;
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
