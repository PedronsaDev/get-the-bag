using System;
using PurrNet;
using UnityEngine;

#pragma warning disable UAC1001 // Unity serializer skips nullable struct, but PurrNet RPC handles it natively

namespace Project.Combat
{
    /// <summary>
    /// Encapsulates all data required to execute a hit or attack against an IDamageable entity.
    /// Fully serializable across network RPCs.
    /// </summary>
    [Serializable]
    public struct DamagePayload
    {
        public float Amount;
        public DamageType Type;
        public HitLocation Location;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public Vector3 ImpactDirection;
        public PlayerID? AttackerId;

        public DamagePayload(
            float amount, 
            DamageType type = DamageType.Physical, 
            HitLocation location = HitLocation.Core,
            Vector3 hitPoint = default,
            Vector3 hitNormal = default,
            Vector3 impactDirection = default,
            PlayerID? attackerId = null)
        {
            Amount = amount;
            Type = type;
            Location = location;
            HitPoint = hitPoint;
            HitNormal = hitNormal == default ? Vector3.up : hitNormal;
            ImpactDirection = impactDirection == default ? Vector3.forward : impactDirection;
            AttackerId = attackerId;
        }
    }

    /// <summary>
    /// Feedback report generated after damage evaluation.
    /// Used by UI, audio, hit-markers, damage numbers, and kill counters.
    /// </summary>
    [Serializable]
    public struct DamageResult
    {
        public bool Applied;
        public float RawDamage;
        public float EffectiveDamage;
        public float DamageToShield;
        public float DamageToHealth;
        public bool IsCritical;
        public bool IsFatal;
        public HitLocation Location;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public PlayerID? AttackerId;
        public float RemainingHealth;
        public float RemainingShield;
    }
}
