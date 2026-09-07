using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Pooled impact VFX and audio entity instantiated upon bullet collision with geometry or entities.
    /// Supports surface normal alignment and elemental particle coloring.
    /// </summary>
    [DisallowMultipleComponent]
    public class ImpactEffectInstance : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _impactClips;
        [SerializeField] private float _lifetime = 0.8f;

        private float _currentLife;
        private bool _isAlive;

        public bool IsInPool { get; set; }
        public event Action<ImpactEffectInstance> OnDespawn;

        private void Awake()
        {
            if (!_particleSystem)
            {
                _particleSystem = GetComponentInChildren<ParticleSystem>();
            }
            if (!_audioSource)
            {
                _audioSource = GetComponentInChildren<AudioSource>();
            }
        }

        public void Initialize(Vector3 position, Vector3 normal, DamageType element)
        {
            transform.position = position;
            if (normal.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(normal);
            }

            _currentLife = 0f;
            _isAlive = true;

            Color elementColor = CombatVisualHelper.GetElementColor(element);

            if (_particleSystem)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _particleSystem.main;
                main.startColor = elementColor;

                _particleSystem.Clear();
                _particleSystem.Play();
            }

            if (_audioSource && _impactClips != null && _impactClips.Length > 0)
            {
                var clip = _impactClips[UnityEngine.Random.Range(0, _impactClips.Length)];
                if (clip)
                {
                    _audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.12f);
                    _audioSource.PlayOneShot(clip);
                }
            }
        }

        private void Update()
        {
            if (!_isAlive) return;

            _currentLife += Time.deltaTime;
            if (_currentLife >= _lifetime)
            {
                Despawn();
            }
        }

        public void Despawn()
        {
            if (!_isAlive) return;
            _isAlive = false;

            if (_particleSystem)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            OnDespawn?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
