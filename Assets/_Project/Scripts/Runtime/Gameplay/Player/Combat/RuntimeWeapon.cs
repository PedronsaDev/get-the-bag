using System;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Logging;

namespace Project.Player.Combat
{
[Serializable]
public class RuntimeWeapon
{
    private WeaponDefinition _definition;
    private int _currentAmmo;
    private int _currentMagazineSize;
    
    public WeaponDefinition Definition => _definition;
    public int CurrentAmmo => _currentAmmo;
    public int CurrentMagazineSize => _currentMagazineSize;

    // Server-owned per-item model. Preserve it when changing slots.
    public bool TryConsumeRound()
    {
        // TODO: Reject empty magazine; decrement exactly once per accepted shot.
        return false;
    }

    public bool TryCompleteReload(int availableReserve, out int consumedReserve)
    {
        consumedReserve = 0;
        // TODO: Transfer missing rounds bounded by reserve; reject negative inputs.
        return false;
    }

    public WeaponItemState CaptureState()
    {
        return new WeaponItemState
        {
            WeaponId = _definition.Id,
            MagazineAmmo = _currentAmmo
        };
    }

    public void RestoreState(WeaponItemState state)
    {
        if (state.WeaponId != _definition.Id)
        {
            AppLog.LogError(LogChannel.Combat, $"Cannot restore state for weapon '{_definition.Id}' from mismatched state '{state.WeaponId}'.", this);
            return;
        }

        if (state.MagazineAmmo < 0 || state.MagazineAmmo > _definition.BaseMagazineSize)
        {
            AppLog.LogError(LogChannel.Combat, $"Cannot restore state for weapon '{_definition.Id}' with invalid magazine ammo {state.MagazineAmmo}.", this);
            return;
        }
        
        _currentAmmo = state.MagazineAmmo;
    }

    public RuntimeWeapon(WeaponDefinition definition)
    {
        _definition = definition;
        _currentAmmo = definition.BaseMagazineSize;
        _currentMagazineSize = definition.BaseMagazineSize;
    }
}
}
