using UnityEngine;

namespace Project.Definitions.Weapons
{
[System.Serializable]
public class WeaponReloadAnimation
{
    [Header("Reload Animation Clips")]
    [SerializeField] private AnimationClip _firstPersonClip;
    [SerializeField] private AnimationClip _bodyClip;
    [SerializeField] private AnimationClip _weaponClip;
    [SerializeField] private AnimationCurve _supportHandWeight;

    public AnimationClip FirstPersonClip => _firstPersonClip;
    public AnimationClip BodyClip => _bodyClip;
    public AnimationClip WeaponClip => _weaponClip;
    public AnimationCurve SupportHandWeight => _supportHandWeight;
}
}
