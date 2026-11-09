# Animation content validation

Run these commands in Edit Mode from **Tools > Animation**. They inspect assets and return/log issues; they do not repair prefabs, change import settings, enter Play Mode or save assets. Double-click an issue in the Console to select its asset/component.

| Command | Select in the Project window | Scope |
|---|---|---|
| Validate Selected Compatibility | One CharacterVisualDefinition and one WeaponDefinition | Family compatibility and weapon asset checks |
| Validate Selected Character | One character prefab, normally CHAR_PF_Player.prefab | Rig, body, sockets, renderers, visibility and presenter bindings |
| Validate Selected Character and Weapons | One character prefab plus its supported WeaponDefinition assets | Character checks plus each explicitly selected character/weapon pair |
| Validate All Weapon Assets | Nothing required | Every WeaponDefinition under Assets/_Project; prefab checks only |

The last command does not declare every weapon compatible with every character. The debug rifle may pass asset validation while lacking a presentation profile for the current character. Select the pistol for the initial fully supported pair. A fallback without dedicated FP arms does not demonstrate support for another FP arms family.

## Results

**Errors** identify broken configuration: missing references or scripts, incompatible family IDs, invalid assets, an unregistered rig, foreign skin bones, an unlisted body mesh, competing animation components or an invalid fire/reload adapter. Resolve errors before treating a content pair as supported.

**Warnings** identify a policy or visual check that needs review. The current saved body has root motion disabled; its Animator culling still requires owner shadow-pose verification. The validator never changes that setting automatically. A zero-error result with warnings does not certify visual or multiplayer behavior.

## What is checked

The 9 November reload migration adds grouped Normal/Empty FP/world clip checks and support-hand
curve key ranges (progress/weight 0-1, finite values). Explicit character/weapon-pair validation
also checks the Normal body clip is assigned and supplied body reload clips match the body Animator
and are non-looping with positive length. Empty body motion is optional; runtime uses Normal's
clip and curve when absent. An empty hand curve means no holding correction during authored reload.

- Character profile: required rig/arms IDs, nonempty holding families, blank and duplicate entries; exact ordinal comparison of weapon requirements against supported families.
- Body: valid Humanoid Avatar, both hand bones, enabled Animator with a controller; renderer group is present, unique and complete for the character's mesh renderers.
- Ownership: sockets, aim references, renderers, skin bones and root bones belong to the character; the world socket is in the right-hand hierarchy, and the FP socket is outside the world body and independent of the world socket.
- Rigging: RigBuilder is on the body Animator, upper-body Rig belongs to its hierarchy and is registered once; aim constraints have local targets and do not target a descendant of their constrained bone.
- Presentation: one rig binding and weapon presenter, correct presenter references, enabled visibility component, LocalViewmodel layer and no embedded weapon instances in the saved character.
- Weapon assets: required world prefab, optional dedicated FP prefab, implicit Addressable folder entries, GUID catalog/build inclusion for bundled groups, root WeaponInstance and local FirePoint, missing scripts and valid skin references.
- View separation: dedicated world and FP assets cannot accidentally be the same asset; known FP procedural scripts cannot be placed in a world weapon. Leave the FP reference empty to use the supported world-only fallback.
- Attached weapon Animators cannot apply root motion, and dynamic rigidbodies cannot compete with socket attachment.
- Current WeaponAnimator adapter: local Animator/WeaponInstance, configured controller layer, Idle/Fire/Reload states with motions, Float ReloadSpeed parameter, Reload state's speed multiplier and a positive configured reload duration matching a resolved clip (including overrides).
- Animation writers: compare actual Transform targets from controller clips and Humanoid bone mappings, including enabled legacy Animation components. Multiple Animators on separate targets are allowed. Missing explicit Transform binding paths are reported.

## What still requires inspection

No static validator can identify arbitrary arm meshes by artistic intent, certify a correct grip from a family label, or prove what an opaque custom script writes at runtime. FP procedural components and asset-reference mistakes are detected; also review the actual world mesh for accidental arms.

Animator overlap checks cover imported Transform curves and Humanoid skeleton mappings. They do not certify all constraint/procedural ordering, future custom Playables, material animation or every custom OnAnimatorMove implementation. Review those writers when introducing them.

The current WeaponAnimator contract is checked; this is not validation of the future action/variant catalog. Simple clip and override durations are compared automatically; blended reload motions require a duration review. Add the future motion-set/variant validation alongside that runtime system, not as invented data in this milestone.

The earlier Milestone 1 acceptance list is retained below. The active sequence is now
[the simpler workflow](IMPLEMENTATION.md); locomotion/body reload are already implemented.

1. Review world grip, muzzle direction and absence of FP arms from a connected observer while moving and aiming in several directions.
2. Inspect locomotion root trajectories on a duplicate test setup. Then migrate the actual body to motor-owned movement without changing collision/movement behavior.
3. Verify that the shadow-only owner body continues to animate with the chosen culling setting.
4. Repeat the relevant owner/observer equipment, effects and cancellation checks after those changes.

The milestone labels above describe the earlier plan. Use CURRENT-STATE for current body,
shared action timing and support-hand implementations.

## Code entry points

All implementation is in the Editor-only Project.Weapons.Editor assembly:

- AnimationCompatibilityValidator.Validate: compatibility metadata.
- WeaponAssetValidator.Validate: weapon prefabs and current animation adapter.
- CharacterRigValidator.Validate: actual rig references; does not depend on Awake or IsBindingUsable.
- VisualHierarchyValidator.Validate / ValidateAnimationWriters: skin references, missing scripts and overlapping animation targets.
- ValidationIssue and ValidationSeverity: shared diagnostic results. AnimationValidationCommands owns the menu commands and reporting.
- AnimationValidationCommands.ValidatePresentation: explicit character prefab and supported weapons, returning a deduplicated issue collection.

Issues have Message, Asset and Level (Error or Warning). Query these methods without Console logging for previews or future UI Toolkit inspectors. The menu layer handles selection, contextual AppLog messages and error/warning totals.

## Verified checkpoint — 29 October 2026

Compilation passed. Sixty-one focused checks covered valid and broken rigs, socket/reference ownership, renderer and skin coverage, rig layers, aim feedback loops, animation writer overlap, current/overridden reload contracts, all menu commands and read-only behavior. Sixteen weapon-asset regressions covered required/optional references, invalid assets, FirePoint ownership and implicit Addressable folder inclusion. The 20 custom assembly definitions were checked for dependency cycles; none were found. The repository's optional Tools/Validate-Assemblies.ps1 was absent, so it was not run.

The saved Player plus pistol reports zero errors and two warnings: body root motion and body culling. The debug rifle passes weapon-asset validation, but its missing presentation profile still excludes it from the declared supported character/pistol pair. No runtime source, prefab, clip/import setting or scene was changed by this Editor tooling checkpoint. These checks do not replace the remaining connected-peer and visual acceptance work.
