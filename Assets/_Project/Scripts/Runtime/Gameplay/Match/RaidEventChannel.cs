using System;
using UnityEngine;

namespace Project.Match
{
[CreateAssetMenu(fileName = "RaidEventChannel", menuName = "Events/RaidEventChannel")]
public class RaidEventChannel : ScriptableObject
{
    public const string ResourcePath = "EventChannels/RaidEventChannel";

    public event Action<RaidManager> OnRaidManagerUpdated;
    public event Action<RaidState> OnStateChanged;
    public event Action OnRaidFailed;
    public event Action OnRaidCompleted;
    public event Action<RaidResults> OnResultsReady;
    
    public event Action OnInfilCompleted;
    
    public void RaiseRaidManagerUpdated(RaidManager raidManager)
    {
        OnRaidManagerUpdated?.Invoke(raidManager);
    }
    
    public void RaiseStateChanged(RaidState newState)
    {
        OnStateChanged?.Invoke(newState);
    }
    
    public void RaiseRaidCompleted()
    {
        OnRaidCompleted?.Invoke();
    }

    public void RaiseResultsReady(RaidResults results)
    {
        OnResultsReady?.Invoke(results);
    }

    public void RaiseRaidFailed()
    {
        OnRaidFailed?.Invoke();
    }
    
    public void RaiseInfilCompleted()
    {
        OnInfilCompleted?.Invoke();
    }
}
}
