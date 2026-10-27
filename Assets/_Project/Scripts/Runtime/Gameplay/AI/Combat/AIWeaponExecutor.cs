using System;
using Project.AI.Blackboard;
using Project.AI.Locomotion;
using Project.AI.Perception;
using Project.Combat;
using Project.Combat.Core;
using Project.Combat.Health;
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
    [SerializeField] private Transform _muzzlePoint;

    private AIWeaponConfig _weaponConfig => _blackboard.ArchetypeConfig.WeaponConfig;

    private float _shotTimer;
    private HealthComponent _health;
    private int _shotsFiredInCurrentBurst;
    private int _targetBurstCount;
    private readonly Collider[] _muzzleOverlaps = new Collider[32];
    private readonly RaycastHit[] _firingObstructions = new RaycastHit[32];

    public event Action OnBurstCompleted;
    public event Action<Vector3, Vector3> OnShotFired;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>(true);
        if (!_blackboard)
            _blackboard = GetComponent<AIBlackboard>();

        if (!_aimController)
            _aimController = GetComponent<AIAimLookController>();
    }

    protected override void OnSpawned()
    {
        if (!isServer || !_blackboard || !_blackboard.TryInitializeArchetype() || !_aimController)
        {
            enabled = false;
            return;
        }

        enabled = true;
        ResetBurstCycle();
    }

    protected override void OnDespawned() => ResetExecutionState();

    private void OnDisable() => ResetExecutionState();

    private void ResetExecutionState()
    {
        _shotTimer = 0f;
        _shotsFiredInCurrentBurst = 0;
        _targetBurstCount = 0;
    }

    private void Update()
    {
        if (!isServer || !_blackboard || !_blackboard.IsArchetypeReady || !_aimController || (_health && _health.IsDead))
            return;

        UpdateBurstCadence(Time.deltaTime);
    }

    private void UpdateBurstCadence(float deltaTime)
    {
        bool canShoot = _blackboard.Target && _blackboard.WantsToShoot && _aimController.IsAimAligned && _blackboard.HasLineOfSight;

        if (!canShoot)
            return;

        _shotTimer -= deltaTime;
        if (_shotTimer <= 0f)
        {

            float shotInterval = 60.0f / _weaponConfig.RoundsPerMinute;
            if (!FireSingleShot())
                return;
            _shotTimer = shotInterval;
            _shotsFiredInCurrentBurst++;

            if (_shotsFiredInCurrentBurst >= _targetBurstCount)
            {
                ResetBurstCycle();
                OnBurstCompleted?.Invoke();
            }
        }
    }

    /// <summary>Read-only firing geometry using remembered target data, shared with predicted peek validation.</summary>
    public bool TryGetFiringGeometry(out Vector3 muzzlePosition, out Vector3 aimPoint)
    {
        muzzlePosition = default;
        aimPoint = default;
        if (!_blackboard || !_blackboard.IsArchetypeReady || !_blackboard.Target || !_aimController)
            return false;
        Vector3 muzzle = _muzzlePoint ? _muzzlePoint.position : transform.position + Vector3.up * 1.4f;
        Vector3 aim = _aimController.GetAimPoint(_blackboard.TargetPosition);
        float distanceSqr = (aim - muzzle).sqrMagnitude;
        if (!Finite(muzzle) || !Finite(aim) || !float.IsFinite(distanceSqr) || distanceSqr <= 0.000001f)
            return false;
        muzzlePosition = muzzle;
        aimPoint = aim;
        return true;
    }

    /// <summary>Main-thread query. Ignores own colliders and triggers; full query buffers fail conservatively.</summary>
    public bool HasClearFiringLine(Vector3 origin, Vector3 aim)
    {
        if (!_blackboard || !_blackboard.IsArchetypeReady || !Finite(origin) || !Finite(aim))
            return false;
        Vector3 ray = aim - origin;
        float distance = ray.magnitude;
        if (!float.IsFinite(distance) || distance < 0.001f || distance > _weaponConfig.MaxRange)
            return false;
        LayerMask mask = _blackboard.ArchetypeConfig.VisionConfig.ObstacleMask;
        Transform self = _blackboard.transform;
        int count = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(Physics.defaultContactOffset, 0.001f),
            _muzzleOverlaps, mask, QueryTriggerInteraction.Ignore);
        bool blocked = count == _muzzleOverlaps.Length;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _muzzleOverlaps[i];
            if (hit && !hit.transform.IsChildOf(self)) blocked = true;
            _muzzleOverlaps[i] = null;
        }
        if (blocked) return false;
        count = Physics.RaycastNonAlloc(origin, ray / distance, _firingObstructions, distance, mask, QueryTriggerInteraction.Ignore);
        blocked = count == _firingObstructions.Length;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _firingObstructions[i].collider;
            if (hit && !hit.transform.IsChildOf(self)) blocked = true;
            _firingObstructions[i] = default;
        }
        return !blocked;
    }

    private bool FireSingleShot()
    {
        if (!TryGetFiringGeometry(out Vector3 muzzlePos, out Vector3 aimPoint) || !HasClearFiringLine(muzzlePos, aimPoint))
            return false;

        Vector3 aimDirection = (aimPoint - muzzlePos).normalized;
        Vector3 shotDirection = CalculateSpreadDirection(aimDirection, _weaponConfig.SpreadAngleDegrees);

        if (Physics.Raycast(muzzlePos, shotDirection, out RaycastHit hit, _weaponConfig.MaxRange, _weaponConfig.HitMask,
                QueryTriggerInteraction.Ignore))
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
        return true;
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    [ObserversRpc(runLocally: true)]
    private void RpcPlayFireFeedback(Vector3 muzzlePos, Vector3 endPoint)
    {
        OnShotFired?.Invoke(muzzlePos, endPoint);
    }

    private Vector3 CalculateSpreadDirection(Vector3 baseDirection, float spreadAngleDegrees)
    {
        Vector2 circle = Random.insideUnitCircle * Mathf.Tan(spreadAngleDegrees * Mathf.Deg2Rad);
        Vector3 spreadDir = baseDirection + (transform.right * circle.x) + (transform.up * circle.y);
        return spreadDir.normalized;
    }

    private void ResetBurstCycle()
    {
        _shotsFiredInCurrentBurst = 0;
        _targetBurstCount = Random.Range(_weaponConfig.MinBurstCount, _weaponConfig.MaxBurstCount + 1);
        _shotTimer = 0f;
    }
}
}
