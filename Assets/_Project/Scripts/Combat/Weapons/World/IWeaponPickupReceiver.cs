namespace Project.Combat
{
    /// <summary>
    /// Contract for entities (such as players) capable of picking up or equipping dropped weapons.
    /// Distinguishes between tap (e.g. inventory pickup) and hold (e.g. active slot equip/swap).
    /// </summary>
    public interface IWeaponPickupReceiver
    {
        void ReceiveWeaponPickup(WeaponInstance weapon, ItemNetSnapshot snapshot, bool isHold);
    }
}
