using UnityEngine;

namespace Project.Combat
{
[CreateAssetMenu(fileName = "Beh_Bouncing", menuName = "Combat/Behaviors/Bouncing Projectile")]
public class BouncingBehaviorDefinition : ProjectileBehaviorDefinition
{
    [Header("Bouncing Physics")] 
    [Range(1, 10)] 
    [SerializeField] private int _maxBounces = 3;
    [Range(0.1f, 0.98f)] 
    [SerializeField] private float _bounciness = 0.65f;

    [Tooltip("Multiplies standard Physics.gravity. 0 = no gravity in flight.")] 
    [SerializeField] private float _gravityMultiplier = 1.2f;
    [Tooltip("If true, direct impacts against IDamageable entities immediately detonate and deal damage instead of bouncing.")]
    [SerializeField] private bool _detonateOnEnemyHit = true;
    [Tooltip("When bouncing off floor/ground surfaces, aligns the ricochet horizontal heading with the shot sightline to eliminate lateral barrel drift.")]
    [SerializeField] private bool _alignGroundBounce = true;

    public int MaxBounces => _maxBounces;
    public float Bounciness => _bounciness;
    public float GravityMultiplier => _gravityMultiplier;
    public bool DetonateOnEnemyHit => _detonateOnEnemyHit;
    public bool AlignGroundBounce => _alignGroundBounce;

    public override IProjectileBehavior CreateRuntimeInstance()
    {
        return new BouncingRuntimeRunner(_maxBounces, _bounciness, _gravityMultiplier, _detonateOnEnemyHit, _alignGroundBounce);
    }

    private class BouncingRuntimeRunner : IProjectileBehavior
    {
        private readonly int _maxBounces;
        private readonly float _bounciness;
        private readonly float _gravityMultiplier;
        private readonly bool _detonateOnEnemyHit;
        private readonly bool _alignGroundBounce;
        private int _remainingBounces;
        private Vector3 _shotHeading;

        public BouncingRuntimeRunner(int maxBounces, float bounciness, float gravityMultiplier, bool detonateOnEnemyHit, bool alignGroundBounce)
        {
            _maxBounces = maxBounces;
            _bounciness = bounciness;
            _gravityMultiplier = gravityMultiplier;
            _detonateOnEnemyHit = detonateOnEnemyHit;
            _alignGroundBounce = alignGroundBounce;
            _remainingBounces = maxBounces;
        }

        public void OnSpawn(ProjectileInstance projectile)
        {
            _remainingBounces = _maxBounces;
            // Cache the horizontal firing heading from the initial shot
            Vector3 forward = projectile.Payload.ImpactDirection;
            Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up);
            _shotHeading = horizontal.sqrMagnitude > 0.001f ? horizontal.normalized : Vector3.forward;
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

                // Viewplane Ricochet Normalization:
                // When reflecting off predominantly horizontal surfaces (floors/ground/ramps where normal.y > 0.5f),
                // strip out the lateral barrel offset drift so the bullet bounces straight ahead along the shot sightline.
                if (_alignGroundBounce && hit.normal.y > 0.5f && _shotHeading.sqrMagnitude > 0.001f)
                {
                    Vector3 verticalComponent = Vector3.Project(rawReflect, hit.normal);
                    Vector3 horizontalComponent = rawReflect - verticalComponent;
                    float horizontalSpeed = horizontalComponent.magnitude;

                    // Align horizontal rebound speed directly with the intended shot heading
                    projectile.Velocity = verticalComponent + (_shotHeading * horizontalSpeed);
                }
                else
                {
                    // Vertical walls/pillars: preserve realistic pool-table bank reflection
                    projectile.Velocity = rawReflect;

                    // Update heading to follow the bank shot for any subsequent ground rebounds
                    Vector3 wallReboundHorizontal = Vector3.ProjectOnPlane(rawReflect, Vector3.up);
                    if (wallReboundHorizontal.sqrMagnitude > 0.001f)
                    {
                        _shotHeading = wallReboundHorizontal.normalized;
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