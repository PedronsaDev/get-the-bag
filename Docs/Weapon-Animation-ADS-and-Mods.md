# Weapon animation, ADS, and modification plan

This document describes a staged way to build the weapon presentation system for GetTheBag. The target is a first-person view that feels crisp, responsive, and physical: authored animation provides clear intent and timing, while IK and restrained procedural motion adapt it to each weapon. Gameplay remains server-authoritative, and cosmetic animation never decides whether a shot or reload happened.

The visual target is informed by public first-person animation talks from [Bungie](https://www.gdcvault.com/play/1022297/The-Art-of-First-Person) and [Respawn](https://www.gdcvault.com/play/1027904/Animation-Summit-Sustained-Animation-Excellence), plus public GDC material describing layered recoil and IK. Those sources do not expose Apex Legends' exact internal weapon-animation pipeline. The recommendations here are an established, practical architecture for the current Unity project, not claims about proprietary implementation details.

## Goals and boundaries

The first pass should provide:

- One clean first-person idle and locomotion pose for a weapon.
- A readable equip, fire, reload, and optional chambering/inspect action.
- Hands that stay aligned to weapon grips during ordinary use and can leave them during authored actions.
- Aim down sights (ADS) that aligns the sight to the center of the camera, blends FOV and viewmodel pose, and uses the selected optic's configuration.
- A restrained recoil/sway layer that does not make the view difficult to read.
- Third-person presentation for other players using a suitable body/weapon pose, without replicating per-frame bone transforms.
- Attachments that affect presentation and resolved gameplay stats without mutating shared ScriptableObject definitions.

The first pass does not need per-weapon bespoke locomotion, complex optical render textures, extensive weapon inspection trees, procedural hand animation for every action, or client-side hit authority. Build one weapon to a high-quality baseline before generalizing the workflow.

## Current project state

Update after this plan: the mod/stat foundation is now implemented, including
attachment definitions/registry, compatibility rules, immutable resolved stats and
replicated selected IDs. See [Weapons.md](Weapons.md) for current behavior and setup.
The presentation/ADS proposals below remain future work; the original inventory
of missing modification support below describes the state when this plan was written.

The following is based on the code and prefabs inspected for this plan:

- `WeaponDefinition` provides stable weapon identity, baseline combat values, fire mode, and one Addressables visual prefab reference. It has no ADS configuration, animation set, or modification model yet.
- `PlayerWeaponController` is the server authority for fire acceptance, ammo, and timed reload state. It replicates `WeaponEquipmentState`; accepted shots are sent to observers.
- `WeaponEquipmentState` represents active slot, per-slot weapon IDs/ammo, action, and equip revision. Its action state currently only distinguishes `Idle` and `Reloading`.
- `PlayerWeaponPresenter` loads one weapon prefab for the owner and one for remote players. It triggers optional `Fire`, `Reload`, and `IsReloading` Animator parameters on both the arms animator and `WeaponInstance` animator.
- `WeaponInstance` exposes `FirePoint`, `LeftHandGrip`, and `RightHandGrip`. The debug weapon prefabs assign both grip transforms.
- `Player.prefab` contains an Animation Rigging `RigBuilder` and rig objects. The inspected runtime code does not yet bind the weapon grip transforms to hand IK targets. Verify the serialized constraints and rig evaluation order in the Editor before relying on them.
- `FPSCameraController` currently writes FOV each frame for base/sprint movement. It also exposes `AddPitchRecoil`, which immediately changes camera pitch; the weapon firing flow does not currently call it, and it has no recoil recovery model.
- The input provider finds an input action named `Aim`, but currently maps its pressed/held values into `SecondaryFirePressed` and `SecondaryFireHeld`. `PlayerInputData` has no dedicated ADS field. Separate ADS intent from secondary fire before adding aiming behavior.
- The current hitscan direction is built from the camera rotation and validated by the server. ADS should initially improve the viewmodel/sight alignment and camera FOV without changing the trusted authority boundary.

These seams are enough to start, but they are not yet an ADS or attachment implementation.

## Recommended ownership model

Think of the system as four cooperating layers:

```text
server-approved weapon/action state ───────┐
local input and immediate visual response ─┼─> presentation state
weapon/attachment definition data ─────────┘      │
                                                  ├─ authored arms/weapon animation
                                                  ├─ IK hand placement
                                                  ├─ viewmodel alignment and FOV
                                                  └─ small cosmetic motion
```

The weapon is the reference object that hands attach to. The arms Animator authors the pose and action. IK corrects the pose to fit the currently equipped weapon or optic. During reload/inspect, authored motion is allowed to override grip IK where the hand needs to move. Procedural effects add small offsets after those intentional poses.

Avoid making the weapon prefab the owner of all player-arm animation. The weapon definition chooses which presentation profile and visual configuration to use; the presenter coordinates it; the arm rig animates the arms; the weapon prefab animates its own mechanical parts. This keeps weapons interchangeable and keeps player animation out of combat authority code.

### Responsibility table

| Concern | Owner | Authority |
|---|---|---|
| Ammo, fire cadence, reload acceptance/completion, resolved combat values | `PlayerWeaponController` / runtime weapon state | Server |
| Weapon/attachment IDs and effective stat calculation | Loadout instance and one stat resolver | Server for gameplay; same resolved data available to owner UI |
| First-person arm animation, ADS blend, sway, visual recoil | First-person presenter/viewmodel components | Local owner, predicted for responsiveness |
| Weapon slide/bolt/magazine motion | Equipped weapon visual and its authored clip/profile | Local presentation; third-person equivalent where needed |
| Remote body pose and weapon action | Replicated compact state plus remote presenter | Observers render server-approved state |
| Hit result | Existing server-side fire/hitscan flow | Server |

The first-person viewmodel may respond immediately to local input, then reconcile to accepted state. Gameplay never waits for an animation event. Conversely, a client animation event must never grant ammo, authorize a shot, or complete a server reload.

## Animation system design

### Pose and action composition

Use animation for the motion that has authored intent: equip, reload, chamber, inspect, and the main firing pose. Use IK for final hand placement and small mismatches, not to invent the whole action. The Animation Rigging Two Bone IK constraint solves a limb toward a target and can use a hint to control elbow bend; this is exactly the kind of correction layer needed here ([Unity documentation](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html)).

Suggested conceptual order:

```text
base arms pose (idle / locomotion / aim)
  -> action layer (equip / fire / reload / inspect)
  -> weapon-specific or attachment-specific pose correction
  -> hand IK (enabled/weighted per action)
  -> small viewmodel recoil and sway offsets
```

The precise Animator layer and rig order should be verified in the actual graph. A common failure is letting full-weight IK pin both hands to grips throughout a reload; that fights the authored reload clip. During an action, lower or disable the relevant hand constraint, then blend it back to the weapon grip as the hand returns.

### Animator contract

Replace the current tiny optional parameter convention with an intentional presentation contract as the number of actions grows. The contract can remain simple: a base locomotion/aim state, an action identifier, action start/stop, and a fire pulse/sequence. Validate required parameters when the visual initializes, and report a clear asset-context warning when a controller does not satisfy its selected profile.

Do not put weapon gameplay logic inside Animator state machines. A normalized clip time or authored marker can help align cosmetic events, but server timing and validation stay in the controller. If the reload visual has a magazine-removal moment, treat it as VFX/mesh presentation; the server still transfers ammo at its accepted reload completion deadline. If you later need a visible ammo transfer moment, define a server-owned commit time in resolved weapon data and make both server and presentation follow that same value.

Keep action transitions interruptible by an explicit policy. For example, firing may be allowed during idle/aim only; reload may be interrupted by a valid weapon switch or downed state; inspect may be cancelled by fire/reload/aim loss. The controller determines accepted action state. The presenter responds to it and blends out safely.

### Weapon versus arms animation

For the owner view, prefer one of these authoring arrangements and keep it consistent:

1. **Shared viewmodel Animator hierarchy:** arms and the weapon's moving parts are under one animation graph, so a clip can coordinate them directly. This gives the simplest synchronization for reload and chambering but makes a single prefab/rig hierarchy more involved.
2. **Separate arms and weapon Animators:** this matches the current `PlayerWeaponPresenter` + `WeaponInstance` shape. Both receive the same action and timing contract. It supports modular weapon prefabs, but clips must be authored and tested together because one Animator cannot directly key another Animator's transforms.

For the current architecture, the separate-Animator arrangement is a reasonable first step. A weapon can animate its magazine, bolt, slide, trigger, and small mechanical recoil; the arm controller animates the hands/body pose. Keep both profiles selected by weapon presentation data and test their normalized timing together. Don't add a second independent reload state machine inside the weapon prefab.

### Animation set for the first weapon

Author only the states needed to evaluate the pipeline:

- **Idle/ready:** stable silhouette, slight authored life only if desired.
- **Equip:** short, readable bring-up; gameplay can remain immediately active or have an explicit server equip lock later.
- **Fire:** one concise recoil cycle, then return. Full-auto uses repeated impulses/blending, not a long uninterruptible firing clip.
- **Reload:** one tactical reload first. Add empty reload/chamber distinction only when the weapon rules need it.
- **Aim pose:** a stable pose aligned to the sight. It can be a blend from hip pose, while the viewmodel root aligns precisely to the selected sight reference.
- **Inspect:** optional, fully cosmetic, cancellable, and never allowed to block authoritative gameplay unexpectedly.

Prefer weapon archetype profiles (pistol, rifle, shotgun) over one unique state machine per weapon. Add a per-weapon override only when the silhouette or mechanism genuinely needs it.

## IK and attachment anchors

Keep the grip transforms on the weapon visual. The presenter supplies their world transforms as targets to the first-person rig. Keep elbow hints on the player rig, because elbow direction is a character/pose concern rather than a weapon attachment concern.

- Right hand: use as the stable firing/aiming hand. Its animation should carry most of the authored pose. IK may make a small final correction; avoid allowing right-hand IK to pull the weapon away from the sight alignment.
- Left hand: use the weapon's support grip during idle, firing, and ADS. Release or reduce its IK weight for reload, inspect, and any action that moves that hand.
- Weapon root: parent under a viewmodel anchor/socket, not under the left hand. This gives the presenter a stable transform for ADS and recoil while both hand targets follow the weapon.
- Attachment mounts: author named transforms for optic, muzzle, magazine, and other supported slots on the weapon prefab. Keep mount transforms stable across compatible variants.
- Sight reference: provide an iron-sight reference on the base weapon. An optic attachment can provide its own sight alignment reference and aiming configuration.

The exact right-hand policy depends on the rig. If both hands are fully constrained to a weapon that is also moved by hand animation, the graph can become circular or overconstrained. Choose one transform as the source of truth for the weapon pose, then make IK follow it.

## ADS design

ADS has three independent outcomes that should share one aim state:

1. **Input/state:** the owner requested aim; the server accepts or rejects aim according to weapon/action rules.
2. **Visual alignment:** the presenter blends the weapon/viewmodel so the active sight reference aligns with the camera center.
3. **Gameplay handling:** the resolved weapon stats apply ADS accuracy/recoil/movement modifiers, and the server uses the accepted aim state for firing validation and calculations.

The local camera should not be physically moved to meet the gun sight. Move the viewmodel to the camera's aiming line. This preserves the player's world view and keeps camera aiming as the stable reference. Initially, keep the current camera-ray hitscan approach; tune the viewmodel/optic alignment against that ray. Later, if you add muzzle-origin ballistics or close-range parallax correction, make that an explicit server gameplay change and test near-wall behavior.

### ADS transition and visual alignment

The weapon or installed sight supplies an `AimReference` transform whose position and forward/up orientation represent the optic's usable sight line. The presenter computes a target viewmodel-root pose that brings this reference to the camera's aim anchor. Blend from hip pose to that target with a damped transition; avoid snapping or independently blending the arms and weapon into different positions.

The sight-alignment target and support-hand grip target should be evaluated after the root has moved. Then update the hand IK targets/weights and evaluate the rig. Keep small animation offsets in the authored base pose, with the alignment solve responsible for final placement. Avoid multiple scripts writing the same viewmodel transform in `Update`.

ADS should be a continuous weight from 0 to 1 rather than only a boolean, because it drives viewmodel position, FOV, animation blend, sensitivity, and camera sway. The requested state can be boolean; the presentation transition is smoothed locally.

### FOV, reticle, and input

The camera currently writes base/sprint FOV directly. ADS cannot add a second competing FOV writer. Centralize FOV composition in the camera controller or a dedicated camera presentation component. It should choose a target from locomotion and ADS/scope state, then perform one smooth blend. Define precedence deliberately: e.g. sprint blocks ADS; sprint FOV is used only when not ADS; ADS target is determined by the active sight; downed/other camera overrides have explicit priority.

Reticle/UI behavior should follow the same local aim blend. A simple first pass can use a central reticle plus optic overlay; a later scope-specific camera/render texture is optional and adds cost and complexity. If using a magnified scope, author the optical FOV and reticle scale together. Avoid changing the player's look input scaling by accident when changing camera FOV.

The `Aim` InputAction already exists, but the provider currently maps it into secondary fire. Introduce dedicated `AimHeld` input data and route it through a dedicated weapon aim request/state. Do not overload primary/secondary fire, because it makes action gating and controller behavior ambiguous.

### ADS networking and authority

For the owner, predict the presentation blend as soon as aim input changes so ADS feels immediate. Send aim intent to the server through a validated owner RPC. The server rejects it when no weapon is equipped, the player cannot act, or an incompatible action is active. The server's accepted aim state is the source for gameplay modifiers and the compact state observers need for third-person aiming pose.

Do not network the camera/viewmodel transform every frame. Replicate compact state such as active slot, action, installed attachment IDs/config revision, and accepted aim boolean/weight if third-person presentation needs it. The owner can smooth local visuals; remote clients can use a third-person aim pose and their own interpolation. Existing shot events remain the trigger for remote fire animation. Cosmetic inspect details do not need to be networked unless other players must see them.

Be explicit about release and interruption: switching weapons, reloading, sprinting if prohibited, becoming downed/dead, or despawning should clear accepted ADS state and stop the local blend without leaving the camera zoomed. A stale RPC for a previous equip revision should not re-enable ADS on the wrong weapon.

## Weapon mods and animation compatibility

### Data model

Keep `WeaponDefinition` as the base authored weapon asset. Do not create a different base definition for every attachment combination, and never modify a shared ScriptableObject at runtime.

Use a saved loadout/runtime weapon instance to hold stable IDs:

```text
WeaponInstanceId (only if persistence/trading/world identity requires it)
WeaponDefinitionId
InstalledAttachmentIds by slot
```

An attachment definition should describe its stable ID, slot, compatibility rules, stat modifiers, optional visual prefab/reference, and optional sight/animation presentation contribution. The loadout stores IDs rather than direct object references in replicated state. Resolve those IDs against loaded registries on the server and validate slot compatibility before use.

### One stat resolver

Resolve a weapon in one deterministic pass:

```text
base WeaponDefinition + valid installed attachments
    -> immutable ResolvedWeaponStats + ResolvedWeaponPresentation
```

`ResolvedWeaponStats` should contain the effective values the runtime weapon/controller actually needs, such as damage, magazine capacity, reload duration, fire rate, range, accuracy/spread, recoil tuning, aim movement modifier, and ADS transition/handling data. `ResolvedWeaponPresentation` should identify the selected visual parts, active sight reference, arm/weapon animation profile, and optic FOV/reticle data.

Define modifier ordering and limits once. A practical rule is: gather compatible modifiers in stable slot/ID order, apply additive/multiplicative operations in a documented order, then clamp to allowed bounds. Reject invalid combinations on the server. The owner UI should use the same resolved result so its displayed stats match server behavior.

Do not store resolved values back into `WeaponDefinition` or attachment ScriptableObjects. Cache the resolved snapshot per runtime weapon/loadout revision; rebuild it only when the loadout changes.

### Visual mod assembly

Start with one visual source of truth. For each supported attachment slot, the weapon prefab provides a named mount. The presenter instantiates the selected compatible visual under that mount and retains/releases any Addressables handles with the same ownership discipline as the base weapon model. The attachment visual may expose data markers such as sight alignment or muzzle reference, but it should not own player input, ammo, firing, or the main action state.

Avoid a combinatorial prefab for every weapon-plus-mod combination. Also avoid arbitrary runtime socket searching by display name. Validate required mounts and sight references when content is loaded or in editor validation, then report the weapon and missing slot clearly.

The first mod milestone can support one optic only. Confirm that it changes the optic mesh, sight reference, ADS FOV, and optionally aim speed. Then add a muzzle attachment or magazine only after mount validation and visual cleanup are reliable.

### Animation compatibility for mods

Attachments should fit an animation contract rather than each creating a separate player Animator. A mod may select an animation profile override only when it changes a meaningful pose/action (for example, a different magazine reload or a stock that changes the support pose). Ordinary optics and muzzle devices should reuse the weapon archetype clips and only change visual alignment/handling data.

If a mod changes the reload mechanics, the same resolved stats must drive both the server reload deadline and cosmetic timing. If a mod adds a moving mechanism, animate that mechanism in the attachment/weapon visual profile. Do not let installed parts introduce an independent reload timer.

## Recoil, sway, and impact

Keep these layers individually tunable and opt-out capable:

- **Gameplay recoil/accuracy:** server-side resolved values used for shot validation/accuracy. Keep it separate from purely cosmetic camera shake.
- **Aim/camera recoil:** a deliberate, bounded effect on view aim if the design wants recoil to affect the next shot. It needs a recovery/return model and must be reconciled with the owner's look input; an instantaneous pitch write alone is not a complete recoil system.
- **Viewmodel recoil:** a fast authored or procedural arm/weapon impulse, with controlled return and small weapon-specific variation.
- **Camera impact:** a much smaller transient response. Use sparingly so the sight remains readable.
- **Sway/bob:** low-amplitude motion driven by look/movement and blended down while ADS. Keep its target/solver separate from recoil so a recoil kick is not smoothed away by sway damping.
- **Weapon mechanism:** animate slide/bolt/trigger independently where the weapon design benefits.

For the desired punchy style, tune the motion around a clear impulse and a quick, controlled settle. Favor strong timing contrast over large travel distance. Do not stack a large camera pitch kick, large viewmodel translation, large rotation, and strong camera shake for every shot. During automatic fire, test whether each shot adds a distinct impulse or whether a stable cyclic motion reads better; avoid accumulating unbounded offset.

Inspect the aim after recoil too: when ADS, the weapon can kick away from the sight line and return, but the camera/gameplay aim policy must be intentional. If weapon recoil affects aim, use a server-known recoil/accuracy policy for actual shots; do not derive authoritative hit direction from whatever cosmetic transform happened to render that frame.

## Multiplayer presentation policy

The owner and remote players need different assets/poses:

- Owner: dedicated first-person arms/viewmodel, predicted ADS visual, detailed reload/fire/inspect, local recoil/sway, first-person-only shadows as currently configured.
- Remote player: third-person body/weapon pose, compact aim/action states, confirmed fire event, no first-person camera motion or per-frame bone replication.
- Server/dedicated server: no need to instantiate viewmodel assets. It validates actions and updates compact gameplay state.

The network should communicate intent/state, not animation samples. Remote animation can start from the replicated action and smooth locally. Late observers should still get a sensible current pose from the snapshot (e.g. Idle/Aiming/Reloading) instead of relying only on a transient trigger that they may have missed.

Attachments must be derived from server-approved IDs. Clients should not report arbitrary visual/stat modifications as trusted values. Validate loadout and compatibility before publishing the equipment state.

## Staged implementation plan

### Milestone 0: Lock down contracts and authoring checks

1. Separate the Aim input from secondary fire in `PlayerInputData` and `LocalPlayerInputProvider`.
2. Document which actions interrupt or block aim, fire, reload, and inspect.
3. Inspect `Player.prefab` rig layers/constraints and establish evaluation order; confirm grip targets and hints are valid.
4. Decide whether the current separate arm/weapon Animators remain the first-pass arrangement (recommended) and define one shared action/timing contract.
5. Add editor/runtime validation for required animation parameters, weapon grips, mount points, and base sight reference.

**Exit check:** no controller owns gameplay because a clip started; Aim no longer collides with secondary fire; one weapon prefab reports all required presentation references.

### Milestone 1: One authored weapon loop

1. Use the debug rifle or pistol as the only test case.
2. Author and hook up idle, equip, single-shot fire, and tactical reload for first-person arms.
3. Add the corresponding weapon mechanism animation to the weapon Animator.
4. Trigger first-person animation immediately for responsiveness; trigger remote animation only from accepted server state/events.
5. Verify reload cancellation, weapon switching, downing, death, despawn, and a weapon visual that loads late.

**Exit check:** firing does not lock player aim, reload completes from the server timer, and no hand remains pinned to a grip when its authored clip needs to move it.

### Milestone 2: Grip IK and pose correction

1. Connect the support-hand Two Bone IK target to `WeaponInstance.LeftHandGrip`.
2. Add elbow hints and tune the base pose before enabling full constraint weight.
3. Blend support-hand IK out for reload/inspect, then back in as the hand returns.
4. Test a second weapon with a different grip position to prove the rig adapts instead of relying on one prefab's exact coordinates.
5. Add right-hand IK only if the authored primary-hand pose cannot reliably fit all weapon variants.

**Exit check:** both weapons hold correctly without visible elbow flips, hand penetration, or unwanted gun rotation.

### Milestone 3: ADS without mods

1. Add one iron-sight reference to each base weapon visual.
2. Add a dedicated owner ADS request and server-accepted state; define action rejection/interruption rules.
3. Blend the viewmodel root so the sight aligns with the camera aim anchor; ensure grip targets follow afterward.
4. Centralize FOV composition so sprint and ADS cannot overwrite each other.
5. Add ADS pose blend, reticle behavior, sensitivity policy, and ADS sway/recoil tuning.
6. Keep the existing camera-ray shot direction as the first gameplay baseline. Test at close range, mid range, while moving, and under recoil.
7. Replicate only compact accepted aim/action state needed for remote body pose.

**Exit check:** the optic is centered, zoom is smooth, shots land where the sight indicates, camera FOV has one writer, and remote players do not receive per-frame viewmodel transforms.

### Milestone 4: Resolved stats and one optic mod

1. Define an attachment ID/slot/compatibility model and selected attachment IDs on the runtime/loadout data.
2. Build a pure, deterministic resolver from base definition plus valid attachment definitions to immutable stats and presentation data.
3. Have `RuntimeWeapon`/controller consume the resolved combat values rather than reading base values when mods are active.
4. Add optic mount and attachment visual loading with clear Addressables handle ownership and cleanup.
5. Select the optic's sight reference, FOV, and reticle configuration in resolved presentation data.
6. Publish only validated attachment IDs/revision to clients; each client resolves the same visuals from loaded content.

**Exit check:** the optic changes appearance and ADS alignment/FOV, the server uses the resolved values, the UI can show the same values, and swapping/reloading content does not leak handles.

### Milestone 5: Recoil and locomotion polish

1. Add viewmodel impulse/recovery first and tune it without camera shake.
2. Add gameplay aim recoil only if it is part of the intended skill/accuracy model; define server and local reconciliation.
3. Add tiny camera impact after viewmodel motion reads well.
4. Add hip sway/bob, blend down in ADS, and test sprint/crouch/slide/downed transitions.
5. Test semi-auto taps and sustained automatic/burst cadence at realistic frame rates.

**Exit check:** shots feel distinct and responsive, ADS stays readable, and procedural motion does not accumulate or fight authored clips.

### Milestone 6: Remote pose and content scale-up

1. Add a simple third-person aiming and firing pose for remote players.
2. Ensure late joiners get a valid current pose from replicated state.
3. Add reload/chamber profile variants only for archetypes that require them.
4. Add other attachment slots one by one; test every supported combination against mount/animation compatibility rules.
5. Profile animator/rig cost only after the behavior and asset pipeline are correct.

**Exit check:** host, client, and remote observer agree on equipment/action state; cosmetic differences never affect server combat.

## Validation matrix

| Scenario | Owner view | Server/gameplay | Remote view |
|---|---|---|---|
| Enter play with weapon equipped | Viewmodel loads, arms ready, sight reference valid | Loadout and base stats initialized | Third-person weapon appears |
| Aim press/release rapidly | Smooth blend with no stuck zoom | Accepted aim state changes or rejects by action rules | Pose changes from compact state |
| Aim while sprinting/reloading/downed | Follows documented interruption policy | Server enforces the same policy | Snapshot converges to accepted state |
| Semi-auto and automatic fire | Correct per-shot visual response | Ammo/cadence/hits remain server-owned | Confirmed fire animation, no local-only hits |
| Tactical/empty reload | Hand leaves grip, mechanism motion aligns | Ammo transfers only at authoritative completion | Reload pose has sensible duration/state |
| Equip/swap during load/action | Old animation and visual clean up | Stale equip revision/action rejected | Correct weapon/action snapshot |
| Attach/remove optic | Mesh, sight target, FOV/reticle update together | Attachment ID/compatibility and resolved stats validated | Same optic and aim pose resolve |
| Despawn/rejoin | Camera/zoom/recoil and loaded visuals reset | State is reconstructed from loadout | No stale trigger-only state or leaked handle |

Use recordings or frame-by-frame review for hand intersections, muzzle/sight alignment, pose pops, and transition timing. A rig can be technically valid while looking poor; visual acceptance should be explicit per weapon archetype.

## Decisions to keep stable

- Do not let animation events control ammo, damage, or network authority.
- Do not replicate camera, arms, weapon, or IK transforms every frame.
- Do not mutate shared weapon/attachment ScriptableObjects at runtime.
- Do not let multiple systems write camera FOV or the same viewmodel root transform.
- Do not constrain both hands at full weight during every action.
- Do not build a unique combined prefab for every possible weapon/attachment combination.
- Keep resolved combat data and resolved visual presentation separate, even when one resolver creates both.
- Validate stable IDs, compatibility, required mount points, sight references, and animation contracts before the content reaches a live player.

## Source references

- David Helsby, Bungie, [The Art of First Person Animation for Destiny (GDC)](https://www.gdcvault.com/play/1022297/The-Art-of-First-Person). Its overview emphasizes the persistent relationship between first-person arms, weapon, and camera motion.
- Alejandro Potter, Respawn Entertainment, [Animation Summit: Sustained Animation Excellence in Apex Legends (GDC)](https://www.gdcvault.com/play/1027904/Animation-Summit-Sustained-Animation-Excellence). This establishes a public Respawn animation-methods reference, but does not disclose a specific weapon-rig implementation in the public session description.
- Unity, [Two Bone IK constraint (Animation Rigging)](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html). Describes target/hint control for a two-bone limb and weighted blending.
- David Therriault, [In Your Hands: First Person Animation (GDC slides)](https://media.gdcvault.com/gdc2015/presentations/Therriault_David_InYourHands.pdf). The published material illustrates multiple recoil layers including weapon mechanism/skeleton motion, body and camera motion, aiming, and two-bone IK. Use it as a general layered-animation reference, not as an Apex-specific description.
