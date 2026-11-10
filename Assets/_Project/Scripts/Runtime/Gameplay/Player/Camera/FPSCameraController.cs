using Project.Combat.Core;
using Project.Combat.Health;
using Project.Core;
using Project.Player.Input;
using Project.Player.Locomotion;
using PurrNet;
using UnityEngine;

namespace Project.Player.FPSCamera
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
    [Tooltip("How far the camera dips while pulling up over a ledge.")]
    [SerializeField] private float _vaultDip = 0.3f;

    [Header("Look Settings")]
    [SerializeField] private float _maxPitch = 85f;
    [SerializeField] private float _minPitch = -85f;

    [Header("Fire Recoil")]
    [Tooltip("Master scale for fire kick and damage flinch (accessibility). 0 turns the camera effects off.")]
    [Range(0f, 2f)] [SerializeField] private float _effectsIntensity = 1f;
    [Tooltip("Permanent view climb per shot, in degrees. The aim really moves, so bullets follow it.")]
    [SerializeField] private float _climbPerShot = 0.35f;
    [Tooltip("Random sideways drift per shot, in degrees (+/-).")]
    [SerializeField] private float _yawDriftPerShot = 0.12f;
    [Tooltip("Most climb that can pile up before the view stops rising, in degrees.")]
    [SerializeField] private float _maxClimb = 6f;
    [Tooltip("Seconds after the last shot before the view starts settling back.")]
    [SerializeField] private float _recoverDelay = 0.08f;
    [Tooltip("How fast the leftover climb settles back down (higher = faster).")]
    [SerializeField] private float _recoverRate = 7f;
    [Tooltip("How quickly each shot's climb is applied (higher = snappier, lower = smoother). About 1/rate seconds.")]
    [SerializeField] private float _climbApplyRate = 28f;
    [Tooltip("Fire kick scale when fully aimed down sights.")]
    [Range(0f, 1f)] [SerializeField] private float _aimKickMultiplier = 0.6f;
    [Tooltip("Tiny FOV pulse per shot, in degrees. Sells the punch of each round.")]
    [SerializeField] private float _fireFovKick = 1.2f;
    [SerializeField] private float _fovKickMax = 3.5f;
    [SerializeField] private float _fovKickRecovery = 12f;

    [Header("Damage Flinch")]
    [Tooltip("Damage that gives a full-strength flinch.")]
    [SerializeField] private float _fullFlinchDamage = 40f;
    [Range(0f, 1f)] [SerializeField] private float _minFlinch = 0.25f;

    [Header("References")] 
    [SerializeField] private Transform _pitchPivot;
    [SerializeField] private Transform _cameraTargetPoint;
    [SerializeField] private LocalPlayerInputProvider _inputProvider;
    [SerializeField] private LocomotionController _locomotionController;
    [SerializeField] private PlayerFacade _playerFacade;
    
    [Header("Camera References")]
    [SerializeField] private FPSCameraTilt _cameraTilt;
    [SerializeField] private FPSCameraImpact _cameraImpact;

    private UnityEngine.Camera _cam;
    private float _aimProgress;
    private float _aimFov = 75f;
    private float _fovCurrent;
    private float _fovKick;
    private float _recoilDebt;
    private float _pendingClimb;
    private float _lastShotTime = float.NegativeInfinity;
    private HealthComponent _health;

    public void SetAimPresentation(float progress, float fov)
    {
        _aimProgress = Mathf.Clamp01(progress);
        _aimFov = Mathf.Clamp(fov, 20f, 120f);
    }

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

    /// <summary>
    /// Raises the view by <paramref name="recoilDegrees"/>. The climb is remembered so it can settle back
    /// down later, unless the player pulls down against it first.
    /// </summary>
    public void AddPitchRecoil(float recoilDegrees)
    {
        if (recoilDegrees <= 0f) return;

        // Queued, then eased in over a few frames by ApplyPendingClimb so each shot lifts the view smoothly.
        _pendingClimb += Mathf.Min(recoilDegrees, Mathf.Max(0f, _maxClimb - _recoilDebt - _pendingClimb));
    }

    private void ApplyPendingClimb(float dt)
    {
        if (_pendingClimb <= 0f) return;

        float step = _pendingClimb * (1f - Mathf.Exp(-_climbApplyRate * dt));
        if (_pendingClimb - step < 0.001f) step = _pendingClimb;

        _pendingClimb -= step;
        _currentPitch = Mathf.Clamp(_currentPitch - step, _minPitch, _maxPitch);
        _recoilDebt += step;
    }

    /// <summary>Call once per shot fired by the local player. Drives climb, drift, snap and FOV punch.</summary>
    public void OnWeaponFired()
    {
        if (!isOwner || _effectsIntensity <= 0f) return;

        float scale = Mathf.Lerp(1f, _aimKickMultiplier, Mathf.SmoothStep(0f, 1f, _aimProgress)) * _effectsIntensity;

        AddPitchRecoil(_climbPerShot * scale);
        _currentYaw += Random.Range(-_yawDriftPerShot, _yawDriftPerShot) * scale;
        _fovKick = Mathf.Min(_fovKick + _fireFovKick * scale, _fovKickMax);
        _lastShotTime = Time.time;

        if (_cameraImpact) _cameraImpact.AddFireKick(scale);
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

        _fovCurrent = _cam ? _cam.fieldOfView : _baseFOV;
        _fovKick = 0f;
        _recoilDebt = 0f;
        _pendingClimb = 0f;

        _health = _playerFacade ? _playerFacade.HealthComponent : null;
        if (_health) _health.OnDamaged += HandleDamaged;
    }

    protected override void OnDespawned()
    {
        _aimProgress = 0f;
        _fovKick = 0f;
        _recoilDebt = 0f;
        _pendingClimb = 0f;

        if (_health)
        {
            _health.OnDamaged -= HandleDamaged;
            _health = null;
        }

        if (isOwner && CameraRig.Instance)
        {
            CameraRig.Instance.StopFollowing();
        }
    }

    private void HandleDamaged(DamageResult result)
    {
        if (!isOwner || _effectsIntensity <= 0f || !_cameraImpact || result.EffectiveDamage <= 0f) return;

        float strength = Mathf.Clamp01(result.EffectiveDamage / Mathf.Max(1f, _fullFlinchDamage));
        strength = Mathf.Max(strength, _minFlinch) * _effectsIntensity;

        // HitNormal points out of the body toward whoever shot, so its side tells us which way to roll.
        var local = Quaternion.Inverse(PlanarRotation) * result.HitNormal;
        _cameraImpact.AddDamageKick(strength, Mathf.Clamp(local.x * 2f, -1f, 1f));
    }

    private void Update()
    {
        if (!isOwner)
            return;

        var dt = Time.deltaTime;
        
        HandleLookInput();
        ApplyPendingClimb(dt);
        RecoverRecoil(dt);
        HandleDynamicFOV(dt);
        ApplyTransforms(dt);

        if (_cameraTilt) _cameraTilt.OnUpdate(dt);
        if (_cameraImpact) _cameraImpact.OnUpdate(dt, _aimProgress);
    }

    private void HandleDynamicFOV(float dt)
    {
        if (!_cam) return;

        var loco = CurrentLocomotionState;
        var wantsFastFOV = loco.IsSprinting || loco.IsSliding || loco.Phase == LocomotionPhase.Vaulting;
        var targetFOV = wantsFastFOV ? _sprintFOV : _baseFOV;
        targetFOV = Mathf.Lerp(targetFOV, _aimFov, Mathf.SmoothStep(0f, 1f, _aimProgress));

        // Smooth the base FOV on its own, then add the per-shot punch on top so the two do not fight.
        _fovCurrent = Mathf.Lerp(_fovCurrent, targetFOV, 1f - Mathf.Exp(-_fovSmoothness * dt));
        _fovKick *= Mathf.Exp(-_fovKickRecovery * dt);
        if (_fovKick < 0.001f) _fovKick = 0f;

        _cam.fieldOfView = _fovCurrent + _fovKick;
    }

    private void HandleLookInput()
    {
        Vector2 look = _inputProvider
            ? _inputProvider.GetCurrentInput().LookVector
            : (_playerFacade ? _playerFacade.CurrentInput.LookVector : Vector2.zero);

        // Pulling down against the climb pays off the debt, so the view does not spring back afterwards.
        if (look.y < 0f && _recoilDebt > 0f)
            _recoilDebt = Mathf.Max(0f, _recoilDebt + look.y);

        _currentYaw += look.x;
        _currentPitch -= look.y;
        _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);
    }

    // After a short pause the view eases back down by whatever climb the player has not countered.
    private void RecoverRecoil(float dt)
    {
        if (_recoilDebt <= 0f || _pendingClimb > 0f || Time.time < _lastShotTime + _recoverDelay) return;

        float step = Mathf.Max(_recoilDebt * (1f - Mathf.Exp(-_recoverRate * dt)), 0.5f * dt);
        step = Mathf.Min(step, _recoilDebt);

        _currentPitch = Mathf.Clamp(_currentPitch + step, _minPitch, _maxPitch);
        _recoilDebt -= step;
    }

    private void ApplyTransforms(float dt)
    {
        float targetY;
        var loco = CurrentLocomotionState;

        if (loco.IsDowned)
            targetY = _downedHeight;
        else if (loco.IsSliding)
            targetY = _slidingHeight;
        else if (loco.IsCrouching)
            targetY = _crouchingHeight;
        else
            targetY = _standingHeight;

        // Pull-up feel: camera drops toward the ledge mid-vault, then recovers as the body clears it.
        if (loco.Phase == LocomotionPhase.Vaulting && loco.VaultDuration > 0.01f)
        {
            float vaultProgress = Mathf.Clamp01(loco.VaultTimer / loco.VaultDuration);
            targetY -= _vaultDip * Mathf.Sin(vaultProgress * Mathf.PI);
        }

        Vector3 pitchNodePos = _pitchPivot.localPosition;
        pitchNodePos.y = Mathf.Lerp(pitchNodePos.y, targetY, 1f - Mathf.Exp(-_heightSmoothness * dt));
        _pitchPivot.localPosition = pitchNodePos;

        _pitchPivot.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
    }
}
}
