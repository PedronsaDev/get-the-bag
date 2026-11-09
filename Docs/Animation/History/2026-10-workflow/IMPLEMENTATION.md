> Historical snapshot preserved on 6 November 2026. Superseded by [the current workflow](../../README.md). Statements below describe earlier plans or checkpoints, not current implementation status.

# Ordered implementation roadmap

Read [the design](README.md) first and use [the authoring checklist](AUTHORING.md) for assets. Every type/path below is a proposed implementation target unless the current audit identifies it as existing. Each milestone should be a reviewable change with its own acceptance gate. Preserve working tuning and migrate one responsibility at a time.

Preparatory cleanup and Milestone 0 are recorded in [the current-state record](CURRENT-STATE.md) and [the baseline/conventions](../../MILESTONE-0.md). The original connected-observer baseline includes known presentation failures. Milestone 1 now includes the current rig/world-pistol setup, [resource ownership extraction](../../WEAPON-LOADING.md), [dual FP/world bindings with visibility](../../DUAL-VIEW.md), tested on connected host/client Editors, and [Editor validation](../../VALIDATION.md). The body root-motion migration, shadow-pose review and final visual acceptance still remain. Preserve the completed aim-direction correction.

## Milestone 0 — Capture the baseline and freeze conventions

**Recorded:** [inventory, host/observer evidence, conventions and repeat procedure](../../MILESTONE-0.md). Use that frozen baseline for subsequent milestones.

**Scope:** the current pistol, current FP arms and current Humanoid body. No new bob, turn warping or generalized retargeting.

1. Inventory the Player prefab, current renderers, Animator Controllers/masks, weapon definitions, FP/world prefabs, action clips, rig constraints, effect emitters and Addressable IDs.
2. Record a host/observer baseline of walking, strafing, aiming, fire, reload, switch and downed. Identify exactly which parts already work remotely. Save named test captures/settings, not just a subjective memory.
3. Record the rig families, canonical forward/up convention, handedness, unit scale, named semantic sockets, root-motion policy and clip import presets. Keep the current FP Resample Curves setting until export tests justify changing it.
4. Document valid/unsupported model/weapon combinations and optional versus required clips.
5. Preserve GUIDs and all current unstaged/staged work. Mark earlier design proposals as historical via the canonical docs entry point; do not delete their useful evidence.

**Deliverable:** a validated asset inventory and a repeatable one-pistol test case. Existing gameplay remains the comparison baseline. Current source inspection alone is not a substitute for a real second-client capture.

## Milestone 1 — Make one character's visual binding obvious

**First implementation milestone to do next.** Its goal is a reliable two-view binding, not a complete action framework.

1. Add a minimal CharacterVisualDefinition/RigBinding for the current body: body Animator, supported Humanoid bones, world weapon socket, RigBuilder/rigs, aim target and renderer groups. Cache bindings at spawn/model change, never search bone names every frame.
2. Add/resolve the current pistol's WeaponPresentationDefinition (or a temporary explicit binding asset): world weapon prefab, current combined FP prefab, compatible FP arms family and body holding family. Keep existing WeaponDefinition prefab fields as an adapter during migration; choose one authoritative configuration source and warn on conflicting values.
3. Extend PlayerWeaponPresenter's existing generation-safe Addressables lifecycle into two VisualBindings: WorldWeapon for every visible world character, FirstPersonWeapon only for the owner. Each owns its handle, instance and cleanup; both belong to the same equip generation.
4. Bind the world weapon to the body's dominant-hand socket. Author/validate the correct grip pose and local socket offset. Make the world prefab weapon-only, so spawning it never creates another set of FP arms.
5. Add a small character-level visibility controller: owner FP visible, owner world body/weapon supplies shadows, observers see world body/weapon only. Move the existing ShadowsOnly responsibility out of weapon selection incrementally. Allocate effect/audio ownership once per shot/recipient.
6. Disable automatic body root-motion application and validate locomotion clips' root trajectories in a duplicate test setup before migrating the actual prefab. Preserve the motor's movement/collision behavior.
7. Preserve the preparatory PlayerAimTarget correction (camera world forward) and keep the current target NetworkTransform temporarily. Its four-yaw/pitch regression passed; repeat on a connected observer when the visual binding is ready.
8. Add editor validation for the required bindings, renderer groups, duplicate Animator/root writers, Addressable references and supported rig family. The tool should point to the invalid asset/component.

