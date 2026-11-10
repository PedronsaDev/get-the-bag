using UnityEngine;

namespace Project.Core
{
[CreateAssetMenu(fileName = "TransitionEventChannel", menuName = "Events/TransitionEventChannel")]
public class TransitionEventChannel : ScriptableObject
{
    public const string ResourcePath = "EventChannels/TransitionEventChannel";

    public event System.Action OnTransitionStarted;
    public event System.Action OnTransitionCompleted;

    /// <summary>How long the transition screen takes to become fully opaque. Set by the screen itself at runtime.</summary>
    public float FadeInSeconds { get; set; }

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