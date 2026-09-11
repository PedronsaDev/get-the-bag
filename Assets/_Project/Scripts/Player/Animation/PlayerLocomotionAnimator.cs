using System.Collections.Generic;
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

    private readonly HashSet<int> _availableParameterHashes = new();

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

        SetFloat(HorizontalAxisHash, state.MoveInput.x);
        SetFloat(VerticalAxisHash, state.MoveInput.y);
        SetFloat(MoveSpeedHash, state.MoveInputMagnitude);

        bool isGrounded = state.Phase == LocomotionPhase.Grounded;
        bool isMoving = state.MoveInputMagnitude > 0.1f;
        SetBool(IsGroundedHash, isGrounded);
        SetBool(IsMovingHash, isMoving);
        SetBool(IsSprintingHash, state.IsSprinting);
        SetBool(IsCrouchingHash, state.IsCrouching);
        SetBool(IdleHash, state.AnimationState == LocomotionAnimationState.Idle);
        SetBool(IsDownedHash, state.IsDowned);
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