using Project.Core;
using PurrNet;
using UnityEngine;

namespace Project.Player
{
    public class FPSCameraController : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _maxPitch = 85f;
        [SerializeField] private float _minPitch = -85f;

        [Header("References")]
        [SerializeField] private Transform _pitchPivot;
        [SerializeField] private PlayerFacade _playerFacade;

        private float _currentPitch;
        private float _currentYaw;

        public float CurrentYaw => _currentYaw;
        public float CurrentPitch => _currentPitch;
        public Quaternion CameraRotation => Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        public Quaternion PlanarRotation => Quaternion.Euler(0f, _currentYaw, 0f);

        private void Awake()
        {
            if (!_playerFacade)
                _playerFacade = GetComponentInParent<PlayerFacade>();

            _currentYaw = transform.eulerAngles.y;
        }

        protected override void OnSpawned()
        {
            if (!isOwner)
            {
                enabled = false;
                return;
            }

            _currentYaw = transform.eulerAngles.y;

            if (CameraRig.Instance && _pitchPivot)
                CameraRig.Instance.Follow(_pitchPivot);
        }

        protected override void OnDespawned()
        {
            if (isOwner && CameraRig.Instance)
                CameraRig.Instance.StopFollowing();
        }

        private void Update()
        {
            if (!isOwner)
                return;

            HandleLookInput();
            ApplyTransforms();
        }

        private void HandleLookInput()
        {
            if (!_playerFacade) return;

            Vector2 look = _playerFacade.CurrentInput.LookVector;
            _currentYaw += look.x;
            _currentPitch -= look.y;
            _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);
        }

        private void ApplyTransforms()
        {
            if (_pitchPivot)
            {
                _pitchPivot.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            }
        }
    }
}