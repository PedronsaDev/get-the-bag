using UnityEngine;

namespace Project.Definitions.Weapons
{
[CreateAssetMenu(fileName = "WeaponPresentationDefinition", menuName = "Weapons/WeaponPresentationDefinition")]
public class WeaponPresentationDefinition : ScriptableObject
{
    [Header("Rig Compatibility")]
    [SerializeField] private string _requiredFirstPersonArmsFamily;
    [SerializeField] private string _bodyHoldingFamily;

    [Header("Holding")]
    [SerializeField] private AnimationClip _bodyHoldClip;
    [SerializeField] private AnimationClip _firstPersonIdleEmptyClip;
    [SerializeField] private AnimationClip _worldIdleEmptyClip;

    [Header("Reloads")]
    [SerializeField] private WeaponReloadAnimation _reloadNormal = new();
    [Tooltip("Missing Empty clips use the corresponding Normal clip. A missing body clip also uses the Normal hand curve.")]
    [SerializeField] private WeaponReloadAnimation _reloadEmpty = new();

    public string RequiredFirstPersonArmsFamily => _requiredFirstPersonArmsFamily;
    public string BodyHoldingFamily => _bodyHoldingFamily;

    public AnimationClip BodyHoldClip => _bodyHoldClip;

    public AnimationClip FirstPersonIdleEmptyClip => _firstPersonIdleEmptyClip;
    public AnimationClip WorldIdleEmptyClip => _worldIdleEmptyClip;

    public WeaponReloadAnimation ReloadNormal => _reloadNormal;
    public WeaponReloadAnimation ReloadEmpty => _reloadEmpty;
}
}
