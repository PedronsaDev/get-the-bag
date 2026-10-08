using System;
using Project.Combat.Data;
using Project.Combat.Weapons;

namespace Project.Player.Combat
{
// Server-only mutable state. Definitions are shared, snapshots are read-only copies.
public sealed class RuntimeWeapon
{
    private readonly WeaponDefinition _definition;
    private int _currentAmmo;
    private int _reserveAmmo;
    private double _nextShotTime;
    public IWeaponFireStrategy FireStrategy { get; }
    
    public WeaponDefinition Definition => _definition;
    public int CurrentAmmo => _currentAmmo;
    public int CurrentMagazineSize => _definition.BaseMagazineSize;
    public int ReserveAmmo => _reserveAmmo;
    public bool CanReload => _currentAmmo < CurrentMagazineSize && _reserveAmmo > 0;

    public bool TryFire(double now)
    {
        if (_currentAmmo <= 0 || now < _nextShotTime || !FireStrategy.TryScheduleShot(now))
            return false;
        _currentAmmo--;
        _nextShotTime = now + _definition.FireRateSeconds;
        return true;
    }

    public bool CompleteReloadFromReserve()
    {
        if (!CanReload) return false;
        var consumed = Math.Min(_reserveAmmo, CurrentMagazineSize - _currentAmmo);
        _currentAmmo += consumed;
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

    public RuntimeWeapon(WeaponDefinition definition)
    {
        if (!definition) throw new ArgumentNullException(nameof(definition));
        if (definition.BaseMagazineSize <= 0 || definition.FireRate <= 0 ||
            definition.ReloadDuration <= 0 || !float.IsFinite(definition.ReloadDuration) ||
            definition.Range <= 0 || !float.IsFinite(definition.Range) || definition.BaseDamage < 0)
            throw new ArgumentException("Weapon has invalid combat settings.", nameof(definition));
        _definition = definition;
        _currentAmmo = definition.BaseMagazineSize;
        _reserveAmmo = definition.StartingReserveAmmo;
        FireStrategy = WeaponFireStrategies.Create(definition);
    }
}
}
