using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Delivers instant hitscan raycasts with cone spread, pellet dispatching,
    /// and direct damage integration into IDamageable targets.
    /// </summary>
    public class HitscanDelivery : IProjectileDeliveryStrategy
    {
        public static Action<Vector3, Vector3> OnTracerSpawned; // (origin, endPoint)
        public static Action<DamageResult> OnLocalHitConfirmed;

        public void Deliver(WeaponContext context, WeaponInstance weapon, float currentSpread)
        {
            if (context == null || weapon == null || !context.AimOrigin) return;

            int pellets = Mathf.Max(1, weapon.CompiledStats.PelletsPerShot);
            Vector3 origin = context.AimOrigin.position;
            Vector3 forward = context.AimOrigin.forward;
            Vector3 muzzle = context.MuzzlePoint ? context.MuzzlePoint.position : origin;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDirection = CalculateSpreadDirection(forward, currentSpread);
                Vector3 tracerTarget = origin + (shootDirection * 200f);

                if (Physics.Raycast(origin, shootDirection, out RaycastHit hit, 300f, context.HitMask, QueryTriggerInteraction.Ignore))
                {
                    tracerTarget = hit.point;

                    // Evaluate damage against hit entity
                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        DamagePayload payload = new DamagePayload(
                            amount: weapon.CompiledStats.Damage,
                            type: weapon.CompiledStats.Element,
                            location: HitLocation.Core,
                            hitPoint: hit.point,
                            hitNormal: hit.normal,
                            impactDirection: shootDirection,
                            attackerId: context.ShooterId
                        );

                        DamageResult result = damageable.TakeDamage(payload);
                        OnLocalHitConfirmed?.Invoke(result);
                    }

                    // Physics impact impulse
                    if (hit.rigidbody && !hit.rigidbody.isKinematic)
                    {
                        hit.rigidbody.AddForceAtPosition(shootDirection * (weapon.CompiledStats.Damage * 3f), hit.point, ForceMode.Impulse);
                    }
                }

                OnTracerSpawned?.Invoke(muzzle, tracerTarget);
            }
        }

        private static Vector3 CalculateSpreadDirection(Vector3 baseDirection, float spreadAngleDegrees)
        {
            if (spreadAngleDegrees <= 0.01f)
                return baseDirection;

            // Random uniform point inside cone
            float angle = UnityEngine.Random.Range(0f, spreadAngleDegrees);
            float rotation = UnityEngine.Random.Range(0f, 360f);

            Quaternion coneRotation = Quaternion.AngleAxis(angle, Vector3.up);
            Quaternion rollRotation = Quaternion.AngleAxis(rotation, Vector3.forward);

            Quaternion combined = Quaternion.LookRotation(baseDirection) * rollRotation * coneRotation;
            return combined * Vector3.forward;
        }
    }
}
