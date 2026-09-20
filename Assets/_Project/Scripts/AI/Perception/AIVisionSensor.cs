using Project.Core.Player;
using UnityEngine;

namespace Project.AI.Perception
{
public class AIVisionSensor : MonoBehaviour, IAISensoryReceiver
{
    [Header("Settings")] 
    [SerializeField] private VisionConfig _visionConfig;
    [SerializeField] private float _visionTickInterval = 0.1f; // 10Hz
    
    public Vector3 SensoryOrigin => transform.position + (Vector3.up * _visionConfig.EyeHeight);

    private float _tickTimer;
    private float _cosPrimaryFOV;
    private float _cosPeripheralFOV;

    private void OnEnable()
    {
        if (AIPerceptionBus.Instance) AIPerceptionBus.Instance.RegisterReceiver(this);
    }

    private void OnDisable()
    {
        if (AIPerceptionBus.Instance) AIPerceptionBus.Instance.UnregisterReceiver(this);
    }

    private void Awake()
    {
        _tickTimer = Random.Range(0f, _visionTickInterval);
        CacheVisionParameters();
    }

    private void Update()
    {
        _tickTimer -= Time.deltaTime;
        if (!(_tickTimer <= 0f))
            return;

        _tickTimer = _visionTickInterval;
        EvaluateVision();
    }


    private void EvaluateVision()
    {
        foreach (var player in PlayerIdentifier.ActivePlayers)
        {
            if (!player || player.IsDead)
                continue;

            if (!CanSeeTarget(player.transform))
                continue;

            var stimulus = new StimulusPayload(StimulusType.Sight, player.transform.position, player.gameObject, 1);
            ReceiveStimulus(stimulus);
        }
    }

    public bool CanSeeTarget(Transform target)
    {
        if (!target)
            return false;

        Vector3 eyePosition = SensoryOrigin;

        Vector3 targetEyePosition = target.position + (Vector3.up * 1.5f);
        Vector3 directionToTarget = targetEyePosition - eyePosition;
        float distanceToTarget = directionToTarget.magnitude;

        bool inPrimaryCone = IsInCone(directionToTarget, distanceToTarget, _cosPrimaryFOV, _visionConfig.ViewDistance);
        bool inPeripheralCone = IsInCone(directionToTarget, distanceToTarget, _cosPeripheralFOV,
            _visionConfig.PeripheralDistance);

        if (!inPrimaryCone && !inPeripheralCone)
        {
            return false;
        }

        if (Physics.Raycast(eyePosition, directionToTarget.normalized, out RaycastHit hit, distanceToTarget, _visionConfig.ObstacleMask | _visionConfig.TargetMask))
        {
            if (((1 << hit.transform.gameObject.layer) & _visionConfig.TargetMask) != 0)
                return true;
        }

        return false;
    }

    private bool IsInCone(Vector3 directionToTarget, float distanceToTarget, float cosFOV, float maxDistance)
    {
        if (distanceToTarget > maxDistance)
            return false;

        Vector3 normalizedDirection = directionToTarget / distanceToTarget;

        float dotProduct = Vector3.Dot(transform.forward, normalizedDirection);

        return dotProduct >= cosFOV;
    }

    private void CacheVisionParameters()
    {
        _cosPrimaryFOV = Mathf.Cos((_visionConfig.FieldOfView * 0.5f) * Mathf.Deg2Rad);
        _cosPeripheralFOV = Mathf.Cos((_visionConfig.PeripheralFOV * 0.5f) * Mathf.Deg2Rad);
    }
    
    public void ReceiveStimulus(in StimulusPayload stimulus)
    {
    }

#if UNITY_EDITOR
    
    private void OnValidate()
    {
        CacheVisionParameters();
    }
    
    private void OnDrawGizmosSelected()
    {
        Vector3 eyePosition = transform.position + (Vector3.up * _visionConfig.EyeHeight);
        Gizmos.color = Color.red;
        DrawConeWireframe(eyePosition, transform.forward, _visionConfig.FieldOfView, _visionConfig.ViewDistance);

        Gizmos.color = Color.yellow;
        DrawConeWireframe(eyePosition, transform.forward, _visionConfig.PeripheralFOV,
            _visionConfig.PeripheralDistance);
    }

    private void DrawConeWireframe(Vector3 origin, Vector3 forward, float fov, float distance)
    {
        Vector3 leftRayDirection = Quaternion.Euler(0, -fov * 0.5f, 0) * forward;
        Vector3 rightRayDirection = Quaternion.Euler(0, fov * 0.5f, 0) * forward;

        Gizmos.DrawRay(origin, leftRayDirection * distance);
        Gizmos.DrawRay(origin, rightRayDirection * distance);
        Gizmos.DrawLine(origin + leftRayDirection * distance, origin + rightRayDirection * distance);
    }
#endif
}
}