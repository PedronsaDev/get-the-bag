> Historical snapshot preserved on 6 November 2026. Superseded by [the current workflow](../../README.md). Statements below describe earlier plans or checkpoints, not current implementation status.

# Implementation baseline — 28 October 2026

## Latest locomotion decision — 29 October 2026

CharacterPresentationCapture now reads BaseVelocity and stable grounding after the motor step and
feeds BodyAnimationDriver. The saved Player has an owner-authoritative NetworkAnimator with
automatic parameter sync enabled. The user reports standing idle/walk/strafe/diagonals and
replication working. Use this built-in transport; the earlier custom presentation snapshot sender
proposal is superseded for current locomotion. Next extend resolved crouch stance and verify the
remaining host/client and lifecycle cases. See [BODY-LOCOMOTION](../../BODY-LOCOMOTION.md) and the
[PurrNet knowledge base](../../../PurrNet.md). This documentation update adds no runtime test claims.

## Earlier implementation record

The preparatory cleanup is implemented on `codex/character-animation-system`. The full design in [README](README.md) remains the target, and [IMPLEMENTATION](IMPLEMENTATION.md) defines the migration order. No milestone is considered complete solely because this cleanup compiles.

**29 October readability pass:** custom gameplay dependencies and animation tooling were cleaned up.
See [the current architecture guide](../../../Architecture.md) and [cleanup audit](../../../Code-Audit.md) for the
new validator boundaries, gameplay fixes and host/client verification. The later locomotion decision
above supersedes that pass's pending local-capture/custom-snapshot assumptions.

**Milestone 0 is now recorded:** see [the frozen baseline and conventions](../../MILESTONE-0.md), including real connected-host/observer evidence. That later capture establishes root/aim/action replication and exposes missing body locomotion/downed driving and stale observer rifle retention after a failed pistol load. These known failures remain implementation work; Milestone 1 is next. Historical verification statements below describe the preceding cleanup, not the newer baseline session.

## Code ownership now

**Body animation workflow:** [current capture, NetworkAnimator transport and remaining steps](../../BODY-LOCOMOTION.md).
The original isolated driver checks predate the user's live capture and NetworkAnimator integration;
retain their test limits rather than treating them as full Milestone 2 acceptance.

**Editor validation is implemented:** [commands, supported checks and limits](../../VALIDATION.md). Character/weapon profiles, actual rig and renderer bindings, Addressable prefabs, current weapon playback contracts and overlapping animation writers can be checked without changing runtime gameplay. The current Player/pistol still needs the documented root-motion, shadow-culling and connected-observer visual review before Milestone 1 is complete.

**Dual-view loading and visibility are now implemented:** [current setup and verification](../../DUAL-VIEW.md). The local owner has FP plus shadow-only world weapon instances; observers have the visible world weapon. CharacterVisualVisibility owns rendering/effect selection. Current replacement failure clears obsolete views, and both loaded views share per-client reload timing. The saved Player prefab is wired to the rig/visibility components. A real connected host/client test verified both directions of shot/muzzle playback, switching/fallback, and partial-load cancellation/failure/supersession. This supersedes the single-view checkpoint below; Milestone 1 still needs its remaining compatibility/editor/root-motion validation work.

**Earlier single-view extraction checkpoint:** WeaponVisualBinding was introduced to own one instantiated WeaponInstance and its Addressables prefab handle, with one active binding in the presenter. The subsequent dual-view implementation above reuses that resource owner and supersedes that single-binding topology. The earlier verification below remains historical evidence; it does not complete Milestone 1.

Compilation and targeted Play Mode checks passed for host shot/recoil, ADS/reload timing/guard, movement gates, independent owner/non-owner presenters, replacement cancellation and reset. Loading checks verified independent instances and handle reference counts, single-use loading, repeated disposal, cancellation/disposal of pending operations, rejected prefabs without root WeaponInstance and destroyed sockets. Controlled temporary providers verified exactly one resource release for each failure path. No new two-client or player-build test was run for this extraction. See [weapon-loading ownership](../../WEAPON-LOADING.md).

| Responsibility | Current owner |
|---|---|
| Server ammunition, fire permission, reload deadline, equipment | PlayerWeaponController and existing weapon gameplay types |
| One visual's Addressable load, instance and resource cleanup | WeaponVisualBinding, in Player/Combat |
| Equip generation/cancellation, separate world/FP bindings, forwarding fire/reload | PlayerWeaponPresenter, in Player/Combat |
| Character sockets, Animator/rig references and renderer groups | CharacterRigBinding, in Player/Animation |
| Owner/observer body, weapon and effect visibility | CharacterVisualVisibility, in Player/Animation |
| Owner ADS update, recoil/look/movement update order and reset | FirstPersonWeaponMotion, in Player/Animation |
| Authored Idle/Fire/Reload playback and ReloadSpeed | WeaponAnimator |
| Calibrated ADS transform | WeaponAimPose and WeaponAimPoseEditor |
| Cosmetic procedural transforms | WeaponLookSway, WeaponMovementSway, WeaponRecoil; stable shared WeaponSpring |
| Prototype torso aim target | PlayerAimTarget, using camera world forward; existing target NetworkTransform retained |

