using Project.Core;
using PurrNet;
using UnityEngine;

namespace Project.Player
{
    public class FPSCameraController : NetworkBehaviour
    {
        [Header("Settings")]
        
        [Header("Dynamic FOV")]
        [SerializeField] private float _baseFOV = 90f;
        [SerializeField] private float _sprintFOV = 105f;
        [SerializeField] private float _fovSmoothness = 8f;
        
        [Header("Dynamic Height")]
        [SerializeField] private float _standingHeight = 1.75f;
        [SerializeField] private float _crouchingHeight = 1.1f;
        [SerializeField] private float _slidingHeight = 0.8f;
        [SerializeField] private float _downedHeight = 0.5f;
        [SerializeField] private float _heightSmoothness = 10f;
        
        [Header("Look Settings")]
        [SerializeField] private float _maxPitch = 85f;
        [SerializeField] private float _minPitch = -85f;

        [Header("References")]
        [SerializeField] private Transform _pitchPivot;
        [SerializeField] private Transform _cameraTargetPoint;
        [SerializeField] private PlayerFacade _playerFacade;
        
        private Camera _cam;
        
        private float _currentPitch;
        private float _currentYaw;

        public Transform PitchPivot => _pitchPivot;
        public float CurrentYaw => _currentYaw;
        public float CurrentPitch => _currentPitch;
        public Quaternion CameraRotation => Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        public Quaternion PlanarRotation => Quaternion.Euler(0f, _currentYaw, 0f);

        public void AddPitchRecoil(float recoilDegrees)
        {
            _currentPitch -= recoilDegrees;
            _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);
        }

        protected override void OnSpawned()
        {
            this.enabled = isOwner;
            
            if (!isOwner)
                return;

            _currentYaw = transform.eulerAngles.y;

            if (CameraRig.Instance && _cameraTargetPoint)
            {
                CameraRig.Instance.Follow(_cameraTargetPoint);
                _cam = CameraRig.Instance.Camera;
            }
        }

        protected override void OnDespawned()
        {
            if (isOwner && CameraRig.Instance)
            {
                CameraRig.Instance.StopFollowing();
            }
        }

        private void Update()
        {
            if (!isOwner)
                return;

            var dt = Time.deltaTime;
            HandleLookInput();
            HandleDynamicFOV(dt);
            ApplyTransforms(dt);
        }

        private void HandleDynamicFOV(float dt)
        {
            var wantsFastFOV = _playerFacade.LocomotionState.IsSprinting ||_playerFacade.LocomotionState.IsSliding;
            var targetFOV = wantsFastFOV ? _sprintFOV : _baseFOV;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, dt * _fovSmoothness);
        }

        private void HandleLookInput()
        {
            if (!_playerFacade) return;

            var look = _playerFacade.CurrentInput.LookVector;
            
            _currentYaw += look.x;
            _currentPitch -= look.y;
            _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);
        }

        private void ApplyTransforms(float dt)
        {
            var targetY = 0f;
            
            if (_playerFacade && _playerFacade.LocomotionState.IsDowned)
                targetY = _downedHeight;
            else if (_playerFacade && _playerFacade.LocomotionState.IsSliding)
                targetY = _slidingHeight;
            else if (_playerFacade && _playerFacade.LocomotionState.IsCrouching)
                targetY = _crouchingHeight;
            else
                targetY = _standingHeight;

            Vector3 pitchNodePos = _pitchPivot.localPosition;
            pitchNodePos.y = Mathf.Lerp(pitchNodePos.y, targetY, dt * _heightSmoothness);
            _pitchPivot.localPosition = pitchNodePos;
            
            _pitchPivot.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            
        }
    }
}