using NaughtyAttributes;
using Project.AI.Combat;
using Project.AI.Cover;
using Project.AI.Perception;
using UnityEngine;
using UnityEngine.Serialization;
using Project.AI.Locomotion;

namespace Project.AI.Configuration
{
[CreateAssetMenu(fileName = "New AI Archetype Config", menuName = "AI/Archetype Config", order = 1)]
public class AIArchetypeConfig : ScriptableObject
{
    [Header("Archetype Info & Settings")]
    [SerializeField] private string _archetypeName;
    [SerializeField] private AIRole _role = AIRole.Guard;

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
    [FormerlySerializedAs("_aiCoverConfig")]
    private AICoverScoringConfig _coverConfig = new();
    [BoxGroup("Cover Settings")]
    [SerializeField, ShowIf("_canUseCover")]
    private AICoverExecutionConfig _coverExecutionConfig = new();

    [BoxGroup("Combat Tuning")]
    [SerializeField] private AICombatTuningConfig _combatTuning = new();

    [SerializeField] private CombatTokenType _preferredTokenType = CombatTokenType.RangedBurst;

    public string ArchetypeName => _archetypeName;
    public AIRole Role => _role;
    public AIMovementConfig MovementConfig => _movementConfig;
    public AIVisionConfig VisionConfig => _visionConfig;
    public AIWeaponConfig WeaponConfig => _weaponConfig;
    public AICombatTuningConfig CombatTuning => _combatTuning;
    public bool CanUseCover => _canUseCover;
    public AICoverScoringConfig CoverConfig => _coverConfig;
    public AICoverExecutionConfig CoverExecutionConfig => _coverExecutionConfig;
    public CombatTokenType PreferredTokenType => _preferredTokenType;

    public bool TryValidate(out string error)
    {
        return AIArchetypeValidation.TryValidate(this, out error);
    }
}
}
