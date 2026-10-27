using Project.AI.Combat;
using Project.AI.Perception;
using Project.Combat.Core;
using Project.Combat.Health;
using Project.Core.Player;
using PurrNet;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Presentation
{
    [DisallowMultipleComponent]
    public sealed class AIGuardPresentation : NetworkBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");

        [SerializeField] private AIWeaponExecutor _weapon;
        [SerializeField] private Animator _animator;
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private HealthComponent _health;
        [SerializeField] private AITargetMemory _memory;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private LineRenderer _tracer;
        [SerializeField, Range(0f, 1f)] private float _shotVolume = 0.35f;
        [SerializeField, Min(0.01f)] private float _tracerDuration = 0.07f;
        [SerializeField, Min(0f)] private float _animationDamping = 0.1f;

        private bool _subscribed;
        private float _tracerUntil;

        private void Awake()
        {
            if (!_weapon) _weapon = GetComponent<AIWeaponExecutor>();
            if (!_animator) _animator = GetComponentInChildren<Animator>(true);
            if (!_agent) _agent = GetComponent<NavMeshAgent>();
            if (!_health) _health = GetComponentInChildren<HealthComponent>(true);
            if (!_memory) _memory = GetComponent<AITargetMemory>();
            ClearEffects();
        }

        protected override void OnSpawned() => Subscribe();
        private void OnEnable() { if (isSpawned) Subscribe(); }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            if (_weapon) _weapon.OnShotFired += PlayShot;
            if (_health) _health.OnDamaged += HandleDamage;
        }

        protected override void OnDespawned() => StopPresentation();
        private void OnDisable() => StopPresentation();

        private void StopPresentation()
        {
            if (_subscribed)
            {
                if (_weapon) _weapon.OnShotFired -= PlayShot;
                if (_health) _health.OnDamaged -= HandleDamage;
                _subscribed = false;
            }
            ClearEffects();
        }

        private void ClearEffects()
        {
            if (_tracer) _tracer.enabled = false;
            if (_muzzleFlash) _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            if (!isSpawned) return;
            if (isServer && _animator && _animator.isActiveAndEnabled)
            {
                float speed = _agent && _agent.isActiveAndEnabled && _agent.isOnNavMesh &&
                              (!_health || !_health.IsDead) ? _agent.velocity.magnitude : 0f;
                _animator.SetFloat(Speed, speed, _animationDamping, Time.deltaTime);
            }
            if (_tracer && _tracer.enabled)
            {
                if (Time.time >= _tracerUntil) _tracer.enabled = false;
                else if (_muzzle) _tracer.SetPosition(0, _muzzle.position);
            }
        }

        private void PlayShot(Vector3 origin, Vector3 endPoint)
        {
            if (!isClient || !isActiveAndEnabled || (_health && _health.IsDead)) return;
            if (_muzzleFlash) _muzzleFlash.Play(true);
            if (!_tracer) return;
            _tracer.SetPosition(0, _muzzle ? _muzzle.position : origin);
            _tracer.SetPosition(1, endPoint);
            _tracerUntil = Time.time + _tracerDuration;
            _tracer.enabled = true;
        }

        private void HandleDamage(DamageResult damage)
        {
            if (!isServer || !_memory || !damage.Applied || !damage.AttackerId.HasValue ||
                (_health && _health.IsDead)) return;
            if (PlayerIdentifier.TryGetPlayer(damage.AttackerId.Value, out var attacker) && !attacker.IsDead)
                _memory.ReportDamage(attacker.gameObject, attacker.BodyTransform.position);
        }
    }
}
