using System;
using PurrNet;
using UnityEngine;

namespace Project.Combat
{
/// <summary>
/// Server-authoritative vitality manager supporting health, shields, armor, and elemental interactions.
/// Works for Players, AI Enemies, Bosses, and interactive entities.
/// </summary>
public class HealthComponent : NetworkBehaviour, IDamageable
{
    [Header("Health Settings")] 
    [SerializeField] private HealthType _healthType = HealthType.Flesh;
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField, Range(0f, 0.9f)] private float _armorRating = 0f;
    [SerializeField] private bool _invulnerable = false;

    [Header("Shield Settings")] 
    [SerializeField] private float _maxShield = 0f;
    [SerializeField] private float _shieldRegenDelay = 4f;
    [SerializeField] private float _shieldRegenRate = 15f;

    [Header("Networked State")] 
    [SerializeField] private SyncVar<float> _currentHealth = new(100f);
    [SerializeField] private SyncVar<float> _currentShield = new(0f);

    private float _lastDamageTime = -100f;
    private bool _isDead;

    public bool HasShield => _maxShield > 0f;
    public float CurrentHealth => _currentHealth.value;
    public float MaxHealth => _maxHealth;
    public float CurrentShield => _currentShield.value;
    public float MaxShield => _maxShield;
    public HealthType HealthType => _healthType;
    public bool IsDead => _isDead || _currentHealth.value <= 0f;

    public bool Invulnerable
    {
        get => _invulnerable;
        set => _invulnerable = value;
    }

    public event Action<DamageResult> OnDamaged;
    public event Action<DamageResult> OnDeath;
    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action<float, float> OnShieldChanged; // (current, max)
    public event Action OnShieldBroken;

    private void Awake()
    {
        _currentHealth.onChanged += (val) => OnHealthChanged?.Invoke(val, _maxHealth);
        _currentShield.onChanged += (val) => OnShieldChanged?.Invoke(val, _maxShield);
    }

    protected override void OnSpawned()
    {
        if (!NetworkManager.main || isServer)
        {
            _currentHealth.value = _maxHealth;
            _currentShield.value = _maxShield;
            _isDead = false;
        }
    }

    private void Update()
    {
        // Server-authoritative shield regeneration
        if (!isServer && NetworkManager.main) return;
        if (_isDead || _maxShield <= 0f) return;

        if (Time.time >= _lastDamageTime + _shieldRegenDelay && _currentShield.value < _maxShield)
        {
            float newShield = Mathf.Min(_maxShield, _currentShield.value + _shieldRegenRate * Time.deltaTime);
            _currentShield.value = newShield;
        }
    }

