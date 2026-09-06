using System.Collections.Generic;
using Project.Core.Input;
using Project.Core.UI;
using PurrNet;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player.Input
{
/// <summary>
/// Polls Unity's Input System for the local player instance.
/// Converts hardware inputs into a pure PlayerInputData struct every frame/tick.
/// Remote players should instead use RemotePlayerInputProvider.
/// </summary>
public class LocalPlayerInputProvider : NetworkBehaviour, IPlayerInputProvider
{
    [Header("Settings")]
    [SerializeField] private float _lookSensitivity = 0.5f;
    [SerializeField] private bool _invertY;
    [SerializeField] private bool _enableCursorLock = true;

    /// <summary>
    /// Dynamic multiplier for sensitivity (e.g. 0.5f during ADS / sniper zoom).
    /// </summary>
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
    
    private InputAction _cancelAction;
    
    private List<InputBlockerUI> _activeUIBlockers = new List<InputBlockerUI>();
    
    
    protected override void OnSpawned()
    {
        this.enabled = isOwner;
        if (!isOwner)
            return;
        
        if (_enableCursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        InitializeActions();
        _playerMap?.Enable();
        _uiMap?.Enable();
    }

    protected override void OnDespawned()
    {
        if (!isOwner)
            return;
        
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
        
        _cancelAction = _uiMap?.FindAction("Cancel");
    }

    private void Update()
    {
        if (!isOwner) return;

        bool cancelPressed = _cancelAction != null && _cancelAction.WasPressedThisFrame();

        if (cancelPressed)
        {
            CloseLastActiveUIBlocker();
        }
    }

    public PlayerInputData GetCurrentInput()
    {
        if (_playerMap is not { enabled: true })
            return PlayerInputData.Empty;

        if (PedronsaDev.Console.Console.IsOpen)
            return PlayerInputData.Empty;

        Vector2 rawLook = _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        var scaledLookSensitivity = _lookSensitivity * 0.1f;
        Vector2 scaledLook = new Vector2(
            rawLook.x * scaledLookSensitivity * SensitivityMultiplier,
            rawLook.y * scaledLookSensitivity * (_invertY ? -1f : 1f) * SensitivityMultiplier
        );

        return new PlayerInputData
        {
            MoveVector = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero,
            LookVector = scaledLook,

            JumpPressed = _jumpAction != null && _jumpAction.WasPressedThisFrame(),
            JumpHeld = _jumpAction != null && _jumpAction.IsPressed(),
            SprintHeld = _sprintAction != null && _sprintAction.IsPressed(),
            CrouchHeld = _crouchAction != null && _crouchAction.IsPressed(),

            PrimaryFirePressed = _attackAction != null && _attackAction.WasPressedThisFrame(),
            PrimaryFireHeld = _attackAction != null && _attackAction.IsPressed(),
            SecondaryFirePressed = _aimAction != null && _aimAction.WasPressedThisFrame(),
            SecondaryFireHeld = _aimAction != null && _aimAction.IsPressed(),

            ReloadPressed = _reloadAction != null && _reloadAction.WasPressedThisFrame(),
            GrenadePressed = _grenadeAction != null && _grenadeAction.WasPressedThisFrame(),
            SkillPressed = _skillAction != null && _skillAction.WasPressedThisFrame(),
            MeleePressed = _meleeAction != null && _meleeAction.WasPressedThisFrame(),
            PingPressed = _pingAction != null && _pingAction.WasPressedThisFrame(),
            
            InteractPressed = _interactAction != null && _interactAction.WasPressedThisFrame(),
            InteractHeld = _interactAction != null && _interactAction.IsPressed(),
            WeaponSlotPressed = GetWeaponSlotPressed()
        };
    }

    private void CloseLastActiveUIBlocker()
    {
        if (_activeUIBlockers.Count > 0)
        {
            var lastBlocker = _activeUIBlockers[^1];
            lastBlocker.Close();
        }
    }

    private int GetWeaponSlotPressed()
    {
        if (_slot1Action != null && _slot1Action.WasPressedThisFrame()) return 1;
        if (_slot2Action != null && _slot2Action.WasPressedThisFrame()) return 2;
        if (_slot3Action != null && _slot3Action.WasPressedThisFrame()) return 3;
        if (_slot4Action != null && _slot4Action.WasPressedThisFrame()) return 4;
        return 0;
    }
    
    public void RegisterUIBlocker(InputBlockerUI blocker)
    {
        if (_activeUIBlockers.Contains(blocker))
            return;
        
        _activeUIBlockers.Add(blocker);
        if (_activeUIBlockers.Count == 1)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _playerMap?.Disable();
        }
    }

    public void UnregisterUIBlocker(InputBlockerUI blocker)
    {
        if (!_activeUIBlockers.Contains(blocker))
            return;
        
        _activeUIBlockers.Remove(blocker);
        if (_activeUIBlockers.Count == 0)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _playerMap?.Enable();
        }
    }
}
}