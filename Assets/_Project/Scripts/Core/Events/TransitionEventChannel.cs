using UnityEngine;

namespace Project.Core
{
[CreateAssetMenu(fileName = "TransitionEventChannel", menuName = "Events/TransitionEventChannel")]
public class TransitionEventChannel : ScriptableObject
{
    public event System.Action OnTransitionStarted;
    public event System.Action OnTransitionCompleted;

    public void RaiseOnTransitionStarted()
    {
        OnTransitionStarted?.Invoke();
    }

    public void RaiseOnTransitionCompleted()
    {
        OnTransitionCompleted?.Invoke();
    }
}
}