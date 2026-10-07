using System.Collections.Generic;
using Project.Player.Locomotion;
using PurrNet;
using UnityEngine;

namespace Project.Player.Animation
{
public class PlayerLocomotionAnimator : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private NetworkAnimator _networkAnimator;
    [SerializeField] private LocomotionController _locomotionController;

    [Header("Damping")]
    [SerializeField] private float _inputDampingSpeed = 12f;

    private readonly HashSet<int> _availableParameterHashes = new();
    private float _currentHorizontal;
    private float _currentVertical;
    private float _currentSpeed;

    private static readonly int AnimationStateHash = Animator.StringToHash("AnimationState");
    private static readonly int LocomotionPhaseHash = Animator.StringToHash("LocomotionPhase");
    private static readonly int HorizontalAxisHash = Animator.StringToHash("HorizontalAxis");
    private static readonly int VerticalAxisHash = Animator.StringToHash("VerticalAxis");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsDownedHash = Animator.StringToHash("IsDowned");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int SlidingHash = Animator.StringToHash("IsSliding");

    private void Awake()
    {
        if (!_animator)
            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        if (!_networkAnimator)
            _networkAnimator = GetComponent<NetworkAnimator>() ?? GetComponentInChildren<NetworkAnimator>();
        if (!_locomotionController)
            _locomotionController = GetComponent<LocomotionController>() ?? GetComponentInParent<LocomotionController>();
    }

    protected override void OnSpawned()
    {
        enabled = isOwner && _animator && _locomotionController;
        if (!enabled)
        {
            return;
        }

        CacheAnimatorParameters();
        _locomotionController.OnAnimationStateChanged += HandleAnimationStateChanged;
        ApplyLocomotionToAnimator();
    }

    protected override void OnDespawned()
    {
        if (_locomotionController)
        {
            _locomotionController.OnAnimationStateChanged -= HandleAnimationStateChanged;
        }
    }

    private void Update()
    {
        ApplyLocomotionToAnimator();
    }

    private void HandleAnimationStateChanged(LocomotionAnimationState _)
    {
        ApplyLocomotionToAnimator();
    }

    private void ApplyLocomotionToAnimator()
    {
        LocomotionState state = _locomotionController.State;

        SetInt(AnimationStateHash, (int)state.AnimationState);
        SetInt(LocomotionPhaseHash, (int)state.Phase);

        float dt = Time.deltaTime;
        float targetH = state.MoveInput.x;
        float targetV = state.MoveInput.y;
        float targetSpeed = state.MoveInputMagnitude;

        if (dt > 0f)
        {
            _currentHorizontal = Mathf.MoveTowards(_currentHorizontal, targetH, dt * _inputDampingSpeed);
            _currentVertical = Mathf.MoveTowards(_currentVertical, targetV, dt * _inputDampingSpeed);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, dt * _inputDampingSpeed);
        }
        else
        {
            _currentHorizontal = targetH;
            _currentVertical = targetV;
            _currentSpeed = targetSpeed;
        }

        SetFloat(HorizontalAxisHash, _currentHorizontal);
        SetFloat(VerticalAxisHash, _currentVertical);
        SetFloat(MoveSpeedHash, _currentSpeed);

        bool isGrounded = state.Phase == LocomotionPhase.Grounded;
        bool isMoving = state.MoveInputMagnitude > 0.1f;
        SetBool(IsGroundedHash, isGrounded);
        SetBool(IsMovingHash, isMoving);
        SetBool(IsSprintingHash, state.IsSprinting);
        SetBool(IsCrouchingHash, state.IsCrouching);
        SetBool(IdleHash, state.AnimationState == LocomotionAnimationState.Idle);
        SetBool(IsDownedHash, state.IsDowned);
        SetBool(SlidingHash, state.IsSliding);
    }

    private void CacheAnimatorParameters()
    {
        _availableParameterHashes.Clear();
        var parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            _availableParameterHashes.Add(parameters[i].nameHash);
        }
    }

    private void SetFloat(int hash, float value)
    {
        if (_availableParameterHashes.Contains(hash))
        {
            _animator.SetFloat(hash, value);
        }
    }

    private void SetInt(int hash, int value)
    {
        if (_availableParameterHashes.Contains(hash))
        {
            _animator.SetInteger(hash, value);
        }
    }

    private void SetBool(int hash, bool value)
    {
        if (_availableParameterHashes.Contains(hash))
        {
            _animator.SetBool(hash, value);
        }
    }
}
}