using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Cover
{
public class CoverRegistry : MonoBehaviour
{
    public static CoverRegistry Instance { get; private set; }
    
    private static readonly List<CoverPoint> _allPoints = new();

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
        if (Instance == this)
        {
            Instance = null;
        }
        
        _allPoints.Clear();
    }

    public static void Register(CoverPoint point)
    {
        if (point && !_allPoints.Contains(point)) 
            _allPoints.Add(point);
    }

    public static void Unregister(CoverPoint point)
    {
        if (point) 
            _allPoints.Remove(point);
    }

    public static int GetCoverPointsInRangeNonAlloc(Vector3 center, float radius, List<CoverPoint> resultsBuffer)
    {
        resultsBuffer.Clear();
        
        float radiusSqr = radius * radius;

        for (int i = _allPoints.Count - 1; i >= 0; i--)
        {
            var point = _allPoints[i];
            if (!point)
            {
                _allPoints.RemoveAt(i);
                continue;
            }

            if (point.IsReserved)
                continue;

            float distSqr = (point.Position - center).sqrMagnitude;
            if (distSqr <= radiusSqr)
            {
                resultsBuffer.Add(point);
            }
        }

        return resultsBuffer.Count;
    }

    private static void PruneInvalidPoints()
    {
        for (int i = _allPoints.Count - 1; i >= 0; i--)
        {
            var point = _allPoints[i];
            if (!point)
            {
                continue;
            }
        }
    }
}
}