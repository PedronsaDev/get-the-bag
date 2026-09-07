using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Kinematic ballistic projectile entity with swept-sphere continuous collision detection.
    /// Supports dynamic behavioral modifiers for legendary projectile splitting, waves, and acceleration.
    /// </summary>
    public class ProjectileInstance : MonoBehaviour
    {
        [SerializeField] private float _collisionRadius = 0.08f;
        [SerializeField] private TrailRenderer _trailRenderer;

        private Vector3 _position;
        private Vector3 _velocity;
        private DamagePayload _payload;
        private LayerMask _hitMask;
        private float _maxLifetime = 6.0f;
        private float _currentLife;
        private float _distanceTraveled;
        private bool _isAlive;
        private bool _isCosmetic;

        private readonly List<IProjectileBehavior> _behaviors = new();

        public Vector3 Position { get => _position; set => _position = value; }
        public Vector3 Velocity { get => _velocity; set => _velocity = value; }
        public DamagePayload Payload { get => _payload; set => _payload = value; }
        public LayerMask HitMask { get => _hitMask; set => _hitMask = value; }
        public float DistanceTraveled => _distanceTraveled;
        public float ElapsedLife => _currentLife;
        public bool IsAlive => _isAlive;
        public bool IsCosmetic => _isCosmetic;
        public bool IsInPool { get; set; }

        public event Action<ProjectileInstance> OnDespawn;

        public void Initialize(Vector3 startPosition, Vector3 initialVelocity, DamagePayload payload, LayerMask hitMask, IEnumerable<IProjectileBehavior> behaviors = null, bool isCosmetic = false)
        {
            _position = startPosition;
            _velocity = initialVelocity;
            _payload = payload;
            _hitMask = hitMask;
            _isCosmetic = isCosmetic;
            _currentLife = 0f;
            _distanceTraveled = 0f;
            _isAlive = true;

            transform.position = _position;

            if (_velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity.normalized);
            }

            if (_trailRenderer)
            {
                _trailRenderer.Clear();
                _trailRenderer.emitting = true;
            }

            _behaviors.Clear();
            if (behaviors != null)
            {
                _behaviors.AddRange(behaviors);
            }

            for (int i = 0; i < _behaviors.Count; i++)
            {
                _behaviors[i].OnSpawn(this);
            }
        }

        public void AddBehavior(IProjectileBehavior behavior)
        {
            if (behavior != null)
            {
                _behaviors.Add(behavior);
                behavior.OnSpawn(this);
            }
        }

        private void Update()
        {
            if (!_isAlive) return;

            float dt = Time.deltaTime;
            _currentLife += dt;

            if (_currentLife >= _maxLifetime)
            {
                Despawn();
                return;
            }

            // 1. Execute travel behaviors (sine waves, lateral splitting, acceleration)
            for (int i = 0; i < _behaviors.Count; i++)
            {
                _behaviors[i].OnTravel(this, dt);
            }

            // 2. Swept sphere collision check
            float stepDistance = _velocity.magnitude * dt;
            Vector3 travelDirection = _velocity.normalized;

            if (Physics.SphereCast(_position, _collisionRadius, travelDirection, out RaycastHit hit, stepDistance, _hitMask, QueryTriggerInteraction.Ignore))
            {
                HandleImpact(hit);
                return;
            }

            // 3. Advance position
            _position += _velocity * dt;
            _distanceTraveled += stepDistance;
            transform.position = _position;

            if (_velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity.normalized);
            }
        }

        private void HandleImpact(RaycastHit hit)
        {
            _position = hit.point;
            transform.position = _position;
            var preventDespawn = false;

            // Notify behaviors
            foreach (var projectileBehavior in _behaviors)
            {
                if (projectileBehavior.OnHit(this, hit, ref _payload)) 
                    preventDespawn = true;
            }

            // Always spawn impact VFX and audio
            ImpactEffectPool.Instance.Spawn(hit.point, hit.normal, _payload.Type);

            // Deliver damage and physics only if non-cosmetic
            if (!_isCosmetic)
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    _payload.HitPoint = hit.point;
                    _payload.HitNormal = hit.normal;
                    _payload.ImpactDirection = _velocity.normalized;
                    damageable.TakeDamage(_payload);
                }

                if (hit.rigidbody && !hit.rigidbody.isKinematic)
                {
                    hit.rigidbody.AddForceAtPosition(_velocity.normalized * (_payload.Amount * 3f), hit.point, ForceMode.Impulse);
                }
            }
            
            if (preventDespawn)
                return;

            Despawn();
        }

        public void Despawn()
        {
            if (!_isAlive) return;
            _isAlive = false;
            if (_trailRenderer)
            {
                _trailRenderer.emitting = false;
                _trailRenderer.Clear();
            }
            OnDespawn?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
