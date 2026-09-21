using System;
using System.Collections.Generic;
using UnityEngine;
using UnityPhysics = UnityEngine.Physics;

namespace Project.Core.Match
{
    public enum SpawnPointStatus
    {
        Valid,
        Disabled,
        Obstructed,
        Floating,
        SteepSlope,
        Underground
    }

    public struct ValidationInfo
    {
        public SpawnPointStatus Status;
        public string Description;
        public Collider[] OverlappingColliders;
        public RaycastHit GroundHit;
        public bool HasGroundHit;
        public bool IsValid => Status == SpawnPointStatus.Valid;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class SpawnPoint : MonoBehaviour
    {
        [Header("Status")]
        [SerializeField] private bool _isEnabled = true;
        [SerializeField] private bool _autoDisableIfObstructed = false;

        [Header("Capsule Dimensions")]
        [Tooltip("Capsule radius matching Player collider.")]
        [SerializeField] private float _capsuleRadius = 0.5f;

        [Tooltip("Capsule height matching Player collider.")]
        [SerializeField] private float _capsuleHeight = 2.0f;

        [Tooltip("Center offset matching Player collider.")]
        [SerializeField] private Vector3 _capsuleCenter = new Vector3(0f, 1f, 0f);

        [Header("Validation Layers")]
        [Tooltip("Layers considered solid obstacles that block spawning.")]
        [SerializeField] private LayerMask _obstacleLayers = 1; // Default layer

        [Tooltip("Layers considered walkable ground.")]
        [SerializeField] private LayerMask _groundLayers = 1; // Default layer

        [Header("Ground Alignment")]
        [Tooltip("Maximum distance to raycast downward to detect ground.")]
        [SerializeField] private float _groundRaycastDistance = 2.0f;

        [Tooltip("Maximum allowed vertical gap between spawn point pivot and ground before considered floating.")]
        [SerializeField] private float _maxGroundDistance = 0.15f;

        [Tooltip("Maximum walkable slope angle in degrees.")]
        [SerializeField] private float _maxSlopeAngle = 45f;

        private static readonly List<SpawnPoint> s_ActiveSpawnPoints = new List<SpawnPoint>();
        private static bool s_ProviderRegistered = false;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public bool AutoDisableIfObstructed
        {
            get => _autoDisableIfObstructed;
            set => _autoDisableIfObstructed = value;
        }

        public float CapsuleRadius => _capsuleRadius;
        public float CapsuleHeight => _capsuleHeight;
        public Vector3 CapsuleCenter => _capsuleCenter;
        public LayerMask ObstacleLayers => _obstacleLayers;
        public LayerMask GroundLayers => _groundLayers;
        public float GroundRaycastDistance => _groundRaycastDistance;
        public float MaxGroundDistance => _maxGroundDistance;
        public float MaxSlopeAngle => _maxSlopeAngle;

        public static IReadOnlyList<SpawnPoint> ActiveSpawnPoints => s_ActiveSpawnPoints;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_ActiveSpawnPoints.Clear();
            s_ProviderRegistered = false;
        }

        private void OnEnable()
        {
            if (!s_ActiveSpawnPoints.Contains(this))
            {
                s_ActiveSpawnPoints.Add(this);
            }
        }

        private void OnDisable()
        {
            s_ActiveSpawnPoints.Remove(this);
        }

        private void Awake()
        {
            if (_autoDisableIfObstructed)
            {
                var validation = Validate(true);
                if (!validation.IsValid)
                {
                    _isEnabled = false;
                }
            }

            RegisterProviderIfNeeded();
        }

        private void RegisterProviderIfNeeded()
        {
            if (s_ProviderRegistered)
                return;

            var spawner = FindAnyObjectByType<PurrNet.PlayerSpawner>();
            if (spawner)
            {
                spawner.SetRespawnPointProvider(new ValidatedSpawnPointProvider(spawner));
                s_ProviderRegistered = true;
            }
        }

