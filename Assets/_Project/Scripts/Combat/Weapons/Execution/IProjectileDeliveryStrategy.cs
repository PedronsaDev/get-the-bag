using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Contract for firing mechanisms (Hitscan, Ballistic Projectile, Pellet Spread, Laser Beam).
    /// </summary>
    public interface IProjectileDeliveryStrategy
    {
        void Deliver(WeaponContext context, WeaponInstance weapon, float currentSpread);
    }
}
