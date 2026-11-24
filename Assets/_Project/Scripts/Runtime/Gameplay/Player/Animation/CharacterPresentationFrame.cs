using UnityEngine;

namespace Project.Player.Animation
{
public readonly struct CharacterPresentationFrame
{
    public CharacterPresentationFrame(Vector3 velocity, bool isGrounded, bool isCrouching, bool isSprinting, bool isSliding, bool isDowned)
    {
        Velocity = velocity;
        IsGrounded = isGrounded;
        IsCrouching = isCrouching;
        IsSprinting = isSprinting;
        IsSliding = isSliding;
        IsDowned = isDowned;
    }

    public Vector3 Velocity { get; }
    public bool IsGrounded { get; }
    public bool IsCrouching { get;}
    public bool IsSprinting { get; }
    public bool IsSliding { get; }
    public bool IsDowned { get; }
    public bool HasFiniteVelocity => float.IsFinite(Velocity.x) && float.IsFinite(Velocity.y) && float.IsFinite(Velocity.z);
}
}
