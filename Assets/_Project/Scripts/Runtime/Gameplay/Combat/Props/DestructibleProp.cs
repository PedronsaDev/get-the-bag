using System;
using System.Collections.Generic;
using Project.Combat.Core;
using Project.Combat.Health;
using PurrNet;
using UnityEngine;

namespace Project.Combat.Props
{
public class DestructibleProp : NetworkBehaviour, IDamageable
{
    [Header("Durability")]
    [Min(1f)] [SerializeField] private float _maxHealth = 40f;
    [SerializeField] private SyncVar<float> _currentHealth = new(40f);

    [Header("Destruction Effects")]
    [SerializeField] private GameObject _debrisPrefab;
    [SerializeField] private GameObject _explosionVfxPrefab;
    [SerializeField] private AudioClip _destructionAudio;

    [Header("Explosion")]
    [SerializeField] private bool _isExplosive;
    [Min(0.01f)] [SerializeField] private float _explosionRadius = 4.5f;
    [Min(0f)] [SerializeField] private float _explosionDamage = 80f;
    [Min(0f)] [SerializeField] private float _explosionForce = 600f;
    [SerializeField] private LayerMask _damageLayerMask = ~0;

    public float CurrentHealth => _currentHealth.value;
    public bool IsDestroyed => CurrentHealth <= 0f;
    public event Action OnDestroyed;

    protected override void OnSpawned()
    {
        if (isServer) _currentHealth.value = _maxHealth;
    }

    public DamageResult TakeDamage(DamagePayload payload)
    {
        if (!isServer || IsDestroyed || !float.IsFinite(payload.Amount) || payload.Amount <= 0f)
            return default;
        var appliedDamage = Mathf.Min(CurrentHealth, payload.Amount);
        _currentHealth.value -= appliedDamage;
        var result = new DamageResult
        {
            Applied = true, RawDamage = payload.Amount, EffectiveDamage = appliedDamage,
            DamageToHealth = appliedDamage, IsFatal = IsDestroyed,
            HitPoint = payload.HitPoint, HitNormal = payload.HitNormal,
            Location = payload.Location, AttackerId = payload.AttackerId,
            RemainingHealth = CurrentHealth
        };
        if (!result.IsFatal) return result;

        // Marked destroyed before chain explosions can attempt to hit this prop again.
        if (_isExplosive) ExecuteExplosion(payload.AttackerId);
        RpcNotifyDestroyed(transform.position, transform.rotation);
        Despawn();
        return result;
    }

    private void ExecuteExplosion(PlayerID? attackerId)
    {
        var origin = transform.position;
        var targets = new Dictionary<IDamageable, Vector3>();
        var bodies = new HashSet<Rigidbody>();
        var colliders = Physics.OverlapSphere(origin, _explosionRadius, _damageLayerMask,
            QueryTriggerInteraction.Collide);
        foreach (var collider in colliders)
        {
            if (collider.transform.IsChildOf(transform)) continue;
            if (collider.isTrigger && !collider.TryGetComponent<IDamageable>(out _)) continue;
            // AoE damages a character once, rather than once for every limb hitbox.
            IDamageable target = collider.GetComponentInParent<HealthComponent>();
            target ??= collider.GetComponentInParent<DestructibleProp>();
            target ??= collider.GetComponentInParent<IDamageable>();
            if (target is Hitbox hitbox && hitbox.TargetHealth) target = hitbox.TargetHealth;
            if (target != null)
            {
                var point = collider.ClosestPoint(origin);
                if (!targets.TryGetValue(target, out var previous) ||
                    (point - origin).sqrMagnitude < (previous - origin).sqrMagnitude)
                    targets[target] = point;
            }
            if (!collider.isTrigger && collider.attachedRigidbody &&
                !collider.attachedRigidbody.isKinematic) bodies.Add(collider.attachedRigidbody);
        }
        foreach (var pair in targets)
        {
            var offset = pair.Value - origin;
            var falloff = Mathf.Clamp01(1f - offset.magnitude / _explosionRadius);
            pair.Key.TakeDamage(new DamagePayload(_explosionDamage * falloff,
                hitPoint: pair.Value, hitNormal: -offset.normalized,
                impactDirection: offset.normalized, attackerId: attackerId));
        }
        foreach (var body in bodies)
            if (body) body.AddExplosionForce(_explosionForce, origin, _explosionRadius, 1f, ForceMode.Impulse);
    }

    [ObserversRpc(runLocally: true)]
    private void RpcNotifyDestroyed(Vector3 position, Quaternion rotation)
    {
        OnDestroyed?.Invoke();
        if (!isClient) return;
        if (_debrisPrefab) Instantiate(_debrisPrefab, position, rotation);
        if (_explosionVfxPrefab) Instantiate(_explosionVfxPrefab, position, Quaternion.identity);
        if (_destructionAudio) AudioSource.PlayClipAtPoint(_destructionAudio, position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _maxHealth = float.IsFinite(_maxHealth) ? Mathf.Max(1f, _maxHealth) : 40f;
        _explosionRadius = float.IsFinite(_explosionRadius) ? Mathf.Max(0.01f, _explosionRadius) : 4.5f;
        _explosionDamage = float.IsFinite(_explosionDamage) ? Mathf.Max(0f, _explosionDamage) : 80f;
        _explosionForce = float.IsFinite(_explosionForce) ? Mathf.Max(0f, _explosionForce) : 600f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_isExplosive) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }
#endif
}
}
