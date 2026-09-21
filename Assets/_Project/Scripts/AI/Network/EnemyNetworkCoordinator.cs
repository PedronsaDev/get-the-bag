using Project.AI.Perception;
using PurrNet;
using UnityEngine;

namespace Project.AI.Network
{
[RequireComponent(typeof(AITargetMemory))]
public class EnemyNetworkCoordinator : NetworkBehaviour
{
    [SerializeField] private AITargetMemory _targetMemory;
    private readonly SyncVar<AlertState> _alertState = new SyncVar<AlertState>(AlertState.Unaware);

    private void Awake()
    {
        if (!_targetMemory) _targetMemory = GetComponent<AITargetMemory>();
    }
    
    protected override void OnSpawned()
    {
        if (isServer && _targetMemory) 
            _targetMemory.OnStateChanged += HandleStateChanged;
        
        _alertState.onChanged += HandleAlertStateChanged;
        HandleAlertStateChanged(_alertState.value);
    }

    protected override void OnDespawned()
    {
        if (isServer && _targetMemory)
            _targetMemory.OnStateChanged -= HandleStateChanged;

        _alertState.onChanged -= HandleAlertStateChanged;
    }

    private void HandleStateChanged(AlertState state)
    {
        _alertState.value = state;
    }
    
    private void HandleAlertStateChanged(AlertState state)
    {
        // Do something
    }

}
}