using UnityEngine;

namespace Project.Combat
{
    [CreateAssetMenu(fileName = "Beh_Accelerating", menuName = "Combat/Behaviors/Accelerating Projectile")]
    public class AcceleratingBehaviorDefinition : ProjectileBehaviorDefinition
    {
        [Header("Acceleration Settings")]
        [Tooltip("Acceleration in m/s^2 along the travel direction.")]
        [SerializeField] private float _accelerationRate = 20f;
        [Tooltip("Maximum velocity clamp.")]
        [SerializeField] private float _maxSpeed = 120f;

        public float AccelerationRate => _accelerationRate;
        public float MaxSpeed => _maxSpeed;

        public override IProjectileBehavior CreateRuntimeInstance()
        {
            return new AcceleratingRuntimeRunner(_accelerationRate, _maxSpeed);
        }

        private class AcceleratingRuntimeRunner : IProjectileBehavior
        {
            private readonly float _acceleration;
            private readonly float _maxSpeed;

            public AcceleratingRuntimeRunner(float acceleration, float maxSpeed)
            {
                _acceleration = acceleration;
                _maxSpeed = maxSpeed;
            }

            public void OnSpawn(ProjectileInstance projectile) { }

            public void OnTravel(ProjectileInstance projectile, float deltaTime)
            {
                float currentSpeed = projectile.Velocity.magnitude;
                if (currentSpeed < _maxSpeed)
                {
                    float newSpeed = Mathf.Min(_maxSpeed, currentSpeed + (_acceleration * deltaTime));
                    projectile.Velocity = projectile.Velocity.normalized * newSpeed;
                }
            }

            public bool OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload) => false;
        }
    }
}
