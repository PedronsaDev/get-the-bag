using Project.Core.Input;
using Project.Player.Input;
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
    [SerializeField] private int _remotePlayerLayerIndex;
    
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