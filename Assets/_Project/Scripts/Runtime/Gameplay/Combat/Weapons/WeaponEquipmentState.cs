using System;
using PurrNet.Packing;

namespace Project.Combat.Weapons
{
public enum WeaponSlot
{
    None = 0,
    Main = 1,
    Secondary = 2
}

public enum WeaponActionState
{
    Idle = 0,
    Reloading = 2
}

[Serializable]
public struct WeaponItemState : IPackedAuto
{
    public string WeaponId;
    public int MagazineAmmo;
    public int ReserveAmmo;
}

[Serializable]
public struct WeaponEquipmentState : IPackedAuto
{
    public WeaponItemState Main;
    public WeaponItemState Secondary;
    public WeaponSlot ActiveSlot;
    public WeaponActionState Action;

    public uint EquipRevision;

    public WeaponItemState ActiveWeapon => ActiveSlot switch
    {
        WeaponSlot.Main => Main,
        WeaponSlot.Secondary => Secondary,
        _ => default
    };
}
}
