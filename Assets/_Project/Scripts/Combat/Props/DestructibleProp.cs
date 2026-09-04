using System;
using PurrNet;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Network-synchronized destructible prop (crates, barrels, barricades).
    /// Supports debris spawning and optional area-of-effect explosions.
    /// </summary>
    public class DestructibleProp : NetworkBehaviour, IDamageable
    {
        [Header("Durability")]
        [SerializeField] private float _maxHealth = 40f;
        [SerializeField] private SyncVar<float> _currentHealth = new(40f);

        [Header("Destruction Effects")]
        [SerializeField] private GameObject _debrisPrefab;
        [SerializeField] private GameObject _explosionVfxPrefab;
        [SerializeField] private AudioClip _destructionAudio;

        [Header("Explosive Attributes")]
        [SerializeField] private bool _isExplosive = false;
        [SerializeField] private float _explosionRadius = 4.5f;
        [SerializeField] private float _explosionDamage = 80f;
        [SerializeField] private float _explosionForce = 600f;
        [SerializeField] private LayerMask _damageLayerMask = ~0;

        private bool _isDestroyed;

        public event Action OnDestroyed;

        private void Awake()
        {
            _currentHealth.value = _maxHealth;
        }

        protected override void OnSpawned()
        {
            if (isServer)
            {
                _currentHealth.value = _maxHealth;
                _isDestroyed = false;
            }
        }

        public DamageResult TakeDamage(DamagePayload payload)
        {
            if (_isDestroyed)
                return new DamageResult { Applied = false };

            if (NetworkManager.main != null && !isServer)
            {
                RpcRequestTakeDamage(payload);
                return new DamageResult
                {
                    Applied = true,
                    RawDamage = payload.Amount,
                    EffectiveDamage = payload.Amount
                };
            }

            return ProcessDamageAuthoritative(payload);
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestTakeDamage(DamagePayload payload, RPCInfo info = default)
        {
            if (!payload.AttackerId.HasValue)
            {
                payload.AttackerId = info.sender;
            }

            ProcessDamageAuthoritative(payload);
        }

        private DamageResult ProcessDamageAuthoritative(DamagePayload payload)
        {
            if (_isDestroyed)
                return new DamageResult { Applied = false };

            _currentHealth.value = Mathf.Max(0f, _currentHealth.value - payload.Amount);
            bool isFatal = _currentHealth.value <= 0.001f;

            DamageResult result = new DamageResult
            {
                Applied = true,
                RawDamage = payload.Amount,
                EffectiveDamage = payload.Amount,
                IsFatal = isFatal,
                HitPoint = payload.HitPoint,
                HitNormal = payload.HitNormal,
                Location = payload.Location,
                AttackerId = payload.AttackerId,
                RemainingHealth = _currentHealth.value
            };

            if (isFatal)
            {
                _isDestroyed = true;

                // Server executes explosion AoE damage
                if (_isExplosive)
                {
                    ExecuteExplosionAoE(payload.AttackerId);
                }

                RpcHandleDestruction(transform.position);
            }

            return result;
        }

        private void ExecuteExplosionAoE(PlayerID? attackerId)
        {
            Vector3 origin = transform.position;
            Collider[] colliders = Physics.OverlapSphere(origin, _explosionRadius, _damageLayerMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider coll = colliders[i];
                if (coll.gameObject == gameObject) continue;

                // Check distance falloff
                float distance = Vector3.Distance(origin, coll.transform.position);
                float falloff = Mathf.Clamp01(1f - (distance / _explosionRadius));
                float calculatedDamage = _explosionDamage * falloff;

                if (coll.TryGetComponent<IDamageable>(out var target) || (coll.transform.parent && coll.transform.parent.TryGetComponent(out target)))
                {
                    Vector3 direction = (coll.transform.position - origin).normalized;
                    DamagePayload aoePayload = new DamagePayload(
                        calculatedDamage,
                        DamageType.Explosive,
                        HitLocation.Core,
                        coll.bounds.center,
                        -direction,
                        direction,
                        attackerId
                    );

                    target.TakeDamage(aoePayload);
                }

                // Physics force
                if (coll.attachedRigidbody && !coll.attachedRigidbody.isKinematic)
                {
                    coll.attachedRigidbody.AddExplosionForce(_explosionForce, origin, _explosionRadius, 1f, ForceMode.Impulse);
                }
            }
        }

        [ObserversRpc(runLocally: true)]
        private void RpcHandleDestruction(Vector3 position)
        {
            _isDestroyed = true;
            OnDestroyed?.Invoke();

            if (_debrisPrefab)
            {
                Instantiate(_debrisPrefab, position, transform.rotation);
            }

            if (_explosionVfxPrefab)
            {
                Instantiate(_explosionVfxPrefab, position, Quaternion.identity);
            }

            if (_destructionAudio)
            {
                AudioSource.PlayClipAtPoint(_destructionAudio, position);
            }

            // Disable renderers and colliders
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = false;

            foreach (var c in GetComponentsInChildren<Collider>())
                c.enabled = false;

            // Destroy object on server/host
            if (NetworkManager.main == null || isServer)
            {
                Destroy(gameObject, 0.2f);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_isExplosive)
            {
                Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, _explosionRadius);
            }
        }
#endif
    }
}
