using UnityEngine;

namespace Project.Combat.Weapons
{
[DisallowMultipleComponent]
public sealed class WeaponLookSway : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _swayRoot;

    [Header("Maximum Offsets")] 
    [SerializeField] private Vector2 _positionAmount = new(0.012f, 0.008f);
    [SerializeField] private Vector3 _rotationAmount = new(2f, 2f, 1.5f);
    [SerializeField, Min(1f)] private float _fullTurnRate = 180f;

    [Header("Spring Settings")]
    [SerializeField, Min(0.01f)] private float _positionFrequency = 3.5f;
    [SerializeField, Range(0.1f, 2f)] private float _positionDampingRatio = 1f;
    [SerializeField, Min(0.01f)] private float _rotationFrequency = 5.5f;
    [SerializeField, Range(0.1f, 2f)] private float _rotationDampingRatio = 1f;

    [Header("Action Weights")] 
    [SerializeField, Range(0f, 1f)] private float _aimMultiplier = 0.2f;
    [SerializeField, Min(1f)] private float _aimResponseMultiplier = 1.6f;
    [SerializeField, Min(0.01f)] private float _weightResponse = 12f;

    private Vector3 _restPosition;
    private Quaternion _restRotation;

    private Vector3 _position;
    private Vector3 _positionVelocity;
    private Vector3 _rotation;
    private Vector3 _rotationVelocity;

    private float _reloadWeight = 1f;

    private void Awake()
    {
        if (!_swayRoot)
            return;

        _restPosition = _swayRoot.localPosition;
        _restRotation = _swayRoot.localRotation;
    }

    public void Tick(Vector2 lookDelta, float aimWeight, bool reloading, float deltaTime)
    {
        if (!_swayRoot || !float.IsFinite(deltaTime) || deltaTime <= 0f) return;

        if (!float.IsFinite(lookDelta.x) || !float.IsFinite(lookDelta.y))
            lookDelta = Vector2.zero;

        Vector2 turn = lookDelta / (deltaTime * Mathf.Max(1f, _fullTurnRate));
        turn.x = Mathf.Clamp(turn.x, -1f, 1f);
        turn.y = Mathf.Clamp(turn.y, -1f, 1f);

        if (reloading) turn = Vector2.zero;

        Vector3 positionTarget = new Vector3(-turn.x * _positionAmount.x, -turn.y * _positionAmount.y, 0f);
        Vector3 rotationTarget = new Vector3(turn.y * _rotationAmount.x, -turn.x * _rotationAmount.y,
            -turn.x * _rotationAmount.z);

        aimWeight = Mathf.Clamp01(aimWeight);
        float responseScale = Mathf.Lerp(1f, _aimResponseMultiplier, aimWeight);

        Spring(ref _position, ref _positionVelocity, positionTarget, _positionFrequency * responseScale,
            _positionDampingRatio, deltaTime);
        Spring(ref _rotation, ref _rotationVelocity, rotationTarget, _rotationFrequency * responseScale,
            _rotationDampingRatio, deltaTime);

        _reloadWeight = Mathf.Lerp(_reloadWeight, reloading ? 0f : 1f, 1f - Mathf.Exp(-_weightResponse * deltaTime));
        float overallWeight = _reloadWeight * Mathf.Lerp(1f, _aimMultiplier, aimWeight);

        _swayRoot.SetLocalPositionAndRotation(
            _restPosition + (_position * overallWeight),
            _restRotation * Quaternion.Euler(_rotation * overallWeight)
        );
    }

    private static void Spring(ref Vector3 current, ref Vector3 velocity, Vector3 target, float frequency,
        float dampingRatio, float dt)
    {
        float f = Mathf.Max(0.01f, frequency);
        float zeta = Mathf.Max(0.01f, dampingRatio);

        float omega = 2f * Mathf.PI * f;

        Vector3 acceleration = (target - current) * (omega * omega) - (velocity * (2f * zeta * omega));

        velocity += acceleration * dt;
        current += velocity * dt;
    }

    public void ResetPose()
    {
        _position = _positionVelocity = _rotation = _rotationVelocity = Vector3.zero;
        _reloadWeight = 1f;
        if (_swayRoot) _swayRoot.SetLocalPositionAndRotation(_restPosition, _restRotation);
    }

    private void OnDisable() => ResetPose();
}
}