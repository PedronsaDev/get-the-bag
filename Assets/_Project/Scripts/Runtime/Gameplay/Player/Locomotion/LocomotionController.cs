using Project.Player.FPSCamera;
using Project.Player.Input;

namespace Project.Player.Locomotion
{
using System;
using System.Collections.Generic;
using KinematicCharacterController;
using PurrNet;
using UnityEngine;
using UnityPhysics = UnityEngine.Physics;

public struct PlayerCharacterInputs
{
    public float MoveAxisForward;
    public float MoveAxisRight;
    public Quaternion CameraRotation;
    public bool JumpDown;
    public bool JumpHeld;
    public bool SprintHeld;
    public bool CrouchHeld;
}

public enum JumpStatus
{
    None,
    Requested,
    Executed,
    DoubleJumpUsed
}

public enum LocomotionPhase
{
    Grounded,
    Airborne,
    Vaulting,
    Noclip
}

public enum LocomotionAnimationState
{
    Idle,
    Walking,
    Sprinting,
    Crouching,
    Sliding,
    CrouchWalking,
    Jumping,
    Falling,
    Vaulting,
    Downed,
    Crawling
}

[Serializable]
public struct LocomotionState
{
    public float JumpBufferTimer;
    public float TimeSinceJumpRequested;
    public float TimeSinceLastAbleToJump;
    public float TimeSinceLastJump;
    public float WallJumpGraceTimer;
    public Vector3 WallJumpNormal;
    public JumpStatus CurrentJumpStatus;
    public bool CanWallJump;
    public bool HasJumped;
    public bool HasDoubleJumped;
    public bool HasWallJumped;
    public bool CoyoteJumpQueued;
    public float LandingTime;
    public LocomotionPhase Phase;
    public LocomotionAnimationState AnimationState;
    public Vector2 MoveInput;
    public float MoveInputMagnitude;
    public bool IsSprinting;
    public bool IsCrouching;
    public bool WasCrouchingLastFrame;
    public bool IsSliding;
    public bool IsDowned;
    public bool IsDraggingTeammate;
    public float TargetHeight;
    public Vector3 VaultStartPosition;
    public Vector3 VaultTargetPosition;
    public float VaultTimer;
    public bool IsNoclipEnabled;
}

[Serializable]
public struct VaultEvaluation
{
    public bool CanVault;
    public bool HasWallHit;
    public bool HasLedgeHit;
    public bool IsLedgeSurfaceWalkable;
    public bool IsLandingClear;
    public bool IsWallVertical;
    [NonSerialized] public RaycastHit WallHit;
    [NonSerialized] public RaycastHit LedgeHit;
    public Vector3 TargetFootPos;
    public float LedgeHeight;
    public string FailureReason;
}


public class LocomotionController : NetworkBehaviour, ICharacterController
{
    private interface ILocomotionPhaseHandler
    {
        LocomotionPhase Phase { get; }
        void Enter();
        void Exit();
        void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
    }

    private sealed class GroundedPhaseHandler : ILocomotionPhaseHandler
    {
        private readonly LocomotionController _controller;
        public GroundedPhaseHandler(LocomotionController controller) => _controller = controller;
        public LocomotionPhase Phase => LocomotionPhase.Grounded;

        public void Enter()
        {
            if (_controller._isCrouchInputHeld)
                _controller.State.WasCrouchingLastFrame = false;
        }

        public void Exit()
        {
            _controller.State.IsSliding = false;
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            var currentSpeed = currentVelocity.magnitude;

            if (_controller._isDowned)
            {
                _controller.State.IsSliding = false;
            }
            else if (_controller._isCrouchInputHeld && !_controller.State.WasCrouchingLastFrame &&
                     currentSpeed > _controller._minSpeedToSlide)
            {
                _controller.State.IsSliding = true;
                currentVelocity += currentVelocity.normalized * _controller._slideEntryBoost;
                currentSpeed = currentVelocity.magnitude;
            }
            else if (!_controller._isCrouchInputHeld || currentSpeed < _controller._slideStopSpeed)
            {
                _controller.State.IsSliding = false;
            }

            Vector3 effectiveGroundNormal = _controller._motor.GroundingStatus.GroundNormal;
            currentVelocity = _controller._motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) *
                              currentSpeed;

            Vector3 reorientedInput;
            float targetSpeed;

            if (_controller._isDowned && _controller._dragTarget)
            {
                Vector3 dragForwardPos = _controller._dragTarget.position + (_controller._dragTarget.forward * 1.2f);
                Vector3 toDragger = dragForwardPos - _controller._motor.TransientPosition;
                Vector3 planarToDragger = Vector3.ProjectOnPlane(toDragger, effectiveGroundNormal);
                float dist = planarToDragger.magnitude;

                if (dist > 0.25f)
                {
                    reorientedInput = planarToDragger.normalized;
                    targetSpeed = Mathf.Clamp(dist * 3f, 1.2f, 4f);
                }
                else
                {
                    reorientedInput = Vector3.zero;
                    targetSpeed = 0f;
                }
            }
            else
            {
                var inputRight = Vector3.Cross(_controller._moveInputVector, _controller._motor.CharacterUp);
                reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized *
                                  _controller._moveInputVector.magnitude;

                var baseSpeed = _controller.State.IsSliding ? 0f : _controller.GetGroundMoveSpeed();
                targetSpeed = baseSpeed;

                if (!_controller.State.IsSliding && reorientedInput.sqrMagnitude > 0f)
                {
                    var movementDir = reorientedInput.normalized;

                    var slopeDot = Vector3.Dot(movementDir, -_controller._motor.CharacterUp);

                    if (slopeDot > 0f)
                    {
                        targetSpeed += (baseSpeed * slopeDot * _controller._downhillSpeedMultiplier);
                    }
                    else if (slopeDot < 0f)
                    {
                        targetSpeed -= (baseSpeed * Mathf.Abs(slopeDot) * _controller._uphillSpeedPenalty);
                        targetSpeed = Mathf.Max(targetSpeed, 1f);
                    }
                }
            }

            var targetMovementVelocity = reorientedInput * targetSpeed;
            var sharpness = _controller.State.IsSliding
                ? _controller._slideFriction
                : _controller._stableMovementSharpness;
            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity,
                1f - Mathf.Exp(-sharpness * deltaTime));

            if (!_controller.State.IsSliding)
                return;

            var slopeGravity = Vector3.ProjectOnPlane(_controller._gravity, effectiveGroundNormal);