Weapon presentation components now live in `Assets/_Project/Scripts/Runtime/Gameplay/Combat/Weapons/Presentation/`. Their namespace remains `Project.Combat.Weapons` and assembly remains `Project.Combat`. The ADS inspector moved to `Scripts/Editor/Weapons/Presentation/`; all seven moved scripts retained their GUIDs. Serialized tuning and prefab references were preserved.

Look sway's duplicate semi-implicit spring was unstable with the current tuning at low frame rates and during hitches. It now uses the shared analytic WeaponSpring solver already used by recoil/movement. This is the only change to a moved effect's calculations; its serialized settings remain unchanged.

The presenter binds motion only for the local owner and resets it on disable/release. Input and locomotion gates, reload synchronization and Addressables cancellation stay in their existing owner. The unused presenter event duplicates and empty PlayInspect method were removed; WeaponInstance's real Fired/ReloadingChanged events remain. No inspect implementation is advertised.

The three unused WeaponPrototype assets and their folder were removed after dependency, raw GUID and Addressables entry checks found no outside references. A local pre-cleanup backup and working-tree patch are under ignored `.codex/animation-cleanup-before-20261028/`. Other pre-existing changes remain untouched; nothing was staged or committed.

## Historical cleanup content snapshot

These findings predate the user's world-pistol setup and the dual-view implementation. Use [DUAL-VIEW.md](../../DUAL-VIEW.md) for current behavior and remaining work.

- The authored pistol is `weap_pistol_a`, with owner prefab `Assets/_Project/Prefabs/Weapons/Pistols/WEAP_FP_Pistol_A.prefab`. Its current actions are Idle, Fire and Reload, plus procedural ADS, look/movement sway and recoil.
- The pistol's world Prefab Reference is empty. A world weapon-only prefab and simultaneous owner FP/world bindings are Milestone 1 work. A full owner/observer pistol test cannot pass until that is configured.
- `weap_ar_debug` has an existing world prefab and supports the legacy weapon-only fallback. It was used for the cleanup's non-owner lifecycle check.
- Body locomotion, masked holding and torso aiming remain the current prototype. This cleanup did not change the Player prefab, body Animator, clip imports, root-motion setting or locomotion motor.
- Authoritative action identity/timestamps, per-rig clip profiles, normal/empty reload selection, inspect variants, compact aim snapshots, foot IK and owner legs remain roadmap work.

## Verification completed

- Unity reported completed compilation with no compilation errors after the refactor and asset moves.
- Live combat-gym host advanced frames. A server-accepted shot reached the owner presenter's recoil path without changing gameplay camera aim.
- Addressable owner load retained the moved animation/ADS/recoil components. ADS advanced, reload blocked ADS, and ReloadSpeed matched clip duration divided by resolved gameplay reload duration.
- Disable/reset, repeated clear, independent presenter handles and superseded equip requests passed. A non-owner rifle presenter acquired no owner motion driver.
- The extracted driver ticked look/grounded movement and retained the airborne, vaulting, noclip and sliding movement gates.
- Torso target direction matched camera forward at actor yaw 0/90/180/270 degrees with camera pitch.
- Existing recoil and movement component regressions passed at 30/60/144 FPS and with a half-second frame, including damping/recovery, ADS attenuation, reload attenuation and reset/subscription checks. Recoil randomness was disabled only on the temporary test clone for deterministic comparisons; saved tuning was untouched.
- Look sway passed all 24 combinations of 10/15/20/30/60/144 FPS, hip/ADS and ordinary/250ms hitch frames after using the shared solver.
- All 37 pre-existing tracked working-tree diffs matched the pre-cleanup snapshot after verification. Existing Unity YAML whitespace warnings were left with that work.

Transient harnesses are in ignored `.codex/check-animation-cleanup*.cs`. The former debug-ID/input-sampling scripts are historical: their assumptions do not establish current gameplay parity. No fresh separate-client or player-build animation test was performed in this cleanup.

## Repeatable human check and next step

1. Open the combat gym and run a host. Check pistol idle/fire/reload, ADS entry/exit, strafe response and recoil recovery. Confirm reload blocks ADS and completion returns to idle. Switch to the rifle and back, then stop Play Mode.
2. Capture this on a separate connected observer once a world pistol exists. Compare facing, pitch, holding, shots and reload; record what is missing rather than assuming remote parity.
3. Start Milestone 1 with the current pistol/body only: explicit character/weapon visual binding, a weapon-only world pistol, owner FP plus world views, and character visibility ownership. Validate root motion before changing the saved body setup.

Keep action variants, generalized retargeting and new procedural effects out of this first binding change. New content becomes easier to add after the two views and their ownership are reliable.
