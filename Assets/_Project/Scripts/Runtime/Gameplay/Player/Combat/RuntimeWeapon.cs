using System;
using System.Collections.Generic;
using Project.Combat.Data;
using Project.Combat.Weapons;

namespace Project.Player.Combat
{
public sealed class RuntimeWeapon
{
    private readonly WeaponDefinition _definition;
    private readonly WeaponResolvedStats _resolvedStats;
    private readonly string[] _attachmentIds;
    
    private int _currentAmmo;
    private int _reserveAmmo;
    private double _nextShotTime;
    
    public IWeaponFireStrategy FireStrategy { get; }
    public WeaponDefinition Definition => _definition;
    public WeaponResolvedStats ResolvedStats => _resolvedStats;
    
    public int CurrentAmmo => _currentAmmo;
    public int CurrentMagazineSize => _resolvedStats.MagazineSize;
    public int ReserveAmmo => _reserveAmmo;
    public bool CanReload => _currentAmmo < CurrentMagazineSize && _reserveAmmo > 0;

    public bool TryFire(double now)
    {
        if (_currentAmmo <= 0 || now < _nextShotTime || !FireStrategy.TryScheduleShot(now))
            return false;
        _currentAmmo--;
        _nextShotTime = now + _resolvedStats.FireRateSeconds;
        return true;
    }

    public bool CompleteReloadFromReserve()
    {
        if (!CanReload) 
            return false;
        
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
            AttachmentIds = (string[])_attachmentIds.Clone(),
            MagazineAmmo = _currentAmmo,
            ReserveAmmo = _reserveAmmo
        };
    }

    public RuntimeWeapon(WeaponDefinition definition, IReadOnlyList<string> attachmentIds = null)
    {
        if (!definition) 
            throw new ArgumentNullException(nameof(definition));
        
        _definition = definition;
        _resolvedStats = WeaponStatResolver.Resolve(definition, attachmentIds, out _attachmentIds);
        _currentAmmo = _resolvedStats.MagazineSize;
        _reserveAmmo = definition.StartingReserveAmmo;
        FireStrategy = WeaponFireStrategies.Create(definition, _resolvedStats);
    }
}
}
