using Project.AI.Locomotion;
using Project.Combat;
using Project.Core.Physics;
using PurrNet;
using UnityEngine;

namespace Project.AI
{
public class AIController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private HealthComponent _healthComponent;
    [SerializeField] private AILocomotor _locomotor;
    [SerializeField] private Ragdoll _ragdoll;

    protected override void OnSpawned()
    {
        _healthComponent.OnDeath += HandleDeath;
    }

    protected override void OnDespawned()
    {
        _healthComponent.OnDeath -= HandleDeath;
    }

    private void HandleDeath(DamageResult result)
    {
        _ragdoll.Activate();
        _locomotor.Stop();
    }
}
}