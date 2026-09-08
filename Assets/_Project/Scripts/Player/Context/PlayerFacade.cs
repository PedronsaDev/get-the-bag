using Project.Core.Input;
using Project.Player.Detection;
using Project.Player.Input;
using Project.Player.Interaction;
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
    [SerializeField] private int _remotePlayerLayerIndex;
    
    public LocalPlayerInputProvider InputProvider => _inputProvider;
    public PlayerInteractor Interactor => _interactor;
    public PlayerTargetDetector TargetDetector => _targetDetector;
    public LocomotionState LocomotionState { get; private set; }
    public PlayerInputData CurrentInput { get; private set; }

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