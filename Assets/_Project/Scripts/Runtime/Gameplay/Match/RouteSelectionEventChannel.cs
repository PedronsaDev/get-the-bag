using System;
using Project.Core.Match;
using UnityEngine;

namespace Project.Core.Events
{
[CreateAssetMenu(fileName = "RouteSelectionEventChannel", menuName = "Events/RouteSelectionEventChannel")]
public class RouteSelectionEventChannel : ScriptableObject
{
    public event Action<LevelSelectorConsole> OnConsoleInteracted;

    public void RaiseOnConsoleInteracted(LevelSelectorConsole console)
    {
        OnConsoleInteracted?.Invoke(console);
    }
}
}
