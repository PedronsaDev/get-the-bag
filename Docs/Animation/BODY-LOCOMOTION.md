# Body locomotion capture and replication

This is the current Milestone 2 foundation. BodyAnimationDriver is attached to the Player root and
assigned the body's Animator and visual facing transform. It consumes samples; it does not read
input, move the character or transmit data. CharacterPresentationCapture now supplies local samples.
The existing PurrNet NetworkAnimator handles body Animator parameter replication. See the
[PurrNet knowledge base](../PurrNet.md) before adding networking code.

## Current contract

CharacterPresentationFrame contains world-space relative Velocity (metres/second, excluding support/platform motion), IsGrounded, IsCrouching and IsSprinting. It has no Unity object references. This immutable struct is a local value contract; NetworkAnimator handles the current replicated output.

IsCrouching is the resolved stance after the ceiling check. IsSprinting is resolved after movement
from the request, stable grounding and support-relative planar speed, excluding crouch, slide,
downed, dragging and non-grounded phases. It retains the existing 0.1 m/s activation threshold for
its ADS/camera consumers. The body driver also applies its own existing movement check before
enabling sprint animation. Input collection no longer calculates sprint state from old motor data.

CharacterPresentationCapture calls BodyAnimationDriver.ApplyFrame(frame, deltaTime) after the motor
step. It returns false for a disabled driver, invalid data or invalid Animator configuration.
The existing local capture uses:

```csharp
var frame = new CharacterPresentationFrame(
    locomotion.Motor.BaseVelocity,
    locomotion.Motor.GroundingStatus.IsStableOnGround,
    locomotion.State.IsCrouching,
    locomotion.State.IsSprinting);
bodyAnimationDriver.ApplyFrame(frame, deltaTime);
```

LocomotionController.AfterCharacterUpdate raises OnMovementCompleted on its normal and noclip
exit paths. Capture subscribes/unsubscribes in OnEnable/OnDisable. Keep notifications present when
adding new phases. BaseVelocity excludes support/platform motion, so riding a train does not by
itself request walking. Observers use NetworkAnimator's received parameters; their disabled local
motor/controller is not a source of movement data.

## NetworkAnimator ownership

The saved Player NetworkAnimator references the body Animator with Owner Auth and Auto Sync
Parameters enabled. Its parameter exclusion list is empty. The existing driver can keep writing
floats/bools directly to the Animator. Keep local capture as the owner-side producer and avoid a
second observer-side producer overwriting received values.

The local CharacterPresentationFrame is not sent over the network. A separate
NetworkCharacterPresentation component, frame-broadcast event and custom snapshot buffer are
not prerequisites for this locomotion implementation. Revisit those only for a demonstrated gap.
Future triggers must use NetworkAnimator.SetTrigger. See the
[official NetworkAnimator reference](https://purrnet.dev/docs/plug-n-play-components/network-animator).

## Animator behavior

- HorizontalAxis and VerticalAxis are body-facing axes, normalized against Walk Reference Speed and clamped as one vector. The existing standing blend tree uses unit cardinal positions.
- MoveSpeed is actual grounded planar speed in metres/second. IsGrounded, IsMoving and Idle are also updated.
- The default walk reference speed is 3 m/s as an initial calibration value, not a measurement of the authored clips. Tune it against the locomotion clips.
- Small velocities are removed by Movement Dead Zone. Axis Damping Time controls exponential smoothing. The first frame and a zero deltaTime snap immediately.
- Only yaw from Facing Reference is used. The starter assumes world Y-up. Nonstandard gravity/up and independent visual-yaw playback belong to later capture/driver work.
- Airborne samples suppress ground walking. No jump/fall state or animation has been added yet.
- IsCrouching and IsSprinting are now owned by this driver alongside its six original parameters. Slide, downed, action and upper-body parameters remain outside its current scope.
- ResetPose clears all eight owned parameters and smoothing state. Call it at teleport or presentation reset boundaries. Disable also resets; Bind explicitly resets/rebinds after a model/controller replacement.

## Root-motion prerequisite

The pre-migration audit found body Apply Root Motion enabled. The driver refuses frames while
that flag is enabled and logs a contextual warning once; it never changes the flag automatically.
When reproducing the setup or adding a model, check root trajectories in a duplicate and ensure
the actual animated body uses motor-owned movement. User-reported working playback does not
replace checking that policy on each saved prefab/model.

An assigned driver still requires capture and valid Animator configuration. NetworkAnimator
synchronizes presentation parameters; it does not replace the movement motor or gameplay rules.

## Next implementation steps

1. Preserve working capture and NetworkAnimator setup; verify saved root-motion policy and visual grip/shadow acceptance.
2. Standing idle/walk/strafe/diagonals and replication are user-reported working. Continue with host watching client and client watching host, wall pushing, stopping, platform riding and late observation.
3. Preserve the implemented resolved crouch and sprint path. Check low ceilings and blocked movement in Play Mode, then add airborne/slide/downed animation coverage in small steps.
4. Synchronize newly added body parameters through the same NetworkAnimator. Review root/animation timing under latency and model/ownership changes. Add game-specific aim/action data only when an existing component or sync type cannot satisfy the requirement; custom snapshots are conditional future work.

Earlier compilation/isolated checks covered parameter mapping, facing conversion, clamping, damping,
guards, reset and rebind. The user subsequently reported working locomotion and NetworkAnimator
replication. No new runtime/latency/late-join test was run for this documentation update.
