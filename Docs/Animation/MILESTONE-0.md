# Milestone 0 — Baseline and conventions

**Baseline recorded, 28 October 2026.** This milestone records the current system, including failures; it does not certify animation parity. Milestone 1 can now use the named cases below as its comparison. No gameplay, prefab, controller, model-import or package changes were made for this milestone.

## Evidence and scope

The saved [asset inventory](Baseline/2026-10-28/asset-inventory.json) covers three prefabs, two weapon definitions, six discovered Animator Controllers, eighteen model/animation FBXs and one mask. It records GUIDs, hierarchy/local transforms, component bindings/tuning, renderer layers/shadows, controllers/blend trees/transitions, Humanoid bones, clip curves, import flags and Addressable entries. Only the Player/body controller and Pistol_A controller are the active baseline controllers; discovery of another controller does not imply support.

The [host trace](Baseline/2026-10-28/host-trace.json) and [observer trace](Baseline/2026-10-28/observer-trace.json) come from two connected Editor processes, not a fake non-owner presenter. They include advancing frame counters, actor ownership, world positions, body parameters/states, aim targets, equipment/ammo/action changes and accepted-shot events. Samples use UTC and each peer's local Unity time; local Unity times are not a synchronized gameplay clock.

The scenario used Unity 6000.6.2f1, Unity CLI beta.14, MPPM main Editor plus virtual Player 2, direct local UDP, and Scene_Gym_Combat. Host owner was `001`; observer owner was `002`. These IDs are run-specific. The host produced 181 samples over 885 frames; the observer produced 196 samples over 972 frames. No recorder exceptions were reported.

Test settings: target frame rate 60, VSync off, background simulation enabled. Synthetic input used temporary keyboard/mouse devices with Editor input routed to the Game view; gameplay/UI permission checks remained active. Host and observer pawns were placed on open gym floor at (-8,100.01,-8) and (-12,100.01,-12). The diagnostic observer camera followed the remote pawn, excluded LocalViewmodel and selected an unobstructed viewpoint. It was a capture camera, not a new player camera feature. Host images are the 1920x1080 Game view; observer images are 1024x576 renders. The later confirmed-downed captures are 1024x576. Settings, temporary devices/cameras and scenario were removed/restored afterward.

## What the baseline actually does

| Case | Owner result | Connected observer result | Consequence |
|---|---|---|---|
| 01 Idle | Combined pistol arms/weapon visible | Body holding pose, no world pistol | World pistol reference is missing |
| 02 Walk forward | Pawn moved about 4.31m during sampled input | Root movement observed | HorizontalAxis, VerticalAxis and MoveSpeed stayed zero on both peers; body gait is not driven |
| 03 Strafe right | Pawn moved about 4.62m laterally during sampled input | Root movement observed | Same missing body locomotion parameter driver |
| 04 ADS/look | ADS reached 1; camera direction changed | BodyAimTarget updated remotely | Target endpoints differed by about 0.021m at the last ADS samples; this is a sample comparison, not a latency/accuracy guarantee |
| 05 Fire | One accepted pistol shot; ammo 24 → 23 | Same weapon/sequence event observed, ammo replicated | No observer pistol means no corresponding world weapon animation/effect to compare |
| 06–07 Reload | Reloading → Idle; resolved duration 1.6s; ammo 23 → 24 | Reloading/ammo changes replicated | No world reload clip/driver; holding pose continues |
| 08 Rifle switch | Existing weapon-only fallback loaded | World rifle loaded | Holding family still uses Pistol Hold; it is not a finished rifle pose |
| 09 Return to pistol | Authored FP pistol returned | Equipment says pistol, previous rifle remains visible | Known stale-world-visual failure when replacement loading fails; address in Milestone 1 |
| 11 Confirmed downed | Gameplay downed state true | Same owner's downed state true; body Animator IsDowned remains false | Downed gameplay replication exists; downed presentation is missing |

The loadout includes MOD_Magazine_Extended and MOD_Muzzle_Basic, so baseline magazine capacity is **24**, not the definition's base 12. Reload's source clip is 2s; resolved gameplay reload is 1.6s, yielding ReloadSpeed 1.25. Preserve attachments when comparing later runs.

The sequence's original `10-downed` captures were not accepted as a confirmed host-downed case: the two-player health/team state produced a different transition. The separate [host probe](Baseline/2026-10-28/downed-host-probe.json) and [observer probe](Baseline/2026-10-28/downed-observer-probe.json), captured immediately after controlled revival/downing, are the accepted downed-state evidence. The unrelated health/team-state behavior was not diagnosed or changed here. Use case 11 for comparison, not a filename alone.

The missing world pistol produces the existing invalid-prefab error. Inspector stale-selection errors may also occur during Editor Play Mode transitions. This baseline is not a claim of a clean runtime console or a player-build test.

