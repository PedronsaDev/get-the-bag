using Project.Core.Data;
using UnityEngine;

namespace Project.Combat.Data
{
[CreateAssetMenu(fileName = "WeaponRegistry", menuName = "Registries/Weapon Registry")]
public class WeaponRegistry : ScriptableRegistry<WeaponDefinition>
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