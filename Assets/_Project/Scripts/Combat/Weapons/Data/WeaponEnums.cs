namespace Project.Combat
{
    /// <summary>
    /// Core archetype of the firearm.
    /// </summary>
    public enum WeaponType : byte
    {
        Pistol = 0,
        Shotgun = 1,
        SubmachineGun = 2,
        AssaultRifle = 3,
        SniperRifle = 4,
        HeavyLauncher = 5
    }

    /// <summary>
    /// Sockets and part classifications that make up an assembled firearm.
    /// </summary>
    public enum WeaponPartType : byte
    {
        Receiver = 0,   // Root core / body of the weapon
        Barrel = 1,     // Determines projectile pattern, base damage, and delivery type
        Grip = 2,       // Affects handling, recoil recovery, and reload speed
        Stock = 3,      // Affects stability, max spread, and kick
        Sight = 4,      // Determines zoom level, ADS speed, and optic visual
        Magazine = 5,   // Determines capacity and reload timing
        Accessory = 6   // Elemental mod or legendary red-text perk
    }

    /// <summary>
    /// Trigger cycle mechanics.
    /// </summary>
    public enum FiringMode : byte
    {
        SemiAuto = 0,   // One shot per trigger pull
        FullAuto = 1,   // Cycles continuously while trigger is held
        Burst = 2,      // Fires N rounds per trigger pull with burst interval
        Charge = 3      // Charges up energy before discharging
    }

    /// <summary>
    /// Numeric stats modified by parts and manufacturers.
    /// </summary>
    public enum StatType : byte
    {
        Damage = 0,
        FireRate = 1,
        MagazineSize = 2,
        ReloadSpeed = 3,
        BaseSpread = 4,
        MaxSpread = 5,
        RecoilKick = 6,
        ProjectileSpeed = 7,
        PelletsPerShot = 8,
        CriticalMultiplier = 9
    }

    /// <summary>
    /// How a stat modifier mathematically modifies the base stat.
    /// </summary>
    public enum ModifierOperation : byte
    {
        FlatAdditive = 0,           // Stat += Value (e.g. +10 damage, +4 mag size)
        PercentageAdditive = 1,     // Stat += BaseStat * (Value / 100) (e.g. +15% damage)
        PercentageMultiplicative = 2 // Stat *= (1 + Value / 100) (e.g. compound multipliers)
    }

    /// <summary>
    /// Mathematical model used to scale weapon damage with item level.
    /// </summary>
    public enum LevelScalingMode : byte
    {
        Exponential = 0, // Base * Pow(1 + rate, level - 1) [Borderlands 2/3 style, compound scaling]
        Linear = 1       // Base * (1 + (level - 1) * rate) [Borderlands 1 style, flat % scaling]
    }
}
