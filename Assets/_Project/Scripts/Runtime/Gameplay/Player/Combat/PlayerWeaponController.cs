using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Content;
using Project.Logging;
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

    [Header("Default Weapons (Optional)")] 
    [SerializeField] private WeaponDefinition _defaultMainWeapon;
    [SerializeField] private WeaponDefinition _defaultSecondaryWeapon;

    private RuntimeWeapon _mainWeapon;
    private RuntimeWeapon _secondaryWeapon;
    private SyncVar<WeaponEquipmentState> _confirmedState = new();
    private CancellationTokenSource _presentationLifetime;

    public WeaponEquipmentState ConfirmedState => _confirmedState.value;

    private void Awake()
    {
        if (!_inputProvider)
            _inputProvider = GetComponent<LocalPlayerInputProvider>();
        if (!_locomotionController)
            _locomotionController = GetComponentInChildren<LocomotionController>();
        if (!_presenter)
            _presenter = GetComponentInChildren<PlayerWeaponPresenter>(true);
        if (!_downedState) 
            _downedState = GetComponent<PlayerDownedState>();
    }

    protected override void OnSpawned()
    {
        _presentationLifetime = new CancellationTokenSource();
        if (isServer)
            InitializeLoadoutServer();

        if (_presenter)
            _presenter.ConfigureView(isOwner);

        _confirmedState.onChanged += HandleEquipmentChanged;
        HandleEquipmentChanged(_confirmedState.value);

    }

    protected override void OnDespawned()
    {
        _confirmedState.onChanged -= HandleEquipmentChanged;

        var lifetime = _presentationLifetime;
        _presentationLifetime = null;
        lifetime?.Cancel();
        lifetime?.Dispose();

        if (_presenter) 
            _presenter.ClearVisuals();
    }

    private void Update()
    {
        // TODO: Owner-only input after provider readiness; route slots/fire/reload/drop.
        // Respect UI/downed gates. Server separately advances authoritative reload timers.

        if (!isOwner)
            return;

        var slotPressed = _inputProvider.GetCurrentInput().WeaponSlotPressed;
        switch (slotPressed)
        {
            case 1:
                RequestEquip(WeaponSlot.Main);
                break;
            case 2:
                RequestEquip(WeaponSlot.Secondary);
                break;
        }
    }

    private void InitializeLoadoutServer()
    {
        if (!isServer)
            return;

        if (!GameContent.IsLoaded || !GameContent.WeaponRegistry)
        {
            AppLog.LogError(LogChannel.Combat, "Cannot initialize player loadout: GameContent or WeaponRegistry is not loaded.", this);
            return;
        }

        var mainWeapon = CreateDefaultWeapon(_defaultMainWeapon);
        var secondaryWeapon = CreateDefaultWeapon(_defaultSecondaryWeapon);

        WeaponSlot initialSlot;
        if (mainWeapon != null)
        {
            initialSlot = WeaponSlot.Main; 
        }
        else if (secondaryWeapon != null)
        {
            initialSlot = WeaponSlot.Secondary;
        }
        else
        {
            initialSlot = WeaponSlot.None;
            AppLog.LogWarning(LogChannel.Combat, "Player has no default weapons assigned.", this);
        }
        
        var state = new WeaponEquipmentState
        {
            Main = mainWeapon?.CaptureState() ?? default,
            Secondary = secondaryWeapon?.CaptureState() ?? default,
            ActiveSlot = initialSlot,
            Action = WeaponActionState.Idle,
            Revision = 1
        };
        
        _mainWeapon = mainWeapon;
        _secondaryWeapon = secondaryWeapon;
        _confirmedState.value = state;
    }

    private RuntimeWeapon CreateDefaultWeapon(WeaponDefinition definition)
    {
        if (!definition)
        {
            return null;
        }

        if (!GameContent.WeaponRegistry.TryGetById(definition.Id, out var weapon))
        {
            AppLog.LogError(LogChannel.Combat,
                $"Cannot create default weapon '{definition.Id}': not found in registry.", this);
            return null;
        }

        if (weapon.BaseMagazineSize <= 0)
        {
            AppLog.LogError(LogChannel.Combat, $"Weapon '{weapon.Id}' must have a positive magazine capacity", this);
            return null;
        }

        var runtimeWeapon = new RuntimeWeapon(weapon);
        return runtimeWeapon;
    }

    public void RequestEquip(WeaponSlot slot)
    {
        if (isOwner) 
            RpcRequestEquip(slot);
    }

    [ServerRpc(requireOwnership: true)]
    private void RpcRequestEquip(WeaponSlot slot)
    {
        if (_downedState.IsDowned || _downedState.IsDead)
            return;
        
        if (_confirmedState.value.ActiveSlot == slot)
            return;
        
        if (slot != WeaponSlot.Main && slot != WeaponSlot.Secondary)
            return;

        if (_confirmedState.value.Action != WeaponActionState.Idle)
            return;
        
        var weapon = slot == WeaponSlot.Main ? _mainWeapon : _secondaryWeapon;
        if (weapon == null)
            return;

        var nextState = _confirmedState.value;
        nextState.ActiveSlot = slot;
        nextState.Revision++;
        _confirmedState.value = nextState;
    }

    public void RequestFire(Vector3 aimDirection)
    {
        // TODO: Owner feedback and RPC. Add shot sequence if implementing prediction.
    }

    [ServerRpc(requireOwnership: true)]
    private void RpcRequestFire(Vector3 aimDirection)
    {
        // TODO: Validate ammo, cadence, action, alive state and plausible finite aim.
        // Resolve damage; publish ammo; notify observers of accepted shot.
        // Do not trust client-provided muzzle position or damage.
    }

    public void RequestReload()
    {
        // TODO: Owner only; request authoritative reload, optionally predict cosmetics.
    }

    [ServerRpc(requireOwnership: true)]
    private void RpcRequestReload()
    {
        // TODO: Validate/start server timer; transfer ammunition on completion.
        // Define interruption rules for switch/death/despawn/drop.
    }

    public void RequestDrop()
    {
        // TODO: Owner only; add ownership-checked RPC. Preserve ammo in world state.
        // Clear slot only after world spawn succeeds; rollback failures.
    }

    public bool TryPickupServer(WorldWeapon worldWeapon, WeaponSlot destination)
    {
        // TODO: Called by validated interaction RPC on THIS player's controller.
        // Check server, sender ownership, reach, availability and destination policy.
        // Claim/transfer atomically; rollback failure; despawn world object after success.
        return false;
    }

    private void HandleEquipmentChanged(WeaponEquipmentState state)
    {
        AppLog.Log(LogChannel.Combat, $"Player equipment changed: ActiveSlot={state.ActiveSlot}, Action={state.Action}, Revision={state.Revision}", this);

        if (_presenter && _presentationLifetime != null && isClient)
            _presenter.PresentAsync(state, _presentationLifetime.Token).Forget(exception =>
            {
                if (!(exception is OperationCanceledException))
                    AppLog.LogException(LogChannel.Combat, exception, this);
            });
    }
}
}
