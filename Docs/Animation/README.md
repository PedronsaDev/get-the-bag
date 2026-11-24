# Character and weapon animation

**Animation 1.0:** start with the [daily workflow](QUICKSTART.md) and
[acceptance checklist](V1-CHECKLIST.md). The [approved release scope](V1-RELEASE-PLAN.md) takes
priority over the larger tooling roadmap below. Runtime/playback, source art, direct export and
repeatable checks are in place. Integration into main was requested on 9 November 2026;
final movement, contact and shadow approval remains a manual follow-up.

Start here. This is the current direction, updated 9 November 2026. The earlier design and
milestone plan are [historical reference](History/2026-10-workflow/README.md).

## The working agreement

Keep dedicated first-person arms/weapon animation for camera composition, plus a full world
body holding a world weapon. Keep the current combined first-person export for now. These are
runtime presentations; they do not require independently maintained copies of the weapon model.
Exact first-person/full-body arm pose matching is not a requirement of this initial slice.

Build the workflow around one weapon and one coordinated action. Start with Pistol A and its
normal reload. Add an empty reload only after the normal reload is easy to author, import,
configure and preview. Support the current arms/body family before expanding compatibility.

## Read only what you need

| Task | Guide |
| --- | --- |
| Create or update art | [Authoring](AUTHORING.md) |
| Implement the new workflow | [Implementation steps](IMPLEMENTATION.md) |
| Find current code, assets and known gaps | [Current state](CURRENT-STATE.md) |
| Inspect existing content | [Validation commands](VALIDATION.md) |
| Understand existing body movement | [Body locomotion](BODY-LOCOMOTION.md) |
| Understand loading and visibility | [Dual views](DUAL-VIEW.md), [resource ownership](WEAPON-LOADING.md) |

## Folder and filename conventions

The Unity root is `Assets/_Project/Animations`:

```text
Characters/Human/
  Locomotion/Walk, Crouch, Crawl, Jump, Sprint, Slide/
  Controllers/Human_Body.controller, Human_AI.controller
  Masks/Human_UpperBody.mask
Weapons/Pistol_A/
  Idle/
  Fire/
  Reload/Normal, Empty/
  Controllers/Pistol_A_FP.controller, Pistol_A_World.controller
Interactables/Button, BasicDoor/
```

Group a weapon's view clips by action. `FP_Pistol_A_Reload_Normal.fbx` contains the first-person
performance, `Body_Pistol_A_Reload_Normal.fbx` the world-body action, and
`WEP_Pistol_A_Reload_Normal.fbx` the weapon mechanism motion. Normal clips use `Reload/Normal`;
new Body/WEP Empty clips use `Reload/Empty`. The existing FP Empty file remains directly under
`Reload`. Character-wide movement uses names such as `Body_Human_Walk_Forward.fbx`.

Keep models under Art/Models, materials under Art/Materials and textures under Art/Textures.
Move/rename Unity assets through the Editor to preserve metadata. Imported FBX clip aliases,
Animator state names and parameter names are separate playback contracts; folder cleanup does
not require renaming them. Do not duplicate clips for compatible character skins.

## Keep these ownership boundaries

| Responsibility | Owner |
| --- | --- |
| Movement and collisions | Existing locomotion motor |
| Fire, ammunition, reload eligibility/duration/cancellation | RuntimeWeapon and PlayerWeaponController |
| Confirmed equipment and action timing | Existing WeaponEquipmentState and shared network clock |
| Load/release views safely | PlayerWeaponPresenter and WeaponVisualBinding |
| Body locomotion parameters | CharacterPresentationCapture and BodyAnimationDriver |
| Current authored weapon/body playback | WeaponAnimator and UpperBodyPoseController |
| Aim, sway, recoil and hand correction | Existing procedural components and character rigs |
| Artist configuration and checks | Existing presentation asset and Editor validation; focused tooling comes next |

Animation consumes gameplay decisions. Authoring tools prepare content; they do not introduce
another gameplay controller, ammunition counter, root-motion owner or network stream.
Read [PurrNet](../PurrNet.md) before changing networking. Current body locomotion already uses
NetworkAnimator; weapon actions already have confirmed identity and timing.

## What belongs to the animator

Author locomotion, holding poses, reload choreography, hand/prop contact, mechanism movement
and deliberate inspect actions. Use procedural motion for aim direction, ADS alignment, bounded
sway/recoil and support-hand correction while holding. Fade hand correction when authored
motion needs that hand. Terrain foot correction remains later work.

## Keep the implementation small

- One master weapon model/rig; coordinated authoring scenes may reference it in both views.
- One supported arms/body family first. Shared skeletons enable reuse; proportions still need review.
- One weapon presentation asset, organized by action. Reuse it before adding another profile type.
- One normal reload as the end-to-end example. Preserve the existing empty reload behavior.
- Keep working loading, visibility, gameplay, controllers and clips during the transition.
- Build automation for observed repetitive setup. Do not start with a universal action graph,
  generic animation framework, arbitrary rig compatibility or a replacement networking system.

The current Inspector groups existing reload fields by action. Pistol A now has a prepared
two-scene Blender file with a Pistol sidebar for Normal reload, Empty reload and Empty idle;
see [AUTHORING](AUTHORING.md). World Empty clips are imported and assigned. The new reload data
entries now drive playback and validation, with separate support-hand curves and the duplicate
fields removed. A combined Unity action preview and automatic prefab/controller setup remain
planned. See [IMPLEMENTATION](IMPLEMENTATION.md).

## Evidence and history

[The earlier design](History/2026-10-workflow/README.md),
[earlier roadmap](History/2026-10-workflow/IMPLEMENTATION.md),
[earlier authoring guide](History/2026-10-workflow/AUTHORING.md) and
[earlier state record](History/2026-10-workflow/CURRENT-STATE.md) preserve prior decisions.
[Milestone 0](MILESTONE-0.md) and [baseline captures](Baseline/) are historical evidence.
Older milestone numbers are not the active implementation sequence.

The recovered demo studies inform authored motion plus procedural correction. They do not
establish a complete multiplayer/art pipeline. Preserve their observations as evidence rather
than treating their component structure as a requirement for this project.
