using Project.Combat;
using UnityEngine;

namespace Player.AI
{
public class Ragdoll : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private HealthComponent _healthComponent;
    private Rigidbody[] _rbs;

    private void Start()
    {
        _rbs = GetComponentsInChildren<Rigidbody>();
        _healthComponent.OnDeath += OnDeath;
        Deactivate();
    }

    private void OnDestroy()
    {
        _healthComponent.OnDeath -= OnDeath;
    }

    private void OnDeath(DamageResult obj)
    {
        Activate();
    }

    public void Activate()
    {
        _animator.enabled = false;
        foreach (var rb in _rbs)
        {
            rb.isKinematic = false;
        }
    }

    public void Deactivate()
    {
        _animator.enabled = true;
        foreach (var rb in _rbs)
        {
            rb.isKinematic = true;
        }
    }
}
}