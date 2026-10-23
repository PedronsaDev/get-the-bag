using System;
using Project.AI.Blackboard;
using Project.AI.Locomotion;
using Project.AI.Perception;
using Project.Combat;
using Project.Combat.Core;
using PurrNet;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.AI.Combat
{
[DisallowMultipleComponent]
public class AIWeaponExecutor : NetworkBehaviour
{
    [Header("References")] 
    [SerializeField] private AIBlackboard _blackboard;
    [SerializeField] private AIAimLookController _aimController;
    [SerializeField] private AIWeaponConfig _weaponConfig;
    [SerializeField] private Transform _muzzlePoint;

    private float _shotTimer;
    private float _burstPauseTimer;
    private int _shotsFiredInCurrentBurst;
    private int _targetBurstCount;
    private bool _isPausingBetweenBursts;
    
    public event Action OnBurstCompleted;

    private void Awake()
    {
        if (!_blackboard) 
            _blackboard = GetComponent<AIBlackboard>();

        if (!_aimController) 
            _aimController = GetComponent<AIAimLookController>();
    }

    protected override void OnSpawned()
    {
        if (!isServer)
        {
            enabled = false;
            return;
        }

        ResetBurstCycle();
    }

    private void Update()
    {
        if (!isServer || !_blackboard || !_aimController)
            return;
        
        UpdateBurstCadence(Time.deltaTime);
    }

    private void UpdateBurstCadence(float deltaTime)
    {
        bool canShoot = _blackboard.WantsToShoot && _aimController.IsAimAligned && _blackboard.HasLineOfSight;

        if (!canShoot)
            return;

        if (_isPausingBetweenBursts)
        {
            _burstPauseTimer -= deltaTime;
            if (_burstPauseTimer <= 0f)
                ResetBurstCycle();
            return;
        }
        
        _shotTimer -= deltaTime;
        if (_shotTimer <= 0f)
        {
            float shotInterval = 60.0f / _weaponConfig.RoundsPerMinute;
            _shotTimer = shotInterval;

            FireSingleShot();
            _shotsFiredInCurrentBurst++;

            if (_shotsFiredInCurrentBurst >= _targetBurstCount)
            {
                ResetBurstCycle(); // Reset shot counters immediately
                OnBurstCompleted?.Invoke();
            }
        }
    }

    private void FireSingleShot()
    {
        Vector3 muzzlePos = _muzzlePoint ? _muzzlePoint.position : transform.position + Vector3.up * 1.4f;
        Vector3 aimPoint = _aimController.AimTargetPoint;

        Vector3 aimDirection = (aimPoint - muzzlePos).normalized;
        Vector3 shotDirection = CalculateSpreadDirection(aimDirection, _weaponConfig.SpreadAngleDegrees);

        if (Physics.Raycast(muzzlePos, shotDirection, out RaycastHit hit, _weaponConfig.MaxRange, _weaponConfig.HitMask))
        {
            if (hit.collider.TryGetComponent(out IDamageable damageable) || 
                (damageable = hit.collider.GetComponentInParent<IDamageable>()) != null)
            {
                var payload = new DamagePayload(
                    amount: _weaponConfig.DamagePerShot,
                    location: HitLocation.Core,
                    hitPoint: hit.point,
                    hitNormal: hit.normal,
                    impactDirection: shotDirection
                );
                damageable.TakeDamage(payload);
            }
        }

        AIPerceptionBus.EmitSound(muzzlePos, _weaponConfig.GunshotAcousticIntensity, gameObject);

        Vector3 endPoint = hit.collider ? hit.point : (muzzlePos + shotDirection * _weaponConfig.MaxRange);
        
        RpcPlayFireFeedback(muzzlePos, endPoint);
    }

    [ObserversRpc(runLocally: true)]
    private void RpcPlayFireFeedback(Vector3 muzzlePos, Vector3 endPoint)
    {
        // Trigger client-side tracer and muzzle flash
    }

    private Vector3 CalculateSpreadDirection(Vector3 baseDirection, float spreadAngleDegrees)
    {
        Vector2 circle = Random.insideUnitCircle * Mathf.Tan(spreadAngleDegrees * Mathf.Deg2Rad);
        Vector3 spreadDir = baseDirection + (transform.right * circle.x) + (transform.up * circle.y);
        return spreadDir.normalized;
    }

    private void ResetBurstCycle()
    {
        _isPausingBetweenBursts = false;
        _shotsFiredInCurrentBurst = 0;
        _targetBurstCount = Random.Range(_weaponConfig.MinBurstCount, _weaponConfig.MaxBurstCount + 1);
        _shotTimer = 0f;
    }
}
}
