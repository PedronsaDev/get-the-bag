using UnityEngine;

namespace Project.AI.Cover
{
/// <summary>A successful spatial query, not permission to shoot. Default values are invalid.</summary>
public readonly struct CoverPeekResult
{
    public bool IsValid { get; }
    public CoverPeekDirection Direction { get; }
    public Vector3 Destination { get; }

    internal CoverPeekResult(CoverPeekDirection direction, Vector3 destination)
    {
        IsValid = true;
        Direction = direction;
        Destination = destination;
    }
}
}
