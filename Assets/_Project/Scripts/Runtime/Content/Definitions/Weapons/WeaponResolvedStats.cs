namespace Project.Combat.Data
{
public readonly struct WeaponResolvedStats
{
    public int Damage { get; }
    public int MagazineSize { get; }
    public float ReloadDuration { get; }
    public float ReloadDurationEmpty { get; }
    public int FireRate { get; }
    public float FireRateSeconds => 60f / FireRate;
    public float Range { get; }

    public WeaponResolvedStats(int damage, int magazineSize, float reloadDuration, float reloadDurationEmpty, int fireRate, float range)
    {
        Damage = damage;
        MagazineSize = magazineSize;
        ReloadDuration = reloadDuration;
        ReloadDurationEmpty = reloadDurationEmpty;
        FireRate = fireRate;
        Range = range;
    }
}
}