### Named visual references

| Pose | Owner | Observer |
|---|---|---|
| Idle | [FP idle](Baseline/2026-10-28/host-01-idle.png) | [World idle](Baseline/2026-10-28/observer-01-idle.png) |
| Forward | [FP movement](Baseline/2026-10-28/host-02-walk-forward.png) | [World movement](Baseline/2026-10-28/observer-02-walk-forward.png) |
| Strafe | [FP strafe](Baseline/2026-10-28/host-03-strafe-right.png) | [World strafe](Baseline/2026-10-28/observer-03-strafe-right.png) |
| ADS/look | [FP ADS](Baseline/2026-10-28/host-04-ads-look.png) | [World look](Baseline/2026-10-28/observer-04-ads-look.png) |
| Reload | [FP reload](Baseline/2026-10-28/host-06-reload.png) | [World reload state](Baseline/2026-10-28/observer-06-reload.png) |
| Failed replacement | [FP pistol returned](Baseline/2026-10-28/host-09-return-pistol.png) | [Stale world visual](Baseline/2026-10-28/observer-09-return-pistol.png) |
| Confirmed downed | [FP downed](Baseline/2026-10-28/host-11-downed-confirmed.png) | [World downed state](Baseline/2026-10-28/observer-11-downed-confirmed.png) |

These are still frames. The traces establish state/position changes, but stills do not certify the quality of every animation frame. Review the repeated sequence in motion when accepting future milestones.

## Frozen rig and coordinate contract

These are documented conventions for the next implementation, not new profile assets or runtime IDs.

| Item | Baseline convention |
|---|---|
| FP rig family label | `FP_PistolA_Generic_v1`: combined Arms_Rig + Weap_Rig paths, one Animator on imported Pistol_A_Idle root, no Humanoid Avatar |
| Body family label | `Body_Mixamo_Humanoid_v1`: Player-Body Avatar, mixamorig bones, full-body Humanoid |
| Gameplay/view coordinates | Unity world/local +Z forward, +Y up, +X right; left-handed Unity coordinate convention. Camera world forward is already a direction; never apply actor yaw to it again |
| Weapon handedness | Right hand is dominant; world weapon socket currently points directly to mixamorig:RightHand |
| Units | Gameplay distances are metres. Keep presentation wrapper scales (1,1,1); preserve existing FBX unit conversion and imported compensation until a duplicate export is validated |
| Existing scale exception | FP Arms_Rig and arms mesh contain scale 100 in the imported hierarchy. Do not normalize them independently or assume every imported bone/root is unit scale |
| Root motion | FP Animator Apply Root Motion false. Motor owns character displacement. Body Apply Root Motion is currently true: recorded migration issue, not the desired final policy. Disable it only after the duplicate-clip check in Milestone 1 |
| Curves | FP Idle/Fire/Reload are Generic with Resample Curves false, importer globalScale 1. Keep curve compression/error and per-clip root/loop flags from the inventory, not one blanket import preset |
| Body imports | Current body/locomotion clips are Humanoid, Resample Curves true, importer globalScale 1. Preserve Avatar mapping and recorded per-clip flags |

Do not infer Blender bone axes or apply a new Blender export preset from Unity's world axes. The shipped FBX hierarchy/curve bindings are the compatibility contract for this baseline. A new exporter must reproduce those paths, scale and contact poses in a duplicate test before replacing the source exports.

### Semantic names and their current bindings

| Semantic role | Current binding relative to Player or weapon root |
|---|---|
| MotorRoot | Player/Character |
| BodyAnimator | Character/Root/Mesh Root/Player-Third-Person-Model |
| AimOrigin | Character/Root/Pitch Pivot |
| FirstPersonSocket | Pitch Pivot → Recoil Pivot → Tilt Pivot → Impact Pivot → Bob Pivot → Juice Pivot → Camera Target → ViewmodelSocket |
| WorldWeaponSocket | Body's mixamorig:RightHand; no explicit grip adapter yet |
| FP procedural roots | AimMotionRoot → SwayRoot → MovementRoot → RecoilRoot |
| FP authored root | RecoilRoot/Pistol_A_Idle, containing Arms_Rig and Weap_Rig |
| Muzzle/FirePoint | Pistol_A_Idle/Weap_Rig/weap_root/FirePoint |
| Sight/AimReference | Pistol_A_Idle/Weap_Rig/weap_root/AimReference; calibration samples Idle at time 0, sight distance 0.2m |
| BodyAimTarget | Body/AimTargets/BodyAimTarget; owner target NetworkTransform retained |

Exact full paths and GUIDs are in the inventory. New bindings should map semantic roles once; do not search names each frame. Existing LeftHandPos/RightHandPos markers are authoring aids, not an implemented runtime IK contract.

