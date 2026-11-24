using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Cover
{
public class CoverRegistry : MonoBehaviour
{
    private const float CellSize = 8f;

    public static CoverRegistry Instance { get; private set; }

    private static readonly Dictionary<long, List<CoverPoint>> _cells = new();
    private static readonly Dictionary<CoverPoint, long> _cellOf = new();

    public static int Count => _cellOf.Count;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        _cells.Clear();
        _cellOf.Clear();
    }

    public static void Register(CoverPoint point)
    {
        if (!point || _cellOf.ContainsKey(point))
            return;

        long key = CellKey(point.Position);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new List<CoverPoint>(4);
            _cells.Add(key, cell);
        }
        cell.Add(point);
        _cellOf.Add(point, key);
        CoverScoringEngine.ReserveCandidateCapacity(_cellOf.Count);
    }

    public static void Unregister(CoverPoint point)
    {
        if (!_cellOf.TryGetValue(point, out long key))
            return;

        _cellOf.Remove(point);
        if (_cells.TryGetValue(key, out var cell))
            cell.Remove(point);
    }

    public static int GetCoverPointsInRangeNonAlloc(Vector3 center, float radius, List<CoverPoint> resultsBuffer)
    {
        resultsBuffer.Clear();

        float radiusSqr = radius * radius;
        int minX = Cell(center.x - radius);
        int maxX = Cell(center.x + radius);
        int minZ = Cell(center.z - radius);
        int maxZ = Cell(center.z + radius);

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                if (_cells.TryGetValue(Pack(x, z), out var cell))
                    CollectFromCell(cell, center, radiusSqr, resultsBuffer);
            }
        }

        return resultsBuffer.Count;
    }

    private static void CollectFromCell(List<CoverPoint> cell, Vector3 center, float radiusSqr, List<CoverPoint> results)
    {
        for (int i = cell.Count - 1; i >= 0; i--)
        {
            var point = cell[i];
            if (!point)
            {
                cell.RemoveAt(i);
                _cellOf.Remove(point);
                continue;
            }

            if (point.IsReserved)
                continue;

            if ((point.Position - center).sqrMagnitude <= radiusSqr)
                results.Add(point);
        }
    }

    private static int Cell(float coordinate) => Mathf.FloorToInt(coordinate / CellSize);

    private static long Pack(int x, int z) => ((long)x << 32) | (uint)z;

    private static long CellKey(Vector3 position) => Pack(Cell(position.x), Cell(position.z));
}
}
