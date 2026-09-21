namespace Project.Loot
{
    /// <summary>
    /// Contract for any entity capable of wearing, equipping, and carrying a duffle bag.
    /// Keeps Project.Loot completely decoupled from Project.Player.
    /// </summary>
    public interface IDuffleBagCarrier
    {
        bool HasBag { get; }
        float CurrentWeight { get; }
        float MaxCapacityKg { get; }
        int CurrentValue { get; }
        void EquipBag(DuffleBagSnapshot snapshot);
        bool TryStowItem(LootItemDefinition item);
    }
}