The current UpperBodyRig is active with weight 1. ChestAim constrains mixamorig:Spine2 to BodyAimTarget, weight 1, +Z aim/+Y up, Scene Up, all constrained axes enabled, maintain offset false and limits -180..180. This records current tuning; it does not prescribe these limits for every body. PlayerAimTarget projects Pitch Pivot forward 100m.

Player_UpperBody enables Body, Head, arms and fingers; Root, legs, foot IK and hand IK are disabled. Layer order is locomotion first, Upper Body override second (weight 1), then the rig correction. PurrNet NetworkAnimator is owner-authoritative with automatic parameter sync; it currently has no gameplay locomotion parameter writer to synchronize.

## Supported content and required authoring

| Combination | Status |
|---|---|
| Current Pistol_A FP clips + exact current combined rig | Supported baseline: Idle, Fire, Reload and procedural ADS/sway/recoil |
| Current Mixamo Humanoid body + current locomotion/holding clips | Valid imported/Animator setup; runtime gait/downed parameter driving incomplete |
| Current body + world Pistol_A | Unsupported until a weapon-only world prefab and grip binding are provided |
| Debug rifle + current views | Weapon-only fallback/lifecycle test; no authored matching FP rig or rifle holding family |
| Different FP arms skeleton/proportions, another weapon, another body Avatar | Unvalidated; do not promise automatic retargeting or shared clip compatibility |

Required for the current FP adapter: compatible Idle, Fire and normal Reload clips; matching state names; ReloadSpeed float/state multiplier; explicit source reload duration; required Animator/weapon/muzzle bindings. ADS uses an authored reference pose/marker plus procedural placement. Gameplay grants ammunition and decides shot/reload legality.

Required for the first body locomotion implementation: idle and forward/back/left/right walk coverage, holding pose, mapped Avatar and a parameter driver. Crouch idle/four directions, sprint and slide assets already exist, but their presence does not prove gameplay selection works. A downed body pose/transition is still required before claiming visual downed support.

Optional later content: equip/unequip, empty reload/slide-open state, inspect variants, alternate grips, jump/land actions and additive pose corrections. There is no inspect selector, empty-reload selector, foot IK, owner-leg visibility feature or generalized rig/weapon compatibility system yet.

## Repeat the one-pistol baseline

1. Keep this branch's Player prefab, FP pistol, definitions/attachments, controllers and import settings. Use the [file manifest](Baseline/2026-10-28/file-manifest.json) to identify the exact content; never reset unrelated developer changes to reproduce it.
2. Open Scene_Gym_Combat. Run a main host and one MPPM clone in the same scene (clone player index 1), or connect a separate development client. Confirm two peers and that each owns one pawn. Enable background simulation for local multi-window testing.
3. Use an open flat floor area and separate spawn positions. Confirm the local input action map is enabled, no console/menu blocks input, and both peers' frame counters advance. For automated Editor input, temporarily use IgnoreFocus and AllDeviceInputAlwaysGoesToGameView, then restore them. Do not bypass UI action gates.
4. Record two seconds idle, roughly one second forward, one second right strafe, ADS entry/hold/exit plus yaw/pitch, one safely directed accepted shot, normal reload through completion, rifle equip, and return to pistol. Record the current gaps as well as successful behavior. Compare by owner/equip identity and action sequence, not fixed player IDs or one screenshot.
5. Check body parameters while moving, remote target direction while looking, shot sequence/ammo, resolved reload duration and visual identity after switching. To capture downed, ensure the intended pawn is actually downed (not true-dead) on both peers and capture before bleedout; capture its Animator parameter separately. The accepted probes demonstrate this distinction.
6. Save new named host/observer images and telemetry in a new baseline folder. Do not silently overwrite the frozen baseline. Include render/input settings and any temporary setup such as an observation camera or administrative revival.
7. Stop both clients through normal game shutdown, restore the prior scene/scenario/input/render settings, and compare Git state. No saved scene or prefab changes should be needed for a recording.

For a fresh saved-asset inventory, run `Tools/AnimationBaseline/CaptureAssetInventory.cs` through Unity CLI's `script run` in Edit Mode; it writes a new timestamped folder. The original transient sampling scripts remain in ignored `.codex/animation-milestone0/`; the protocol above is the supported human repeat procedure.

## Implementation handoff

Milestone 1: supply the world pistol, explicit rig/grip binding, simultaneous owner FP/world instances and coherent visibility; include an explicit policy for a failed replacement so an old rifle cannot masquerade as the pistol. Preserve the current FP feel and gameplay authority. Milestone 2 then supplies the shared local/remote body parameter driver; authoritative action records and expanded clips follow in the roadmap order.
