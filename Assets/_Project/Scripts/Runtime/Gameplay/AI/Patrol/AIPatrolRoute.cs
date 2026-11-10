using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Patrol
{
// Child transforms are the waypoints, in order. A waypoint's forward is where the guard looks.
public class AIPatrolRoute : MonoBehaviour
{
    [SerializeField, Tooltip("Loop back to the first point, or walk the route forward and back.")]
    private bool _loop = true;
    [SerializeField, Min(0f)] private float _waitSeconds = 3f;
    [SerializeField, Range(0f, 180f)] private float _lookSweepDegrees = 45f;
    [SerializeField, Min(0.1f)] private float _lookSweepPeriod = 4f;
    [SerializeField, Min(1f)] private float _turnSpeedDegrees = 120f;

    public bool Loop => _loop;
    public float WaitSeconds => _waitSeconds;
    public float LookSweepDegrees => _lookSweepDegrees;
    public float LookSweepPeriod => _lookSweepPeriod;
    public float TurnSpeedDegrees => _turnSpeedDegrees;
    public int PointCount => transform.childCount;

    public Transform GetPoint(int index) => transform.GetChild(index);

    public int NearestPointIndex(Vector3 position)
    {
        int nearest = 0;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < PointCount; i++)
        {
            float distance = (GetPoint(i).position - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }
        return nearest;
    }

    public static int NextIndex(int current, int count, bool loop, ref int direction)
    {
        if (count <= 1)
            return 0;
        if (loop)
            return (current + 1) % count;

        int next = current + direction;
        if (next >= count)
        {
            direction = -1;
            return count - 2;
        }
        if (next < 0)
        {
            direction = 1;
            return 1;
        }
        return next;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        for (int i = 0; i < PointCount; i++)
        {
            Vector3 position = GetPoint(i).position;
            bool onNavMesh = NavMesh.SamplePosition(position, out _, 0.5f, NavMesh.AllAreas);
            Gizmos.color = onNavMesh ? Color.green : Color.red;
            Gizmos.DrawSphere(position, 0.2f);
            Gizmos.DrawRay(position, GetPoint(i).forward);

            int next = i + 1;
            if (next < PointCount || (_loop && PointCount > 1))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(position, GetPoint(next % PointCount).position);
            }
        }
    }
#endif
}
}
