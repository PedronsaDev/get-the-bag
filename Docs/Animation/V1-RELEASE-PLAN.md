# Animation system 1.0 release plan

Status: approved by the user on 9 November 2026; implementation is in the isolated release
candidate. See [V1-CHECKLIST](V1-CHECKLIST.md) for verified results and outstanding acceptance.

## Release objective

Make the existing animation system dependable enough for the team to continue developing the
game. A developer can configure a weapon using the supported rigs, export its clips, and verify
owner/observer playback using a short documented workflow. Release the current foundation and
return to gameplay work once the acceptance checks pass.

This replaces the larger authoring-tools roadmap as the immediate release target. The existing
combined-preview and prefab-generation ideas can be revisited after 1.0.

## What is already available

- Dedicated owner arms/weapon and world body/weapon presentations.
- Body locomotion driven from post-movement data, with existing NetworkAnimator replication.
- Procedural aiming, ADS alignment, sway, recoil and holding-hand correction.
- Gameplay-owned fire, ammunition, reload variant selection and confirmed action timing.
- Per-instance authored playback, late-phase reconstruction and cancellable visual loading.
- Grouped Normal/Empty reload entries, each with FP/body/world clips and a support-hand curve.
- Pistol Normal/Empty reload art and empty-slide idles; existing fire/effect presentation.
- Saved-asset validation and 83 passing isolated checks from the latest migration.

Recent isolated checks do not establish current connected-client visual correctness. The owner
shadow-body culling policy also still needs a visual acceptance check.

## Supported 1.0 scope

| Area | 1.0 agreement |
| --- | --- |
| Reference content | Pistol A is the fully configured reference. Existing debug rifle is a switching/fallback case, not finished rifle art. |
| Character compatibility | Current arms/body family and compatible skins. Different skeletons/proportions require a separate content review. |
| Authoring | FP arms and pistol authored together; world body and mechanism authored together; one shared pistol model/rig source. |
| Authored playback | Existing idle/fire, Normal/Empty reload, empty idle and current locomotion/stance states. Safe instant equipment changes are acceptable. |
| Procedural playback | Preserve current aim, ADS, sway/recoil and holding IK; ensure their weights recover after actions. |
| Networking | Existing NetworkAnimator locomotion and confirmed weapon state/timing. Keep their current ownership boundaries. |
| Extensibility | New compatible weapons configure the existing definitions, prefabs and clips. Normal/Empty remain explicit named entries for this release. |
| Quality | Correct and stable contact, poses, visibility and action transitions at gameplay distances. Further animation polish can continue as content work. |

World muzzle effects and the existing firing presentation are sufficient for this reference
release. A dedicated world slide-fire clip or new third-person firing performance is optional
unless testing reveals that the present result fails normal gameplay readability.

## Implementation order

### 1. Establish the release boundary

Audit the full difference against main before adding release changes. The current
`codex/character-animation-system` branch is one commit ahead of main (`6fa2863`, a broad
systems refactor), and its working tree also contains animation moves and unrelated plugin,
loot, scene and package edits. A normal branch merge would include the existing committed
refactor; staging all working changes would include unrelated work too.

Create a separate release worktree from main. Bring over the animation changes and their
required dependencies using an explicit file/hunk list. Preserve asset moves together with
their original .meta files, shared material/texture dependencies and reference updates.
Inspect the broad refactor for required shared dependencies instead of assuming it is either
all needed or all disposable. Keep unrelated work in the original checkout.

Compile and run the existing animation checks in that release checkout. This establishes the
actual code that will be tested and merged. Record the included dependencies and any unresolved
scope issue before expanding the change set. Never reset the user's current checkout.

Completion: a compiling release candidate with a reviewable main-to-candidate diff and preserved
asset GUIDs. Existing feature behavior must not be lost during extraction.

### 2. Close runtime and content acceptance gaps

Keep the existing architecture. Review the current weapon/body adapters, presenter and saved
prefabs against the cases below; make small fixes where a failure is demonstrated.

- Verify Normal/Empty clip selection, gameplay-duration scaling and hand-curve selection in
  first-person and world presentations.
- Verify reload completion/cancellation, equipment changes, downed/recovery and disable/re-enable
  restore the appropriate idle, aim correction and holding-hand correction.
- Verify late observation and delayed visual loading reconstruct the current action phase and
  cannot publish stale weapon views or leave duplicate effects.
- Check the existing playable locomotion states in the body controller, including movement
  during reload. Fix missing bindings/transitions for enabled gameplay states where necessary.
- Resolve the owner shadow-body culling question with a live pose/shadow check. Apply an explicit
  Animator update policy where required; record any justified remaining warning.
- Review the actual Humanoid-retargeted magazine/hand contact and slide-release beat. Correct
  visible jumps, gross floating hands or stale mechanism poses; keep the existing choreography.

Primary existing code: WeaponAnimator, UpperBodyPoseController, PlayerWeaponPresenter,
CharacterRigBinding/visibility, and the current presentation validators. Change movement or
networking code only for a reproduced integration defect, after checking project PurrNet guidance
and the installed API.

