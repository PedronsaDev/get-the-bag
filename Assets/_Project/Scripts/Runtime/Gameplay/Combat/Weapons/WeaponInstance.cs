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
    public event Action<bool> ReloadingChanged; // True if reloading, false if finished reloading
    private bool _isReloading;
    public bool IsReloading => _isReloading;
    public float ReloadDuration { get; private set; }
    // Presentation clock: local time when the confirmed reload was first observed.
    public double ReloadStartedAt { get; private set; }
    
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

    public void SetReloading(bool reloading, float duration = 0f, double startedAt = 0d)
    {
        if (_isReloading == reloading) return;
        if (reloading)
        {
            if (!float.IsFinite(duration) || duration <= 0f || !double.IsFinite(startedAt))
                throw new ArgumentOutOfRangeException(nameof(duration), "Reload presentation requires valid timing.");
            ReloadDuration = duration;
            ReloadStartedAt = startedAt;
        }
        _isReloading = reloading;
        ReloadingChanged?.Invoke(reloading);
    }
}
}
