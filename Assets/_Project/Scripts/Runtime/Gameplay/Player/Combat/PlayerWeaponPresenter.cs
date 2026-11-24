using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Combat.Presentation;
using Project.Combat.Weapons;
using Project.Content;
using Project.Core;
using Project.Logging;
using Project.Player.Animation;
using Project.Player.FPSCamera;
using Project.Player.Locomotion;
using UnityEngine;

namespace Project.Player.Combat
{
public class PlayerWeaponPresenter : MonoBehaviour
{
    [Header("Presentation References")]
    [SerializeField] private CharacterRigBinding _characterRigBinding;
    [SerializeField] private CharacterVisualVisibility _visibility;
    
    [Header("Events")]
    [SerializeField] private PlayerWeaponEventChannel _weaponEvents;

    private bool _isLocalOwner;
    private bool _viewConfigured;
    private WeaponVisualBinding _firstPersonBinding;
    private WeaponVisualBinding _worldBinding;
    public WeaponInstance FirstPersonVisual => _firstPersonBinding?.Instance;
    public WeaponInstance WorldVisual => _worldBinding?.Instance;

    private CancellationTokenSource _equipCancellation;
    private uint _requestVersion;
    private bool _isReloading;
    private WeaponEquipmentState _latestState;
    private PlayerWeaponController _weaponController;
    private LocomotionController _locomotion;
    private FPSCameraController _cameraController;
    private FirstPersonWeaponMotion _firstPersonMotion;
    private Vector2 _lookDelta;
    private Vector2 _moveInput;
    private bool _aimHeld;
    private string _requestedId;
    private WeaponSlot _requestedSlot;
    private uint _requestedRevision;
    private string _presentedId;
    private WeaponSlot _presentedSlot;
    private uint _presentedRevision;
    private bool _isAiming;

    private void Awake()
    {
        if (!_characterRigBinding) _characterRigBinding = GetComponent<CharacterRigBinding>();
        if (!_visibility) _visibility = GetComponent<CharacterVisualVisibility>();
        _weaponController = GetComponentInParent<PlayerWeaponController>();
    }

    public void ConfigureView(bool isLocalOwner)
    {
        if (_viewConfigured && _isLocalOwner != isLocalOwner) ClearVisuals();
        _isLocalOwner = isLocalOwner;
        if (!_characterRigBinding) _characterRigBinding = GetComponent<CharacterRigBinding>();
        if (!_visibility) _visibility = GetComponent<CharacterVisualVisibility>();
        if (!_visibility) throw new InvalidOperationException("Player presentation requires CharacterVisualVisibility.");
        _visibility.Configure(isLocalOwner, _characterRigBinding);
        if (!_weaponController) _weaponController = GetComponentInParent<PlayerWeaponController>();
        _cameraController = _weaponController ? _weaponController.GetComponentInChildren<FPSCameraController>(true) : null;
        _locomotion = GetComponentInChildren<LocomotionController>(true);
        if (!_locomotion) _locomotion = GetComponentInParent<LocomotionController>();
        _viewConfigured = true;
    }

    private bool RequestedMatches(WeaponEquipmentState state) =>
        _requestedId == state.ActiveWeapon.WeaponId && _requestedSlot == state.ActiveSlot &&
        _requestedRevision == state.EquipRevision;

    private void UpdatePresentationState(WeaponEquipmentState state)
    {
        if (_latestState.ActiveWeapon.WeaponId != state.ActiveWeapon.WeaponId ||
            _latestState.ActiveSlot != state.ActiveSlot || _latestState.EquipRevision != state.EquipRevision)
            SetReloading(false);

        _latestState = state;

        SynchronizeAmmo(state.ActiveWeapon.WeaponId, state.ActiveSlot, state.EquipRevision);
        SetReloading(state.Action == WeaponActionState.Reloading);
    }

