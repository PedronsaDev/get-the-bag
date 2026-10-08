namespace Project.Combat.Data
{
// Created once per equipped build. Never mutate the shared definition to apply mods.
public readonly struct WeaponResolvedStats
{
    public int Damage { get; }
    public int MagazineSize { get; }
    public float ReloadDuration { get; }
    public int FireRate { get; }
    public float FireRateSeconds => 60f / FireRate;
    public float Range { get; }

    internal WeaponResolvedStats(int damage, int magazineSize, float reloadDuration, int fireRate, float range)
    {
        Damage = damage;
        MagazineSize = magazineSize;
        ReloadDuration = reloadDuration;
        FireRate = fireRate;
        Range = range;
    }
}
}
