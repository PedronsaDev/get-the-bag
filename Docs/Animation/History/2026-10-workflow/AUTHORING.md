> Historical snapshot preserved on 6 November 2026. Superseded by [the current workflow](../../README.md). Statements below describe earlier plans or checkpoints, not current implementation status.

# Human authoring and content workflow

Use this with [the design](README.md) and [the implementation roadmap](IMPLEMENTATION.md). This is the target workflow to build toward; the profile tools and action catalog described here are not all implemented yet.

## 1. The four things to remember

1. **Body locomotion is authored on the full body.** Unity blends directions and later corrects feet/turning.
2. **First-person weapon actions are authored with arms and weapon together.** Keep meshes separate; they can use the two coordinated armatures already in the Blender file.
3. **World upper-body actions are authored on a full-body character with its world weapon.** The runtime mask chooses which bones contribute. You do not need separate leg and torso models for animation layering.
4. **Every view plays the same semantic action and phase.** Clip files, skeletons and camera framing may differ.

The standard content question is: which action/variant, for which view and rig family? A file named Reload alone does not answer all three.

## 2. Who does what?

| Role/task | Primary authoring location | Output |
|---|---|---|
| Character artist/rigger | DCC tool and Unity rig import | Body/arms meshes, compatible skeletons, avatar, semantic bone/socket bindings |
| Animator | DCC tool with weapon and preview camera | Per-view action clips, locomotion, intended contacts and consistent rest poses |
| Designer/technical artist | Unity profiles and preview tools | Clip mappings, marker phases, variants, weights, holding sets and procedural tuning |
| Gameplay developer | Existing weapon/action rules | Accepted action, variant eligibility, duration, cancellation and ammo commit |
| Networking developer | Presentation state/clock adapter | Ordered identities, timing, snapshots, late join and observer playback |
| QA/reviewer | FP + observer test scene | Visual contact, transitions, timing, cleanup, supported combinations and rendering checks |

One person can fill several roles. Their work still crosses explicit asset/data contracts.

## 3. Minimum content for the first slice

| Content | First person | World body | World weapon |
|---|---|---|---|
| Holding/idle | Existing combined idle | Pistol holding pose/loop | Neutral mechanism state |
| Fire | Existing clip + restrained procedural kick | Small authored/additive-compatible fire response | Slide/bolt cycle if visible |
| Tactical reload | Existing/new combined clip | Full-body-authored reload used through upper-body mask | Matching magazine/slide motion if separate |
| Locomotion | Existing optional small procedural motion | Idle, forward/back/left/right; crouch/run coverage staged afterward | Follows holding socket |

Normal reload first. Empty reload/rare inspect are extension milestones, not missing prerequisites. Rig-family/slot fallback is explicit and validated.

## 4. First-person export recipe

1. Work in one DCC scene containing the intended arms and weapon. Preserve consistent skeleton names, hierarchy, scale and view axes for that rig family.
2. Keep arms, gun, magazine and slide as useful separate meshes or rigidly weighted parts. Do not join skeletons simply to satisfy a naming convention; compatible exported bindings matter more than the number of authoring armatures.
3. Use a preview camera approximating the intended view. Frame contact and animation readability; also inspect side/front views for bad wrist/elbow bends.
4. Choose matching arm/weapon actions together: FP_Reload_Tactical with WEP_Reload_Tactical, for example. The action names alone do not synchronize their playback.
5. Decide one frame range and sample rate for the coordinated action. Check action ranges, actual keyframes and NLA strip ranges; exclude stale/duplicate stashed takes from the export selection.
6. Bake evaluated arm/weapon bone motion while both rigs play the matching action. Export meshes/required bones, excluding control shapes, staging props and the preview camera. Blender constraints/controllers are authoring helpers; the runtime requires their evaluated result.
7. Export an in-place rig/action. The stationary top-level rig root is separate from the animated weapon/root bone. The latter can move freely during actions.
8. Import as the compatible Generic rig for current FP content. Confirm full clip range, looping only for loops, no applied runtime root motion, and stable scale/contact. Retain the current Resample Curves-off baseline for these exports; it is not a universal rule for unrelated assets.
9. Preview the exported animation in Unity before replacing a working viewmodel. Check idle loops, rapid fire interruption, hand/magazine contact and restoration of the holding pose.
10. Bind into the FP ViewMotionSet; use the existing AimReference calibration for each sight. Store procedural tuning in the matching profile, not as animation-specific gameplay code.

