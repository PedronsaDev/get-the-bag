using Project.Core;
using Project.Player.Input;
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
    [SerializeField] private LocalPlayerInputProvider _inputProvider;
    [SerializeField] private LocomotionController _locomotionController;
    [SerializeField] private PlayerFacade _playerFacade;

    private Camera _cam;

    private float _currentPitch;
    private float _currentYaw;

    public Transform PitchPivot => _pitchPivot;
    public float CurrentYaw => _currentYaw;
    public float CurrentPitch => _currentPitch;
    public Quaternion CameraRotation => Quaternion.Euler(_currentPitch, _currentYaw, 0f);
    public Quaternion PlanarRotation => Quaternion.Euler(0f, _currentYaw, 0f);

    private LocomotionState CurrentLocomotionState => _locomotionController
        ? _locomotionController.State
        : (_playerFacade ? _playerFacade.LocomotionState : default);

    public void AddPitchRecoil(float recoilDegrees)
    {
        _currentPitch -= recoilDegrees;
        _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);
    }

    private void Awake()
    {
        if (!_inputProvider)
            _inputProvider = GetComponent<LocalPlayerInputProvider>() ??
                             GetComponentInParent<LocalPlayerInputProvider>() ??
                             GetComponentInChildren<LocalPlayerInputProvider>();
        if (!_locomotionController)
            _locomotionController = GetComponent<LocomotionController>() ??
                                    GetComponentInParent<LocomotionController>() ??
                                    GetComponentInChildren<LocomotionController>();
        if (!_playerFacade)
            _playerFacade = GetComponent<PlayerFacade>() ??
                            GetComponentInParent<PlayerFacade>() ?? GetComponentInChildren<PlayerFacade>();
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
        var loco = CurrentLocomotionState;
        var wantsFastFOV = loco.IsSprinting || loco.IsSliding;
        var targetFOV = wantsFastFOV ? _sprintFOV : _baseFOV;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, dt * _fovSmoothness);
    }

    private void HandleLookInput()
    {
        Vector2 look = _inputProvider
            ? _inputProvider.GetCurrentInput().LookVector
            : (_playerFacade ? _playerFacade.CurrentInput.LookVector : Vector2.zero);

        _currentYaw += look.x;
        _currentPitch -= look.y;
        _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);
    }

    private void ApplyTransforms(float dt)
    {
        var targetY = 0f;
        var loco = CurrentLocomotionState;

        if (loco.IsDowned)
            targetY = _downedHeight;
        else if (loco.IsSliding)
            targetY = _slidingHeight;
        else if (loco.IsCrouching)
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