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
            Vector3 aimOrigin = context.AimOrigin.position;
            Vector3 aimForward = context.AimOrigin.forward;
            float speed = Mathf.Max(10f, weapon.CompiledStats.ProjectileSpeed);

            // Crosshair convergence: cast ray from camera/crosshair to find actual target point in the world
            Vector3 targetPoint;
            if (Physics.Raycast(aimOrigin, aimForward, out RaycastHit hit, 300f, context.HitMask, QueryTriggerInteraction.Ignore))
            {
                // When aiming straight down at feet, the muzzle (held forward in hands) is in front of feet.
                // Converge onto the camera crosshair line just in front of the muzzle rather than aiming backwards.
                float muzzleViewOffset = Vector3.Dot(origin - aimOrigin, aimForward);
                float minHitDist = Mathf.Max(0.5f, muzzleViewOffset + 0.3f);

                if (hit.distance < minHitDist)
                {
                    targetPoint = aimOrigin + (aimForward * minHitDist);
                }
                else
                {
                    targetPoint = hit.point;
                }
            }
            else
            {
                targetPoint = aimOrigin + (aimForward * 200f);
            }

            // Direct trajectory from barrel fire point towards the crosshair target point
            Vector3 baseDirection = (targetPoint - origin).normalized;

            // Safety fallback: if target point is behind the fire point
            if (Vector3.Dot(baseDirection, aimForward) < 0.1f)
            {
                baseDirection = aimForward;
            }

            Vector3 playerHorizontalForward = context.Owner ? context.Owner.transform.forward : Vector3.ProjectOnPlane(aimForward, Vector3.up).normalized;
            if (playerHorizontalForward.sqrMagnitude < 0.001f) playerHorizontalForward = Vector3.forward;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDirection = CalculateSpreadDirection(baseDirection, currentSpread);
                Vector3 velocity = shootDirection * speed;

                DamagePayload payload = new DamagePayload(
                    amount: weapon.CompiledStats.Damage,
                    type: weapon.CompiledStats.Element,
                    location: HitLocation.Core,
                    hitPoint: origin,
                    hitNormal: Vector3.up,
                    impactDirection: playerHorizontalForward,
                    attackerId: context.ShooterId
                );

                // Instantiate behaviors from ScriptableObjects
                List<IProjectileBehavior> behaviors = new();
                if (weapon.ProjectileBehaviors != null && weapon.ProjectileBehaviors.Count > 0)
                {
                    for (int b = 0; b < weapon.ProjectileBehaviors.Count; b++)
                    {
                        var behDef = weapon.ProjectileBehaviors[b];
                        if (behDef)
                        {
                            var instance = behDef.CreateRuntimeInstance();
                            if (instance != null) behaviors.Add(instance);
                        }
                    }
                }
                else if (weapon.UniqueBehaviorIds != null && weapon.UniqueBehaviorIds.Count > 0)
                {
                    // Fallback for legacy string IDs
                    behaviors = LegendaryModifierFactory.CreateProjectileBehaviors(weapon.UniqueBehaviorIds);
                }

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
