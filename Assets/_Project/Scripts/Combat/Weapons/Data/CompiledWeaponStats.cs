using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// The final, compiled runtime statistics of an assembled firearm after all parts,
    /// manufacturers, and level scaling have been evaluated.
    /// </summary>
    [Serializable]
    public struct CompiledWeaponStats
    {
        public float Damage;
        public float FireRate;           // Rounds per second
        public int MagazineSize;
        public float ReloadSpeed;        // Seconds to complete reload
        public float BaseSpread;         // Initial cone spread in degrees
        public float MaxSpread;          // Maximum spread during sustained fire
        public float RecoilKick;         // Camera/crosshair kick intensity
        public float ProjectileSpeed;    // Velocity in m/s (0 = instant hitscan)
        public int PelletsPerShot;       // Number of simultaneous pellets (e.g. 1 for rifle, 8+ for shotgun)
        public float CriticalMultiplier; // Locational headshot multiplier (default 2.0x)
        public DamageType Element;       // Physical, Incendiary, Corrosive, Shock, Explosive
        public FiringMode FiringMode;    // FullAuto, SemiAuto, Burst, Charge
        public int BurstCount;           // Number of rounds per burst trigger pull
        public float BurstInterval;      // Delay between shots inside a burst

        /// <summary>
        /// Time in seconds between consecutive shots.
        /// </summary>
        public readonly float CycleTime => FireRate > 0.01f ? 1f / FireRate : 0.1f;

        /// <summary>
        /// True if this weapon uses instant raycast rather than physical ballistic projectile.
        /// </summary>
        public readonly bool IsHitscan => ProjectileSpeed <= 0.01f;
    }
}
