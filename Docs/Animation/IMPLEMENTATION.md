# Implement the simpler authoring workflow

**Current priority:** finish the approved [1.0 release plan](V1-RELEASE-PLAN.md), using its
[acceptance checklist](V1-CHECKLIST.md). The combined preview and generator work below is deferred
until the tested foundation is released. The daily workflow is [QUICKSTART](QUICKSTART.md).

This replaces the earlier numbered milestone sequence. Keep the working runtime as the
comparison baseline. Start from [current state](CURRENT-STATE.md) and [authoring](AUTHORING.md).
Each step must make Pistol A easier to update before we expand the supported content.

## Preparation — completed by the cleanup

- Separate current instructions from archived design/history.
- Group existing presentation fields by action without changing serialized names or asset GUIDs.
- Clarify original controller slots versus per-weapon replacement clips.
- Simplify existing playback internals and guard failed-initialization cleanup.
- Keep the existing art, prefabs, gameplay, procedural motion and networking in place.

This prepares implementation. It does not create an export pipeline or combined preview.
Verification results belong in CURRENT-STATE rather than being inferred from this checklist.

## Step 1 — Prove the art workflow with one normal reload

**6 November checkpoint:** `FPS/Pistol_A_Authoring.blend` now contains First Person and World
scenes, shared linked pistol data, the existing Normal reload actions and a focused three-file
trial export preset. Blender round-trip checks and temporary Unity imports passed; working
Unity clips were not replaced. See [the exact workflow](AUTHORING.md) and
[verification limits](CURRENT-STATE.md).

**Next human checkpoint:** open the new file, scrub both scenes and make one deliberate magazine
handling adjustment in a copy of the action/file. Export it and review hand contact in Unity
before adopting changed content. The setup/export proof does not certify new choreography.

**9 November update:** Blender actions now identify their view explicitly. The Pistol sidebar
previews/exports Normal reload, Empty reload and Empty idle. World Empty idle/reload and a matching
body Empty reload have been imported, bound and checked in isolation. Review the small thumb
release and retargeted hand contact in game. This art addition does not complete Step 2's runtime
data migration or Step 3's combined Unity preview.

Retain the original source actions, shared master, verified bake settings and existing Unity art
folders. Keep working controller bindings while reviewing the trial exports. Record any friction
encountered during the human checkpoint before expanding the focused exporter into wider tooling.

## Step 2 — Configure reloads through one action entry

**9 November runtime checkpoint:** WeaponAnimator, UpperBodyPoseController and validation now
consume the existing `ReloadNormal` / `ReloadEmpty` entries. Both saved presentations (pistol and
debug rifle) were audited/migrated, preserving grouped tuning and GUIDs; the seven old serialized
reload fields were removed. A body action selects its hand curve even when its clip is unchanged.
Missing Empty body motion falls back to Normal's clip and curve together. The standard Inspector
now shows the two grouped entries; a dedicated UI Toolkit Inspector remains optional follow-up.

Current next checkpoint: review Normal/Empty in owner/observer gameplay, then add the combined
preview in Step 3. The isolated checks recorded in CURRENT-STATE do not replace that visual review.

Evolve WeaponPresentationDefinition rather than adding a competing profile asset. Design the
smallest data representation needed for Normal and Empty reload variants, their view clips and
hand-control curves. Preserve holding/idle behavior and gameplay-owned variant selection.
Keep Content definitions independent of gameplay assemblies.

Migrate saved pistol data explicitly and update its existing runtime consumers and validator
together. Preserve the existing asset/script GUIDs and remove migrated duplicate fields after
verifying their references. Avoid a permanent old-data/new-data fallback chain.

If the standard grouped Inspector still creates friction, add a focused UI Toolkit Inspector on
that same asset. Keep a variant's clips and hand curve together and reuse existing validation.
Do not build a general graph editor.

**Checkpoint:** Normal and Empty still play through current confirmed timing. Adding another
reload variant should be a data change once gameplay has an explicit reason to select it.
Do not implement a weighted inspect selector before an inspect action is requested.

## Step 3 — Preview and remove repeated setup

Provide an isolated preview for the selected action with owner/world views and one scrubber.
Preview the same phase across body/weapon clips and display the hand-control weight. Use the
existing runtime selection/playback rules where practical; avoid a second interpretation of
gameplay timing. Never spawn gameplay/network objects merely to scrub an animation.

Only automate setup proven repetitive in Step 1: template slot assignments, controller or
prefab preparation and import checks. Separate editable artist tuning from derived output.
Regeneration must preserve tuning and stable references; do not overwrite the source art.

**Checkpoint:** an animator updates the normal reload, checks both views and validates it from
one authoring entry without manually editing controller internals or saving the gameplay scene.

## Step 4 — Extend after the pistol workflow is clear

Validate an empty reload through the same workflow, then a second weapon and a compatible
arms/body variation. Measure what can be reused and document what requires contact adjustment.
Introduce new behavior only for actual gameplay needs, such as interruptible shell loading.

Keep server ammunition/reload rules, generation-safe Addressables loading, NetworkAnimator
locomotion and confirmed action timing. Read [PurrNet](../PurrNet.md) before changing replication.

**Checkpoint:** a new team member can explain where art is authored, what is exported, what is
configured and how to verify it. Confirm owner/observer playback, interrupted reloads, switching
and late visual loading. Record untested cases explicitly.
