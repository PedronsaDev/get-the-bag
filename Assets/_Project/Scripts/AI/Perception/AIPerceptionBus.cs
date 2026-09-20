using System.Collections.Generic;
using Project.Logging;
using UnityEngine;

namespace Project.AI.Perception
{

public class AIPerceptionBus : MonoBehaviour
{
    public static AIPerceptionBus Instance;
    
    [Header("Settings")] 
    [SerializeField] private LayerMask _occlusionMask;
    [Range(0f, 1f)]
    [SerializeField] private float _wallAttenuation = 0.25f; // 75% volume reduction through walls
    [SerializeField] private float _minimumAudibleThreshold = 1.0f; // Below 1m radius, sound dies
    
    private readonly List<IAISensoryReceiver> _receivers = new(64);

    private readonly RaycastHit[] _rayBuffer = new RaycastHit[1];
    
    private void Awake()
    {
        if (Instance)
        {
            AppLog.LogWarning(LogChannel.AI, "More than one AIPerceptionBus found. destroying duplicate.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) 
            Instance = null;
    }
    
    public void RegisterReceiver(IAISensoryReceiver receiver)
    {
        if (!_receivers.Contains(receiver))
            _receivers.Add(receiver);
    }

    public void UnregisterReceiver(IAISensoryReceiver receiver)
    {
        _receivers.Remove(receiver);
    }
    
    public static void Emit(in StimulusPayload stimulus)
    {
        if (Instance) Instance.EmitStimulus(stimulus);
    }

    public void EmitStimulus(in StimulusPayload stimulus)
    {
        float baseRadiusSqr = stimulus.Intensity * stimulus.Intensity;

        for (int i = _receivers.Count - 1; i >= 0; i--)
        {
            var receiver = _receivers[i];
        
            if (receiver == null || (receiver is Object uObj && !uObj))
            {
                _receivers.RemoveAt(i);
                continue;
            }

            Vector3 toReceiver = receiver.SensoryOrigin - stimulus.Position;
            float distSqr = toReceiver.sqrMagnitude;

            if (distSqr > baseRadiusSqr)
                continue;

            float distance = Mathf.Sqrt(distSqr);
            Vector3 direction = toReceiver / distance;

            float effectiveIntensity = stimulus.Intensity;

            if (stimulus.Type == StimulusType.Sound && _occlusionMask != 0)
            {
                int hitCount = Physics.RaycastNonAlloc(
                    stimulus.Position, 
                    direction, 
                    _rayBuffer, 
                    distance, 
                    _occlusionMask
                );

                if (hitCount > 0)
                {
                    effectiveIntensity *= _wallAttenuation;

                    float muffledRadiusSqr = effectiveIntensity * effectiveIntensity;
                    if (distSqr > muffledRadiusSqr || effectiveIntensity < _minimumAudibleThreshold)
                        continue;
                }
            }

            StimulusPayload finalStimulus = new StimulusPayload(
                stimulus.Type,
                stimulus.Position,
                stimulus.Source,
                effectiveIntensity,
                stimulus.TimeStamp
            );

            receiver.ReceiveStimulus(finalStimulus);
        }
    }
}
}