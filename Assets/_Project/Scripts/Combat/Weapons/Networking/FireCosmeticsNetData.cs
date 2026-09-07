using System;
using PurrNet.Packing;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Lightweight network packet broadcasted on gunshot to synchronize visual muzzle flashes,
    /// bullet tracers, ballistic projectiles, and impact VFX across remote clients.
    /// Eliminates the need for NetworkTransform on individual bullet entities.
    /// </summary>
    [Serializable]
    public struct FireCosmeticsNetData : IPackedAuto
    {
        public Vector3 MuzzlePosition;
        public Vector3 TargetPoint;
        public Vector3 AimForward;
        public float CurrentSpread;
        public int RandomSeed;
        public byte PelletCount;
        public byte ElementByte;
        public bool IsBallistic;
        public float ProjectileSpeed;
        public string PatternId;

        public DamageType Element => (DamageType)ElementByte;

        public FireCosmeticsNetData(
            Vector3 muzzlePosition,
            Vector3 targetPoint,
            Vector3 aimForward,
            float currentSpread,
            int randomSeed,
            int pelletCount,
            DamageType element,
            bool isBallistic,
            float projectileSpeed,
            string patternId = null)
        {
            MuzzlePosition = muzzlePosition;
            TargetPoint = targetPoint;
            AimForward = aimForward;
            CurrentSpread = currentSpread;
            RandomSeed = randomSeed;
            PelletCount = (byte)Mathf.Clamp(pelletCount, 1, 255);
            ElementByte = (byte)element;
            IsBallistic = isBallistic;
            ProjectileSpeed = projectileSpeed;
            PatternId = patternId ?? string.Empty;
        }
    }
}
