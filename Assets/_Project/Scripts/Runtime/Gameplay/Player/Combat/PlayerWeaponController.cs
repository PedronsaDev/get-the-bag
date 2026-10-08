using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Content;
using Project.Logging;
using Project.Player.FPSCamera;
using Project.Player.Input;
using Project.Player.Locomotion;
using PurrNet;
using UnityEngine;

namespace Project.Player.Combat
{
public class PlayerWeaponController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerDownedState _downedState;
    [SerializeField] private LocalPlayerInputProvider _inputProvider;
    [SerializeField] private LocomotionController _locomotionController;
    [SerializeField] private PlayerWeaponPresenter _presenter;
    [SerializeField] private FPSCameraController _cameraController;
    [SerializeField] private Transform _aimOrigin;

    [Header("Default Weapons (Optional)")]
    [SerializeField] private WeaponDefinition _defaultMainWeapon;
    [SerializeField] private WeaponDefinition _defaultSecondaryWeapon;

    private RuntimeWeapon _mainWeapon;
    private RuntimeWeapon _secondaryWeapon;
    private readonly SyncVar<WeaponEquipmentState> _confirmedState = new();
    private readonly WeaponHitscan _hitscan = new();
    private CancellationTokenSource _presentationLifetime;
    private RuntimeWeapon _reloadWeapon;
    private double _reloadEndsAt;
    private Vector3 _serverAim;
    private double _lastTriggerUpdate;
    private bool _serverTriggerHeld;
    private bool _triggerBlockedUntilRelease;
    private bool _lastSentHeld;
    private uint _lastSentEquipRevision;
    private double _nextInputSend;
    private uint _shotSequence;
    private bool _running;
    private string _presentedId;
    private WeaponSlot _presentedSlot;
    private WeaponActionState _presentedAction;
    private uint _presentationVersion;

    private const double AIM_SEND_INTERVAL = 0.05;
    private const double TRIGGER_TIMEOUT = 0.5;

    public WeaponEquipmentState ConfirmedState => _confirmedState.value;
    public event Action<WeaponShot> OnShotAccepted;

    private void Awake()
    {
        if (!_inputProvider) _inputProvider = GetComponent<LocalPlayerInputProvider>();
        if (!_locomotionController) _locomotionController = GetComponentInChildren<LocomotionController>();
        if (!_presenter) _presenter = GetComponentInChildren<PlayerWeaponPresenter>(true);
        if (!_downedState) _downedState = GetComponent<PlayerDownedState>();
        if (!_cameraController) _cameraController = GetComponentInChildren<FPSCameraController>(true);
    }

    protected override void OnSpawned()
    {
        _running = true;
        _lastSentHeld = false;
        _lastSentEquipRevision = 0;
        _nextInputSend = 0;
        _presentedId = null;
        _presentedSlot = WeaponSlot.None;
        _presentedAction = WeaponActionState.Idle;
        _presentationLifetime = new CancellationTokenSource();
        if (isServer) InitializeLoadoutServer();
        if (_presenter) _presenter.ConfigureView(isOwner);
        _confirmedState.onChanged += HandleEquipmentChanged;
        HandleEquipmentChanged(_confirmedState.value);
    }

    protected override void OnDespawned()
    {
        _running = false;
        _confirmedState.onChanged -= HandleEquipmentChanged;
        StopFiring();
        _reloadWeapon = null;
        _mainWeapon = _secondaryWeapon = null;
        _presentationVersion++;
        var lifetime = _presentationLifetime;
        _presentationLifetime = null;
        lifetime?.Cancel();
        lifetime?.Dispose();
        if (_presenter) _presenter.ClearVisuals();
    }

    private void Update()
    {
        if (!_running) return;
        if (isServer) 
            TickServer(Time.timeAsDouble);
        
        if (!isOwner || !_inputProvider) return;

        var input = _inputProvider.GetCurrentInput();
        if (input.WeaponSlotPressed == 1) RequestEquip(WeaponSlot.Main);
        else if (input.WeaponSlotPressed == 2) RequestEquip(WeaponSlot.Secondary);
        if (input.ReloadPressed) RequestReload();

        var held = input.PrimaryFireHeld;
        var state = ConfirmedState;
        if (held != _lastSentHeld || input.PrimaryFirePressed ||
            state.EquipRevision != _lastSentEquipRevision || (held && Time.timeAsDouble >= _nextInputSend))
        {
            var direction = _cameraController ? _cameraController.CameraRotation * Vector3.forward : transform.forward;
            SetFireInput(held || input.PrimaryFirePressed, direction);
            if (input.PrimaryFirePressed && !held) SetFireInput(false, direction);
        }
    }

    private bool CanAct => !_downedState || (!_downedState.IsDowned && !_downedState.IsDead);
    private RuntimeWeapon ActiveWeapon => ConfirmedState.ActiveSlot switch
    {
        WeaponSlot.Main => _mainWeapon,
        WeaponSlot.Secondary => _secondaryWeapon,
        _ => null
    };

