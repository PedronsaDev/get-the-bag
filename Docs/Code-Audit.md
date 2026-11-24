# Custom gameplay cleanup — 29 October 2026

Branch: `codex/character-animation-system`. This pass starts from the existing working tree,
including its unfinished animation work. It does not describe the difference from `main` as if all
of that work were new. Nothing was staged or committed.

## Scope and method

The starting inventory contains 191 custom C# files and 20 assembly definitions, including editor
and development tools. The pass follows callers, shared state, lifecycle callbacks, serialized
bindings and module references from player/weapon animation into the rest of custom gameplay.
It inventories every custom source file, then concentrates changes on concrete duplication,
mixed responsibilities, difficult control flow and failure paths in those dependency chains.
Existing small components that already have a clear owner are retained.

This is a source/dependency cleanup plus focused regressions. It is not exhaustive gameplay QA,
a performance certification, or completion of the character-animation roadmap.

| Area | Assessment and resulting action |
| --- | --- |
| Application | Startup and lifetime are already separate. Keep their shutdown and content-readiness ordering. |
| Content/definitions | Keep shared registries, per-weapon definitions and stat resolution. No new configuration framework. |
| Foundation | Keep interaction, persistence, player-status and input contracts. Standardize the camera warning through AppLog. |
| Player input/context | Remove an empty blocker callback; centralize console player lookup on the existing facade. |
| Player locomotion | Extract state/input types, vault queries and editor drawings; share repeated phase-handler initialization. |
| Downed/dragging/interactions | Keep existing component boundaries and authority checks. Their state/cleanup branches represent real gameplay. |
| Player weapon presentation | Name the repeated equipment/reload-state update; retain cancellation, request identity and visual ownership. |
| Body animation | Separate motion calculation from Animator writes; replace tuple-based parameter declarations with ordinary name lists. |
| Combat | Keep pure RuntimeWeapon rules, per-weapon firing strategies, damage and presentation components. Do not merge simulation with visuals. |
| AI | Keep perception, memory, blackboard, navigation, cover validation and tokens separate. Name the combat state's active-peek and return-to-cover steps; fix its private stance spelling. |
| Loot/profile | Keep spawning, world objects, carrying and persistence bridges in their existing owners. Retain server and cancellation guards. |
| Match/spawning | Separate the spawn provider and editor gizmos. Fix removal of destroyed trigger collider keys. |
| Networking | Put the startup gate in its own file and share duplicate singleton registration. Preserve the network protocol. |
| Persistence | Reuse native async file operations; propagate cancellation and async failure; report save success only after completion. |
| UI | Share tap/hold formatting, make shake cancellation restore its origin, and route the missing-root warning through AppLog. No layout migration. |
| Editor/development | Replace partial animation validation with explicit validators. Keep menus/diagnostics and existing development tools. Add a repeatable assembly check. |

## What is easier to read now

`LocomotionController` falls from roughly 1,490 to 1,002 lines by moving real responsibilities,
not hiding them in partial classes. `LocomotionData` holds the existing types; `VaultDetector`
holds physics queries; `LocomotionGizmos` is in the editor assembly. KCC simulation, phase changes
and serialized movement tuning remain on the controller. Phase implementations still share its
private movement state; extracting them further would require a larger state-access redesign.

`SpawnPoint` validates an authored point. `ValidatedSpawnPointProvider` selects points for PurrNet.
`SpawnPointGizmos` draws diagnostics in the editor. Selection/fallback behavior is preserved.

Animation validation has visible entry points:

| Entry point | Responsibility |
| --- | --- |
| AnimationCompatibilityValidator.Validate | Character/weapon metadata compatibility |
| CharacterRigValidator.Validate | Character sockets, Animator, rig constraints and renderer groups |
| WeaponAssetValidator.Validate | Addressable prefabs, binding and weapon action-controller setup |
| VisualHierarchyValidator.Validate | Skin references, missing scripts and overlapping animation writers |
| AnimationValidationCommands | Selection, menu commands and reporting |
| ValidationIssue / ValidationSeverity | Shared diagnostic results |

`PlayerFacade.Find` is used by movement and health console commands, which then read cached
components. It is a convenience for occasional commands, not a per-frame scene-query service.
`AICombatState` now names its two substantial high-cover branches, so the main decision path is
readable without expanding all navigation and timeout handling inline.

Splitting files adds a small amount of namespace/class boilerplate. The benefit is clearer ownership
and less duplicated logic. No runtime speed or memory gain is claimed solely from file/line counts.

