using Project.Player.Camera.Helpers;
using Project.Player.Locomotion;
using UnityEngine;

namespace Project.Player.FPSCamera
{
public class FPSCameraImpact : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _impactPivot;
    [SerializeField] private LocomotionController _locomotionController;

    [Header("Impact Intensities")]
    [SerializeField] private float _landVelocity = -15f;
    [SerializeField] private float _jumpVelocity = 10f;
    [SerializeField] private float _vaultVelocity = -10f;

    [Tooltip("Fall speed (m/s) below which landing gives no camera kick.")]
    [SerializeField] private float _minLandSpeed = 3f;
    [Tooltip("Fall speed (m/s) at which the landing kick reaches full strength.")]
    [SerializeField] private float _fullLandSpeed = 16f;

    [SerializeField] private SpringSettings _impactSpring = new(100f, 12f);

    [Header("Fire Kick")]
    [Tooltip("Stiff, well damped spring so each shot snaps up and settles before the next one.")]
    [SerializeField] private SpringSettings _fireSpring = new(320f, 26f);
    [Tooltip("Pitch velocity (deg/s) added per shot. Negative looks up. Peak offset is roughly 1/20 of this value.")]
    [SerializeField] private float _firePitchVelocity = -34f;
    [SerializeField] private float _fireYawVelocity = 5f;
    [SerializeField] private float _fireRollVelocity = 7f;
    [Tooltip("Kick fades out as the fire spring approaches this pitch (degrees), so full auto cannot stack forever.")]
    [SerializeField] private float _fireMaxPitch = 3.5f;

    [Header("Damage Flinch")]
    [SerializeField] private SpringSettings _damageSpring = new(180f, 16f);
    [SerializeField] private float _damagePitchVelocity = -28f;
    [SerializeField] private float _damageRollVelocity = 40f;

    [Header("Movement Bob")]
    [SerializeField] private bool _enableBob = true;
    [SerializeField] private float _bobPitch = 0.07f;
    [SerializeField] private float _bobRoll = 0.09f;
    [Tooltip("Bob cycles per second while walking.")]
    [SerializeField] private float _bobFrequency = 1.8f;
    [SerializeField] private float _bobSprintMultiplier = 1.3f;
    [SerializeField] private float _bobCrouchMultiplier = 0.5f;
    [Tooltip("Bob strength while fully aimed down sights.")]
    [Range(0f, 1f)] [SerializeField] private float _bobAimMultiplier = 0.1f;
    [SerializeField] private float _bobBlendSpeed = 8f;

    private readonly Vector3Spring _impactMath = new();
    private readonly Vector3Spring _fireMath = new();
    private readonly Vector3Spring _damageMath = new();

    private LocomotionPhase _lastPhase;
    private bool _wasJumped;
    private bool _wasDoubleJumped;
    private bool _wasVaulting;
    private float _bobPhase;
    private float _bobWeight;
    private Vector3 _bobRotation;

    public void OnUpdate(float dt, float aimProgress = 0f)
    {
        HandleImpacts(dt);

        if (_enableBob) 
            HandleBob(dt, aimProgress);
        
        ApplyTransforms();
    }

    public void AddFireKick(float strength = 1f)
    {
        float headroom = _fireMaxPitch > 0f
            ? Mathf.Clamp01(1f - Mathf.Abs(_fireMath.CurrentValue.x) / _fireMaxPitch)
            : 1f;
        float k = strength * headroom;

        _fireMath.AddVelocity(new Vector3(
            _firePitchVelocity * k,
            Random.Range(-_fireYawVelocity, _fireYawVelocity) * k,
            Random.Range(-_fireRollVelocity, _fireRollVelocity) * k));
    }

    /// <summary>
    /// Head snap when hit. <paramref name="sideInput"/> is -1 (hit from the left) to +1 (from the right).
    /// </summary>
    public void AddDamageKick(float strength, float sideInput)
    {
        strength = Mathf.Clamp01(strength);
        _damageMath.AddVelocity(new Vector3(
            _damagePitchVelocity * strength,
            0f,
            -_damageRollVelocity * Mathf.Clamp(sideInput, -1f, 1f) * strength));
    }

