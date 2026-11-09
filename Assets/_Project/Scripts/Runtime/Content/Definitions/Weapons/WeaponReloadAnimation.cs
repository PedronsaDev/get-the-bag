using UnityEngine;

namespace Project.Definitions.Weapons
{
[System.Serializable]
public class WeaponReloadAnimation
{
    [Header("Reload Animation Clips")]
    [SerializeField] private AnimationClip _firstPersonClip;
    [SerializeField] private AnimationClip _bodyClip;
    [Tooltip("World weapon mechanism only: slide, magazine and other moving parts.")]
    [SerializeField] private AnimationClip _weaponClip;
    [Tooltip("X: normalized reload progress (0-1). Y: support-hand correction (0-1). An empty curve leaves the authored hand free.")]
    [SerializeField] private AnimationCurve _supportHandWeight;

    public AnimationClip FirstPersonClip => _firstPersonClip;
    public AnimationClip BodyClip => _bodyClip;
    public AnimationClip WeaponClip => _weaponClip;
    public AnimationCurve SupportHandWeight => _supportHandWeight;
}
}
