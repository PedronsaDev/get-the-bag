using System;
using UnityEngine;

namespace Project.AI.Perception
{
[Serializable]
public struct VisionConfig
{
    public float ViewDistance;
    [Range(1f, 360f)]public float FieldOfView;
    public float PeripheralDistance;
    [Range(1f, 360f)]public float PeripheralFOV;
    public LayerMask ObstacleMask;
    public LayerMask TargetMask;
    public float EyeHeight;
}
}