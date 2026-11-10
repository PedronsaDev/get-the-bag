using Project.Combat.Core;
using PurrNet;
using UnityEngine;

namespace Project.Combat.Props
{
/// <summary>
/// Shooting-range target. Damage is applied on the server; at zero health the target
/// falls over (replicated through a SyncVar), then stands back up after a delay.
/// </summary>
public class PracticeTarget : NetworkBehaviour, IDamageable
{
    [Header("Durability")]
    [Min(1f)] [SerializeField] private float _maxHealth = 60f;
    [Min(0.5f)] [SerializeField] private float _resetSeconds = 4f;

    [Header("Visual")]
    [Tooltip("Rotated when the target is down. Defaults to this transform.")]
    [SerializeField] private Transform _visual;
    [SerializeField] private float _downAngle = 90f;
    [SerializeField] private float _rotateSpeed = 360f;
    [SerializeField] private Collider[] _hitColliders;

    public SyncVar<bool> IsDown = new(false);
    private bool _localDown;
    private float _health;
    private float _upAt;
    private Quaternion _upRotation;

    private bool Down => isSpawned ? IsDown.value : _localDown;

    private void Awake()
    {
        if (!_visual) _visual = transform;
        _upRotation = _visual.localRotation;
        if (_hitColliders == null || _hitColliders.Length == 0)
            _hitColliders = GetComponentsInChildren<Collider>();
    }

    protected override void OnSpawned()
    {
        if (isServer) _health = _maxHealth;
    }

    private void Update()
    {
        // Offline hubs are never network-spawned, so the server path is also the offline path.
        bool authority = !isSpawned || isServer;
        if (authority && Down && Time.time >= _upAt)
            StandUp();

        var target = Down ? _upRotation * Quaternion.Euler(-_downAngle, 0f, 0f) : _upRotation;
        _visual.localRotation = Quaternion.RotateTowards(_visual.localRotation, target, _rotateSpeed * Time.deltaTime);

        bool shootable = !Down;
        for (int i = 0; i < _hitColliders.Length; i++)
            if (_hitColliders[i] && _hitColliders[i].enabled != shootable)
                _hitColliders[i].enabled = shootable;
    }

    public DamageResult TakeDamage(DamagePayload payload)
    {
        bool authority = !isSpawned || isServer;
        if (!authority || Down || !float.IsFinite(payload.Amount) || payload.Amount <= 0f)
            return default;

        if (!isSpawned && _health <= 0f) _health = _maxHealth;

        float applied = Mathf.Min(_health, payload.Amount);
        _health -= applied;
        bool fatal = _health <= 0f;

        if (fatal)
        {
            _upAt = Time.time + _resetSeconds;
            SetDown(true);
        }

        return new DamageResult
        {
            Applied = true,
            RawDamage = payload.Amount,
            EffectiveDamage = applied,
            DamageToHealth = applied,
            IsFatal = fatal,
            HitPoint = payload.HitPoint,
            HitNormal = payload.HitNormal,
            Location = payload.Location,
            AttackerId = payload.AttackerId,
            RemainingHealth = _health
        };
    }

    private void StandUp()
    {
        _health = _maxHealth;
        SetDown(false);
    }

    private void SetDown(bool down)
    {
        if (isSpawned) IsDown.value = down;
        else _localDown = down;
    }
}
}
