using System;
using PurrNet;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Contextual state supplied to trigger strategies and projectile delivery systems during firing.
    /// Decouples weapon firing logic from specific player controllers or AI agents.
    /// </summary>
    public class WeaponContext
    {
        public PlayerID? ShooterId { get; set; }
        public GameObject Owner { get; set; }
        public Transform AimOrigin { get; set; }     // Typically the Camera Pitch Pivot / Center of Screen
        public Transform MuzzlePoint { get; set; }   // Visual barrel tip for tracers and muzzle flash
        public LayerMask HitMask { get; set; } = ~0; // Geometry & Damageable collision mask

        public WeaponContext(GameObject owner, Transform aimOrigin, Transform muzzlePoint, PlayerID? shooterId = null, LayerMask? hitMask = null)
        {
            Owner = owner;
            AimOrigin = aimOrigin ? aimOrigin : owner.transform;
            MuzzlePoint = muzzlePoint ? muzzlePoint : AimOrigin;
            ShooterId = shooterId;
            if (hitMask.HasValue) HitMask = hitMask.Value;
        }
    }
}
