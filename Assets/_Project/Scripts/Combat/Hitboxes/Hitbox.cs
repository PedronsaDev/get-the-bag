using UnityEngine;

namespace Project.Combat
{
/// <summary>
/// Locational hitbox router. Attaches to individual body part colliders (Head, Limbs, Armor Plating).
/// Applies multipliers and flat reductions, tags the HitLocation, and forwards to the owning HealthComponent.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Hitbox : MonoBehaviour, IDamageable
{
    [Header("Hitbox Settings")] 
    [SerializeField] private HitLocation _location = HitLocation.Core;
    [SerializeField] private float _damageMultiplier = 1.0f;
    [SerializeField] private float _flatDamageReduction = 0.0f;

    [Header("Target Vitality")]
    [SerializeField] private HealthComponent _targetHealth;

    public HitLocation Location => _location;
    public float DamageMultiplier => _damageMultiplier;
    public HealthComponent TargetHealth => _targetHealth;

    private void Awake()
    {
        if (!_targetHealth)
        {
            _targetHealth = GetComponentInParent<HealthComponent>();
        }
    }

    public DamageResult TakeDamage(DamagePayload payload)
    {
        payload.Location = _location;
        payload.Amount = Mathf.Max(0f, (payload.Amount - _flatDamageReduction) * _damageMultiplier);

        if (_targetHealth)
        {
            return _targetHealth.TakeDamage(payload);
        }

        var fallbackDamageable = transform.parent ? transform.parent.GetComponentInParent<IDamageable>() : null;
        if (fallbackDamageable != null && fallbackDamageable != (IDamageable)this)
        {
            return fallbackDamageable.TakeDamage(payload);
        }

        return new DamageResult { Applied = false };
    }

#if UNITY_EDITOR
    private BoxCollider _boxCollider;
    private CapsuleCollider _capsuleCollider;
    private SphereCollider _sphereCollider;

    private void OnValidate()
    {
        TryGetComponent(out _boxCollider);
        TryGetComponent(out _capsuleCollider);
        TryGetComponent(out _sphereCollider);

        if (!_targetHealth)
        {
            _targetHealth = GetComponentInParent<HealthComponent>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Color color = _location switch
        {
            HitLocation.Head => Color.blue,
            HitLocation.WeakPoint => Color.yellow,
            HitLocation.Limb => Color.green,
            HitLocation.ArmorPlating => Color.gray,
            _ => Color.red
        };

        color.a = 0.45f;
        Gizmos.color = color;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (_boxCollider)
        {
            Gizmos.DrawCube(_boxCollider.center, _boxCollider.size);
        }
        else if (_capsuleCollider)
        {
            Gizmos.DrawCube(_capsuleCollider.center,
                new Vector3(_capsuleCollider.radius * 2f, _capsuleCollider.height, _capsuleCollider.radius * 2f));
        }
        else if (_sphereCollider)
        {
            Gizmos.DrawSphere(_sphereCollider.center, _sphereCollider.radius);
        }
    }
#endif
}
}