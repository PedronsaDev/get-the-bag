# Current first-person/world weapon binding

The presenter now creates two independent views for the local owner and one for each observed character. This guide covers loading and visibility. Body locomotion/actions have separate drivers; see [current state](CURRENT-STATE.md). Earlier milestone numbers describe historical checkpoints.

| Character as seen by this client | Camera weapon | Body weapon |
|---|---|---|
| Locally owned player | FP prefab under FirstPersonSocket | World prefab under WorldWeaponSocket, shadow-only |
| Other player | No FP instance | Visible world prefab under WorldWeaponSocket |

## Responsibilities and Inspector setup

- **CharacterRigBinding:** the assigned body Animator, rig, aim references, body renderers and weapon sockets. Its existing bindings remain the source of these references.
- **CharacterVisualVisibility:** owns body/weapon visibility. It is now attached to the Player root. The presenter supplies ownership and the rig binding at spawn; this component needs no separate Inspector assignments.
- **PlayerWeaponPresenter:** has CharacterRigBinding and CharacterVisualVisibility references, both wired on the saved Player prefab. It selects equipment, owns one request version/cancellation source and holds separate world/FP bindings. Public FirstPersonVisual and WorldVisual expose the current instances for inspection.
- **WeaponVisualBinding:** still owns exactly one clone and one Addressables handle. The presenter requests an inactive clone while preparing an equip, then activates it after both required views are ready.
- **WeaponMuzzleFlash:** listens to its own WeaponInstance. Visibility suppresses the owner's world flash and enables the matching FP/observer flash.

The old presenter socket/renderer fields were removed after migrating to the already assigned rig binding. There is one source for those references. Preserve Player.prefab and script .meta files when moving assets.

The weapon definition is still the authoritative prefab configuration during this milestone. WorldPrefabReference maps to the existing serialized `_prefabReference` through FormerlySerializedAs, preserving older saved assignments after the user's field rename. The pistol keeps its dedicated FP asset; the rifle uses a separate instance of its world asset under the FP socket as the existing fallback.

## What happens when a weapon changes

```text
Equipment state changes (weapon ID, slot or equip revision)
→ cancel the previous equip request
→ remove obsolete weapon views
→ load an inactive world instance
→ for the owner, load an inactive FP instance (or fallback)
→ verify the request is still current
→ apply visibility, attach FP procedural motion, and synchronize reload state
→ adopt the bindings and activate the completed views
```

Pending bindings belong to that request until adoption. Failure or cancellation disposes every partial result. A failed current replacement leaves no obsolete rifle/pistol visible. Completion/failure from an older request cannot remove a newer request's adopted views. Ammunition, fire permission and equipment authority remain in PlayerWeaponController; a visual failure cannot change them.

Ammo/reload changes for the same equipment reuse the instances. Fire/reload notifications go to both existing views; ADS, look/movement sway and recoil update the FP view only. Disabling the presenter cancels pending work and resets FP motion. Re-enabling reuses valid views or requests missing ones. Clear/despawn destroys both instances and releases both handles.

Both views on a client receive the same locally observed reload timing. This is not authoritative cross-client phase synchronization; shared action identity/timestamps and late-join seeking remain later roadmap work. The world prefab currently has no authored weapon mechanism or full-body reload playback, so forwarding reload state alone does not animate the world magazine/arms.

## Visibility and effects

Owner body and world mesh renderers supply shadows. Non-shadow world effects are hidden from that owner: world particles are suppressed, world lights disabled and world AudioSources muted. FP renderers use LocalViewmodel and do not cast duplicate shadows; FP light masks target that layer. Observers use the normal world renderers and world muzzle flash. Do not disable the whole owner world-weapon GameObject to hide it, because that also removes its shadow.

The policy operates on runtime instances, leaving the saved weapon mesh/particle tuning intact. Audio routing beyond the per-view mute policy is not a new audio system in this change.

## Verified checkpoint

Unity compilation passed. A main host and a real MPPM client were connected in the combat gym. Each peer had two local weapon views and one world view for the other character. Checks covered:

- Correct rig sockets, visible observer world weapons, owner body/world shadow settings and FP layer/shadow settings.
- Accepted shots from both the host and the separate client. On each shooter's own client only the FP flash played; on the observer only the world flash played. Each final shot spent one round, with both equipment states agreeing.
- Shared per-client reload timing, preserved ReloadSpeed, and reuse on unchanged equipment.
- Network rifle switch using the FP fallback and return to the pistol with matching visual identity.
- Pre-cancel, current-equip failure, repeated clear, disable/re-enable, and superseded equips.
- Controlled second-view failure/cancellation/supersession after the world clone was loaded: the partial world clone stayed inactive, partial resources were released exactly once, and older requests did not remove newer views.
- Existing resource-owner checks for independent clones, handle reference counts, single-use loading and repeated disposal.

Temporary scripts/results/images are under ignored `.codex/` (`check-dual-view.cs`, `dual-view-host-result.json`, `dual-view-*-shots.json`, `dual-view-host.png`, `dual-view-client.png`). The frozen Milestone 0 evidence was not overwritten. No standalone build, latency/loss stress or alternate rig-family test was performed. Original scene/scenario and background setting were restored; no commit or staging was performed.

## Remaining Milestone 1 work

Rig/weapon compatibility data and [Editor validation](VALIDATION.md) are now implemented. Review the saved world grip in motion and validate root trajectories in a duplicate setup before disabling automatic body root motion on the actual prefab. Verify the owner's shadow-only body keeps updating with its chosen Animator culling policy. Character/body model replacement is not implemented by this checkpoint. Body gait/downed parameter driving remains Milestone 2; action variants, world reload/holding coverage and authoritative action phases follow the roadmap.
