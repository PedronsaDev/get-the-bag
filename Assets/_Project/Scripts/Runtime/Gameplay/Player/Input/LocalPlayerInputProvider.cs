using Project.Core.Input;
using Project.Core.Player;
using Project.Core.UI;
using PurrNet;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player.Input
{
public class LocalPlayerInputProvider : NetworkBehaviour, IPlayerInputProvider
{
    [Header("Settings")]
    [SerializeField] private float _lookSensitivity = 0.5f;
    [SerializeField] private bool _invertY;
    [SerializeField] private bool _enableCursorLock = true;
    
    public float SensitivityMultiplier { get; set; } = 1.0f;

    public float LookSensitivity
    {
        get => _lookSensitivity;
        set => _lookSensitivity = value;
    }

    public bool InvertY
    {
        get => _invertY;
        set => _invertY = value;
    }
    
    [Header("Input Action Asset")] 
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Action Map Configuration")] 
    [SerializeField] private string _playerActionMapName = "Player";
    [SerializeField] private string _uiActionMapName = "UI";

    private InputActionMap _playerMap;
    private InputActionMap _uiMap;
    
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _attackAction;
    private InputAction _aimAction;
    private InputAction _reloadAction;
    private InputAction _interactAction;
    private InputAction _crouchAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _grenadeAction;
    private InputAction _skillAction;
    private InputAction _meleeAction;
    private InputAction _pingAction;
    private InputAction _slot1Action;
    private InputAction _slot2Action;
    private InputAction _slot3Action;
    private InputAction _slot4Action;
    private InputAction _playerInventoryAction;
    private InputAction _dropBagAction;
    
    private InputAction _cancelAction;
    private InputAction _uiInventoryAction;
    private InputAction _dropItemAction;
    private InputAction _favoriteItemAction;
    private InputAction _trashItemAction;
    private InputAction _dropAllTrashAction;
    private PlayerIdentifier _identifier;
    
    protected override void OnSpawned()
    {
        _identifier = GetComponent<PlayerIdentifier>();
        this.enabled = isOwner;
        if (!isOwner)
            return;
        
        UIBlockerSystem.CursorLockPolicy = ApplyCursorLockState;

        if (_enableCursorLock && !UIBlockerSystem.IsCursorUnlocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        InitializeActions();
        _playerMap?.Enable();
        _uiMap?.Enable();

        UIBlockerSystem.OnBlockerStateChanged += HandleBlockerStateChanged;
    }

    protected override void OnDespawned()
    {
        if (!isOwner)
            return;
        
        UIBlockerSystem.OnBlockerStateChanged -= HandleBlockerStateChanged;
        if (UIBlockerSystem.CursorLockPolicy == ApplyCursorLockState)
        {
            UIBlockerSystem.CursorLockPolicy = null;
        }

        _playerMap?.Disable();
        _uiMap?.Disable();
    }

    private void InitializeActions()
    {
        if (!_inputActions)
        {
            Debug.LogWarning($"[{nameof(LocalPlayerInputProvider)}] No InputActionAsset assigned on {gameObject.name}.",
                this);
            return;
        }

        _playerMap = _inputActions.FindActionMap(_playerActionMapName);
        _uiMap = _inputActions.FindActionMap(_uiActionMapName);
        if (_playerMap == null)
        {
            Debug.LogError(
                $"[{nameof(LocalPlayerInputProvider)}] Action map '{_playerActionMapName}' not found in InputActionAsset!",
                this);
            return;
        }

        // Gameplay actions
        _moveAction = _playerMap.FindAction("Move");
        _lookAction = _playerMap.FindAction("Look");
        _attackAction = _playerMap.FindAction("Attack");
        _aimAction = _playerMap.FindAction("Aim");
        _reloadAction = _playerMap.FindAction("Reload");
        _interactAction = _playerMap.FindAction("Interact");
        _crouchAction = _playerMap.FindAction("Crouch");
        _jumpAction = _playerMap.FindAction("Jump");
        _sprintAction = _playerMap.FindAction("Sprint");
        _grenadeAction = _playerMap.FindAction("Grenade");
        _skillAction = _playerMap.FindAction("Skill");
        _meleeAction = _playerMap.FindAction("Melee");
        _pingAction = _playerMap.FindAction("Ping");
        _slot1Action = _playerMap.FindAction("WeaponSlot1");
        _slot2Action = _playerMap.FindAction("WeaponSlot2");
        _slot3Action = _playerMap.FindAction("WeaponSlot3");
        _slot4Action = _playerMap.FindAction("WeaponSlot4");
        _playerInventoryAction = _playerMap.FindAction("Inventory");
        _dropBagAction = _playerMap.FindAction("DropBag");

        // UI & Meta actions
        if (_uiMap != null)
        {
            _cancelAction = _uiMap.FindAction("Cancel");
            _uiInventoryAction = _uiMap.FindAction("Inventory");
            _dropItemAction = _uiMap.FindAction("DropItem");
            _favoriteItemAction = _uiMap.FindAction("FavoriteItem");
            _trashItemAction = _uiMap.FindAction("TrashItem");
            _dropAllTrashAction = _uiMap.FindAction("DropAllTrash");
        }
    }

    private void Update()
    {
        if (!isOwner) return;

        UpdateUIInputs();
    }

    private void UpdateUIInputs()
    {
        if (PedronsaDev.Console.Console.IsOpen || (_identifier && !_identifier.GameplayEnabled))
            return;
        
        bool cancelPressed = _cancelAction != null && _cancelAction.WasPressedThisFrame();
        
        bool invPressed = (_uiInventoryAction != null && _uiInventoryAction.WasPressedThisFrame())
                       || (_playerInventoryAction != null && _playerInventoryAction.WasPressedThisFrame());
        
        bool dropPressed = _dropItemAction != null && _dropItemAction.WasPressedThisFrame();
        bool favPressed = _favoriteItemAction != null && _favoriteItemAction.WasPressedThisFrame();
        bool trashPressed = _trashItemAction != null && _trashItemAction.WasPressedThisFrame();
        bool dropAllTrashPressed = _dropAllTrashAction != null && _dropAllTrashAction.WasPressedThisFrame();

        UIInput.CancelPressed = cancelPressed;
        UIInput.ToggleInventoryPressed = invPressed;
        UIInput.DropItemPressed = dropPressed;
        UIInput.FavoriteItemPressed = favPressed;
        UIInput.TrashItemPressed = trashPressed;
        UIInput.DropAllTrashPressed = dropAllTrashPressed;

        if (cancelPressed)
        {
            UIInput.TriggerCancel();
            UIBlockerSystem.TryCloseTopBlocker();
        }

        if (invPressed)
        {
            UIInput.TriggerToggleInventory();
        }

        if (dropPressed) UIInput.TriggerDropItem();
        if (favPressed) UIInput.TriggerFavoriteItem();
        if (trashPressed) UIInput.TriggerTrashItem();
        if (dropAllTrashPressed) UIInput.TriggerDropAllTrash();
    }

    public PlayerInputData GetCurrentInput()
    {
        if (PedronsaDev.Console.Console.IsOpen)
            return PlayerInputData.Empty;

        bool movementBlocked = UIBlockerSystem.IsMovementBlocked;
        bool lookBlocked = UIBlockerSystem.IsLookBlocked;
        bool actionsBlocked = UIBlockerSystem.IsActionsBlocked;

        Vector2 rawLook = lookBlocked ? Vector2.zero : (_lookAction?.ReadValue<Vector2>() ?? Vector2.zero);
        var scaledLookSensitivity = _lookSensitivity * 0.1f;
        Vector2 scaledLook = new Vector2(
            rawLook.x * scaledLookSensitivity * SensitivityMultiplier,
            rawLook.y * scaledLookSensitivity * (_invertY ? -1f : 1f) * SensitivityMultiplier
        );

        Vector2 moveVector = movementBlocked ? Vector2.zero : (_moveAction?.ReadValue<Vector2>() ?? Vector2.zero);

        bool invPressed = (_playerInventoryAction != null && _playerInventoryAction.WasPressedThisFrame())
                       || (_uiInventoryAction != null && _uiInventoryAction.WasPressedThisFrame());

        return new PlayerInputData
        {
            MoveVector = moveVector,
            LookVector = scaledLook,

            JumpPressed = !actionsBlocked && _jumpAction != null && _jumpAction.WasPressedThisFrame(),
            JumpHeld = !actionsBlocked && _jumpAction != null && _jumpAction.IsPressed(),
            SprintHeld = !movementBlocked && _sprintAction != null && _sprintAction.IsPressed(),
            CrouchHeld = !movementBlocked && _crouchAction != null && _crouchAction.IsPressed(),

            PrimaryFirePressed = !actionsBlocked && _attackAction != null && _attackAction.WasPressedThisFrame(),
            PrimaryFireHeld = !actionsBlocked && _attackAction != null && _attackAction.IsPressed(),
            AimPressed = !actionsBlocked && _aimAction != null && _aimAction.WasPressedThisFrame(),
            AimHeld = !actionsBlocked && _aimAction != null && _aimAction.IsPressed(),

            ReloadPressed = !actionsBlocked && _reloadAction != null && _reloadAction.WasPressedThisFrame(),
            GrenadePressed = !actionsBlocked && _grenadeAction != null && _grenadeAction.WasPressedThisFrame(),
            SkillPressed = !actionsBlocked && _skillAction != null && _skillAction.WasPressedThisFrame(),
            MeleePressed = !actionsBlocked && _meleeAction != null && _meleeAction.WasPressedThisFrame(),
            PingPressed = !actionsBlocked && _pingAction != null && _pingAction.WasPressedThisFrame(),
            
            InteractPressed = !actionsBlocked && _interactAction != null && _interactAction.WasPressedThisFrame(),
            InteractHeld = !actionsBlocked && _interactAction != null && _interactAction.IsPressed(),
            
            DropBagHeld = !actionsBlocked && _dropBagAction != null && _dropBagAction.IsPressed(),
            DropBagReleased = !actionsBlocked && _dropBagAction != null && _dropBagAction.WasReleasedThisFrame(),
            
            
            InventoryPressed = invPressed,
            WeaponSlotPressed = actionsBlocked ? 0 : GetWeaponSlotPressed(),
        };
    }

    private void ApplyCursorLockState()
    {
        if (_enableCursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleBlockerStateChanged(bool isBlocked)
    {
        
    }

    private int GetWeaponSlotPressed()
    {
        if (_slot1Action != null && _slot1Action.WasPressedThisFrame()) return 1;
        if (_slot2Action != null && _slot2Action.WasPressedThisFrame()) return 2;
        if (_slot3Action != null && _slot3Action.WasPressedThisFrame()) return 3;
        if (_slot4Action != null && _slot4Action.WasPressedThisFrame()) return 4;
        return 0;
    }
    
    public void RegisterUIBlocker(IInputBlocker blocker)
    {
        UIBlockerSystem.Register(blocker);
    }

    public void UnregisterUIBlocker(IInputBlocker blocker)
    {
        UIBlockerSystem.Unregister(blocker);
    }
}
}
