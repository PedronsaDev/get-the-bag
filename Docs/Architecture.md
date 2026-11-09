# Code ownership for the animation release

Custom code lives under `Assets/_Project/Scripts`. Folders describe responsibility and assembly
definitions enforce dependencies. Keep the existing namespaces; some predate the folder layout.

| Folder under Scripts | Responsibility |
| --- | --- |
| Runtime/Content | Definitions, registries and immutable resolved weapon tuning |
| Runtime/Gameplay/Combat/Weapons | Weapon visual instances, authored clip playback, effects and equipment/action data |
| Runtime/Gameplay/Player/Combat | Weapon gameplay, confirmed state, view loading and presentation coordination |
| Runtime/Gameplay/Player/Locomotion | Motor-owned movement and stance state |
| Runtime/Gameplay/Player/Animation | Body parameters, character binding/visibility, body actions and procedural weapon motion |
| Runtime/Infrastructure | Existing networking, logging and persistence services |
| Runtime/Application | Existing application bootstrap and lifetime coordination |
| Editor/Weapons/Presentation | Saved character/weapon validation and authoring support |
| Tools/Animation and Tools/Blender | Developer checks/export tools outside Assets; no runtime components |

`RuntimeWeapon` owns ammunition, fire eligibility and reload capacity. `PlayerWeaponController`
owns authoritative requests and confirmed equipment/action state. Animation consumes that state;
it never awards ammunition or decides that a reload is complete.

`PlayerWeaponPresenter` coordinates the world and owner-only FP views. Each WeaponVisualBinding
owns its load handle and clone. Keep request cancellation/version checks so an old load cannot
replace newer equipment. `WeaponAnimator` handles authored weapon playback; UpperBodyPoseController
selects the body clip and hand curve from the same confirmed reload variant.

`LocomotionController` owns movement. CharacterPresentationCapture passes post-motor state to
BodyAnimationDriver. Existing NetworkAnimator replicates body locomotion; action playback uses
the existing confirmed sequence/start/duration. See [PurrNet](PurrNet.md) before networking changes.

`CharacterRigBinding` declares the body, hands, sockets and procedural rigs. CharacterVisualVisibility
sets owner/observer rendering and keeps the hidden owner's animated sockets/shadows updating.
FirstPersonWeaponMotion combines the existing ADS, sway and recoil components.

Use [the animation quickstart](Animation/QUICKSTART.md) for content changes and the
[acceptance checklist](Animation/V1-CHECKLIST.md) for this release's test status. Other gameplay,
AI, persistence and plugin cleanup from the original feature branch is outside this release.
