using System;
using Project.Logging;
using UnityEngine;

namespace Project.Player.Animation
{
[DisallowMultipleComponent]
public sealed class BodyAnimationDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _bodyAnimator;
    [SerializeField] private Transform _facingReference;

    [Header("Standing locomotion")]
    [SerializeField, Min(0.01f)] private float _walkReferenceSpeed = 3f;
    [SerializeField, Min(0f)] private float _movementDeadZone = 0.05f;
    [SerializeField, Min(0f)] private float _axisDampingTime = 0.1f;

    private static readonly int HorizontalAxis = Animator.StringToHash("HorizontalAxis");
    private static readonly int VerticalAxis = Animator.StringToHash("VerticalAxis");
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int Idle = Animator.StringToHash("Idle");
    private static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
    private static readonly int IsSliding = Animator.StringToHash("IsSliding");
    private static readonly int IsDowned = Animator.StringToHash("IsDowned");

    private static readonly string[] FloatParameters = { "HorizontalAxis", "VerticalAxis", "MoveSpeed", "VerticalSpeed" };
    private static readonly string[] BoolParameters = { "IsGrounded", "IsMoving", "Idle", "IsCrouching", "IsSprinting", "IsSliding", "IsDowned" };

    private Animator _validatedAnimator;
    private RuntimeAnimatorController _validatedController;
    private bool _parametersValid;
    private bool _reportedBindingProblem;
    private bool _reportedFrameProblem;

    public Animator BodyAnimator => _bodyAnimator;
    public Transform FacingReference => _facingReference;
    public bool HasFrame { get; private set; }
    public Vector2 MovementAxes { get; private set; }
    public float PlanarSpeed { get; private set; }
    public float VerticalVelocity { get; private set; }

    private void Awake() => ResolveReferences();
    private void Reset() => ResolveReferences();

    private void ResolveReferences()
    {
        var rig = GetComponent<CharacterRigBinding>();
        if (!_bodyAnimator && rig)
            _bodyAnimator = rig.BodyAnimator;

        if (!_facingReference && _bodyAnimator)
            _facingReference = _bodyAnimator.transform;
    }

    public void Bind(Animator bodyAnimator, Transform facingReference)
    {
        if (!bodyAnimator)
            throw new ArgumentNullException(nameof(bodyAnimator));
        if (!facingReference)
            throw new ArgumentNullException(nameof(facingReference));

        ResetPose();
        _bodyAnimator = bodyAnimator;
        _facingReference = facingReference;
        _validatedAnimator = null;
        _validatedController = null;
        _reportedBindingProblem = false;
    }

    public bool ApplyFrame(in CharacterPresentationFrame frame, float deltaTime)
    {
        if (!isActiveAndEnabled)
            return false;

        if (!frame.HasFiniteVelocity || !float.IsFinite(deltaTime) || deltaTime < 0f)
        {
            if (!_reportedFrameProblem)
                AppLog.LogWarning(LogChannel.Animation, "Body animation ignored a non-finite velocity or invalid playback deltaTime.", context: this);
            _reportedFrameProblem = true;
            return false;
        }
        if (!TryValidateBinding())
            return false;

        if (!TryUpdateMovement(frame, deltaTime, out var moving))
            return false;

        var sprinting = frame.IsSprinting && moving && !frame.IsCrouching;
        WriteParameters(frame.IsGrounded, moving, frame.IsCrouching, sprinting, frame.IsSliding, frame.IsDowned);
        HasFrame = true;
        _reportedFrameProblem = false;
        return true;
    }

