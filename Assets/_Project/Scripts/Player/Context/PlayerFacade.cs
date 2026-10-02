using Project.Combat;
using Project.Combat.Health;
using Project.Core.Input;
using Project.Player.Data;
using Project.Player.Detection;
using Project.Player.Input;
using Project.Player.Interaction;
using Project.Player.Locomotion;
using Project.Player.Loot;
using PurrNet;
using UnityEngine;

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
    [SerializeField] private PlayerDownedState _downedState;
    [SerializeField] private HealthComponent _healthComponent;
    [SerializeField] private RuntimePlayerProfile _profile;
    [SerializeField] private PlayerDuffleBagController _duffleBagController;
    [SerializeField] private int _remotePlayerLayerIndex;
    
    public LocomotionController LocomotionController => _locomotionController;
    public PlayerDownedState DownedState => _downedState;
    public HealthComponent HealthComponent => _healthComponent;
    public IPlayerInputProvider InputProvider => _activeInputProvider;
    public LocalPlayerInputProvider LocalInputProvider => _inputProvider;
    public PlayerInteractor Interactor => _interactor;
    public PlayerTargetDetector TargetDetector => _targetDetector;
    public RuntimePlayerProfile Profile => _profile;
    public PlayerDuffleBagController DuffleBagController => _duffleBagController;
    public LocomotionState LocomotionState => _locomotionController ? _locomotionController.State : default;
    public PlayerInputData CurrentInput { get; private set; }

    private IPlayerInputProvider _activeInputProvider;

    private void Awake()
    {
        if (!_inputProvider)
            _inputProvider = GetComponent<LocalPlayerInputProvider>() ?? GetComponentInChildren<LocalPlayerInputProvider>();

        _activeInputProvider = (IPlayerInputProvider)_inputProvider ?? GetComponent<IPlayerInputProvider>() ?? GetComponentInChildren<IPlayerInputProvider>();

        if (!_locomotionController)
            _locomotionController = GetComponent<LocomotionController>() ?? GetComponentInChildren<LocomotionController>();
        if (!_downedState)
            _downedState = GetComponent<PlayerDownedState>() ?? GetComponentInChildren<PlayerDownedState>();
        if (!_healthComponent)
            _healthComponent = GetComponent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>(true) ?? GetComponentInParent<HealthComponent>(true);
        if (!_interactor)
            _interactor = GetComponent<PlayerInteractor>() ?? GetComponentInChildren<PlayerInteractor>();
        if (!_targetDetector)
            _targetDetector = GetComponent<PlayerTargetDetector>() ?? GetComponentInChildren<PlayerTargetDetector>();
        if (!_profile)
            _profile = GetComponent<RuntimePlayerProfile>() ?? GetComponentInChildren<RuntimePlayerProfile>();
        if (!_duffleBagController) 
            _duffleBagController = GetComponent<PlayerDuffleBagController>();
    }

    protected override void OnSpawned()
    {
        enabled = isOwner;
        
        if (!isOwner)
        {
            gameObject.layer = _remotePlayerLayerIndex;
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

        CurrentInput = _activeInputProvider?.GetCurrentInput() ?? PlayerInputData.Empty;
    }
}
}