**Gate:** owner and observer can display the pistol with exactly one appropriate body/arm representation per camera. Owner shadow includes a world pistol. Rapid equip cancellation releases both view bindings. A visual failure never changes weapon inventory or server behavior.

**Keep this deliberately small:** no appearance selection UI, weighted inspect picker or new locomotion graph in this change. The profile fields establish where those will plug in.

## Milestone 2 — Local locomotion capture with PurrNet Animator replication

**Current decision, 29 October:** use `CharacterPresentationFrame`, `CharacterPresentationCapture`
and `BodyAnimationDriver` locally, with the existing PurrNet `NetworkAnimator` replicating body
parameters. Standing locomotion and replication are user-reported working. A custom
`NetworkCharacterPresentation` and snapshot sender are not required for this milestone.
See [the PurrNet reference](../../../PurrNet.md) and [current body workflow](../../BODY-LOCOMOTION.md).

1. Keep a small local frame independent of Unity object references. Velocity/grounded already exist; extend it with resolved stance and phase as their animation coverage is implemented. It does not need network metadata merely to drive the local Animator.
2. Capture post-motor data for the owner. Use actual velocity for body locomotion, existing input for FP movement response. Normalize world velocity into the visual body's current facing space.
3. Use NetworkAnimator's owner authority and automatic parameter sync for the body. Keep existing root NetworkTransform replication. Observer motors remain disabled, and must not feed default state over received parameters. Synchronize only the parameters needed by that body controller; triggers go through NetworkAnimator.SetTrigger.
4. Inspect the installed built-in timing/interpolation behavior when reviewing root/body/aim consistency under latency. Add custom playback or timestamp data only for a measured requirement the built-ins do not cover; avoid adding another root writer.
5. Keep the BodyAnimationDriver as the owner-side parameter adapter. Observer Animators receive the replicated parameter stream. Consolidate redundant controller parameters only in a later compatible migration.
6. Start with standing idle and forward/back/strafe. Use separate crouch coverage and clear phase transitions next. Add gait nominal speeds and bounded state play rates. Confirm movement stops when blocked and does not walk merely because input is held.
7. Verify stopping, teleport reset, moving supports, interrupted visibility, late join, model changes and ownership changes using the built-in path. Record remaining gaps before designing any replacement transport.

**Gate:** a second connected client sees idle/walk/strafe/crouch and stable state changes. Test both host/client directions, latency and late observation. Diagonal blend, wall pushing and platform riding are reviewed. A local camera/input provider is unnecessary for observer animation.

## Milestone 3 — Put normal reload on a shared action record and clock

**Proposed ownership:** common serializable action records beside existing equipment/shot data; authority/selection in PlayerWeaponController; a small PurrNet presentation-clock adapter at the networking/player boundary.

1. Define actor/equipment/action identity, action ID, variant ID, start tick, frozen duration and status. Keep a latest action snapshot that late join can reconstruct. Preserve the existing equipment revision and ammo fields.
2. Implement/verify the clock adapter using the installed tick API. Explicitly test separate host/client offsets and clock correction. Move the server reload deadline into the same clock policy atomically; define pause/time-scaling behavior.
3. On accepted normal reload, freeze the selected duration and variant. Continue the existing authoritative reserve transfer at the gameplay deadline.
4. Publish active/completed/cancelled action identity. Include current action in spawn/equipment reconstruction. Cancelling an old sequence cannot cancel its replacement.
5. Extend shot identity with equipment revision. Deduplicate accepted shots and reject visuals from old equips, including switching away and back to the same weapon ID.
6. Adapt old WeaponInstance.Fired/ReloadingChanged consumers to the record while preserving behavior. There must be one playback entry point per view, not old and new callbacks both firing.

**Gate:** host and remote agree on reload duration and selected normal variant; late loading joins at current phase; cancellation/switch/downed/despawn cleans up without granting ammo. Confirmed-only latency is documented. No prediction added yet.

## Milestone 4 — Bind one action to both perspectives

