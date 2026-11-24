using System.Collections.Generic;
using UnityEngine;
using UnityPhysics = UnityEngine.Physics;

namespace Project.Match
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
    [SerializeField] private float _capsuleRadius = 0.5f;
    [SerializeField] private float _capsuleHeight = 2.0f;
    [SerializeField] private Vector3 _capsuleCenter = new Vector3(0f, 1f, 0f);

    [Header("Validation Layers")]
    [SerializeField] private LayerMask _obstacleLayers = 1; // Default layer
    [SerializeField] private LayerMask _groundLayers = 1; // Default layer

    [Header("Ground Alignment")] 
    [SerializeField] private float _groundRaycastDistance = 2.0f;
    [SerializeField] private float _maxGroundDistance = 0.15f;
    [SerializeField] private float _maxSlopeAngle = 45f;

    private static readonly List<SpawnPoint> s_ActiveSpawnPoints = new();
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

    public ValidationInfo Validate(bool checkOverlap = true)
    {
        var info = new ValidationInfo();

        if (!_isEnabled || !gameObject.activeInHierarchy)
        {
            info.Status = SpawnPointStatus.Disabled;
            info.Description = "Spawn point is disabled.";
            return info;
        }

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        float maxRayDist = _groundRaycastDistance + 0.5f;

        if (UnityPhysics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, maxRayDist, _groundLayers,
                QueryTriggerInteraction.Ignore))
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
                info.Description =
                    $"Ground slope ({slopeAngle:F1} deg) exceeds max walkable slope ({_maxSlopeAngle:F1} deg).";
                return info;
            }
        }
        else
        {
            info.Status = SpawnPointStatus.Floating;
            info.Description = "No ground detected beneath spawn point.";
            return info;
        }

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

    public bool TryGetGroundSnapPosition(out Vector3 snappedPosition)
    {
        snappedPosition = transform.position;
        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
        float maxDist = _groundRaycastDistance + 2.0f;

        if (UnityPhysics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDist, _groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            snappedPosition = hit.point;
            return true;
        }

        return false;
    }


}

}
