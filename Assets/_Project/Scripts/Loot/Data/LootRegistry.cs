using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Loot.Data
{
[CreateAssetMenu(fileName = "LootRegistry", menuName = "Loot/Loot Registry")]
public class LootRegistry : ScriptableObject
{
    private static LootRegistry _instance;

    public static LootRegistry Instance
    {
        get
        {
            if (_instance) return _instance;

            _instance = Resources.Load<LootRegistry>("LootRegistry");

#if UNITY_EDITOR
            if (!_instance)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LootRegistry");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<LootRegistry>(path);
                }
                else
                {
                    if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                    {
                        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project"))
                            UnityEditor.AssetDatabase.CreateFolder("Assets", "_Project");
                        UnityEditor.AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                    }

                    _instance = CreateInstance<LootRegistry>();
                    UnityEditor.AssetDatabase.CreateAsset(_instance, "Assets/_Project/Resources/LootRegistry.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
                }
            }
#endif

            if (!_instance)
            {
                _instance = CreateInstance<LootRegistry>();
                _instance.name = "LootRegistry_RuntimeFallback";
            }

            return _instance;
        }
    }

    public static event Action OnRegistryUpdated;

    [Header("Catalog")] [SerializeField] private List<LootItemDefinition> _registeredItems = new();

    private readonly Dictionary<string, LootItemDefinition> _itemsById = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LootItemDefinition> RegisteredItems => _registeredItems;

    private void OnEnable()
    {
        InitializeLookup();
    }

    public void InitializeLookup()
    {
        _itemsById.Clear();
        for (int i = 0; i < _registeredItems.Count; i++)
        {
            var item = _registeredItems[i];
            if (item && !string.IsNullOrEmpty(item.Id))
            {
                _itemsById[item.Id] = item;
            }
        }
    }

    public bool TryGetItem(string id, out LootItemDefinition item)
    {
        if (string.IsNullOrEmpty(id))
        {
            item = null;
            return false;
        }

        if (_itemsById.Count == 0 && _registeredItems.Count > 0)
        {
            InitializeLookup();
        }

        return _itemsById.TryGetValue(id, out item);
    }

    public LootItemDefinition GetRandomItemByCategory(LootCategory category)
    {
        var candidates = new List<LootItemDefinition>();
        for (int i = 0; i < _registeredItems.Count; i++)
        {
            var item = _registeredItems[i];
            if (item && item.Category == category)
            {
                candidates.Add(item);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

#if UNITY_EDITOR
    public void RegisterItem(LootItemDefinition item)
    {
        if (!item || _registeredItems.Contains(item)) return;
        _registeredItems.Add(item);
        InitializeLookup();
        UnityEditor.EditorUtility.SetDirty(this);
        OnRegistryUpdated?.Invoke();
    }
#endif
}
}