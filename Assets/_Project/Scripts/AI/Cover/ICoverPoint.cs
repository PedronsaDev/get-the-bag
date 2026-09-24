using UnityEngine;

namespace Project.AI.Cover
{
public interface ICoverPoint 
{
    Vector3 Position { get; }
    Vector3 Forward { get; }
    CoverHeight Height { get; }
    CoverPeekDirection PeekDirections { get; }
    bool IsOccupied { get; }
    
    bool TryReserve(int entityId);
    void Release(int entityId);
}
}