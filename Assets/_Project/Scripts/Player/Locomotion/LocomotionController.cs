
namespace Project.Player
{
using System;
using System.Collections.Generic;
using KinematicCharacterController;
using PurrNet;
using UnityEngine;

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
    Vaulting
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
    Vaulting
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
    public float LandingTime;
    public LocomotionPhase Phase;
    public LocomotionAnimationState AnimationState;
    public Vector2 MoveInput;
    public float MoveInputMagnitude;
    public bool IsSprinting;
    public bool IsCrouching;
    public bool WasCrouchingLastFrame;
    public bool IsSliding;
    public float TargetHeight;
    public Vector3 VaultStartPosition;
    public Vector3 VaultTargetPosition;
    public float VaultTimer;
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

            if (_controller._isCrouchInputHeld && !_controller.State.WasCrouchingLastFrame && currentSpeed > _controller._minSpeedToSlide)
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
            currentVelocity = _controller._motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentSpeed;

            var inputRight = Vector3.Cross(_controller._moveInputVector, _controller._motor.CharacterUp);
            var reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _controller._moveInputVector.magnitude;

            var baseSpeed = _controller.State.IsSliding ? 0f : _controller.GetGroundMoveSpeed();
            var targetSpeed = baseSpeed;

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

            var targetMovementVelocity = reorientedInput * targetSpeed;
            var sharpness = _controller.State.IsSliding ? _controller._slideFriction : _controller._stableMovementSharpness;
            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-sharpness * deltaTime));

            if (!_controller.State.IsSliding) 
                return;
            
            var slopeGravity = Vector3.ProjectOnPlane(_controller._gravity, effectiveGroundNormal);
                
            currentVelocity += slopeGravity * (_controller._slideGravityMultiplier * deltaTime);
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
        
        public void Exit() { }

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

        public void Enter() { }
        public void Exit() { }

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
    [SerializeField] private float _jumpPreGroundingGraceTime = 0f;
    [SerializeField] private float _jumpPostGroundingGraceTime = 0f;
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
    [SerializeField] private float _maxVaultHeight = 2f;
    [SerializeField] private float _vaultDuration = 0.7f;

    [Header("Dynamic Gravity")] 
    [SerializeField] private float _fallGravityMultiplier = 0.9f;
    [SerializeField] private float _slideGravityMultiplier = 2.5f;
    [SerializeField] private Vector3 _gravity = new(0, -23f, 0);

    private readonly Dictionary<LocomotionPhase, ILocomotionPhaseHandler> _phaseHandlers = new();
    private ILocomotionPhaseHandler _currentPhaseHandler;
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private bool _isSprintInputHeld;
    private bool _isCrouchInputHeld;
    private bool _isJumpInputHeld;

    private void Awake()
    {
        if (!_motor)
            _motor = GetComponent<KinematicCharacterMotor>();

        if (_motor)
            _motor.CharacterController = this;

        if (!_playerFacade)
            _playerFacade = GetComponent<PlayerFacade>();

        if (!_fpsCameraController)
            _fpsCameraController = GetComponent<FPSCameraController>();
        if (!_fpsCameraController)
            _fpsCameraController = GetComponentInChildren<FPSCameraController>();

        _phaseHandlers.Clear();
        _phaseHandlers[LocomotionPhase.Grounded] = new GroundedPhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Airborne] = new AirbornePhaseHandler(this);
        _phaseHandlers[LocomotionPhase.Vaulting] = new VaultingPhaseHandler(this);
        SwitchPhase(LocomotionPhase.Airborne);
    }

    protected override void OnSpawned()
    {
        _motor.enabled = isOwner;
        enabled = isOwner;

        if (!isOwner) 
            return;

        _networkTransform.enabled = false;
        _motor.SetPosition(transform.parent.position);
        _networkTransform.enabled = true;

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

        SwitchPhase(_motor.GroundingStatus.IsStableOnGround ? LocomotionPhase.Grounded : LocomotionPhase.Airborne);
    }

    private void Update()
    {
        if (!isOwner) return;

        if (!_playerFacade)
            _playerFacade = GetComponent<PlayerFacade>();

        if (!_playerFacade) return;

        var input = _playerFacade.CurrentInput;
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
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, _motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, _motor.CharacterUp).normalized;
        }

        _lookInputVector = cameraPlanarDirection;
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, _motor.CharacterUp);
        _moveInputVector = cameraPlanarRotation * moveInputVector;
        
        _isSprintInputHeld = inputs.SprintHeld && inputs is { CrouchHeld: false, MoveAxisForward: > 0.1f };
        _isCrouchInputHeld = inputs.CrouchHeld;
        _isJumpInputHeld = inputs.JumpHeld;

        State.MoveInput = new Vector2(inputs.MoveAxisRight, inputs.MoveAxisForward);
        State.MoveInputMagnitude = moveInputVector.magnitude;
        State.IsSprinting = _isSprintInputHeld;
        State.IsCrouching = _isCrouchInputHeld;

        if (inputs.JumpDown && State.CurrentJumpStatus == JumpStatus.None)
        {
            State.JumpBufferTimer = _jumpBufferTime;
        }

        TryQueueJumpFromBuffer();
    }

    private bool CanVault(out Vector3 targetFootPos)
    {
        targetFootPos = Vector3.zero;

        Vector3 forward = _lookInputVector.sqrMagnitude > 0.1f ? _lookInputVector.normalized : _motor.CharacterForward;

        Vector3 chestPos = _motor.TransientPosition; //+ _motor.CharacterUp; //* (_motor.Capsule.height * 0.5f);

        if (Physics.Raycast(chestPos, forward, out RaycastHit wallHit, _vaultCheckDistance, _vaultLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(Vector3.Dot(wallHit.normal, _motor.CharacterUp)) < 0.2f)
            {
                Vector3 ledgeRayOrigin = wallHit.point - (wallHit.normal * 0.1f) + (_motor.CharacterUp * _maxVaultHeight);

                if (Physics.Raycast(ledgeRayOrigin, -_motor.CharacterUp, out RaycastHit ledgeHit, _maxVaultHeight, _vaultLayerMask, QueryTriggerInteraction.Ignore))
                {
                    targetFootPos = ledgeHit.point + (forward * (_motor.Capsule.radius * 1.2f));
                    Vector3 capsuleBottom = targetFootPos + _motor.CharacterUp * (_motor.Capsule.radius + 0.05f);
                    Vector3 capsuleTop = targetFootPos + _motor.CharacterUp * (_motor.Capsule.height - _motor.Capsule.radius);

                    if (!Physics.CheckCapsule(capsuleBottom, capsuleTop, _motor.Capsule.radius, _vaultLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
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
        bool wantsToCrouch = _isCrouchInputHeld;

        if (!wantsToCrouch && State.TargetHeight < _standingHeight)
        {
            float radius = _motor.Capsule.radius;
            Vector3 castOrigin = _motor.TransientPosition + _motor.CharacterUp * (_motor.Capsule.height - radius);
            float castDistance = _standingHeight - _motor.Capsule.height;

            if (Physics.SphereCast(castOrigin, radius, _motor.CharacterUp, out _, castDistance, _ceilingCheckLayerMask, QueryTriggerInteraction.Ignore))
            {
                wantsToCrouch = true;
            }
        }

        State.TargetHeight = wantsToCrouch ? _crouchingHeight : _standingHeight;
        float newHeight = Mathf.Lerp(_motor.Capsule.height, State.TargetHeight, deltaTime * _crouchTransitionSpeed);

        _motor.SetCapsuleDimensions(_motor.Capsule.radius, newHeight, newHeight * 0.5f);
    }

    public void BeforeCharacterUpdate(float deltaTime) { }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (_motor.GroundingStatus.IsStableOnGround && !_motor.LastGroundingStatus.IsStableOnGround)
            OnLanded();
        else if (!_motor.GroundingStatus.IsStableOnGround && _motor.LastGroundingStatus.IsStableOnGround)
            OnLeaveStableGround();
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        State.TimeSinceJumpRequested += deltaTime;
        State.TimeSinceLastJump += deltaTime; 
        State.JumpBufferTimer = Mathf.Max(0f, State.JumpBufferTimer - deltaTime);

        if (State.CurrentJumpStatus == JumpStatus.Requested && State.TimeSinceJumpRequested >= _jumpPreGroundingGraceTime)
            State.CurrentJumpStatus = JumpStatus.None;

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

    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }

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

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        if (_allowWallJump && !_motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable)
            RegisterWallContact(hitNormal);
    }

    private void OnLeaveStableGround() { }

    private void OnLanded()
    {
        State.HasJumped = false;
        State.HasDoubleJumped = false;
        State.HasWallJumped = false;
        State.CurrentJumpStatus = JumpStatus.None;
        State.TimeSinceJumpRequested = 0f;
        State.LandingTime = Time.time;
        SwitchPhase(LocomotionPhase.Grounded);
    }

    private void TryQueueJumpFromBuffer()
    {
        if (State.Phase == LocomotionPhase.Vaulting) return; // FIX: Do not queue jumps while vaulting
        if (State.JumpBufferTimer <= 0f || State.CurrentJumpStatus != JumpStatus.None) return;
        if (State.TimeSinceLastJump < 0.2f) return;

        bool inCoyoteTime = !State.HasJumped && State.TimeSinceLastAbleToJump <= _jumpPostGroundingGraceTime;
        bool canJumpFromGround = IsGroundedForJump() || inCoyoteTime;
        bool canDoubleJump = _allowDoubleJump && !State.HasDoubleJumped && !_motor.GroundingStatus.IsStableOnGround;
        bool canWallJump = _allowWallJump && State is { CanWallJump: true, HasWallJumped: false };
        
        bool canVault = CanVault(out _);

        if (canJumpFromGround || canDoubleJump || canWallJump || canVault)
        {
            State.TimeSinceJumpRequested = 0f;
            State.CurrentJumpStatus = JumpStatus.Requested;
            State.JumpBufferTimer = 0f;
        }
    }

    private void ApplyJumpIfRequested(ref Vector3 currentVelocity)
    {
        if (State.CurrentJumpStatus != JumpStatus.Requested) return;

        if (CanVault(out Vector3 vaultTargetPos))
        {
            State.VaultTargetPosition = vaultTargetPos;
            SwitchPhase(LocomotionPhase.Vaulting);
            
            State.CurrentJumpStatus = JumpStatus.Executed;
            State.HasJumped = true;
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
            State.TimeSinceLastJump = 0f;
        }
        else if (IsGroundedForJump() || (!State.HasJumped && State.TimeSinceLastAbleToJump <= _jumpPostGroundingGraceTime))
        {
            Vector3 jumpDirection = _motor.CharacterUp;
            _motor.ForceUnground();
            currentVelocity += (jumpDirection * _jumpSpeed) - Vector3.Project(currentVelocity, _motor.CharacterUp);

            State.CurrentJumpStatus = JumpStatus.Executed;
            State.HasJumped = true;
            State.TimeSinceLastJump = 0f;
        }
        else if (!_motor.GroundingStatus.IsStableOnGround && _allowDoubleJump && !State.HasDoubleJumped)
        {
            _motor.ForceUnground();
            currentVelocity += (_motor.CharacterUp * _doubleJumpSpeed) - Vector3.Project(currentVelocity, _motor.CharacterUp);
            
            State.CurrentJumpStatus = JumpStatus.DoubleJumpUsed;
            State.HasDoubleJumped = true;
            State.TimeSinceLastJump = 0f;
        }

        if (State.CurrentJumpStatus is JumpStatus.Executed or JumpStatus.DoubleJumpUsed)
        {
            State.CurrentJumpStatus = JumpStatus.None;
            SwitchPhase(LocomotionPhase.Airborne);
        }
    }

    private float GetGroundMoveSpeed()
    {
        float speedMultiplier = 1f;
        if (_isCrouchInputHeld) speedMultiplier = _crouchSpeedMultiplier;
        else if (_isSprintInputHeld) speedMultiplier = _sprintSpeedMultiplier;

        float globalBuffMult = 1f;

        return _maxStableMoveSpeed * speedMultiplier * globalBuffMult;
    }

    private void UpdateAnimationState(Vector3 currentVelocity)
    {
        LocomotionAnimationState nextState;
        float moveInputMagnitude = _moveInputVector.magnitude;
        float verticalSpeed = Vector3.Dot(currentVelocity, _motor.CharacterUp);

        switch (State.Phase)
        {
            case LocomotionPhase.Vaulting:
                nextState = LocomotionAnimationState.Vaulting;
                break;
            case LocomotionPhase.Airborne:
                nextState = verticalSpeed >= 0f ? LocomotionAnimationState.Jumping : LocomotionAnimationState.Falling;
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
                    nextState = moveInputMagnitude > 0.1f ? LocomotionAnimationState.CrouchWalking : LocomotionAnimationState.Crouching;
                }
                else if (moveInputMagnitude > 0.1f)
                {
                    nextState = _isSprintInputHeld ? LocomotionAnimationState.Sprinting : LocomotionAnimationState.Walking;
                }
                else
                {
                    nextState = LocomotionAnimationState.Idle;
                }
                break;
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
            if (Physics.Raycast(origin, dir, out RaycastHit hit, checkDist, _wallJumpLayerMask, QueryTriggerInteraction.Ignore))
            {
                RegisterWallContact(hit.normal);
                if (State.CanWallJump) break;
            }
        }
    }

    private bool IsGroundedForJump() => _allowJumpingWhenSliding
        ? _motor.GroundingStatus.FoundAnyGround
        : _motor.GroundingStatus.IsStableOnGround;

    private void UpdatePhaseFromGrounding()
    {
        if (State.Phase == LocomotionPhase.Vaulting) return; 

        LocomotionPhase targetPhase = _motor.GroundingStatus.IsStableOnGround ? LocomotionPhase.Grounded : LocomotionPhase.Airborne;
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

#if UNITY_EDITOR
    
    private void OnDrawGizmosSelected()
    {
        if (!_motor) return;

        Gizmos.color = Color.yellow;
        float radius = _motor.Capsule.radius;
        Vector3 castOrigin = transform.position + _motor.CharacterUp * (_motor.Capsule.height - radius);
        float castDistance = _standingHeight - _motor.Capsule.height;
        Gizmos.DrawWireSphere(castOrigin, radius);
        Gizmos.DrawLine(castOrigin, castOrigin + _motor.CharacterUp * castDistance);
        Gizmos.DrawWireSphere(castOrigin + _motor.CharacterUp * castDistance, radius);

        Gizmos.color = Color.cyan;
        Vector3 wallCheckOrigin = transform.position + _motor.CharacterUp * (_motor.Capsule.height * 0.5f);
        float wallCheckDist = _motor.Capsule.radius + 0.2f;
        Gizmos.DrawRay(wallCheckOrigin, transform.forward * wallCheckDist);
        Gizmos.DrawRay(wallCheckOrigin, -transform.forward * wallCheckDist);
        Gizmos.DrawRay(wallCheckOrigin, transform.right * wallCheckDist);
        Gizmos.DrawRay(wallCheckOrigin, -transform.right * wallCheckDist);

        Gizmos.color = Color.blue;
        Vector3 chestPos = transform.position + _motor.CharacterUp * (_motor.Capsule.height * 0.7f);
        
        Vector3 forward = (Application.isPlaying && _lookInputVector.sqrMagnitude > 0.1f) ? _lookInputVector.normalized : transform.forward;
        
        Gizmos.DrawRay(chestPos, forward * _vaultCheckDistance);

        if (!Physics.Raycast(chestPos, forward, out RaycastHit wallHit, _vaultCheckDistance, _vaultLayerMask,
                QueryTriggerInteraction.Ignore)) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(wallHit.point, 0.05f);

        Vector3 ledgeRayOrigin = wallHit.point - (wallHit.normal * 0.1f) + (_motor.CharacterUp * _maxVaultHeight);
            
        Gizmos.DrawRay(ledgeRayOrigin, -_motor.CharacterUp * _maxVaultHeight);

        if (Physics.Raycast(ledgeRayOrigin, -_motor.CharacterUp, out RaycastHit ledgeHit, _maxVaultHeight, _vaultLayerMask, QueryTriggerInteraction.Ignore))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(ledgeHit.point, 0.05f);
                
            Vector3 targetFootPos = ledgeHit.point + (forward * _motor.Capsule.radius);
            Vector3 capsuleBottom = targetFootPos + _motor.CharacterUp * (_motor.Capsule.radius + 0.05f);
            Vector3 capsuleTop = targetFootPos + _motor.CharacterUp * (_motor.Capsule.height - _motor.Capsule.radius);
                
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(capsuleBottom, _motor.Capsule.radius);
            Gizmos.DrawWireSphere(capsuleTop, _motor.Capsule.radius);
            Gizmos.DrawLine(capsuleBottom + transform.right * _motor.Capsule.radius, capsuleTop + transform.right * _motor.Capsule.radius);
            Gizmos.DrawLine(capsuleBottom - transform.right * _motor.Capsule.radius, capsuleTop - transform.right * _motor.Capsule.radius);
        }
    }
#endif
}
}