    private void OnEnable()
    {
        if (_locomotionController)
        {
            _locomotionController.OnPhaseChanged += HandlePhaseChanged;
            _locomotionController.OnLandedAtSpeed += HandleLanded;
        }
    }

    private void OnDisable()
    {
        if (_locomotionController)
        {
            _locomotionController.OnPhaseChanged -= HandlePhaseChanged;
            _locomotionController.OnLandedAtSpeed -= HandleLanded;
        }

        _impactMath.Reset();
        _fireMath.Reset();
        _damageMath.Reset();
        _bobWeight = 0f;
        _bobRotation = Vector3.zero;
    }

    private void HandlePhaseChanged(LocomotionPhase newPhase)
    {
        if (newPhase == LocomotionPhase.Vaulting && _lastPhase != LocomotionPhase.Vaulting)
        {
            _impactMath.AddVelocity(new Vector3(_vaultVelocity, 0f, 0f));
        }

        _lastPhase = newPhase;
    }

    // Landing kick scales with fall speed: a hop barely nudges the camera, a long drop hits hard.
    private void HandleLanded(float fallSpeed)
    {
        float t = Mathf.InverseLerp(_minLandSpeed, _fullLandSpeed, fallSpeed);
        if (t <= 0f) return;
        _impactMath.AddVelocity(new Vector3(_landVelocity * Mathf.Lerp(0.35f, 1f, t), 0f, 0f));
    }

    private void HandleImpacts(float dt)
    {
        // Jump kick: edge-detect HasJumped every frame (phase changes alone miss ground jumps).
        bool isJumped = _locomotionController && _locomotionController.State.HasJumped &&
                        _locomotionController.State.Phase != LocomotionPhase.Vaulting;
        if (isJumped && !_wasJumped)
            _impactMath.AddVelocity(new Vector3(_jumpVelocity, 0f, 0f));
        _wasJumped = isJumped;

        _impactMath.Update(dt, _impactSpring);
        _fireMath.Update(dt, _fireSpring);
        _damageMath.Update(dt, _damageSpring);
    }

    private void HandleBob(float dt, float aimProgress)
    {
        float targetWeight = 0f;
        float frequencyScale = 1f;

        if (_locomotionController)
        {
            var state = _locomotionController.State;
            bool walking = state.Phase == LocomotionPhase.Grounded && !state.IsSliding && !state.IsDowned &&
                           state.MoveInputMagnitude > 0.1f;
            if (walking)
            {
                targetWeight = Mathf.Clamp01(state.MoveInputMagnitude);
                if (state.IsSprinting)
                {
                    targetWeight *= _bobSprintMultiplier;
                    frequencyScale = 1.35f;
                }
                else if (state.IsCrouching)
                {
                    targetWeight *= _bobCrouchMultiplier;
                    frequencyScale = 0.7f;
                }
            }
        }

        _bobWeight = Mathf.Lerp(_bobWeight, targetWeight, 1f - Mathf.Exp(-_bobBlendSpeed * dt));
        if (_bobWeight < 0.001f)
        {
            _bobRotation = Vector3.zero;
            return;
        }

        _bobPhase = Mathf.Repeat(_bobPhase + dt * _bobFrequency * frequencyScale * Mathf.PI * 2f, Mathf.PI * 2f);
        float aimScale = Mathf.Lerp(1f, _bobAimMultiplier, Mathf.Clamp01(aimProgress));

        // Pitch bobs twice per cycle (each footfall), roll once (side to side sway).
        _bobRotation = new Vector3(
            Mathf.Sin(_bobPhase * 2f) * _bobPitch,
            0f,
            Mathf.Sin(_bobPhase) * _bobRoll) * (_bobWeight * aimScale);
    }

    private void ApplyTransforms()
    {
        if (!_impactPivot) return;

        var value = _impactMath.CurrentValue + _fireMath.CurrentValue + _damageMath.CurrentValue + _bobRotation;
        _impactPivot.localRotation = Quaternion.Euler(value.x, value.y, value.z);
    }
}
}
