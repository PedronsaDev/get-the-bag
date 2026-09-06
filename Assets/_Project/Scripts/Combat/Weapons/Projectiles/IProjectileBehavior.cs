using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Pluggable interface for custom projectile flight, splitting, and impact mechanics.
    /// Powers unique/legendary projectile behaviors (Conference Call, Sand Hawk, Harold, etc.).
    /// </summary>
    public interface IProjectileBehavior
    {
        void OnSpawn(ProjectileInstance projectile);
        void OnTravel(ProjectileInstance projectile, float deltaTime);
        bool OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload);
    }
}
