using System;
using Project.Combat;
using Project.Core.UI;
using PurrNet;
using UnityEngine;

namespace Project.Player.Detection
{
    public class PlayerTargetDetector : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerFacade _playerFacade;
        [SerializeField] private FPSCameraController _cameraController;

        [Header("Detection Settings")]
        [SerializeField] private float _detectionRange = 80f;
        [Range(1f, 25f)] [SerializeField] private float _crosshairAngle = 8f;
        [SerializeField] private float _assistRadius = 1.5f;
        [SerializeField] private LayerMask _detectableLayers;
        [SerializeField] private LayerMask _occlusionLayers = 1 << 0; // Default environment geometry

        public Transform AimTransform => _cameraController ? _cameraController.PitchPivot : transform;

        public event Action<IDamageable, bool> OnTargetChanged; // (Target, isDirectSight)

        private IDamageable _currentTarget;
        public IDamageable CurrentTarget => _currentTarget;

        private readonly Collider[] _overlapBuffer = new Collider[32];

        private void Awake()
        {
            if (_detectableLayers.value == 0)
            {
                int damageableLayer = LayerMask.NameToLayer("Damageable");
                if (damageableLayer >= 0)
                    _detectableLayers = 1 << damageableLayer;
            }

            if (_occlusionLayers.value == 0)
            {
                _occlusionLayers = 1 << 0; // Default layer
            }

            // Sanitize inspector values if legacy bloated radius was saved
            if (_assistRadius > 10f)
                _assistRadius = 1.5f;

            if (!_playerFacade)
                _playerFacade = GetComponentInParent<PlayerFacade>();

            if (!_cameraController)
                _cameraController = GetComponentInChildren<FPSCameraController>();
        }

        private void Update()
        {
            if (!isOwner)
                return;

            if (UIBlockerSystem.IsActionsBlocked)
            {
                if (_currentTarget != null)
                {
                    _currentTarget = null;
                    OnTargetChanged?.Invoke(null, false);
                }
                return;
            }

            UpdateTargetDetection();
        }

        private void UpdateTargetDetection()
        {
            var aim = AimTransform;
            var origin = aim.position;
            var forward = aim.forward;

            IDamageable bestTarget = null;
            bool isDirectSight = false;

            LayerMask combinedMask = _detectableLayers | _occlusionLayers;
            float maxRange = _detectionRange;

            // 1. Direct Center Raycast (highest priority - dead on crosshair)
            if (Physics.Raycast(origin, forward, out RaycastHit hit, _detectionRange, combinedMask, QueryTriggerInteraction.Ignore))
            {
                // If we hit an obstacle/wall, cap search distance so proximity search won't see through that wall
                if ((_occlusionLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    maxRange = hit.distance;
                }
                else
                {
                    var candidate = hit.collider.GetComponentInParent<IDamageable>();
                    if (candidate != null && !IsTargetDead(candidate))
                    {
                        bestTarget = candidate;
                        isDirectSight = true;
                    }
                }
            }

            // 2. Near Crosshair Cone Scan (if crosshair isn't directly on a living target)
            if (bestTarget == null && maxRange > 0.5f)
            {
                int count = Physics.OverlapSphereNonAlloc(origin, maxRange, _overlapBuffer, _detectableLayers, QueryTriggerInteraction.Ignore);
                float bestAngle = _crosshairAngle;

                for (int i = 0; i < count; i++)
                {
                    var col = _overlapBuffer[i];
                    _overlapBuffer[i] = null; // Clear reference
                    if (!col) continue;

                    Vector3 targetCenter = col.bounds.center;
                    Vector3 toTarget = targetCenter - origin;
                    float dist = toTarget.magnitude;

                    // Out of range or behind camera
                    if (dist > maxRange || dist < 0.2f || Vector3.Dot(forward, toTarget) <= 0f) continue;

                    Vector3 dirToTarget = toTarget / dist;
                    float angle = Vector3.Angle(forward, dirToTarget);

                    // Check if inside crosshair cone
                    if (angle < bestAngle)
                    {
                        // Check line of sight (ensure walls/obstacles don't block visibility)
                        if (!Physics.Linecast(origin, targetCenter, _occlusionLayers, QueryTriggerInteraction.Ignore))
                        {
                            var candidate = col.GetComponentInParent<IDamageable>();
                            if (candidate != null && !IsTargetDead(candidate))
                            {
                                bestAngle = angle;
                                bestTarget = candidate;
                                isDirectSight = false;
                            }
                        }
                    }
                }
            }

            if (bestTarget != _currentTarget)
            {
                _currentTarget = bestTarget;
                OnTargetChanged?.Invoke(_currentTarget, isDirectSight);
            }
        }

        private static bool IsTargetDead(IDamageable damageable)
        {
            if (damageable == null) return true;
            if (damageable is Hitbox hb) return hb.TargetHealth != null && hb.TargetHealth.IsDead;
            if (damageable is HealthComponent hc) return hc.IsDead;
            if (damageable is Component comp)
            {
                var health = comp.GetComponentInParent<HealthComponent>();
                if (health != null) return health.IsDead;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!AimTransform) return;
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.35f);
            Gizmos.matrix = Matrix4x4.TRS(AimTransform.position, AimTransform.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, _crosshairAngle * 2f, Mathf.Min(_detectionRange, 30f), 0.5f, 1.0f);
        }
#endif
    }
}