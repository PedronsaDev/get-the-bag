using NaughtyAttributes;
using UnityEngine;

namespace Project.Combat.Data
{
[CreateAssetMenu(fileName = "WEAP_TYPE_NAME", menuName = "Weapons/Weapon Definition", order = 1)]
public class WeaponDefinition : ScriptableObject
{
    [Header("Registry & Identity")]
    [SerializeField, ReadOnly] private string _guid = string.Empty;
    public string WeaponID = "weap_string_id";
    public string DisplayName = "[DEFAULT_WEAPON_NAME]";
    public GameObject WorldPrefab;
    
    [Header("Weapon Stats")]
    public WeaponCategory Category = WeaponCategory.Pistol;
    
    public string Guid => _guid;
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_guid))
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
            {
                _guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            }
            if (string.IsNullOrEmpty(_guid))
            {
                _guid = System.Guid.NewGuid().ToString("N");
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
}