using NaughtyAttributes;
using Project.AI.Combat;
using Project.AI.Cover;
using Project.AI.Perception;
using UnityEngine;
using UnityEngine.Serialization;

namespace Project.AI.Configuration
{
[CreateAssetMenu(fileName = "New AI Archetype Config", menuName = "AI/Archetype Config", order = 1)]
public class AIArchetypeConfig : ScriptableObject
{
    [Header("Archetype Info & Settings")]
    [SerializeField] private string _archetypeName;

    [BoxGroup("Movement Settings")]
    [SerializeField] private AIMovementConfig _movementConfig = new();

    [BoxGroup("Vision Settings")]
    [FormerlySerializedAs("_aimConfig")]
    [SerializeField] private AIVisionConfig _visionConfig = new();

    [BoxGroup("Weapon Settings")]
    [SerializeField] private AIWeaponConfig _weaponConfig = new();

    [BoxGroup("Cover Settings")]
    [SerializeField] private bool _canUseCover = false;
    [BoxGroup("Cover Settings")]
    [SerializeField, ShowIf("_canUseCover")]
    private AICoverScoringConfig _aiCoverConfig = new();

    [SerializeField] private CombatTokenType _preferredTokenType = CombatTokenType.RangedBurst;

    public string ArchetypeName => _archetypeName;
    public AIMovementConfig MovementConfig => _movementConfig;
    public AIVisionConfig VisionConfig => _visionConfig;
    public AIWeaponConfig WeaponConfig => _weaponConfig;
    public bool CanUseCover => _canUseCover;
    public AICoverScoringConfig AICoverConfig => _aiCoverConfig;
    public CombatTokenType PreferredTokenType => _preferredTokenType;

    /// <summary>Checks authored data without modifying it or querying the scene. Call before simulation starts.</summary>
    public bool TryValidate(out string error)
    {
        return AIArchetypeValidation.TryValidate(this, out error);
    }
}
}
