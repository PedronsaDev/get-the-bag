namespace Project.Combat.Data
{

public enum WeaponCategory
{
    Pistol,
    Rifle,
    Shotgun,
    Sniper,
    Melee
}

[System.Flags]
public enum WeaponCategoryMask
{
    None = 0,
    Pistol = 1 << 0,
    Rifle = 1 << 1,
    Shotgun = 1 << 2,
    Sniper = 1 << 3,
    Melee = 1 << 4,
    All = ~0
}

public static class WeaponCategoryExtensions
{
    public static WeaponCategoryMask ToMask(this WeaponCategory category)
    {
        return category switch
        {
            WeaponCategory.Pistol => WeaponCategoryMask.Pistol,
            WeaponCategory.Rifle => WeaponCategoryMask.Rifle,
            WeaponCategory.Shotgun => WeaponCategoryMask.Shotgun,
            WeaponCategory.Sniper => WeaponCategoryMask.Sniper,
            WeaponCategory.Melee => WeaponCategoryMask.Melee,
            _ => WeaponCategoryMask.None
        };
    }

    public static bool IsInMask(this WeaponCategory category, WeaponCategoryMask mask)
    {
        return (mask & category.ToMask()) != 0;
    }
}
}
