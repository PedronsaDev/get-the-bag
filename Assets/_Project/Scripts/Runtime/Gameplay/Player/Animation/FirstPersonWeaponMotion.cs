using Project.Combat.Weapons;
using UnityEngine;

namespace Project.Player.Animation
{

internal sealed class FirstPersonWeaponMotion
{
    private readonly WeaponAimPose _aim;
    private readonly WeaponLookSway _look;
    private readonly WeaponMovementSway _movement;
    private readonly WeaponRecoil _recoil;

    public FirstPersonWeaponMotion(WeaponInstance visual)
    {
        _aim = visual.GetComponent<WeaponAimPose>();
        _look = visual.GetComponent<WeaponLookSway>();
        _movement = visual.GetComponent<WeaponMovementSway>();
        _recoil = visual.GetComponent<WeaponRecoil>();
    }

    public bool CanAim => _aim && _aim.isActiveAndEnabled;
    public float AimProgress => CanAim ? _aim.Progress : 0f;
    public float AimFov => CanAim ? _aim.AimFov : 75f;

    public void TickAim(bool aiming, float deltaTime)
    {
        if (CanAim) _aim.Tick(aiming, deltaTime);
    }

    public void TickMotion(Vector2 lookDelta, Vector2 moveInput, bool reloading, float deltaTime)
    {
        var aimWeight = _aim ? _aim.EvaluatedWeight : 0f;
        if (_recoil && _recoil.isActiveAndEnabled) _recoil.Tick(reloading, deltaTime);
        if (_look && _look.isActiveAndEnabled) _look.Tick(lookDelta, aimWeight, reloading, deltaTime);
        if (_movement && _movement.isActiveAndEnabled) _movement.Tick(moveInput, aimWeight, reloading, deltaTime);
    }

    public void ResetPose()
    {
        if (_recoil) _recoil.ResetPose();
        if (_movement) _movement.ResetPose();
        if (_look) _look.ResetPose();
        if (_aim) _aim.ResetPose();
    }
}
}