Completion: the reference pistol behaves consistently through the action/lifecycle cases. No
extra animation controller, generic action framework or replication stream is introduced.

### 3. Make the artist workflow self-contained

Finish the small Blender workflow already in use:

- Add an explicit **Export to Unity** action to the current Pistol panel, with a visible Unity
  project-folder setting and mappings for the existing Normal reload, Empty reload and Empty-idle
  presets. Keep staging export available for review.
- Bake paired body/mechanism tracks with their matching ranges. Preserve current bone paths,
  scale/attachment conversion and authoring scene/action/frame/mode after export.
- Validate destinations before writing, preserve existing Unity .meta files and clip aliases,
  and back up overwritten FBXs. Export never regenerates prefabs/controllers or rewrites tuning.
- Version the exporter source with the project. Include a release copy of the canonical authoring
  file and its linked master/dependencies under SourceArt outside Assets, using the existing LFS
  rules. Keep the user's current external Blender files intact. Verify relative library/texture
  references resolve from the release copy, so the workflow is available on another machine.
- Provide one short start-to-finish guide: choose the action, animate, export, inspect the two
  grouped reload entries, run validation, and test the views. Explain authored versus procedural
  responsibility and the supported rig family in the same guide.
- Prove a second configuration can use the existing compatible rig with different action data;
  existing reference art may be reused for this configuration check. New rifle art is not required.

Completion: a developer can update a reload and see it in Unity without assistant-run file-copy
commands or editing the Animator controller. Source art and export instructions are versioned.

### 4. Package repeatable validation

Retain the useful isolated regressions as a documented, repeatable repository check outside
Assets. Add checks only for new exporter/runtime changes and defects fixed during this release.
The existing animation validation menu remains the saved-content check.

Run compile/content validation on the release candidate, verify LFS assets and reference GUIDs,
and produce a development build for the final gameplay smoke test. Update CURRENT-STATE and a
short 1.0 checklist to describe the supported workflow and known limits accurately.

Completion: the result can be checked again without reconstructing scripts from an ignored
assistant workspace. No one-off validation machinery becomes part of runtime gameplay.

## Testing after implementation

Use a local host and a separate client with the current transport. Exercise both host-owned
and client-owned players; include a development-build run so the check covers actual imports,
Addressables content and startup outside the Editor.

| Scenario | Required result |
| --- | --- |
| Idle/movement/aim | Owner and observer see correct stance/aim; motor owns the root; enabled locomotion states work, including movement while reloading. |
| Fire and empty state | Correct muzzle/effects per view; no duplicate owner effects; last shot reaches empty idle; ammunition is spent by gameplay once. |
| Normal/Empty reload | Matching world body/mechanism contact; selected clips and hand curve; gameplay duration respected; slide and holding pose correct afterward. |
| Cancellation and transitions | Switching, downing/recovery and disabling/re-enabling leave no stuck reload, hand IK, aiming weight or obsolete weapon view. |
| Join/load/lifecycle | A newly observing or delayed-loading view resumes current action phase; repeated switches and despawn/respawn/disconnect leave no visible duplicates or runtime errors. |
| Rendering | Owner sees the intended FP view and body/world shadows; observer sees body/world weapon; shadow poses keep updating. |
| Content workflow | Export a reviewed change, reimport, validate, and reproduce it from the versioned source copy without losing GUIDs or tuning. |

For mid-reload observation tests, temporarily lengthen the reload in the test session if needed
so joining/loading completes before the action ends. Restore production tuning afterward.

## Definition of done

- Release candidate compiles and all retained/new relevant isolated checks pass.
- Saved reference character/pistol validation has zero errors; remaining warnings have explicit,
  verified reasons and do not conceal a broken owner/observer pose.
- The host/client matrix above passes on the candidate, including the development-build smoke.
- No repeatable animation exceptions, stale views, stuck action/IK states or major contact jumps.
- Source art/exporter and a short workflow are available from version control.
- Main-to-candidate review contains the intended animation changes and documented dependencies;
  unrelated current work remains preserved separately.

## Deferred beyond 1.0

Combined Unity preview/scrubber, custom Inspector/prefab generator, arbitrary rig support,
procedural terrain legs/foot placement, turn-in-place overhaul, new inspect/equip systems,
weighted action catalogs, advanced reload types, extra production weapon art, broad networking
redesign and a large latency/performance stress campaign.

## Test and merge sequence

1. User reviews/adjusts this release scope.
2. Implement in the isolated release candidate and run the repeatable checks.
3. Review the in-game owner/observer result together; fix acceptance failures only.
4. Commit the reviewed release files with their metadata and LFS objects. Reconcile any changes
   main received, repeat affected checks, and review the exact final merge diff.
5. Merge the passing candidate into main, perform a short post-merge smoke and mark the tested
   result as animation system 1.0. The original checkout retains unrelated unfinished work.

Stop expanding the system when these checks pass. The following work should be gameplay/features;
future animation additions use the documented content pipeline or a separately scoped release.
