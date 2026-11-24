using Project.Combat.Weapons;
using UnityEngine;

namespace Project.Combat.Presentation
{
public class WeaponMuzzleFlash : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponInstance _weaponInstance;
    [SerializeField] private ParticleSystem _muzzleFlashVFX;
    private bool _presentationEnabled = true;

    public void SetPresentationEnabled(bool enabled)
    {
        _presentationEnabled = enabled;
        if (!enabled && _muzzleFlashVFX)
            _muzzleFlashVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Awake()
    {
        if (!_weaponInstance)
            _weaponInstance = GetComponentInParent<WeaponInstance>();

        if (!_muzzleFlashVFX)
            _muzzleFlashVFX = GetComponentInChildren<ParticleSystem>();
    }

    private void OnEnable()
    {
        if (_weaponInstance)
            _weaponInstance.OnFired += HandleWeaponFired;
    }

    private void OnDisable()
    {
        if (_weaponInstance)
            _weaponInstance.OnFired -= HandleWeaponFired;

        if (_muzzleFlashVFX)
        {
            _muzzleFlashVFX.Stop();
            _muzzleFlashVFX.Clear();
        }
    }

    private void HandleWeaponFired()
    {
        if (_presentationEnabled && _muzzleFlashVFX) _muzzleFlashVFX.Play();
    }
}
}
