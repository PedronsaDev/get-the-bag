using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Global object pool for cosmetic hitscan tracers, ensuring zero GC allocations during firefights.
    /// </summary>
    public class TracerPool : MonoBehaviour
    {
        private static TracerPool _instance;
        public static TracerPool Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindAnyObjectByType<TracerPool>();
                    if (!_instance)
                    {
                        var go = new GameObject("[TracerPool]");
                        _instance = go.AddComponent<TracerPool>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [SerializeField] private TracerInstance _tracerPrefab;
        private readonly Queue<TracerInstance> _availablePool = new();
        private Material _cachedMaterial;

        private void Awake()
        {
            if (!_instance)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        public TracerInstance Spawn(Vector3 start, Vector3 target, float speed = -1f, Color? color = null, Action onArrival = null)
        {
            TracerInstance tracer;

            if (_availablePool.Count > 0)
            {
                tracer = _availablePool.Dequeue();
                tracer.IsInPool = false;
                tracer.gameObject.SetActive(true);
            }
            else
            {
                tracer = CreateNewTracer();
                tracer.OnDespawn += ReturnToPool;
            }

            tracer.Initialize(start, target, speed, color, onArrival);
            return tracer;
        }

        private TracerInstance CreateNewTracer()
        {
            if (_tracerPrefab)
            {
                return Instantiate(_tracerPrefab, transform);
            }

            // Procedural fallback: construct clean glowing trail tracer
            var go = new GameObject("Procedural_Tracer");
            go.transform.SetParent(transform);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.08f;
            trail.startWidth = 0.045f;
            trail.endWidth = 0.005f;
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            if (!_cachedMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Unlit/Color");
                if (shader)
                {
                    _cachedMaterial = new Material(shader);
                }
            }

            if (_cachedMaterial)
            {
                trail.material = _cachedMaterial;
            }

            var instance = go.AddComponent<TracerInstance>();
            return instance;
        }

        private void ReturnToPool(TracerInstance tracer)
        {
            if (tracer && !tracer.IsInPool)
            {
                tracer.IsInPool = true;
                _availablePool.Enqueue(tracer);
            }
        }
    }
}
