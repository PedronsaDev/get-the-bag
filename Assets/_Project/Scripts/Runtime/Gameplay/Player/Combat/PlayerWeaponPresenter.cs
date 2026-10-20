using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Combat.Weapons;
using Project.Combat.Data;
using Project.Player.Locomotion;
using Project.Player.FPSCamera;
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
    
    [Header("Third Person References")]
    [SerializeField] private List<SkinnedMeshRenderer> _thirdPersonMeshes = new();
    [SerializeField] private Transform _thirdPersonSocket;

    private bool _isLocalOwner;
    private WeaponInstance _activeVisual;

    private bool _ownsPrefabHandle;
    private AsyncOperationHandle<GameObject> _activeWeaponHandle;
    
    private CancellationTokenSource _equipCancellation;
    private uint _requestVersion;
    private bool _isReloading;
    private double _reloadStartedAt;
    private WeaponEquipmentState _latestState;
    private LocomotionController _locomotion;
    private FPSCameraController _cameraController;
    private WeaponAimPose _aimPose;
    private WeaponLookSway _lookSway;
    private Vector2 _lookDelta;
    private bool _aimHeld;
    private string _requestedId;
    private WeaponSlot _requestedSlot;
    public event Action Fired;
    public event Action<bool> ReloadingChanged;

    public void ConfigureView(bool isLocalOwner)
    {
        if (_isLocalOwner != isLocalOwner) ClearVisuals();
        _isLocalOwner = isLocalOwner;
        _cameraController = GetComponentInParent<PlayerWeaponController>()?.GetComponentInChildren<FPSCameraController>(true);
        _locomotion = GetComponentInParent<LocomotionController>();
        if (!_locomotion) _locomotion = transform.root.GetComponentInChildren<LocomotionController>();
        
        if (_firstPersonArms)
        {
            _firstPersonArms.SetActive(isLocalOwner);
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

    public void ApplyState(WeaponEquipmentState state)
    {
        if (_requestedId != state.ActiveWeapon.WeaponId || _requestedSlot != state.ActiveSlot)
            SetReloading(false);
        _latestState = state;
        SetReloading(state.Action == WeaponActionState.Reloading);
        var id = state.ActiveWeapon.WeaponId;
        if (_requestedId == id && _requestedSlot == state.ActiveSlot) return;
        _requestedId = id;
        _requestedSlot = state.ActiveSlot;
        PresentAsync(state, CancellationToken.None).Forget(exception =>
        {
            if (exception is not OperationCanceledException)
                AppLog.LogException(LogChannel.Combat, exception, this);
        });
    }

    public async UniTask PresentAsync(WeaponEquipmentState state, CancellationToken lifetimeToken)
    {
        lifetimeToken.ThrowIfCancellationRequested();
        if (!this)
            return;

        CancelPendingRequest();
        var version = _requestVersion;
        try
        {
            await LoadVisualAsync(state.ActiveWeapon.WeaponId, version, lifetimeToken);
        }
        catch
        {
            if (version == _requestVersion) _requestedId = null;
            throw;
        }
    }

    private async UniTask LoadVisualAsync(string id, uint version, CancellationToken lifetimeToken)
    {

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

        var reference = _isLocalOwner && definition.FirstPersonPrefabReference != null &&
                        definition.FirstPersonPrefabReference.RuntimeKeyIsValid()
            ? definition.FirstPersonPrefabReference : definition.PrefabReference;
        if (reference == null || !reference.RuntimeKeyIsValid())
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
            handle = Addressables.LoadAssetAsync<GameObject>(reference.RuntimeKey);
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
            _aimPose = visual.GetComponent<WeaponAimPose>();
            _lookSway = visual.GetComponent<WeaponLookSway>();
            _activeWeaponHandle = handle;
            _ownsPrefabHandle = true;
            SynchronizeReload();
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
        _aimHeld = false;
        _requestedId = null;
        _requestedSlot = WeaponSlot.None;
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
        if (_lookSway) _lookSway.ResetPose();
        _lookSway = null;
        _lookDelta = Vector2.zero;
        if (_aimPose) _aimPose.ResetPose();
        _aimPose = null;
        if (_isLocalOwner && _cameraController) _cameraController.SetAimPresentation(0f, 75f);
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

    public void PlayFire(string weaponId)
    {
        if (!_activeVisual || _activeVisual.Definition.Id != weaponId) return;
        _activeVisual.PlayFire();
        Fired?.Invoke();
    }

    public void SetReloading(bool reloading)
    {
        if (_isReloading == reloading) return;
        _isReloading = reloading;
        if (reloading) _reloadStartedAt = Time.timeAsDouble;
        SynchronizeReload();
        ReloadingChanged?.Invoke(reloading);
    }

    private void SynchronizeReload()
    {
        if (!_activeVisual) return;
        if (!_isReloading)
        {
            _activeVisual.SetReloading(false);
            return;
        }
        if (_activeVisual.Definition.Id != _latestState.ActiveWeapon.WeaponId)
            return;
        
        var duration = WeaponStatResolver.Resolve(_activeVisual.Definition,
            _latestState.ActiveWeapon.AttachmentIds).ReloadDuration;
        _activeVisual.SetReloading(true, duration, _reloadStartedAt);
    }

    private void Update()
    {
        if (!_isLocalOwner) return;
        if (!_aimPose || !_aimPose.isActiveAndEnabled)
        {
            if (_cameraController) _cameraController.SetAimPresentation(0f, 75f);
            return;
        }
        var currentWeapon = _activeVisual && _activeVisual.Definition &&
                            _activeVisual.Definition.Id == _latestState.ActiveWeapon.WeaponId;
        _aimPose.Tick(_aimHeld && !_isReloading && currentWeapon, Time.deltaTime);
        if (_cameraController) _cameraController.SetAimPresentation(_aimPose.Progress, _aimPose.AimFov);
    }

    public void SetAimHeld(bool held) => _aimHeld = held;
    public void SetLookDelta(Vector2 delta) => _lookDelta = delta;

    private void LateUpdate()
    {
        if (_isLocalOwner && _lookSway && _lookSway.isActiveAndEnabled)
            _lookSway.Tick(_lookDelta, _aimPose ? _aimPose.EvaluatedWeight : 0f, _isReloading, Time.deltaTime);
        _lookDelta = Vector2.zero;
    }

    private void OnDisable()
    {
        _aimHeld = false;
        _lookDelta = Vector2.zero;
        if (_lookSway) _lookSway.ResetPose();
        if (_aimPose) _aimPose.ResetPose();
        if (_isLocalOwner && _cameraController) _cameraController.SetAimPresentation(0f, 75f);
    }

    public void PlayInspect()
    {
    }

    private void OnDestroy()
    {
        ClearVisuals();
    }
}
}
