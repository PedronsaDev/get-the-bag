using UnityEngine;

namespace Project.AI.Locomotion
{
public enum MovementStatus : byte
{
    None,
    Pending,
    Moving,
    Arrived,
    Failed
}
}