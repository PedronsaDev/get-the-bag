using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    public static class LegendaryBehaviorIds
    {
        public const string ConferenceCall = "ConferenceCall_Split";
        public const string SandHawk = "SandHawk_Wave";
        public const string UnkemptHarold = "Harold_AccelerateSplit";
        public const string TheButcher = "TheButcher_AmmoRefund";
    }

    /// <summary>
    /// Factory that instantiates projectile behaviors and weapon modifiers based on unique IDs.
    /// </summary>
    public static class LegendaryModifierFactory
    {
        public static List<IProjectileBehavior> CreateProjectileBehaviors(IReadOnlyList<string> uniqueIds)
        {
            List<IProjectileBehavior> list = new();
            if (uniqueIds == null) return list;

            for (int i = 0; i < uniqueIds.Count; i++)
            {
                switch (uniqueIds[i])
                {
                    case LegendaryBehaviorIds.ConferenceCall:
                        list.Add(new ConferenceCallBehavior());
                        break;
                    case LegendaryBehaviorIds.SandHawk:
                        list.Add(new SandHawkBehavior());
                        break;
                    case LegendaryBehaviorIds.UnkemptHarold:
                        list.Add(new UnkemptHaroldBehavior());
                        break;
                }
            }

            return list;
        }
    }

    /// <summary>
    /// Conference Call: As the main projectile travels forward, it spawns child pellets
    /// that fire perpendicularly (left and right) along its trajectory at fixed intervals.
    /// </summary>
    public class ConferenceCallBehavior : IProjectileBehavior
    {
        private float _interval = 0.07f;
        private float _timer;
        private int _splitsRemaining = 5;
        private bool _isChild;

        public ConferenceCallBehavior(bool isChild = false)
        {
            _isChild = isChild;
        }

        public void OnSpawn(ProjectileInstance projectile)
        {
            _timer = 0.04f;
        }

        public void OnTravel(ProjectileInstance projectile, float deltaTime)
        {
            if (_isChild || _splitsRemaining <= 0) return;

            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _timer = _interval;
                _splitsRemaining--;

                Vector3 forward = projectile.Velocity.normalized;
                Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
                float childSpeed = projectile.Velocity.magnitude * 0.85f;

                DamagePayload childPayload = projectile.Payload;
                childPayload.Amount *= 0.5f; // Child pellets deal half damage

                // Spawn Left and Right perpendicular child pellets
                ProjectilePool.Instance.Spawn(projectile.Position, right * childSpeed, childPayload, projectile.HitMask, new[] { new ConferenceCallBehavior(true) });
                ProjectilePool.Instance.Spawn(projectile.Position, -right * childSpeed, childPayload, projectile.HitMask, new[] { new ConferenceCallBehavior(true) });
            }
        }

        public void OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload) { }
    }

    /// <summary>
    /// Sand Hawk: Moves in a sinusoidal bird-wing flapping pattern with horizontal/vertical oscillation.
    /// </summary>
    public class SandHawkBehavior : IProjectileBehavior
    {
        private float _frequency = 12.0f;
        private float _amplitude = 3.5f;
        private Vector3 _baseDirection;
        private Vector3 _rightAxis;
        private float _time;

        public void OnSpawn(ProjectileInstance projectile)
        {
            _baseDirection = projectile.Velocity.normalized;
            _rightAxis = Vector3.Cross(_baseDirection, Vector3.up).normalized;
            _time = 0f;
        }

        public void OnTravel(ProjectileInstance projectile, float deltaTime)
        {
            _time += deltaTime;
            float wave = Mathf.Sin(_time * _frequency) * _amplitude;
            float speed = projectile.Velocity.magnitude;

            // Oscillate velocity horizontally in wing shape
            Vector3 modulatedDir = (_baseDirection + (_rightAxis * wave * 0.3f)).normalized;
            projectile.Velocity = modulatedDir * speed;
        }

        public void OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload) { }
    }

    /// <summary>
    /// Unkempt Harold: Projectile starts at medium speed, accelerates forward,
    /// and splits horizontally into 3 -> 5 -> 7 explosive gyrojets as distance increases.
    /// </summary>
    public class UnkemptHaroldBehavior : IProjectileBehavior
    {
        private bool _hasSplit1;
        private bool _hasSplit2;
        private bool _isChild;

        public UnkemptHaroldBehavior(bool isChild = false)
        {
            _isChild = isChild;
        }

        public void OnSpawn(ProjectileInstance projectile) { }

        public void OnTravel(ProjectileInstance projectile, float deltaTime)
        {
            // Accelerate
            projectile.Velocity += projectile.Velocity.normalized * (18f * deltaTime);

            if (_isChild) return;

            float dist = projectile.DistanceTraveled;

            // Split 1 at 4 meters: bifurcates left and right
            if (!_hasSplit1 && dist >= 4.0f)
            {
                _hasSplit1 = true;
                SpawnLateralSpawns(projectile, 8f);
            }
            // Split 2 at 10 meters: splits again wider
            else if (!_hasSplit2 && dist >= 10.0f)
            {
                _hasSplit2 = true;
                SpawnLateralSpawns(projectile, 16f);
            }
        }

        private void SpawnLateralSpawns(ProjectileInstance projectile, float angle)
        {
            Vector3 forward = projectile.Velocity.normalized;
            float speed = projectile.Velocity.magnitude;

            Quaternion leftRot = Quaternion.AngleAxis(-angle, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(angle, Vector3.up);

            ProjectilePool.Instance.Spawn(projectile.Position, (leftRot * forward) * speed, projectile.Payload, projectile.HitMask, new[] { new UnkemptHaroldBehavior(true) });
            ProjectilePool.Instance.Spawn(projectile.Position, (rightRot * forward) * speed, projectile.Payload, projectile.HitMask, new[] { new UnkemptHaroldBehavior(true) });
        }

        public void OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload)
        {
            // Harold explosive payload
            payload.Type = DamageType.Explosive;
        }
    }
}