    public void ApplyState(WeaponEquipmentState state)
    {
        if (RequestedMatches(state))
        {
            UpdatePresentationState(state);
            return;
        }
        PresentAsync(state, CancellationToken.None).Forget(exception =>
        {
            if (exception is not OperationCanceledException)
                AppLog.LogException(LogChannel.Combat, exception, this);
        });
    }

    public async UniTask PresentAsync(WeaponEquipmentState state, CancellationToken lifetimeToken)
    {
        lifetimeToken.ThrowIfCancellationRequested();
        if (!this) return;
        CancelPendingRequest();
        var version = _requestVersion;
        UpdatePresentationState(state);
        _requestedId = state.ActiveWeapon.WeaponId;
        _requestedSlot = state.ActiveSlot;
        _requestedRevision = state.EquipRevision;
        try
        {
            await LoadVisualsAsync(state, version, lifetimeToken);
        }
        catch
        {
            if (version == _requestVersion)
            {
                _requestedId = null;
                ReleaseActiveVisuals();
            }
            throw;
        }
    }

    private async UniTask LoadVisualsAsync(WeaponEquipmentState state, uint version, CancellationToken lifetimeToken)
    {
        var id = state.ActiveWeapon.WeaponId;
        if (string.IsNullOrWhiteSpace(id))
        {
            ReleaseActiveVisuals();
            return;
        }
        if (!_viewConfigured || !_characterRigBinding || !_characterRigBinding.IsBindingUsable)
            throw new InvalidOperationException("Configure a usable character rig before presenting weapons.");
        if (!GameContent.IsLoaded || !GameContent.WeaponRegistry)
            throw new InvalidOperationException("Weapon content is not ready.");
        if (!GameContent.WeaponRegistry.TryGetById(id, out var definition))
            throw new KeyNotFoundException($"Weapon ID '{id}' not found in WeaponRegistry.");

        var worldSocket = _characterRigBinding.WorldWeaponSocket;
        var firstPersonSocket = _characterRigBinding.FirstPersonSocket;
        var sameEquipment = _presentedId == id && _presentedSlot == state.ActiveSlot &&
                            _presentedRevision == state.EquipRevision;
        var worldReady = _worldBinding != null && _worldBinding.IsLoaded && WorldVisual.transform.parent == worldSocket;
        var firstPersonReady = !_isLocalOwner || (_firstPersonBinding != null && _firstPersonBinding.IsLoaded &&
                                                 FirstPersonVisual.transform.parent == firstPersonSocket);
        if (sameEquipment && worldReady && firstPersonReady) return;

        ReleaseActiveVisuals();
        if (!worldSocket || (_isLocalOwner && !firstPersonSocket))
            throw new InvalidOperationException($"Weapon '{id}' has no configured presentation socket.");
        var worldReference = definition.WorldPrefabReference;
        if (worldReference == null || !worldReference.RuntimeKeyIsValid())
            throw new InvalidOperationException($"Weapon '{id}' has no valid world prefab reference.");
        var firstPersonReference = definition.FirstPersonPrefabReference != null &&
                                   definition.FirstPersonPrefabReference.RuntimeKeyIsValid()
            ? definition.FirstPersonPrefabReference : worldReference;

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken, this.GetCancellationTokenOnDestroy());
        var token = cancellation.Token;
        _equipCancellation = cancellation;
        var pendingWorld = new WeaponVisualBinding();
        var pendingFirstPerson = _isLocalOwner ? new WeaponVisualBinding() : null;
        try
        {
            await pendingWorld.LoadAsync(worldReference, definition, worldSocket, token, activateOnLoad: false);
            EnsureRequestCurrent(version, token);
            if (pendingFirstPerson != null)
                await pendingFirstPerson.LoadAsync(firstPersonReference, definition, firstPersonSocket, token, activateOnLoad: false);
            EnsureRequestCurrent(version, token);

            AdoptVisuals(state, pendingWorld, pendingFirstPerson);
            pendingWorld = null;
            pendingFirstPerson = null;
        }
        finally
        {
            try { pendingFirstPerson?.Dispose(); }
            finally
            {
                pendingWorld?.Dispose();
                if (ReferenceEquals(_equipCancellation, cancellation)) _equipCancellation = null;
                cancellation.Dispose();
            }
        }
    }

