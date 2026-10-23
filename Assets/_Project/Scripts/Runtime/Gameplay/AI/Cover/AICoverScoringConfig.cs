using System;
using NaughtyAttributes;
using UnityEngine;

namespace Project.AI.Cover
{

[Serializable]
public class AICoverScoringConfig
{
    [Header("Search Constraints")]
    public float MaxSearchRadius = 15.0f;
    public LayerMask ObstacleMask = ~0;

    [Header("Utility Weights (Must sum to 1.0)")] 
    [Range(0f, 1f)] public float ProximityWeight = 0.4f;
    [Range(0f, 1f)] public float FacingWeight = 0.3f;
    [Range(0f, 1f)] public float PeekWeight = 0.15f;
    [Range(0f, 1f)] public float IdealRangeWeight = 0.15f;
    
    [Header("Disqualification Thresholds")]
    public float MinFacingAlignment = 0.5f;

#if UNITY_EDITOR
    private bool ValidateWeightSum()
    {
        float total = ProximityWeight + FacingWeight + PeekWeight + IdealRangeWeight;
        return Mathf.Abs(total - 1f) < 0.001f;
    }

    [Button("Normalize Weights")]
    private void NormalizeWeights()
    {
        float total = ProximityWeight + FacingWeight + PeekWeight + IdealRangeWeight;
        if (total <= 0.0001f) return;

        ProximityWeight /= total;
        FacingWeight /= total;
        PeekWeight /= total;
        IdealRangeWeight /= total;
    }
#endif
}
}