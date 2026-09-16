using System;
using Project.Core.Match;
using Project.Logging;
using UnityEngine;

namespace Project.Core.Events
{
[CreateAssetMenu(fileName = "RaidEventChannel", menuName = "Events/RaidEventChannel")]
public class RaidEventChannel : ScriptableObject
{
    // Raid Manager Events
    public event Action<RaidManager> OnRaidManagerUpdated;
    public event Action<RaidState> OnStateChanged;
    public event Action OnRaidFailed;
    public event Action OnRaidCompleted;
    
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