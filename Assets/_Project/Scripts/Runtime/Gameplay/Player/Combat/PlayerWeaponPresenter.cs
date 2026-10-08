using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Combat.Weapons;
using Project.Content;
using Project.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Project.Player.Combat
{
public class PlayerWeaponPresenter : MonoBehaviour
{
    [Header("First Person References")]
    [SerializeField] private Transform _firstPersonSocket;
    [SerializeField] private GameObject _firstPersonArms;
    [SerializeField] private Animator _firstPersonAnimator;
    
    [Header("Third Person References")]
    [SerializeField] private List<SkinnedMeshRenderer> _thirdPersonMeshes = new();
    [SerializeField] private Transform _thirdPersonSocket;
    [SerializeField] private Animator _thirdPersonAnimator;

    private bool _isLocalOwner;
    private WeaponInstance _activeVisual;

    private bool _ownsPrefabHandle;
    private AsyncOperationHandle<GameObject> _activeWeaponHandle;
    
    private CancellationTokenSource _equipCancellation;
    private uint _requestVersion;
    private bool _isReloading;
    public event Action Fired;
    public event Action<bool> ReloadingChanged;

    public void ConfigureView(bool isLocalOwner)
    {
        _isLocalOwner = isLocalOwner;
        
        if (_firstPersonArms)
        {
            _firstPersonArms.SetActive(isLocalOwner);
        } 
        else
        {
            AppLog.LogWarning(LogChannel.Combat, "First person arms reference is missing. Assign a GameObject to the PlayerWeaponPresenter.", this);
        }
        
        var thirdPersonShadowMode = isLocalOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        
        foreach (var mesh in _thirdPersonMeshes)
        {
            if (!mesh)
            {
                AppLog.LogWarning(LogChannel.Combat, "One of the third person mesh references is missing. Ensure all SkinnedMeshRenderer references are assigned in the PlayerWeaponPresenter.", this);
                continue;
            }
                
            mesh.shadowCastingMode = thirdPersonShadowMode;
        }
    }

    public async UniTask PresentAsync(WeaponEquipmentState state, CancellationToken lifetimeToken)
    {
        lifetimeToken.ThrowIfCancellationRequested();
        if (!this)
            return;

        CancelPendingRequest();
        var version = _requestVersion;
        var weaponState = state.ActiveSlot switch
        {
            WeaponSlot.Main => state.Main,
            WeaponSlot.Secondary => state.Secondary,
            WeaponSlot.None => default,
            _ => throw new ArgumentOutOfRangeException(nameof(state), "Unknown weapon slot.")
        };
        var id = weaponState.WeaponId;

        if (string.IsNullOrWhiteSpace(id))
        {
            ReleaseActiveVisual();
            return;
        }

        if (!GameContent.IsLoaded || !GameContent.WeaponRegistry)
            throw new InvalidOperationException("Weapon content is not ready.");

        if (!GameContent.WeaponRegistry.TryGetById(id, out var definition))
            throw new KeyNotFoundException($"Weapon ID '{id}' not found in WeaponRegistry.");

        var socket = _isLocalOwner ? _firstPersonSocket : _thirdPersonSocket;
        if (!socket)
            throw new InvalidOperationException($"Weapon '{id}' has no configured presentation socket.");

        if (_activeVisual && _activeVisual.Definition == definition &&
            _activeVisual.transform.parent == socket && _ownsPrefabHandle && _activeWeaponHandle.IsValid())
            return;

        if (definition.PrefabReference == null || !definition.PrefabReference.RuntimeKeyIsValid())
            throw new InvalidOperationException($"Weapon '{id}' has no valid prefab reference.");

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeToken, this.GetCancellationTokenOnDestroy());
        var token = cancellation.Token;
        _equipCancellation = cancellation;
        AsyncOperationHandle<GameObject> handle = default;
        GameObject instance = null;

        try
        {
            token.ThrowIfCancellationRequested();
            handle = Addressables.LoadAssetAsync<GameObject>(definition.PrefabReference.RuntimeKey);
            var prefab = await handle.ToUniTask(cancellationToken: token, autoReleaseWhenCanceled: false);

            token.ThrowIfCancellationRequested();
            if (version != _requestVersion || !this || !socket || !GameContent.IsLoaded)
                throw new OperationCanceledException("Weapon presentation was superseded or stopped.", token);
            if (!prefab || !prefab.TryGetComponent<WeaponInstance>(out _))
                throw new InvalidOperationException($"Weapon '{id}' prefab requires WeaponInstance on its root.");

            instance = Instantiate(prefab, socket, false);
            var visual = instance.GetComponent<WeaponInstance>();
            visual.Initialize(definition);

            token.ThrowIfCancellationRequested();
            if (version != _requestVersion || !this || !socket)
                throw new OperationCanceledException("Weapon presentation was superseded.", token);

            ReleaseActiveVisual();
            _activeVisual = visual;
            _activeWeaponHandle = handle;
            _ownsPrefabHandle = true;
            visual.SetReloading(_isReloading);
            instance = null;
            handle = default;
        }
        finally
        {
            if (instance)
            {
                instance.SetActive(false);
                Destroy(instance);
            }
            if (handle.IsValid())
                Addressables.Release(handle);
            if (ReferenceEquals(_equipCancellation, cancellation))
                _equipCancellation = null;
            cancellation.Dispose();
        }
    }
    
    public void ClearVisuals()
    {
        SetReloading(false);
        CancelPendingRequest();
        ReleaseActiveVisual();
    }

    private void CancelPendingRequest()
    {
        _requestVersion++;
        var cancellation = _equipCancellation;
        _equipCancellation = null;
        cancellation?.Cancel();
    }

    private void ReleaseActiveVisual()
    {
        var visual = _activeVisual;
        var handle = _activeWeaponHandle;
        var ownsHandle = _ownsPrefabHandle;
        _activeVisual = null;
        _activeWeaponHandle = default;
        _ownsPrefabHandle = false;

        if (visual)
        {
            visual.gameObject.SetActive(false);
            Destroy(visual.gameObject);
        }
        if (ownsHandle && handle.IsValid())
            Addressables.Release(handle);
    }

    public void PlayFire()
    {
        if (_activeVisual) PlayFire(_activeVisual.Definition.Id);
    }

    public void PlayFire(string weaponId)
    {
        if (!_activeVisual || _activeVisual.Definition.Id != weaponId) return;
        _activeVisual.PlayFire();
        WeaponAnimation.PlayFire(_isLocalOwner ? _firstPersonAnimator : _thirdPersonAnimator);
        Fired?.Invoke();
    }

    public void PlayReload()
    {
        SetReloading(true);
    }

    public void SetReloading(bool reloading)
    {
        if (_activeVisual) _activeVisual.SetReloading(reloading);
        if (_isReloading == reloading) return;
        _isReloading = reloading;
        WeaponAnimation.SetReloading(_isLocalOwner ? _firstPersonAnimator : _thirdPersonAnimator, reloading);
        ReloadingChanged?.Invoke(reloading);
    }

    private void OnDestroy()
    {
        ClearVisuals();
    }
}
}
