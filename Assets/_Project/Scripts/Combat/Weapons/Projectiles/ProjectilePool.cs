using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Global or scene-local pool for ballistic projectiles, ensuring zero GC allocations during combat.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        private static ProjectilePool _instance;
        public static ProjectilePool Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindAnyObjectByType<ProjectilePool>();
                    if (!_instance)
                    {
                        var go = new GameObject("[ProjectilePool]");
                        _instance = go.AddComponent<ProjectilePool>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [SerializeField] private ProjectileInstance _defaultPrefab;
        private readonly Queue<ProjectileInstance> _availablePool = new();

        private void Awake()
        {
            if (!_instance) _instance = this;
        }

        public ProjectileInstance Spawn(Vector3 position, Vector3 velocity, DamagePayload payload, LayerMask hitMask, IEnumerable<IProjectileBehavior> behaviors = null, bool isCosmetic = false)
        {
            ProjectileInstance proj;

            if (_availablePool.Count > 0)
            {
                proj = _availablePool.Dequeue();
                proj.IsInPool = false;
                proj.gameObject.SetActive(true);
            }
            else
            {
                if (_defaultPrefab)
                {
                    proj = Instantiate(_defaultPrefab, transform);
                }
                else
                {
                    var go = new GameObject("Projectile");
                    go.transform.SetParent(transform);
                    proj = go.AddComponent<ProjectileInstance>();
                }
                proj.OnDespawn += ReturnToPool;
            }

            proj.Initialize(position, velocity, payload, hitMask, behaviors, isCosmetic);
            return proj;
        }

        public ProjectileInstance SpawnCosmetic(Vector3 position, Vector3 velocity, LayerMask hitMask, IEnumerable<IProjectileBehavior> behaviors = null, DamageType element = DamageType.Physical)
        {
            DamagePayload dummyPayload = new DamagePayload(
                amount: 0f,
                type: element,
                location: HitLocation.Core,
                hitPoint: position,
                hitNormal: Vector3.up,
                impactDirection: velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector3.forward
            );

            return Spawn(position, velocity, dummyPayload, hitMask, behaviors, isCosmetic: true);
        }

        private void ReturnToPool(ProjectileInstance proj)
        {
            if (proj && !proj.IsInPool)
            {
                proj.IsInPool = true;
                _availablePool.Enqueue(proj);
            }
        }
    }
}
