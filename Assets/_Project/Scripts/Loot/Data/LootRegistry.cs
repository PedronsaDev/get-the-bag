using Project.Core.Data;
using UnityEngine;

namespace Project.Loot.Data 
{
[CreateAssetMenu(fileName = "LootRegistry", menuName = "Registries/Loot Registry")]
public class LootRegistry : ScriptableRegistry<LootItemDefinition>
{
#if UNITY_EDITOR
    
    [ContextMenu("Scan Project Assets")]
    public override void ScanProjectAssets()
    {
        base.ScanProjectAssets();
    }
#endif    
}
}