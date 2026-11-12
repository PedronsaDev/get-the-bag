using System;
using UnityEngine;

namespace Project.Core
{
[CreateAssetMenu(fileName = "PlayerWeaponEventChannel", menuName = "Events/PlayerWeaponEventChannel")]
public class PlayerWeaponEventChannel : ScriptableObject
{
    public event Action OnADSStarted;
    public event Action OnADSEnded;
    
    public void RaiseADSStarted()
    {
        OnADSStarted?.Invoke();
    }
    
    public void RaiseADSEnded()
    {
        OnADSEnded?.Invoke();
    }
}
}