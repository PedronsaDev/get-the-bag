using Project.Loot.Data;

namespace Project.Loot
{
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