        /// <summary>
        /// Validates this spawn point for obstacle clipping and ground alignment.
        /// </summary>
        public ValidationInfo Validate(bool checkOverlap = true)
        {
            var info = new ValidationInfo();

            if (!_isEnabled || !gameObject.activeInHierarchy)
            {
                info.Status = SpawnPointStatus.Disabled;
                info.Description = "Spawn point is disabled.";
                return info;
            }

            // 1. Ground contact check
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            float maxRayDist = _groundRaycastDistance + 0.5f;

            if (UnityPhysics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, maxRayDist, _groundLayers, QueryTriggerInteraction.Ignore))
            {
                info.GroundHit = groundHit;
                info.HasGroundHit = true;

                float groundDelta = transform.position.y - groundHit.point.y;

                if (groundDelta < -0.05f)
                {
                    info.Status = SpawnPointStatus.Underground;
                    info.Description = $"Spawn point is underground ({-groundDelta:F2}m below ground).";
                    return info;
                }

                if (groundDelta > _maxGroundDistance)
                {
                    info.Status = SpawnPointStatus.Floating;
                    info.Description = $"Spawn point is floating ({groundDelta:F2}m above ground).";
                    return info;
                }

                float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
                if (slopeAngle > _maxSlopeAngle)
                {
                    info.Status = SpawnPointStatus.SteepSlope;
                    info.Description = $"Ground slope ({slopeAngle:F1} deg) exceeds max walkable slope ({_maxSlopeAngle:F1} deg).";
                    return info;
                }
            }
            else
            {
                info.Status = SpawnPointStatus.Floating;
                info.Description = "No ground detected beneath spawn point.";
                return info;
            }

            // 2. Obstacle Overlap Check
            if (checkOverlap)
            {
                float skinOffset = 0.04f;
                float halfHeightMinusRadius = Mathf.Max(0.01f, (_capsuleHeight * 0.5f) - _capsuleRadius);
                Vector3 worldCenter = transform.TransformPoint(_capsuleCenter);
                Vector3 bottomSphere = worldCenter - Vector3.up * halfHeightMinusRadius + Vector3.up * skinOffset;
                Vector3 topSphere = worldCenter + Vector3.up * halfHeightMinusRadius;

                Collider[] hits = UnityPhysics.OverlapCapsule(
                    bottomSphere,
                    topSphere,
                    _capsuleRadius * 0.98f,
                    _obstacleLayers,
                    QueryTriggerInteraction.Ignore);

                List<Collider> validHits = null;
                if (hits is { Length: > 0 })
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hitCol = hits[i];
                        if (hitCol.transform == transform || hitCol.transform.IsChildOf(transform))
                            continue;

                        validHits ??= new List<Collider>();
                        validHits.Add(hitCol);
                    }
                }

