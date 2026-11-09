# Current animation implementation

## Animation 1.0 — 9 November 2026

The isolated `codex/animation-v1-release` checkout is based on main and contains only the selected
animation changes/dependencies. Read [QUICKSTART](QUICKSTART.md) for the current artist workflow;
the earlier external Blender paths below are historical setup records. Canonical release source
is `SourceArt/Pistol_A/Pistol_A_Authoring.blend`, linked to its neighboring Models master, with
packed preview images. The embedded text loads the single maintained `Tools/Blender/PistolAnimationTools.py`.

The Pistol panel now includes a Unity project selector and direct export, backups and rollback.
The owner's hidden body and world mechanisms use AlwaysAnimate for sockets/shadows; the body
restores its original culling policy on observer configuration or visibility disable.

The debug rifle explicitly uses the pistol body hold/Normal reload and curve as placeholder data;
Empty falls back to Normal. This fixes the missing-body-reload error reproduced during switching.
It is not production rifle animation content. Saved content and 92 isolated checks pass.

A Windows development build succeeded. A separate built client connected to the Editor host;
Normal/Empty timing, phase reconstruction after visual recreation/reconnection, and host action
cancellation/recovery were exercised. See [the checklist](V1-CHECKLIST.md) for completed checks,
remaining movement/contact/shadow review and the final-build results. The user requested
integration into main on 9 November 2026; those manual checks remain follow-up work.

Updated 9 November 2026. This describes the code/assets retained for the simpler authoring
workflow. It is separate from [future implementation steps](IMPLEMENTATION.md). Older claims
and milestone checkpoints are preserved in [history](History/2026-10-workflow/CURRENT-STATE.md).

## Existing responsibilities

| Area | Current implementation |
| --- | --- |
| Weapon rules | RuntimeWeapon and PlayerWeaponController own ammunition, firing and reload decisions |
| Action state | WeaponEquipmentState carries reload variant, sequence, equipment revision, start and duration |
| Action clock | Existing PurrNet tick clock; presenter translates to local unscaled playback time |
| Views and loading | PlayerWeaponPresenter owns separate world/FP WeaponVisualBindings with cancellation/version checks |
| Visual state | WeaponInstance forwards accepted fire, ammo and reload state to each loaded view |
| Weapon playback | WeaponAnimator selects FirstPerson or World clips and owns per-instance overrides |
| Body actions | UpperBodyPoseController selects holding/reload clips, seeks confirmed phase and weights rigs |
| Locomotion | CharacterPresentationCapture samples post-motor state; BodyAnimationDriver writes Animator parameters |
| Replication | Existing NetworkAnimator replicates body locomotion; reload presentation consumes confirmed equipment state |
| Procedural motion | FirstPersonWeaponMotion coordinates ADS, look/movement sway and recoil |
| Character binding | CharacterRigBinding declares body, sockets and rigs; CharacterVisualVisibility applies view visibility |
| Validation | Existing compatibility, rig, weapon and animation-writer validators under Editor/Weapons/Presentation |

Custom runtime paths are under `Assets/_Project/Scripts/Runtime/`. Player presentation lives in
`Gameplay/Player/Animation` and `Gameplay/Player/Combat`; weapon view components live in
`Gameplay/Combat/Weapons/Presentation`; definitions live in `Content/Definitions/Weapons`.
The [project code guide](../Architecture.md) describes assembly ownership.

## Current pistol assets

| Asset | Path under Assets/_Project |
| --- | --- |
| Character | Prefabs/Player/CHAR_PF_Player.prefab |
| Gameplay definition | Data/Weapons/Weapon Definitions/Pistols/WEAP_Pistol_A.asset |
| Presentation | Data/Weapons/Visual Definitions/Weapon/WEAPPRES_Pistol_A.asset |
| Owner view | Prefabs/Weapons/Pistols/WEAP_FP_Pistol_A.prefab |
| World weapon | Prefabs/Weapons/Pistols/WEAP_PF_Pistol_A.prefab |
| Weapon action clips | Animations/Weapons/Pistol_A/Idle, Fire and Reload/ |
| Weapon controllers | Animations/Weapons/Pistol_A/Controllers/ |
| Body locomotion/controllers/mask | Animations/Characters/Human/ |