    private void InitializeLoadoutServer()
    {
        if (!GameContent.IsLoaded || !GameContent.WeaponRegistry)
        {
            AppLog.LogError(LogChannel.Combat, "Cannot initialize player loadout: weapon content is not ready.", this);
            return;
        }
        _mainWeapon = CreateDefaultWeapon(_defaultMainWeapon);
        _secondaryWeapon = CreateDefaultWeapon(_defaultSecondaryWeapon);
        _reloadWeapon = null;
        _serverTriggerHeld = false;
        _triggerBlockedUntilRelease = false;
        _shotSequence = 0;
        _confirmedState.value = new WeaponEquipmentState
        {
            Main = _mainWeapon?.CaptureState() ?? default,
            Secondary = _secondaryWeapon?.CaptureState() ?? default,
            ActiveSlot = _mainWeapon != null ? WeaponSlot.Main :
                _secondaryWeapon != null ? WeaponSlot.Secondary : WeaponSlot.None,
            Action = WeaponActionState.Idle,
            Revision = 1,
            EquipRevision = 1
        };
    }

    private RuntimeWeapon CreateDefaultWeapon(WeaponDefinition definition)
    {
        if (!definition) return null;
        if (!GameContent.WeaponRegistry.TryGetById(definition.Id, out var weapon))
        {
            AppLog.LogError(LogChannel.Combat, $"Weapon '{definition.Id}' is absent from the registry.", this);
            return null;
        }
        try { return new RuntimeWeapon(weapon); }
        catch (Exception exception)
        {
            AppLog.LogException(LogChannel.Combat, exception, this);
            return null;
        }
    }

    public void RequestEquip(WeaponSlot slot)
    {
        if (_running && isOwner) RpcRequestEquip(slot);
    }

    [ServerRpc(requireOwnership: true)]
    private void RpcRequestEquip(WeaponSlot slot)
    {
        if (!_running || !CanAct || slot == ConfirmedState.ActiveSlot ||
            (slot != WeaponSlot.Main && slot != WeaponSlot.Secondary)) return;
        var weapon = slot == WeaponSlot.Main ? _mainWeapon : _secondaryWeapon;
        if (weapon == null || ConfirmedState.Action == WeaponActionState.Equipping) return;
        StopFiring();
        _reloadWeapon = null;
        weapon.FireStrategy.Reset(_triggerBlockedUntilRelease);
        var state = ConfirmedState;
        state.ActiveSlot = slot;
        state.Action = WeaponActionState.Idle;
        state.ReloadDuration = 0;
        state.EquipRevision++;
        Publish(state);
    }

    public void SetFireInput(bool held, Vector3 aimDirection)
    {
        if (!_running || !isOwner) return;
        var state = ConfirmedState;
        RpcSetFireInput(held, aimDirection, state.ActiveSlot, state.EquipRevision);
        _lastSentHeld = held;
        _lastSentEquipRevision = state.EquipRevision;
        _nextInputSend = Time.timeAsDouble + AIM_SEND_INTERVAL;
    }

    public void RequestFire(Vector3 aimDirection)
    {
        SetFireInput(true, aimDirection);
        SetFireInput(false, aimDirection);
    }

    [ServerRpc(requireOwnership: true)]
    private void RpcSetFireInput(bool held, Vector3 direction, WeaponSlot slot, uint equipRevision)
    {
        if (!_running || slot != ConfirmedState.ActiveSlot || equipRevision != ConfirmedState.EquipRevision) return;
        var weapon = ActiveWeapon;
        if (weapon == null) return;
        if (!held)
        {
            _serverTriggerHeld = false;
            _triggerBlockedUntilRelease = false;
            weapon.FireStrategy.SetTrigger(false);
            return;
        }
        if (!ValidAim(direction)) return;
        if (!CanAct || ConfirmedState.Action != WeaponActionState.Idle)
        {
            _triggerBlockedUntilRelease = true;
            weapon.FireStrategy.Reset(requireRelease: true);
            return;
        }
        if (_triggerBlockedUntilRelease) return;
        _serverAim = direction.normalized;
        _lastTriggerUpdate = Time.timeAsDouble;
        _serverTriggerHeld = true;
        weapon.FireStrategy.SetTrigger(true);
        TryFireServer(Time.timeAsDouble);
    }

    private static bool ValidAim(Vector3 direction) => float.IsFinite(direction.x) &&
        float.IsFinite(direction.y) && float.IsFinite(direction.z) &&
        direction.sqrMagnitude >= 0.01f && direction.sqrMagnitude <= 4f;

