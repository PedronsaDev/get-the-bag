using System;
using UnityEngine;

namespace Project.Combat
{
    public interface ITriggerStrategy
    {
        void Tick(WeaponInstance weapon, float deltaTime, bool pressed, bool held, Action fireCallback);
        void Reset();
    }

    /// <summary>
    /// Semi-automatic trigger: Requires releasing and pressing trigger for each discharge.
    /// </summary>
    public class SemiAutoTrigger : ITriggerStrategy
    {
        private float _timeSinceLastShot = 999f;

        public void Tick(WeaponInstance weapon, float deltaTime, bool pressed, bool held, Action fireCallback)
        {
            _timeSinceLastShot += deltaTime;

            if (pressed && _timeSinceLastShot >= weapon.CompiledStats.CycleTime && weapon.CurrentAmmoInMag > 0)
            {
                _timeSinceLastShot = 0f;
                fireCallback?.Invoke();
            }
        }

        public void Reset()
        {
            _timeSinceLastShot = 999f;
        }
    }

    /// <summary>
    /// Full-automatic trigger: Cycles continuously while trigger is held.
    /// </summary>
    public class FullAutoTrigger : ITriggerStrategy
    {
        private float _timeSinceLastShot = 999f;

        public void Tick(WeaponInstance weapon, float deltaTime, bool pressed, bool held, Action fireCallback)
        {
            _timeSinceLastShot += deltaTime;

            if ((pressed || held) && _timeSinceLastShot >= weapon.CompiledStats.CycleTime && weapon.CurrentAmmoInMag > 0)
            {
                _timeSinceLastShot = 0f;
                fireCallback?.Invoke();
            }
        }

        public void Reset()
        {
            _timeSinceLastShot = 999f;
        }
    }

    /// <summary>
    /// Burst trigger: Discharges a rapid sequence of N rounds on trigger pull.
    /// </summary>
    public class BurstTrigger : ITriggerStrategy
    {
        private float _timeSinceLastBurst = 999f;
        private int _burstsRemaining = 0;
        private float _burstTimer = 0f;

        public void Tick(WeaponInstance weapon, float deltaTime, bool pressed, bool held, Action fireCallback)
        {
            _timeSinceLastBurst += deltaTime;

            if (_burstsRemaining > 0)
            {
                _burstTimer -= deltaTime;
                if (_burstTimer <= 0f)
                {
                    if (weapon.CurrentAmmoInMag > 0)
                    {
                        fireCallback?.Invoke();
                    }
                    _burstsRemaining--;
                    _burstTimer = weapon.CompiledStats.BurstInterval;
                }
                return;
            }

            if (pressed && _timeSinceLastBurst >= weapon.CompiledStats.CycleTime && weapon.CurrentAmmoInMag > 0)
            {
                _timeSinceLastBurst = 0f;
                _burstsRemaining = Mathf.Min(weapon.CompiledStats.BurstCount, weapon.CurrentAmmoInMag);
                _burstTimer = 0f; // Fire first round immediately
            }
        }

        public void Reset()
        {
            _timeSinceLastBurst = 999f;
            _burstsRemaining = 0;
            _burstTimer = 0f;
        }
    }
}