    /// <summary>
    /// IDamageable entry point.
    /// If called on client, delegates to ServerRpc; if server or offline, evaluates immediately.
    /// </summary>
    public DamageResult TakeDamage(DamagePayload payload)
    {
        if (_invulnerable || _isDead)
        {
            return new DamageResult { Applied = false };
        }

        if (NetworkManager.main && !isServer)
        {
            // Client detected hit -> ask server to process
            RpcRequestTakeDamage(payload);
            return new DamageResult
            {
                Applied = true,
                RawDamage = payload.Amount,
                EffectiveDamage = payload.Amount,
                HitPoint = payload.HitPoint,
                HitNormal = payload.HitNormal,
                Location = payload.Location,
                AttackerId = payload.AttackerId
            };
        }

        // Server-side evaluation
        return ProcessDamageAuthoritative(payload);
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcRequestTakeDamage(DamagePayload payload, RPCInfo info = default)
    {
        // Inject sender as attacker if not explicitly specified
        if (!payload.AttackerId.HasValue)
        {
            payload.AttackerId = info.sender;
        }

        ProcessDamageAuthoritative(payload);
    }

    private DamageResult ProcessDamageAuthoritative(DamagePayload payload)
    {
        if (_invulnerable || _isDead)
        {
            return new DamageResult { Applied = false };
        }

        _lastDamageTime = Time.time;
        float rawDamage = payload.Amount;
        float remainingToAbsorb = rawDamage;
        float damageToShield = 0f;
        float damageToHealth = 0f;
        bool shieldWasActive = _currentShield.value > 0f;

        // 1. True Damage bypasses all shields and armor
        if (payload.Type == DamageType.TrueDamage)
        {
            damageToHealth = remainingToAbsorb;
            _currentHealth.value = Mathf.Max(0f, _currentHealth.value - damageToHealth);
        }
        else
        {
            // 2. Shield absorption (Shock deals 1.5x bonus to shields)
            if (_currentShield.value > 0f)
            {
                float shieldMultiplier = payload.Type == DamageType.Shock ? 1.5f : 1.0f;
                float effectiveDamageVsShield = remainingToAbsorb * shieldMultiplier;

                if (_currentShield.value >= effectiveDamageVsShield)
                {
                    damageToShield = effectiveDamageVsShield;
                    _currentShield.value -= effectiveDamageVsShield;
                    remainingToAbsorb = 0f;
                }
                else
                {
                    // Shield broken! Remaining damage pierces through
                    damageToShield = _currentShield.value;
                    float leftoverDamage = (effectiveDamageVsShield - _currentShield.value) / shieldMultiplier;
                    _currentShield.value = 0f;
                    remainingToAbsorb = leftoverDamage;

                    if (shieldWasActive)
                    {
                        RpcOnShieldBroken();
                    }
                }
            }

            // 3. Health absorption & Elemental / Armor interactions
            if (remainingToAbsorb > 0f)
            {
                float healthMultiplier = 1.0f;

                switch (_healthType)
                {
                    case HealthType.Armored:
                        // Armor resists kinetic, but corrosive melts it
                        if (payload.Type == DamageType.Corrosive)
                            healthMultiplier = 1.5f;
                        else if (payload.Type == DamageType.Physical)
                            healthMultiplier = Mathf.Max(0.1f, 1f - _armorRating);
                        break;

                    case HealthType.Flesh:
                        // Flesh takes bonus damage from fire
                        if (payload.Type == DamageType.Incendiary)
                            healthMultiplier = 1.5f;
                        break;

                    case HealthType.Mechanical:
                        // Machines take bonus damage from corrosive and shock
                        if (payload.Type == DamageType.Shock || payload.Type == DamageType.Corrosive)
                            healthMultiplier = 1.3f;
                        break;
                }

                damageToHealth = remainingToAbsorb * healthMultiplier;
                _currentHealth.value = Mathf.Max(0f, _currentHealth.value - damageToHealth);
            }
        }

        bool isFatal = _currentHealth.value <= 0.001f;
        if (isFatal)
        {
            _isDead = true;
        }

        DamageResult result = new DamageResult
        {
            Applied = true,
            RawDamage = rawDamage,
            EffectiveDamage = damageToShield + damageToHealth,
            DamageToShield = damageToShield,
            DamageToHealth = damageToHealth,
            IsCritical = payload.Location is HitLocation.Head or HitLocation.WeakPoint,
            IsFatal = isFatal,
            Location = payload.Location,
            HitPoint = payload.HitPoint,
            HitNormal = payload.HitNormal,
            AttackerId = payload.AttackerId,
            RemainingHealth = _currentHealth.value,
            RemainingShield = _currentShield.value
        };

        RpcOnDamaged(result);

        if (isFatal)
        {
            RpcOnDeath(result);
        }

        return result;
    }

    [ObserversRpc(runLocally: true)]
    private void RpcOnDamaged(DamageResult result)
    {
        OnDamaged?.Invoke(result);
    }

    [ObserversRpc(runLocally: true)]
    private void RpcOnShieldBroken()
    {
        OnShieldBroken?.Invoke();
    }

    [ObserversRpc(runLocally: true)]
    private void RpcOnDeath(DamageResult result)
    {
        _isDead = true;
        OnDeath?.Invoke(result);
    }

    /// <summary>
    /// Restores health up to maximum.
    /// </summary>
    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f) return;
        if (NetworkManager.main && !isServer) return;

        _currentHealth.value = Mathf.Min(_maxHealth, _currentHealth.value + amount);
    }

    /// <summary>
    /// Restores shield up to maximum.
    /// </summary>
    public void RechargeShield(float amount)
    {
        if (_isDead || amount <= 0f) return;
        if (NetworkManager.main && !isServer) return;

        _currentShield.value = Mathf.Min(_maxShield, _currentShield.value + amount);
    }
}
}