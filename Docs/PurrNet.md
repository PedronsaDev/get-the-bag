# PurrNet project knowledge base

Reviewed **29 October 2026**. Start with the [official documentation](https://purrnet.dev/docs).
This is a maintained project reference and review guide, not a copy of the entire documentation
or a claim that every existing system has passed multiplayer validation.

## Check the version before using an API

At this review, `package.json` reports **PurrNet 1.24.1**. `Packages/manifest.json` selects the Git
`release` ref; `Packages/packages-lock.json` resolves it to `7061f0a5b46b0aaff85d5dfe0f9791c5b153012a`.
The current source is under `Library/PackageCache/dev.purrnet.purrnet@d94aa16b6b00`.
The cache suffix is a package fingerprint, not the Git commit or a permanent path.

For each networking change, read the relevant official page and confirm the API in the installed
package. Online documentation may describe a newer release or explicitly mention `dev` behavior.
Recheck these facts after package updates. Treat older examples in `GEMINI.md` as historical advice
to verify, especially member names, RPC argument casing and lifecycle overloads.

## Choose an existing capability first

| Need | First capability to inspect | Our decision |
| --- | --- | --- |
| Body Animator parameters | [NetworkAnimator](https://purrnet.dev/docs/plug-n-play-components/network-animator) | Use the existing component for locomotion replication. |
| Position/rotation/parent replication | [NetworkTransform](https://purrnet.dev/docs/plug-n-play-components/network-transform) | Keep the existing root stream and interpolation; each transform has one movement owner. |
| Current replicated state | [SyncVar](https://purrnet.dev/docs/systems-and-modules/network-modules/sync-types/syncvar) | Prefer a sync type for state a newly observing player must reconstruct. |
| A request or directed notification | [RPCs](https://purrnet.dev/docs/systems-and-modules/remote-procedure-call-rpc) | Choose ServerRpc, ObserversRpc or TargetRpc according to the recipient and authority. |
| Networked world object | [Network Prefabs](https://purrnet.dev/docs/systems-and-modules/network-manager/network-prefabs), [spawning/despawning](https://purrnet.dev/docs/systems-and-modules/network-identity/spawning-and-despawning) | Register the asset and use the supported spawn path under the chosen Network Rules. |
| Addressable world object | [Addressable spawning](https://purrnet.dev/docs/systems-and-modules/addressables/addressable-spawning-and-despawning) | Reuse PurrNet's loading/spawn/release integration. |
| Shared scene changes | [Scene Module](https://purrnet.dev/docs/systems-and-modules/scene-module) | Use the server's scene module; gameplay may add a readiness gate. |
| Spawn/load/disconnect handling | [Connection and player events](https://purrnet.dev/docs/systems-and-modules/network-manager/connection-and-player-events) | Use the event for the actual lifecycle stage needed. |

Custom transport code needs a concrete requirement that the installed component cannot meet.
An action identity, validated gameplay rule or clip binding can still be custom data without
requiring a new general networking framework.

## Authority and lifecycle

Owner authority assigns writes to the owner, with server control when an owner is absent;
server authority assigns writes to the server. Choose the mode deliberately and inspect the
configured Network Rules. Ownership and control are related concepts, not interchangeable checks.
[Owner Auth](https://purrnet.dev/docs/terminology/owner-auth),
[Controller](https://purrnet.dev/docs/terminology/controller),
[Network Rules](https://purrnet.dev/docs/systems-and-modules/network-manager/network-rules).

Our project policy is owner-controlled player movement/body presentation, with server-controlled
weapon acceptance, ammunition, damage, loot transactions and match decisions. AI gameplay remains
server-controlled. Replicating an animation does not grant permission to change gameplay state.

Use `Awake` for local component references. For a NetworkBehaviour's manager-dependent subscriptions,
use its spawned lifetime and remove subscriptions on despawn. Parameterless spawn/despawn callbacks
run once on a host; role-specific `bool asServer` overloads run per side. Joining a connection,
membership in a scene, scene loading and observing an object are distinct lifecycle events.
[Lifecycle and events](https://purrnet.dev/docs/systems-and-modules/network-manager/connection-and-player-events).

Network modules should be initialized before their spawned lifetime and kept as stable instances;
do not replace a SyncVar/module after spawning. Match subscriptions to the module's actual cleanup
behavior when testing despawn/respawn. [Module pitfalls](https://purrnet.dev/docs/systems-and-modules/network-modules/common-pitfalls).

## Current animation decision

NetworkAnimator can automatically relay the selected Animator parameters. Triggers must go through
`NetworkAnimator.SetTrigger`; direct `Animator.SetTrigger` is not automatically captured. Follow the
documented component setup and parameter selection. [NetworkAnimator reference](https://purrnet.dev/docs/plug-n-play-components/network-animator).

Installed-source check: `NetworkAnimator.cs` exposes `ownerAuth`, `autoSyncParameters` and
`dontSyncParameters`. `NetworkAnimatorLogic.OnTick` gathers parameter changes on the controlling
peer when automatic synchronization is enabled. This supports the existing driver's direct
Animator float/bool writes.

Saved Player prefab check: the NetworkAnimator references the body Animator, `_ownerAuth` and
`_autoSyncParameters` are enabled. `_dontSyncParameters` excludes `BodyReloadSpeed`, which each
body derives from the confirmed weapon action and its bound clip. Current locomotion flow:

```text
Owner motor -> CharacterPresentationCapture -> BodyAnimationDriver -> body Animator
                                                                       |
                                                                NetworkAnimator
                                                                       |
Observer body Animator <-----------------------------------------------+
```

The eleven current driver parameters are `HorizontalAxis`, `VerticalAxis`, `MoveSpeed`, `VerticalSpeed`,
`IsGrounded`, `IsMoving`, `Idle`, `IsCrouching`, `IsSprinting`, `IsSliding` and `IsDowned`.
The local frame is a data contract, not a network packet. Remote motors are
disabled and must not write default movement values over the received Animator parameters.
Keep the current local capture; a separate NetworkCharacterPresentation/frame broadcaster is
not a requirement for this locomotion path.

The user reports local idle/walk/strafe/diagonals and NetworkAnimator replication working. That
report and the source/prefab inspection are not a fresh latency, late-join or dedicated-server test.
Future reload/inspect selection, duration, interruption and ammunition rules remain gameplay-owned.
Check what NetworkAnimator already provides before adding action playback synchronization.

## State, RPCs and asset lifetime

SyncVar is for a current value; an RPC is a directed call. Specify who can change the value and
how a new observer receives the latest state. Do not assume an unbuffered animation event restores
an in-progress action for a late joiner. [SyncVar](https://purrnet.dev/docs/systems-and-modules/network-modules/sync-types/syncvar).

For requests, use the received `RPCInfo.sender` when caller identity is needed. Ownership permission
does not replace gameplay checks such as distance, rate, equipment identity or whether an action is
allowed. Inspect `runLocally` and recipients to avoid double effects on the host.
[RPC reference](https://purrnet.dev/docs/systems-and-modules/remote-procedure-call-rpc).

Installed constructor spelling is lower camel case, e.g. `[ServerRpc(requireOwnership: true)]`.
Use C# named constructor arguments with `:`. Confirm the actual constructor before copying an online
example; the reviewed RPC page also contains older capitalized argument names.

For registered network prefabs, PurrNet supports Unity-style Instantiate/Destroy subject to Network
Rules. Avoid adding a second spawn announcement automatically; inspect the selected spawn path.
[Spawning/despawning](https://purrnet.dev/docs/systems-and-modules/network-identity/spawning-and-despawning).

For the explicit Addressables path, the installed manager offers `SpawnAddressableByGuidAsync`
and `DespawnAddressable`; the docs also describe Addressables.InstantiateAsync/ReleaseInstance
integration. Use a matching lifecycle. Keep cancellation, obsolete-load rejection and release ownership.
Our cosmetic FP/world weapon bindings are separate from spawning an authoritative world loot object.
[Addressable lifecycle](https://purrnet.dev/docs/systems-and-modules/addressables/addressable-spawning-and-despawning).

Shared scene loads belong to `sceneModule` on the server. Gameplay readiness can add conditions
after loading. Review the loaded/unloaded events with the player, scene and server/client role;
connection or scene membership alone is insufficient to establish readiness.
[Scene management](https://purrnet.dev/docs/systems-and-modules/scene-module),
[scene/player events](https://purrnet.dev/docs/systems-and-modules/network-manager/connection-and-player-events).

## Review existing systems in this order

These are review targets, not instructions to rewrite working systems or claims of defects.

| System | Next review question |
| --- | --- |
| Body locomotion | Preserve resolved crouch/sprint behavior, then add airborne/slide/downed coverage in small steps. Exercise host watching client, client watching host, stopping, wall pushing, moving supports and late observation. |
| Weapon actions | Which existing state/NetworkAnimator behavior already covers playback? Retain authoritative shots/ammo/reload and add only missing action identity/timing/variant data. |
| Aim rig | Keep the working target stream until a measured need justifies replacement. Parameter sync or a small sync type may be enough; a general snapshot framework is not presumed necessary. |
| Scene coordinator | Compare its custom readiness counting with supported loaded/unloaded events. Include destination identity, disconnects, failed loads and bounded wait behavior in its future review. |
| Loot and equipment spawning | Confirm prefab registration, spawn authority, initial state and matching release; retain async generation/cancellation protections. |
| Health, downed, inventory and AI | Check authoritative writes, request validation, persistent reconstruction and one set of subscriptions/effects on host. |

## Reload timing checkpoint — 31 October 2026

Installed PurrNet 1.24.1 exposes `networkManager.tickModule.syncedPreciseTick` and
`tickDeltaDouble`. Their product gives seconds in server-aligned tick space: the server uses
its own ticks, while clients use a periodically corrected estimate. This clock advances with
unscaled network time; reloads now continue independently of Unity timeScale. Tick synchronization
correction and pause/slow-motion presentation still need explicit integration tests.

Validation: Unity compiled the changed scripts and ten isolated Edit Mode checks passed using
the generated RPC implementation and a controlled TickManager clock. Checks covered atomic
acceptance, stale/duplicate rejection, subsequent sequence, deadline/ammunition, downed cancellation
and weapon-switch cancellation. The harness uses NetworkManager.CallAllRegisters before exercising
SyncVar in Edit Mode. No fresh connected multiplayer, latency or late-join run is claimed.

PlayerWeaponController now publishes ActionSequence, ActionStartedAt and the frozen ActionDuration
together with Reloading in the existing WeaponEquipmentState SyncVar. Server completion compares
the same clock against ActionStartedAt + ActionDuration. Fire cadence and trigger timeout retain
their existing clock. Downed and equipment-switch cancellation retain the existing Idle path.

The reload presentation checkpoint now consumes confirmed identity/timing in PlayerWeaponPresenter.
PlayerWeaponController.NetworkTime reads the current server-aligned tick clock. When a matching
visual is adopted, the presenter calculates elapsed network time and translates it into a local
unscaled start timestamp for the existing WeaponInstance/WeaponAnimator path. Both views receive
the same frozen duration, translated start and action sequence. Duplicate sequences do not replay;
a new sequence can restart playback even if an intervening Idle state was not observed. Equipment
revision changes cancel the obsolete visuals before applying a replacement.

WeaponAnimator uses unscaled update mode and local unscaled time, matching the server reload's
timeScale-independent policy. Reloads already expired when visuals load are not replayed; confirmed
gameplay state still owns the input guard and ammunition. Clock correction after playback begins
does not continuously seek an already-playing clip. Variants remain pending; the body playback
extension is described below. Do not compare ActionStartedAt directly with Time.timeAsDouble.

Validation: compilation and 24 isolated checks passed using the actual pistol prefabs, FP Animator
and the presenter's visual-adoption path. Tests cover 25%/50% phase seeking, frozen duration/speed,
both view payloads, duplicate/new sequences, delayed adoption, expired/future timestamps, cancellation,
equipment revision and animation re-enable. Delays use a controlled tick clock; re-enable seeds an
elapsed local timestamp because time does not advance within an Edit Mode callback. Addressable I/O,
real transport latency, continuous clock correction and an actual paused player loop were not tested.
No fresh connected host/client or late-join verification is claimed.

### Body reload playback — 31 October 2026

UpperBodyPoseController now subscribes to the existing PlayerWeaponController.OnEquipmentChanged
event on every peer, reads current state on enable and releases playback/subscriptions on disable.
It starts Upper_Body_Layer.Weap_Reload using elapsed network time and the bound clip's actual
length divided by the confirmed duration. ActionSequence plus EquipRevision suppress duplicate
starts while allowing a replacement action. Idle, despawn default state and downed state stop
owned playback and return to Upper_Body_Layer.Weap_Hold. Expired starts are skipped and future starts clamp to zero.

The saved Player binds the original hold/reload template clips and the weapon controller. Each
character now owns an AnimatorOverrideController, with per-weapon clip selection described below.
The original pistol reload clip is 2.433 seconds long. The same component owns the
pose weights: holding uses layer/aim weights 1/1, reload uses 1/0 and downed uses 0/0. Downed wins
over arriving reload state. The body Animator uses unscaled time to match the action clock.
Global slow-motion locomotion behavior and moving-platform visual timing need separate review.

Body action playback uses the existing equipment SyncVar as its transport; each peer calls its
local Animator.Play from that record. Do not also broadcast NetworkAnimator.Play/trigger commands
for this reload. BodyReloadSpeed is excluded from NetworkAnimator parameter synchronization to
avoid a second writer. Existing locomotion parameters and upper-layer weight still use NetworkAnimator;
Rig.weight remains locally applied on each peer. No custom snapshot sender or new RPC was added.

Validation: compilation and 29 isolated checks passed on actual Player prefab contents and its
Humanoid Animator. These cover seeking/speed, unchanged locomotion state, duplicate/replacement
identity, completion, downed priority, expiry/future timing, disable/re-enable and subscriptions.
An offline simulated observer role also plays and cancels from the confirmed action while lacking
NetworkAnimator control. This is not a transport, real host/client, visual hand-contact or latency test.
Harness and results are in ignored .codex/body-reload-before-20261031.

### Per-weapon body reload variants — 1 November 2026

WeaponPresentationDefinition supplies BodyHoldClip, the required normal BodyReloadClip and optional
BodyReloadEmptyClip. UpperBodyPoseController binds the hold/normal defaults and caches both reload
candidates when EquipRevision changes. Binding runs before the Idle early return, and the revision
is remembered only after successful binding. Its original serialized _holdClip/_reloadClip remain
stable override keys; the runtime selected clip supplies playback length.

PlayerWeaponController already selects ReloadType from server RuntimeWeapon.CurrentAmmo when
accepting the reload. Normal is zero/default; Empty is selected at zero magazine ammunition. The
selection is published with the existing action identity/timing. The body controller chooses the
clip for each new action after deduplication, timing validation and expiry checks. Empty uses its
optional clip when assigned, otherwise the equipped weapon's normal clip. A subsequent Normal
action restores the normal override, and equipping another weapon clears the prior empty candidate.
No additional RPC, packet type, Animator parameter or serialized prefab reference was added.

This checkpoint initially implemented body clip variants; first-person support is described below.
Server ActionDuration remains the common resolved reload duration. Variant-specific gameplay
durations and world-mechanism state remain separate work. The current pistol body Empty field
references its normal body reload clip as a placeholder; its FP Normal and Empty clips are distinct.

Validation: Unity compilation and 25 isolated checks passed using two actual Player prefab copies
and temporary in-memory weapon definitions/presentation assets. Distinct non-looping Humanoid clips
stand in for test variants; no authored clips/assets were changed. Tests cover Normal/Empty/Normal
without equipment changes or intervening Idle, speed/phase, duplicate updates, fallback, switching,
expiry, enable reconstruction, cleanup and per-character isolation. Shared assets and scene stayed
clean. The harness restores GameContent's original registry/load state in finally. No fresh Play Mode,
transport, connected host/client or real authored empty-reload visual validation is claimed.
Harness and results: ignored .codex/body-reload-variants-before-20261101.

### First-person reload variants — 1 November 2026

WeaponInstance now stores the confirmed ReloadType before OnReloadingChanged fires; the presenter
forwards it to both visual instances with duration/start/sequence. WeaponAnimator owns one runtime
AnimatorOverrideController per weapon visual. Awake creates the override and captures the original
effective Reload clip without reading the weapon definition, because visual initialization occurs
after instantiation. On reload start, it reads the initialized weapon's PresentationDefinition,
selects FirstPersonReloadClip or the optional FirstPersonReloadEmptyClip, and applies it to the
original serialized Reload slot. Missing Empty falls back to Normal; legacy presentations without
FP clips retain the original effective controller clip. Speed uses the selected clip's actual
length divided by the confirmed gameplay duration, replacing the old manual _reloadClipDuration.

OnDestroy restores the underlying controller only when the Animator still references the owned
override, then destroys that override. A controller installed by another system is preserved.
The saved FP pistol prefab now binds its original A_Pistol_A_Reload slot. WeaponAssetValidator
checks that slot, the effective clip, optional FP variants, non-looping positive length and
Humanoid/Generic compatibility. No prefab/script GUIDs, network messages or action clock changed.

Validation: compilation and 31 isolated checks passed using the actual FP prefab and authored
Normal/Empty clips (both 2 seconds, Generic, non-looping). These cover transforms, selection,
different-length test variants, phase/speed, duplicate/new sequences, fallbacks, cancellation,
re-enable, fire interaction, isolation and validator failures. The saved pistol validator reports
zero issues. Seven additional checks passed in an owned empty Play Mode scene: the player loop
advanced, authored Empty was selected and advanced while timeScale was zero, and component removal
restored/released owned controllers while preserving an external replacement. Scene_Hub was restored
clean and stopped, with Run In Background returned to its original off setting. Auto-tick was
already enabled before the test and remains enabled. No connected multiplayer/latency test or
full-game reload/prop visual acceptance is claimed. Current gameplay duration is still shared
between Normal and Empty; body Empty motion remains the configured placeholder.

Harnesses/results: ignored .codex/fp-reload-variants-before-20261101.

SyncTimer was also inspected: it provides a built-in replicated countdown with reconciliation,
but its installed start-message path starts a fresh local countdown on receipt. The action's
shared start timestamp is retained for this phase-seeking requirement. Source evidence lives in
Runtime/CoreModules/Ticks/TickManager.cs and Runtime/Components/NetworkModule/SyncTimer.cs;
see [SyncVar](https://purrnet.dev/docs/systems-and-modules/network-modules/sync-types/syncvar)
and [SyncTimer](https://purrnet.dev/docs/systems-and-modules/network-modules/sync-types/synctimer).

### Chamber-aware reload totals and variant durations — 2 November 2026

RuntimeWeapon.CurrentAmmo is the total loaded ammunition, including the chambered round when
nonempty. CurrentMagazineSize remains the resolved magazine capacity. The current model assumes
automatic chambering and uninterrupted reloads: a loaded reload targets magazine capacity plus
one, while an empty reload targets magazine capacity. CanReload and CompleteReloadFromReserve
use the same target, and the final transfer is limited by reserve ammunition. With capacity 12,
5 loaded / 20 reserve becomes 13 / 12; an empty reload becomes 12 / 8; 1 / 1 becomes 2 / 0.
Reloading again after the empty reload can top off from 12 to 13. Firing subtracts one from the
loaded total. Starting ammo remains magazine capacity, including the initially chambered round.
This does not model magazine removal, manual chambering or a separate chamber inventory.

The existing WeaponItemState.MagazineAmmo field carries this loaded total; its serialized name
is retained for existing consumers. The server still owns ammo transfers and publishes them in
the existing equipment SyncVar. No additional chamber flag, RPC or animation transport is needed
for this combined-counter model. The HUD displays loaded total / reserve using its existing data.

The server selects ReloadType and the matching resolved Normal/Empty duration when accepting
the action. BaseReloadEmptyDuration uses the normal base duration when configured as zero;
both durations then receive the same attachment modifiers exactly once. The new field defaults
to zero, and the resolver rejects nonfinite or negative effective Empty durations. Existing
explicit positive overrides are retained. The confirmed ActionDuration drives the deadline and
both presentation paths, so client animation does not decide when ammo becomes available.

Validation: Unity compilation and 41 isolated checks passed, including a 294-combination ammo
conservation sweep, low reserve, full/top-off/empty reloads, firing, attachment capacities and
durations, fallback and invalid durations. Actual Player prefab contents with a controlled tick
clock exercise Normal/Empty selection, duplicate rejection, no early transfer, completion and
published loaded totals. These invoke the server handlers offline; connected transport, latency
and visual Play Mode acceptance were not exercised. Scene_Hub stayed stopped and clean.
Harness and pre-edit backups: ignored .codex/chamber-reload-before-20261102.

### World pistol mechanism playback — 6 November 2026

WeaponAnimator now has a serialized WeaponAnimationView: FirstPerson is the zero/default value
for existing viewmodels, and World selects the world presentation clips. WeaponPresentationDefinition
adds WorldReloadClip, optional WorldReloadEmptyClip, and optional WorldIdleEmptyClip. Missing Empty
reload uses World Normal; missing world clips retain the world controller's own clip, never an FP
clip. World fire motion is optional: with no Fire state, a fire event leaves reload ownership alone.

WEAP_PF_Pistol_A now has the adapter on its WeaponInstance root and a separate Pistol_A_World
controller on WeaponMesh/Weap_Rig. This export's paths begin at weap_root, so that Animator location
matches the supplied Pistol_World_Reload_Normal FBX. Its imported clip is currently named Scene
and lasts 2.4 seconds. The controller defaults to Idle and uses ReloadSpeed for its Reload state.
The generated looping Pistol_World_Idle clip stores the prefab's neutral local pose for the rig,
weapon root, magazine and slide. These are constant rest keys, not whole-weapon movement.

The existing presenter already forwards accepted reload start, duration, sequence and variant to
both WeaponInstances. The world adapter now consumes those events with the same phase/speed rules
as FP. A 1.6-second action plays the 2.4-second world clip at speed 1.5. Cancellation returns to
the neutral clip. No new RPC, replicated property, ammo transfer or animation authority was added.
The original FP controller, clip imports, body bindings and existing asset GUIDs were preserved.

WeaponAssetValidator checks the adapter's view against the prefab role, original Idle/Reload slots,
loop/type rules and transform paths in optional presentation clips. A world override using FP arm
paths is rejected. World controllers require Idle/Reload; FP still requires Idle/Fire/Reload.

Validation: compilation and 28 isolated checks passed on actual FP/world prefab contents. These
cover view isolation, speed/phase, duplicates, optional Empty fallbacks, cancellation and magazine
restoration, enable reconstruction and authoring validation failures. A further native Animator
sample at 50 percent progress matches the authored world magazine pose and confirms actual motion.
The FP controller hash is unchanged and the saved pistol validator reports zero issues.

A dedicated Play Mode harness was prepared but was not executed because the world prefab was open
in Prefab Mode. A subsequent command mistakenly entered the main scene's Play Mode; it was stopped
immediately, the temporary background override was removed, and Scene_Hub and the world prefab
were confirmed clean and stopped. Auto-tick was already enabled and remains enabled. No connected
multiplayer, latency, real-frame mechanism test or final body/magazine visual acceptance is claimed.
Only the supplied Normal world reload is configured. Authored world Empty slide-lock/fire motion
remains future content. Harnesses and backups: ignored .codex/world-reload-before-20261106.

## Workflow for every future networked feature

1. Identify the gameplay owner, visual owner and data that another peer actually needs.
2. Consult the relevant built-in component/module and the installed source before proposing custom code.
3. Record the authority, replicated state versus events, spawn/late-join behavior and cleanup owner.
4. Implement the smallest missing game-specific behavior using existing modules.
5. Verify both host/client directions and the changed lifecycle cases. Claim only the cases exercised.
6. Update this reference with the version, source links, project decision and remaining limits.

When two transports would write the same parameters or transform, select one before extending it.
Use the [architecture guide](Architecture.md) for code ownership and the
[animation roadmap](Animation/IMPLEMENTATION.md) for the current implementation order.
