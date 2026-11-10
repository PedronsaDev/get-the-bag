using Project.Definitions;
using UnityEngine;

namespace Project.Definitions.Weapons
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