**Proposed ownership:** ViewMotionSet resolver, first-person action player (migration of WeaponAnimator), BodyAnimationDriver action slot, world mechanism action adapter.

1. Create controller templates with fixed slots: FP Idle/Fire/Action; body locomotion + WeaponHold + masked Action; world mechanism Idle/Action/Fire as needed. Maintain one controller template per compatible rig family.
2. Create per-instance AnimatorOverrideControllers. Resolve clips by semantic action/variant IDs, install overrides at action boundaries and start/seek the correct slot. Do not mutate controller assets or recreate overrides per frame.
3. Replace manual `_reloadClipDuration` usage with the actual resolved clip length. Validate base state speed/multiplier and keep fire cadence/reload deadline independent of clip lengths.
4. Author/import the current pistol's world holding and normal reload body clips. Add the world magazine/slide motion where separate from the body clip. Both views use the same phase; authored contact points use agreed normalized marker phases.
5. Implement an action instance that owns playback/props/IK weights and can cancel idempotently. Reconstruct magazine/slide visibility at a seeked phase; skip old one-shot sounds. Explicitly restore state at action end, failure and cancellation.
6. Forward the existing ADS/recoil/sway components one FP context containing aim weight, action masks and frame time. Preserve their per-weapon tuning; remove scattered gameplay queries gradually.
7. Validate clip bindings, mask/rig compatibility, state hashes, timings and fallback rules in the editor. Retire the hard-coded Reload event path only after the current fire/reload regression passes through the new player.

**Gate:** the owner and observer see the same normal reload/action phase with perspective-appropriate motion. Moving during reload preserves legs. Fire immediately after reload cannot be interrupted by an old cancel. Missing visual assets fail gracefully and report a precise validation error.

## Milestone 5 — Consolidate body aiming and remove target transform replication

The target-stream replacement is conditional: compare the working NetworkTransform with
NetworkAnimator-driven aim parameters or an existing sync type first. A custom aim snapshot
path is an option only if the current/built-in paths fail the actual timing or authoring requirement.

1. Derive gameplay aim from one normalized direction/quaternion. Separate camera cosmetic pivots from that aim source.
2. If replacing target replication, reconstruct BodyAimTarget from the chosen replicated aim data. Preserve owner responsiveness and verify observer timing against the existing root stream; add timestamped playback only if required.
3. Calibrate the holding pose and spine axes per RigBinding. Start with chest pitch and body yaw following gameplay yaw; add limited yaw correction only where there is a real body-yaw difference.
4. Replace one full-strength chest hinge with calibrated distributed spine/chest weights if needed. Keep head look independent and clamp pitch/yaw. Fade corrections during authored actions that need to own the upper body.
5. Test the selected replacement on a real observer before removing BodyAimTarget's NetworkTransform. If the current stream remains the simplest adequate solution, retain it. Keep the pawn's actual movement NetworkTransform. Targets must have one writer.
6. Update rig targets before animation evaluation. Verify Animator/RigBuilder update mode and update order; do not try to correct the same spine again in LateUpdate.

**Gate:** a remote player can look around while idle, continuously and without firing. World muzzle/holding direction is believable while strafing/crouching; feet do not tilt with pitch. No target feedback loop, double-yaw rotation or duplicate target stream remains.

## Milestone 6 — Add support-hand adaptation and prop cleanup

1. Bind world grip and elbow hints by semantic name per weapon/body pair. The dominant hand remains weapon master in this first scheme.
2. Add Two Bone IK support-hand correction after body aiming. Calibrate offsets on the target body, not by copying Generic FP bone transforms.
3. Define phase curves/markers for support-hand release/reacquire. Reload owns authored hand choreography while the IK weight fades out.
4. Add default prop-rest state and action-scoped prop overrides for magazine/bolt/slide. Reconstruct visibility at phase; completion/cancel clears overrides and reapplies persistent mechanism state.
5. Add a debug view showing active action, channel ownership, phase and hand weights. Use UI Toolkit for editor controls under the existing project conventions.

**Gate:** different grips and reload phases do not fight the body clip. Switching during hand release or disabling a visual leaves no stale target or hidden magazine.