            currentVelocity += slopeGravity * (_controller._slideGravityMultiplier * deltaTime);
        }
    }

    private sealed class NoclipPhaseHandler : ILocomotionPhaseHandler
    {
        private readonly LocomotionController _controller;

        public NoclipPhaseHandler(LocomotionController controller) => _controller = controller;
        public LocomotionPhase Phase => LocomotionPhase.Noclip;

        public void Enter()
        {
            if (_controller._motor)
            {
                _controller._motor.SetCapsuleCollisionsActivation(false);
                _controller._motor.SetMovementCollisionsSolvingActivation(false);
                _controller._motor.SetGroundSolvingActivation(false);
                _controller._motor.ForceUnground();
            }

            _controller.State.IsNoclipEnabled = true;
            _controller.State.Phase = LocomotionPhase.Noclip;
            _controller.State.IsSliding = false;
        }

        public void Exit()
        {
            if (_controller._motor)
            {
                _controller._motor.SetCapsuleCollisionsActivation(true);
                _controller._motor.SetMovementCollisionsSolvingActivation(true);
                _controller._motor.SetGroundSolvingActivation(true);
            }

            _controller.State.IsNoclipEnabled = false;
            _controller.State.TimeSinceLastJump = 0f;
            _controller.State.CurrentJumpStatus = JumpStatus.None;
            _controller.State.JumpBufferTimer = 0f;
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Quaternion camRot = _controller._fpsCameraController
                ? _controller._fpsCameraController.CameraRotation
                : _controller._cameraRotation;

            Vector3 fwd = camRot * Vector3.forward;
            Vector3 rgt = camRot * Vector3.right;
            Vector3 up = _controller._motor ? _controller._motor.CharacterUp : Vector3.up;

            Vector3 moveDir = (fwd * _controller.State.MoveInput.y) + (rgt * _controller.State.MoveInput.x);

            if (_controller._isJumpInputHeld)
                moveDir += up;
            if (_controller._isCrouchInputHeld)
                moveDir -= up;

            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            float speed = _controller._noclipSpeed;
            if (_controller._isRawSprintHeld)
                speed *= _controller._noclipSprintMultiplier;

            Vector3 targetVelocity = moveDir * speed;
            float sharpness = _controller._noclipSharpness;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-sharpness * deltaTime));
        }
    }

    private sealed class VaultingPhaseHandler : ILocomotionPhaseHandler
    {
        private readonly LocomotionController _controller;
        public VaultingPhaseHandler(LocomotionController controller) => _controller = controller;
        public LocomotionPhase Phase => LocomotionPhase.Vaulting;

        public void Enter()
        {
            _controller.State.VaultTimer = 0f;
            _controller.State.VaultStartPosition = _controller._motor.TransientPosition;
            _controller._motor.ForceUnground();
        }

        public void Exit()
        {
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            _controller.State.VaultTimer += deltaTime;
            float progress = Mathf.Clamp01(_controller.State.VaultTimer / _controller._vaultDuration);

            if (progress >= 1f)
            {
                float exitSpeed = _controller.GetGroundMoveSpeed();
                currentVelocity = _controller._moveInputVector * exitSpeed;
                _controller.SwitchPhase(LocomotionPhase.Airborne);
            }
            else
            {
                float verticalProgress = Mathf.Sqrt(progress);
                float horizontalProgress = progress * progress;

                Vector3 startPos = _controller.State.VaultStartPosition;
                Vector3 targetPos = _controller.State.VaultTargetPosition;

                Vector3 currentPos = startPos;

                currentPos.y = Mathf.Lerp(startPos.y, targetPos.y, verticalProgress);
                currentPos.x = Mathf.Lerp(startPos.x, targetPos.x, horizontalProgress);
                currentPos.z = Mathf.Lerp(startPos.z, targetPos.z, horizontalProgress);

                currentPos.y += Mathf.Sin(progress * Mathf.PI) * 0.15f;

                currentVelocity = (currentPos - _controller._motor.TransientPosition) / deltaTime;
            }
        }
    }

    private sealed class AirbornePhaseHandler : ILocomotionPhaseHandler
    {
        private readonly LocomotionController _controller;
        public AirbornePhaseHandler(LocomotionController controller) => _controller = controller;
        public LocomotionPhase Phase => LocomotionPhase.Airborne;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            var currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, _controller._motor.CharacterUp);
            var verticalVelocity = currentVelocity - currentVelocityOnInputsPlane;

            if (_controller._moveInputVector.sqrMagnitude > 0f)
            {
                var targetAirSpeed = _controller.GetGroundMoveSpeed();
                var currentPlanarSpeed = currentVelocityOnInputsPlane.magnitude;

                var speedToMaintain = Mathf.Max(currentPlanarSpeed, targetAirSpeed);
                var targetMovement = _controller._moveInputVector * speedToMaintain;

                if (_controller._motor.GroundingStatus.FoundAnyGround)
                {
                    var perpendicularObstructionNormal = Vector3.Cross(
                        Vector3.Cross(_controller._motor.CharacterUp, _controller._motor.GroundingStatus.GroundNormal),
                        _controller._motor.CharacterUp).normalized;

                    if (Vector3.Dot(targetMovement, perpendicularObstructionNormal) < 0f)
                    {
                        targetMovement = Vector3.ProjectOnPlane(targetMovement, perpendicularObstructionNormal);
                    }
                }

                currentVelocityOnInputsPlane = Vector3.MoveTowards(
                    currentVelocityOnInputsPlane,
                    targetMovement,
                    _controller._airAccelerationSpeed * deltaTime
                );
            }

            currentVelocity = currentVelocityOnInputsPlane + verticalVelocity;

            float gravityMultiplier = 1f;

            if (currentVelocity.y <= 0f)
            {
                gravityMultiplier = _controller._fallGravityMultiplier;
            }

            currentVelocity += _controller._gravity * (gravityMultiplier * deltaTime);

            currentVelocity *= (1f / (1f + (_controller._drag * deltaTime)));
        }
    }

    public LocomotionState State;
    public event Action<LocomotionPhase> OnPhaseChanged;
    public event Action<LocomotionAnimationState> OnAnimationStateChanged;

    [Header("References")] 
    [SerializeField] private KinematicCharacterMotor _motor;
    [SerializeField] private NetworkTransform _networkTransform;
    [SerializeField] private LocalPlayerInputProvider _inputProvider;
    [SerializeField] private PlayerFacade _playerFacade;
    [SerializeField] private FPSCameraController _fpsCameraController;

    [Header("Stable Movement")] 
    [SerializeField] private float _maxStableMoveSpeed = 6f;
    [SerializeField] private float _sprintSpeedMultiplier = 1.5f;
    [SerializeField] private float _crouchSpeedMultiplier = 0.5f;
    [SerializeField] private float _stableMovementSharpness = 15f;

    [SerializeField] private float _downhillSpeedMultiplier = 0.6f;
    [SerializeField] private float _uphillSpeedPenalty = 0.6f;

    [Header("Air Movement")] 
    [SerializeField] private float _airAccelerationSpeed = 15f;
    [SerializeField] private float _drag = 0.1f;

    [Header("Jumping")] 
    [SerializeField] private LayerMask _wallJumpLayerMask = ~0;
    [SerializeField] private bool _allowJumpingWhenSliding = true;
    [SerializeField] private bool _allowDoubleJump = true;
    [SerializeField] private bool _allowWallJump = false;
    [SerializeField] private float _jumpSpeed = 9f;
    [SerializeField] private float _doubleJumpSpeed = 8f;
    [SerializeField] private float _jumpPreGroundingGraceTime = 0.15f;
    [SerializeField] private float _jumpPostGroundingGraceTime = 0.2f;
    [SerializeField] private float _jumpBufferTime = 0.15f;

    [Header("Crouch & Dimensions")] 
    [SerializeField] private float _standingHeight = 2f;
    [SerializeField] private float _crouchingHeight = 1.2f;
    [SerializeField] private float _crouchTransitionSpeed = 10f;
    [SerializeField] private LayerMask _ceilingCheckLayerMask = ~0;

    [Header("Ground Sliding")] 
    [SerializeField] private float _slideEntryBoost = 2f;
    [SerializeField] private float _slideFriction = 1f;
    [SerializeField] private float _minSpeedToSlide = 6.2f;
    [SerializeField] private float _slideStopSpeed = 5f;

    //[Header("Wall Sliding")] 
    //[SerializeField] private float _maxWallSlideSpeed = 3f;

    [Header("Vaulting & Ledge Grab")] 
    [SerializeField] private LayerMask _vaultLayerMask = ~0;
    [SerializeField] private float _vaultCheckDistance = 2f;
    [SerializeField] private float _minVaultHeight = 0.4f;
    [SerializeField] private float _maxVaultHeight = 2f;
    [SerializeField] private float _vaultDuration = 0.7f;

    [Header("Dynamic Gravity")] 
    [SerializeField] private float _fallGravityMultiplier = 0.9f;
    [SerializeField] private float _slideGravityMultiplier = 2.5f;
    [SerializeField] private Vector3 _gravity = new(0, -29f, 0);

    [Header("Noclip")] 
    [SerializeField] private float _noclipSpeed = 12f;
    [SerializeField] private float _noclipSprintMultiplier = 2.5f;
    [SerializeField] private float _noclipSharpness = 20f;

    [Header("Downed & Dragging")] 
    [SerializeField] private float _crawlSpeed = 1.2f;
    [SerializeField] private float _downedCapsuleHeight = 0.8f;
    [SerializeField] private float _draggingSpeedMultiplier = 0.6f;

    [Header("Movement Debug & Gizmos")] 
    [SerializeField] private bool _enableGizmos = true;
    [SerializeField] private bool _drawOnlyWhenSelected;
    [SerializeField] private bool _drawVelocityVector = true;
    [SerializeField] private bool _drawInputVector = true;
    [SerializeField] private bool _drawGroundStatus = true;
    [SerializeField] private bool _drawCapsuleBounds = true;
    [SerializeField] private bool _drawCeilingCheck = true;
    [SerializeField] private bool _drawWallAndVaultChecks = true;
    [SerializeField] private bool _drawOverheadStatus = true;

    private bool _isDowned;
    private bool _isDraggingTeammate;
    private Transform _dragTarget;

    public bool IsDowned => _isDowned;
    public bool IsDraggingTeammate => _isDraggingTeammate;

    public static bool GlobalGizmosEnabled { get; set; } = true;

    public bool EnableGizmos
    {
        get => _enableGizmos;
        set => _enableGizmos = value;
    }

    public KinematicCharacterMotor Motor => _motor;
    public Vector3 MoveInputVector => _moveInputVector;
    public Vector3 LookInputVector => _lookInputVector;
    public float StandingHeight => _standingHeight;
    public float CrouchingHeight => _crouchingHeight;
    public float DownedCapsuleHeight => _downedCapsuleHeight;
    public float MinVaultHeight => _minVaultHeight;
    public float MaxVaultHeight => _maxVaultHeight;

    public void SetDowned(bool downed)
    {
        _isDowned = downed;
        State.IsDowned = downed;
        if (downed)
        {
            State.IsSliding = false;
            _isSprintInputHeld = false;
            if (_motor && _motor.Capsule)
            {
                _motor.SetCapsuleDimensions(_motor.Capsule.radius, _downedCapsuleHeight, _downedCapsuleHeight * 0.5f);
            }
        }
        else
        {
            if (_motor && _motor.Capsule)
            {
                _motor.SetCapsuleDimensions(_motor.Capsule.radius, _standingHeight, _standingHeight * 0.5f);
            }
        }
    }

    public void SetDraggingTeammate(bool dragging)
    {
        _isDraggingTeammate = dragging;
        State.IsDraggingTeammate = dragging;
        if (dragging)
        {
            _isSprintInputHeld = false;
        }
    }

    public void SetDragTarget(Transform dragger)
    {
        _dragTarget = dragger;
    }

    public void ClearDragTarget()
    {
        _dragTarget = null;
    }

    private readonly Dictionary<LocomotionPhase, ILocomotionPhaseHandler> _phaseHandlers = new();
    private ILocomotionPhaseHandler _currentPhaseHandler;
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private Quaternion _cameraRotation = Quaternion.identity;
    private bool _isRawSprintHeld;
    private bool _isSprintInputHeld;
    private bool _isCrouchInputHeld;
    private bool _isJumpInputHeld;

    private void Awake()
    {
        if (!_motor)
            _motor = GetComponent<KinematicCharacterMotor>();

        if (_motor)
            _motor.CharacterController = this;

        if (!_inputProvider)
            _inputProvider = GetComponent<LocalPlayerInputProvider>() ??
                             GetComponentInParent<LocalPlayerInputProvider>() ??
                             GetComponentInChildren<LocalPlayerInputProvider>();

        if (!_playerFacade)
            _playerFacade = GetComponent<PlayerFacade>() ??
                            GetComponentInParent<PlayerFacade>() ?? GetComponentInChildren<PlayerFacade>();

        if (!_fpsCameraController)
            _fpsCameraController = GetComponent<FPSCameraController>();
        if (!_fpsCameraController)
            _fpsCameraController = GetComponentInChildren<FPSCameraController>();

        _phaseHandlers.Clear();
        _phaseHandlers[LocomotionPhase.Grounded] = new GroundedPhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Airborne] = new AirbornePhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Vaulting] = new VaultingPhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Noclip] = new NoclipPhaseHandler(this);
        SwitchPhase(LocomotionPhase.Airborne);
    }

    protected override void OnSpawned()
    {
        _motor.enabled = isOwner;
        enabled = isOwner;

        if (!isOwner)
            return;

        SetPlayerPosition(transform.parent.position);

        State = new LocomotionState
        {
            TimeSinceJumpRequested = Mathf.Infinity,
            TimeSinceLastJump = Mathf.Infinity,
            LandingTime = -100f,
            CurrentJumpStatus = JumpStatus.None,
            Phase = LocomotionPhase.Airborne,
            AnimationState = LocomotionAnimationState.Falling
        };

        _phaseHandlers.Clear();
        _phaseHandlers[LocomotionPhase.Grounded] = new GroundedPhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Airborne] = new AirbornePhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Vaulting] = new VaultingPhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Noclip] = new NoclipPhaseHandler(this);

        SwitchPhase(_motor.GroundingStatus.IsStableOnGround ? LocomotionPhase.Grounded : LocomotionPhase.Airborne);
    }

    private void SetPlayerPosition(Vector3 position)
    {
        _networkTransform.enabled = false;
        _motor.SetPosition(position);
        _networkTransform.enabled = true;
    }

    private void Update()
    {
        if (!isOwner) return;

        if (!_inputProvider && !_playerFacade)
        {
            _inputProvider = GetComponent<LocalPlayerInputProvider>() ??
                             GetComponentInParent<LocalPlayerInputProvider>() ??
                             GetComponentInChildren<LocalPlayerInputProvider>();
            if (!_inputProvider)
                _playerFacade = GetComponent<PlayerFacade>() ??
                                GetComponentInParent<PlayerFacade>() ?? GetComponentInChildren<PlayerFacade>();
        }

        var input = _inputProvider
            ? _inputProvider.GetCurrentInput()
            : (_playerFacade ? _playerFacade.CurrentInput : default);
        Quaternion camRot = _fpsCameraController
            ? _fpsCameraController.CameraRotation
            : transform.rotation;

        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs
        {
            MoveAxisForward = input.MoveVector.y,
            MoveAxisRight = input.MoveVector.x,
            CameraRotation = camRot,
            JumpDown = input.JumpPressed,
            JumpHeld = input.JumpHeld,
            SprintHeld = input.SprintHeld,
            CrouchHeld = input.CrouchHeld
        };

        SetInputs(ref characterInputs);
    }

    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        _cameraRotation = inputs.CameraRotation;
        _isRawSprintHeld = inputs.SprintHeld;

        Vector3 moveInputVector =
            Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

        Vector3 cameraPlanarDirection =
            Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, _motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, _motor.CharacterUp)
                .normalized;
        }

        _lookInputVector = cameraPlanarDirection;
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, _motor.CharacterUp);
        _moveInputVector = cameraPlanarRotation * moveInputVector;

        if (_isDowned || _isDraggingTeammate)
        {
            inputs.SprintHeld = false;
            inputs.JumpDown = false;
            inputs.JumpHeld = false;
        }

        _isSprintInputHeld = !_isDowned && !_isDraggingTeammate && inputs.SprintHeld &&
                             inputs is { CrouchHeld: false, MoveAxisForward: > 0.1f };
        _isCrouchInputHeld = inputs.CrouchHeld;
        _isJumpInputHeld = !_isDowned && !_isDraggingTeammate && inputs.JumpHeld;

        State.MoveInput = new Vector2(inputs.MoveAxisRight, inputs.MoveAxisForward);
        State.MoveInputMagnitude = moveInputVector.magnitude;
        State.IsSprinting = _isSprintInputHeld;
        State.IsCrouching = _isCrouchInputHeld;

        if (inputs.JumpDown && State.CurrentJumpStatus == JumpStatus.None && !_isDowned && !_isDraggingTeammate)
        {
            State.JumpBufferTimer = _jumpBufferTime;
        }

        TryQueueJumpFromBuffer();
    }

    public VaultEvaluation EvaluateVault()
    {
        var eval = new VaultEvaluation();
        if (_isDowned || _isDraggingTeammate || !_motor || State.Phase == LocomotionPhase.Noclip)
        {
            eval.FailureReason = "Disabled / Downed / Noclip";
            return eval;
        }

        Vector3 basePos = _motor.TransientPosition;
        Vector3 up = _motor.CharacterUp;
        Vector3 forward = (Application.isPlaying && _lookInputVector.sqrMagnitude > 0.1f)
            ? _lookInputVector.normalized
            : transform.forward;

        float radius = _motor.Capsule ? _motor.Capsule.radius : 0.5f;
        float height = _motor.Capsule ? _motor.Capsule.height : _standingHeight;

        float lowCheckHeight = Mathf.Clamp(_minVaultHeight, 0.3f, 0.6f);
        float midCheckHeight = Mathf.Clamp(_maxVaultHeight * 0.5f, 0.7f, 1.1f);

        Vector3 rayOriginLow = basePos + up * lowCheckHeight;
        Vector3 rayOriginMid = basePos + up * midCheckHeight;

        bool hitMid = UnityPhysics.Raycast(rayOriginMid, forward, out RaycastHit wallHitMid, _vaultCheckDistance,
            _vaultLayerMask, QueryTriggerInteraction.Ignore);
        bool hitLow = UnityPhysics.Raycast(rayOriginLow, forward, out RaycastHit wallHitLow, _vaultCheckDistance,
            _vaultLayerMask, QueryTriggerInteraction.Ignore);

        RaycastHit wallHit;
        if (hitLow && hitMid)
        {
            wallHit = wallHitLow.distance <= wallHitMid.distance ? wallHitLow : wallHitMid;
            eval.HasWallHit = true;
        }
        else if (hitLow)
        {
            wallHit = wallHitLow;
            eval.HasWallHit = true;
        }
        else if (hitMid)
        {
            wallHit = wallHitMid;
            eval.HasWallHit = true;
        }
        else
        {
            eval.FailureReason = "No wall in range";
            return eval;
        }

        eval.WallHit = wallHit;

        eval.IsWallVertical = Mathf.Abs(Vector3.Dot(wallHit.normal, up)) < 0.35f;
        if (!eval.IsWallVertical)
        {
            eval.FailureReason = "Wall is not vertical";
            return eval;
        }

        Vector3 inwardPos = wallHit.point - (wallHit.normal * 0.15f);
        Vector3 ledgeRayOrigin =
            Vector3.ProjectOnPlane(inwardPos - basePos, up) + basePos + up * (_maxVaultHeight + 0.15f);
        float dropDistance = _maxVaultHeight + 0.15f - (_minVaultHeight * 0.5f);

        if (!UnityPhysics.Raycast(ledgeRayOrigin, -up, out RaycastHit ledgeHit, dropDistance, _vaultLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            eval.FailureReason = "Wall too high (No ledge surface)";
            return eval;
        }

        eval.HasLedgeHit = true;
        eval.LedgeHit = ledgeHit;

        float ledgeHeight = Vector3.Dot(ledgeHit.point - basePos, up);
        eval.LedgeHeight = ledgeHeight;

        if (ledgeHeight < _minVaultHeight)
        {
            eval.FailureReason = $"Ledge too low ({ledgeHeight:F2}m < {_minVaultHeight:F2}m)";
            return eval;
        }

        if (ledgeHeight > _maxVaultHeight)
        {
            eval.FailureReason = $"Ledge too high ({ledgeHeight:F2}m > {_maxVaultHeight:F2}m)";
            return eval;
        }

        eval.IsLedgeSurfaceWalkable = Vector3.Dot(ledgeHit.normal, up) >= 0.7f;
        if (!eval.IsLedgeSurfaceWalkable)
        {
            eval.FailureReason = "Ledge surface too steep";
            return eval;
        }

        Vector3 targetFootPos = ledgeHit.point + (forward * (radius * 1.15f));
        eval.TargetFootPos = targetFootPos;

        Vector3 capsuleBottom = targetFootPos + up * (radius + 0.05f);
        Vector3 capsuleTop = targetFootPos + up * (height - radius);

        eval.IsLandingClear = !UnityPhysics.CheckCapsule(capsuleBottom, capsuleTop, radius * 0.95f, _vaultLayerMask,
            QueryTriggerInteraction.Ignore);
        if (!eval.IsLandingClear)
        {
            eval.FailureReason = "Landing space obstructed";
            return eval;
        }

        eval.CanVault = true;
        return eval;
    }

    private bool CanVault(out Vector3 targetFootPos)
    {
        var eval = EvaluateVault();
        targetFootPos = eval.TargetFootPos;
        return eval.CanVault;
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_fpsCameraController)
        {
            currentRotation = Quaternion.Euler(0f, _fpsCameraController.CurrentYaw, 0f);
        }
        else if (_lookInputVector.sqrMagnitude > 0.001f)
        {
            currentRotation = Quaternion.LookRotation(_lookInputVector, _motor.CharacterUp);
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        UpdatePhaseFromGrounding();
        _currentPhaseHandler?.UpdateVelocity(ref currentVelocity, deltaTime);

        ApplyJumpIfRequested(ref currentVelocity);
        UpdateAnimationState(currentVelocity);
    }

    private void UpdateCrouchHeight(float deltaTime)
    {
        if (State.Phase == LocomotionPhase.Noclip)
        {
            if (_motor && _motor.Capsule && !Mathf.Approximately(State.TargetHeight, _standingHeight))
            {
                State.TargetHeight = _standingHeight;
                _motor.SetCapsuleDimensions(_motor.Capsule.radius, _standingHeight, _standingHeight * 0.5f);
            }

            return;
        }

        if (_isDowned)
        {
            State.TargetHeight = _downedCapsuleHeight;
            float downedHeight =
                Mathf.Lerp(_motor.Capsule.height, State.TargetHeight, deltaTime * _crouchTransitionSpeed);
            _motor.SetCapsuleDimensions(_motor.Capsule.radius, downedHeight, downedHeight * 0.5f);
            return;
        }

        bool wantsToCrouch = _isCrouchInputHeld;

        if (!wantsToCrouch && State.TargetHeight < _standingHeight)
        {
            float radius = _motor.Capsule.radius;
            Vector3 castOrigin = _motor.TransientPosition + _motor.CharacterUp * (_motor.Capsule.height - radius);
            float castDistance = _standingHeight - _motor.Capsule.height;

            if (Physics.SphereCast(castOrigin, radius, _motor.CharacterUp, out _, castDistance, _ceilingCheckLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                wantsToCrouch = true;
            }
        }

        State.TargetHeight = wantsToCrouch ? _crouchingHeight : _standingHeight;
        float newHeight = Mathf.Lerp(_motor.Capsule.height, State.TargetHeight, deltaTime * _crouchTransitionSpeed);

        _motor.SetCapsuleDimensions(_motor.Capsule.radius, newHeight, newHeight * 0.5f);
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (State.Phase == LocomotionPhase.Noclip) return;

        bool becameStable = _motor.GroundingStatus.IsStableOnGround && !_motor.LastGroundingStatus.IsStableOnGround;
        bool becameGroundedOnSlide = _allowJumpingWhenSliding && _motor.GroundingStatus.FoundAnyGround &&
                                     !_motor.LastGroundingStatus.FoundAnyGround && !_motor.MustUnground();

        if (becameStable || becameGroundedOnSlide)
            OnLanded();
        else if (!_motor.GroundingStatus.IsStableOnGround && _motor.LastGroundingStatus.IsStableOnGround)
            OnLeaveStableGround();
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (State.Phase == LocomotionPhase.Noclip)
        {
            State.TimeSinceJumpRequested += deltaTime;
            State.TimeSinceLastJump += deltaTime;
            State.JumpBufferTimer = 0f;
            State.CanWallJump = false;
            State.WallJumpGraceTimer = 0f;
            UpdateCrouchHeight(deltaTime);
            State.WasCrouchingLastFrame = _isCrouchInputHeld;
            return;
        }

        State.TimeSinceJumpRequested += deltaTime;
        State.TimeSinceLastJump += deltaTime;
        State.JumpBufferTimer = Mathf.Max(0f, State.JumpBufferTimer - deltaTime);

        if (State.CurrentJumpStatus == JumpStatus.Requested &&
            State.TimeSinceJumpRequested >= Mathf.Max(_jumpPreGroundingGraceTime, _jumpBufferTime))
        {
            State.CurrentJumpStatus = JumpStatus.None;
            State.CoyoteJumpQueued = false;
        }

        if (IsGroundedForJump())
        {
            State.TimeSinceLastAbleToJump = 0f;
            State.CanWallJump = false;
            State.WallJumpGraceTimer = 0f;
        }
        else
        {
            State.TimeSinceLastAbleToJump += deltaTime;
            State.WallJumpGraceTimer = Mathf.Max(0f, State.WallJumpGraceTimer - deltaTime);
            if (State.WallJumpGraceTimer <= 0f)
                State.CanWallJump = false;

            UpdateProximityWallCheck();
        }

        TryQueueJumpFromBuffer();
        UpdateCrouchHeight(deltaTime);

        State.WasCrouchingLastFrame = _isCrouchInputHeld;
    }

    public bool IsColliderValidForCollisions(Collider coll) => State.Phase != LocomotionPhase.Noclip;

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    private void RegisterWallContact(Vector3 hitNormal)
    {
        float wallDotUp = Mathf.Abs(Vector3.Dot(hitNormal, _motor.CharacterUp));

        if (!(wallDotUp < 0.9f)) return;
        if (State.HasWallJumped && !(Vector3.Dot(State.WallJumpNormal, hitNormal) < 0.5f)) return;

        State.CanWallJump = true;
        State.WallJumpGraceTimer = 0.30f;
        State.WallJumpNormal = hitNormal;
        State.HasWallJumped = false;
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        if (State.Phase == LocomotionPhase.Noclip) return;
        if (_allowWallJump && !_motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable)
            RegisterWallContact(hitNormal);
    }

    private void OnLeaveStableGround()
    {
    }

    private void OnLanded()
    {
        State.HasJumped = false;
        State.HasDoubleJumped = false;
        State.HasWallJumped = false;
        State.CoyoteJumpQueued = false;
        State.CurrentJumpStatus = JumpStatus.None;
        State.TimeSinceJumpRequested = 0f;
        State.TimeSinceLastJump = Mathf.Infinity;
        State.LandingTime = Time.time;
        SwitchPhase(LocomotionPhase.Grounded);
    }

    private void TryQueueJumpFromBuffer()
    {
        if (_isDowned || _isDraggingTeammate) return;
        if (State.Phase is LocomotionPhase.Vaulting or LocomotionPhase.Noclip) return;
        if (State.JumpBufferTimer <= 0f || State.CurrentJumpStatus != JumpStatus.None) return;
        if (State.TimeSinceLastJump < 0.2f) return;

        bool inCoyoteTime = !State.HasJumped
                            && !_motor.GroundingStatus.IsStableOnGround
                            && !_motor.MustUnground()
                            && State.Phase != LocomotionPhase.Vaulting
                            && State.TimeSinceLastAbleToJump <= _jumpPostGroundingGraceTime;

        bool canJumpFromGround = IsGroundedForJump() || inCoyoteTime;
        bool canDoubleJump = _allowDoubleJump
                             && !State.HasDoubleJumped
                             && !_motor.GroundingStatus.IsStableOnGround
                             && !_motor.MustUnground()
                             && !inCoyoteTime
                             && (State.HasJumped || State.TimeSinceLastAbleToJump > _jumpPostGroundingGraceTime);

        bool canWallJump = _allowWallJump && State is { CanWallJump: true, HasWallJumped: false };

        bool canVault = CanVault(out _);

        if (canJumpFromGround || canDoubleJump || canWallJump || canVault)
        {
            State.TimeSinceJumpRequested = 0f;
            State.CurrentJumpStatus = JumpStatus.Requested;
            State.CoyoteJumpQueued = inCoyoteTime;
            State.JumpBufferTimer = 0f;
        }
    }

    private void ApplyJumpIfRequested(ref Vector3 currentVelocity)
    {
        if (_isDowned || _isDraggingTeammate || State.Phase == LocomotionPhase.Noclip) return;
        if (State.CurrentJumpStatus != JumpStatus.Requested) return;

        if (CanVault(out Vector3 vaultTargetPos))
        {
            State.VaultTargetPosition = vaultTargetPos;
            SwitchPhase(LocomotionPhase.Vaulting);

            State.CurrentJumpStatus = JumpStatus.Executed;
            State.HasJumped = true;
            State.CoyoteJumpQueued = false;
            State.TimeSinceLastJump = 0f;
            return;
        }

        bool canWallJump = _allowWallJump && State is { CanWallJump: true, HasWallJumped: false };

        if (canWallJump)
        {
            Vector3 jumpDirection = (State.WallJumpNormal + _motor.CharacterUp).normalized;
            _motor.ForceUnground();
            currentVelocity += (jumpDirection * _jumpSpeed) - Vector3.Project(currentVelocity, _motor.CharacterUp);

            State.CurrentJumpStatus = JumpStatus.Executed;
            State.HasJumped = true;
            State.HasWallJumped = true;
            State.CanWallJump = false;
            State.CoyoteJumpQueued = false;
            State.TimeSinceLastJump = 0f;
        }
        else if (IsGroundedForJump() || State.CoyoteJumpQueued || (!State.HasJumped && !_motor.MustUnground() &&
                                                                   State.TimeSinceLastAbleToJump <=
                                                                   _jumpPostGroundingGraceTime))
        {
            Vector3 jumpDirection = _motor.CharacterUp;
            _motor.ForceUnground();
            currentVelocity += (jumpDirection * _jumpSpeed) - Vector3.Project(currentVelocity, _motor.CharacterUp);

            State.CurrentJumpStatus = JumpStatus.Executed;
            State.HasJumped = true;
            State.CoyoteJumpQueued = false;
            State.TimeSinceLastJump = 0f;
        }
        else if (!_motor.GroundingStatus.IsStableOnGround && _allowDoubleJump && !State.HasDoubleJumped &&
                 (State.HasJumped || State.TimeSinceLastAbleToJump > _jumpPostGroundingGraceTime))
        {
            _motor.ForceUnground();
            currentVelocity += (_motor.CharacterUp * _doubleJumpSpeed) -
                               Vector3.Project(currentVelocity, _motor.CharacterUp);

            State.CurrentJumpStatus = JumpStatus.DoubleJumpUsed;
            State.HasDoubleJumped = true;
            State.HasJumped = true;
            State.CoyoteJumpQueued = false;
            State.TimeSinceLastJump = 0f;
        }

        if (State.CurrentJumpStatus is JumpStatus.Executed or JumpStatus.DoubleJumpUsed)
        {
            State.CurrentJumpStatus = JumpStatus.None;
            State.CoyoteJumpQueued = false;
            SwitchPhase(LocomotionPhase.Airborne);
        }
    }

    private float GetGroundMoveSpeed()
    {
        if (_isDowned)
            return _crawlSpeed;

        float speedMultiplier = 1f;
        if (_isCrouchInputHeld) speedMultiplier = _crouchSpeedMultiplier;
        else if (_isSprintInputHeld) speedMultiplier = _sprintSpeedMultiplier;

        if (_isDraggingTeammate)
            speedMultiplier *= _draggingSpeedMultiplier;

        float globalBuffMult = 1f;

        return _maxStableMoveSpeed * speedMultiplier * globalBuffMult;
    }

    private void UpdateAnimationState(Vector3 currentVelocity)
    {
        LocomotionAnimationState nextState;
        float moveInputMagnitude = _moveInputVector.magnitude;
        float verticalSpeed = Vector3.Dot(currentVelocity, _motor.CharacterUp);

        if (_isDowned)
        {
            nextState = (_dragTarget || moveInputMagnitude > 0.1f)
                ? LocomotionAnimationState.Crawling
                : LocomotionAnimationState.Downed;
        }
        else
        {
            switch (State.Phase)
            {
                case LocomotionPhase.Noclip:
                    nextState = moveInputMagnitude > 0.1f
                        ? (_isSprintInputHeld ? LocomotionAnimationState.Sprinting : LocomotionAnimationState.Walking)
                        : LocomotionAnimationState.Idle;
                    break;
                case LocomotionPhase.Vaulting:
                    nextState = LocomotionAnimationState.Vaulting;
                    break;
                case LocomotionPhase.Airborne:
                    nextState = verticalSpeed >= 0f
                        ? LocomotionAnimationState.Jumping
                        : LocomotionAnimationState.Falling;
                    break;
                case LocomotionPhase.Grounded:
                default:
                {
                    if (State.IsSliding)
                    {
                        nextState = LocomotionAnimationState.Sliding;
                    }
                    else if (_isCrouchInputHeld)
                    {
                        nextState = moveInputMagnitude > 0.1f
                            ? LocomotionAnimationState.CrouchWalking
                            : LocomotionAnimationState.Crouching;
                    }
                    else if (moveInputMagnitude > 0.1f)
                    {
                        nextState = _isSprintInputHeld
                            ? LocomotionAnimationState.Sprinting
                            : LocomotionAnimationState.Walking;
                    }
                    else
                    {
                        nextState = LocomotionAnimationState.Idle;
                    }

                    break;
                }
            }
        }

        if (State.AnimationState == nextState) return;

        State.AnimationState = nextState;
        OnAnimationStateChanged?.Invoke(nextState);
    }


    private void UpdateProximityWallCheck()
    {
        if (_motor.GroundingStatus.IsStableOnGround || !_allowWallJump) return;

        Vector3 origin = _motor.TransientPosition + _motor.CharacterUp * (_motor.Capsule.height * 0.5f);
        float checkDist = _motor.Capsule.radius + 0.2f;

        Vector3[] checkDirs =
        {
            _motor.CharacterForward,
            -_motor.CharacterForward,
            _motor.CharacterRight,
            -_motor.CharacterRight,
            _moveInputVector.sqrMagnitude > 0.01f ? _moveInputVector.normalized : _motor.CharacterForward
        };

        foreach (Vector3 dir in checkDirs)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, checkDist, _wallJumpLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                RegisterWallContact(hit.normal);
                if (State.CanWallJump) break;
            }
        }
    }

    private bool IsGroundedForJump()
    {
        if (!_motor) return false;
        if (_motor.MustUnground()) return false;
        if (State.Phase is LocomotionPhase.Airborne or LocomotionPhase.Noclip) return false;

        return _allowJumpingWhenSliding
            ? (_motor.GroundingStatus.IsStableOnGround || _motor.GroundingStatus.FoundAnyGround)
            : _motor.GroundingStatus.IsStableOnGround;
    }

    private void UpdatePhaseFromGrounding()
    {
        if (State.Phase is LocomotionPhase.Vaulting or LocomotionPhase.Noclip) return;

        LocomotionPhase targetPhase = _motor.GroundingStatus.IsStableOnGround
            ? LocomotionPhase.Grounded
            : LocomotionPhase.Airborne;
        if (targetPhase != State.Phase) SwitchPhase(targetPhase);
    }

    private void SwitchPhase(LocomotionPhase nextPhase)
    {
        if (!_phaseHandlers.TryGetValue(nextPhase, out ILocomotionPhaseHandler nextHandler)) return;
        if (_currentPhaseHandler != null && _currentPhaseHandler.Phase == nextPhase)
        {
            State.Phase = nextPhase;
            return;
        }

        _currentPhaseHandler?.Exit();
        _currentPhaseHandler = nextHandler;
        State.Phase = nextPhase;
        _currentPhaseHandler.Enter();

        OnPhaseChanged?.Invoke(nextPhase);
    }

    public bool IsNoclipEnabled => State.IsNoclipEnabled;

    public bool SetNoclipEnabled(bool mode)
    {
        if (State.IsNoclipEnabled == mode) return false;

        State.IsNoclipEnabled = mode;
        SwitchPhase(mode
            ? LocomotionPhase.Noclip
            : (_motor.GroundingStatus.IsStableOnGround ? LocomotionPhase.Grounded : LocomotionPhase.Airborne));
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_drawOnlyWhenSelected) return;
        DrawLocomotionGizmos(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawLocomotionGizmos(true);
    }

    private void DrawLocomotionGizmos(bool selected)
    {
        if (!GlobalGizmosEnabled || !_enableGizmos) return;
        if (!_motor) return;

        Vector3 pos = transform.position;
        Vector3 up = _motor.CharacterUp;
        Vector3 fwd = transform.forward;
        Vector3 rgt = transform.right;

        float radius = _motor.Capsule ? _motor.Capsule.radius : 0.5f;
        float height = _motor.Capsule ? _motor.Capsule.height : _standingHeight;
        Vector3 center = pos + up * (height * 0.5f);

        if (_drawCapsuleBounds)
        {
            Color capsuleColor = GetStateColor();
            capsuleColor.a = selected ? 0.9f : 0.6f;
            DrawWireCapsule(pos, up, fwd, rgt, radius, height, capsuleColor);
        }

        if (_drawCeilingCheck && height < _standingHeight - 0.05f)
        {
            float checkDist = _standingHeight - height;
            Vector3 checkOrigin = pos + up * (height - radius);
            bool blocked = UnityPhysics.SphereCast(checkOrigin, radius * 0.9f, up, out RaycastHit ceilHit, checkDist,
                _ceilingCheckLayerMask, QueryTriggerInteraction.Ignore);

            if (blocked)
            {
                UnityEditor.Handles.color = new Color(0.95f, 0.25f, 0.25f, 0.9f);
                UnityEditor.Handles.DrawDottedLine(checkOrigin, ceilHit.point, 3f);
                UnityEditor.Handles.DrawWireDisc(ceilHit.point, ceilHit.normal, radius * 0.5f);
                UnityEditor.Handles.Label(ceilHit.point + up * 0.15f, "CEILING BLOCKED",
                    GetDebugMiniLabelStyle(new Color(0.95f, 0.25f, 0.25f, 1f)));
            }
            else
            {
                UnityEditor.Handles.color = new Color(0.2f, 0.9f, 0.3f, 0.6f);
                UnityEditor.Handles.DrawLine(checkOrigin, checkOrigin + up * checkDist);
                UnityEditor.Handles.DrawWireDisc(checkOrigin + up * checkDist, up, radius);
            }
        }

        if (_drawVelocityVector)
        {
            Vector3 velocity = _motor.Velocity;
            float speed = velocity.magnitude;
            if (speed > 0.05f)
            {
                Color velColor = GetStateColor();
                velColor.a = 1f;
                float arrowLen = Mathf.Clamp(speed * 0.3f, 0.5f, 4f);
                Vector3 velDir = velocity.normalized;
                Vector3 velTip = center + velDir * arrowLen;

                UnityEditor.Handles.color = velColor;
                UnityEditor.Handles.DrawLine(center, velTip);
                DrawArrowHead(velTip, velDir, up, 0.18f, velColor);

                UnityEditor.Handles.DrawSolidDisc(center, up, 0.06f);

                if (selected)
                {
                    UnityEditor.Handles.Label(velTip + up * 0.1f, $"{speed:F1} m/s", GetDebugMiniLabelStyle(velColor));
                }
            }
        }

        if (_drawInputVector && _moveInputVector.sqrMagnitude > 0.01f)
        {
            Color inputColor = new Color(1.0f, 0.65f, 0.0f, 0.95f);
            Vector3 inputOrigin = pos + up * 0.05f;
            Vector3 inputDir = _moveInputVector.normalized;
            float inputLen = Mathf.Clamp(_moveInputVector.magnitude * 1.2f, 0.4f, 1.5f);
            Vector3 inputTip = inputOrigin + inputDir * inputLen;

            UnityEditor.Handles.color = inputColor;
            UnityEditor.Handles.DrawLine(inputOrigin, inputTip);
            DrawArrowHead(inputTip, inputDir, up, 0.14f, inputColor);
            UnityEditor.Handles.DrawWireDisc(inputOrigin, up, 0.1f);
        }

        if (_drawGroundStatus)
        {
            if (_motor.GroundingStatus.FoundAnyGround)
            {
                Vector3 gPoint = _motor.GroundingStatus.GroundPoint;
                Vector3 gNormal = _motor.GroundingStatus.GroundNormal;
                bool isStable = _motor.GroundingStatus.IsStableOnGround;
                Color groundColor = isStable ? new Color(0.2f, 0.85f, 0.3f, 0.9f) : new Color(0.95f, 0.3f, 0.2f, 0.9f);

                UnityEditor.Handles.color = groundColor;
                UnityEditor.Handles.DrawWireDisc(gPoint, gNormal, 0.35f);
                UnityEditor.Handles.color = new Color(groundColor.r, groundColor.g, groundColor.b, 0.15f);
                UnityEditor.Handles.DrawSolidDisc(gPoint, gNormal, 0.35f);

                UnityEditor.Handles.color = groundColor;
                Vector3 normalTip = gPoint + gNormal * 0.7f;
                UnityEditor.Handles.DrawLine(gPoint, normalTip);
                DrawArrowHead(normalTip, gNormal, fwd, 0.12f, groundColor);

                float slopeAngle = Vector3.Angle(gNormal, Vector3.up);
                if (selected)
                {
                    string slopeText = isStable ? $"Slope: {slopeAngle:F1}\u00b0" : $"UNSTABLE ({slopeAngle:F1}\u00b0)";
                    UnityEditor.Handles.Label(normalTip + up * 0.1f, slopeText, GetDebugMiniLabelStyle(groundColor));
                }
            }
            else
            {
                if (UnityPhysics.Raycast(pos, -up, out RaycastHit hit, 50f, _ceilingCheckLayerMask,
                        QueryTriggerInteraction.Ignore))
                {
                    UnityEditor.Handles.color = new Color(0.6f, 0.4f, 0.95f, 0.8f);
                    UnityEditor.Handles.DrawDottedLine(pos, hit.point, 3f);
                    UnityEditor.Handles.DrawWireDisc(hit.point, hit.normal, radius * 0.6f);

                    float altitude = hit.distance;
                    if (selected && altitude > 0.3f)
                    {
                        UnityEditor.Handles.Label(hit.point + hit.normal * 0.1f, $"Alt: {altitude:F1}m",
                            GetDebugMiniLabelStyle(new Color(0.6f, 0.4f, 0.95f, 1f)));
                    }
                }
            }
        }

        if (_drawWallAndVaultChecks)
        {
            Vector3 waistPos = pos + up * (height * 0.5f);
            float wallCheckDist = radius + 0.25f;

            UnityEditor.Handles.color = new Color(0.2f, 0.75f, 0.9f, 0.5f);
            UnityEditor.Handles.DrawLine(waistPos, waistPos + fwd * wallCheckDist);
            UnityEditor.Handles.DrawLine(waistPos, waistPos - fwd * wallCheckDist);
            UnityEditor.Handles.DrawLine(waistPos, waistPos + rgt * wallCheckDist);
            UnityEditor.Handles.DrawLine(waistPos, waistPos - rgt * wallCheckDist);

            if (State is { CanWallJump: true, WallJumpNormal: { sqrMagnitude: > 0.1f } })
            {
                Color wjColor = new Color(0.1f, 0.9f, 0.9f, 1f);
                Vector3 wjTip = waistPos + State.WallJumpNormal * 1.2f;
                UnityEditor.Handles.color = wjColor;
                UnityEditor.Handles.DrawLine(waistPos, wjTip);
                DrawArrowHead(wjTip, State.WallJumpNormal, up, 0.15f, wjColor);
                UnityEditor.Handles.Label(wjTip + up * 0.15f, "WALL JUMP READY", GetDebugMiniLabelStyle(wjColor));
            }

            var vaultEval = EvaluateVault();

            Vector3 lookFwd = (Application.isPlaying && _lookInputVector.sqrMagnitude > 0.1f)
                ? _lookInputVector.normalized
                : fwd;
            Vector3 rayOriginMid = pos + up * Mathf.Clamp(_maxVaultHeight * 0.5f, 0.7f, 1.1f);

            UnityEditor.Handles.color = vaultEval.HasWallHit
                ? (vaultEval.CanVault ? Color.green : new Color(0.95f, 0.6f, 0.1f, 0.8f))
                : new Color(0.2f, 0.5f, 1.0f, 0.4f);
            UnityEditor.Handles.DrawLine(rayOriginMid, rayOriginMid + lookFwd * _vaultCheckDistance);

            if (vaultEval.HasWallHit)
            {
                UnityEditor.Handles.color = vaultEval.IsWallVertical ? Color.cyan : Color.red;
                UnityEditor.Handles.DrawWireDisc(vaultEval.WallHit.point, vaultEval.WallHit.normal, 0.08f);

                Vector3 inwardPos = vaultEval.WallHit.point - (vaultEval.WallHit.normal * 0.15f);
                Vector3 ledgeRayOrigin =
                    Vector3.ProjectOnPlane(inwardPos - pos, up) + pos + up * (_maxVaultHeight + 0.15f);
                float dropDistance = _maxVaultHeight + 0.15f - (_minVaultHeight * 0.5f);

                UnityEditor.Handles.color = vaultEval.HasLedgeHit
                    ? (vaultEval.CanVault ? Color.green : new Color(0.95f, 0.35f, 0.2f, 0.9f))
                    : Color.gray;
                UnityEditor.Handles.DrawLine(ledgeRayOrigin, ledgeRayOrigin - up * dropDistance);

                if (vaultEval.HasLedgeHit)
                {
                    UnityEditor.Handles.DrawSolidDisc(vaultEval.LedgeHit.point, vaultEval.LedgeHit.normal, 0.06f);

                    if (vaultEval.CanVault)
                    {
                        DrawWireCapsule(vaultEval.TargetFootPos, up, fwd, rgt, radius, height,
                            new Color(0.2f, 0.9f, 0.4f, 0.6f));
                        UnityEditor.Handles.Label(vaultEval.TargetFootPos + up * height,
                            $"VAULT READY ({vaultEval.LedgeHeight:F2}m)", GetDebugMiniLabelStyle(Color.green));
                    }
                    else if (selected)
                    {
                        Color failColor = new Color(0.95f, 0.35f, 0.2f, 1f);
                        DrawWireCapsule(vaultEval.TargetFootPos, up, fwd, rgt, radius, height,
                            new Color(0.95f, 0.35f, 0.2f, 0.25f));
                        UnityEditor.Handles.Label(vaultEval.TargetFootPos + up * (height * 0.5f),
                            $"CANNOT VAULT:\n{vaultEval.FailureReason}", GetDebugMiniLabelStyle(failColor));
                    }
                }
                else if (selected)
                {
                    UnityEditor.Handles.Label(ledgeRayOrigin - up * (dropDistance * 0.5f),
                        "WALL TOO HIGH\n(No Ledge In Reach)",
                        GetDebugMiniLabelStyle(new Color(0.95f, 0.35f, 0.2f, 1f)));
                }
            }
        }

        if (_drawOverheadStatus)
        {
            Vector3 headPos = pos + up * (height + 0.4f);
            float totalSpeed = _motor.Velocity.magnitude;
            Vector3 planarVel = Vector3.ProjectOnPlane(_motor.Velocity, up);
            float hSpeed = planarVel.magnitude;
            float vSpeed = Vector3.Dot(_motor.Velocity, up);
            float slopeAngle = _motor.GroundingStatus.FoundAnyGround
                ? Vector3.Angle(_motor.GroundingStatus.GroundNormal, Vector3.up)
                : 0f;

            string statusText = $"[LOCO] {State.Phase} | {State.AnimationState}\n" +
                                $"Speed: {totalSpeed:F1} m/s (H:{hSpeed:F1} V:{vSpeed:+0.0;-0.0;0.0})\n" +
                                $"Ground: {(_motor.GroundingStatus.FoundAnyGround ? (_motor.GroundingStatus.IsStableOnGround ? "Stable" : "Unstable") : "Airborne")} ({slopeAngle:F1}\u00b0)\n" +
                                $"Height: {height:F2}m / {_standingHeight:F2}m";

            Color labelColor = GetStateColor();
            var labelStyle = new GUIStyle
            {
                normal = { textColor = labelColor },
                fontSize = selected ? 11 : 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            UnityEditor.Handles.Label(headPos, statusText, labelStyle);
        }
    }

    private Color GetStateColor()
    {
        if (State.Phase == LocomotionPhase.Noclip) return new Color(0.2f, 0.9f, 1f, 1f);
        if (_isDowned) return new Color(0.96f, 0.26f, 0.21f, 1f);
        if (State.Phase == LocomotionPhase.Vaulting) return new Color(0.13f, 0.77f, 0.36f, 1f);
        if (State.IsSliding) return new Color(0.14f, 0.78f, 0.86f, 1f);
        if (State.Phase == LocomotionPhase.Airborne) return new Color(0.66f, 0.44f, 0.98f, 1f);
        if (State.IsSprinting) return new Color(0.96f, 0.62f, 0.07f, 1f);
        if (State.IsCrouching) return new Color(0.92f, 0.45f, 0.15f, 1f);
        return new Color(0.22f, 0.74f, 0.58f, 1f);
    }

    private static void DrawArrowHead(Vector3 tip, Vector3 direction, Vector3 normal, float size, Color color)
    {
        Vector3 right = Vector3.Cross(direction, normal).normalized;
        if (right.sqrMagnitude < 0.01f)
            right = Vector3.Cross(direction, Vector3.forward).normalized;

        Vector3 back = tip - direction * size;
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.DrawLine(tip, back + right * (size * 0.5f));
        UnityEditor.Handles.DrawLine(tip, back - right * (size * 0.5f));
        UnityEditor.Handles.DrawLine(back + right * (size * 0.5f), back - right * (size * 0.5f));
    }

    private static void DrawWireCapsule(Vector3 basePos, Vector3 up, Vector3 forward, Vector3 right, float radius,
        float height, Color color)
    {
        UnityEditor.Handles.color = color;
        float halfHeightMinusRadius = Mathf.Max(0.01f, (height * 0.5f) - radius);
        Vector3 center = basePos + up * (height * 0.5f);
        Vector3 bottomSphere = center - up * halfHeightMinusRadius;
        Vector3 topSphere = center + up * halfHeightMinusRadius;

        UnityEditor.Handles.DrawWireDisc(bottomSphere, up, radius);
        UnityEditor.Handles.DrawWireArc(bottomSphere, right, forward, 180f, radius);
        UnityEditor.Handles.DrawWireArc(bottomSphere, -forward, right, 180f, radius);

        UnityEditor.Handles.DrawWireDisc(topSphere, up, radius);
        UnityEditor.Handles.DrawWireArc(topSphere, -right, forward, 180f, radius);
        UnityEditor.Handles.DrawWireArc(topSphere, forward, right, 180f, radius);

        UnityEditor.Handles.DrawLine(bottomSphere + forward * radius, topSphere + forward * radius);
        UnityEditor.Handles.DrawLine(bottomSphere - forward * radius, topSphere - forward * radius);
        UnityEditor.Handles.DrawLine(bottomSphere + right * radius, topSphere + right * radius);
        UnityEditor.Handles.DrawLine(bottomSphere - right * radius, topSphere - right * radius);
    }

    private static GUIStyle GetDebugMiniLabelStyle(Color textColor)
    {
        return new GUIStyle
        {
            normal = { textColor = textColor },
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }
#endif
}
}