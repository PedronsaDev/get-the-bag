using System;
using System.Collections.Generic;
using Project.Logging;
using UnityEngine;

namespace Project.Core.Data
{
public interface IRegistryItem
{
    string Guid { get; }
    string Id { get; }
}


public abstract class ScriptableRegistry<TItem> : ScriptableObject 
    where TItem : ScriptableObject, IRegistryItem 
{
    [SerializeField] private List<TItem> _items = new();
    
    private readonly Dictionary<string, TItem> _byGuid = new();
    private readonly Dictionary<string, TItem> _byId = new(StringComparer.OrdinalIgnoreCase);
    
    public IReadOnlyList<TItem> Items => _items;
    
    public void InitializeLookup()
    {
        _byGuid.Clear();
        _byId.Clear();

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (!item) continue;

            if (!string.IsNullOrEmpty(item.Guid))
                _byGuid[item.Guid] = item;

            if (!string.IsNullOrEmpty(item.Id))
                _byId[item.Id] = item;
        }
    }

    public bool TryGetByGuid(string guid, out TItem item)
    {
        if (string.IsNullOrEmpty(guid))
        {
            item = null;
            return false;
        }

        if (_byGuid.Count == 0 && _items.Count > 0)
            InitializeLookup();

        return _byGuid.TryGetValue(guid, out item);
    }

    public bool TryGetById(string id, out TItem item)
    {
        if (string.IsNullOrEmpty(id))
        {
            item = null;
            return false;
        }

        if (_byId.Count == 0 && _items.Count > 0)
            InitializeLookup();

        return _byId.TryGetValue(id, out item);
    }

#if UNITY_EDITOR
    public virtual void ScanProjectAssets()
    {
        var foundItems = new List<TItem>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(TItem).Name}");

        foreach (var g in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TItem>(path);

            if (asset && !foundItems.Contains(asset)) 
                foundItems.Add(asset);
        }
        
        foundItems.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        
        _items = foundItems;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        InitializeLookup();
    }
#endif
}
}