## Milestone 7 — Prove extensibility with empty reload

1. Add `Empty` beside the existing `Tactical` variant in the pistol action catalog. Capture eligibility from server state before reload changes ammunition; keep existing default IDs valid.
2. Add gameplay duration policy for the new variant if its allowed reload time differs. Stat resolution supplies duration; the animation record freezes the selected result.
3. Author FP/body/mechanism motions for the new variant and bind them in each compatible motion set. No new network message or dedicated EmptyReload animation script is added.
4. Add persistent slide-lock presentation for supporting pistols, including the last accepted shot/post-shot ammo state. Keep an explicit closed/open idle mechanism state so a default idle clip cannot close the slide accidentally.
5. Define cancel-at-phase behavior for both variants and synchronize it with the gameplay policy. Do not simulate a chamber round unless gameplay adds that concept.
6. Confirm a weapon with only Tactical still works through an explicit supported-variant policy; incompatible Empty behavior must not silently play the wrong reload.

**Gate:** old weapons retain behavior; empty versus non-empty selection is correct at start, matches across clients, survives late join, and restores slide/magazine state on cancellation.

## Milestone 8 — Add common/rare inspect without graph duplication

1. Add the semantic Inspect action and eligibility/interruption policy. It is optional presentation, but its replicated timing still needs a chosen identity.
2. Add Common and Rare variants with designer-authored weights and optional conditions. Validate nonnegative finite weights and handle a set with no eligible variants explicitly.
3. Select once for the accepted action, then replicate the chosen ID. Use a defined seed/selection rule only if deterministic variation is required; do not let observers call independent Random.Range.
4. Bind clips to the existing generic Action slot for FP/body/mechanism. A missing optional body inspect may fall back to holding with the same action phase and no required prop manipulation.
5. Fire/reload/equip/downed cancels inspect according to policy. Ensure cancel cleanup cannot interrupt the replacing action.

**Gate:** forcing Rare shows the selected variant on all supported views; normal weighted selection is stable and new variants require data/content rather than a new state graph. Existing Fire/Reload paths pass unchanged.

## Milestone 9 — Prove content scalability with a second weapon and appearance

1. Add one rifle using the same gameplay/presentation interfaces. Supply its world prefab, compatible FP view, holding family, action bindings and tuning.
2. Add a second body skin on the same compatible Humanoid family, validating avatar, sockets and grip calibration. Reuse body clips where acceptable.
3. Add one different FP arms family only after the shared-family path works. Supply its compatible clips/prefabs explicitly, with tooling showing supported weapon/arms pairs.
4. Build a simple resolver/registration workflow that reports exact override/default/fallback chosen. Cache by content identity; clear instance data on load/rebind.
5. Profile asset duplication and loading before deciding to replace combined FP views with a shared runtime-skinned armature. Preserve the combined path as a supported authoring option.

**Gate:** two weapons and two supported appearances work without new per-weapon scripts. Unsupported pairs produce a clear validation result. Character/model changes clear old handles and rig targets; gameplay identity survives cosmetic changes.

## Milestone 10 — Visual body turning, head look and optional local legs

1. Introduce independent WorldVisualYaw, leaving the existing motor/camera yaw behavior intact. Test root-relative rotation math while the actor rotates.
2. Implement limited idle torso yaw, hysteresis and visual turn-in-place using authored turn clips and a single visual-yaw curve owner. Moving/airborne/full-body actions have explicit facing rules.
3. Recompute directional locomotion and relative aim using visual body yaw, including observers on the same render clock. Never add aim yaw twice.
4. Add head/neck correction with rig-specific limits after torso aim. Release it when an authored action owns head motion.
5. Add owner lower-body geometry only if desired: skin it to the world-body skeleton, hide upper-body/head from that camera and test the neck/waist seam. Keep a full world representation for shadows and other cameras.

**Gate:** standing turns plant/reposition feet plausibly; strafe direction remains correct; looking down shows one coherent body; owner shadow and remote character agree sufficiently. No extra independent leg Animator or camera tied directly to animated head bob.

## Milestone 11 — Foot contact and first-person polish

