using Project.Combat.Data;
using Project.Logging;
using UnityEngine;

namespace Project.Combat.Weapons
{
[RequireComponent(typeof(Animator))]
public class WeaponInstance : MonoBehaviour
{
    
    [Header("References")]
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Transform _leftHandGrip;
    [SerializeField] private Transform _rightHandGrip;
    [SerializeField] private Animator _animator;
    private WeaponDefinition _definition;
    
    public Transform FirePoint => _firePoint;
    public Transform LeftHandGrip => _leftHandGrip;
    public Transform RightHandGrip => _rightHandGrip;
    public WeaponDefinition Definition => _definition;
    
    public void Initialize(WeaponDefinition definition)
    {
        _definition = definition;
        
        if (!_firePoint)
        {
            _firePoint = transform.Find("FirePoint");
            if (!_firePoint) 
                AppLog.LogWarning(LogChannel.Combat, $"WeaponInstance '{name}' has no fire point. Assign one or create a direct child named 'FirePoint'.", this);
        }
    }

    public void Despawn()
    {
        // TODO: Ask the owner to release this instance. Local presenter and world service
        // have different ownership; this visual component must not guess release policy.
        
    }

    public void PlayFire()
    {
        // TODO: Cosmetic effects/animation; no authoritative ammo or damage.
    }

    public void PlayReload()
    {
        // TODO: Magazine/bolt animation coordinated with arms; no gameplay decisions.
    }
}
}
