using System;
using UnityEngine;

namespace Project.Player.Locomotion
{
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
    public float VaultDuration;
    public float VaultEntrySpeed;
    public Vector3 VaultInward;
    public Vector3 VaultExpectedPosition;
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
    public Vector3 InwardDirection;
    public float LedgeHeight;
    public string FailureReason;
}

}
