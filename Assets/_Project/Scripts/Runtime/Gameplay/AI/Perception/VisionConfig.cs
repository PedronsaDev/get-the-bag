using UnityEngine;

namespace Project.AI.Perception
{
[CreateAssetMenu(fileName = "AIVisionConfig", menuName = "AI/AIVisionConfig")]
public class VisionConfig : ScriptableObject
{
    public float ViewDistance = 40.0f;
    [Range(1f, 360f)] public float FieldOfView = 20.0f;
    public float PeripheralDistance = 25.0f;
    [Range(1f, 360f)] public float PeripheralFOV = 50.0f;
    public LayerMask ObstacleMask;
    public LayerMask TargetMask;
    public float EyeHeight = 1.5f;
}
}