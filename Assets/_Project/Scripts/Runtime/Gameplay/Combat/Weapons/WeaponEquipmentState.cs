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
    Idle,
    Equipping,
    Reloading
}

[Serializable]
public struct WeaponItemState : IPackedAuto
{
    public string WeaponId;
    public int MagazineAmmo;
    public int ReserveAmmo;
    // TODO (later): Stable part IDs, rolled modifiers, durability and persistent instance ID.
}

[Serializable]
public struct WeaponEquipmentState : IPackedAuto
{
    public WeaponItemState Main;
    public WeaponItemState Secondary;
    public WeaponSlot ActiveSlot;
    public WeaponActionState Action;

    public uint Revision;
    public uint EquipRevision;
    public float ReloadDuration;
}
}
