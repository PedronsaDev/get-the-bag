using System;
using Project.Definitions.Weapons;
using Project.Logging;
using UnityEngine;

namespace Project.Combat.Weapons
{
public class WeaponInstance : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Transform _leftHandGrip;

    private WeaponDefinition _definition;

    public event Action OnFired;
    public event Action<bool> OnReloadingChanged;
    public event Action<int> OnAmmoChanged;

    public Transform FirePoint => _firePoint;
    public Transform LeftHandGrip => _leftHandGrip;
    public WeaponDefinition Definition => _definition;

    private bool _isReloading;
    public bool IsReloading => _isReloading;
    public float ReloadDuration { get; private set; }
    public double ReloadStartedAt { get; private set; }
    public uint ReloadSequence { get; private set; }
    public ReloadVariant ReloadType { get; private set; } = ReloadVariant.Normal;
    public int LoadedAmmo { get; private set; }

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
        OnFired?.Invoke();
    }

    public void SetReloading(bool reloading, float duration = 0f, double startedAt = 0d, uint sequence = 0, ReloadVariant reloadType = ReloadVariant.Normal)
    {
        if (_isReloading == reloading && (!reloading || ReloadSequence == sequence)) return;
        if (reloading)
        {
            if (!float.IsFinite(duration) || duration <= 0f || !double.IsFinite(startedAt))
                throw new ArgumentOutOfRangeException(nameof(duration), "Reload presentation requires valid timing.");
            ReloadDuration = duration;
            ReloadStartedAt = startedAt;
            ReloadSequence = sequence;
            ReloadType = reloadType;
        }
        _isReloading = reloading;
        OnReloadingChanged?.Invoke(reloading);
    }

    public void SetLoadedAmmo(int ammo)
    {
        if (LoadedAmmo == ammo)
            return;

        LoadedAmmo = ammo;
        OnAmmoChanged?.Invoke(ammo);
    }
}
}
