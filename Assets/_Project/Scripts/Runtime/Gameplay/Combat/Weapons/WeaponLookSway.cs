using UnityEngine;

namespace Project.Combat.Weapons
{
    [DisallowMultipleComponent]
    public sealed class WeaponLookSway : MonoBehaviour
    {
        [SerializeField] private Transform _swayRoot;
        
        [Header("Maximum offsets at full turn rate")]
        [SerializeField] private Vector2 _positionAmount = new Vector2(0.012f, 0.008f);
        [SerializeField] private Vector3 _rotationAmount = new Vector3(2f, 2f, 1.5f);
        [SerializeField, Min(1f)] private float _fullTurnRate = 180f;
        
        [Header("Spring response (Hz)")]
        [SerializeField, Min(0.01f)] private float _positionFrequency = 3.5f;
        [SerializeField, Min(0.01f)] private float _rotationFrequency = 5.5f;
        
        [Header("Action weights")]
        [SerializeField, Range(0f, 1f)] private float _aimMultiplier = 0.2f;
        [SerializeField, Min(1f)] private float _aimResponseMultiplier = 1.6f;
        
        [SerializeField, Min(0.01f)] private float _weightResponse = 12f;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _position, _positionVelocity, _rotation, _rotationVelocity;
        private float _reloadWeight = 1f;

        private void Awake()
        {
            if (!_swayRoot) return;
            _restPosition = _swayRoot.localPosition;
            _restRotation = _swayRoot.localRotation;
        }

        public void Tick(Vector2 lookDelta, float aimWeight, bool reloading, float deltaTime)
        {
            if (!_swayRoot || !float.IsFinite(deltaTime) || deltaTime <= 0f) return;
            if (!float.IsFinite(lookDelta.x) || !float.IsFinite(lookDelta.y)) lookDelta = Vector2.zero;
            var turn = lookDelta / (deltaTime * Mathf.Max(1f, _fullTurnRate));
            turn.x = Mathf.Clamp(turn.x, -1f, 1f);
            turn.y = Mathf.Clamp(turn.y, -1f, 1f);
            if (reloading) turn = Vector2.zero;
            var positionTarget = new Vector3(-turn.x * _positionAmount.x, -turn.y * _positionAmount.y, 0f);
            var rotationTarget = new Vector3(turn.y * _rotationAmount.x, -turn.x * _rotationAmount.y, -turn.x * _rotationAmount.z);
            aimWeight = Mathf.Clamp01(aimWeight);
            var response = Mathf.Lerp(1f, _aimResponseMultiplier, aimWeight);
            Spring(ref _position, ref _positionVelocity, positionTarget, _positionFrequency * response, deltaTime);
            Spring(ref _rotation, ref _rotationVelocity, rotationTarget, _rotationFrequency * response, deltaTime);
            _reloadWeight = Mathf.Lerp(_reloadWeight, reloading ? 0f : 1f, 1f - Mathf.Exp(-_weightResponse * deltaTime));
            var weight = _reloadWeight * Mathf.Lerp(1f, _aimMultiplier, aimWeight);
            _swayRoot.SetLocalPositionAndRotation(_restPosition + _position * weight,
                _restRotation * Quaternion.Euler(_rotation * weight));
        }

        private static void Spring(ref Vector3 value, ref Vector3 velocity, Vector3 target, float frequency, float dt)
        {
            var omega = 2f * Mathf.PI * Mathf.Max(0.01f, frequency);
            var offset = value - target;
            var coefficient = velocity + omega * offset;
            var decay = Mathf.Exp(-omega * dt);
            value = target + (offset + coefficient * dt) * decay;
            velocity = (velocity - coefficient * (omega * dt)) * decay;
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
