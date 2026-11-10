using Project.AI.Perception;
using Project.Combat.Health;
using Project.Core.Physics;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Presentation
{
[DisallowMultipleComponent]
public class AIDeathRagdoll : MonoBehaviour
{
    [SerializeField] private HealthComponent _health;
    [SerializeField] private Ragdoll _ragdoll;
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private AICorpse _corpse;

    private void Awake()
    {
        if (!_corpse)
            _corpse = GetComponent<AICorpse>();
        if (!_health)
            _health = GetComponent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>(true);
        if (!_ragdoll)
            _ragdoll = GetComponentInChildren<Ragdoll>(true);
        if (!_agent)
            _agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        if (_health)
            _health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (_health)
            _health.OnDeath -= HandleDeath;
    }

    private void HandleDeath(Project.Combat.Core.DamageResult result)
    {
        if (_agent)
            _agent.enabled = false;
        if (_ragdoll)
            _ragdoll.Activate();
        if (_corpse)
            _corpse.enabled = true;
    }
}
}
