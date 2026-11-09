# Author one weapon action

For the 1.0 release candidate, use [QUICKSTART](QUICKSTART.md). Its source lives under
`SourceArt/Pistol_A` in this checkout and its panel can export directly to Unity. The external
file paths and staging-only instructions below record the earlier setup and remain background
reference; the quickstart is the current daily workflow.

This guide separates the working setup from the workflow we are building. Start with
[the current direction](README.md); see [implementation steps](IMPLEMENTATION.md) for tooling
that does not exist yet.

## Source assets and runtime objects

Keep the weapon model and mechanism rig separate from the character as reusable source art.
Animate them together in context. Separate meshes/armatures do not mean separate choreography.
A combined arms-and-weapon first-person FBX is an acceptable output.

| Source responsibility | Runtime output |
| --- | --- |
| Master pistol mesh/rig, including slide and magazine | Weapon geometry used by both views |
| Compatible arms mesh/rig and first-person camera composition | Owner arms-and-weapon prefab |
| Full-body rig with pistol in the hand | Body action plus matching world mechanism motion |
| Character movement clips | Body locomotion controller |

The first prepared file is
[Pistol_A_Authoring.blend](C:/Projects/blender/get-the-bag/FPS/Pistol_A_Authoring.blend).
Its **First Person** and **World** scenes contain coordinated Normal and Empty reloads.
Both pistol instances share linked mesh/rest-rig data from `Models/Pistol_A_Model.blend`.
The animation actions and armature objects remain local and editable. Original source files
remain separate and unchanged; make new workflow edits in this authoring file.

## Start with the prepared pistol file

1. Open `Pistol_A_Authoring.blend`. **World is a scene**, selected from Blender's Scene selector;
   it is not an object or bone. The file is saved on World with Empty reload selected.
2. In a 3D View, press **N**, open **Pistol**, choose **Reload - Normal**, **Reload - Empty** or
   **Idle - Empty**, then click **Preview Action**. This selects the matching takes and frame
   ranges in both scenes. Press Space to play. If the tab is absent, run **Pistol Animation
   Tools.py** once in the Scripting workspace; enabling automatic script execution is optional.
3. First Person uses `Arms_Rig` / `FP_Pistol_A_Reload_Normal_Arms` and `Weap_Rig` /
   `FP_Pistol_A_Reload_Normal_Weapon`, frames **0-60 at 30 fps**; Empty uses the equivalent
   `Reload_Empty` pair.
   Select `Arms_Rig`, enter Pose Mode and use its existing hand controls when editing the arms.
   Mechanism poses are on `Weap_Rig`. Keep the action pair selected together.
4. World uses `Body` / `Body_Pistol_A_Reload_Normal` and `Weapon_Pistol` /
   `WEP_Pistol_A_Reload_Normal`, frames **1-73 at 30 fps**. Empty uses the equivalent
   `Reload_Empty` pair. Preserve its timeline markers.
   This body's existing animation is baked FK on 65 deform bones; it does not contain the
   larger control rig from `Body_Rig_Base.blend`. Added body meshes provide a preview only.
5. Click **Export Action** in the Pistol sidebar. This exports the selected preset, even if
   another action is currently active, and restores the current authoring state afterward.
6. Outputs go to `FPS/Exported/WorkflowCheck/`. Existing Unity assets are not replaced. Use a
   controlled import/review before adopting a changed clip into the working presentation.

| Output | Contents |
| --- | --- |
| FP_Pistol_A_Reload_Normal.fbx | Baked first-person arms and weapon, plus their meshes |
| Body_Pistol_A_Reload_Normal.fbx | Body skeleton and action; no preview meshes |
| WEP_Pistol_A_Reload_Normal.fbx | Weapon skeleton and mechanism action; no mesh or body carry motion |
| FP_Pistol_A_Reload_Empty.fbx | Existing first-person Empty reload performance |
| Body_Pistol_A_Reload_Empty.fbx | Matching world-body reload with a small thumb slide-release press |
| WEP_Pistol_A_Reload_Empty.fbx | Magazine handling, slide held open until frame 60, closed by frame 64 |
| FP_Pistol_A_Idle_Empty.fbx | Existing idle arms plus held-open first-person slide |
| WEP_Pistol_A_Idle_Empty.fbx | Stationary mechanism, seated magazine and held-open slide; frames 1-31 |

Normal reloads live under `Assets/_Project/Animations/Weapons/Pistol_A/Reload/Normal/`.
The new Body/WEP Empty reloads are in `Reload/Empty/`; the existing FP Empty reload remains in
`Reload/`. Empty idles are in `Idle/`. To adopt a reviewed export, replace only
the FBX at its existing destination; retain Unity's .meta file and imported clip aliases.
The staging folder remains separate so running the exporter does not change the working game.

The mechanism export temporarily disables `Reload_RightHand_Attachment` and exports in neutral
rig space. Unity supplies the hand attachment. Including the weapon mesh in this animation-only
export adds a wrapper to Unity's curve paths and does not match the current mechanism Animator.
The preset retains the existing `Scene` FBX take name while restoring the artist-facing scene
names afterward. It restores attachment, transforms, actions, selected objects, frames and mode.
It does not save the .blend or write into the Unity project.

