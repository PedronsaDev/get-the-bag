# First-person weapons: current setup and authoring

Start with [the character animation plan](Animation/README.md), [the current authoring workflow](Animation/AUTHORING.md) and [the verified current state](Animation/CURRENT-STATE.md). This guide describes the working pistol, not the future profile/action framework.

## Current assets

| Part | Location or responsibility |
|---|---|
| Blender source | `C:/Projects/blender/get-the-bag/FPS/Pistol_A.blend` |
| Imported clips/controller | `Assets/_Project/Animations/Weapons/Pistol_A/` |
| Owner prefab | `Assets/_Project/Prefabs/Weapons/Pistols/WEAP_FP_Pistol_A.prefab` |
| Weapon definition | `Assets/_Project/Data/Weapons/Weapon Definitions/Pistols/WEAP_Pistol_A.asset` (`weap_pistol_a`) |
| Selection/loading | PlayerWeaponPresenter loads world views for all characters and an additional FP view for the owner |
| Authored playback | WeaponAnimator listens to WeaponInstance fire/reload events |
| Muzzle effects | WeaponMuzzleFlash listens to its view's shot event; CharacterVisualVisibility selects which effect is presented |
| Procedural orchestration | FirstPersonWeaponMotion ticks WeaponAimPose, WeaponRecoil, WeaponLookSway and WeaponMovementSway |
| ADS calibration | WeaponAimPose's AimReference marker and its custom inspector |

The pistol's World Prefab Reference points to `Assets/_Project/Prefabs/Weapons/Pistols/WEAP_PF_Pistol_A.prefab`. The owner gets the existing FP view plus a shadow-only world weapon; observers get the visible world weapon. The rifle keeps a weapon-only FP fallback with no matching authored FP arms set. See [the current dual-view setup](Animation/DUAL-VIEW.md) for ownership, visibility and verified limits.

## Author clips without breaking the rig

1. Keep arms and weapon as separate meshes. Coordinated armatures are fine: animate them together in the same Blender scene so hands, slide and magazine stay in contact. Export their evaluated motion together for the combined viewmodel.
2. Keep skeleton names, rest pose, scale, hierarchy and animated paths consistent across Idle, Fire and Reload exports. Bake constraints into exported bone animation. Constraints remain editable in Blender.
3. Keep the first-person model near a stable authoring origin and compose it through a preview camera. Do not add movement root motion to FP arms or the weapon; gameplay controls player movement.
4. Export into the existing Pistol_A animation folder when updating this weapon. Preserve Unity .meta files and prefab references. Inspect the animation binding paths if a bone stops moving.
5. Retain the current import tuning, including disabled Resample Curves that fixed the hand/weapon sliding. Preview hand contacts during idle and reload before changing import settings.
6. Assign compatible clips to the existing controller's Idle, Fire and Reload states. Keep ReloadSpeed as a float and the Reload state's speed multiplier. Set WeaponAnimator's reload clip duration to the authored source clip duration; runtime resolves playback speed from gameplay duration.

The earlier automated Blender exporter and combined-viewmodel builder described in this file are absent from the current repository. There is no current one-click builder to rely on; edit the existing prefab/controller explicitly.

## Keep transform writers separate

The current procedural stack uses AimMotionRoot, SwayRoot, MovementRoot and RecoilRoot above the animated model. Each component writes its assigned parent; the Animator writes the authored skeleton below them. Preserve the saved hierarchy and component references when replacing a mesh or clip.

- WeaponAimPose blends the hip pose toward the calibrated ADS pose. Use the AimReference inspector controls to align the sight with the camera.
- Look sway responds to look input; movement sway responds to permitted grounded movement. ADS and reload reduce these effects.
- Recoil adds cosmetic shot impulses and settles with the spring solver. It does not change gameplay aim or damage.
- WeaponAnimator starts Fire for accepted shots. Reload starts from observed gameplay timing, sets ReloadSpeed and catches up a late-loaded visual locally. Gameplay completion/cancellation returns playback to Idle.
- Disabling/releasing the presenter resets procedural offsets and releases its visual binding.

Keep authored recoil small enough to combine with procedural recoil. Do not key procedural parent roots in the imported clips. Do not add a second Animator that writes the same bones.

## What is implemented and what comes later

Idle, Fire, Reload, calibrated ADS, look/movement sway and procedural recoil are the current baseline. Equip, sprint and inspect action playback, normal/empty reload selection and weighted inspect variants are roadmap work. An exported extra clip alone does not add its gameplay selection or playback mapping.

The full body has its own locomotion/holding/torso prototype; FP clips are not full-body clips. Author future world upper-body actions against a full-body character and weapon, then use a mask at runtime. Follow the canonical authoring guide for these proposed content families.

For the current smoke check, open Scene_Gym_Combat, run a host, and exercise idle, fire, reload, ADS and switching. A separate connected observer is required to establish multiplayer animation parity. The complete dual-view system is not implemented yet.
