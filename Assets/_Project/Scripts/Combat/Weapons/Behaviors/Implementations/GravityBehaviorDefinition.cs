using UnityEngine;

namespace Project.Combat
{
    [CreateAssetMenu(fileName = "BEH_Gravity_New", menuName = "Combat/Behaviors/Gravity Trajectory")]
    public class GravityBehaviorDefinition : ProjectileBehaviorDefinition
    {
        [Header("Gravity Scale")]
        [Tooltip("Multiplies standard Physics.gravity. 1 = standard gravity.")]
        [SerializeField] private float _gravityScale = 1.0f;

        public float GravityScale => _gravityScale;

        public override IProjectileBehavior CreateRuntimeInstance()
        {
            return new GravityRuntimeRunner(_gravityScale);
        }

        private class GravityRuntimeRunner : IProjectileBehavior
        {
            private readonly float _gravityScale;

            public GravityRuntimeRunner(float gravityScale)
            {
                _gravityScale = gravityScale;
            }

            public void OnSpawn(ProjectileInstance projectile) { }

            public void OnTravel(ProjectileInstance projectile, float deltaTime)
            {
                projectile.Velocity += Physics.gravity * (_gravityScale * deltaTime);
            }

            public bool OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload) => false;
        }
    }
}
