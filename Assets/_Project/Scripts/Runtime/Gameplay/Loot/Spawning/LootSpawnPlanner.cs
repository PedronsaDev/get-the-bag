using System.Collections.Generic;
using Project.Definitions.Loot;
using UnityEngine;

namespace Project.Loot.Spawning
{
public readonly struct PlannedLoot
{
    public readonly LootDefinition Loot;
    public readonly LootSpawnPoint Point;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;

    public PlannedLoot(LootDefinition loot, LootSpawnPoint point)
    {
        Loot = loot;
        Point = point;
        Position = point.transform.position;
        Rotation = point.transform.rotation;
    }
}

/// <summary>
/// Decides what loot appears where. Pure logic, no spawning, so the editor can simulate it.
/// Same seed + same points + same modifiers = same result.
/// </summary>
public static class LootSpawnPlanner
{
    public static void Plan(IReadOnlyList<LootSpawnPoint> points, IReadOnlyList<LootSpawnGroup> groups,
        System.Random rng, in LootRollModifiers mods, List<PlannedLoot> results)
    {
        results.Clear();
        var groupPoints = new List<LootSpawnPoint>();

        foreach (var group in groups)
        {
            if (!group || !group.isActiveAndEnabled) continue;

            group.GetPoints(groupPoints);
            Shuffle(groupPoints, rng);
            int pick = group.RollPickCount(rng, mods, groupPoints.Count);

            for (int i = 0; i < pick; i++)
            {
                if (groupPoints[i].TryRoll(rng, mods, !group.PickedPointsRollChance, out var loot))
                    results.Add(new PlannedLoot(loot, groupPoints[i]));
            }
        }

        foreach (var point in points)
        {
            if (!point || !point.isActiveAndEnabled) continue;
            if (point.GetComponentInParent<LootSpawnGroup>()) continue;

            if (point.TryRoll(rng, mods, false, out var loot))
                results.Add(new PlannedLoot(loot, point));
        }
    }

    /// <summary>Finds every spawn point and group in loaded scenes, in a stable order so seeds are repeatable.</summary>
    public static void FindInScene(List<LootSpawnPoint> points, List<LootSpawnGroup> groups)
    {
        points.Clear();
        groups.Clear();
        points.AddRange(Object.FindObjectsByType<LootSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        groups.AddRange(Object.FindObjectsByType<LootSpawnGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        points.Sort((a, b) => Compare(a.transform.position, b.transform.position));
        groups.Sort((a, b) => Compare(a.transform.position, b.transform.position));
    }

    private static int Compare(Vector3 a, Vector3 b)
    {
        int c = a.x.CompareTo(b.x);
        if (c != 0) return c;
        c = a.y.CompareTo(b.y);
        return c != 0 ? c : a.z.CompareTo(b.z);
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
}
