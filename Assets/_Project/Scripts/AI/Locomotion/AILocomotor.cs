using PurrNet;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Locomotion
{
public class AILocomotor : NetworkBehaviour
{
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private Transform _target;
    [SerializeField] private Animator _animator;

    protected override void OnSpawned()
    {
        _agent.enabled = true;
        _agent.destination = _target.position;
    }

    private void Update()
    {
        if (!isServer)
            return;

        _animator.SetFloat("Speed", _agent.velocity.magnitude);
    }

    public void Stop()
    {
        _agent.enabled = false;
    }
}
}