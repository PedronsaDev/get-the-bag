using UnityEngine;

namespace Project.Combat.Weapons
{
    [DisallowMultipleComponent]
    public sealed class WeaponMovementSway : MonoBehaviour
    {
        [SerializeField] private Transform _movementRoot;

        [Header("Offsets at Full Movement Input")]
        [Tooltip("X responds to strafing; Y and Z respond to forward/backward input.")]
        [SerializeField] private Vector3 _positionAmount = new Vector3(-0.012f, -0.004f, -0.015f);
        [Tooltip("Pitch responds to forward/backward input; yaw and roll respond to strafing.")]
        [SerializeField] private Vector3 _rotationAmount = new Vector3(1.4f, -1f, -2.5f);

        [Header("Target Response")]
        [SerializeField, Min(0.01f)] private float _positionTargetResponse = 8f;
        [SerializeField, Min(0.01f)] private float _rotationTargetResponse = 12f;

        [Header("Spring Dynamics")]
        [SerializeField, Min(0.01f)] private float _positionFrequency = 2.4f;
        [SerializeField, Range(0.1f, 2f)] private float _positionDampingRatio = 0.9f;
        [SerializeField, Min(0.01f)] private float _rotationFrequency = 3.5f;
        [SerializeField, Range(0.1f, 2f)] private float _rotationDampingRatio = 0.85f;

        [Header("Action Weights")]
        [SerializeField, Range(0f, 1f)] private float _aimMultiplier = 0.15f;
        [SerializeField, Min(0.01f)] private float _weightResponse = 12f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _positionTarget, _rotationTarget;
        private Vector3 _position, _positionVelocity, _rotation, _rotationVelocity;
        private float _reloadWeight = 1f;

        private void Awake()
        {
            if (!_movementRoot) return;
            _restPosition = _movementRoot.localPosition;
            _restRotation = _movementRoot.localRotation;
        }

        public void Tick(Vector2 moveInput, float aimWeight, bool reloading, float deltaTime)
        {
            if (!_movementRoot || !float.IsFinite(deltaTime) || deltaTime <= 0f) return;
            if (!float.IsFinite(moveInput.x) || !float.IsFinite(moveInput.y)) moveInput = Vector2.zero;
            moveInput = reloading ? Vector2.zero : Vector2.ClampMagnitude(moveInput, 1f);
            aimWeight = float.IsFinite(aimWeight) ? Mathf.Clamp01(aimWeight) : 0f;

            var positionTarget = new Vector3(moveInput.x * _positionAmount.x,
                moveInput.y * _positionAmount.y, moveInput.y * _positionAmount.z);
            var rotationTarget = new Vector3(moveInput.y * _rotationAmount.x,
                moveInput.x * _rotationAmount.y, moveInput.x * _rotationAmount.z);
            _positionTarget = Vector3.Lerp(_positionTarget, positionTarget,
                1f - Mathf.Exp(-_positionTargetResponse * deltaTime));
            _rotationTarget = Vector3.Lerp(_rotationTarget, rotationTarget,
                1f - Mathf.Exp(-_rotationTargetResponse * deltaTime));
            WeaponSpring.Step(ref _position, ref _positionVelocity, _positionTarget,
                _positionFrequency, _positionDampingRatio, deltaTime);
            WeaponSpring.Step(ref _rotation, ref _rotationVelocity, _rotationTarget,
                _rotationFrequency, _rotationDampingRatio, deltaTime);

            _reloadWeight = Mathf.Lerp(_reloadWeight, reloading ? 0f : 1f,
                1f - Mathf.Exp(-_weightResponse * deltaTime));
            var weight = _reloadWeight * Mathf.Lerp(1f, _aimMultiplier, aimWeight);
            _movementRoot.SetLocalPositionAndRotation(_restPosition + _position * weight,
                _restRotation * Quaternion.Euler(_rotation * weight));
        }

        public void ResetPose()
        {
            _positionTarget = _rotationTarget = Vector3.zero;
            _position = _positionVelocity = _rotation = _rotationVelocity = Vector3.zero;
            _reloadWeight = 1f;
            if (_movementRoot) _movementRoot.SetLocalPositionAndRotation(_restPosition, _restRotation);
        }

        private void OnDisable() => ResetPose();
    }
}
