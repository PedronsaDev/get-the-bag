using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Waves
{
public class AIWaveSpawnPoint : MonoBehaviour
{
    private const float EyeHeight = 1.6f;
    private const float BodyHeight = 1f;

    private static readonly List<AIWaveSpawnPoint> _all = new(8);
    private static readonly List<AIWaveSpawnPoint> _candidates = new(8);

    public static IReadOnlyList<AIWaveSpawnPoint> All => _all;

    public Vector3 Position => transform.position;

    private void OnEnable() => _all.Add(this);

    private void OnDisable() => _all.Remove(this);

    public static bool TryPick(IReadOnlyList<Vector3> players, float minDistance, out AIWaveSpawnPoint point)
    {
        _candidates.Clear();
        bool anyHidden = false;

        for (int i = 0; i < _all.Count; i++)
        {
            var candidate = _all[i];
            if (!IsFarEnough(candidate.Position, players, minDistance))
                continue;

            bool hidden = !IsSeenByAny(candidate.Position, players);
            // First hidden point found: drop the visible ones collected so far.
            if (hidden && !anyHidden)
            {
                _candidates.Clear();
                anyHidden = true;
            }
            if (hidden == anyHidden)
                _candidates.Add(candidate);
        }

        if (_candidates.Count == 0)
        {
            point = null;
            return false;
        }

        point = _candidates[Random.Range(0, _candidates.Count)];
        return true;
    }

    private static bool IsFarEnough(Vector3 position, IReadOnlyList<Vector3> players, float minDistance)
    {
        float minSqr = minDistance * minDistance;
        for (int i = 0; i < players.Count; i++)
        {
            if ((players[i] - position).sqrMagnitude < minSqr)
                return false;
        }
        return true;
    }

    private static bool IsSeenByAny(Vector3 position, IReadOnlyList<Vector3> players)
    {
        Vector3 target = position + Vector3.up * BodyHeight;
        for (int i = 0; i < players.Count; i++)
        {
            Vector3 eye = players[i] + Vector3.up * EyeHeight;
            if (!Physics.Linecast(eye, target, ~0, QueryTriggerInteraction.Ignore))
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.7f, 1.8f, 0.7f));
        Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward);
    }
#endif
}
}
