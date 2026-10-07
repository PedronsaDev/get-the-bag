using UnityEngine;

namespace Project.Core.Physics
{
public class Ragdoll : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    private Rigidbody[] _rbs;

    private void Start()
    {
        _rbs = GetComponentsInChildren<Rigidbody>();
        Deactivate();
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