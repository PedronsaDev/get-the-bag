using System;
using UnityEngine;

namespace Project.AI.Perception
{
public enum AlertState : byte
{
    Unaware,
    Suspicious,
    Alerted,
    Searching
}

[Serializable]
public struct TargetRecord
{
    public GameObject Target;
    public Vector3 LastKnownPosition;
    public float LastSeenTimestamp;
    public float SuspicionLevel; // 0.0 to 1.0f
    public bool HasLineOfSight;
}
}