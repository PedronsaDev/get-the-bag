using UnityEngine;

namespace Project.Definitions.Loot
{
/// <summary>A reusable weighted loot list, for example "Office Desk" or "Vault". Spawn points reference it.</summary>
[CreateAssetMenu(fileName = "POOL_Name", menuName = "Loot/Loot Pool")]
public class LootPool : ScriptableObject
{
    [SerializeField, TextArea(1, 3)] private string _notes;
    [SerializeField] private LootTable _table = new();

    public LootTable Table => _table;
}
}
