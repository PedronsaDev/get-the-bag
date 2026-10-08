using System;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Logging;

namespace Project.Player.Combat
{
[Serializable]
public class RuntimeWeapon
{
    private readonly WeaponDefinition _definition;
    private int _currentAmmo;
    private readonly int _currentMagazineSize;
    private int _reserveAmmo;
    private double _nextShotTime;
    public IWeaponFireStrategy FireStrategy { get; }
    
    public WeaponDefinition Definition => _definition;
    public int CurrentAmmo => _currentAmmo;
    public int CurrentMagazineSize => _currentMagazineSize;
    public int ReserveAmmo => _reserveAmmo;
    public bool CanReload => _currentAmmo < _currentMagazineSize && _reserveAmmo > 0;
    public bool CanFire(double now) => _currentAmmo > 0 && now >= _nextShotTime;

    public bool TryFire(double now)
    {
        if (!CanFire(now) || !FireStrategy.TryScheduleShot(now) || !TryConsumeRound())
            return false;
        _nextShotTime = now + _definition.FireRateSeconds;
        return true;
    }

    public bool TryConsumeRound()
    {
        if (_currentAmmo <= 0) return false;
        _currentAmmo--;
        return true;
    }

    public bool TryCompleteReload(int availableReserve, out int consumedReserve)
    {
        consumedReserve = 0;
        if (availableReserve <= 0) return false;
        consumedReserve = Math.Min(availableReserve, _currentMagazineSize - _currentAmmo);
        _currentAmmo += consumedReserve;
        return consumedReserve > 0;
    }

    public bool CompleteReloadFromReserve()
    {
        if (!TryCompleteReload(_reserveAmmo, out var consumed)) return false;
        _reserveAmmo -= consumed;
        return true;
    }

    public WeaponItemState CaptureState()
    {
        return new WeaponItemState
        {
            WeaponId = _definition.Id,
            MagazineAmmo = _currentAmmo,
            ReserveAmmo = _reserveAmmo
        };
    }

    public void RestoreState(WeaponItemState state)
    {
        if (state.WeaponId != _definition.Id)
        {
            AppLog.LogError(LogChannel.Combat, $"Cannot restore state for weapon '{_definition.Id}' from mismatched state '{state.WeaponId}'.", _definition);
            return;
        }

        if (state.MagazineAmmo < 0 || state.MagazineAmmo > _currentMagazineSize || state.ReserveAmmo < 0)
        {
            AppLog.LogError(LogChannel.Combat, $"Cannot restore state for weapon '{_definition.Id}' with invalid ammo: magazine={state.MagazineAmmo}, reserve={state.ReserveAmmo}.", _definition);
            return;
        }
        
        _currentAmmo = state.MagazineAmmo;
        _reserveAmmo = state.ReserveAmmo;
    }

    public RuntimeWeapon(WeaponDefinition definition)
    {
        if (!definition) throw new ArgumentNullException(nameof(definition));
        if (definition.BaseMagazineSize <= 0 || definition.FireRate <= 0 ||
            definition.ReloadDuration <= 0 || !float.IsFinite(definition.ReloadDuration) ||
            definition.Range <= 0 || !float.IsFinite(definition.Range) || definition.BaseDamage < 0)
            throw new ArgumentException("Weapon has invalid combat settings.", nameof(definition));
        _definition = definition;
        _currentAmmo = definition.BaseMagazineSize;
        _currentMagazineSize = definition.BaseMagazineSize;
        _reserveAmmo = definition.StartingReserveAmmo;
        FireStrategy = WeaponFireStrategies.Create(definition);
    }
}
}
