using UnityEngine;

namespace Project.Combat
{
    [CreateAssetMenu(fileName = "Beh_Bouncing", menuName = "Combat/Behaviors/Bouncing Projectile")]
    public class BouncingBehaviorDefinition : ProjectileBehaviorDefinition
    {
        [Header("Bouncing Physics")]
        [Range(1, 10)] [SerializeField] private int _maxBounces = 3;
        [Range(0.1f, 0.98f)] [SerializeField] private float _bounciness = 0.65f;
        [Tooltip("Multiplies standard Physics.gravity. 0 = no gravity in flight.")]
        [SerializeField] private float _gravityMultiplier = 1.2f;
        [Tooltip("If true, direct impacts against IDamageable entities immediately detonate and deal damage instead of bouncing.")]
        [SerializeField] private bool _detonateOnEnemyHit = true;
        [Tooltip("When bouncing off floor/ground surfaces, aligns the ricochet horizontal heading with the camera forward view so bullets bounce straight down the sightline rather than preserving the gun barrel offset angle.")]
        [SerializeField] private bool _alignGroundBounceToAim = true;

        public int MaxBounces => _maxBounces;
        public float Bounciness => _bounciness;
        public float GravityMultiplier => _gravityMultiplier;
        public bool DetonateOnEnemyHit => _detonateOnEnemyHit;
        public bool AlignGroundBounceToAim => _alignGroundBounceToAim;

        public override IProjectileBehavior CreateRuntimeInstance()
        {
            return new BouncingRuntimeRunner(_maxBounces, _bounciness, _gravityMultiplier, _detonateOnEnemyHit, _alignGroundBounceToAim);
        }

        private class BouncingRuntimeRunner : IProjectileBehavior
        {
            private readonly int _maxBounces;
            private readonly float _bounciness;
            private readonly float _gravityMultiplier;
            private readonly bool _detonateOnEnemyHit;
            private readonly bool _alignGroundBounceToAim;
            private int _remainingBounces;
            private Vector3 _aimHeading;

            public BouncingRuntimeRunner(int maxBounces, float bounciness, float gravityMultiplier, bool detonateOnEnemyHit, bool alignGroundBounceToAim)
            {
                _maxBounces = maxBounces;
                _bounciness = bounciness;
                _gravityMultiplier = gravityMultiplier;
                _detonateOnEnemyHit = detonateOnEnemyHit;
                _alignGroundBounceToAim = alignGroundBounceToAim;
                _remainingBounces = maxBounces;
            }

            public void OnSpawn(ProjectileInstance projectile)
            {
                _remainingBounces = _maxBounces;

                // Cache horizontal aim heading from initial impact direction / player horizontal forward
                Vector3 forward = projectile.Payload.ImpactDirection;
                Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up);
                if (horizontal.sqrMagnitude > 0.001f)
                {
                    _aimHeading = horizontal.normalized;
                }
                else if (forward.sqrMagnitude > 0.001f)
                {
                    _aimHeading = forward.normalized;
                }
                else
                {
                    _aimHeading = Vector3.forward;
                }
            }

            public void OnTravel(ProjectileInstance projectile, float deltaTime)
            {
                if (_gravityMultiplier > 0.001f)
                {
                    projectile.Velocity += Physics.gravity * (_gravityMultiplier * deltaTime);
                }
            }

            public bool OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload)
            {
                // Direct impact on enemy damages and explodes
                if (_detonateOnEnemyHit && hit.collider.GetComponentInParent<IDamageable>() != null)
                {
                    return false;
                }

                // Bounce off surfaces
                if (_remainingBounces > 0)
                {
                    _remainingBounces--;

                    Vector3 rawReflect = Vector3.Reflect(projectile.Velocity, hit.normal) * _bounciness;

                    // If bouncing off a floor/ground surface (normal predominantly facing upwards)
                    // and aim alignment is enabled, align the horizontal rebound with the camera forward heading
                    if (_alignGroundBounceToAim && hit.normal.y > 0.5f && _aimHeading.sqrMagnitude > 0.001f)
                    {
                        Vector3 verticalComponent = Vector3.Project(rawReflect, hit.normal);
                        Vector3 horizontalComponent = rawReflect - verticalComponent;
                        float horizontalSpeed = horizontalComponent.magnitude;

                        // Preserve horizontal speed, but orient it straight along the aim heading!
                        Vector3 alignedHorizontal = _aimHeading * horizontalSpeed;
                        projectile.Velocity = verticalComponent + alignedHorizontal;
                    }
                    else
                    {
                        projectile.Velocity = rawReflect;

                        // When bouncing off a vertical surface (wall/pillar), update the horizontal heading
                        // so any subsequent ground bounces continue following the wall bank-shot direction
                        Vector3 wallReboundHorizontal = Vector3.ProjectOnPlane(rawReflect, Vector3.up);
                        if (wallReboundHorizontal.sqrMagnitude > 0.001f)
                        {
                            _aimHeading = wallReboundHorizontal.normalized;
                        }
                    }

                    // Nudge along normal to prevent re-colliding in subsequent frame
                    projectile.Position = hit.point + (hit.normal * 0.12f);
                    projectile.transform.position = projectile.Position;
                    return true;
                }

                return false;
            }
        }
    }
}
