using Project.Player.Locomotion;
using UnityEngine;

namespace Project.Player.FPSCamera
{
public class FPSCameraTilt : MonoBehaviour
{
    [Header("Settings")]
    [Header("Procedural Tilt")]
    [SerializeField] private float _strafeTilt = 1.5f;
    [SerializeField] private float _slideTilt = 4.5f;
    [SerializeField] private float _tiltSmoothness = 8f;

    [Header("References")]
    [SerializeField] private Transform _tiltPivot;
    [SerializeField] private LocomotionController _locomotionController;

    private float _currentTilt;

    private void Awake()
    {
        if (!_locomotionController)
            _locomotionController = GetComponent<LocomotionController>() ?? GetComponentInParent<LocomotionController>() ?? GetComponentInChildren<LocomotionController>();
    }

    public void OnUpdate(float dt)
    {
        HandleTilt(dt);
        ApplyTransforms();
    }

    private void HandleTilt(float dt)
    {
        if (!_locomotionController) return;

        float targetRoll = 0f;
        if (_locomotionController.State.IsSliding)
            targetRoll = _slideTilt;
        else if (_locomotionController.State.Phase == LocomotionPhase.Grounded)
            targetRoll = -_locomotionController.State.MoveInput.x * _strafeTilt;

        // Exponential smoothing: same feel at any frame rate (a plain dt*k lerp speeds up and slows down with FPS).
        _currentTilt = Mathf.Lerp(_currentTilt, targetRoll, 1f - Mathf.Exp(-_tiltSmoothness * dt));
    }

    private void ApplyTransforms()
    {
        if (_tiltPivot)
            _tiltPivot.localRotation = Quaternion.Euler(0f, 0f, _currentTilt);
    }
}
}
