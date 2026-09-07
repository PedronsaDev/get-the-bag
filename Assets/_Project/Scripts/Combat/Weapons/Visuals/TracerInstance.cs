using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Lightweight visual projectile/tracer entity for hitscan bullets and high-speed rounds.
    /// Traverses space smoothly from muzzle to target point, triggers an arrival callback
    /// (e.g. for impact VFX), and returns itself to the pool with zero garbage collection.
    /// </summary>
    [DisallowMultipleComponent]
    public class TracerInstance : MonoBehaviour
    {
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private float _defaultSpeed = 400f; // m/s

        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _speed;
        private Action _onArrival;
        private float _maxLifetime = 3.0f;
        private float _elapsedLifetime;
        private bool _isAlive;

        public bool IsInPool { get; set; }
        public event Action<TracerInstance> OnDespawn;

        private void Awake()
        {
            if (!_trailRenderer)
            {
                _trailRenderer = GetComponentInChildren<TrailRenderer>();
            }
        }

        public void Initialize(Vector3 start, Vector3 target, float speed = -1f, Color? color = null, Action onArrival = null)
        {
            _startPosition = start;
            _targetPosition = target;
            _speed = speed > 10f ? speed : _defaultSpeed;
            _onArrival = onArrival;
            _elapsedLifetime = 0f;
            _isAlive = true;

            transform.position = _startPosition;
            Vector3 travelDir = (_targetPosition - _startPosition).normalized;
            if (travelDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(travelDir);
            }

            if (_trailRenderer)
            {
                _trailRenderer.Clear();
                _trailRenderer.emitting = true;

                if (color.HasValue)
                {
                    Color baseCol = color.Value;
                    Color fadeCol = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
                    
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        new[] { new GradientColorKey(baseCol, 0.0f), new GradientColorKey(baseCol, 0.7f), new GradientColorKey(fadeCol, 1.0f) },
                        new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) }
                    );
                    _trailRenderer.colorGradient = gradient;
                }
            }
        }

        private void Update()
        {
            if (!_isAlive) return;

            float dt = Time.deltaTime;
            _elapsedLifetime += dt;

            if (_elapsedLifetime >= _maxLifetime)
            {
                Arrival();
                return;
            }

            Vector3 currentPos = transform.position;
            float stepDistance = _speed * dt;
            float distanceToTarget = Vector3.Distance(currentPos, _targetPosition);

            if (stepDistance >= distanceToTarget || distanceToTarget <= 0.05f)
            {
                transform.position = _targetPosition;
                Arrival();
            }
            else
            {
                transform.position = Vector3.MoveTowards(currentPos, _targetPosition, stepDistance);
            }
        }

        private void Arrival()
        {
            if (!_isAlive) return;
            _isAlive = false;

            try
            {
                _onArrival?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _onArrival = null;
                Despawn();
            }
        }

        public void Despawn()
        {
            _isAlive = false;
            if (_trailRenderer)
            {
                _trailRenderer.emitting = false;
                _trailRenderer.Clear();
            }

            OnDespawn?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
