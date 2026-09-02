using System;
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
    [SerializeField] private int _remotePlayerLayerIndex;
    
    public PlayerInputData CurrentInput { get; private set; }

    private void Awake()
    {
        if (!_inputProvider)
            _inputProvider = GetComponent<LocalPlayerInputProvider>();
    }
    

    protected override void OnSpawned()
    {
        if (!isOwner)
        {
            this.gameObject.layer = _remotePlayerLayerIndex;
            DisableLocalComponents();
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
    }


    private void DisableLocalComponents()
    {
        if (_inputProvider)
            _inputProvider.enabled = false;
    }
}
}