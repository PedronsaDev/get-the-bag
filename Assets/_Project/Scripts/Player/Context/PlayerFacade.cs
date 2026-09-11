using Project.Core.Input;
using Project.Player.Detection;
using Project.Player.Input;
using Project.Player.Interaction;
using Project.Player.Loot;
using PurrNet;
using UnityEngine;
using UnityEngine.Serialization;

namespace Project.Player
{
[DefaultExecutionOrder(-100)]
public class PlayerFacade : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private LocalPlayerInputProvider _inputProvider;
    [SerializeField] private LocomotionController _locomotionController;
    [SerializeField] private PlayerInteractor _interactor;
    [SerializeField] private PlayerTargetDetector _targetDetector;
    [FormerlySerializedAs("_inventory")]
    [SerializeField] private PlayerInventoryController _inventoryController;
    [SerializeField] private Project.Player.Locomotion.PlayerDownedState _downedState;
    [SerializeField] private Project.Combat.HealthComponent _healthComponent;
    [SerializeField] private int _remotePlayerLayerIndex;
    
    public PlayerInventoryController InventoryController => _inventoryController;
    public LocomotionController LocomotionController => _locomotionController;
    public Project.Player.Locomotion.PlayerDownedState DownedState => _downedState;
    public Project.Combat.HealthComponent Health => _healthComponent;
    public LocalPlayerInputProvider InputProvider => _inputProvider;
    public PlayerInteractor Interactor => _interactor;
    public PlayerTargetDetector TargetDetector => _targetDetector;
    public LocomotionState LocomotionState { get; private set; }
    public PlayerInputData CurrentInput { get; private set; }

    private void Awake()
    {
        if (!_inventoryController)
            _inventoryController = GetComponent<PlayerInventoryController>() ?? GetComponentInChildren<PlayerInventoryController>();
        if (!_inputProvider)
            _inputProvider = GetComponent<LocalPlayerInputProvider>() ?? GetComponentInChildren<LocalPlayerInputProvider>();
        if (!_locomotionController)
            _locomotionController = GetComponent<LocomotionController>() ?? GetComponentInChildren<LocomotionController>();
        if (!_downedState)
            _downedState = GetComponent<Project.Player.Locomotion.PlayerDownedState>() ?? GetComponentInChildren<Project.Player.Locomotion.PlayerDownedState>();
        if (!_healthComponent)
            _healthComponent = GetComponent<Project.Combat.HealthComponent>() ?? GetComponentInChildren<Project.Combat.HealthComponent>(true) ?? GetComponentInParent<Project.Combat.HealthComponent>(true);
        if (!_interactor)
            _interactor = GetComponent<PlayerInteractor>() ?? GetComponentInChildren<PlayerInteractor>();
        if (!_targetDetector)
            _targetDetector = GetComponent<PlayerTargetDetector>() ?? GetComponentInChildren<PlayerTargetDetector>();
    }

    protected override void OnSpawned()
    {
        this.enabled = isOwner;
        
        if (!isOwner)
        {
            this.gameObject.layer = _remotePlayerLayerIndex;
            return;
        }
        
        LocalPlayerContext.RegisterLocalPlayer(this);
    }
    
    protected override void OnDespawned()
    {
        if (!isOwner)
            return;
        
        LocalPlayerContext.UnregisterLocalPlayer(this);
    }

    private void Update()
    {
        if (!isOwner)
            return;

        CurrentInput = _inputProvider.GetCurrentInput();
        LocomotionState = _locomotionController.State;
    }
}
}