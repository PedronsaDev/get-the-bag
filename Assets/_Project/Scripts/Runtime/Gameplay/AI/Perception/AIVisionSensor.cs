using Project.AI.Blackboard;
using Project.Core.Player;
using PurrNet;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.AI.Perception
{
public class AIVisionSensor : NetworkBehaviour, IAISensoryReceiver
{
    [Header("References")]
    [SerializeField] private AIBlackboard _blackboard;

    [Header("Settings")]
    [SerializeField] private float _visionTickInterval = 0.1f; // 10Hz

    [Header("References")]
    [SerializeField] private AITargetMemory _targetMemory;

    private AIVisionConfig VisionConfig => _blackboard && _blackboard.ArchetypeConfig
        ? _blackboard.ArchetypeConfig.VisionConfig : null;

    public Vector3 SensoryOrigin => transform.position + Vector3.up * (VisionConfig?.EyeHeight ?? 0f);

    private float _tickTimer;
    private float _cosPrimaryFOV;
    private float _cosPeripheralFOV;

    private bool _isPeripheral;
    private AIPerceptionBus _registeredBus;

    protected override void OnSpawned()
    {
        UnregisterReceiver();
        if (!isServer || !_blackboard || !_blackboard.TryInitializeArchetype() || !_targetMemory ||
            !float.IsFinite(_visionTickInterval) || _visionTickInterval <= 0f)
        {
            enabled = false;
            return;
        }

        CacheVisionParameters();
        _tickTimer = Random.Range(0f, _visionTickInterval);
        enabled = true;
        _registeredBus = AIPerceptionBus.Instance;
        if (_registeredBus)
            _registeredBus.RegisterReceiver(this);
    }

    protected override void OnDespawned()
    {
        UnregisterReceiver();
    }

    private void OnDisable() => UnregisterReceiver();

    private void UnregisterReceiver()
    {
        if (_registeredBus)
            _registeredBus.UnregisterReceiver(this);
        _registeredBus = null;
    }

    private void Awake()
    {
        if (!_blackboard)
            _blackboard = GetComponent<AIBlackboard>();

        if (!_targetMemory)
            _targetMemory = GetComponent<AITargetMemory>();

    }

    private void Update()
    {
        if (!isServer || !_blackboard || !_blackboard.IsArchetypeReady || !_targetMemory)
            return;
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0f)
            return;

        _tickTimer = _visionTickInterval;
        EvaluateVision();
    }


    private void EvaluateVision()
    {
        PlayerIdentifier closestPlayer = null;
        Vector3 closestPosition = default;
        float closestDistanceSqr = float.MaxValue;
        bool isPeripheralTarget = false;

        for (int i = 0; i < PlayerIdentifier.ActivePlayers.Count; i++)
        {
            var player = PlayerIdentifier.ActivePlayers[i];
            if (!player || player.IsDead) continue;
            var body = player.BodyTransform;
            if (CanSeeTargetBody(body, player.transform))
            {
                Vector3 position = body.position;
                float distSqr = (position - SensoryOrigin).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestPlayer = player;
                    closestPosition = position;
                    isPeripheralTarget = _isPeripheral;
                }
            }
        }

        if (closestPlayer)
        {
            float distance = Mathf.Sqrt(closestDistanceSqr);
            float maxRange = isPeripheralTarget ? VisionConfig.PeripheralDistance : VisionConfig.ViewDistance;
            _targetMemory.ReportVisualContact(closestPlayer.gameObject, closestPosition, distance, maxRange, isPeripheralTarget, _visionTickInterval);
        }
        else
        {
            _targetMemory.ReportLostSight();
        }
    }

    public bool CanSeeTarget(Transform target)
    {
        if (target && target.TryGetComponent(out PlayerIdentifier player))
            return CanSeeTargetBody(player.BodyTransform, player.transform);

        return CanSeeTargetBody(target, target);
    }

    private bool CanSeeTargetBody(Transform targetBody, Transform targetIdentity)
    {
        _isPeripheral = false;

        if (!isServer || !isActiveAndEnabled || !_blackboard || !_blackboard.IsArchetypeReady ||
            !targetBody || !targetIdentity)
            return false;

        Vector3 eyePosition = SensoryOrigin;

        Vector3 targetEyePosition = targetBody.position + (Vector3.up * 1.5f);
        Vector3 directionToTarget = targetEyePosition - eyePosition;
        float distanceToTarget = directionToTarget.magnitude;
        if (distanceToTarget <= 0.001f)
            return false;

        var config = VisionConfig;
        bool inPrimaryCone = IsInCone(directionToTarget, distanceToTarget, _cosPrimaryFOV, config.ViewDistance);
        bool inPeripheralCone = IsInCone(directionToTarget, distanceToTarget, _cosPeripheralFOV, config.PeripheralDistance);

        if (!inPrimaryCone && !inPeripheralCone)
        {
            return false;
        }

        if (Physics.Raycast(eyePosition, directionToTarget.normalized, out RaycastHit hit, distanceToTarget,
                config.ObstacleMask | config.TargetMask, QueryTriggerInteraction.Ignore))
        {
            if (((1 << hit.transform.gameObject.layer) & config.TargetMask) != 0 &&
                (hit.transform == targetIdentity || hit.transform.IsChildOf(targetIdentity)))
            {
                _isPeripheral = !inPrimaryCone;
                return true;
            }
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
        var config = VisionConfig;
        _cosPrimaryFOV = Mathf.Cos(config.FieldOfView * 0.5f * Mathf.Deg2Rad);
        _cosPeripheralFOV = Mathf.Cos(config.PeripheralFOV * 0.5f * Mathf.Deg2Rad);
    }

    public void ReceiveStimulus(in StimulusPayload stimulus)
    {
        if (!isServer || !isActiveAndEnabled || !_blackboard || !_blackboard.IsArchetypeReady || !_targetMemory)
            return;
        switch (stimulus.Type)
        {
            case StimulusType.Sound:
                if (stimulus.Source == gameObject)
                    return;
                _targetMemory.ReportNoise(stimulus.Position, stimulus.Intensity);
                break;
            case StimulusType.Damage:
                _targetMemory.ReportDamage(stimulus.Source, stimulus.Position);
                break;
        }
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (!_blackboard)
            TryGetComponent(out _blackboard);
        var config = VisionConfig;
        if (config == null)
            return;

        Vector3 eyePosition = SensoryOrigin;
        DrawVisionFan(eyePosition, transform.forward, config.FieldOfView, config.ViewDistance,
            new Color(1f, 0.2f, 0.2f));
        DrawVisionFan(eyePosition, transform.forward, config.PeripheralFOV, config.PeripheralDistance,
            new Color(1f, 0.8f, 0.2f));
    }

    private void DrawVisionFan(Vector3 origin, Vector3 forward, float fov, float distance, Color color)
    {
        if (fov <= 0f || distance <= 0f) return;
        float halfFov = fov * 0.5f;
        Vector3 startDirection = Quaternion.Euler(0, -halfFov, 0) * forward;

        Handles.color = new Color(color.r, color.g, color.b, 0.08f);
        Handles.DrawSolidArc(origin, Vector3.up, startDirection, fov, distance);

        Handles.color = color;
        Handles.DrawWireArc(origin, Vector3.up, startDirection, fov, distance);

        if (fov < 360f)
        {
            Handles.DrawLine(origin, origin + (startDirection * distance));
            Vector3 endDirection = Quaternion.Euler(0, halfFov, 0) * forward;
            Handles.DrawLine(origin, origin + (endDirection * distance));
        }
    }
#endif
}
}
