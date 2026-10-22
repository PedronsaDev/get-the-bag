using Project.Logging;
using UnityEngine;

namespace Project.Combat.Weapons
{
[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponInstance))]
public sealed class WeaponRecoil : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private Transform _recoilRoot;
    [SerializeField] private WeaponInstance _weapon;

    [Header("Base Impulses")] 
    [SerializeField] private Vector3 _positionImpulse = new(0f, 0f, -0.035f);
    [SerializeField] private Vector3 _rotationImpulse = new(-2.5f, 0f, 0f);

    [Header("Shot Randomization Range (+/-)")] 
    [SerializeField] private Vector3 _positionImpulseRandomness = new(0.002f, 0.002f, 0.005f);
    [SerializeField] private Vector3 _rotationImpulseRandomness = new(0.5f, 1.2f, 0.8f);

    [Header("Spring Dynamics")] 
    [SerializeField, Min(0.01f)] private float _positionFrequency = 14.5f;
    [SerializeField, Range(0.1f, 2f)] private float _positionDampingRatio = 0.85f;
    [SerializeField, Min(0.01f)] private float _rotationFrequency = 16.5f;
    [SerializeField, Range(0.1f, 2f)] private float _rotationDampingRatio = 0.8f;

    [Header("Accumulation Limits")] 
    [SerializeField] private Vector3 _maxPosition = new(0.012f, 0.012f, 0.045f);
    [SerializeField] private Vector3 _maxRotation = new(8f, 5f, 4f);
    [SerializeField] private Vector3 _maxPositionVelocity = new(0.5f, 0.5f, 1.5f);
    [SerializeField] private Vector3 _maxRotationVelocity = new(80f, 50f, 50f);

    [Header("Action Weights")] 
    [SerializeField, Range(0f, 1f)] private float _aimPositionMultiplier = 0.5f;
    [SerializeField, Range(0f, 1f)] private float _aimRotationMultiplier = 0.5f;
    [SerializeField, Min(1f)] private float _reloadRecoveryMultiplier = 1.6f;

    private Vector3 _restPosition;
    private Quaternion _restRotation;
    private WeaponAimPose _aimPose;

    private Vector3 _position;
    private Vector3 _positionVelocity;
    private Vector3 _rotation;
    private Vector3 _rotationVelocity;
    private bool _hasRestPose;

    private void Awake()
    {
        if (!_weapon)
            _weapon = GetComponent<WeaponInstance>();

        _aimPose = GetComponent<WeaponAimPose>();

        CachePositionsAndRotations();

        if (!_recoilRoot)
            AppLog.LogWarning(LogChannel.Combat, "Weapon recoil requires an assigned RecoilRoot.", this);
    }

    private void OnEnable()
    {
        if (_weapon) _weapon.Fired += OnFired;
    }

    private void OnDisable()
    {
        if (_weapon) _weapon.Fired -= OnFired;
        ResetPose();
    }

    public void CachePositionsAndRotations()
    {
        if (!_recoilRoot) return;

        _restPosition = _recoilRoot.localPosition;
        _restRotation = _recoilRoot.localRotation;
        _hasRestPose = true;
    }

    private void OnFired()
    {
        if (!_recoilRoot) return;

        float aimWeight = (_aimPose && _aimPose.isActiveAndEnabled)
            ? Mathf.Clamp01(_aimPose.EvaluatedWeight)
            : 0f;

        Vector3 posImpulse = _positionImpulse + new Vector3(
            Random.Range(-_positionImpulseRandomness.x, _positionImpulseRandomness.x),
            Random.Range(-_positionImpulseRandomness.y, _positionImpulseRandomness.y),
            Random.Range(-_positionImpulseRandomness.z, _positionImpulseRandomness.z)
        );

        Vector3 rotImpulse = _rotationImpulse + new Vector3(
            Random.Range(-_rotationImpulseRandomness.x, _rotationImpulseRandomness.x),
            Random.Range(-_rotationImpulseRandomness.y, _rotationImpulseRandomness.y),
            Random.Range(-_rotationImpulseRandomness.z, _rotationImpulseRandomness.z)
        );

        float posWeight = Mathf.Lerp(1f, _aimPositionMultiplier, aimWeight);
        float rotWeight = Mathf.Lerp(1f, _aimRotationMultiplier, aimWeight);

        _positionVelocity = ClampAxes(_positionVelocity + (posImpulse * posWeight), _maxPositionVelocity);
        _rotationVelocity = ClampAxes(_rotationVelocity + (rotImpulse * rotWeight), _maxRotationVelocity);
    }

    public void Tick(bool reloading, float deltaTime)
    {
        if (!_recoilRoot || !_hasRestPose || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            return;

        float recovery = reloading ? Mathf.Max(1f, _reloadRecoveryMultiplier) : 1f;

        WeaponSpring.Step(ref _position, ref _positionVelocity, Vector3.zero,
            _positionFrequency * recovery, _positionDampingRatio, deltaTime);

        WeaponSpring.Step(ref _rotation, ref _rotationVelocity, Vector3.zero,
            _rotationFrequency * recovery, _rotationDampingRatio, deltaTime);

        LimitTravel(ref _position, ref _positionVelocity, _maxPosition);
        LimitTravel(ref _rotation, ref _rotationVelocity, _maxRotation);

        _recoilRoot.SetLocalPositionAndRotation(
            _restPosition + _position,
            _restRotation * Quaternion.Euler(_rotation)
        );
    }

    private static Vector3 ClampAxes(Vector3 value, Vector3 limits)
    {
        return new Vector3(
            Mathf.Clamp(value.x, -Mathf.Abs(limits.x), Mathf.Abs(limits.x)),
            Mathf.Clamp(value.y, -Mathf.Abs(limits.y), Mathf.Abs(limits.y)),
            Mathf.Clamp(value.z, -Mathf.Abs(limits.z), Mathf.Abs(limits.z))
        );
    }

    private static void LimitTravel(ref Vector3 value, ref Vector3 velocity, Vector3 limits)
    {
        Vector3 clamped = ClampAxes(value, limits);

        if (!Mathf.Approximately(clamped.x, value.x) && (value.x * velocity.x > 0f)) velocity.x = 0f;
        if (!Mathf.Approximately(clamped.y, value.y) && (value.y * velocity.y > 0f)) velocity.y = 0f;
        if (!Mathf.Approximately(clamped.z, value.z) && (value.z * velocity.z > 0f)) velocity.z = 0f;

        value = clamped;

        if (value.sqrMagnitude < 1e-10f && velocity.sqrMagnitude < 1e-8f)
        {
            value = Vector3.zero;
            velocity = Vector3.zero;
        }
    }

    public void ResetPose()
    {
        _position = _positionVelocity = _rotation = _rotationVelocity = Vector3.zero;
        if (_recoilRoot && _hasRestPose)
            _recoilRoot.SetLocalPositionAndRotation(_restPosition, _restRotation);
    }
}
}