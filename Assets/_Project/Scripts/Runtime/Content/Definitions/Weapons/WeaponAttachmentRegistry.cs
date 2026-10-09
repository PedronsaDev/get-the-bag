using Project.Core.Data;
using UnityEngine;

namespace Project.Combat.Data
{
[CreateAssetMenu(fileName = "WeaponAttachmentRegistry", menuName = "Registries/Weapon Attachment Registry")]
public class WeaponAttachmentRegistry : ScriptableRegistry<WeaponAttachmentDefinition>
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
