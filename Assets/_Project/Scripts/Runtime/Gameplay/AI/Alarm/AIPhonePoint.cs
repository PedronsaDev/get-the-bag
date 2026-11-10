using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Alarm
{
public class AIPhonePoint : MonoBehaviour
{
    private static readonly List<AIPhonePoint> _all = new(8);

    [SerializeField, Min(0.5f)] private float _callSeconds = 4f;

    private EntityId _user;
    private bool _inUse;

    public static IReadOnlyList<AIPhonePoint> All => _all;

    public float CallSeconds => _callSeconds;
    public Vector3 Position => transform.position;

    private void OnEnable()
    {
        _inUse = false;
        _all.Add(this);
    }

    private void OnDisable() => _all.Remove(this);

    public static bool TryReserveNearest(Vector3 from, EntityId user, out AIPhonePoint phone)
    {
        phone = null;
        float nearest = float.MaxValue;
        for (int i = 0; i < _all.Count; i++)
        {
            var candidate = _all[i];
            if (candidate._inUse)
                continue;

            float distance = (candidate.Position - from).sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                phone = candidate;
            }
        }

        if (!phone)
            return false;

        phone._inUse = true;
        phone._user = user;
        return true;
    }

    public void Release(EntityId user)
    {
        if (_inUse && _user == user)
            _inUse = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
    }
#endif
}
}
