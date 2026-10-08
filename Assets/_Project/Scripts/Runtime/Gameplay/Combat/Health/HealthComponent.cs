using System;
using Project.Combat.Core;
using PurrNet;
using UnityEngine;

namespace Project.Combat.Health
{
public class HealthComponent : NetworkBehaviour, IDamageable
{
    [Header("Health")]
    [Min(1f)] [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private bool _invulnerable;

    [Header("Shield")]
    [Min(0f)] [SerializeField] private float _maxShield;
    [Min(0f)] [SerializeField] private float _shieldRegenDelay = 4f;
    [Min(0f)] [SerializeField] private float _shieldRegenRate = 15f;

    [SerializeField] private SyncVar<float> _currentHealth = new(100f);
    [SerializeField] private SyncVar<float> _currentShield = new(0f);
    private readonly SyncVar<bool> _isInvulnerable = new();
    private float _lastDamageTime;

    public float CurrentHealth => _currentHealth.value;
    public float MaxHealth => _maxHealth;
    public float CurrentShield => _currentShield.value;
    public float MaxShield => _maxShield;
    public bool HasShield => _maxShield > 0f;
    public bool IsDead => CurrentHealth <= 0f;
    public bool Invulnerable => isSpawned ? _isInvulnerable.value : _invulnerable;

    public event Action<DamageResult> OnDamaged;
    public event Action<DamageResult> OnDeath;
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnShieldChanged;
    public event Action OnShieldBroken;

    private void Awake()
    {
        _currentHealth.onChanged += HandleHealthChanged;
        _currentShield.onChanged += HandleShieldChanged;
    }

    protected override void OnDestroy()
    {
        _currentHealth.onChanged -= HandleHealthChanged;
        _currentShield.onChanged -= HandleShieldChanged;
        base.OnDestroy();
    }

    protected override void OnSpawned()
    {
        if (!isServer) return;
        _currentHealth.value = _maxHealth;
        _currentShield.value = _maxShield;
        _isInvulnerable.value = _invulnerable;
        _lastDamageTime = Time.time;
    }

    private void Update()
    {
        if (!isServer || IsDead || !HasShield || _shieldRegenRate <= 0f ||
            Time.time < _lastDamageTime + _shieldRegenDelay) return;
        RechargeShield(_shieldRegenRate * Time.deltaTime);
    }

    public DamageResult TakeDamage(DamagePayload payload)
    {
        if (!isServer || IsDead || Invulnerable || !PositiveFinite(payload.Amount)) return default;

        var shieldDamage = Mathf.Min(CurrentShield, payload.Amount);
        var healthDamage = Mathf.Min(CurrentHealth, payload.Amount - shieldDamage);
        _lastDamageTime = Time.time;
        _currentShield.value -= shieldDamage;
        _currentHealth.value -= healthDamage;

        var result = new DamageResult
        {
            Applied = true,
            RawDamage = payload.Amount,
            EffectiveDamage = shieldDamage + healthDamage,
            DamageToShield = shieldDamage,
            DamageToHealth = healthDamage,
            IsCritical = payload.Location is HitLocation.Head or HitLocation.WeakPoint,
            IsFatal = IsDead,
            Location = payload.Location,
            HitPoint = payload.HitPoint,
            HitNormal = payload.HitNormal,
            AttackerId = payload.AttackerId,
            RemainingHealth = CurrentHealth,
            RemainingShield = CurrentShield
        };
        RpcNotifyDamage(result);
        return result;
    }

    [ObserversRpc(runLocally: true)]
    private void RpcNotifyDamage(DamageResult result)
    {
        OnDamaged?.Invoke(result);
        if (result.DamageToShield > 0f && result.RemainingShield == 0f)
            OnShieldBroken?.Invoke();
        if (result.IsFatal) OnDeath?.Invoke(result);
    }

    public bool Heal(float amount)
    {
        if (!isServer || IsDead || !PositiveFinite(amount) || CurrentHealth >= _maxHealth) return false;
        _currentHealth.value += Mathf.Min(amount, _maxHealth - CurrentHealth);
        return true;
    }

    public bool RechargeShield(float amount)
    {
        if (!isServer || IsDead || !PositiveFinite(amount) || CurrentShield >= _maxShield) return false;
        _currentShield.value += Mathf.Min(amount, _maxShield - CurrentShield);
        return true;
    }

    public bool Revive(float healthAmount)
    {
        if (!isServer || !IsDead || !PositiveFinite(healthAmount)) return false;
        _currentHealth.value = Mathf.Clamp(healthAmount, 1f, _maxHealth);
        _lastDamageTime = Time.time;
        return true;
    }

    public bool SetInvulnerable(bool value)
    {
        if (!isServer) return false;
        _isInvulnerable.value = value;
        return true;
    }

    private void HandleHealthChanged(float current) => OnHealthChanged?.Invoke(current, _maxHealth);
    private void HandleShieldChanged(float current) => OnShieldChanged?.Invoke(current, _maxShield);
    private static bool PositiveFinite(float amount) => float.IsFinite(amount) && amount > 0f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        _maxHealth = float.IsFinite(_maxHealth) ? Mathf.Max(1f, _maxHealth) : 100f;
        _maxShield = float.IsFinite(_maxShield) ? Mathf.Max(0f, _maxShield) : 0f;
        _shieldRegenDelay = float.IsFinite(_shieldRegenDelay) ? Mathf.Max(0f, _shieldRegenDelay) : 4f;
        _shieldRegenRate = float.IsFinite(_shieldRegenRate) ? Mathf.Max(0f, _shieldRegenRate) : 15f;
    }
#endif
}
}
