using Project.Player.Camera.Helpers;
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
    [SerializeField] private float _doubleJumpVelocity = -12f;
    [SerializeField] private float _vaultVelocity = -10f;

    [SerializeField] private SpringSettings _impactSpring = new(100f, 12f);

    private Vector3Spring _impactMath = new();

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
            _locomotionController.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (_locomotionController)
            _locomotionController.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(LocomotionPhase newPhase)
    {
        if (newPhase == LocomotionPhase.Grounded && _lastPhase == LocomotionPhase.Airborne)
        {
            _impactMath.AddVelocity(new Vector3(_landVelocity, 0f, 0f));
        }

        if (newPhase == LocomotionPhase.Vaulting && _lastPhase != LocomotionPhase.Vaulting)
        {
            _impactMath.AddVelocity(new Vector3(_vaultVelocity, 0f, 0f));
        }
        
        bool isJumped = _locomotionController.State.HasJumped;
        _wasJumped = isJumped;
        if (isJumped && !_wasJumped)
        {
            _impactMath.AddVelocity(new Vector3(_jumpVelocity, 0f, 0f));
        }

        _lastPhase = newPhase;
    }

    private void HandleImpacts(float dt)
    {
        _impactMath.Update(dt, _impactSpring);
    }

    private void ApplyTransforms()
    {
        var impactValue = _impactMath.CurrentValue;
        _impactPivot.localRotation = Quaternion.Euler(impactValue.x, impactValue.y, impactValue.z);
    }
}
}