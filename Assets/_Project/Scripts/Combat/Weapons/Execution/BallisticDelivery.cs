using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Spawns physical/kinematic ballistic projectiles through ProjectilePool.
    /// Used for rocket launchers, gyrojet pistols, plasma rifles, and legendary projectile behaviors.
    /// </summary>
    public class BallisticDelivery : IProjectileDeliveryStrategy
    {
        public void Deliver(WeaponContext context, WeaponInstance weapon, float currentSpread)
        {
            if (context == null || weapon == null || !context.AimOrigin) return;

            int pellets = Mathf.Max(1, weapon.CompiledStats.PelletsPerShot);
            Vector3 origin = context.MuzzlePoint ? context.MuzzlePoint.position : context.AimOrigin.position;
            Vector3 aimForward = context.AimOrigin.forward;
            float speed = Mathf.Max(10f, weapon.CompiledStats.ProjectileSpeed);

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDirection = CalculateSpreadDirection(aimForward, currentSpread);
                Vector3 velocity = shootDirection * speed;

                DamagePayload payload = new DamagePayload(
                    amount: weapon.CompiledStats.Damage,
                    type: weapon.CompiledStats.Element,
                    location: HitLocation.Core,
                    hitPoint: origin,
                    hitNormal: Vector3.up,
                    impactDirection: shootDirection,
                    attackerId: context.ShooterId
                );

                // Instantiate behaviors based on weapon's unique behavior IDs
                List<IProjectileBehavior> behaviors = LegendaryModifierFactory.CreateProjectileBehaviors(weapon.UniqueBehaviorIds);

                ProjectilePool.Instance.Spawn(origin, velocity, payload, context.HitMask, behaviors);
            }
        }

        private static Vector3 CalculateSpreadDirection(Vector3 baseDirection, float spreadAngleDegrees)
        {
            if (spreadAngleDegrees <= 0.01f)
                return baseDirection;

            float angle = UnityEngine.Random.Range(0f, spreadAngleDegrees);
            float rotation = UnityEngine.Random.Range(0f, 360f);

            Quaternion coneRotation = Quaternion.AngleAxis(angle, Vector3.up);
            Quaternion rollRotation = Quaternion.AngleAxis(rotation, Vector3.forward);

            return (Quaternion.LookRotation(baseDirection) * rollRotation * coneRotation) * Vector3.forward;
        }
    }
}