The preset uses selected objects only, deform bones, no leaf bones, baked animation at one-frame
steps, no NLA/all-action export, zero curve simplification, -Z forward / Y up and unit scaling.
Rig shapes/cameras are excluded. Do not reset the working preview attachment manually.

This is a focused pistol tool with three presets. Other actions/weapons still need an explicit
export setup. The embedded **START HERE - Pistol Authoring** text repeats the essential steps.

Blender names identify the view: `FP_Pistol_A_<Action>_Arms/Weapon` are paired first-person
tracks; `Body_Pistol_A_<Action>` is the world body; `WEP_Pistol_A_<Action>` is the world mechanism.
`REF_` actions are retained source/reference poses. Renaming does not change bone names or
Animator state names. `Body_Pistol_A_Hold` supplies the World Empty-idle preview; no additional
body idle export is needed, and Unity keeps its existing body holding clip.

## Worked example: Pistol A normal reload

### First-person take

Work with arms, pistol, magazine and slide visible together. Use a preview camera matching the
intended framing. Author the hands and weapon as a coordinated performance. Weapon bones can
move relative to the arms; the exported top-level rig stays in place. Runtime root motion is off.

### World take

Work with the full body and pistol together. For this initial upper-body action, keep the
pelvis/legs neutral. The right hand carries the pistol through the established grip/socket pose.
The weapon mechanism animates the magazine/slide relative to that carried pistol.

Export the body and mechanism from the same evaluated take, using the same frame range and
sample rate. Bake Blender constraints into exported bone motion. Do not independently animate
the hand and magazine and try to repair their contact with runtime IK.

The world pistol's top-level mechanism/root keeps its calibrated attachment pose. First-person
weapon movement can be more expressive because the animation is composed for its camera.

### Import and configure with today's tools

1. Keep skeleton names, hierarchy, scale and export selection consistent with the working files.
2. Import body motion as compatible Humanoid animation; first-person and mechanism motion use
   their compatible Generic hierarchies. Use Create From This Model if a copied Avatar does not
   match the exported hierarchy; retargeting does not repair an incompatible hierarchy.
3. Disable looping for reloads, enable it for idles and keep runtime root motion off. Retain the
   working Resample Curves-off baseline for the current first-person exports.
4. Open `WEAPPRES_Pistol_A`. Expand **Reload Normal** or **Reload Empty**. Each entry contains
   **First Person Clip**, **Body Clip**, **Weapon Clip** (world mechanism) and **Support Hand Weight**.
   Configure that action together; the old individual reload fields have been migrated and removed.
5. Tune **Support Hand Weight** inside the selected entry. X is action progress from 0 to 1;
   Y is correction weight from 0 to 1. Zero lets the authored hand move; one applies the saved
   holding grip correction. Normal and Empty have independent curves, even if they share a body clip.
6. Run the [saved-asset validation](VALIDATION.md), then check both views in the game.

Current model/controller slot references are technical setup. Keep the working assignments;
the upcoming authoring tool should remove their repetition from everyday content work.

## Timing is part of the action

Gameplay owns reload duration. Playback derives speed from clip length / gameplay duration.
For the current normal reload example, a 2.0 s first-person clip and 2.4 s body/mechanism clips
play at 1.25x and 1.5x when gameplay takes 1.6 s. Halfway through gameplay, the views sample
their respective clips at 1.0 s and 1.2 s.

At that halfway point, UpperBodyPoseController also samples the selected entry's hand curve at
0.5. The saved pistol curves currently evaluate to 0 there, so the authored support hand is free.
Changing only Empty's curve affects Empty reloads; Normal keeps its own curve.

Matching duration aligns endpoints. Important hand/contact and gameplay-relevant beats still
need to be authored at matching relative positions. Keep paired world body/mechanism tracks
on the same timeline. More advanced phase remapping is not part of the initial implementation.

## Empty reload and future variants

The Empty world idle/reload and matching body reload are now imported and assigned on
`WEAPPRES_Pistol_A`. Runtime playback and validation now read only the grouped Normal/Empty
entries. Both curves initially retain the former shared curve's shape, but are independently
editable. A missing Empty FP/world clip uses that view's Normal clip, then its controller default.
A missing Empty body clip uses the Normal body clip **and Normal hand curve**. An assigned Empty
body clip uses Empty's own curve; an empty curve disables support-hand correction during the reload.

The initial world Empty reload reuses Normal magazine choreography, holds the slide open through
frame 60 and releases it over frames 60-64. A small right-thumb press accompanies that release;
it is not a hand-over-slide rack. Refine poses/contact in context before approving the final art.
Do not create another animation component or gameplay reload script just for another clip.
Future action-data work should grow only as actual variant requirements appear.

## Fine control and acceptance

Animators own poses, fingers, contact, mechanism motion, camera composition and deliberate
timing. Procedural tuning owns ADS alignment, bounded sway/recoil and holding correction.
Shared skeletons permit animation reuse; new body proportions still need a grip/contact review.

Check standing and moving reloads, aiming before/after reload, cancellation, weapon switching,
empty/loaded idles and downed transitions. Check the world view from front/side as well as the
owner camera. Look for floating hands, magazine jumps, drifting roots and stale mechanism poses.
Test an observing client before declaring network presentation complete.

The focused Blender Normal/Empty reload and Empty idle exports are available. A combined Unity preview and automatic
prefab/controller setup remain future work. Continue using the existing Inspector, validation
menu and game views for gameplay/observer acceptance.
