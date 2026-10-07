using UnityEngine;

namespace Project.Core.Interaction
{
    public interface IPlayerInteractor
    {
        GameObject InteractorGameObject { get; }
        Transform AimTransform { get; }
        IInteractable CurrentTarget { get; }
        float CurrentHoldProgress { get; } // 0.0 to 1.0
    }
}
