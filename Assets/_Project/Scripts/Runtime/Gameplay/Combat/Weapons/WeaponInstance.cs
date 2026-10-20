using System;
using Project.Combat.Data;
using Project.Logging;
using UnityEngine;

namespace Project.Combat.Weapons
{
public class WeaponInstance : MonoBehaviour
{
    
    [Header("References")]
    [SerializeField] private Transform _firePoint;
    private WeaponDefinition _definition;
    
    public Transform FirePoint => _firePoint;
    public WeaponDefinition Definition => _definition;
    public event Action Fired;
    public event Action<bool> ReloadingChanged;
    private bool _isReloading;
    
    public void Initialize(WeaponDefinition definition)
    {
        _definition = definition;
        
        if (!_firePoint)
        {
            _firePoint = transform.Find("FirePoint");
            if (!_firePoint) 
                AppLog.LogWarning(LogChannel.Combat, $"WeaponInstance '{name}' has no fire point. Assign one or create a direct child named 'FirePoint'.", this);
        }
    }

    public void PlayFire()
    {
        Fired?.Invoke();
    }

    public void SetReloading(bool reloading)
    {
        if (_isReloading == reloading) return;
        _isReloading = reloading;
        ReloadingChanged?.Invoke(reloading);
    }
}
}