The world mechanism Animator is on `WeaponMesh/Weap_Rig`, with curve paths relative to that
object. Its normal reload currently has the imported alias `A_WEP_PIstol_A_Reload_Normal`.
Preserve the calibrated FBX
conversion transforms; setting every rig scale/rotation to identity would break those bindings.

## What the current content supports

The pistol has dedicated first-person Normal/Empty reload and Empty idle clips. The body and
world weapon now have separate Normal/Empty reload clips, and the world weapon has a looping
Empty idle that holds the slide open. These are assigned on the pistol presentation. The world controller has
Idle/Reload; Fire is optional until its mechanism clip is authored.

The support-hand correction fades through the selected reload entry's own curve. Normal and Empty
are independent, although the saved pistol currently gives both the same shape. Aim correction is suppressed during body reload, while the authored
body/weapon motion supplies choreography. The holding IK target follows the right-hand hierarchy;
it is calibrated on equip rather than copied in a separate per-frame follower.

## Reload data migration — 9 November 2026

WeaponPresentationDefinition now has one `ReloadNormal` and one `ReloadEmpty` entry of type
WeaponReloadAnimation, each holding the FP, body and world mechanism clips plus hand-control curve.
WeaponAnimator reads the selected view's grouped clips; UpperBodyPoseController selects the body
clip and its matching curve on each new action. Selection updates the curve before the unchanged-clip
early return, allowing shared clips with different hand control. Missing Empty body motion uses
Normal's clip and curve together; missing FP/world motion retains the existing Normal/controller fallback.

The pistol and debug rifle presentations were backed up and migrated before deleting seven old
serialized reload fields. Existing grouped values were retained; only missing settings were copied
from legacy fields. Post-compilation comparison confirmed all other data and both .meta files were
unchanged. The old accessors are gone; there is no permanent legacy-data fallback.

Weapon validation now checks grouped FP/world clips and curve keys. Explicit character/weapon-pair
validation also checks required Normal body motion and supplied body reload rig type/duration/bindings.
Empty hand curves intentionally leave the authored hand free. Authoring instructions use the two
standard Inspector entries; no new runtime controller, custom Inspector or network stream was added.

Verification and backups: `.codex/reload-entry-migration-20261109`. Compilation passed, along with
28 FP/world regressions, 34 body regressions (including seven hand-curve cases) and nine actual
Empty-clip playback checks. Twelve validation checks passed, including body rig/loop errors,
curve ranges and optional Empty fallback. Final asset/state results are recorded in the reports
there. Saved character/pistol retains its pre-existing body Animator culling warning.
No fresh Play Mode/connected-client or final visual hand-contact acceptance is claimed.

## Earlier cleanup baseline

- Existing presentation fields are grouped by Holding, Normal Reload, Empty Reload and hand control.
  Serialized field names, types, GUIDs and saved clip assignments are preserved.
- Controller slot tooltips distinguish template keys from per-weapon replacement clips.
- WeaponAnimator uses shared static state hashes and one readable Animator readiness check.
- Body override assignment no longer builds a temporary list for two slots. Destruction safely
  handles failed initialization and preserves a controller installed by another owner.
- Active guides are shorter and separated from archived plans. Existing loading and gameplay
  remain the implementation baseline.

## Still to implement

Coordinated Unity preview and automatic prefab/controller setup remain future work. A focused
Unity authoring Inspector can follow if the standard grouped fields need improvement. Neither
arbitrary rig compatibility nor a general action framework is implemented.

## Empty world clips and action naming — 9 November 2026

This records the earlier art checkpoint. The reload data migration above supersedes its field
assignments and the statement that runtime data migration was unchanged at that time.

The live authoring state was backed up before editing. All 27 existing actions now identify
their view/role: `FP_Pistol_A_<Action>_Arms/Weapon`, `Body_Pistol_A_<Action>`,
`WEP_Pistol_A_<Action>` for world mechanisms, and `REF_` for retained references. Original
FCurve fingerprints were unchanged. Bone names, rig objects and standalone source files were
not renamed.

