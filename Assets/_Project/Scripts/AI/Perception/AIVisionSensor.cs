using NaughtyAttributes;
using Project.Core.Player;
using PurrNet;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.AI.Perception
{
public class AIVisionSensor : NetworkBehaviour, IAISensoryReceiver
{
    [Header("Settings")] 
    [Expandable] 
    [SerializeField] private VisionConfig _visionConfig;
    [SerializeField] private float _visionTickInterval = 0.1f; // 10Hz
    
    [Header("References")]
    [SerializeField] private AITargetMemory _targetMemory;

    public Vector3 SensoryOrigin => _visionConfig ? transform.position + (Vector3.up * _visionConfig.EyeHeight) : Vector3.zero;

    private float _tickTimer;
    private float _cosPrimaryFOV;
    private float _cosPeripheralFOV;
    
    private bool _isPeripheral;

    protected override void OnSpawned()
    {
        if (!isServer)
        {
            enabled = false;
            return;
        }
        
        if (AIPerceptionBus.Instance) AIPerceptionBus.Instance.RegisterReceiver(this);
    }

    protected override void OnDespawned()
    {
        if (!isServer)
        {
            return;
        }
        
        if (AIPerceptionBus.Instance) AIPerceptionBus.Instance.UnregisterReceiver(this);
    }

    private void Awake()
    {
        if (!_targetMemory) _targetMemory = GetComponent<AITargetMemory>();
        _tickTimer = Random.Range(0f, _visionTickInterval);
        CacheVisionParameters();
    }

    private void Update()
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0f)
            return;

        _tickTimer = _visionTickInterval;
        EvaluateVision();
    }


    private void EvaluateVision()
    {
        PlayerIdentifier closestPlayer = null;
        float closestDistanceSqr = float.MaxValue;
        bool isPeripheralTarget = false;
        
        for (int i = 0; i < PlayerIdentifier.ActivePlayers.Count; i++)
        {
            var player = PlayerIdentifier.ActivePlayers[i];
            if (!player || player.IsDead) continue;
            if (CanSeeTarget(player.transform))
            {
                float distSqr = (player.transform.position - SensoryOrigin).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestPlayer = player;
                    isPeripheralTarget = _isPeripheral;
                }
            }
        }
        
        if (closestPlayer)
        {
            float distance = Mathf.Sqrt(closestDistanceSqr);
            float maxRange = isPeripheralTarget ? _visionConfig.PeripheralDistance : _visionConfig.ViewDistance;
            _targetMemory.ReportVisualContact(closestPlayer.gameObject, closestPlayer.transform.position, distance, maxRange, isPeripheralTarget, _visionTickInterval);
        }
        else
        {
            _targetMemory.ReportLostSight();
        }
    }

    public bool CanSeeTarget(Transform target)
    {
        _isPeripheral = false;
        
        if (!target)
            return false;

        Vector3 eyePosition = SensoryOrigin;

        Vector3 targetEyePosition = target.position + (Vector3.up * 1.5f);
        Vector3 directionToTarget = targetEyePosition - eyePosition;
        float distanceToTarget = directionToTarget.magnitude;

        bool inPrimaryCone = IsInCone(directionToTarget, distanceToTarget, _cosPrimaryFOV, _visionConfig.ViewDistance);
        bool inPeripheralCone = IsInCone(directionToTarget, distanceToTarget, _cosPeripheralFOV, _visionConfig.PeripheralDistance);

        if (!inPrimaryCone && !inPeripheralCone)
        {
            return false;
        }

        if (Physics.Raycast(eyePosition, directionToTarget.normalized, out RaycastHit hit, distanceToTarget,
                _visionConfig.ObstacleMask | _visionConfig.TargetMask))
        {
            if (((1 << hit.transform.gameObject.layer) & _visionConfig.TargetMask) != 0)
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
        if (!_visionConfig)
            return;
        
        _cosPrimaryFOV = Mathf.Cos((_visionConfig.FieldOfView * 0.5f) * Mathf.Deg2Rad);
        _cosPeripheralFOV = Mathf.Cos((_visionConfig.PeripheralFOV * 0.5f) * Mathf.Deg2Rad);
    }

    public void ReceiveStimulus(in StimulusPayload stimulus)
    {
        switch (stimulus.Type)
        {
            case StimulusType.Sound:
                _targetMemory.ReportNoise(stimulus.Position, stimulus.Intensity);
                break;
            case StimulusType.Damage:
                _targetMemory.ReportDamage(stimulus.Source, stimulus.Position);
                break;
        }
    }

#if UNITY_EDITOR

    private void OnValidate() => CacheVisionParameters();

    private void OnDrawGizmosSelected()
    {
        if (!_visionConfig)
        {
            var labelStyle = new GUIStyle
            {
                normal = { textColor = Color.red },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            
            string labelText = "MISSING_VISION_CONFIGURATION";
            Handles.Label(this.transform.position, labelText, labelStyle);
            
            return;
        }
        
        Vector3 eyePosition = SensoryOrigin;
        DrawVisionFan(eyePosition, transform.forward, _visionConfig.FieldOfView, _visionConfig.ViewDistance,
            new Color(1f, 0.2f, 0.2f));
        DrawVisionFan(eyePosition, transform.forward, _visionConfig.PeripheralFOV, _visionConfig.PeripheralDistance,
            new Color(1f, 0.8f, 0.2f));
    }

    private void DrawVisionFan(Vector3 origin, Vector3 forward, float fov, float distance, Color color)
    {
        if (fov <= 0f || distance <= 0f) return;
        float halfFov = fov * 0.5f;
        Vector3 startDirection = Quaternion.Euler(0, -halfFov, 0) * forward;
        // 1. Semi-transparent filled arc (10% alpha)
        Handles.color = new Color(color.r, color.g, color.b, 0.08f);
        Handles.DrawSolidArc(origin, Vector3.up, startDirection, fov, distance);
        // 2. Solid outline arc
        Handles.color = color;
        Handles.DrawWireArc(origin, Vector3.up, startDirection, fov, distance);
        // 3. Boundary lines (skip if 360 full circle)
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