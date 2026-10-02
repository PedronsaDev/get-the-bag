using System;
using System.Collections.Generic;
using NaughtyAttributes;
using Project.Combat.Data;
using Project.Logging;
using UnityEngine;

namespace Project.Combat
{
[CreateAssetMenu(fileName = "WeaponRegistry", menuName = "Weapons/Weapon Registry", order = 0)]
public class WeaponRegistry : ScriptableObject
{
    private static WeaponRegistry _instance;

    public static WeaponRegistry Instance
    {
        get
        {
            if (_instance)
                return _instance;
            
#if UNITY_EDITOR
            if (!_instance)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:WeaponRegistry");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponRegistry>(path);
                }
                else
                {
                    if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                    {
                        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project"))
                            UnityEditor.AssetDatabase.CreateFolder("Assets", "_Project");
                        UnityEditor.AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                    }

                    _instance = CreateInstance<WeaponRegistry>();
                    UnityEditor.AssetDatabase.CreateAsset(_instance, "Assets/_Project/Resources/WeaponRegistry.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
                }
            }
#endif
            _instance = CreateInstance<WeaponRegistry>();
            _instance.name = "WeaponRegistry_RuntimeFallback";

            return _instance;
        }
    }
    
    [Header("Registered Weapons")]
    [SerializeField, ReadOnly] private List<WeaponDefinition> _weapons = new();
    
    private readonly Dictionary<string, WeaponDefinition> _weaponsBygGuid = new();
    private readonly Dictionary<string, WeaponDefinition> _weaponsByStringId = new(StringComparer.OrdinalIgnoreCase);
    
    public IReadOnlyList<WeaponDefinition> Weapons => _weapons;
    
    private event Action OnRegistryUpdated;
    
    private void OnEnable()
    {
        InitializeLookup();
#if UNITY_EDITOR
        if (_weapons.Count == 0)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this) ScanProjectAssets(silent: true);
            };
        }
#endif
    }
    
    private void InitializeLookup()
    {
        _weaponsBygGuid.Clear();
        _weaponsByStringId.Clear();

        for (int i = 0; i < _weapons.Count; i++)
        {
            var def = _weapons[i];
            if (!def)
                continue;

            if (!string.IsNullOrEmpty(def.Guid)) 
                _weaponsBygGuid[def.Guid] = def;
            if (!string.IsNullOrEmpty(def.WeaponID)) 
                _weaponsByStringId[def.WeaponID] = def;
        }
    }
    
    public bool TryGetWeaponByGuid(string guid, out WeaponDefinition weapon)
    {
        if (string.IsNullOrEmpty(guid))
        {
            AppLog.LogWarning(LogChannel.Combat, $"WeaponRegistry: TryGetWeaponByGuid called with null or empty guid.");
            weapon = null;
            return false;
        }
        
        if (_weaponsBygGuid.Count == 0 && _weapons.Count > 0) 
            InitializeLookup();
        
        return _weaponsBygGuid.TryGetValue(guid, out weapon);
    }
    
    public bool TryGetWeaponByStringId(string weaponId, out WeaponDefinition weapon)
    {
        if (string.IsNullOrEmpty(weaponId))
        {
            weapon = null;
            AppLog.LogWarning(LogChannel.Combat, $"WeaponRegistry: TryGetWeaponByStringId called with null or empty weaponId.");
            return false;
        }
        
        if (_weaponsByStringId.Count == 0 && _weapons.Count > 0) 
            InitializeLookup();
        
        return _weaponsByStringId.TryGetValue(weaponId, out weapon);
    }

#if UNITY_EDITOR
    
    [ContextMenu("Scan Project For All Weapons & Parts")]
    public void ScanProjectAssets()
    {
        ScanProjectAssetsInternal(silent: false);
    }

    public bool ScanProjectAssets(bool silent)
    {
        return ScanProjectAssetsInternal(silent);
    }
    
    private bool ScanProjectAssetsInternal(bool silent)
    {
        var foundWeapons = new List<WeaponDefinition>();
        
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:WeaponDefinition");
        foreach (var g in guids)
        {
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (def && !foundWeapons.Contains(def)) 
                foundWeapons.Add(def);
        }
        
        foundWeapons.Sort((a, b) => string.Compare(a.WeaponID, b.WeaponID, StringComparison.OrdinalIgnoreCase));
        bool changed = !AreAssetListsEqual(_weapons, foundWeapons);

        if (changed)
        {
            _weapons = foundWeapons;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            InitializeLookup();
            OnRegistryUpdated?.Invoke();

            if (!silent) 
                AppLog.Log(LogChannel.Editor, $"WeaponRegistry Auto-scanned: found {foundWeapons.Count} weapons.");
            
            return true;
        }
        else
        {
            InitializeLookup();
            return false;
        }
    }
    
    private static bool AreAssetListsEqual<T>(List<T> a, List<T> b) where T : UnityEngine.Object
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
    
    public void RegisterWeapon(WeaponDefinition weapon)
    {
        if (weapon && !_weapons.Contains(weapon))
        {
            _weapons.Add(weapon);
            _weapons.Sort((a, b) => string.Compare(a.WeaponID, b.WeaponID, StringComparison.OrdinalIgnoreCase));
            InitializeLookup();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            OnRegistryUpdated?.Invoke();
        }
    }
    
#endif
}
}