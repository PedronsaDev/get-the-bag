using System;
using Project.Combat.Data;

namespace Project.Combat.Weapons
{
public static class WeaponFireStrategies
{
    public static IWeaponFireStrategy Create(WeaponDefinition weapon, WeaponResolvedStats stats)
    {
        if (weapon.CustomFireMode)
            return weapon.CustomFireMode.CreateStrategy(weapon, stats) ??
                   throw new InvalidOperationException("Custom fire mode returned no strategy.");
        return weapon.FireMode switch
        {
            WeaponFireMode.SemiAutomatic => new SemiAutomaticFireStrategy(),
            WeaponFireMode.FullAutomatic => new FullAutomaticFireStrategy(),
            WeaponFireMode.Burst => new BurstFireStrategy(weapon.BurstSize,
                stats.FireRateSeconds, weapon.BurstCooldown),
            _ => throw new ArgumentOutOfRangeException(nameof(weapon), "Unknown fire mode.")
        };
    }
}

public abstract class TriggerFireStrategy : IWeaponFireStrategy
{
    protected bool Held;
    protected bool Pressed;
    private bool _requiresRelease;

    public void SetTrigger(bool held)
    {
        if (!held) _requiresRelease = false;
        if (_requiresRelease) return;
        if (held && !Held) Pressed = true;
        Held = held;
    }

    public virtual void Reset(bool requireRelease)
    {
        Held = Pressed = false;
        _requiresRelease = requireRelease;
    }

    public abstract bool TryScheduleShot(double now);
}

public sealed class SemiAutomaticFireStrategy : TriggerFireStrategy
{
    public override bool TryScheduleShot(double now)
    {
        if (!Pressed) return false;
        Pressed = false;
        return true;
    }
}

public sealed class FullAutomaticFireStrategy : TriggerFireStrategy
{
    public override bool TryScheduleShot(double now)
    {
        var fire = Held || Pressed;
        Pressed = false;
        return fire;
    }
}

public sealed class BurstFireStrategy : TriggerFireStrategy
{
    private readonly int _size;
    private readonly double _interval;
    private readonly double _cooldown;
    private int _remaining;
    private double _nextShot;
    private double _nextBurst;

    public BurstFireStrategy(int size, double interval, double cooldown)
    {
        if (size < 1 || interval <= 0 || cooldown < 0 ||
            !double.IsFinite(interval) || !double.IsFinite(cooldown))
            throw new ArgumentOutOfRangeException(nameof(size));
        _size = size;
        _interval = interval;
        _cooldown = cooldown;
    }

    public override bool TryScheduleShot(double now)
    {
        if (_remaining == 0 && Pressed && now >= _nextBurst)
        {
            _remaining = _size;
            _nextShot = now;
        }

        Pressed = false;
        if (_remaining == 0 || now < _nextShot) return false;
        _remaining--;
        _nextShot = now + _interval;
        if (_remaining == 0) _nextBurst = now + Math.Max(_cooldown, _interval);
        return true;
    }

    public override void Reset(bool requireRelease)
    {
        base.Reset(requireRelease);
        _remaining = 0;
    }
}
}
