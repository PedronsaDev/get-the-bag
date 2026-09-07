using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
/// <summary>
/// Global object pool for bullet and projectile impact visual/audio effects.
/// Eliminates garbage collection spikes when automatic weapons or shotguns hit surfaces rapidly.
/// </summary>
public class ImpactEffectPool : MonoBehaviour
{
    private static ImpactEffectPool _instance;

    public static ImpactEffectPool Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindAnyObjectByType<ImpactEffectPool>();
                if (!_instance)
                {
                    var go = new GameObject("[ImpactEffectPool]");
                    _instance = go.AddComponent<ImpactEffectPool>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    [SerializeField] private ImpactEffectInstance _impactPrefab;
    private readonly Queue<ImpactEffectInstance> _availablePool = new();
    private Material _cachedParticleMaterial;

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

    public ImpactEffectInstance Spawn(Vector3 position, Vector3 normal, DamageType element = DamageType.Physical)
    {
        ImpactEffectInstance effect;

        if (_availablePool.Count > 0)
        {
            effect = _availablePool.Dequeue();
            effect.IsInPool = false;
            effect.gameObject.SetActive(true);
        }
        else
        {
            effect = CreateNewEffect();
            effect.OnDespawn += ReturnToPool;
        }

        effect.Initialize(position, normal, element);
        return effect;
    }

    private ImpactEffectInstance CreateNewEffect()
    {
        return Instantiate(_impactPrefab, transform);
    }

    private void ReturnToPool(ImpactEffectInstance effect)
    {
        if (effect && !effect.IsInPool)
        {
            effect.IsInPool = true;
            _availablePool.Enqueue(effect);
        }
    }
}
}