using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Definitions.Weapons;
using Project.Combat.Weapons;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Project.Player.Combat
{
public sealed class WeaponVisualBinding : IDisposable
{
    private WeaponInstance _weaponInstance;
    private AsyncOperationHandle<GameObject> _prefabHandle;
    private bool _ownsPrefabHandle;
    private bool _loadStarted;
    private bool _disposed;

    public WeaponInstance Instance => _weaponInstance;
    public bool IsLoaded => !_disposed && _weaponInstance && _ownsPrefabHandle && _prefabHandle.IsValid();

    public async UniTask LoadAsync(AssetReferenceT<GameObject> prefabReference, WeaponDefinition definition,
        Transform socket, CancellationToken lifetimeToken, bool activateOnLoad = true)
    {
        ThrowIfDisposed();
        if (_loadStarted)
            throw new InvalidOperationException("A weapon visual binding can only load once.");
        lifetimeToken.ThrowIfCancellationRequested();
        if (!definition) throw new ArgumentNullException(nameof(definition));
        if (!socket) throw new ArgumentNullException(nameof(socket));
        if (prefabReference == null || !prefabReference.RuntimeKeyIsValid())
            throw new ArgumentException("Weapon visual requires a valid Addressable prefab reference.",
                nameof(prefabReference));

        _loadStarted = true;
        AsyncOperationHandle<GameObject> handle = default;
        GameObject instance = null;
        try
        {
            handle = Addressables.LoadAssetAsync<GameObject>(prefabReference.RuntimeKey);
            var prefab = await handle.ToUniTask(cancellationToken: lifetimeToken, autoReleaseWhenCanceled: false);

            lifetimeToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (!socket || !definition)
                throw new OperationCanceledException("Weapon visual's socket or definition was destroyed.", lifetimeToken);
            if (!prefab || !prefab.TryGetComponent<WeaponInstance>(out _))
                throw new InvalidOperationException($"Weapon '{definition.Id}' prefab requires WeaponInstance on its root.");

            instance = UnityEngine.Object.Instantiate(prefab, socket, false);
            if (!activateOnLoad) instance.SetActive(false);
            var weapon = instance.GetComponent<WeaponInstance>();
            weapon.Initialize(definition);

            lifetimeToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            _weaponInstance = weapon;
            _prefabHandle = handle;
            _ownsPrefabHandle = true;
            instance = null;
            handle = default;
        }
        finally
        {
            try
            {
                if (instance)
                {
                    instance.SetActive(false);
                    UnityEngine.Object.Destroy(instance);
                }
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var weapon = _weaponInstance;
        var handle = _prefabHandle;
        var ownsHandle = _ownsPrefabHandle;
        _weaponInstance = null;
        _prefabHandle = default;
        _ownsPrefabHandle = false;

        try
        {
            if (weapon)
            {
                weapon.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(weapon.gameObject);
            }
        }
        finally
        {
            if (ownsHandle && handle.IsValid()) Addressables.Release(handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WeaponVisualBinding));
    }
}
}
