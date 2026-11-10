using System;
using UnityEngine;

namespace Project.Match
{
[CreateAssetMenu(fileName = "RouteSelectionEventChannel", menuName = "Events/RouteSelectionEventChannel")]
public class RouteSelectionEventChannel : ScriptableObject
{
    public const string ResourcePath = "EventChannels/RouteSelectionChannel";

    public event Action<LevelSelectorConsole> OnConsoleInteracted;

    public void RaiseOnConsoleInteracted(LevelSelectorConsole console)
    {
        OnConsoleInteracted?.Invoke(console);
    }
}
}
