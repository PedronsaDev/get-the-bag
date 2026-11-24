# Animation 1.0 acceptance

Candidate branch: `codex/animation-v1-release`, based on main `e6d5819`.
Candidate checkout: `get-the-bag-animation-v1`. The original feature checkout remains intact.

## Implementation

- [x] Extract animation assets, their metadata and the required runtime/editor dependencies.
- [x] Compile the isolated checkout and run the 83 existing regressions.
- [x] Keep grouped Normal/Empty entries and per-variant hand curves as the single reload data source.
- [x] Explicitly animate the owner's hidden body and world mechanisms; retain observer body culling.
- [x] Add eight owner visibility/culling lifecycle checks and a debug-rifle fallback regression.
- [x] Add Export to Unity to the existing Blender panel, preserving metadata and backing up FBXs.
- [x] Verify all three export presets, state restoration, invalid destinations, missing metadata and rollback after a partial copy.
- [x] Prepare the canonical authoring file, linked master and exporter for version control outside Assets; pack the shared palette and use a relative library path.
- [x] Add a single [daily workflow](QUICKSTART.md) and a repository check runner.

## Runtime and build acceptance

- [x] Combat gym starts the candidate host, spawns the reference player and loads both weapon views.
- [x] Confirm frames advance and inspect a captured owner view.
- [x] Development player build succeeds and starts (final corrected build: 0 errors, 8 warnings).
- [x] Separate client connects; host-owned and client-owned fire/Normal/Empty reload checks pass.
- [x] Normal/Empty selection and shared timing/phase verified on both peers; Empty slide stays open until release.
- [ ] Final human hand/magazine contact and slide-pose review.
- [ ] Movement/aim and enabled stance transitions, including moving reloads.
- [x] Host switching, cancellation, downed/recovery and presenter disable/re-enable automated checks.
- [x] Real reconnect/new observation and recreated visual loading during reload resume matching body/world phases.
- [x] Corrected build repeats host switching/cancellation/downed/recovery and six client-owned switches with no new runtime errors; exactly two owner views remain.
- [ ] Client interactive movement/contact review with the user.
- [ ] Owner/observer rendering and moving body/weapon shadows.
- [ ] Human visual review with the user.

## Integration

- [x] Review the exact main-to-candidate diff and required shared dependencies.
- [x] All 112 copied metadata GUIDs match the source; original checkout Git status is unchanged; Git LFS fsck passes and source .blend files use LFS.
- [x] Source libraries use relative paths and preview images load from packed data.
- [x] Relocated source package loads its relative library, packed images and tool and passes all export checks.
- [x] Commit reviewed release files with the requested message, fast-forward main and run the animation checks.

Candidate reports are under `.codex/animation-checks` and `.codex/v1`. Test-player output is under
`.codex/v1/build`. These generated files are excluded from source control. Unchecked cases are
remaining manual follow-up work. On 9 November 2026 the user explicitly requested integration
into main before those final human visual checks.

Implementation handoff: 92 isolated checks pass after cleanup; the corrected Windows build has
0 errors. Both test processes/sessions have been stopped, the gym scene is clean, product version
and background/preloaded settings are restored, and temporary development instrumentation is off
in the project. The already-produced development executable retains its test instrumentation.
The merge includes only the reviewed animation release and required dependencies. Code comments
were removed with C# token and Python AST equivalence checks. Artist-facing instructions remain.

Connected snapshots verified body/world phases against confirmed progress (for example, late-loaded
client progress 0.4353, body 0.4352, world 0.4350; newly joining observer progress 0.4184, body
0.4184, world 0.4175). The test used temporary 6/8-second runtime reload durations; saved weapon
tuning was not changed. The first switch test exposed an unconfigured debug-rifle body reload;
the candidate now assigns explicit reference placeholder data and retains a regression for it.

The hidden Player's RenderTexture capture was black and is not visual evidence. The live Editor
observer image rendered the body/world weapon; final contact approval remains a human test.

The final development build has eight warnings. Recorded console details include existing
missing-script references in the projectile/impact-effect assets and DeepShafts pools, debug/shader
stripping messages and gizmo-only locomotion field warnings. These are tracked separately from
animation validation (zero errors/warnings for the configured reference pairs). The post-fix
connected action/switch check reported no new runtime errors. Broader effects/level cleanup is
outside this release and is not certified by the animation tests.

## Release scope and dependencies

The candidate includes the animation folder migration, shared palette relocation, current
player/weapon prefabs, visual definitions, weapon definitions/stats, weapon action/equipment data,
body presentation/locomotion capture, and their validation. Existing Addressables/network prefab
registrations follow the same asset GUIDs. The locomotion extraction and its Editor gizmo assembly
references are required by the current post-motor presentation interfaces. The Animation log channel
is included for those components. Pipeline matches the tested 0.10 package for the check tools.

AI behavior cleanup, loot changes, outline-plugin changes, logging UI refactors, console lookup/input
cleanup, persistence refactors and unrelated scene changes were excluded. The broad original
refactor commit was not merged wholesale. Generated font/settings changes and empty legacy/test folders were excluded from the merge.

The optional Tools/Validate-Assemblies.ps1 does not exist in this repository. Unity compilation
verifies the included assembly references; no additional assembly dependency was introduced by
the culling/export changes themselves.
