using System;
using Cysharp.Threading.Tasks;
using Project.Combat.Data;
using Project.Logging;
using Project.Loot.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Project.Content
{
public enum GameContentLoadState
{
    NotLoaded,
    Loading,
    Ready,
    Failed,
    Stopped
}

public static class GameContent
{

    private static GameContentLoadState _loadState = GameContentLoadState.NotLoaded;
    private static UniTaskCompletionSource _loadCompletionSource = new();
    private static UniTaskCompletionSource _shutdownCompletionSource = new();
    
    private static bool _shutdownRequested = false;
    private static int _generation;
    
    private static AsyncOperationHandle<LootRegistry> _lootRegistryHandle;
    private static LootRegistry _lootRegistry;

    private static AsyncOperationHandle<WeaponRegistry> _weaponRegistryHandle;
    private static WeaponRegistry _weaponRegistry;
    
    public static bool IsLoaded => _loadState == GameContentLoadState.Ready;
    public static LootRegistry LootRegistry => _lootRegistry;
    public static WeaponRegistry WeaponRegistry => _weaponRegistry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _generation++;
        UnloadRegistries();
        _loadCompletionSource.TrySetCanceled();
        _shutdownCompletionSource.TrySetResult();
        _loadState = GameContentLoadState.NotLoaded;
        _loadCompletionSource = new UniTaskCompletionSource();
        _shutdownCompletionSource = new UniTaskCompletionSource();
        _shutdownRequested = false;
        
        _lootRegistryHandle = default;
        _lootRegistry = null;
        
        _weaponRegistryHandle = default;
        _weaponRegistry = null;
    }

    public static async UniTask InitializeRegistriesAsync()
    {
        if (_shutdownRequested || _loadState == GameContentLoadState.Stopped)
        {
            throw new OperationCanceledException("Game content is shutting down.");
        }
        
        switch (_loadState)
        {
            case GameContentLoadState.Ready:
                AppLog.Log(LogChannel.General, "[GameContent]: Game content already loaded. Skipping initialization.");
                return;
            case GameContentLoadState.Loading:
            case GameContentLoadState.Failed:
                await _loadCompletionSource.Task;
                return;
            default:
                break;
        }

        _loadState = GameContentLoadState.Loading;
        var generation = _generation;
        var completion = _loadCompletionSource;
        
        try
        {
            // Loot registry
            _lootRegistryHandle = Addressables.LoadAssetAsync<LootRegistry>("LootRegistry");
            var lootRegistry = await _lootRegistryHandle.ToUniTask();
            
            // Weapon registry
            _weaponRegistryHandle = Addressables.LoadAssetAsync<WeaponRegistry>("WeaponRegistry");
            var weaponRegistry = await _weaponRegistryHandle.ToUniTask();

            if (generation != _generation)
                throw new OperationCanceledException("The content lifetime has ended.");

            if (_shutdownRequested)
            {
                throw new OperationCanceledException("Shutdown interrupted content initialization.");
            }
            
            lootRegistry.InitializeLookup();
            _lootRegistry = lootRegistry;
            
            weaponRegistry.InitializeLookup();
            _weaponRegistry = weaponRegistry;
            
        }
        catch (Exception e)
        {
            if (generation != _generation)
            {
                completion.TrySetCanceled();
                throw new OperationCanceledException("The content lifetime has ended.", e);
            }

            UnloadRegistries();

            if (_shutdownRequested)
            {
                CompleteShutdown();
                _loadCompletionSource.TrySetCanceled();
                throw new OperationCanceledException("Shutdown interrupted content initialization.", e);
            }
            
            _loadState = GameContentLoadState.Failed;
            _loadCompletionSource.TrySetException(e);
            
            AppLog.LogException(LogChannel.General, e);
            throw;
        }
        
        _loadState = GameContentLoadState.Ready;
        _loadCompletionSource.TrySetResult();
    }

    private static void UnloadRegistries()
    {
        _lootRegistry = null;
        var lootHandle = _lootRegistryHandle;
        _lootRegistryHandle = default;
        
        if (lootHandle.IsValid())
            Addressables.Release(lootHandle);
        
        _weaponRegistry = null;
        var weaponHandle = _weaponRegistryHandle;
        _weaponRegistryHandle = default;

        if (weaponHandle.IsValid())
            Addressables.Release(weaponHandle);
    }

    private static void CompleteShutdown()
    {
        UnloadRegistries();
        _loadState = GameContentLoadState.Stopped;
        _shutdownCompletionSource.TrySetResult();
    }

    public static UniTask ShutdownAsync()
    {
        _shutdownRequested = true;
        if (_loadState != GameContentLoadState.Loading)
            CompleteShutdown();

        return _shutdownCompletionSource.Task;
    }

    public static void RequestShutdown() => ShutdownAsync().Forget();
}
}
