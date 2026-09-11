using System;
using Project.Core.Match;
using UnityEngine;

namespace Project.Core.Events
{
[CreateAssetMenu(fileName = "RouteSelectionEventChannel", menuName = "Events/RouteSelectionEventChannel")]
public class RouteSelectionEventChannel : ScriptableObject
{
    public event Action<TrainRouteConsole> OnConsoleInteracted;

    public void RaiseOnConsoleInteracted(TrainRouteConsole console)
    {
        OnConsoleInteracted?.Invoke(console);
    }
}
}