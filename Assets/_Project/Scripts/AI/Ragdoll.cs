using UnityEngine;

namespace Player.AI
{
public class Ragdoll : MonoBehaviour
{
    private Rigidbody[] _rbs;
    [SerializeField] private Animator _animator;

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