Animation-only FBXs are allowed once the compatible base hierarchy is stable. Do not instantiate one full model for each clip. One model instance can play compatible idle/fire/reload clips from different files.

## 5. World-body locomotion recipe

1. Choose a Humanoid body/avatar family. Verify mapped hips, spine/chest, limbs and feet. A differently proportioned body still needs contact review even if import retargeting succeeds.
2. Author/import full-body in-place loops. Keep the gait's natural pelvis and arm motion; the runtime upper-body mask will replace weapon-related bones when needed.
3. Supply nominal movement speed and cycle/contact information for each gait. Synchronize left/right foot cycle conventions across directional clips where blending requires it.
4. Validate forward, backward, lateral and diagonal blends. Use actual ground movement to feed speed/direction; do not label a clip's apparent speed arbitrarily to fit a desired gameplay speed.
5. Configure root trajectory/import settings so the motor remains movement owner. Check for a drifting skeleton within a stationary GameObject; disabling Apply Root Motion alone does not guarantee an in-place result.
6. Tune bounded locomotion state play rates. If a clip is far outside the desired speed range, improve coverage rather than stretching it excessively.
7. Add crouch, sprint, air and landing states in the staged roadmap. Sliding/vault/downed may need full-body ownership and temporary suppression of weapon overlays.
8. Add terrain foot correction after the base gait works. Foot IK is not a replacement for a plausible gait or correct speed.

Turn-in-place clips are a later asset set. Record the intended visual turn angle/curve and foot contacts. The visual facing controller consumes the turn once; the motor does not also receive that root rotation.

## 6. World holding and action recipe

The current pistol mechanism Animator lives on `WeaponMesh/Weap_Rig`, because the weapon-only
reload export binds `weap_root/...` relative to that object. Check each export's actual curve paths
before choosing an Animator root. The world prefab uses `Pistol_A_World.controller` and a root
`WeaponAnimator` with View set to World; FP retains its existing controller and FirstPerson view.
Assign the normal mechanism clip to `World Reload Clip` on the weapon presentation asset. Empty
world reload/idle clips are optional; the supplied Normal clip currently provides the fallback.
The controller's original Idle/Reload clips remain stable adapter slot references. The generated
world Idle stores the prefab's neutral pose for completion and cancellation. World Fire is optional
until its mechanism motion is authored. The gameplay action supplies timing for both views.

1. Place the world pistol/rifle in the full-body authoring scene and create a comfortable holding pose. Keep reference alignment consistent with the dominant-hand socket.
2. Author reload/equip/inspect with the shoulders and chest visible. First-person poses can be a reference, but do not assume a perspective-cheated FP clip is acceptable on a full body.
3. Keep pelvis/legs neutral when authoring an upper-body-only action, or intentionally author full-body motion if the action should own locomotion too. A mask cannot make an extreme torso pose physically plausible by itself.
4. Use coordinated world weapon mechanism motion when the magazine/slide is a separate animated object. Export the body and mechanism takes with agreed action phase markers.
5. Bind body clips through the compatible Humanoid family and upper-body mask. Generic weapon mechanism clips target their own hierarchy.
6. Configure BodyAim calibration/limits for that holding family and body. Bone forward/up axes and weapon socket calibration are data; never scatter Mixamo-specific paths through gameplay code.
7. Place support grip and elbow hints on the weapon/profile. Declare the hand-release windows for authored reload/reach. The hand IK must fade out when the clip needs to move that hand.
8. Preview body locomotion + holding + aim + action together. Check extreme look directions and support-hand alignment on each supported body.

## 7. Example: add an empty reload