Added `WEP_Pistol_A_Idle_Empty` (frames 1-31), `WEP_Pistol_A_Reload_Empty` and
`Body_Pistol_A_Reload_Empty` (frames 1-73), plus `Body_Pistol_A_Hold` for preview. Empty reload
preserves Normal magazine motion, holds the slide open through frame 60, closes it by frame 64
and adds a small right-thumb press. World mechanism root is stationary. This is an initial
slide-release performance for artist review, not a hand-over-slide rack.

The embedded **Pistol Animation Tools.py** registers **N sidebar > Pistol** with Normal reload,
Empty reload and Empty idle presets. Preview selects coordinated actions; Export writes staged
FBXs and restores authoring state. If automatic scripts are disabled, run the text once to show
the panel. No global security preference was changed. The saved file opens on World/Empty reload.

Imported new assets under `Animations/Weapons/Pistol_A/Reload/Empty/` (Body/WEP Empty reload)
and `Idle/` (WEP Empty idle). Existing FP FBXs and Normal FBXs were retained. Assigned the three
clips to the current presentation fields and corresponding nested Empty body/weapon fields;
runtime code/data migration was not changed. Existing FP assignments and hand curves remain.

Verification completed:

- Original 27 action curve fingerprints preserved; all 73 sampled Empty magazine poses match
  Normal, and all 73 sampled world mechanism root poses remain constant.
- Inspected Blender closeups at frames 1/60/64. Exported both new presets and confirmed action,
  pose and scale restoration. Reopened the saved file in a separate Blender process: linked
  master resolves, actions/ranges persist, embedded panel registers.
- New Unity mechanism curves bind to the existing World Animator. Empty idle imports as looping
  Generic 1 s; both reloads are 2.4 s, with a valid Humanoid Avatar for the body.
- Existing isolated FP/world regression passed 28/28; body regression passed 27/27.
- Nine additional actual-clip playback checks passed: empty/loaded slide poses, loop continuity,
  Empty selection, 1.5x playback for a 1.6 s gameplay reload, start/end matching and cancellation.
  An initial test threshold incorrectly ignored the rig's 100x conversion; checked imported
  curve values and corrected the test's local-unit threshold. No art scaling was changed.
- Editor remained stopped and Scene_Hub clean. No scenes/prefabs or runtime scripts changed.

Reports/backups are in `.codex/pistol-empty-20261109`; the pre-edit live Blender backup is also
recorded there. Final Humanoid-retargeted thumb/hand contact and connected observer playback
still need in-game acceptance. These isolated checks do not certify those visual/network cases.

## Pistol authoring setup — 6 November 2026

The following records the original 6 November export proof; see the 9 November update below for
current action names, added Empty clips and their assignments.

`C:/Projects/blender/get-the-bag/FPS/Pistol_A_Authoring.blend` contains the existing first-person
and world Normal reload in separate scenes. The original `Pistol_A.blend`,
`Body_Pistol_Reload_Normal.blend`, `Body_Rig_Base.blend` and master pistol file were retained;
SHA-256 comparisons confirmed their contents were unchanged.

Both pistol objects reference the same linked master mesh and armature data. Local actions,
attachment constraints and pose controls remain editable. Sampled deform-bone poses matched
before/after linking exactly. The world scene includes a scaled rest-space body mesh reference;
the existing body action remains on its baked 65-bone FK rig. A new world IK/control rig was not
created. FP retains its existing controls.

The embedded `Export Normal Reload.py` writes three trial FBXs to `FPS/Exported/WorkflowCheck`.
It bakes the selected coordinated actions, excludes helper geometry and exports the body and
world mechanism as animation-only files. Mechanism export removes the authoring attachment
motion temporarily; its object/root remains stationary. Export restores the authoring state.

Verification:

- Reopened the saved authoring file; its relative master-library path resolves.
- Ran the embedded exporter from Pose Mode and verified scene, actions, selection, frames,
  mode and rig transforms were restored. Original source file hashes still matched.