    private void TickServer(double now)
    {
        if (!CanAct)
        {
            StopFiring();
            if (_reloadWeapon != null)
            {
                _reloadWeapon = null;
                var canceled = ConfirmedState;
                canceled.Action = WeaponActionState.Idle;
                canceled.ReloadDuration = 0;
                Publish(canceled);
            }
            return;
        }
        if (_reloadWeapon != null)
        {
            if (now < _reloadEndsAt) return;
            _reloadWeapon.CompleteReloadFromReserve();
            _reloadWeapon = null;
            var completed = ConfirmedState;
            completed.Action = WeaponActionState.Idle;
            completed.ReloadDuration = 0;
            Publish(completed);
        }
        if (_serverTriggerHeld && now - _lastTriggerUpdate > TRIGGER_TIMEOUT) StopFiring();
        TryFireServer(now);
    }

    private void StopFiring()
    {
        _triggerBlockedUntilRelease |= _serverTriggerHeld;
        _serverTriggerHeld = false;
        ActiveWeapon?.FireStrategy.Reset(_triggerBlockedUntilRelease);
    }

    private Vector3 GetServerAimOrigin()
    {
        if (_aimOrigin) return _aimOrigin.position;
        if (_cameraController && _cameraController.PitchPivot) return _cameraController.PitchPivot.position;
        return (_locomotionController ? _locomotionController.transform.position : transform.position) + Vector3.up * 1.5f;
    }

    private void TryFireServer(double now)
    {
        var weapon = ActiveWeapon;
        if (weapon == null || !CanAct || ConfirmedState.Action != WeaponActionState.Idle ||
            !ValidAim(_serverAim) || !weapon.TryFire(now)) return;
        var shot = _hitscan.Execute(weapon.Definition, transform, owner, GetServerAimOrigin(), _serverAim);
        shot.Slot = ConfirmedState.ActiveSlot;
        shot.Sequence = ++_shotSequence;
        Publish(ConfirmedState);
        RpcShotAccepted(shot);
    }

    [ObserversRpc(runLocally: true)]
    private void RpcShotAccepted(WeaponShot shot)
    {
        if (!isClient) return;
        if (_presenter) _presenter.PlayFire(shot.WeaponId);
        OnShotAccepted?.Invoke(shot);
    }

    public void RequestReload()
    {
        if (_running && isOwner) RpcRequestReload(ConfirmedState.ActiveSlot, ConfirmedState.EquipRevision);
    }

    [ServerRpc(requireOwnership: true)]
    private void RpcRequestReload(WeaponSlot slot, uint equipRevision)
    {
        if (!_running || !CanAct || slot != ConfirmedState.ActiveSlot ||
            equipRevision != ConfirmedState.EquipRevision || ConfirmedState.Action != WeaponActionState.Idle) return;
        var weapon = ActiveWeapon;
        if (weapon == null || !weapon.CanReload) return;
        StopFiring();
        _reloadWeapon = weapon;
        _reloadEndsAt = Time.timeAsDouble + weapon.Definition.ReloadDuration;
        var state = ConfirmedState;
        state.Action = WeaponActionState.Reloading;
        state.ReloadDuration = weapon.Definition.ReloadDuration;
        Publish(state);
    }

    private void Publish(WeaponEquipmentState state)
    {
        state.Main = _mainWeapon?.CaptureState() ?? default;
        state.Secondary = _secondaryWeapon?.CaptureState() ?? default;
        state.Revision++;
        _confirmedState.value = state;
    }

    public void RequestDrop()
    {
        // TODO: Validated server world spawn; preserve ammo and roll back on failure.
    }

    public bool TryPickupServer(WorldWeapon worldWeapon, WeaponSlot destination)
    {
        // TODO: Validate reach, claim atomically, transfer ammo, then despawn world object.
        return false;
    }

    private void HandleEquipmentChanged(WeaponEquipmentState state)
    {
        if (!_presenter || _presentationLifetime == null || !isClient) return;
        var id = state.ActiveSlot == WeaponSlot.Main ? state.Main.WeaponId :
            state.ActiveSlot == WeaponSlot.Secondary ? state.Secondary.WeaponId : null;
        if (_presentedId != id || _presentedSlot != state.ActiveSlot)
        {
            _presentedId = id;
            _presentedSlot = state.ActiveSlot;
            var version = ++_presentationVersion;
            PresentEquipmentAsync(state, version, _presentationLifetime.Token).Forget(exception =>
            {
                if (!(exception is OperationCanceledException))
                    AppLog.LogException(LogChannel.Combat, exception, this);
            });
        }
        if (_presentedAction != state.Action)
        {
            _presentedAction = state.Action;
            _presenter.SetReloading(state.Action == WeaponActionState.Reloading);
        }
    }

    private async UniTask PresentEquipmentAsync(WeaponEquipmentState state, uint version, CancellationToken token)
    {
        try
        {
            await _presenter.PresentAsync(state, token);
            if (version == _presentationVersion && _presenter)
                _presenter.SetReloading(ConfirmedState.Action == WeaponActionState.Reloading);
        }
        catch
        {
            if (version == _presentationVersion) _presentedId = null;
            throw;
        }
    }
}
}