    private bool TryUpdateMovement(CharacterPresentationFrame frame, float deltaTime, out bool moving)
    {
        moving = false;
        var velocity = Vector3.ProjectOnPlane(frame.Velocity, Vector3.up);
        var speed = velocity.magnitude;

        if (!float.IsFinite(speed))
            return false;

        moving = frame.IsGrounded && speed > _movementDeadZone;
        var forward = Vector3.ProjectOnPlane(_facingReference.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f)
            return ReportBindingProblem("Body facing must provide a nonvertical forward direction.");

        var localVelocity = Quaternion.Inverse(Quaternion.LookRotation(forward.normalized, Vector3.up)) * velocity;
        var target = moving
            ? Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z) / _walkReferenceSpeed, 1f)
            : Vector2.zero;

        var blend = 1f;

        if (HasFrame && deltaTime > 0f && _axisDampingTime > 0f)
            blend = 1f - Mathf.Exp(-deltaTime / _axisDampingTime);

        MovementAxes = Vector2.Lerp(MovementAxes, target, blend);
        if (MovementAxes.sqrMagnitude < 0.000001f)
            MovementAxes = Vector2.zero;

        PlanarSpeed = moving ? speed : 0f;
        VerticalVelocity = frame.Velocity.y;
        return true;
    }

    public void ResetPose()
    {
        MovementAxes = Vector2.zero;
        PlanarSpeed = 0f;
        VerticalVelocity = 0f;
        if (HasFrame && _parametersValid && _bodyAnimator && _bodyAnimator == _validatedAnimator &&
            _bodyAnimator.runtimeAnimatorController == _validatedController)
            WriteParameters(false, false, false, false, false, false);
        HasFrame = false;
    }

    private void OnDisable() => ResetPose();

    private bool TryValidateBinding()
    {
        if (!_bodyAnimator || !_facingReference)
            ResolveReferences();
        if (!_bodyAnimator || !_facingReference || !_bodyAnimator.runtimeAnimatorController)
            return ReportBindingProblem("Body animation requires a body Animator/controller and a visual facing reference.");
        if (!_bodyAnimator.isActiveAndEnabled)
            return ReportBindingProblem("Body animation requires an active, enabled body Animator.");
        if (_bodyAnimator.applyRootMotion)
            return ReportBindingProblem("Body animation is waiting for motor-owned movement: validate the clips on a duplicate, then disable body Apply Root Motion before feeding frames.");
        if (!float.IsFinite(_walkReferenceSpeed) || _walkReferenceSpeed <= 0f ||
            !float.IsFinite(_movementDeadZone) || _movementDeadZone < 0f ||
            !float.IsFinite(_axisDampingTime) || _axisDampingTime < 0f)
            return ReportBindingProblem("Body animation tuning requires a positive walk reference speed and finite, nonnegative dead zone/damping.");

        if (_validatedAnimator != _bodyAnimator || _validatedController != _bodyAnimator.runtimeAnimatorController)
        {
            _validatedAnimator = _bodyAnimator;
            _validatedController = _bodyAnimator.runtimeAnimatorController;
            _parametersValid = false;

            var parameters = _bodyAnimator.parameters;
            if (!HasParameters(parameters, FloatParameters, AnimatorControllerParameterType.Float))
                return false;
            if (!HasParameters(parameters, BoolParameters, AnimatorControllerParameterType.Bool))
                return false;
            _parametersValid = true;
        }
        if (!_parametersValid) return false;
        _reportedBindingProblem = false;
        return true;
    }

    private bool HasParameters(AnimatorControllerParameter[] available, string[] names, AnimatorControllerParameterType type)
    {
        foreach (var s in names)
        {
            var found = false;
            foreach (var parameter in available)
                if (parameter.name == s && parameter.type == type) { found = true; break; }
            if (!found) return ReportBindingProblem($"Body animation controller requires '{s}' ({type}).");
        }
        return true;
    }

    private bool ReportBindingProblem(string message)
    {
        if (!_reportedBindingProblem)
            AppLog.LogWarning(LogChannel.Animation, message, context: this);
        _reportedBindingProblem = true;
        return false;
    }

    private void WriteParameters(bool grounded, bool moving, bool crouching, bool sprinting, bool sliding, bool downed)
    {
        _bodyAnimator.SetFloat(HorizontalAxis, MovementAxes.x);
        _bodyAnimator.SetFloat(VerticalAxis, MovementAxes.y);
        _bodyAnimator.SetFloat(MoveSpeed, PlanarSpeed);
        _bodyAnimator.SetBool(IsGrounded, grounded);
        _bodyAnimator.SetBool(IsMoving, moving);
        _bodyAnimator.SetBool(Idle, grounded && !moving);
        _bodyAnimator.SetBool(IsCrouching, crouching);
        _bodyAnimator.SetBool(IsSprinting, sprinting);
        _bodyAnimator.SetBool(IsSliding, sliding);
        _bodyAnimator.SetFloat(VerticalSpeed, VerticalVelocity);
        _bodyAnimator.SetBool(IsDowned, downed);
    }
}
}