- Inspected camera renders of FP frames 0/30 and World frames 1/35.
- Reimported the three FBXs into background Blender. Sampled FP frames 0/15/30/45/60 and world
  frames 1/16/35/54/73. Reconstructed world weapon placement from the exported body hand and
  one constant grip offset. Maximum sampled surface discrepancy was below 0.003 mm; the world
  mechanism root had zero sampled motion.
- Imported all three exports into temporary Unity copies of the existing import settings.
  FP imported as a 2 s Generic clip; body as a 2.4 s Humanoid clip with valid Avatar; mechanism
  as a 2.4 s Generic clip. All retain the working `Scene` take name. FP/world transform paths
  all bind to the current prefabs.
- Working Unity FBXs and their metadata hashes were unchanged. Temporary import assets were
  deleted and Scene_Hub remained clean. No working clip assignments or prefabs were changed.

These checks establish the initial authoring/export contract. No changed artistic performance,
fresh Play Mode/multiplayer test or final Humanoid retargeted hand-contact acceptance is claimed.
The next checkpoint is one deliberate art edit through this workflow, reviewed in Unity.

## Verification

Cleanup verification on 6 November 2026:

- Unity compilation completed without errors.
- The existing world/first-person regression fixture passed all 28 checks: variant selection,
  speed/phase, cancellation/restoration, independent overrides and asset validation.
- The body reload fixture, adapted to the current asset paths, passed all 27 checks, including
  equipment/variant changes, late phase reconstruction and failed-initialization cleanup.
- All 24 serialized fields across the three edited scripts retained their names and types.
- All 25 local links in the four active guides resolve. The cleanup diff has no whitespace errors.
- Saved character/pistol validation reported zero errors and one body Animator culling warning.
  Verify that the owner's shadow-only body updates its pose before accepting that culling policy.
- The Editor remained stopped on a clean Scene_Hub, with no open Prefab Stage. Character,
  weapon definition and presentation assets remained clean. No scenes/prefabs were saved.

These are isolated Edit Mode and saved-asset checks. No new Play Mode, connected multiplayer,
visual hand/magazine contact or render-culling acceptance test was performed. Previous user
reports and historical playtests remain separate evidence.

## Folder and filename migration — 7 November 2026

The canonical animation root is now `Assets/_Project/Animations`, retaining the root the user
created. Weapon actions are grouped under `Weapons/Pistol_A/{Idle,Fire,Reload}`, with controllers
alongside in `Controllers`. Character-wide locomotion, controllers and masks are under
`Characters/Human`. Button and door assets are under `Interactables`.

The 44 planned assets were moved/renamed through AssetDatabase. Every asset GUID and imported
subasset ID was preserved. All FBX and texture bytes were unchanged, and imported clip names,
durations, frame rates, looping and Humanoid/Generic type were retained. Thirteen dependent
assets, including affected prefabs, definitions and Scene_Hub, retained their dependency GUIDs.
The first attempt rolled back one move because JSON float precision made a verification check
report a false mismatch; after normalizing snapshot serialization, all 44 moves passed.

Native .anim/controller/mask display names follow their new filenames. Imported FBX clip aliases
and Animator state/parameter names remain stable to preserve existing serialized bindings.
The shared palette now lives at `Art/Materials/Shared/M_FPS_Palette.mat` and
`Art/Textures/Shared/T_FPS_Palette.png`. Empty legacy animation folders were removed only after
checking that they were empty and not registered Addressable folders.

The Blender Normal reload preset and staging filenames use the same FP_/Body_/WEP_ conventions.
Original Blender action names and authored curves remain unchanged. This migration does not
promote staged animation content over the working Unity FBXs.

Post-migration checks: all 28 first-person/world playback checks and 27 body reload checks
passed using copies of the existing fixtures adapted to the new paths. All 21 Humanoid models
under the new animation root have a valid imported or referenced Avatar. The updated baseline
inventory tool compiles without diagnostics. Saved character/pistol validation still reports
zero errors and the existing body-culling warning. The Editor remains stopped on clean
Scene_Hub. No new Play Mode or connected multiplayer test was run for this asset-only migration.