                if (validHits != null && validHits.Count > 0)
                {
                    info.Status = SpawnPointStatus.Obstructed;
                    info.OverlappingColliders = validHits.ToArray();

                    string[] names = new string[validHits.Count];
                    for (int i = 0; i < validHits.Count; i++)
                    {
                        names[i] = validHits[i].name;
                    }

                    info.Description = $"Obstructed by: {string.Join(", ", names)}";
                    return info;
                }
            }

            info.Status = SpawnPointStatus.Valid;
            info.Description = "Spawn point is clear and grounded.";
            return info;
        }

        /// <summary>
        /// Raycasts downwards to find ground level and calculates ground snap position.
        /// </summary>
        public bool TryGetGroundSnapPosition(out Vector3 snappedPosition)
        {
            snappedPosition = transform.position;
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            float maxDist = _groundRaycastDistance + 2.0f;

            if (UnityPhysics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDist, _groundLayers, QueryTriggerInteraction.Ignore))
            {
                snappedPosition = hit.point;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawSpawnPointGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawSpawnPointGizmo(true);
        }

        private void DrawSpawnPointGizmo(bool selected)
        {
            var info = Validate(true);
            Color mainColor;

            switch (info.Status)
            {
                case SpawnPointStatus.Valid:
                    mainColor = new Color(0.13f, 0.77f, 0.36f, selected ? 0.95f : 0.75f);
                    break;
                case SpawnPointStatus.Obstructed:
                    mainColor = new Color(0.94f, 0.27f, 0.27f, selected ? 0.95f : 0.85f);
                    break;
                case SpawnPointStatus.Floating:
                    mainColor = new Color(0.96f, 0.62f, 0.07f, selected ? 0.95f : 0.85f);
                    break;
                case SpawnPointStatus.SteepSlope:
                case SpawnPointStatus.Underground:
                    mainColor = new Color(0.98f, 0.45f, 0.09f, selected ? 0.95f : 0.85f);
                    break;
                case SpawnPointStatus.Disabled:
                default:
                    mainColor = new Color(0.45f, 0.50f, 0.58f, selected ? 0.55f : 0.35f);
                    break;
            }

            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 rgt = transform.right;
            Vector3 up = Vector3.up;

            // Base footprint disc
            UnityEditor.Handles.color = mainColor;
            UnityEditor.Handles.DrawWireDisc(pos, up, _capsuleRadius);
            UnityEditor.Handles.color = new Color(mainColor.r, mainColor.g, mainColor.b, selected ? 0.22f : 0.10f);
            UnityEditor.Handles.DrawSolidDisc(pos, up, _capsuleRadius);

            // Capsule bounds
            float halfHeightMinusRadius = Mathf.Max(0.01f, (_capsuleHeight * 0.5f) - _capsuleRadius);
            Vector3 worldCenter = transform.TransformPoint(_capsuleCenter);
            Vector3 bottomSphere = worldCenter - up * halfHeightMinusRadius;
            Vector3 topSphere = worldCenter + up * halfHeightMinusRadius;

            UnityEditor.Handles.color = mainColor;

            // Bottom sphere wire disc and hemisphere arcs
            UnityEditor.Handles.DrawWireDisc(bottomSphere, up, _capsuleRadius);
            UnityEditor.Handles.DrawWireArc(bottomSphere, transform.right, transform.forward, 180f, _capsuleRadius);
            UnityEditor.Handles.DrawWireArc(bottomSphere, -transform.forward, transform.right, 180f, _capsuleRadius);

            // Top sphere wire disc and hemisphere arcs
            UnityEditor.Handles.DrawWireDisc(topSphere, up, _capsuleRadius);
            UnityEditor.Handles.DrawWireArc(topSphere, -transform.right, transform.forward, 180f, _capsuleRadius);
            UnityEditor.Handles.DrawWireArc(topSphere, transform.forward, transform.right, 180f, _capsuleRadius);

            // 4 vertical cylinder lines
            UnityEditor.Handles.DrawLine(bottomSphere + fwd * _capsuleRadius, topSphere + fwd * _capsuleRadius);
            UnityEditor.Handles.DrawLine(bottomSphere - fwd * _capsuleRadius, topSphere - fwd * _capsuleRadius);
            UnityEditor.Handles.DrawLine(bottomSphere + rgt * _capsuleRadius, topSphere + rgt * _capsuleRadius);
            UnityEditor.Handles.DrawLine(bottomSphere - rgt * _capsuleRadius, topSphere - rgt * _capsuleRadius);

            // Direction sightline arrow at eye level (1.6m)
            Vector3 eyePos = pos + up * 1.6f;
            Vector3 arrowTip = eyePos + fwd * 0.9f;
            UnityEditor.Handles.color = mainColor;
            UnityEditor.Handles.DrawLine(eyePos, arrowTip);

            Vector3 arrowBack = arrowTip - fwd * 0.25f;
            UnityEditor.Handles.DrawLine(arrowTip, arrowBack + rgt * 0.12f);
            UnityEditor.Handles.DrawLine(arrowTip, arrowBack - rgt * 0.12f);
            UnityEditor.Handles.DrawLine(arrowBack + rgt * 0.12f, arrowBack - rgt * 0.12f);

            // Floating ground connection indicator
            if (info.Status == SpawnPointStatus.Floating)
            {
                UnityEditor.Handles.color = new Color(0.96f, 0.62f, 0.07f, 0.9f);
                if (info.HasGroundHit)
                {
                    UnityEditor.Handles.DrawDottedLine(pos, info.GroundHit.point, 3f);
                    UnityEditor.Handles.DrawWireDisc(info.GroundHit.point, info.GroundHit.normal, 0.25f);
                }
                else
                {
                    UnityEditor.Handles.DrawDottedLine(pos, pos - up * _groundRaycastDistance, 3f);
                }
            }

            // Obstacle overlap indicator
            if (info is { Status: SpawnPointStatus.Obstructed, OverlappingColliders: not null })
            {
                UnityEditor.Handles.color = new Color(0.94f, 0.27f, 0.27f, 0.9f);
                for (int i = 0; i < info.OverlappingColliders.Length; i++)
                {
                    var col = info.OverlappingColliders[i];
                    if (col)
                    {
                        UnityEditor.Handles.DrawLine(worldCenter, col.bounds.center);
                        UnityEditor.Handles.DrawWireCube(col.bounds.center, col.bounds.size);
                    }
                }
            }
            
            var labelStyle = new GUIStyle
            {
                normal = { textColor = mainColor },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            //string labelText = $"{gameObject.name}\n[{info.Status}] {info.Description}";
            string labelText = $"{gameObject.name}";
            UnityEditor.Handles.Label(topSphere + (up * 1f), labelText, labelStyle);

            // Overhead label when selected
            if (selected)
            {
                
            }
        }
#endif
    }

    /// <summary>
    /// Runtime provider that hooks into PurrNet.PlayerSpawner to supply only active, enabled,
    /// and validated spawn points without modifying PurrNet core classes.
    /// </summary>
    public class ValidatedSpawnPointProvider : PurrNet.IProvideSpawnPoints
    {
        private readonly PurrNet.PlayerSpawner _spawner;
        private int _currentIndex = 0;

        public ValidatedSpawnPointProvider(PurrNet.PlayerSpawner spawner)
        {
            _spawner = spawner;
        }

        public PurrNet.SpawnPoint NextSpawnPoint(PurrNet.PlayerID player, PurrNet.SceneID scene)
        {
            var active = SpawnPoint.ActiveSpawnPoints;
            var candidates = new List<SpawnPoint>();

            for (int i = 0; i < active.Count; i++)
            {
                var pt = active[i];
                if (pt && pt.IsEnabled && pt.gameObject.activeInHierarchy)
                {
                    var validation = pt.Validate(true);
                    if (validation.IsValid)
                    {
                        candidates.Add(pt);
                    }
                }
            }

            Transform target = null;

            if (candidates.Count > 0)
            {
                target = candidates[_currentIndex % candidates.Count].transform;
                _currentIndex = (_currentIndex + 1) % candidates.Count;
            }
            else
            {
                // Fallback 1: Any active and enabled spawn point in scene
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i] && active[i].IsEnabled && active[i].gameObject.activeInHierarchy)
                    {
                        target = active[i].transform;
                        break;
                    }
                }

                // Fallback 2: Any non-null transform in PlayerSpawner's serialized list
                if (!target && _spawner && _spawner.SpawnPoints is { Count: > 0 })
                {
                    for (int i = 0; i < _spawner.SpawnPoints.Count; i++)
                    {
                        if (_spawner.SpawnPoints[i])
                        {
                            target = _spawner.SpawnPoints[i];
                            break;
                        }
                    }
                }
            }

            if (target)
            {
                return new PurrNet.SpawnPoint
                {
                    position = target.position,
                    rotation = target.rotation
                };
            }

            return new PurrNet.SpawnPoint
            {
                position = Vector3.zero,
                rotation = Quaternion.identity
            };
        }
    }
}
