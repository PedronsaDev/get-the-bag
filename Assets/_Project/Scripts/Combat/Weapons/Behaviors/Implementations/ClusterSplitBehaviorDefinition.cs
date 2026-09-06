using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    public enum SplitTriggerType
    {
        OnImpact,
        AtDistance,
        OnBounce
    }

    [CreateAssetMenu(fileName = "Beh_ClusterSplit", menuName = "Combat/Behaviors/Cluster Split")]
    public class ClusterSplitBehaviorDefinition : ProjectileBehaviorDefinition
    {
        [Header("Split Trigger")]
        [SerializeField] private SplitTriggerType _trigger = SplitTriggerType.OnImpact;
        [Tooltip("Used when SplitTriggerType is AtDistance.")]
        [SerializeField] private float _splitDistance = 6.0f;

        [Header("Pellet Count & Spread")]
        [Range(2, 12)] [SerializeField] private int _childCount = 4;
        [Range(10f, 90f)] [SerializeField] private float _coneAngle = 45f;
        [SerializeField] private float _childSpeed = 22f;
        [Range(0.1f, 1f)] [SerializeField] private float _childDamageRatio = 0.4f;

        public SplitTriggerType Trigger => _trigger;
        public float SplitDistance => _splitDistance;
        public int ChildCount => _childCount;
        public float ConeAngle => _coneAngle;
        public float ChildSpeed => _childSpeed;
        public float ChildDamageRatio => _childDamageRatio;

        public override IProjectileBehavior CreateRuntimeInstance()
        {
            return new ClusterSplitRuntimeRunner(this, isChild: false);
        }

        private class ClusterSplitRuntimeRunner : IProjectileBehavior
        {
            private readonly ClusterSplitBehaviorDefinition _config;
            private readonly bool _isChild;
            private bool _hasSplit;

            public ClusterSplitRuntimeRunner(ClusterSplitBehaviorDefinition config, bool isChild)
            {
                _config = config;
                _isChild = isChild;
            }

            public void OnSpawn(ProjectileInstance projectile)
            {
                _hasSplit = false;
            }

            public void OnTravel(ProjectileInstance projectile, float deltaTime)
            {
                if (_isChild || _hasSplit) return;

                if (_config._trigger == SplitTriggerType.AtDistance && projectile.DistanceTraveled >= _config._splitDistance)
                {
                    _hasSplit = true;
                    SpawnChildren(projectile, projectile.Velocity.normalized, projectile.Position);
                }
            }

            public bool OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload)
            {
                if (_isChild || _hasSplit) return false;

                if (_config._trigger == SplitTriggerType.OnImpact)
                {
                    _hasSplit = true;
                    SpawnChildren(projectile, hit.normal, hit.point + (hit.normal * 0.1f));
                }

                return false;
            }

            private void SpawnChildren(ProjectileInstance projectile, Vector3 baseDirection, Vector3 spawnPoint)
            {
                if (ProjectilePool.Instance == null) return;

                DamagePayload childPayload = projectile.Payload;
                childPayload.Amount *= _config._childDamageRatio;

                for (int i = 0; i < _config._childCount; i++)
                {
                    float angle = (360f / _config._childCount) * i;
                    Quaternion rot = Quaternion.AngleAxis(angle, baseDirection) * Quaternion.AngleAxis(_config._coneAngle * 0.5f, Vector3.up);
                    Vector3 childDir = rot * baseDirection;
                    Vector3 childVel = childDir * _config._childSpeed;

                    ProjectilePool.Instance.Spawn(spawnPoint, childVel, childPayload, projectile.HitMask, new[] { new ClusterSplitRuntimeRunner(_config, isChild: true) });
                }
            }
        }
    }
}
