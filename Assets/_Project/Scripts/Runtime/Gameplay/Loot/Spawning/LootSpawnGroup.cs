using System.Collections.Generic;
using Project.Loot.Data;
using UnityEngine;

namespace Project.Loot.Spawning
{
/// <summary>
/// Picks a few of the spawn points underneath it, for example "the gold is in 1 of these 3 safes".
/// Points under a group only spawn if the group picks them.
/// </summary>
[AddComponentMenu("Loot/Loot Spawn Group")]
public class LootSpawnGroup : MonoBehaviour
{
    [Tooltip("How many child points to pick (random between min and max, inclusive).")]
    [SerializeField] private Vector2Int _pickCount = new(1, 1);

    [Tooltip("Picked points still roll their own spawn chance. Off = picked points always spawn.")]
    [SerializeField] private bool _pickedPointsRollChance;

    [Tooltip("Scale the pick count by the quantity modifier.")]
    [SerializeField] private bool _scaleWithQuantity = true;

    public bool PickedPointsRollChance => _pickedPointsRollChance;

    public void GetPoints(List<LootSpawnPoint> results)
    {
        results.Clear();
        foreach (var point in GetComponentsInChildren<LootSpawnPoint>(false))
            if (point.enabled && point.GetComponentInParent<LootSpawnGroup>() == this)
                results.Add(point);
    }

    public int RollPickCount(System.Random rng, in LootRollModifiers mods, int available)
    {
        int min = Mathf.Max(0, Mathf.Min(_pickCount.x, _pickCount.y));
        int max = Mathf.Max(_pickCount.x, _pickCount.y);
        float count = rng.Next(min, max + 1);
        if (_scaleWithQuantity) count *= mods.QuantityMultiplier;
        return Mathf.Clamp(Mathf.RoundToInt(count), 0, available);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var points = new List<LootSpawnPoint>();
        GetPoints(points);
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        foreach (var point in points)
            Gizmos.DrawLine(transform.position, point.transform.position + Vector3.up * 0.15f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f,
            $"{name}: picks {_pickCount.x}-{_pickCount.y} of {points.Count}");
    }
#endif
}
}