## Behavior fixes included

- A cancelled async read rethrows cancellation instead of treating it as corruption and restoring a backup.
- Async save errors reach the caller; the manager no longer logs success from `finally` after failure.
- A destroyed Unity collider is still a dictionary key. Zone exit now removes that key instead of returning
  early on Unity's destroyed-object null comparison. Multiple colliders per player remain supported.
- Hiding or interrupting the interaction prompt restores the shake's original position. Subsequent shakes
  no longer start from a cancelled animation's offset.

No saved fields or on-disk save schema were renamed. Existing prefab configuration was not rewritten.
Necessary ownership checks, content generations, cancellation, token renewal and resource disposal remain.
The dictionary serialization callback keeps its direct Unity error diagnostic: switching it to AppLog
could introduce Unity settings/resource access from a serialization worker thread. AppLog itself also
necessarily uses Unity logging internally.

## Verification

The cleanup was checked in Unity 6000.6.2f1:

- Compilation completes without errors. `Tools/Validate-Assemblies.ps1` passes for all 20 custom assemblies.
- A syntax-tree comparison preserves all 722 original public/explicitly serialized runtime field
  declarations by owning type, name and declared type. Script metadata has no duplicate GUIDs.
- 61 character/weapon validation assertions pass, including invalid references, skin bindings, overlapping
  writers, controller parameters, reload timing, override clips and the four existing menu commands.
- 60 body-driver assertions pass: axes, speed normalization, finite values, damping, parameter ownership,
  reset/rebind and root-motion guards. These isolated checks explicitly invoke lifecycle handlers; they
  do not prove a completed runtime body-animation integration.
- 23 targeted persistence/zone/UI checks pass using disposable data and objects. Plain and encrypted
  saves round-trip, corrupt saves restore backups, cancellation propagates without rollback, I/O failure
  propagates, destroyed occupancy keys are removed, and prompt labels/offset cleanup behave correctly.
- 13 movement/lookup checks pass on the host and the same 13 on a real MPPM client. They cover local and
  explicit player IDs, cached components, vault queries, downed/drag restrictions and noclip enter/exit.
  The client's health-changing command resolver also rejects missing server authority.
- The updated single-host AI regression passes 24 checks involving real perception, NavMesh movement,
  denied combat tokens, cover reservations, remembered targets and cleanup. A measured run of 1,000
  denied-token cover updates allocates zero managed bytes in that fixture. Six additional checks cover
  peek arrival, drift, approach timeout and return arrival/timeout.
- The existing two-peer weapon suite passes five grouped scenarios: dual views/visibility, firing and
  reload, equip/fallback, cancellation/lifecycle, and failed or superseded partial loads with exact release.
  Both editors advance frames; a captured host Game View shows the FP pistol and remote world character.

Older AI harnesses referenced a removed private peek method and an obsolete enemy prefab path. The
current fixtures use the present prefab and validator/state boundaries. A stale harness failure is not
reported as a passing game check.

Transient fixtures/results and the starting source backup are in the ignored
`.codex/gameplay-cleanup-before-20261029-193242` folder. No fixture remains in Assets.
Unity is restored to stopped Play Mode, the saved Hub scene, the default scenario and the original
background-run setting. Existing asset/package working-tree edits are compared against the starting hashes.

## Follow-up work with real scope

1. **Body animation integration remains roadmap work.** The driver still needs local motor-frame capture
   and remote animation state. Saved body root motion/culling policy still needs its planned migration.
   The saved player/pistol validation retains the two existing review warnings for those settings.
2. **Scene-transition readiness needs a separate networking fix.** The current coordinator counts readiness
   without fully tying every callback to the requested destination and has no bounded readiness wait.
   This cleanup shares registration code; it does not redesign that handshake. Test disconnects, load
   failure and repeated transitions when implementing it.
3. **Save writes are not atomic.** Backup restoration is retained, but cancellation/crashes during a write
   can still interrupt the primary file. A temporary-file/replace strategy needs its own failure tests.
   Existing XOR processing is obfuscation, not secure encryption.
4. **Broader play coverage remains.** Dedicated server/builds, latency, scene transitions, all loot/extraction
   paths, alternate character rigs and every weapon/attachment combination were not exhaustively tested.
5. **Avoid further speculative decomposition.** Extend existing owners as actual requirements arrive.
   Add action variations through the animation roadmap; do not prebuild a generic animation framework.

For the day-to-day workflow and feature ownership, use [Architecture.md](Architecture.md).