    private void AdoptVisuals(WeaponEquipmentState state, WeaponVisualBinding world, WeaponVisualBinding firstPerson)
    {
        _visibility.ApplyWeaponViews(world.Instance, firstPerson?.Instance);
        var motion = firstPerson == null ? null : new FirstPersonWeaponMotion(firstPerson.Instance);

        _worldBinding = world;
        _firstPersonBinding = firstPerson;
        _firstPersonMotion = motion;
        _presentedId = state.ActiveWeapon.WeaponId;
        _presentedSlot = state.ActiveSlot;
        _presentedRevision = state.EquipRevision;

        _characterRigBinding.SetSupportHandGrip(world.Instance.LeftHandGrip);

        SynchronizeAmmo(state.ActiveWeapon.WeaponId, state.ActiveSlot, state.EquipRevision);
        SynchronizeReload();
        WorldVisual.gameObject.SetActive(true);
        if (FirstPersonVisual) FirstPersonVisual.gameObject.SetActive(true);
    }

    private void EnsureRequestCurrent(uint version, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!this || !isActiveAndEnabled || version != _requestVersion || !GameContent.IsLoaded)
            throw new OperationCanceledException("Weapon presentation was superseded or stopped.", token);
    }

    public void ClearVisuals()
    {
        _aimHeld = false;
        SetAiming(false);
        _requestedId = null;
        _requestedSlot = WeaponSlot.None;
        _requestedRevision = 0;
        SetReloading(false);
        _latestState = default;
        CancelPendingRequest();
        ReleaseActiveVisuals();
    }

    private void CancelPendingRequest()
    {
        _requestVersion++;
        var cancellation = _equipCancellation;
        _equipCancellation = null;
        cancellation?.Cancel();
    }

    private void ReleaseActiveVisuals()
    {
        _firstPersonMotion?.ResetPose();
        _firstPersonMotion = null;
        _lookDelta = _moveInput = Vector2.zero;
        if (_isLocalOwner && _cameraController)
            _cameraController.SetAimPresentation(0f, 75f);

        var firstPerson = _firstPersonBinding;
        var world = _worldBinding;

        _firstPersonBinding = null;
        _worldBinding = null;
        _presentedId = null;
        _presentedSlot = WeaponSlot.None;
        _presentedRevision = 0;

        if (_characterRigBinding)
            _characterRigBinding.SetSupportHandGrip(null);

        try { firstPerson?.Dispose(); }
        finally { world?.Dispose(); }
    }

    public void PlayFire(string weaponId)
    {
        if (_presentedId != weaponId || _presentedId != _latestState.ActiveWeapon.WeaponId) return;
        if (WorldVisual) WorldVisual.PlayFire();
        if (FirstPersonVisual) FirstPersonVisual.PlayFire();
        if (_isLocalOwner && _cameraController) _cameraController.OnWeaponFired();
    }

    // The streak starts at the visible muzzle (first person view for the owner, world model for everyone else)
    // and ends where the server says the shot ended.
    public void PlayTracer(WeaponShot shot)
    {
        if (_presentedId != shot.WeaponId || _presentedId != _latestState.ActiveWeapon.WeaponId) return;

        var visual = _isLocalOwner && FirstPersonVisual ? FirstPersonVisual : WorldVisual;
        var muzzle = visual && visual.FirePoint ? visual.FirePoint.position : shot.Origin;
        BulletTracers.Spawn(muzzle, shot.EndPoint);
    }

    public void SetReloading(bool reloading)
    {
        _isReloading = reloading;
        SynchronizeReload();
    }

    private void SynchronizeAmmo(string weaponId, WeaponSlot slot, uint equipRevision)
    {
        if (_presentedId != _latestState.ActiveWeapon.WeaponId ||
            _presentedSlot != _latestState.ActiveSlot ||
            _presentedRevision != _latestState.EquipRevision)
            return;

        var mag = _latestState.ActiveWeapon.MagazineAmmo;

        if (WorldVisual)
            WorldVisual.SetLoadedAmmo(mag);

        if (FirstPersonVisual)
            FirstPersonVisual.SetLoadedAmmo(mag);
    }

    private void SynchronizeReload()
    {
        var reloading = _isReloading;
        var duration = _latestState.ActionDuration;
        var localStartedAt = 0d;
        if (reloading)
        {
            if (!_weaponController || _presentedId != _latestState.ActiveWeapon.WeaponId ||
                _presentedSlot != _latestState.ActiveSlot || _presentedRevision != _latestState.EquipRevision)
                return;
            if (!float.IsFinite(duration) || duration <= 0f || !double.IsFinite(_latestState.ActionStartedAt))
            {
                AppLog.LogWarning(LogChannel.Combat, "Reload presentation requires valid confirmed action timing.", this);
                return;
            }

            var elapsed = Math.Max(0d, _weaponController.NetworkTime - _latestState.ActionStartedAt);
            reloading = elapsed < duration;
            localStartedAt = Time.unscaledTimeAsDouble - elapsed;
        }

        if (WorldVisual)
            WorldVisual.SetReloading(reloading, duration, localStartedAt, _latestState.ActionSequence, _latestState.ReloadType);
        if (FirstPersonVisual)
            FirstPersonVisual.SetReloading(reloading, duration, localStartedAt, _latestState.ActionSequence, _latestState.ReloadType);
    }

    private void Update()
    {
        if (!_isLocalOwner) return;
        if (_firstPersonMotion == null || !_firstPersonMotion.CanAim)
        {
            SetAiming(false);
            if (_cameraController) _cameraController.SetAimPresentation(0f, 75f);
            return;
        }
        var currentWeapon = FirstPersonVisual && _presentedId == _latestState.ActiveWeapon.WeaponId;
        var aiming = _aimHeld && !_isReloading && currentWeapon;
        SetAiming(aiming);
        _firstPersonMotion.TickAim(aiming, Time.deltaTime);
        if (_cameraController) _cameraController.SetAimPresentation(_firstPersonMotion.AimProgress, _firstPersonMotion.AimFov);
    }

    // Fires only when the aim state flips, so listeners get one call per transition.
    private void SetAiming(bool aiming)
    {
        if (aiming == _isAiming) return;
        _isAiming = aiming;
        if (!_weaponEvents) return;

        if (aiming) _weaponEvents.RaiseADSStarted();
        else _weaponEvents.RaiseADSEnded();
    }

    public void SetAimHeld(bool held) => _aimHeld = held;
    public void SetLookDelta(Vector2 delta) => _lookDelta = delta;
    public void SetMoveInput(Vector2 input) => _moveInput = input;

    private void LateUpdate()
    {
        if (_isLocalOwner && _firstPersonMotion != null)
        {
            var grounded = _locomotion && _locomotion.State.Phase == LocomotionPhase.Grounded &&
                           _locomotion.State is { IsSliding: false, IsDowned: false };
            _firstPersonMotion.TickMotion(_lookDelta, grounded ? _moveInput : Vector2.zero, _isReloading, Time.deltaTime);
        }
        _lookDelta = _moveInput = Vector2.zero;
    }

    private void OnEnable()
    {
        if (_viewConfigured) ApplyState(_latestState);
    }

    private void OnDisable()
    {
        CancelPendingRequest();
        _requestedId = null;
        _aimHeld = false;
        SetAiming(false);
        _lookDelta = _moveInput = Vector2.zero;
        _firstPersonMotion?.ResetPose();
        if (_isLocalOwner && _cameraController) _cameraController.SetAimPresentation(0f, 75f);
    }

    private void OnDestroy() => ClearVisuals();
}
}
