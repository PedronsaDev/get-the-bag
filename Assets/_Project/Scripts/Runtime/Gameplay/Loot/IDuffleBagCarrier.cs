using System.Collections.Generic;
using Project.Definitions.Loot;

namespace Project.Loot
{
public interface IDuffleBagCarrier
{
    bool HasBag { get; }
    float CurrentWeight { get; }
    float MaxCapacityKg { get; }
    int CurrentValue { get; }
    IReadOnlyList<DuffleBagItemEntry> StowedItems { get; }
    void EquipBag(DuffleBagSnapshot snapshot);
    bool TryStowItem(LootDefinition item);
    void ClearBagContents();
}
}