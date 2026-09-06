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
            Vector3 aimOrigin = context.AimOrigin.position;
            Vector3 aimForward = context.AimOrigin.forward;
            Vector3 muzzle = context.MuzzlePoint ? context.MuzzlePoint.position : aimOrigin;
            float speed = Mathf.Max(10f, weapon.CompiledStats.ProjectileSpeed);

            // Crosshair convergence: find authoritative target point under reticle
            Vector3 targetPoint;
            if (Physics.Raycast(aimOrigin, aimForward, out RaycastHit hit, 300f, context.HitMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = aimOrigin + (aimForward * 200f);
            }

            // Point-blank safety check: if target is closer than the muzzle's forward reach (e.g. shooting at feet)
            float muzzleForwardReach = Vector3.Dot(muzzle - aimOrigin, aimForward);
            float targetForwardReach = Vector3.Dot(targetPoint - aimOrigin, aimForward);

            Vector3 baseDirection;
            if (targetForwardReach <= muzzleForwardReach + 0.1f)
            {
                baseDirection = aimForward;
            }
            else
            {
                baseDirection = (targetPoint - muzzle).normalized;
            }

            // Safety fallback: ensure direction never points backwards
            if (Vector3.Dot(baseDirection, aimForward) < 0.1f)
            {
                baseDirection = aimForward;
            }

            Vector3 aimRight = context.AimOrigin.right;
            Vector3 aimUp = context.AimOrigin.up;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDirection;
                if (weapon.PelletPattern != null)
                {
                    Vector2 patternOffset = weapon.PelletPattern.GetPointOffset(i, pellets, currentSpread, weapon.CompiledStats.BaseSpread);
                    shootDirection = PelletPatternDefinition.CalculatePatternDirection(baseDirection, aimRight, aimUp, patternOffset);
                }
                else
                {
                    shootDirection = CalculateSpreadDirection(baseDirection, currentSpread);
                }

                Vector3 velocity = shootDirection * speed;

                DamagePayload payload = new DamagePayload(
                    amount: weapon.CompiledStats.Damage,
                    type: weapon.CompiledStats.Element,
                    location: HitLocation.Core,
                    hitPoint: muzzle,
                    hitNormal: Vector3.up,
                    impactDirection: shootDirection,
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

                // Crisp 1:1 straight trajectory spawned directly from the weapon muzzle
                ProjectilePool.Instance.Spawn(muzzle, velocity, payload, context.HitMask, behaviors);
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
