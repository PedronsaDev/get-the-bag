using UnityEngine;

namespace Project.AI.Configuration
{
[CreateAssetMenu(fileName = "AIWeaponConfig", menuName = "AI/Configuration/AIWeaponConfig", order = 2)]
public class AIWeaponConfig : ScriptableObject
{
    [Header("Damage Tuning")]
    public float DamagePerShot = 12.0f;
    public float MaxRange = 50.0f;

    [Header("Cadence & Burst")]
    public float RoundsPerMinute = 450.0f;
    public int MinBurstCount = 3;
    public int MaxBurstCount = 5;
    public float MinBurstPause = 0.8f;
    public float MaxBurstPause = 1.4f;

    [Header("Accuracy")]
    public float SpreadAngleDegrees = 3.5f;

    [Header("Perception & Audio")]
    public float GunshotAcousticIntensity = 35.0f;

    [Header("Layer Masks")]
    public LayerMask HitMask = ~0;
}
}