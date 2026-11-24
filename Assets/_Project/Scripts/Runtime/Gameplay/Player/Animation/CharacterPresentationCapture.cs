using Project.Logging;
using Project.Player.Locomotion;
using UnityEngine;

namespace Project.Player.Animation
{
public class CharacterPresentationCapture : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private LocomotionController _locomotionController;

    [SerializeField] private BodyAnimationDriver _bodyAnimationDriver;

    private void Awake()
    {
        if (!_locomotionController)
            _locomotionController = GetComponentInChildren<LocomotionController>();

        if (!_bodyAnimationDriver)
            _bodyAnimationDriver = GetComponent<BodyAnimationDriver>();
    }

    private void OnEnable()
    {
        if (_locomotionController)
            _locomotionController.OnMovementCompleted += HandleMovementCompleted;
        else
            AppLog.LogError(LogChannel.Player,
                "Character presentation capture requires a LocomotionController reference.", this);
    }

    private void OnDisable()
    {
        if (_locomotionController)
            _locomotionController.OnMovementCompleted -= HandleMovementCompleted;
    }

    private void HandleMovementCompleted(float dt)
    {
        var frame = new CharacterPresentationFrame(_locomotionController.Motor.BaseVelocity,
            _locomotionController.Motor.GroundingStatus.IsStableOnGround, _locomotionController.State.IsCrouching,
            _locomotionController.State.IsSprinting, _locomotionController.State.IsSliding, _locomotionController.State.IsDowned);
        _bodyAnimationDriver.ApplyFrame(frame, dt);
    }
}
}