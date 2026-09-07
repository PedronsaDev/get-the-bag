using UnityEngine;

namespace Project.Combat
{
    [CreateAssetMenu(fileName = "BEH_Wave_New", menuName = "Combat/Behaviors/Wave Trajectory")]
    public class WaveBehaviorDefinition : ProjectileBehaviorDefinition
    {
        [Header("Wave Parameters")]
        [Tooltip("Oscillation speed in cycles per second.")]
        [SerializeField] private float _frequency = 12.0f;
        [Tooltip("Lateral amplitude width of the wave.")]
        [SerializeField] private float _amplitude = 3.5f;
        [Tooltip("If true, oscillates vertically; otherwise horizontally.")]
        [SerializeField] private bool _verticalWave = false;

        public float Frequency => _frequency;
        public float Amplitude => _amplitude;
        public bool VerticalWave => _verticalWave;

        public override IProjectileBehavior CreateRuntimeInstance()
        {
            return new WaveRuntimeRunner(_frequency, _amplitude, _verticalWave);
        }

        private class WaveRuntimeRunner : IProjectileBehavior
        {
            private readonly float _frequency;
            private readonly float _amplitude;
            private readonly bool _verticalWave;
            private Vector3 _baseDirection;
            private Vector3 _oscillationAxis;
            private float _time;

            public WaveRuntimeRunner(float frequency, float amplitude, bool verticalWave)
            {
                _frequency = frequency;
                _amplitude = amplitude;
                _verticalWave = verticalWave;
            }

            public void OnSpawn(ProjectileInstance projectile)
            {
                _baseDirection = projectile.Velocity.normalized;
                _oscillationAxis = _verticalWave 
                    ? Vector3.up 
                    : Vector3.Cross(_baseDirection, Vector3.up).normalized;
                _time = 0f;
            }

            public void OnTravel(ProjectileInstance projectile, float deltaTime)
            {
                _time += deltaTime;
                float wave = Mathf.Sin(_time * _frequency) * _amplitude;
                float speed = projectile.Velocity.magnitude;

                Vector3 modulatedDir = (_baseDirection + (_oscillationAxis * (wave * 0.3f))).normalized;
                projectile.Velocity = modulatedDir * speed;
            }

            public bool OnHit(ProjectileInstance projectile, RaycastHit hit, ref DamagePayload payload) => false;
        }
    }
}
