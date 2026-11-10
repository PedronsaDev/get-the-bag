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
    //[SerializeField] private float _doubleJumpVelocity = -12f;
    [SerializeField] private float _vaultVelocity = -10f;

    [Tooltip("Fall speed (m/s) below which landing gives no camera kick.")]
    [SerializeField] private float _minLandSpeed = 3f;
    [Tooltip("Fall speed (m/s) at which the landing kick reaches full strength.")]
    [SerializeField] private float _fullLandSpeed = 16f;

    [SerializeField] private SpringSettings _impactSpring = new(100f, 12f);

    private readonly Vector3Spring _impactMath = new();

    private LocomotionPhase _lastPhase;
    private bool _wasJumped;
    private bool _wasDoubleJumped;
    private bool _wasVaulting;


    public void OnUpdate(float dt)
    {
        HandleImpacts(dt);
        ApplyTransforms();
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
    }

    private void ApplyTransforms()
    {
        var impactValue = _impactMath.CurrentValue;
        _impactPivot.localRotation = Quaternion.Euler(impactValue.x, impactValue.y, impactValue.z);
    }
}
}