1. Add restrained foot IK/pelvis correction after verifying clip speed and turn coverage. Release in air/vault/downed actions, limit reach/slope, exclude own colliders and test moving supports.
2. Add ground-distance-driven FP bob, then jump/landing response one at a time. Coordinate phase/amplitude with authored idle and the existing camera impact; avoid duplicate impact triggers.
3. Evaluate camera recoil as a separate gameplay decision. The existing camera recoil API changes shooting orientation; visual recoil does not authorize that change.
4. Prototype a dedicated FP renderer/FOV/clipping solution only if needed. Review near walls, shadows, post-processing, reflections and spectator cameras in the actual Unity renderer.
5. Add distance/visibility budgets for body rigs, IK and LOD; avoid globally pausing authoritative gameplay when a mesh is culled. Hidden owner world bodies still need shadow pose evaluation.

**Gate:** polish improves the combined result at different frame rates, supports accessibility motion settings, and does not change movement, shot authority or remote pose identity.

## Milestone 12 — Release/readiness checks

Run a matrix appropriate to each milestone rather than repeatedly testing everything after every asset edit:

| Area | Required cases |
|---|---|
| Network roles | Host owner, separate client owner, observing peer; dedicated server without visual dependencies before release |
| Timing | Delayed/lost/out-of-order pose samples, action arrival delay, clock correction, late join during reload |
| Identity | Duplicate shot, cancelled old action, switch away/back to same weapon, ownership change, despawn/reconnect |
| Assets | Slow Addressables, cancelled load, missing optional clip, invalid required binding, unsupported body/arms pair |
| Movement | Forward/back/strafe/diagonal, wall pushing, crouch/sprint/slide, jump/fall/land, teleport, moving train/platform |
| Actions | Normal/empty reload at several cancel phases, inspect replacement, fire after reload, downed/death |
| Rendering | Owner main view, observer, shadow, optional legs, spectator/reflection, near wall, multiple cameras |
| Robustness | Re-enable cycles, domain reload settings, no shared-controller mutation, zero retained old subscriptions/handles |
| Performance | Remote count, visible versus hidden body pose costs, IK LOD, per-frame allocation, content memory, player build |

Provide short team-facing reports with tested roles/limitations. Compilation and host-only tests must not be reported as proof of full multiplayer support.

## Change boundaries and implementation ownership

- **LocomotionController:** capture actual state after movement; retain KCC movement rules. Do not embed Animator calls or weapon clip IDs.
- **PlayerWeaponController/RuntimeWeapon:** authoritative action selection, duration/commit/cancel, shot identity. Do not add camera/rig dependencies.
- **PlayerWeaponPresenter:** view loading, generation checks, bind/unbind; gradually delegate animation decisions to view/body drivers.
- **WeaponInstance:** temporary compatibility event adapter, then minimal view binding state. Remove old APIs only after consumers migrate.
- **WeaponAnimator:** migrate into a data-driven FP action player; do not create PistolAnimator/RifleAnimator copies.
- **PlayerAimTarget:** preserve its working target stream; review existing parameter/sync-type options before a conditional replacement, and establish observer parity first.
- **Character presentation capture:** local post-motor data for BodyAnimationDriver. **NetworkAnimator:** current body parameter transport. Add game-specific network data/clock handling only for action/aim requirements that need it; inspect existing components/modules first.
- **BodyAnimationDriver/RigBinding:** locomotion/holding/action parameters and rig targets for one body family.
- **Editor tools:** profiles, content validation, per-view preview and migration reports; keep UnityEditor out of runtime builds.

When adding typed Animation Rigging references, update the owning asmdef and run assembly validation if the repository tool exists. If it is absent, explicitly verify the dependency graph and compilation rather than claiming that script passed. No package-cache changes are needed.

## How a developer adds one feature after the foundation

1. Describe its semantic action/channel and gameplay permission/timing, or identify the existing action it extends.
2. Add variant data and per-rig/view motion bindings.
3. Author/import clips and cosmetic phase markers using the checklist.
4. Validate required coverage, fallback and interruption cleanup.
5. Preview the action in FP/body/mechanism views and inspect its phase/weights.
6. Run the relevant owner/observer and cancellation cases.

A genuinely new gameplay operation may need code. Another reload or inspect clip under an existing operation should primarily need content and data.