1. Add the Empty variant under the existing Reload action; retain Tactical's stable ID/default behavior.
2. Gameplay selects it from the confirmed magazine/mechanism state at action start. If it has a different gameplay duration, update the duration policy rather than deriving time from the clip.
3. Author FP/body/world-mechanism Empty clips with a held-open initial slide where applicable. Verify their contacts against shared normalized markers.
4. Register the clips for each supported rig family and the prop/IK phase data. Keep the same controller/action slot.
5. Declare persistent SlideLockedOpen behavior after the last shot for supporting weapons. Test cancellation before/after magazine removal/insertion and slide release; the resulting state must match gameplay's declared policy.
6. Validate late loading into the action and return to holding. Missing required Empty content must be visible in the report rather than silently using Tactical.

No separate EmptyReload script or RPC is required. A different gameplay operation, such as interruptible per-shell loading, is a new action/timing policy and deserves its own design; another clip variant is not enough to model it.

## 8. Example: add a rare inspect

1. Keep Inspect as the action and add a new stable variant ID with a weight/conditions.
2. Bind its FP/body/mechanism clips and cosmetic markers, using the existing Action slot.
3. Provide a forced-variant preview option for review. Rare content must not require repeated random attempts to test.
4. Test eligible selection and interruption. The accepted variant is selected once and carried to observers.
5. Add explicit optional body fallback if desired. If an inspect manipulates a world magazine, the body/mechanism bindings must agree or the action must be declared owner-only presentation.

Do not use array indices or process-dependent string hashes as persistent/network IDs. Build a stable, collision-checked manifest and define content/protocol compatibility. Adding a new optional variant must not rename existing IDs or invalidate default content.

## 9. Example: add another weapon or body

### Weapon

- Supply gameplay definition and stable weapon ID using the existing weapon workflow.
- Register a world prefab, compatible FP view bindings, holding family, required action mappings and procedural/sight/grip tuning.
- Reuse shared family motion sets where suitable; author overrides only for real differences.
- Validate supported appearance/arms combinations, effect ownership and Addressables cleanup.

### Body/arms appearance

- Reuse a rig family only when the skeleton/import contract is compatible.
- Bind body bones, renderer groups, sockets, aim limits, mask/controller family and optional local-leg meshes.
- Select a compatible FP arms family/sleeves, independently of the body mesh's asset file.
- Retarget/author content for a new family explicitly; changing materials alone does not require duplicate clips.
- Test grip contact, crouch height, feet, neck/waist seams, shadows and at least one reload before approving the appearance.

## 10. Required validation and preview controls

The target editor workflow should show:

- Actor/view, appearance/rig family, weapon and resolved override/fallback.
- Required clip coverage, duplicate IDs, import family/avatar mismatch, invalid masks, missing sockets and phase errors.
- Gameplay duration alongside actual clip duration and resulting playback rate.
- Forced action/variant/phase preview for FP, body and mechanism views.
- Procedural contribution toggles; current aim/action/IK weights and pose ownership.
- Prop state at phase, marker timeline, cancellation preview, and a reset-to-rest action.
- Distinct statuses: Valid, Optional Fallback, Unsupported Combination, and Error.

Editor mutations should support Undo and explicit asset-save behavior. A preview uses temporary copies or a clearly scoped reversible preview mode; it must not accidentally save sampled bones or runtime offsets into prefabs.

## 11. Content review checklist

- [ ] Correct action/variant/view/rig IDs and compatible mappings.
- [ ] Matching export ranges and baked evaluated motion.
- [ ] Correct scale/orientation; no accidental root drift.
- [ ] Stable hand, magazine and foot contacts in Unity.
- [ ] Loop seam and start/end pose acceptable.
- [ ] Gameplay duration and visual phase agree; no animation event grants ammo/damage.
- [ ] Required cancellation/seek restores props, mechanisms and IK.
- [ ] Both owner and observer reviewed for a gameplay-visible action.
- [ ] No duplicate arms, world weapons, muzzle flashes, audio or shadows.
- [ ] New variant/content added through data without duplicating unrelated controller logic.

## 12. The next practical work session

Implement only Milestone 1 from [the roadmap](IMPLEMENTATION.md): establish the one-pistol visual/rig definitions, bind the owner FP and world weapon simultaneously, consolidate visibility, and validate root-motion/aim conventions. Keep the existing working animation/procedural values as the baseline. Then prove the same setup on a separate connected client before expanding its content.
