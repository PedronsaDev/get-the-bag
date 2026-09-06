using UnityEngine;

namespace Project.Combat
{
/// <summary>
/// Base ScriptableObject defining modular, designer-tunable projectile mechanics.
/// ScriptableObjects are strictly stateless configuration assets; runtime state is
/// encapsulated in the lightweight IProjectileBehavior instance returned by CreateRuntimeInstance().
/// </summary>
public abstract class ProjectileBehaviorDefinition : ScriptableObject
{
    [Header("Behavior Identity")] 
    [SerializeField] private string _behaviorId = "beh_custom";
    [SerializeField] private string _displayName = "Custom Behavior";
    [TextArea(2, 4)] 
    [SerializeField] private string _description = "Custom projectile modification.";

    public string BehaviorId => _behaviorId;
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public string Description => _description;

    /// <summary>
    /// Instantiates a fresh, stateful runtime runner for an individual fired bullet.
    /// </summary>
    public abstract IProjectileBehavior CreateRuntimeInstance();
}
}